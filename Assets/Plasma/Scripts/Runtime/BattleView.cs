using System;
using System.Collections.Generic;
using Plasma.Sim;
using UnityEngine;

namespace Plasma
{
    /// <summary>
    /// Runs a BattleSim at a fixed step and draws it like the reference video: the deck with the
    /// squad, the conveyor of "+N" tiles on the left, the puffy upgrade gate in the dock, the red
    /// horde carpet with the boss inside, tracers, smoke, floating numbers and camera shake.
    /// Everything dynamic is GPU-instanced; all labels go through two batched WorldText meshes.
    /// Input: relative horizontal drag (or a BotPolicy).
    /// </summary>
    public class BattleView : MonoBehaviour
    {
        public BattleSim Sim { get; private set; }
        public BotPolicy Bot;                 // when set, the bot plays (attract mode / capture)
        public bool Paused;
        public event Action<SimEvent> OnEvent;
        public Camera Cam;
        public float DragSensitivity = 1.2f;

        const float Step = 1f / 60f;
        const float DrawMaxZ = 125f, LodZ = 21f, TileMaxZ = 125f;
        const float BeltTop = 0.28f;
        float _acc, _slowmo;

        InstancedBatch _soldiers, _enemies, _enemiesLod, _flames, _flameGlow, _puffs, _shadows, _tiles, _slats, _pieces, _sparks;
        WorldText _text, _overlay;
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        float[] _enemyFlash = new float[0];
        float _bossFlash, _recoil, _shake, _gainPulse, _gateFlash, _gateWobble, _gateInflateT = 9, _muzzle;
        Vector3 _camBase; Quaternion _camRot;
        static readonly Vector3 CamPos = new Vector3(-0.3f, 10.5f, -6.2f), CamLook = new Vector3(-0.3f, 0f, 7.4f);
        float CamHalfWidth = 3.9f;   // world half-width visible at the squad line (z = 1)

        // gate collapse animation (the gate that was just broken)
        int _collapseValue; float _collapseT = -1, _inflateDur = Balance.GateInflateTime;

        // conveyor upgrade waves (visual): tiles convert from the belt end outward
        struct Wave { public float T; public int From, To; }
        readonly List<Wave> _waves = new List<Wave>();
        const float WaveSpeed = 45f;

        struct Puff { public Vector3 P, V; public float T, Life, Size; public Color C; }
        readonly List<Puff> _puffList = new List<Puff>(1024);
        struct Piece { public Vector3 P, V, Spin; public float T, Life, Size; public Color C; }
        readonly List<Piece> _pieceList = new List<Piece>(256);
        struct Floater { public string S; public Vector3 P; public float T, Life, Size; public Color C; }
        readonly List<Floater> _floaters = new List<Floater>();
        struct FlyTile { public Vector3 A, B; public float T, Life; public int Value; public bool Caught; }
        readonly List<FlyTile> _flyTiles = new List<FlyTile>();
        struct Badge { public Vector3 A, B; public float T; public int Value; }
        readonly List<Badge> _badges = new List<Badge>();
        struct Arrival { public Vector3 A; public int Slot; public float T; }
        readonly List<Arrival> _arrivals = new List<Arrival>();
        float _killPuffBudget;

        // input
        bool _dragging; float _dragStartFinger, _dragStartTarget;

        public void Begin(BattleSim sim)
        {
            Sim = sim;
            _enemyFlash = new float[sim.EnemyCount];
            _puffList.Clear(); _pieceList.Clear(); _floaters.Clear(); _flyTiles.Clear(); _badges.Clear(); _arrivals.Clear(); _waves.Clear();
            _collapseT = -1; _gateInflateT = 9; _slowmo = 0;
            _acc = 0; _dragging = false;
            foreach (var e in sim.Events) Handle(e);
            sim.Events.Clear();
        }

        void Awake()
        {
            _soldiers = new InstancedBatch(MeshFactory.Soldier, Visuals.Lit, true);
            _enemies = new InstancedBatch(MeshFactory.Enemy, Visuals.Lit, true);
            _enemiesLod = new InstancedBatch(MeshFactory.EnemyLod, Visuals.Lit, true);
            _tiles = new InstancedBatch(MeshFactory.Tile, Visuals.Lit, true);
            _slats = new InstancedBatch(MeshFactory.Cube, Visuals.Lit);
            _pieces = new InstancedBatch(MeshFactory.Piece, Visuals.Lit);
            _flames = new InstancedBatch(MeshFactory.Flame, Visuals.FxSolid);
            _flameGlow = new InstancedBatch(MeshFactory.Sphere, Visuals.FxAdd);
            _sparks = new InstancedBatch(MeshFactory.Sphere, Visuals.FxAdd);
            _puffs = new InstancedBatch(MeshFactory.Sphere, Visuals.FxAlpha);
            _shadows = new InstancedBatch(MeshFactory.Shadow, Visuals.FxFlat);
            _text = new WorldText(false);
            _overlay = new WorldText(true);
            BuildEnvironment();
        }

        // ------------------------------------------------------------------ environment
        void BuildEnvironment()
        {
            var env = new GameObject("Environment").transform;
            env.SetParent(transform, false);
            var cube = MeshFactory.Cube;
            var rail = MeshFactory.RoundedRail;
            float far = Balance.DeckFarZ;
            const float back = -9f, end = 170f;

            // the deck where the squad stands
            float deckW = Balance.DeckMaxX - Balance.DeckMinX;
            float deckCx = (Balance.DeckMinX + Balance.DeckMaxX) * 0.5f;
            Visuals.Solid("Deck", cube, new Vector3(deckCx, -0.35f, (far + back) * 0.5f), new Vector3(deckW, 0.7f, far - back), Palette.Deck, env);
            Visuals.Solid("DeckUnder", cube, new Vector3(deckCx, -1.6f, (far + back) * 0.5f), new Vector3(deckW - 0.6f, 1.8f, far - back - 0.6f), Palette.DeckSide * 0.6f, env);
            // horde bridge (right) continuing far away
            float hw = Balance.HordeMaxX - Balance.HordeMinX + 0.45f;
            Visuals.Solid("HordeBridge", cube, new Vector3(Balance.HordeCenterX, -0.35f, (far + end) * 0.5f), new Vector3(hw, 0.7f, end - far), Palette.Deck * 0.97f, env);
            Visuals.Solid("HordeRailL", rail, new Vector3(Balance.HordeMinX - 0.3f, 0.12f, (far + end) * 0.5f), new Vector3(0.16f, 0.32f, end - far), Palette.Rail, env);
            Visuals.Solid("HordeRailR", rail, new Vector3(Balance.HordeMaxX + 0.3f, 0.12f, (end + back) * 0.5f), new Vector3(0.16f, 0.32f, end - back), Palette.Rail, env);
            // conveyor (left): raised dark belt with side walls
            Visuals.Solid("Belt", cube, new Vector3(Balance.ConvX, BeltTop - 0.45f, (back + end) * 0.5f), new Vector3(Balance.ConvMaxX - Balance.ConvMinX, 0.9f, end - back), Palette.Belt, env);
            Visuals.Solid("BeltWallL", rail, new Vector3(Balance.ConvMinX - 0.08f, BeltTop + 0.05f, (back + end) * 0.5f), new Vector3(0.16f, 0.36f, end - back), Palette.RailDark, env);
            Visuals.Solid("BeltWallR", rail, new Vector3(Balance.ConvMaxX + 0.08f, BeltTop + 0.05f, (far + end) * 0.5f), new Vector3(0.16f, 0.36f, end - far), Palette.RailDark, env);
            Visuals.Solid("BeltUnder", cube, new Vector3(Balance.ConvX, -1.2f, (back + end) * 0.5f), new Vector3(Balance.ConvMaxX - Balance.ConvMinX - 0.3f, 1.6f, end - back), Palette.Belt * 0.5f, env);
            // dock: dark pit at the far edge + metal frame
            float dw = Balance.DockMaxX - Balance.DockMinX + 0.4f;
            Visuals.Solid("DockPit", cube, new Vector3(Balance.DockX, -0.01f, Balance.DockZ + 0.1f), new Vector3(dw, 0.04f, 1.7f), Palette.Slot, env);
            Visuals.Solid("DockBack", rail, new Vector3(Balance.DockX, 0.22f, Balance.DockZ + 0.95f), new Vector3(dw + 0.3f, 0.45f, 0.18f), Palette.Rail, env);
            Visuals.Solid("DockPostL", rail, new Vector3(Balance.DockMinX - 0.28f, 0.22f, Balance.DockZ + 0.1f), new Vector3(0.16f, 0.45f, 1.85f), Palette.Rail, env);
            Visuals.Solid("DockPostR", rail, new Vector3(Balance.DockMaxX + 0.28f, 0.22f, Balance.DockZ + 0.1f), new Vector3(0.16f, 0.45f, 1.85f), Palette.Rail, env);
            Visuals.Solid("DockLip", cube, new Vector3(Balance.DockX, 0.03f, Balance.DockZ - 0.78f), new Vector3(dw, 0.06f, 0.14f), Palette.RailDark, env);
            // deck far edge rail between dock and horde lane
            float gapMin = Balance.DockMaxX + 0.36f, gapMax = Balance.HordeMinX - 0.3f;
            if (gapMax > gapMin) Visuals.Solid("FarRail", rail, new Vector3((gapMin + gapMax) * 0.5f, 0.12f, far - 0.08f), new Vector3(gapMax - gapMin, 0.32f, 0.16f), Palette.Rail, env);
            // deck side lips
            Visuals.Solid("DeckLipL", cube, new Vector3(Balance.DeckMinX + 0.04f, 0.03f, (far + back) * 0.5f), new Vector3(0.1f, 0.06f, far - back), Palette.Rail, env);
            // faint "sea" far below for depth
            Visuals.Solid("Sea", cube, new Vector3(0, -9f, 60f), new Vector3(300f, 0.1f, 300f), Palette.Background * 1.15f, env);
        }

        // ------------------------------------------------------------------ camera
        public void SetupCamera(Camera cam)
        {
            Cam = cam;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            cam.nearClipPlane = 0.5f; cam.farClipPlane = 260f;
            // Framing matched to the reference ad: close, steep, the squad big at the bottom.
            // Override for tuning: -plasmaCam "px,py,pz,lx,ly,lz,halfWidth"
            Vector3 look = CamLook;
            _camBase = CamPos;
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == "-plasmaCam")
                {
                    var f = Array.ConvertAll(a[i + 1].Split(','), x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture));
                    _camBase = new Vector3(f[0], f[1], f[2]); look = new Vector3(f[3], f[4], f[5]); if (f.Length > 6) CamHalfWidth = f[6];
                }
            _camRot = Quaternion.LookRotation(look - _camBase);
            cam.transform.SetPositionAndRotation(_camBase, _camRot);
            FitFov();
        }

        void FitFov()
        {
            if (Cam == null) return;
            float dist = Vector3.Distance(_camBase, new Vector3(_camBase.x, 0, 1.0f));
            float halfW = CamHalfWidth;
            float hFov = 2 * Mathf.Atan(halfW / dist);
            float vFov = 2 * Mathf.Atan(Mathf.Tan(hFov / 2) / Mathf.Max(0.3f, Cam.aspect)) * Mathf.Rad2Deg;
            Cam.fieldOfView = Mathf.Clamp(vFov, 40f, 80f);
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (Sim == null) return;
            FitFov();
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (!Paused)
            {
                HandleInput();
                float simDt = _slowmo > 0 ? dt * 0.35f : dt;
                _slowmo = Mathf.Max(0, _slowmo - dt);
                _acc += simDt;
                while (_acc >= Step)
                {
                    _acc -= Step;
                    if (Bot != null) Bot.Update(Sim, Step);
                    Sim.Step(Step);
                    foreach (var e in Sim.Events) Handle(e);
                    Sim.Events.Clear();
                }
                UpdateEffects(simDt, dt);
            }
            Draw();
        }

        void HandleInput()
        {
            if (Bot != null || Cam == null) return;
            bool down = Input.GetMouseButton(0);
            float fx = Input.mousePosition.x;
            if (Input.touchCount > 0) { down = true; fx = Input.GetTouch(0).position.x; }
            if (down && !_dragging) { _dragging = true; _dragStartFinger = fx; _dragStartTarget = Sim.SquadX; }
            if (!down) _dragging = false;
            if (_dragging)
            {
                float worldPerPixel = 9.5f / Mathf.Max(1, Screen.width) * DragSensitivity;
                Sim.TargetX = Mathf.Clamp(_dragStartTarget + (fx - _dragStartFinger) * worldPerPixel, Balance.SquadMinX, Balance.SquadMaxX);
            }
        }

        static float R(float a, float b) => UnityEngine.Random.Range(a, b);

        void Handle(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Volley: _recoil = 1f; _muzzle = 1f; break;
                case SimEventType.EnemyKilled:
                    if (_killPuffBudget >= 1f || e.Value == 1)
                    {
                        _killPuffBudget = Mathf.Max(0, _killPuffBudget - 1f);
                        AddPuff(new Vector3(e.X + R(-0.1f, 0.1f), 0.35f, e.Z), e.Value == 1 ? 1.0f : R(0.55f, 0.85f), Palette.Smoke, 0.7f);
                    }
                    break;
                case SimEventType.EnemyHit:
                    if (e.Index >= 0 && e.Index < _enemyFlash.Length) _enemyFlash[e.Index] = 1f;
                    break;
                case SimEventType.EnemyReachedSquad:
                    AddPuff(new Vector3(e.X, 0.3f, Balance.DefenseZ), 0.8f, new Color(1f, 0.4f, 0.35f, 0.9f), 0.5f);
                    break;
                case SimEventType.SoldiersLost: _shake = Mathf.Max(_shake, 0.1f); break;
                case SimEventType.GateHit:
                    _gateFlash = 1f; _gateWobble = Mathf.Max(_gateWobble, 0.5f);
                    if (UnityEngine.Random.value < 0.35f) AddSpark(new Vector3(e.X, R(0.3f, 1.1f), Balance.DockZ - 0.5f));
                    break;
                case SimEventType.GateSpawned: _gateInflateT = _collapseT >= 0 ? -0.4f : 0f; _inflateDur = Balance.GateInflateTime + Mathf.Min(0, _gateInflateT); break;   // wait for the old cloth to sink
                case SimEventType.GateBroken:
                {
                    _collapseValue = e.Value; _collapseT = 0;
                    var c = Palette.Gate(e.Value);
                    for (int i = 0; i < 18; i++)
                        _pieceList.Add(new Piece { P = new Vector3(Balance.DockX + R(-1f, 1f), R(0.3f, 1.2f), Balance.DockZ + R(-0.3f, 0.3f)), V = new Vector3(R(-3f, 3f), R(3f, 7f), R(-4f, 1f)), Spin = new Vector3(R(-600, 600), R(-600, 600), 0), Life = R(0.7f, 1.1f), Size = R(0.18f, 0.34f), C = c });
                    for (int i = 0; i < 8; i++) AddPuff(new Vector3(Balance.DockX + R(-1f, 1f), 0.6f, Balance.DockZ), 0.9f, new Color(c.r, c.g, c.b, 0.7f), 0.5f);
                    _badges.Add(new Badge { A = new Vector3(Balance.DockX, 1.6f, Balance.DockZ - 0.3f), B = new Vector3(Balance.ConvX, 1.4f, 5f), T = 0, Value = e.Value });
                    _shake = Mathf.Max(_shake, 0.15f);
                    break;
                }
                case SimEventType.ConveyorUpgraded:
                {
                    int from = _waves.Count > 0 ? _waves[_waves.Count - 1].To : 0;
                    _waves.Add(new Wave { T = -0.45f, From = from, To = e.Value }); // starts when the badge lands
                    if (_waves.Count > 4) _waves.RemoveAt(0);
                    break;
                }
                case SimEventType.TileCaught:
                {
                    var a = new Vector3(Balance.ConvX, BeltTop, Balance.ConvEndZ);
                    var b = new Vector3(Sim.SquadX, 0.4f, 0.2f);
                    _flyTiles.Add(new FlyTile { A = a, B = b, T = 0, Life = 0.22f, Value = e.Value, Caught = true });
                    if (e.Value > 0)
                    {
                        Float("+" + e.Value, new Vector3(Sim.SquadX - Sim.SquadRadius * 0.55f, 1.0f, Sim.SquadRadius * 0.4f + 0.9f), Palette.Gold, 0.6f);
                        int have = Mathf.Min(Sim.Soldiers, Balance.SoldierVisualCap);
                        int n = Mathf.Min(e.Value, 6);
                        for (int i = 0; i < n && _arrivals.Count < 24; i++)
                            _arrivals.Add(new Arrival { A = a + new Vector3(R(0, 0.5f), 0, R(-0.3f, 0.3f)), Slot = Mathf.Max(0, have - 1 - i), T = -i * 0.03f });
                    }
                    break;
                }
                case SimEventType.TileMissed:
                    _flyTiles.Add(new FlyTile { A = new Vector3(Balance.ConvX, BeltTop, Balance.ConvEndZ), B = new Vector3(Balance.ConvX, -3.5f, Balance.ConvEndZ - 1.6f), T = 0, Life = 0.55f, Value = e.Value, Caught = false });
                    break;
                case SimEventType.SoldiersGained: _gainPulse = 1f; break;
                case SimEventType.BossHit: _bossFlash = 1f; break;
                case SimEventType.BossKilled:
                    for (int i = 0; i < 36; i++) AddPuff(new Vector3(e.X + R(-1.2f, 1.2f), R(0.2f, 3f), e.Z + R(-1f, 1f)), R(1.0f, 1.8f), Palette.Smoke, 0.9f);
                    for (int i = 0; i < 14; i++) AddSpark(new Vector3(e.X + R(-1f, 1f), R(0.5f, 2.5f), e.Z));
                    _shake = 0.5f; _slowmo = 0.7f;
                    break;
                case SimEventType.BossBite: _shake = Mathf.Max(_shake, 0.25f); break;
            }
            OnEvent?.Invoke(e);
        }

        void AddPuff(Vector3 p, float size, Color c, float life)
        {
            if (_puffList.Count > 700) return;
            _puffList.Add(new Puff { P = p, T = 0, Life = life, Size = size * 0.6f, C = c, V = new Vector3(R(-0.4f, 0.4f), R(0.8f, 1.6f), R(-0.1f, 0.5f)) });
        }

        void AddSpark(Vector3 p)
        {
            if (_pieceList.Count > 240) return;
            _pieceList.Add(new Piece { P = p, V = new Vector3(R(-2f, 2f), R(1f, 4f), R(-3f, 0f)), Life = 0.25f, Size = R(0.12f, 0.25f), C = new Color(1f, 0.8f, 0.3f, 1f), Spin = Vector3.zero });
        }

        void Float(string text, Vector3 p, Color c, float size)
        {
            if (_floaters.Count > 14) _floaters.RemoveAt(0);
            _floaters.Add(new Floater { S = text, T = 0, Life = 0.8f, P = p, C = c, Size = size });
        }

        void UpdateEffects(float dt, float realDt)
        {
            _killPuffBudget = Mathf.Min(6f, _killPuffBudget + realDt * 45f);
            for (int i = 0; i < _enemyFlash.Length; i++) if (_enemyFlash[i] > 0) _enemyFlash[i] -= dt * 7;
            _bossFlash = Mathf.Max(0, _bossFlash - dt * 7);
            _gateFlash = Mathf.Max(0, _gateFlash - dt * 8);
            _gateWobble = Mathf.Max(0, _gateWobble - dt * 3);
            _gateInflateT += dt;
            _recoil = Mathf.Max(0, _recoil - dt * 10);
            _muzzle = Mathf.Max(0, _muzzle - dt * 14);
            _shake = Mathf.Max(0, _shake - dt * 1.5f);
            _gainPulse = Mathf.Max(0, _gainPulse - dt * 4f);
            if (_collapseT >= 0) { _collapseT += dt; if (_collapseT > 0.7f) _collapseT = -1; }
            for (int i = 0; i < _waves.Count; i++) { var w = _waves[i]; w.T += dt; _waves[i] = w; }

            for (int i = _puffList.Count - 1; i >= 0; i--)
            {
                var p = _puffList[i]; p.T += dt; p.P += p.V * dt; p.V *= 1 - dt * 2.5f;
                if (p.T >= p.Life) { _puffList.RemoveAt(i); continue; }
                _puffList[i] = p;
            }
            for (int i = _pieceList.Count - 1; i >= 0; i--)
            {
                var p = _pieceList[i]; p.T += dt; p.V += Vector3.down * 16f * dt; p.P += p.V * dt;
                if (p.T >= p.Life) { _pieceList.RemoveAt(i); continue; }
                _pieceList[i] = p;
            }
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                var f = _floaters[i]; f.T += realDt;
                if (f.T > f.Life) { _floaters.RemoveAt(i); continue; }
                _floaters[i] = f;
            }
            for (int i = _flyTiles.Count - 1; i >= 0; i--)
            {
                var f = _flyTiles[i]; f.T += dt;
                if (f.T > f.Life) { _flyTiles.RemoveAt(i); continue; }
                _flyTiles[i] = f;
            }
            for (int i = _badges.Count - 1; i >= 0; i--)
            {
                var b = _badges[i]; b.T += dt;
                if (b.T > 0.75f) { _badges.RemoveAt(i); continue; }
                _badges[i] = b;
            }
            for (int i = _arrivals.Count - 1; i >= 0; i--)
            {
                var a = _arrivals[i]; a.T += dt;
                if (a.T > 0.32f) { _arrivals.RemoveAt(i); continue; }
                _arrivals[i] = a;
            }
            if (Cam != null)
            {
                var off = _shake > 0 ? new Vector3(R(-1f, 1f), R(-1f, 1f), 0) * _shake * 0.3f : Vector3.zero;
                Cam.transform.SetPositionAndRotation(_camBase + off, _camRot);
            }
        }

        /// <summary>Value a tile at distance d from the belt end currently shows (upgrade waves travel outward).</summary>
        int TileValue(float d, out float flash)
        {
            flash = 0;
            if (_waves.Count == 0) return Sim.ConveyorValue;
            for (int i = _waves.Count - 1; i >= 0; i--)
            {
                var w = _waves[i];
                float front = w.T * WaveSpeed;
                if (front >= d)
                {
                    float since = (front - d) / WaveSpeed;
                    if (since < 0.15f) flash = 1 - since / 0.15f;
                    return w.To;
                }
                if (i == 0) return w.From;
            }
            return Sim.ConveyorValue;
        }

        // ------------------------------------------------------------------ drawing
        void Draw()
        {
            var sim = Sim;
            float time = Time.time;
            var camRot = Cam != null ? Cam.transform.rotation : Quaternion.identity;
            Quaternion faceUs = Quaternion.Euler(0, 180, 0);

            // ---- squad ----
            int n = Mathf.Min(sim.Soldiers, Balance.SoldierVisualCap);
            float squadScale = 1.5f;
            for (int i = 0; i < n; i++)
            {
                BattleSim.FormationOffset(i, out float ox, out float oz);
                float hop = 0;
                bool arriving = false;
                foreach (var a in _arrivals) if (a.Slot == i) { arriving = true; break; }
                if (arriving) continue;
                var pos = new Vector3(sim.SquadX + ox, hop, Balance.SquadZ + oz - _recoil * 0.025f);
                float glow = i >= n - 6 ? _gainPulse * 0.6f : 0;
                _soldiers.Add(Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(squadScale, squadScale * 1.35f, squadScale)), Palette.Squad, glow);
                _shadows.Add(Matrix4x4.TRS(pos + new Vector3(0.1f, 0.012f, -0.06f), Quaternion.identity, new Vector3(0.5f, 1, 0.42f)), new Color(0, 0, 0.05f, 0.4f));
            }
            foreach (var a in _arrivals)
            {
                if (a.T < 0) continue;
                float k = Mathf.Clamp01(a.T / 0.32f);
                BattleSim.FormationOffset(Mathf.Min(a.Slot, Mathf.Max(0, n - 1)), out float ox, out float oz);
                var target = new Vector3(sim.SquadX + ox, 0, oz);
                var p = Vector3.Lerp(a.A, target, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
                _soldiers.Add(Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * squadScale), Palette.Squad, 0.5f * (1 - k));
                _sparks.Add(Matrix4x4.TRS(p + Vector3.up * 0.3f, Quaternion.identity, Vector3.one * 0.5f), new Color(1f, 0.75f, 0.2f, 0.45f * (1 - k)));
            }
            _soldiers.Flush();

            // ---- horde ----
            for (int i = 0; i < sim.EnemyCount; i++)
            {
                if (!sim.EnemyAlive[i]) continue;
                float z = sim.EnemyZ(i) + ((i * 7919) % 17 - 8) * 0.009f;   // visual jitter: a carpet, not a grid
                if (z > DrawMaxZ) continue;
                bool brute = sim.EnemyBrute[i];
                float s = brute ? 1.55f : 1.18f;
                float fl = i < _enemyFlash.Length ? Mathf.Max(0, _enemyFlash[i]) * 0.7f : 0;
                if (z < LodZ)
                {
                    float bob = Mathf.Abs(Mathf.Sin(time * 7 + i * 0.37f)) * 0.035f;
                    _enemies.Add(Matrix4x4.TRS(new Vector3(sim.EnemyX[i], bob, z), faceUs, new Vector3(s, s, s)), brute ? Palette.Brute : Palette.Enemy, fl);
                }
                else _enemiesLod.Add(Matrix4x4.TRS(new Vector3(sim.EnemyX[i], 0, z), faceUs, new Vector3(s, s, s)), brute ? Palette.Brute : Palette.Enemy, fl);
            }
            _enemies.Flush();
            _enemiesLod.Flush();

            // ---- boss ----
            if (sim.BossAlive && sim.BossZ < DrawMaxZ)
            {
                float bs = sim.Spec.BigBoss ? 3.8f : 3.0f;
                float walk = sim.BossZ > sim.BossStopZ + 0.01f ? 1 : 0.3f;
                float stomp = Mathf.Abs(Mathf.Sin(time * 4)) * 0.08f * walk;
                var rot = faceUs * Quaternion.Euler(0, Mathf.Sin(time * 4) * 6 * walk, Mathf.Sin(time * 8) * 2 * walk);
                var bp = new Vector3(sim.BossX, stomp, sim.BossZ);
                _mpb.Clear(); _mpb.SetColor("_Color", Palette.BossJacket); _mpb.SetFloat("_Flash", _bossFlash * 0.75f);
                Graphics.DrawMesh(MeshFactory.Boss, Matrix4x4.TRS(bp, rot, Vector3.one * bs), Visuals.Lit, 0, null, 0, _mpb);
                _shadows.Add(Matrix4x4.TRS(new Vector3(sim.BossX, 0.015f, sim.BossZ), Quaternion.identity, new Vector3(bs * 0.8f, 1, bs * 0.55f)), new Color(0, 0, 0.05f, 0.4f));
                if (sim.BossRevealed)
                {
                    var top = new Vector3(sim.BossX, bs * 1.25f + 0.25f, sim.BossZ);
                    float f = Mathf.Clamp01(sim.BossHp / Mathf.Max(1, sim.BossMaxHp));
                    float bw = sim.Spec.BigBoss ? 2.4f : 1.9f;
                    DrawFlat(top, camRot, new Vector2(bw + 0.12f, 0.3f), new Color(0.08f, 0.08f, 0.1f, 0.95f));
                    DrawFlat(top + camRot * new Vector3(-(bw * 0.5f) * (1 - f), 0, -0.01f), camRot, new Vector2(bw * f, 0.2f), new Color(0.95f, 0.14f, 0.12f, 1f));
                    _overlay.Add(Mathf.CeilToInt(Mathf.Max(0, sim.BossHp)).ToString(), top + camRot * new Vector3(0, 0.42f, 0), camRot, 0.42f, Color.white, Palette.Outline, 0.12f);
                }
            }

            // ---- conveyor: moving slats + tiles ----
            var tileRot = Quaternion.Euler(16, 0, 0);
            for (int k = 0; ; k++)
            {
                float z = Balance.ConvEndZ + sim.ConvOffset + (k - 0.5f) * Balance.ConvSpacing;
                if (z > TileMaxZ) break;
                if (z < -6f) continue;
                _slats.Add(Matrix4x4.TRS(new Vector3(Balance.ConvX, BeltTop + 0.005f, z), Quaternion.identity, new Vector3(Balance.ConvMaxX - Balance.ConvMinX - 0.1f, 0.02f, 0.08f)), Palette.RailDark * 0.8f);
            }
            _slats.Flush();
            for (int k = 0; ; k++)
            {
                float z = Balance.ConvEndZ + sim.ConvOffset + k * Balance.ConvSpacing;
                if (z > TileMaxZ) break;
                int v = TileValue(z - Balance.ConvEndZ, out float flash);
                var tp = new Vector3(Balance.ConvX, BeltTop, z);
                float pop = 1 + flash * 0.18f;
                _tiles.Add(Matrix4x4.TRS(tp, tileRot, new Vector3(1.62f * pop, 1.12f * pop, 0.24f)), Palette.Gate(v), flash * 0.8f);
                if (z < 40f) _shadows.Add(Matrix4x4.TRS(new Vector3(tp.x + 0.12f, BeltTop + 0.01f, z - 0.45f), Quaternion.identity, new Vector3(1.6f, 1, 0.75f)), new Color(0, 0, 0.05f, 0.45f));
                if (z < 60f) _text.Add("+" + v, tp + tileRot * new Vector3(0, 0.56f, -0.14f), tileRot, 0.56f, Color.white, Palette.Outline, 0.09f);
            }
            foreach (var f in _flyTiles)
            {
                float k = f.T / f.Life;
                Vector3 p; Quaternion r; float s;
                if (f.Caught) { p = Vector3.Lerp(f.A, f.B, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f; r = tileRot; s = 1 - k * 0.7f; }
                else { p = new Vector3(f.A.x, f.A.y - 6f * k * k, f.A.z - 1.6f * k); r = tileRot * Quaternion.Euler(-160 * k, 0, 0); s = 1; }
                _tiles.Add(Matrix4x4.TRS(p, r, new Vector3(1.62f * s, 1.12f * s, 0.24f * s)), Palette.Gate(f.Value), f.Caught ? 0.4f : 0);
            }
            _tiles.Flush();

            // ---- dock gate ----
            if (sim.HasGate && _gateInflateT >= 0)
            {
                var g = sim.CurrentGate;
                float t = _gateInflateT / _inflateDur;
                float inflate = t >= 1 ? 1 : EaseOutBack(Mathf.Clamp01(t));
                float wob = Mathf.Sin(time * 50) * _gateWobble * 0.05f;
                float sy = Mathf.Lerp(0.12f, 1f, inflate) - wob, sx = Mathf.Lerp(1.12f, 1f, inflate) + wob;
                var gp = new Vector3(Balance.DockX, 0, Balance.DockZ);
                var gs = new Vector3(2.45f * sx, 1.55f * sy, 1.05f);
                _mpb.Clear(); _mpb.SetColor("_Color", Palette.Gate(g.Tier)); _mpb.SetFloat("_Flash", _gateFlash * 0.45f);
                if (t < 0.35f)   // still a cloth heap that starts to puff up
                {
                    float k = t / 0.35f;
                    Graphics.DrawMesh(MeshFactory.Cloth, Matrix4x4.TRS(gp, Quaternion.identity, new Vector3(2.5f, 1f + 2.2f * k * k, 1.15f)), Visuals.Lit, 0, null, 0, _mpb);
                }
                else Graphics.DrawMesh(MeshFactory.Pillow, Matrix4x4.TRS(gp, Quaternion.identity, gs), Visuals.Lit, 0, null, 0, _mpb);
                _shadows.Add(Matrix4x4.TRS(gp + new Vector3(0.15f, 0.03f, -0.35f), Quaternion.identity, new Vector3(gs.x * 1.05f, 1, 1.3f)), new Color(0, 0, 0.05f, 0.5f * inflate));
                if (inflate > 0.5f)
                {
                    var labelRot = Quaternion.Euler(8, 0, 0);
                    _text.Add("+" + g.Value, gp + new Vector3(0, 0.8f * sy, -0.74f), labelRot, 0.95f * sy, Color.white, Palette.Outline, 0.09f);
                }
                if (sim.GateAvailable)
                {
                    float f = Mathf.Clamp01(sim.GateHp / Mathf.Max(1, g.Hp));
                    var hb = gp + new Vector3(0, 0.03f, -1.0f);
                    var flat = Quaternion.Euler(90, 0, 0);
                    DrawFlat(hb, flat, new Vector2(2.0f, 0.16f), new Color(0.05f, 0.06f, 0.1f, 0.9f));
                    DrawFlat(hb + new Vector3(-1.0f * (1 - f), 0.005f, 0), flat, new Vector2(1.94f * f, 0.1f), new Color(1, 1, 1, 0.95f));
                }
            }
            if (_collapseT >= 0)
            {
                // burst: the cushion squashes, then lies as a wrinkled cloth that sinks into the slot
                var gp = new Vector3(Balance.DockX, 0, Balance.DockZ);
                _mpb.Clear(); _mpb.SetColor("_Color", Palette.Gate(_collapseValue)); _mpb.SetFloat("_Flash", Mathf.Max(0, 0.7f - _collapseT * 3f));
                if (_collapseT < 0.15f)
                {
                    float k = _collapseT / 0.15f;
                    Graphics.DrawMesh(MeshFactory.Pillow, Matrix4x4.TRS(gp, Quaternion.identity, new Vector3(2.45f * (1 + 0.2f * k), 1.55f * (1 - 0.75f * k), 1.05f * (1 + 0.3f * k))), Visuals.Lit, 0, null, 0, _mpb);
                }
                else
                {
                    float k = (_collapseT - 0.15f) / 0.55f;
                    Graphics.DrawMesh(MeshFactory.Cloth, Matrix4x4.TRS(gp + new Vector3(0, -0.35f * k * k, 0.1f), Quaternion.identity, new Vector3(2.9f, 1.6f * (1 - 0.5f * k), 1.35f)), Visuals.Lit, 0, null, 0, _mpb);
                }
            }
            foreach (var b in _badges)
            {
                float k = Mathf.Clamp01(b.T / 0.6f);
                float e = k * k * (3 - 2 * k);
                var p = Vector3.Lerp(b.A, b.B, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 1.5f;
                float s = (b.T < 0.12f ? b.T / 0.12f : 1f) * (1.7f - 0.5f * k);
                float alpha = b.T > 0.6f ? 1 - (b.T - 0.6f) / 0.15f : 1;
                var c = Palette.Gate(b.Value);
                _mpb.Clear(); _mpb.SetColor("_Color", new Color(c.r, c.g, c.b, 0.55f * alpha));
                Graphics.DrawMesh(MeshFactory.Sphere, Matrix4x4.TRS(p, camRot, Vector3.one * s * 0.95f), Visuals.FxAdd, 0, null, 0, _mpb);
                _mpb.Clear(); _mpb.SetColor("_Color", new Color(0.7f, 0.95f, 1f, alpha));
                Graphics.DrawMesh(MeshFactory.Ring, Matrix4x4.TRS(p, camRot, Vector3.one * s), Visuals.FxFlat, 0, null, 0, _mpb);
                _overlay.Add("+" + b.Value, p, camRot, 0.4f * s, new Color(1, 1, 1, alpha), Palette.Outline, 0.1f);
            }

            // ---- tracers ----
            foreach (var b in sim.Bullets)
            {
                var bp = new Vector3(b.X, 0.36f, b.Z);
                _flames.Add(Matrix4x4.TRS(bp, Quaternion.identity, new Vector3(0.13f, 0.13f, 1.8f)), Color.white);
                _flameGlow.Add(Matrix4x4.TRS(bp + new Vector3(0, 0, 0.1f), Quaternion.identity, new Vector3(0.24f, 0.2f, 1.7f)), new Color(1f, 0.5f, 0.08f, 0.3f));
            }
            if (_muzzle > 0 && n > 0)
            {
                float r = sim.SquadRadius;
                for (int i = 0; i < 3; i++)
                    _flameGlow.Add(Matrix4x4.TRS(new Vector3(sim.SquadX + (i - 1) * r * 0.5f, 0.38f, r * 0.6f + 0.35f), Quaternion.identity, Vector3.one * 0.45f * _muzzle), new Color(1f, 0.7f, 0.2f, 0.6f * _muzzle));
            }
            _flameGlow.Flush();
            _flames.Flush();

            // ---- pieces, sparks, smoke ----
            foreach (var p in _pieceList)
            {
                float k = p.T / p.Life;
                if (p.Spin == Vector3.zero) _sparks.Add(Matrix4x4.TRS(p.P, Quaternion.identity, Vector3.one * p.Size * (1 - k)), new Color(p.C.r, p.C.g, p.C.b, 1 - k));
                else _pieces.Add(Matrix4x4.TRS(p.P, Quaternion.Euler(p.Spin * p.T), Vector3.one * p.Size * (1 - k * 0.6f)), p.C);
            }
            _pieces.Flush();
            _sparks.Flush();
            _shadows.Flush();
            foreach (var p in _puffList)
            {
                float k = p.T / p.Life;
                float s = p.Size * (0.55f + k * 1.0f);
                var c = p.C; c.a *= 1 - k * k;
                _puffs.Add(Matrix4x4.TRS(p.P, Quaternion.identity, Vector3.one * s), c);
            }
            _puffs.Flush();

            // ---- labels ----
            if (n > 0)
            {
                var lp = new Vector3(sim.SquadX, 0.95f + sim.SquadRadius * 0.25f, Balance.SquadZ + sim.SquadRadius * 0.55f + 0.2f);
                float pulse = 1 + _gainPulse * 0.2f;
                _overlay.Add(sim.Soldiers.ToString(), lp, camRot, 0.5f * pulse, Color.white, Palette.Outline, 0.11f);
            }
            foreach (var f in _floaters)
            {
                float k = f.T / f.Life;
                float s = f.Size * (k < 0.15f ? Mathf.Lerp(1.6f, 1f, k / 0.15f) : 1f);
                var c = f.C; c.a = 1 - Mathf.Clamp01((k - 0.6f) / 0.4f);
                _overlay.Add(f.S, f.P + Vector3.up * k * 1.4f, camRot, s, c, Palette.Outline, 0.12f);
            }
            _text.Flush();
            _overlay.Flush();
        }

        void DrawFlat(Vector3 p, Quaternion rot, Vector2 size, Color c)
        {
            _mpb.Clear(); _mpb.SetColor("_Color", c);
            Graphics.DrawMesh(MeshFactory.Cube, Matrix4x4.TRS(p, rot, new Vector3(size.x, size.y, 0.01f)), Visuals.FxFlat, 0, null, 0, _mpb);
        }

        static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }
    }
}

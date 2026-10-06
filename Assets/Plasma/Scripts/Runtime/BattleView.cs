using System;
using System.Collections.Generic;
using Plasma.Sim;
using UnityEngine;

namespace Plasma
{
    /// <summary>
    /// Runs a BattleSim at a fixed step and draws it: instanced soldiers/enemies/bullets/smoke,
    /// gate blocks, boss, labels and camera shake. Input: horizontal drag (or a BotPolicy).
    /// </summary>
    public class BattleView : MonoBehaviour
    {
        public BattleSim Sim { get; private set; }
        public BotPolicy Bot;                 // when set, the bot plays (attract mode / capture)
        public bool Paused;
        public event Action<SimEvent> OnEvent;
        public Camera Cam;
        public float DragSensitivity = 1.15f;

        const float Step = 1f / 60f;
        const float DrawMaxZ = 38f;
        float _acc;

        InstancedBatch _soldiers, _enemies, _bullets, _bulletGlow, _puffs, _boss;
        readonly List<GateView> _gates = new List<GateView>();
        readonly List<GateView> _collapsing = new List<GateView>();
        TextMesh _squadLabel, _squadShadow;
        Transform _bossBar, _bossBarFill; TextMesh _bossLabel;
        float[] _enemyFlash = new float[0];
        float _bossFlash, _recoil, _shake, _gainPulse;
        float _gateSlideAnim;
        Vector3 _camBase; Quaternion _camRot;

        struct Puff { public Vector3 P; public float T, Life, Size; public Color C; public Vector3 V; }
        readonly List<Puff> _puffList = new List<Puff>(1024);

        struct Floater { public TextMesh Tm; public float T; public Vector3 P; }
        readonly List<Floater> _floaters = new List<Floater>();
        readonly Queue<TextMesh> _floaterPool = new Queue<TextMesh>();

        // input
        bool _dragging; float _dragStartFinger, _dragStartTarget;

        public void Begin(BattleSim sim)
        {
            Sim = sim;
            _enemyFlash = new float[sim.EnemyCount];
            _puffList.Clear();
            foreach (var g in _collapsing) g.gameObject.SetActive(false);
            _collapsing.Clear();
            _acc = 0; _dragging = false;
        }

        void Awake()
        {
            _soldiers = new InstancedBatch(MeshFactory.Soldier, Visuals.Lit, true);
            _enemies = new InstancedBatch(MeshFactory.Enemy, Visuals.Lit, true);
            _boss = new InstancedBatch(MeshFactory.Boss, Visuals.Lit, true);
            _bullets = new InstancedBatch(MeshFactory.Bullet, Visuals.FxAlpha);
            _bulletGlow = new InstancedBatch(MeshFactory.Bullet, Visuals.FxAdd);
            _puffs = new InstancedBatch(MeshFactory.Sphere, Visuals.FxAlpha);
            BuildEnvironment();
            for (int i = 0; i < 14; i++) { var g = GateView.Create(transform); g.gameObject.SetActive(false); _gates.Add(g); }
            _squadShadow = Visuals.Label("SquadShadow", transform, Vector3.zero, 0.75f, new Color(0, 0, 0, 0.45f));
            _squadLabel = Visuals.Label("SquadCount", transform, Vector3.zero, 0.75f, Color.white);
            _bossBar = Visuals.Solid("BossBar", MeshFactory.Cube, Vector3.zero, new Vector3(2.6f, 0.28f, 0.05f), new Color(0.12f, 0.12f, 0.14f), transform).transform;
            _bossBarFill = Visuals.Solid("BossBarFill", MeshFactory.Cube, Vector3.zero, new Vector3(2.5f, 0.2f, 0.05f), new Color(0.95f, 0.15f, 0.12f), _bossBar).transform;
            _bossBarFill.SetParent(transform, true);
            _bossLabel = Visuals.Label("BossHp", transform, Vector3.zero, 0.7f, Color.white);
        }

        // ------------------------------------------------------------------ environment
        void BuildEnvironment()
        {
            var env = new GameObject("Environment").transform;
            env.SetParent(transform, false);
            var cube = MeshFactory.Cube;
            float slot = Balance.GateSlotZ;
            // water far below
            Visuals.Solid("Water", cube, new Vector3(0, -4f, 60), new Vector3(200, 0.1f, 260), Palette.Water, env);
            // main platform where the squad moves
            Visuals.Solid("Platform", cube, new Vector3(0.25f, -0.25f, (slot - 8f) * 0.5f), new Vector3(9.7f, 0.5f, slot + 8f), Palette.Deck, env);
            // horde bridge (right)
            Visuals.Solid("Bridge", cube, new Vector3(Balance.HordeCenterX + 0.05f, -0.25f, slot + 70f), new Vector3(Balance.HordeMaxX - Balance.HordeMinX + 0.5f, 0.5f, 140f), Palette.Deck * 0.97f, env);
            // gate conveyor (left)
            Visuals.Solid("Conveyor", cube, new Vector3(Balance.GateCenterX, -0.2f, slot + 40f), new Vector3(Balance.GateMaxX - Balance.GateMinX + 0.3f, 0.4f, 80f), Palette.Rail * 0.9f, env);
            for (int i = 0; i < 40; i++) // conveyor slats
                Visuals.Solid("Slat", cube, new Vector3(Balance.GateCenterX, 0.01f, slot + 1.2f + i * 1.5f), new Vector3(Balance.GateMaxX - Balance.GateMinX, 0.03f, 0.12f), Palette.Rail * 0.7f, env);
            // rails
            Color rail = Palette.Rail;
            Visuals.Solid("RailL", cube, new Vector3(Balance.GateMinX - 0.3f, 0.25f, 50f), new Vector3(0.18f, 0.5f, 120f), rail, env);
            Visuals.Solid("RailR", cube, new Vector3(Balance.HordeMaxX + 0.35f, 0.25f, 50f), new Vector3(0.18f, 0.5f, 120f), rail, env);
            Visuals.Solid("RailMidA", cube, new Vector3(Balance.GateMaxX + 0.12f, 0.25f, slot + 40f), new Vector3(0.14f, 0.5f, 80f), rail, env);
            Visuals.Solid("RailMidB", cube, new Vector3(Balance.HordeMinX - 0.22f, 0.25f, slot + 40f), new Vector3(0.14f, 0.5f, 80f), rail, env);
            // gate slot frame (dark recess like the reference)
            Visuals.Solid("Slot", cube, new Vector3(Balance.GateCenterX, -0.02f, slot), new Vector3(Balance.GateMaxX - Balance.GateMinX + 0.2f, 0.06f, 1.6f), Palette.Slot, env);
            Visuals.Solid("SlotLip", cube, new Vector3(Balance.GateCenterX, 0.12f, slot - 0.85f), new Vector3(Balance.GateMaxX - Balance.GateMinX + 0.3f, 0.24f, 0.12f), rail, env);
            // platform edge lines
            Visuals.Solid("EdgeL", cube, new Vector3(-4.75f, 0.1f, (slot - 8f) * 0.5f), new Vector3(0.12f, 0.2f, slot + 8f), rail, env);
            Visuals.Solid("EdgeR", cube, new Vector3(5.15f, 0.1f, (slot - 8f) * 0.5f), new Vector3(0.12f, 0.2f, slot + 8f), rail, env);
        }

        // ------------------------------------------------------------------ camera
        public void SetupCamera(Camera cam)
        {
            Cam = cam;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Water;
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 200f;
            _camBase = new Vector3(0.3f, 17f, -6.2f);
            _camRot = Quaternion.LookRotation(new Vector3(0.3f, 0, 12f) - _camBase);
            cam.transform.SetPositionAndRotation(_camBase, _camRot);
            FitFov();
        }

        void FitFov()
        {
            if (Cam == null) return;
            float dist = Vector3.Distance(_camBase, new Vector3(0.3f, 0, 1.5f));
            float halfW = 5.35f;
            float hFov = 2 * Mathf.Atan(halfW / dist);
            float vFov = 2 * Mathf.Atan(Mathf.Tan(hFov / 2) / Mathf.Max(0.3f, Cam.aspect)) * Mathf.Rad2Deg;
            Cam.fieldOfView = Mathf.Clamp(vFov, 45f, 82f);
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
                _acc += dt;
                while (_acc >= Step)
                {
                    _acc -= Step;
                    if (Bot != null) Bot.Update(Sim, Step);
                    Sim.Step(Step);
                    foreach (var e in Sim.Events) Handle(e);
                    Sim.Events.Clear();
                }
                UpdateEffects(dt);
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
                float worldPerPixel = 10.5f / Mathf.Max(1, Screen.width) * DragSensitivity;
                Sim.TargetX = Mathf.Clamp(_dragStartTarget + (fx - _dragStartFinger) * worldPerPixel, Balance.SquadMinX, Balance.SquadMaxX);
            }
        }

        void Handle(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Volley: _recoil = 1f; break;
                case SimEventType.EnemyKilled:
                    AddPuff(new Vector3(e.X, 0.35f, e.Z), e.Value == 1 ? 0.9f : 0.6f, Palette.Smoke);
                    break;
                case SimEventType.EnemyHit:
                    if (e.Index >= 0 && e.Index < _enemyFlash.Length) _enemyFlash[e.Index] = 1f;
                    break;
                case SimEventType.EnemyReachedSquad:
                    AddPuff(new Vector3(e.X, 0.3f, Balance.DefenseZ), 0.8f, new Color(1f, 0.35f, 0.3f, 0.9f));
                    break;
                case SimEventType.SoldiersLost: _shake = Mathf.Max(_shake, 0.12f); break;
                case SimEventType.GateHit:
                    if (_gates.Count > 0) _gates[0].Hit();
                    break;
                case SimEventType.GateBroken:
                    StartCollapse(e.Index);
                    _gateSlideAnim = 1f;
                    for (int i = 0; i < 10; i++) AddPuff(new Vector3(Balance.GateCenterX + UnityEngine.Random.Range(-1f, 1f), 0.5f, Balance.GateSlotZ + UnityEngine.Random.Range(-0.4f, 0.4f)), 0.7f, Palette.Gate(LevelGenerator.TierOf(e.Value)) * new Color(1, 1, 1, 0.8f));
                    break;
                case SimEventType.SoldiersGained:
                    _gainPulse = 1f;
                    Float("+" + e.Value, new Vector3(Sim.SquadX, 1.4f, 0.4f), Palette.Gold);
                    break;
                case SimEventType.BossHit: _bossFlash = 1f; break;
                case SimEventType.BossKilled:
                    for (int i = 0; i < 30; i++) AddPuff(new Vector3(e.X + UnityEngine.Random.Range(-1.2f, 1.2f), UnityEngine.Random.Range(0.2f, 2.5f), e.Z + UnityEngine.Random.Range(-1f, 1f)), 1.4f, Palette.Smoke);
                    _shake = 0.5f;
                    break;
                case SimEventType.BossBite: _shake = Mathf.Max(_shake, 0.25f); break;
            }
            OnEvent?.Invoke(e);
        }

        void StartCollapse(int gateIndex)
        {
            if (gateIndex < 0 || gateIndex >= Sim.Gates.Count) return;
            GateView g = null;
            foreach (var c in _collapsing) if (!c.gameObject.activeSelf) { g = c; break; }
            if (g == null) { g = GateView.Create(transform); _collapsing.Add(g); }
            g.transform.position = new Vector3(Balance.GateCenterX, 0, Balance.GateSlotZ);
            g.Show(Sim.Gates[gateIndex], false, 0);
            g.Collapse = 0;
        }

        void AddPuff(Vector3 p, float size, Color c)
        {
            if (_puffList.Count > 900) return;
            _puffList.Add(new Puff { P = p, T = 0, Life = 0.45f, Size = size * 0.55f, C = c, V = new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), 1.2f, UnityEngine.Random.Range(-0.2f, 0.6f)) });
        }

        void Float(string text, Vector3 p, Color c)
        {
            var tm = _floaterPool.Count > 0 ? _floaterPool.Dequeue() : Visuals.Label("Float", transform, Vector3.zero, 0.9f, c);
            tm.gameObject.SetActive(true); tm.text = text; tm.color = c;
            tm.transform.position = p;
            _floaters.Add(new Floater { Tm = tm, T = 0, P = p });
        }

        void UpdateEffects(float dt)
        {
            for (int i = 0; i < _enemyFlash.Length; i++) if (_enemyFlash[i] > 0) _enemyFlash[i] -= dt * 6;
            _bossFlash = Mathf.Max(0, _bossFlash - dt * 6);
            _recoil = Mathf.Max(0, _recoil - dt * 10);
            _shake = Mathf.Max(0, _shake - dt * 1.5f);
            _gainPulse = Mathf.Max(0, _gainPulse - dt * 2.5f);
            _gateSlideAnim = Mathf.Max(0, _gateSlideAnim - dt / Balance.GateSlideTime);
            for (int i = _puffList.Count - 1; i >= 0; i--)
            {
                var p = _puffList[i]; p.T += dt; p.P += p.V * dt;
                if (p.T >= p.Life) { _puffList.RemoveAt(i); continue; }
                _puffList[i] = p;
            }
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                var f = _floaters[i]; f.T += dt;
                f.Tm.transform.position = f.P + Vector3.up * f.T * 1.6f;
                f.Tm.transform.rotation = Cam != null ? Cam.transform.rotation : Quaternion.identity;
                var c = f.Tm.color; c.a = 1 - Mathf.Clamp01((f.T - 0.5f) / 0.4f); f.Tm.color = c;
                if (f.T > 0.9f) { f.Tm.gameObject.SetActive(false); _floaterPool.Enqueue(f.Tm); _floaters.RemoveAt(i); continue; }
                _floaters[i] = f;
            }
            if (Cam != null)
            {
                var off = _shake > 0 ? new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0) * _shake * 0.35f : Vector3.zero;
                Cam.transform.SetPositionAndRotation(_camBase + off, _camRot);
            }
        }

        // ------------------------------------------------------------------ drawing
        void Draw()
        {
            var sim = Sim;
            float time = Time.time;
            Quaternion faceAway = Quaternion.identity, faceUs = Quaternion.Euler(0, 180, 0);

            // soldiers
            int n = Mathf.Min(sim.Soldiers, Balance.SoldierVisualCap);
            float squadScale = 1.25f * (1 + _gainPulse * 0.15f);
            for (int i = 0; i < n; i++)
            {
                BattleSim.FormationOffset(i, out float ox, out float oz);
                float bob = Mathf.Abs(Mathf.Sin(time * 9 + i * 1.7f)) * 0.03f;
                var pos = new Vector3(sim.SquadX + ox, bob, Balance.SquadZ + oz - _recoil * 0.03f);
                _soldiers.Add(Matrix4x4.TRS(pos, faceAway, Vector3.one * squadScale), Palette.Squad, _gainPulse * 0.4f);
            }
            _soldiers.Flush();

            // enemies (only those near enough to be visible)
            for (int i = 0; i < sim.EnemyCount; i++)
            {
                if (!sim.EnemyAlive[i]) continue;
                float z = sim.EnemyZ(i);
                if (z > DrawMaxZ) continue;
                bool brute = sim.EnemyBrute[i];
                float bob = Mathf.Abs(Mathf.Sin(time * 8 + i * 0.37f)) * 0.04f;
                float s = brute ? 1.75f : 1.3f;
                float fl = i < _enemyFlash.Length ? Mathf.Max(0, _enemyFlash[i]) : 0;
                _enemies.Add(Matrix4x4.TRS(new Vector3(sim.EnemyX[i], bob, z), faceUs, new Vector3(s, s, s)), brute ? Palette.Brute : Palette.Enemy, fl * 0.7f);
            }
            _enemies.Flush();

            // boss
            bool bossVisible = sim.BossAlive;
            _bossBar.gameObject.SetActive(bossVisible); _bossBarFill.gameObject.SetActive(bossVisible); _bossLabel.gameObject.SetActive(bossVisible);
            if (bossVisible)
            {
                float bs = sim.Spec.BigBoss ? 5.2f : 4f;
                float stomp = Mathf.Abs(Mathf.Sin(time * 4)) * 0.08f;
                _boss.Add(Matrix4x4.TRS(new Vector3(sim.BossX, stomp, sim.BossZ), faceUs, Vector3.one * bs), Palette.BossJacket, _bossFlash * 0.6f);
                _boss.Flush();
                var top = new Vector3(sim.BossX, bs * 1.15f + 0.4f, sim.BossZ);
                var rot = Cam != null ? Cam.transform.rotation : Quaternion.identity;
                _bossBar.SetPositionAndRotation(top, rot);
                float f = Mathf.Clamp01(sim.BossHp / Mathf.Max(1, sim.BossMaxHp));
                _bossBarFill.SetPositionAndRotation(top + rot * new Vector3(-1.25f * (1 - f), 0, -0.03f), rot);
                _bossBarFill.localScale = new Vector3(2.5f * f, 0.2f, 0.05f);
                _bossLabel.transform.SetPositionAndRotation(top + rot * new Vector3(0, 0.45f, -0.05f), rot);
                _bossLabel.text = Mathf.CeilToInt(Mathf.Max(0, sim.BossHp)).ToString();
            }

            // bullets
            foreach (var b in sim.Bullets)
            {
                var bp = new Vector3(b.X, 0.34f, b.Z);
                _bulletGlow.Add(Matrix4x4.TRS(bp, Quaternion.identity, new Vector3(0.32f, 0.32f, 1.25f)), new Color(1f, 0.45f, 0.05f, 0.55f));
                _bullets.Add(Matrix4x4.TRS(bp, Quaternion.identity, new Vector3(0.12f, 0.12f, 0.9f)), Palette.Bullet);
            }
            _bulletGlow.Flush();
            _bullets.Flush();

            // smoke puffs
            foreach (var p in _puffList)
            {
                float k = p.T / p.Life;
                float s = p.Size * (0.6f + k * 0.9f);
                var c = p.C; c.a *= 1 - k * k;
                _puffs.Add(Matrix4x4.TRS(p.P, Quaternion.identity, Vector3.one * s), c);
            }
            _puffs.Flush();

            // gates: front gate in the slot, the rest queued up the conveyor
            float slide = _gateSlideAnim * Balance.GateQueueSpacing;
            for (int k = 0; k < _gates.Count; k++)
            {
                int gi = sim.GateIndex + k;
                var gv = _gates[k];
                if (gi >= sim.Gates.Count) { gv.gameObject.SetActive(false); continue; }
                var g = sim.Gates[gi];
                gv.transform.position = new Vector3(Balance.GateCenterX, 0, Balance.GateSlotZ + k * Balance.GateQueueSpacing + slide);
                gv.Show(g, k == 0 && sim.GateSlide <= 0, k == 0 ? sim.GateHp / Mathf.Max(1, g.Hp) : 1);
            }

            // squad label
            var camRot = Cam != null ? Cam.transform.rotation : Quaternion.identity;
            var lp = new Vector3(sim.SquadX, 1.1f + sim.SquadRadius * 0.4f, Balance.SquadZ + 0.3f);
            _squadLabel.text = _squadShadow.text = sim.Soldiers.ToString();
            _squadLabel.transform.SetPositionAndRotation(lp, camRot);
            _squadShadow.transform.SetPositionAndRotation(lp + camRot * new Vector3(0.04f, -0.04f, 0.02f), camRot);
        }
    }
}

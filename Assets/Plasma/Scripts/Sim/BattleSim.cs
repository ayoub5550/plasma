using System;
using System.Collections.Generic;

namespace Plasma.Sim
{
    public enum SimState { Running, Won, Lost }

    public struct Bullet { public float X, Z, Dmg; }

    /// <summary>
    /// The whole battle as a deterministic, engine-free simulation. The Unity layer only feeds
    /// input (TargetX) and draws the state; the balance sweep runs it thousands of times headless.
    ///
    /// Mechanic (as in the reference video):
    ///  * the squad auto-fires straight ahead; the player only slides it left/right;
    ///  * a conveyor of "+N" tiles slides toward the player on the left; standing next to the
    ///    belt's end collects each arriving tile (+N soldiers);
    ///  * an upgrade gate inflates in the dock; shooting it down upgrades every tile on the belt
    ///    to the gate's value (+1 -> +5 -> ... -> +99; the belt starts at Balance.StartTileValue);
    ///  * a red horde marches down the right lane with the boss walking inside it; every enemy
    ///    that reaches the squad kills soldiers; the boss stops at the squad and keeps eating.
    /// </summary>
    public class BattleSim
    {
        // ---- configuration ----
        public LevelSpec Spec { get; private set; }
        public readonly RunModifiers Mods;
        public readonly bool Endless;
        public int Wave { get; private set; } = 1;

        // ---- squad ----
        public int Soldiers;
        public int MaxSoldiers;
        public float SquadX;
        public float TargetX;
        public float Time;
        public int Kills;
        public SimState State = SimState.Running;

        // ---- horde ----
        public int EnemyCount;
        public int AliveEnemies;
        public float[] EnemyX = new float[0];
        public int[] EnemyRow = new int[0];
        public int[] EnemyCol = new int[0];
        public float[] EnemyHp = new float[0];
        public bool[] EnemyAlive = new bool[0];
        public bool[] EnemyBrute = new bool[0];
        public float HordeTraveled;
        readonly float[] _colLag = new float[Balance.HordeColumns];   // rows behind a stopped boss lag behind
        readonly bool[] _bossCol = new bool[Balance.HordeColumns];
        int[][] _col;
        int[] _colFront;
        float _hordeHpLeft, _hordeHpTotal;

        // ---- conveyor ----
        public int ConveyorValue;        // value of every tile on the belt
        public float ConvOffset = Balance.ConvSpacing; // distance of the next tile from the belt end
        public int TileSerial;           // number of tiles that reached the end so far (tile k has id TileSerial + k)
        public int TilesCaught;

        // ---- dock gates ----
        public readonly List<GateSpec> Gates = new List<GateSpec>();
        public int GateIndex;
        public float GateHp;
        public float GateInflate;        // >0 while the current gate is inflating (not shootable)

        // ---- boss ----
        public bool BossAlive, BossRevealed;
        public float BossHp, BossMaxHp, BossZ, BossX, BossRadius;
        public int BossRow;
        bool _bossStopped;
        float _biteAcc;

        // ---- bullets / events ----
        public readonly List<Bullet> Bullets = new List<Bullet>(256);
        public readonly List<SimEvent> Events = new List<SimEvent>(256);
        float _fireTimer;
        int _volley;
        static float[] _fx, _fz;

        public BattleSim(LevelSpec spec, RunModifiers mods, bool endless = false)
        {
            Mods = mods; Endless = endless;
            Soldiers = MaxSoldiers = Math.Max(1, mods.StartSoldiers);
            ConveyorValue = Balance.StartTileValue;   // gates <= this value are skipped by StartWave
            SquadX = TargetX = 0.6f;
            BuildFormation();
            StartWave(spec);
        }

        // ------------------------------------------------------------------ setup
        static void BuildFormation()
        {
            if (_fx != null) return;
            int n = Balance.SoldierVisualCap;
            _fx = new float[n]; _fz = new float[n];
            double golden = Math.PI * (3 - Math.Sqrt(5));
            for (int i = 0; i < n; i++)
            {
                double r = Balance.FormationSpacing * Math.Sqrt(i + 0.5) * 1.15;
                double a = i * golden;
                _fx[i] = (float)(r * Math.Cos(a)); _fz[i] = (float)(r * Math.Sin(a) * 0.85);
            }
        }

        /// <summary>Formation offset of soldier i (relative to the squad centre).</summary>
        public static void FormationOffset(int i, out float x, out float z)
        {
            BuildFormation();
            i %= _fx.Length; x = _fx[i]; z = _fz[i];
        }

        public float SquadRadius => Balance.FormationSpacing * 1.15f * (float)Math.Sqrt(Math.Min(Soldiers, Balance.SoldierVisualCap)) + 0.25f;

        void StartWave(LevelSpec spec)
        {
            Spec = spec;
            var rng = new Random(spec.Seed);
            int cols = Balance.HordeColumns;
            int rows = spec.Rows;

            // boss position inside the horde
            BossRadius = spec.BigBoss ? Balance.BigBossRadius : Balance.BossRadius;
            BossRow = Math.Max(2, (int)(rows * spec.BossDepth));
            BossX = Balance.HordeCenterX + (float)(rng.NextDouble() - 0.5) * 0.8f;
            BossZ = Balance.HordeStartZ + BossRow * Balance.HordeRowSpacing;
            for (int c = 0; c < cols; c++)
            {
                float cx = Balance.HordeMinX + (c + 0.5f) * Balance.HordeColSpacing;
                _bossCol[c] = Math.Abs(cx - BossX) < BossRadius + 0.1f;
                _colLag[c] = 0;
            }
            int clearRows = (int)Math.Ceiling(BossRadius / Balance.HordeRowSpacing);

            var xs = new List<float>(); var rs = new List<int>(); var cs = new List<int>(); var br = new List<bool>();
            for (int row = 0; row < rows; row++)
                for (int c = 0; c < cols; c++)
                {
                    if (_bossCol[c] && Math.Abs(row - BossRow) <= clearRows) continue; // room for the boss
                    if (xs.Count >= spec.EnemyCount) break;
                    xs.Add(Balance.HordeMinX + (c + 0.5f) * Balance.HordeColSpacing + (float)(rng.NextDouble() - 0.5) * 0.16f);
                    rs.Add(row); cs.Add(c); br.Add(row > 3 && rng.NextDouble() < spec.BruteFraction);
                }
            int n = xs.Count;
            EnemyCount = AliveEnemies = n;
            EnemyX = xs.ToArray(); EnemyRow = rs.ToArray(); EnemyCol = cs.ToArray(); EnemyBrute = br.ToArray();
            EnemyHp = new float[n]; EnemyAlive = new bool[n];
            var lists = new List<int>[cols];
            for (int c = 0; c < cols; c++) lists[c] = new List<int>();
            _hordeHpTotal = 0;
            for (int i = 0; i < n; i++)
            {
                EnemyHp[i] = (EnemyBrute[i] ? Balance.BruteHp : Balance.GruntHp) * spec.EnemyHpScale;
                _hordeHpTotal += EnemyHp[i];
                EnemyAlive[i] = true;
                lists[EnemyCol[i]].Add(i);
            }
            _hordeHpLeft = _hordeHpTotal;
            _col = new int[cols][]; _colFront = new int[cols];
            for (int c = 0; c < cols; c++) _col[c] = lists[c].ToArray();
            HordeTraveled = 0;

            // gates: append this wave's ladder (endless keeps only gates that are an upgrade)
            int best = ConveyorValue;
            if (GateIndex < Gates.Count) best = Math.Max(best, Gates[Gates.Count - 1].Value);
            bool wasEmpty = GateIndex >= Gates.Count;
            foreach (var g in spec.Gates) if (g.Value > best) { Gates.Add(g); best = g.Value; }
            if (wasEmpty && GateIndex < Gates.Count) SpawnGate();

            BossAlive = true; BossRevealed = false; _bossStopped = false;
            BossHp = BossMaxHp = Math.Max(1, spec.BossHp); _biteAcc = 0;
            Events.Add(new SimEvent(SimEventType.WaveStarted, 0, 0, Wave));
        }

        void SpawnGate()
        {
            GateHp = Gates[GateIndex].Hp;
            GateInflate = Balance.GateInflateTime;
            Events.Add(new SimEvent(SimEventType.GateSpawned, Balance.DockX, Balance.DockZ, Gates[GateIndex].Value, GateIndex));
        }

        // ------------------------------------------------------------------ queries
        public float EnemyZ(int i)
        {
            float z = Balance.HordeStartZ + EnemyRow[i] * Balance.HordeRowSpacing - HordeTraveled;
            if (EnemyRow[i] > BossRow) z += _colLag[EnemyCol[i]];
            return z;
        }

        public bool HasGate => GateIndex < Gates.Count;
        public bool GateAvailable => GateIndex < Gates.Count && GateInflate <= 0;
        public GateSpec CurrentGate => Gates[GateIndex];

        /// <summary>Front-most alive enemy in a column, or -1.</summary>
        public int ColumnFront(int c)
        {
            var arr = _col[c];
            int k = _colFront[c];
            while (k < arr.Length && !EnemyAlive[arr[k]]) k++;
            _colFront[c] = k;
            return k < arr.Length ? arr[k] : -1;
        }

        /// <summary>Z of the closest alive enemy (any column), or +inf.</summary>
        public float FrontZ()
        {
            float best = float.PositiveInfinity;
            for (int c = 0; c < Balance.HordeColumns; c++)
            {
                int e = ColumnFront(c);
                if (e >= 0) { float z = EnemyZ(e); if (z < best) best = z; }
            }
            return best;
        }

        public float HordeHpLeft => Math.Max(0, _hordeHpLeft);
        public float SquadDps => Soldiers * Balance.BaseDamage * Mods.DamageMult * Mods.FireRateMult / Balance.FireInterval;
        public float BossStopZ => Balance.DefenseZ + BossRadius * 0.8f;
        public bool SquadAtBelt => SquadX - SquadRadius <= Balance.ConvMaxX + Balance.CatchReach;
        /// <summary>Squad x at which the belt end is reachable (collect position).</summary>
        public float BeltX => Math.Max(Balance.SquadMinX, Balance.ConvMaxX + Balance.CatchReach + SquadRadius - 0.15f);
        public int TileGain => GainFor(ConveyorValue);
        /// <summary>Soldiers a tile of base value v gives (Tile-bonus upgrade included). The view prints THIS number on
        /// tiles, gates and badges, so what the player reads is exactly what the squad receives.</summary>
        public int GainFor(int v) => v <= 0 ? 0 : Math.Max(1, (int)Math.Round(v * Mods.GateMult));

        /// <summary>Progress 0..1 of the current level (horde + boss HP).</summary>
        public float Progress
        {
            get
            {
                float total = _hordeHpTotal + BossMaxHp;
                float left = Math.Max(0, _hordeHpLeft) + (BossAlive ? Math.Max(0, BossHp) : 0);
                return total <= 0 ? 1 : 1f - left / total;
            }
        }

        // ------------------------------------------------------------------ step
        public void Step(float dt)
        {
            if (State != SimState.Running) return;
            Time += dt;

            // squad movement
            float tx = Math.Max(Balance.SquadMinX, Math.Min(Balance.SquadMaxX, TargetX));
            float maxMove = Balance.SquadMoveSpeed * dt;
            float d = tx - SquadX;
            SquadX += Math.Abs(d) <= maxMove ? d : Math.Sign(d) * maxMove;

            if (GateInflate > 0) GateInflate -= dt;

            StepConveyor(dt);
            StepHorde(dt);
            StepBoss(dt);
            if (State != SimState.Running) return;

            // shooting
            _fireTimer -= dt;
            float interval = Balance.FireInterval / Mods.FireRateMult;
            while (_fireTimer <= 0) { _fireTimer += interval; FireVolley(); }

            float bdz = Balance.BulletSpeed * dt;
            for (int i = Bullets.Count - 1; i >= 0; i--)
            {
                var b = Bullets[i];
                b.Z += bdz;
                if (ResolveBullet(ref b) || b.Z > Balance.BulletMaxZ) { Bullets.RemoveAt(i); continue; }
                Bullets[i] = b;
            }

            // win / next wave
            if (AliveEnemies == 0 && !BossAlive)
            {
                if (Endless) { Wave++; StartWave(LevelGenerator.Create(Wave)); }
                else
                {
                    State = SimState.Won;
                    Events.Add(new SimEvent(SimEventType.Won, SquadX, 0, Soldiers));
                }
            }
        }

        void StepConveyor(float dt)
        {
            ConvOffset -= Balance.ConvSpeed * dt;
            while (ConvOffset <= 0)
            {
                ConvOffset += Balance.ConvSpacing;
                if (SquadAtBelt)
                {
                    TilesCaught++;
                    int gain = TileGain;
                    Events.Add(new SimEvent(SimEventType.TileCaught, Balance.ConvX, Balance.ConvEndZ, gain, TileSerial));
                    if (gain > 0) GainSoldiers(gain);
                }
                else Events.Add(new SimEvent(SimEventType.TileMissed, Balance.ConvX, Balance.ConvEndZ, ConveyorValue, TileSerial));
                TileSerial++;
            }
        }

        /// <summary>
        /// Marching speed: the level's speed, but the horde (and boss) rush in while nothing is
        /// near the deck, so there is no dead time once the front rows are cleared.
        /// </summary>
        public float MarchSpeed
        {
            get
            {
                float front = AliveEnemies > 0 ? FrontZ() : float.PositiveInfinity;
                if (BossAlive && !_bossStopped) front = Math.Min(front, BossZ);
                float boost = front > 8.5f ? Math.Min(3.5f, 1f + (front - 8.5f) * 0.3f) : 1f;
                return Spec.HordeSpeed * boost;
            }
        }
        float _march;

        void StepHorde(float dt)
        {
            _march = MarchSpeed;
            if (AliveEnemies <= 0) return;
            float adv = _march * dt;
            HordeTraveled += adv;
            if (BossAlive && _bossStopped)
                for (int c = 0; c < Balance.HordeColumns; c++) if (_bossCol[c]) _colLag[c] += adv;
            for (int c = 0; c < Balance.HordeColumns; c++)
            {
                int e;
                while ((e = ColumnFront(c)) >= 0 && EnemyZ(e) <= Balance.DefenseZ)
                {
                    KillEnemy(e, false);
                    int bite = EnemyBrute[e] ? Balance.BruteBite : Balance.GruntBite;
                    Events.Add(new SimEvent(SimEventType.EnemyReachedSquad, EnemyX[e], Balance.DefenseZ, bite, e));
                    LoseSoldiers(bite, EnemyX[e]);
                    if (State != SimState.Running) return;
                }
            }
        }

        void StepBoss(float dt)
        {
            if (!BossAlive) return;
            if (!_bossStopped)
            {
                BossZ -= _march * dt;
                if (BossZ <= BossStopZ) { BossZ = BossStopZ; _bossStopped = true; }
            }
            if (!BossRevealed && BossZ < 16f)
            {
                BossRevealed = true;
                Events.Add(new SimEvent(SimEventType.BossRevealed, BossX, BossZ, (int)BossMaxHp));
            }
            if (_bossStopped)
            {
                _biteAcc += Spec.BossBiteRate * dt;
                int n = (int)_biteAcc;
                if (n > 0) { _biteAcc -= n; Events.Add(new SimEvent(SimEventType.BossBite, BossX, BossZ, n)); LoseSoldiers(n, BossX); }
            }
        }

        /// <summary>One volley = up to MaxBulletsPerVolley parallel streams across the squad's front (straight lines like the reference).</summary>
        void FireVolley()
        {
            if (Soldiers <= 0) return;
            int nb = Math.Min(Soldiers, Balance.MaxBulletsPerVolley);
            float dmg = Soldiers * Balance.BaseDamage * Mods.DamageMult / nb;
            float half = Math.Max(0f, SquadRadius - 0.25f);
            float phase = nb > 1 ? ((_volley % 3) - 1) * (half / (nb - 1)) * 0.67f : 0f; // sweep between streams so no column survives
            float front = Balance.SquadZ + SquadRadius * 0.7f + 0.2f;
            for (int k = 0; k < nb; k++)
            {
                float u = nb == 1 ? 0f : (float)k / (nb - 1) * 2f - 1f;
                float jitter = (((_volley * 7 + k * 13) % 5) - 2) * 0.03f;
                Bullets.Add(new Bullet { X = SquadX + u * half + phase + jitter, Z = front, Dmg = dmg });
            }
            _volley++;
            Events.Add(new SimEvent(SimEventType.Volley, SquadX, 0, nb));
        }

        /// <summary>Applies the bullet's damage. Returns true if the bullet is spent.</summary>
        bool ResolveBullet(ref Bullet b)
        {
            // dock lane: the upgrade gate
            if (b.X >= Balance.DockMinX && b.X <= Balance.DockMaxX)
            {
                if (HasGate && b.Z >= Balance.DockZ - 0.35f)
                {
                    if (GateInflate > 0) return true; // the inflating gate soaks it
                    GateHp -= b.Dmg;
                    Events.Add(new SimEvent(SimEventType.GateHit, b.X, Balance.DockZ, 0, GateIndex));
                    if (GateHp <= 0) BreakGate();
                    return true;
                }
                return false;
            }
            // horde lane (with the boss inside it)
            if (b.X >= Balance.HordeMinX && b.X < Balance.HordeMaxX)
            {
                int c = (int)((b.X - Balance.HordeMinX) / Balance.HordeColSpacing);
                if (c < 0) c = 0; if (c >= Balance.HordeColumns) c = Balance.HordeColumns - 1;
                bool bossLane = BossAlive && Math.Abs(b.X - BossX) < BossRadius;
                while (b.Dmg > 0.0001f)
                {
                    int e = ColumnFront(c);
                    float ez = e >= 0 ? EnemyZ(e) : float.PositiveInfinity;
                    if (bossLane && b.Z >= BossZ - 0.45f && ez > BossZ - 0.2f) { HitBoss(ref b); return true; }
                    if (e < 0 || ez > b.Z + 0.15f) break;
                    if (b.Dmg >= EnemyHp[e]) { b.Dmg -= EnemyHp[e]; _hordeHpLeft -= EnemyHp[e]; KillEnemy(e, true); }
                    else { EnemyHp[e] -= b.Dmg; _hordeHpLeft -= b.Dmg; b.Dmg = 0; Events.Add(new SimEvent(SimEventType.EnemyHit, EnemyX[e], ez, 0, e)); }
                }
                return b.Dmg <= 0.0001f;
            }
            return false;
        }

        void HitBoss(ref Bullet b)
        {
            BossHp -= b.Dmg; b.Dmg = 0;
            Events.Add(new SimEvent(SimEventType.BossHit, b.X, BossZ, (int)Math.Ceiling(Math.Max(0, BossHp))));
            if (BossHp <= 0)
            {
                BossAlive = false; Kills++;
                Events.Add(new SimEvent(SimEventType.BossKilled, BossX, BossZ));
            }
        }

        void BreakGate()
        {
            var g = Gates[GateIndex];
            ConveyorValue = g.Value;
            Events.Add(new SimEvent(SimEventType.GateBroken, Balance.DockX, Balance.DockZ, g.Value, GateIndex));
            Events.Add(new SimEvent(SimEventType.ConveyorUpgraded, Balance.ConvX, Balance.ConvEndZ, g.Value, TileSerial));
            GateIndex++;
            if (GateIndex < Gates.Count) SpawnGate();
        }

        void GainSoldiers(int n)
        {
            Soldiers += n; MaxSoldiers = Math.Max(MaxSoldiers, Soldiers);
            Events.Add(new SimEvent(SimEventType.SoldiersGained, SquadX, 0, n));
        }

        void KillEnemy(int e, bool byPlayer)
        {
            if (!byPlayer) _hordeHpLeft -= EnemyHp[e];
            EnemyAlive[e] = false; AliveEnemies--;
            if (byPlayer) { Kills++; Events.Add(new SimEvent(SimEventType.EnemyKilled, EnemyX[e], EnemyZ(e), EnemyBrute[e] ? 1 : 0, e)); }
        }

        void LoseSoldiers(int n, float x)
        {
            if (State != SimState.Running) return;
            Soldiers -= n;
            Events.Add(new SimEvent(SimEventType.SoldiersLost, x, 0, n));
            if (Soldiers <= 0)
            {
                Soldiers = 0; State = SimState.Lost;
                Events.Add(new SimEvent(SimEventType.Lost, SquadX, 0, Wave));
            }
        }
    }
}

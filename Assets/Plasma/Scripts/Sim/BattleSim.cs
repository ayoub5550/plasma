using System;
using System.Collections.Generic;

namespace Plasma.Sim
{
    public enum SimState { Running, Won, Lost }

    public struct Bullet { public float X, Z, Dmg; }

    /// <summary>
    /// The whole battle as a deterministic, engine-free simulation. The Unity layer only feeds
    /// input (TargetX) and draws the state; the balance sweep runs it thousands of times headless.
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
        public int EnemyCount;          // total in current wave
        public int AliveEnemies;
        public float[] EnemyX = new float[0];
        public int[] EnemyRow = new int[0];
        public float[] EnemyHp = new float[0];
        public bool[] EnemyAlive = new bool[0];
        public bool[] EnemyBrute = new bool[0];
        public float HordeStartZ;
        public float HordeTraveled;
        int[][] _col;                    // enemy indices per column, front (lowest row) first
        int[] _colFront;

        // ---- gates ----
        public readonly List<GateSpec> Gates = new List<GateSpec>();
        public int GateIndex;           // index of the gate in the slot
        public float GateHp;
        public float GateSlide;         // >0 while the next gate slides in (not shootable)

        // ---- boss ----
        public bool BossSpawned, BossAlive;
        public float BossHp, BossMaxHp, BossZ, BossX;
        float _biteAcc;

        // ---- bullets / events ----
        public readonly List<Bullet> Bullets = new List<Bullet>(256);
        public readonly List<SimEvent> Events = new List<SimEvent>(256);
        float _fireTimer;
        int _volley;
        static float[] _fx, _fz;          // formation offsets (sunflower)

        public BattleSim(LevelSpec spec, RunModifiers mods, bool endless = false)
        {
            Mods = mods; Endless = endless;
            Soldiers = MaxSoldiers = Math.Max(1, mods.StartSoldiers);
            SquadX = TargetX = Balance.HordeCenterX;
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
                _fx[i] = (float)(r * Math.Cos(a)); _fz[i] = (float)(r * Math.Sin(a) * 0.8);
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
            int n = spec.EnemyCount;
            EnemyCount = AliveEnemies = n;
            EnemyX = new float[n]; EnemyRow = new int[n]; EnemyHp = new float[n]; EnemyAlive = new bool[n]; EnemyBrute = new bool[n];
            int cols = Balance.HordeColumns;
            var lists = new List<int>[cols];
            for (int c = 0; c < cols; c++) lists[c] = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int c = i % cols, row = i / cols;
                bool brute = rng.NextDouble() < spec.BruteFraction && row > 1;
                EnemyBrute[i] = brute;
                EnemyHp[i] = (brute ? Balance.BruteHp : Balance.GruntHp) * spec.EnemyHpScale;
                EnemyAlive[i] = true;
                EnemyRow[i] = row;
                EnemyX[i] = Balance.HordeMinX + (c + 0.5f) * Balance.HordeColSpacing + (float)(rng.NextDouble() - 0.5) * 0.12f;
                lists[c].Add(i);
            }
            _col = new int[cols][]; _colFront = new int[cols];
            for (int c = 0; c < cols; c++) _col[c] = lists[c].ToArray();
            HordeStartZ = spec.HordeStartZ;
            HordeTraveled = 0;

            if (Gates.Count == 0 || GateIndex >= Gates.Count)
            {
                Gates.Clear(); GateIndex = 0;
            }
            Gates.AddRange(spec.Gates);
            if (GateIndex < Gates.Count && GateHp <= 0) GateHp = Gates[GateIndex].Hp;

            BossSpawned = BossAlive = false; BossHp = BossMaxHp = spec.BossHp; _biteAcc = 0;
            Events.Add(new SimEvent(SimEventType.WaveStarted, 0, 0, Wave));
        }

        // ------------------------------------------------------------------ queries
        public float EnemyZ(int i) => HordeStartZ + EnemyRow[i] * Balance.HordeRowSpacing - HordeTraveled;

        public bool GateAvailable => GateIndex < Gates.Count && GateSlide <= 0;
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

        public float SquadDps => Soldiers * Balance.BaseDamage * Mods.DamageMult * Mods.FireRateMult / Balance.FireInterval;

        /// <summary>Progress 0..1 of the current level (horde + boss).</summary>
        public float Progress
        {
            get
            {
                float total = Spec.HordeHpTotal + Math.Max(1, BossMaxHp);
                float left = 0;
                for (int i = 0; i < EnemyCount; i++) if (EnemyAlive[i]) left += EnemyHp[i];
                left += BossSpawned ? Math.Max(0, BossHp) : BossMaxHp;
                return 1f - left / total;
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

            if (GateSlide > 0) GateSlide -= dt;

            // horde advance + contact
            if (AliveEnemies > 0)
            {
                HordeTraveled += Spec.HordeSpeed * dt;
                for (int c = 0; c < Balance.HordeColumns; c++)
                {
                    int e;
                    while ((e = ColumnFront(c)) >= 0 && EnemyZ(e) <= Balance.DefenseZ)
                    {
                        KillEnemy(e, false);
                        int bite = EnemyBrute[e] ? Balance.BruteBite : Balance.GruntBite;
                        LoseSoldiers(bite, EnemyX[e]);
                        Events.Add(new SimEvent(SimEventType.EnemyReachedSquad, EnemyX[e], Balance.DefenseZ, bite, e));
                    }
                }
            }

            // boss
            if (!BossSpawned && AliveEnemies == 0 && BossMaxHp > 0)
            {
                BossSpawned = BossAlive = true; BossZ = Balance.BossSpawnZ; BossX = Balance.HordeCenterX;
                Events.Add(new SimEvent(SimEventType.BossSpawned, BossX, BossZ, (int)BossMaxHp));
            }
            if (BossAlive)
            {
                float stopZ = Balance.DefenseZ + Balance.BossRadius * 0.8f;
                if (BossZ > stopZ) BossZ = Math.Max(stopZ, BossZ - Spec.BossSpeed * dt);
                else
                {
                    _biteAcc += Spec.BossBiteRate * dt;
                    int n = (int)_biteAcc;
                    if (n > 0) { _biteAcc -= n; LoseSoldiers(n, BossX); Events.Add(new SimEvent(SimEventType.BossBite, BossX, BossZ, n)); }
                }
            }

            if (State != SimState.Running) return;

            // shooting
            _fireTimer -= dt;
            float interval = Balance.FireInterval / Mods.FireRateMult;
            while (_fireTimer <= 0) { _fireTimer += interval; FireVolley(); }

            // bullets
            float bdz = Balance.BulletSpeed * dt;
            for (int i = Bullets.Count - 1; i >= 0; i--)
            {
                var b = Bullets[i];
                b.Z += bdz;
                if (ResolveBullet(ref b) || b.Z > Balance.BulletMaxZ) { Bullets.RemoveAt(i); continue; }
                Bullets[i] = b;
            }

            // win / next wave
            if (AliveEnemies == 0 && !BossAlive && (BossSpawned || BossMaxHp <= 0))
            {
                if (Endless)
                {
                    Wave++;
                    StartWave(LevelGenerator.Create(Wave));
                }
                else
                {
                    State = SimState.Won;
                    Events.Add(new SimEvent(SimEventType.Won, SquadX, 0, Soldiers));
                }
            }
        }

        void FireVolley()
        {
            if (Soldiers <= 0) return;
            int shooters = Math.Min(Soldiers, Balance.SoldierVisualCap);
            int nb = Math.Min(Soldiers, Balance.MaxBulletsPerVolley);
            float dmg = Soldiers * Balance.BaseDamage * Mods.DamageMult / nb;
            for (int k = 0; k < nb; k++)
            {
                int s = (_volley * 7 + k * 13) % shooters;
                Bullets.Add(new Bullet { X = SquadX + _fx[s], Z = Balance.SquadZ + 0.5f + _fz[s], Dmg = dmg });
            }
            _volley++;
            Events.Add(new SimEvent(SimEventType.Volley, SquadX, 0, nb));
        }

        /// <summary>Applies the bullet's damage. Returns true if the bullet is spent.</summary>
        bool ResolveBullet(ref Bullet b)
        {
            // gate lane
            if (b.X >= Balance.GateMinX && b.X <= Balance.GateMaxX)
            {
                if (GateIndex < Gates.Count && b.Z >= Balance.GateSlotZ - 0.3f)
                {
                    if (GateSlide > 0) return true; // the incoming gate soaks it
                    GateHp -= b.Dmg;
                    Events.Add(new SimEvent(SimEventType.GateHit, b.X, Balance.GateSlotZ, 0, GateIndex));
                    if (GateHp <= 0) BreakGate();
                    return true;
                }
                return false;
            }
            // horde lane
            if (b.X >= Balance.HordeMinX && b.X < Balance.HordeMaxX)
            {
                if (AliveEnemies > 0)
                {
                    int c = (int)((b.X - Balance.HordeMinX) / Balance.HordeColSpacing);
                    if (c < 0) c = 0; if (c >= Balance.HordeColumns) c = Balance.HordeColumns - 1;
                    int e;
                    while (b.Dmg > 0.0001f && (e = ColumnFront(c)) >= 0 && EnemyZ(e) <= b.Z + 0.15f)
                    {
                        if (b.Dmg >= EnemyHp[e]) { b.Dmg -= EnemyHp[e]; KillEnemy(e, true); }
                        else { EnemyHp[e] -= b.Dmg; b.Dmg = 0; Events.Add(new SimEvent(SimEventType.EnemyHit, EnemyX[e], EnemyZ(e), 0, e)); }
                    }
                    if (b.Dmg <= 0.0001f) return true;
                }
            }
            if (BossAlive && Math.Abs(b.X - BossX) < Balance.BossRadius && b.Z >= BossZ - 0.6f)
            {
                BossHp -= b.Dmg;
                Events.Add(new SimEvent(SimEventType.BossHit, b.X, BossZ, (int)Math.Ceiling(Math.Max(0, BossHp))));
                if (BossHp <= 0)
                {
                    BossAlive = false; Kills++;
                    Events.Add(new SimEvent(SimEventType.BossKilled, BossX, BossZ));
                }
                return true;
            }
            return false;
        }

        void BreakGate()
        {
            var g = Gates[GateIndex];
            int gain = (int)Math.Round(g.Value * Mods.GateMult);
            Events.Add(new SimEvent(SimEventType.GateBroken, Balance.GateCenterX, Balance.GateSlotZ, gain, GateIndex));
            if (gain > 0)
            {
                Soldiers += gain; MaxSoldiers = Math.Max(MaxSoldiers, Soldiers);
                Events.Add(new SimEvent(SimEventType.SoldiersGained, SquadX, 0, gain));
            }
            GateIndex++;
            if (GateIndex < Gates.Count) { GateHp = Gates[GateIndex].Hp; GateSlide = Balance.GateSlideTime; }
        }

        void KillEnemy(int e, bool byPlayer)
        {
            EnemyAlive[e] = false; AliveEnemies--;
            if (byPlayer) { Kills++; Events.Add(new SimEvent(SimEventType.EnemyKilled, EnemyX[e], EnemyZ(e), EnemyBrute[e] ? 1 : 0, e)); }
        }

        void LoseSoldiers(int n, float x)
        {
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

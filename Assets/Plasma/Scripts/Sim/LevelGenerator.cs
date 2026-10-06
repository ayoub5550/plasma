using System;

namespace Plasma.Sim
{
    /// <summary>
    /// Deterministic, infinite level generator. Level N always produces the same LevelSpec.
    /// Difficulty is a rising curve with a 5-level saw-tooth (boss spike, then a relief level).
    /// Every formula is documented in docs/LEVELS.md - keep the two in sync.
    /// </summary>
    public static class LevelGenerator
    {
        static readonly int[] NiceValues = { 1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30, 40, 50, 60, 75, 99 };

        /// <summary>Snap to the closest "nice" gate number (max 99, as in the reference).</summary>
        public static int Nice(double v)
        {
            int best = 1; double bd = double.MaxValue;
            foreach (int n in NiceValues) { double d = Math.Abs(Math.Log(n) - Math.Log(Math.Max(0.5, v))); if (d < bd) { bd = d; best = n; } }
            return best;
        }

        /// <summary>Colour tier by value: grey 0, blue 1-2, green 3-5, cyan 6-12, yellow 15-30, orange 40-60, purple 75+.</summary>
        public static GateTier TierOf(int v)
            => v <= 0 ? GateTier.Grey : v <= 2 ? GateTier.Blue : v <= 5 ? GateTier.Green : v <= 12 ? GateTier.Cyan
             : v <= 30 ? GateTier.Yellow : v <= 60 ? GateTier.Orange : GateTier.Purple;
        public const int MaxVisibleEnemies = 2400;

        // Tunables (kept public so the sweep / tests can print them). Documented in docs/LEVELS.md.
        public static double HordeBase = 90, HordePerLevel = 30, HordeCurve = 0.9, HordeCurveExp = 1.5;
        public static double EnemyHpLin = 0.4, EnemyHpQuad = 0.011;
        public static double BossHpBase = 60, BossHpPerLevel = 10, BigBossMult = 5;
        public static double GateBudgetBase = 22, GateBudgetPerLevel = 6.5, GateRamp = 1.3;
        public static double GateHpBase = 2, GateHpPerValue = 2.2, GateHpExp = 1.1, GateHpGrowth = 0.10;

        /// <summary>Hit points of a basic enemy at this level (brutes have Balance.BruteHp times this).</summary>
        public static double EnemyHp(int level) { int t = Math.Max(0, level - 1); return 1 + EnemyHpLin * t + EnemyHpQuad * t * t; }

        public static LevelSpec Create(int level)
        {
            if (level < 1) level = 1;
            int t = level - 1;
            var rng = new Random(level * 7919 + 17);
            var s = new LevelSpec { Level = level, Seed = level * 7919 + 17 };

            bool bossLevel = level % 5 == 0;
            bool relief = level > 1 && level % 5 == 1;
            s.Kind = bossLevel ? "boss" : relief ? "relief" : "normal";

            // ---- Horde ----
            double n = HordeBase + HordePerLevel * t + HordeCurve * Math.Pow(t, HordeCurveExp);
            if (bossLevel) n *= 0.85;
            if (relief) n *= 0.8;
            s.EnemyHpScale = (float)EnemyHp(level);
            if (n > MaxVisibleEnemies) { s.EnemyHpScale *= (float)(n / MaxVisibleEnemies); n = MaxVisibleEnemies; }
            s.EnemyCount = (int)Math.Round(n);
            s.BruteFraction = level < 4 ? 0f : (float)Math.Min(0.04 + 0.008 * (level - 4), 0.3);
            s.HordeSpeed = (float)Math.Min(1.25 + 0.022 * t, 2.5);
            s.HordeStartZ = 17f;

            // ---- Gates (left conveyor) ----
            // A value budget that grows linearly, spread over a geometric ramp (small gates first,
            // a jackpot last) and snapped to "nice" numbers. A few "+0" blockers sit early.
            int budget = (int)Math.Round(GateBudgetBase + GateBudgetPerLevel * t);
            int count = 7 + Math.Min(level / 4, 6);
            double r = GateRamp;
            double a = budget * (r - 1) / (Math.Pow(r, count) - 1);
            float hpScale = (float)(1 + GateHpGrowth * t);
            var values = new System.Collections.Generic.List<int>();
            for (int j = 0; j < count; j++) values.Add(Nice(a * Math.Pow(r, j) * (0.85 + 0.3 * rng.NextDouble())));
            values.Sort();
            int blockers = Math.Min(3, 1 + level / 10);
            for (int b = 0; b < blockers; b++) values.Insert(rng.Next(0, Math.Max(1, values.Count / 3)), 0);
            foreach (int v in values)
                s.Gates.Add(new GateSpec { Tier = TierOf(v), Value = v, Hp = (float)Math.Round((GateHpBase + GateHpPerValue * Math.Pow(v, GateHpExp)) * hpScale) });

            // ---- Boss ----
            s.BigBoss = bossLevel;
            s.BossHp = (float)Math.Round(EnemyHp(level) * (BossHpBase + BossHpPerLevel * level) * (bossLevel ? BigBossMult : 1));
            s.BossSpeed = bossLevel ? 0.9f : 1.1f;
            s.BossBiteRate = (8 + level * 0.5f) * (bossLevel ? 2 : 1);

            s.BaseReward = (25 + 8 * level) * (bossLevel ? 2 : 1);
            return s;
        }
    }
}

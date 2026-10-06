using System;
using System.Collections.Generic;

namespace Plasma.Sim
{
    /// <summary>
    /// Deterministic, infinite level generator. Level N always produces the same LevelSpec.
    /// Difficulty is a rising curve with a 5-level saw-tooth (boss spike, then a relief level).
    /// Every formula is documented in docs/LEVELS.md - keep the two in sync.
    /// </summary>
    public static class LevelGenerator
    {
        static readonly int[] NiceValues = { 1, 2, 3, 5, 8, 10, 15, 20, 25, 30, 40, 50, 75, 99 };

        /// <summary>Snap to the closest "nice" value (max 99, as in the reference).</summary>
        public static int Nice(double v)
        {
            int best = 1; double bd = double.MaxValue;
            foreach (int n in NiceValues) { double d = Math.Abs(Math.Log(n) - Math.Log(Math.Max(0.5, v))); if (d < bd) { bd = d; best = n; } }
            return best;
        }

        /// <summary>Colour tier by value: grey 0, blue 1-2, green 3-9, cyan 10-24, purple 25-60, yellow 61-99 (the jackpot).</summary>
        public static GateTier TierOf(int v)
            => v <= 0 ? GateTier.Grey : v <= 2 ? GateTier.Blue : v <= 9 ? GateTier.Green : v <= 24 ? GateTier.Cyan
             : v <= 60 ? GateTier.Purple : GateTier.Yellow;

        public const int MaxVisibleEnemies = 2800;

        // Tunables (public so the sweep / tests can print or tweak them). Documented in docs/LEVELS.md.
        public static double HordeBase = 1400, HordePerLevel = 120, HordeCurve = 0, HordeCurveExp = 1.5;
        public static double EnemyHpLin = 0.14, EnemyHpQuad = 0.0008;
        public static double BossHpMult = 0.12, BigBossMult = 3;
        public static double TopBase = 18, TopPerLevel = 4, TopExp = 1.0;
        public static double GateHpBase = 4, GateHpPerValue = 9, GateHpExp = 1.0, GateHpGrowth = 0.06;

        /// <summary>Hit points of a basic enemy at this level (brutes have Balance.BruteHp times this).</summary>
        public static double EnemyHp(int level) { int t = Math.Max(0, level - 1); return 1 + EnemyHpLin * t + EnemyHpQuad * t * t; }

        /// <summary>Jackpot (last gate) value of a level, before snapping.</summary>
        public static double TopValue(int level) => TopBase + TopPerLevel * Math.Pow(Math.Max(0, level - 1), TopExp);

        public static LevelSpec Create(int level)
        {
            if (level < 1) level = 1;
            int t = level - 1;
            var rng = new Random(level * 7919 + 17);
            var s = new LevelSpec { Level = level, Seed = level * 7919 + 17 };

            bool bossLevel = level % 5 == 0;
            bool relief = level > 1 && level % 5 == 1;
            s.Kind = bossLevel ? "boss" : relief ? "relief" : "normal";

            // ---- Horde: a long red carpet ----
            double n = HordeBase + HordePerLevel * t + HordeCurve * Math.Pow(t, HordeCurveExp);
            if (relief) n *= 0.8;
            s.EnemyHpScale = (float)EnemyHp(level);
            if (n > MaxVisibleEnemies) { s.EnemyHpScale *= (float)Math.Sqrt(n / MaxVisibleEnemies); n = MaxVisibleEnemies; }
            s.EnemyCount = (int)Math.Round(n / Balance.HordeColumns) * Balance.HordeColumns;
            s.BruteFraction = level < 4 ? 0f : (float)Math.Min(0.03 + 0.006 * (level - 4), 0.25);
            s.HordeSpeed = (float)Math.Min(0.6 + 0.012 * t, 1.15);

            // ---- Upgrade gates (dock): small first, jackpot last ----
            double top = TopValue(level) * (bossLevel ? 1.6 : 1) * (relief ? 0.85 : 1);
            int topV = Nice(Math.Min(99, top));
            int count = 3 + Math.Min(3, level / 8);                  // 3..6 gates
            var values = new List<int>();
            for (int j = 0; j < count; j++)
            {
                double v = Math.Pow(topV, (double)j / (count - 1));      // geometric 1 .. top
                int nv = Nice(v * (j == 0 || j == count - 1 ? 1 : 0.9 + 0.2 * rng.NextDouble()));
                if (values.Count > 0 && nv <= values[values.Count - 1]) nv = NextNice(values[values.Count - 1]);
                values.Add(Math.Min(99, nv));
            }
            for (int j = values.Count - 1; j > 0; j--) if (values[j] <= values[j - 1]) values.RemoveAt(j);
            float hpScale = (float)(1 + GateHpGrowth * t);
            foreach (int v in values)
                s.Gates.Add(new GateSpec { Tier = TierOf(v), Value = v, Hp = (float)Math.Round((GateHpBase + GateHpPerValue * (Math.Pow(v, GateHpExp) - 1)) * hpScale) });

            // ---- Boss: walks inside the horde ----
            s.BigBoss = bossLevel;
            s.BossDepth = bossLevel ? 0.9f : 0.8f;
            s.BossHp = (float)Math.Round(Math.Max(30, s.HordeHpTotal * BossHpMult * (bossLevel ? BigBossMult : 1)));
            s.BossBiteRate = (6 + level * 0.4f) * (bossLevel ? 2 : 1);

            s.BaseReward = (25 + 8 * level) * (bossLevel ? 2 : 1);
            return s;
        }

        static int NextNice(int v)
        {
            foreach (int n in NiceValues) if (n > v) return n;
            return 99;
        }
    }
}

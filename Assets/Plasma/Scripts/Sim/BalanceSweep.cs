using System;
using System.Text;

namespace Plasma.Sim
{
    /// <summary>
    /// Headless "career" simulation: a bot plays levels 1..N in order, retries on failure and
    /// buys upgrades with its coins exactly like a player would. The report shows whether the
    /// difficulty curve is fair (attempts per level, squad size, time). See docs/LEVELS.md.
    /// </summary>
    public static class BalanceSweep
    {
        public const float Dt = 1f / 30f;
        public const float MaxLevelTime = 400f;

        public struct Result { public bool Won; public float Time; public int Soldiers, MaxSoldiers, Kills; public float Progress; }

        public static Result Play(LevelSpec spec, RunModifiers mods, float skill, int seed)
        {
            var sim = new BattleSim(spec, mods);
            var bot = new BotPolicy(skill, seed);
            while (sim.State == SimState.Running && sim.Time < MaxLevelTime)
            {
                bot.Update(sim, Dt);
                sim.Step(Dt);
                sim.Events.Clear();
            }
            return new Result { Won = sim.State == SimState.Won, Time = sim.Time, Soldiers = sim.Soldiers, MaxSoldiers = sim.MaxSoldiers, Kills = sim.Kills, Progress = sim.Progress };
        }

        public static int WinReward(LevelSpec spec, int soldiersLeft) => spec.BaseReward + Math.Min(soldiersLeft, 1000) / 5;
        public static int LossReward(LevelSpec spec, float progress) => (int)(spec.BaseReward * 0.5f * progress);

        /// <summary>Spends coins on the cheapest upgrade until nothing is affordable.</summary>
        public static void AutoBuy(PlayerProfile p)
        {
            while (true)
            {
                int best = -1, bestCost = int.MaxValue;
                for (int u = 0; u < Upgrades.Count; u++)
                {
                    int c = Upgrades.Cost((UpgradeType)u, p.Upg[u]);
                    if (c < bestCost && p.Upg[u] < Balance.UpgradeMaxLevel) { bestCost = c; best = u; }
                }
                if (best < 0 || !p.TryBuy((UpgradeType)best)) return;
            }
        }

        public static string Career(int levels, float skill, int maxAttempts = 20)
        {
            var sb = new StringBuilder();
            var p = new PlayerProfile();
            sb.AppendLine($"# Career sweep: skill {skill:0.00}, levels 1..{levels}");
            sb.AppendLine();
            sb.AppendLine("| L | kind | enemies | spd | gates(+sum) | boss hp | attempts | time s | max squad | end squad | upg F/R/S/G | coins |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            int totalAttempts = 0, stuck = 0;
            for (int L = 1; L <= levels; L++)
            {
                var spec = LevelGenerator.Create(L);
                Result r = default; int a = 0;
                for (a = 1; a <= maxAttempts; a++)
                {
                    r = Play(spec, p.Modifiers(), skill, L * 31 + a);
                    if (r.Won) { p.Coins += WinReward(spec, r.Soldiers); break; }
                    p.Coins += LossReward(spec, r.Progress);
                    AutoBuy(p);
                }
                AutoBuy(p);
                totalAttempts += Math.Min(a, maxAttempts);
                sb.AppendLine($"| {L} | {spec.Kind} | {spec.EnemyCount}{(spec.EnemyHpScale > 1 ? $"x{spec.EnemyHpScale:0.0}hp" : "")} | {spec.HordeSpeed:0.00} | {spec.Gates.Count}(+{spec.TotalGateValue}) | {spec.BossHp} | {(r.Won ? a.ToString() : "FAIL")} | {r.Time:0} | {r.MaxSoldiers} | {r.Soldiers} | {p.Upg[0]}/{p.Upg[1]}/{p.Upg[2]}/{p.Upg[3]} | {p.Coins} |");
                if (!r.Won) { stuck++; if (stuck >= 1) { sb.AppendLine($"\nSTUCK at level {L} after {maxAttempts} attempts."); break; } }
                p.Level = L + 1;
            }
            sb.AppendLine($"\nTotal attempts: {totalAttempts}");
            return sb.ToString();
        }
    }
}

// Mono console harness for the pure-C# simulation (no Unity needed, compiles in ~2 s).
// Usage: tools/simharness/run.sh [levels] [skill]          -> career sweep (markdown table)
//        tools/simharness/run.sh levels                    -> table of generated levels (docs/LEVELS.md §3)
//        tools/simharness/run.sh trace <level> <skill> [F R S G upgrade levels]  -> per-second timeline of one level
using System;
using System.Globalization;
using Plasma.Sim;

static class Program
{
    static void Main(string[] args)
    {
        var ci = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "trace")
        {
            int level = int.Parse(args[1]); float skill = float.Parse(args[2], ci);
            var p = new PlayerProfile();
            for (int u = 0; u < 4 && 3 + u < args.Length; u++) p.Upg[u] = int.Parse(args[3 + u]);
            var spec = LevelGenerator.Create(level);
            Console.WriteLine($"L{level} {spec.Kind} enemies={spec.EnemyCount} rows={spec.Rows} hp={spec.EnemyHpScale:0.0} speed={spec.HordeSpeed:0.00} boss={spec.BossHp}");
            Console.WriteLine("gates: " + string.Join(" ", spec.Gates.ConvertAll(g => $"+{g.Value}/{g.Hp}")));
            var sim = new BattleSim(spec, p.Modifiers());
            var bot = new BotPolicy(skill, 1);
            float next = 0;
            while (sim.State == SimState.Running && sim.Time < 400)
            {
                bot.Update(sim, BalanceSweep.Dt); sim.Step(BalanceSweep.Dt); sim.Events.Clear();
                if (sim.Time >= next) { next += 1; Console.WriteLine($"t={sim.Time,5:0.0} x={sim.SquadX,5:0.0} soldiers={sim.Soldiers,5} belt=+{sim.ConveyorValue,-3} caught={sim.TilesCaught,3} gate#{sim.GateIndex} hp={sim.GateHp,6:0} front={sim.FrontZ(),6:0.0} alive={sim.AliveEnemies,5} boss={(sim.BossAlive ? sim.BossHp : 0),7:0}@{sim.BossZ,5:0.0}"); }
            }
            Console.WriteLine($"RESULT {sim.State} t={sim.Time:0.0} soldiers={sim.Soldiers} max={sim.MaxSoldiers}");
            return;
        }
        if (args.Length > 0 && args[0] == "levels")
        {
            Console.WriteLine("| Level | kind | enemies | enemy HP | brutes | speed | gates | gate ladder (value/HP) | boss HP | reward |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (int L in new[] { 1, 2, 3, 4, 5, 6, 10, 15, 20, 25, 30, 40, 50, 60, 75, 100 })
            {
                var s = LevelGenerator.Create(L);
                Console.WriteLine($"| {L} | {s.Kind} | {s.EnemyCount} | {s.EnemyHpScale:0.0} | {s.BruteFraction * 100:0}% | {s.HordeSpeed:0.00} | {s.Gates.Count} | {string.Join(" ", s.Gates.ConvertAll(g => $"+{g.Value}/{g.Hp}"))} | {s.BossHp} | {s.BaseReward} |");
            }
            return;
        }
        int levels = args.Length > 0 ? int.Parse(args[0]) : 40;
        float sk = args.Length > 1 ? float.Parse(args[1], ci) : 1f;
        Console.WriteLine(BalanceSweep.Career(levels, sk));
    }
}

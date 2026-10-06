using System;

namespace Plasma.Sim
{
    /// <summary>
    /// Automatic player used by the balance sweep, the demo/attract mode and screenshot capture.
    /// Skill 1 = "expert" (fast, good timing), 0 = "casual" (slow reactions, greedy/late decisions).
    /// It chooses between three spots: the horde lane (defend), the dock (break the upgrade gate)
    /// and the belt end (collect tiles) - the same decision a human makes.
    /// </summary>
    public class BotPolicy
    {
        public readonly float Skill;
        readonly Random _rng;
        float _thinkTimer;
        float _safety;
        float _noise;
        int _mode;           // 0 defend, 1 gate, 2 collect
        float _modeTime;

        public BotPolicy(float skill, int seed = 1)
        {
            Skill = Math.Max(0, Math.Min(1, skill));
            _rng = new Random(seed);
            _safety = 1.5f;
        }

        public void Update(BattleSim sim, float dt)
        {
            _modeTime += dt;
            _thinkTimer -= dt;
            if (_thinkTimer > 0) return;
            float reaction = 0.06f + (1 - Skill) * 0.5f;
            _thinkTimer = reaction;
            _safety = 1.0f + (float)_rng.NextDouble() * (1 - Skill) * 2.5f + Skill * 0.5f;
            _noise = (float)(_rng.NextDouble() - 0.5) * (1 - Skill) * 0.8f;
            sim.TargetX = Decide(sim) + _noise;
        }

        /// <summary>Seconds until the horde (or the boss) starts eating the squad.</summary>
        public static float Eta(BattleSim sim)
        {
            float eta = float.PositiveInfinity;
            float v = Math.Max(sim.Spec.HordeSpeed, sim.MarchSpeed);
            if (sim.AliveEnemies > 0) eta = (sim.FrontZ() - Balance.DefenseZ) / v;
            if (sim.BossAlive) eta = Math.Min(eta, (sim.BossZ - sim.BossStopZ) / v);
            return Math.Max(0, eta);
        }

        int _lastValue = -1, _caughtAtUpgrade;
        float _dashUntil;

        /// <summary>Soldiers expected to be lost if the squad stops shooting the horde for `seconds`.</summary>
        static float ExpectedBites(BattleSim sim, float seconds)
        {
            float reach = sim.Spec.HordeSpeed * seconds;
            float bites = 0;
            for (int c = 0; c < Balance.HordeColumns; c++)
            {
                int e = sim.ColumnFront(c);
                if (e < 0) continue;
                float gap = sim.EnemyZ(e) - Balance.DefenseZ;
                if (gap < reach) bites += (1 + (reach - gap) / Balance.HordeRowSpacing) * (1 + sim.Spec.BruteFraction * 2);
            }
            if (sim.BossAlive && sim.BossZ - sim.BossStopZ < reach) bites += sim.Spec.BossBiteRate * (reach - (sim.BossZ - sim.BossStopZ)) / sim.Spec.HordeSpeed;
            return bites;
        }

        float Decide(BattleSim sim)
        {
            if (sim.ConveyorValue != _lastValue) { _lastValue = sim.ConveyorValue; _caughtAtUpgrade = sim.TilesCaught; }
            float dps = Math.Max(0.01f, sim.SquadDps);
            float eta = Eta(sim);
            float travel = 2f * (Math.Abs(sim.SquadX - Balance.DockX) + 3f) / Balance.SquadMoveSpeed;

            // hysteresis: once defending, keep defending until there is real breathing room
            float need = _safety + (_mode == 0 ? 1.2f : 0f);

            // 1) dock gate: worth it if we can break it before the horde arrives
            if (sim.HasGate)
            {
                float r = sim.SquadRadius;
                float laneW = Balance.DockMaxX - Balance.DockMinX;
                float hitFrac = Math.Min(1f, laneW / (2 * r));
                float tBreak = Math.Max(0, sim.GateHp) / (dps * hitFrac) + Math.Max(0, sim.GateInflate) + travel;
                // an expert also takes a short dash under pressure when the gate is almost down
                bool dash = sim.GateAvailable && sim.GateHp < dps * hitFrac * 0.6f && _rng.NextDouble() < Skill;
                // with a paying belt, only go for the gate once it breaks quickly (squad big enough)
                bool quick = sim.TileGain == 0 || tBreak - travel <= 1.2f + 2.5f * Skill || eta > 25f;
                if ((eta > tBreak + need && quick) || dash)
                {
                    // waiting for a gate to inflate is better spent collecting
                    if (!sim.GateAvailable && sim.TileGain > 0 && sim.GateInflate > 0.3f) return Collect(sim);
                    _mode = 1;
                    return Math.Max(Balance.DockMinX + r * 0.7f, Math.Min(Balance.DockMaxX - 0.25f, Balance.DockX));
                }
            }

            // 1b) committed to a collecting dash?
            if (_dashUntil > sim.Time && sim.TileGain > 0 && sim.HordeHpLeft > sim.SquadDps * 4f) return Collect(sim);

            // 2) collect tiles while it is safe (only worth it while there is a gate left to break,
            //    or while the squad is still too small to finish the level quickly)
            bool needMore = sim.HasGate || sim.SquadDps * 6f < sim.HordeHpLeft + (sim.BossAlive ? sim.BossHp : 0)
                            || (sim.TileGain >= 20 && sim.TilesCaught - _caughtAtUpgrade < 12);   // grab a burst of jackpot (+20 .. +99) tiles
            if (sim.TileGain > 0 && needMore)
            {
                float tilesTime = 1.6f + Skill * 1.5f; // how long we are willing to stay at the belt
                if (eta > tilesTime + need) return Collect(sim);
            }

            // 2b) under pressure: a short dash to the belt pays if the tiles outweigh the bites
            if (sim.TileGain > 0 && needMore && sim.AliveEnemies > 0 && _rng.NextDouble() < 0.3 + 0.7 * Skill)
            {
                float dashT = 1.0f + 0.6f * Skill;
                float awayT = dashT + 2f * Math.Abs(sim.SquadX - sim.BeltX) / Balance.SquadMoveSpeed;
                float gain = sim.TileGain * (dashT * Balance.ConvSpeed / Balance.ConvSpacing);
                float cost = ExpectedBites(sim, awayT);
                if (gain > cost * 1.4f + 5) { _dashUntil = sim.Time + awayT; return Collect(sim); }
            }

            // 3) defend: aim where the threat is
            _mode = 0;
            if (sim.AliveEnemies == 0 && sim.BossAlive) return sim.BossX;
            if (sim.BossAlive && sim.BossZ < sim.FrontZ() + 1.5f && sim.BossZ < 6f) return sim.BossX;
            float bestX = Balance.HordeCenterX, bestScore = float.NegativeInfinity;
            float rad = sim.SquadRadius;
            for (int c = 0; c < Balance.HordeColumns; c++)
            {
                float cx = Balance.HordeMinX + (c + 0.5f) * Balance.HordeColSpacing;
                float score = 0;
                for (int k = 0; k < Balance.HordeColumns; k++)
                {
                    float kx = Balance.HordeMinX + (k + 0.5f) * Balance.HordeColSpacing;
                    if (Math.Abs(kx - cx) > rad - 0.3f) continue;
                    int e = sim.ColumnFront(k);
                    if (e < 0) continue;
                    float z = sim.EnemyZ(e) - Balance.DefenseZ;
                    score += 1f / (Math.Max(0.2f, z) + 0.5f);
                }
                if (score > bestScore) { bestScore = score; bestX = cx; }
            }
            return bestX;
        }

        float Collect(BattleSim sim) { _mode = 2; return sim.BeltX; }
    }
}

using System;

namespace Plasma.Sim
{
    /// <summary>
    /// Automatic player used by the balance sweep, the demo/attract mode and screenshot capture.
    /// Skill 1 = "expert" (instant, optimal-ish), 0 = "casual" (slow reactions, bad timing).
    /// </summary>
    public class BotPolicy
    {
        public readonly float Skill;
        readonly Random _rng;
        float _thinkTimer;
        float _safety;
        float _noise;

        public BotPolicy(float skill, int seed = 1)
        {
            Skill = Math.Max(0, Math.Min(1, skill));
            _rng = new Random(seed);
            _safety = 1.2f;
        }

        public void Update(BattleSim sim, float dt)
        {
            _thinkTimer -= dt;
            if (_thinkTimer > 0) return;
            float reaction = 0.05f + (1 - Skill) * 0.45f;
            _thinkTimer = reaction;
            _safety = 0.8f + (float)_rng.NextDouble() * (1 - Skill) * 3f + Skill * 0.6f;
            _noise = (float)(_rng.NextDouble() - 0.5) * (1 - Skill) * 1.2f;
            sim.TargetX = Decide(sim) + _noise;
        }

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
                if (gap < reach) bites += 1 + (reach - gap) / Balance.HordeRowSpacing;
            }
            return bites;
        }

        float Decide(BattleSim sim)
        {
            float dps = Math.Max(0.01f, sim.SquadDps);
            float speed = sim.Spec.HordeSpeed;

            // time until the horde (or boss) bites
            float eta;
            if (sim.AliveEnemies > 0) eta = (sim.FrontZ() - Balance.DefenseZ) / speed;
            else if (sim.BossAlive) eta = (sim.BossZ - Balance.DefenseZ - Balance.BossRadius * 0.8f) / Math.Max(0.01f, sim.Spec.BossSpeed);
            else eta = float.PositiveInfinity;

            // is the next gate worth it?
            if (sim.GateIndex < sim.Gates.Count)
            {
                float laneW = Balance.GateMaxX - Balance.GateMinX;
                float hitFrac = Math.Min(1f, laneW / (2 * sim.SquadRadius));
                float tBreak = Math.Max(0, sim.GateHp) / (dps * hitFrac) + Math.Abs(sim.SquadX - Balance.GateCenterX) / Balance.SquadMoveSpeed * 2 + Math.Max(0, sim.GateSlide);
                // look ahead 5 gates: +0 blockers are worth breaking if good gates follow
                float value = 0;
                for (int k = 0; k < 5 && sim.GateIndex + k < sim.Gates.Count; k++) value += sim.Gates[sim.GateIndex + k].Value / (1f + k);
                bool boss = !(sim.AliveEnemies > 0) && sim.BossAlive;
                float bossMargin = boss ? 1.5f : 0f;
                bool safe = eta > tBreak + _safety + bossMargin;
                // under pressure: a quick dash is still worth it if the gate pays more than the bites it costs
                bool worthDash = false;
                if (!safe && sim.AliveEnemies > 0 && _rng.NextDouble() < Skill * Skill)
                {
                    float cost = ExpectedBites(sim, tBreak + 0.15f);
                    worthDash = sim.CurrentGate.Value * sim.Mods.GateMult > cost * 1.3f + 1;
                }
                if (value > 0 && (safe || worthDash))
                    return Math.Max(Balance.GateMinX + sim.SquadRadius * 0.6f, Math.Min(Balance.GateMaxX - 0.2f, Balance.GateCenterX));
            }

            if (sim.AliveEnemies == 0 && sim.BossAlive) return sim.BossX;
            if (sim.AliveEnemies == 0) return Balance.HordeCenterX;

            // aim where the threat is: weight each column by how close its front enemy is
            float bestX = Balance.HordeCenterX, bestScore = float.NegativeInfinity;
            float r = sim.SquadRadius;
            for (int c = 0; c < Balance.HordeColumns; c++)
            {
                float cx = Balance.HordeMinX + (c + 0.5f) * Balance.HordeColSpacing;
                float score = 0;
                for (int k = 0; k < Balance.HordeColumns; k++)
                {
                    float kx = Balance.HordeMinX + (k + 0.5f) * Balance.HordeColSpacing;
                    if (Math.Abs(kx - cx) > r + 0.2f) continue;
                    int e = sim.ColumnFront(k);
                    if (e < 0) continue;
                    float z = sim.EnemyZ(e) - Balance.DefenseZ;
                    score += 1f / (Math.Max(0.2f, z) + 0.5f);
                }
                if (score > bestScore) { bestScore = score; bestX = cx; }
            }
            return bestX;
        }
    }
}

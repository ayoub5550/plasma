using System.Collections.Generic;

namespace Plasma.Sim
{
    public enum GateTier { Grey = 0, Blue = 1, Green = 2, Cyan = 3, Yellow = 4, Orange = 5, Purple = 6 }

    public struct GateSpec
    {
        public int Value;     // soldiers granted when broken
        public float Hp;      // damage needed to break it
        public GateTier Tier;
    }

    /// <summary>Everything that defines one level (or one endless wave). Produced by LevelGenerator.</summary>
    public class LevelSpec
    {
        public int Level;
        public int Seed;
        public int EnemyCount;
        public float BruteFraction;
        public float EnemyHpScale = 1f; // >1 once the visible-enemy cap is reached (late levels)
        public float HordeSpeed;     // units/s
        public float HordeStartZ;
        public List<GateSpec> Gates = new List<GateSpec>();
        public float BossHp;
        public float BossSpeed;
        public float BossBiteRate;   // soldiers killed per second while the boss touches the squad
        public bool BigBoss;
        public int BaseReward;       // coins for winning (before bonuses)
        public string Kind;          // "normal", "boss", "relief"

        public int TotalGateValue { get { int s = 0; foreach (var g in Gates) s += g.Value; return s; } }
        public float TotalGateHp { get { float s = 0; foreach (var g in Gates) s += g.Hp; return s; } }
        public int HordeHpTotal { get { int b = (int)(EnemyCount * BruteFraction); return (int)(((EnemyCount - b) * Balance.GruntHp + b * Balance.BruteHp) * EnemyHpScale); } }
    }
}

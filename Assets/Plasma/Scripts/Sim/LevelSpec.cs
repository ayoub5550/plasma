using System.Collections.Generic;

namespace Plasma.Sim
{
    /// <summary>Colour tier of a "+N" value (gate + conveyor tiles). Order = rarity.</summary>
    public enum GateTier { Grey = 0, Blue = 1, Green = 2, Cyan = 3, Purple = 4, Yellow = 5 }

    /// <summary>One upgrade gate in the dock: break it (Hp damage) and the conveyor tiles become "+Value".</summary>
    public struct GateSpec
    {
        public int Value;
        public float Hp;
        public GateTier Tier;
    }

    /// <summary>Everything that defines one level (or one endless wave). Produced by LevelGenerator.</summary>
    public class LevelSpec
    {
        public int Level;
        public int Seed;
        public int EnemyCount;
        public float BruteFraction;
        public float EnemyHpScale = 1f;
        public float HordeSpeed;     // units/s
        public List<GateSpec> Gates = new List<GateSpec>();
        public float BossHp;
        public float BossDepth;      // 0..1 position of the boss inside the horde (0 = front)
        public float BossBiteRate;   // soldiers killed per second while the boss touches the squad
        public bool BigBoss;
        public int BaseReward;       // coins for winning (before bonuses)
        public string Kind;          // "normal", "boss", "relief"

        public int Rows => (EnemyCount + Balance.HordeColumns - 1) / Balance.HordeColumns;
        public float TotalGateHp { get { float s = 0; foreach (var g in Gates) s += g.Hp; return s; } }
        public int TopValue => Gates.Count > 0 ? Gates[Gates.Count - 1].Value : 0;
        public int HordeHpTotal { get { int b = (int)(EnemyCount * BruteFraction); return (int)(((EnemyCount - b) * Balance.GruntHp + b * Balance.BruteHp) * EnemyHpScale); } }
    }
}

// Pure C# (no UnityEngine): shared by the game, the headless balance sweep and the Mono test harness.
namespace Plasma.Sim
{
    /// <summary>All tunable gameplay numbers in one place. See docs/LEVELS.md for the reasoning.</summary>
    public static class Balance
    {
        // ---- World layout (Unity units, +z = away from the player) ----
        public const float SquadZ = 0f;
        public const float DefenseZ = 0.7f;          // an enemy reaching this z hits the squad
        public const float SquadMinX = -4.1f, SquadMaxX = 4.3f;
        public const float SquadMoveSpeed = 16f;     // units/s the squad can slide

        public const float GateMinX = -4.4f, GateMaxX = -1.7f; // left lane (gate conveyor)
        public const float GateCenterX = (GateMinX + GateMaxX) * 0.5f;
        public const float GateSlotZ = 12.5f;        // the active (shootable) gate sits here
        public const float GateQueueSpacing = 1.5f;  // queued gates behind the slot
        public const float GateSlideTime = 0.35f;    // next gate slides into the slot

        public const int HordeColumns = 12;
        public const float HordeMinX = -0.9f;        // right lane (horde)
        public const float HordeColSpacing = 0.45f;
        public const float HordeRowSpacing = 0.45f;
        public const float HordeMaxX = HordeMinX + HordeColumns * HordeColSpacing;
        public const float HordeCenterX = (HordeMinX + HordeMaxX) * 0.5f;

        // ---- Squad / shooting ----
        public const float FireInterval = 0.22f;     // seconds between volleys
        public const float BaseDamage = 1f;          // damage per soldier per volley
        public const int MaxBulletsPerVolley = 18;   // visual cap; damage is pooled into the bullets
        public const float BulletSpeed = 34f;
        public const float BulletMaxZ = 32f;
        public const int SoldierVisualCap = 140;
        public const float FormationSpacing = 0.21f; // sunflower packing radius factor

        // ---- Enemies ----
        public const int GruntHp = 1, BruteHp = 4;
        public const int GruntBite = 1, BruteBite = 3; // soldiers lost when it reaches the squad

        // ---- Boss ----
        public const float BossRadius = 1.3f;
        public const float BossSpawnZ = 22f;

        // ---- Economy ----
        public const int UpgradeMaxLevel = 60;
    }
}

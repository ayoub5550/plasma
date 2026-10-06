// Pure C# (no UnityEngine): shared by the game, the headless balance sweep and the Mono test harness.
namespace Plasma.Sim
{
    /// <summary>All tunable gameplay numbers in one place. See docs/LEVELS.md for the reasoning.</summary>
    public static class Balance
    {
        // ---- World layout (Unity units, +z = away from the player) ----------------------------
        //
        //   x: -4.95 ... -3.45 | -3.4 ......................... 4.7
        //      conveyor (left) |  deck: dock slot (left/centre) + horde lane (right)
        //
        public const float SquadZ = 0f;
        public const float DefenseZ = 0.7f;          // an enemy reaching this z hits the squad
        public const float SquadMinX = -3.05f, SquadMaxX = 3.5f;
        public const float SquadMoveSpeed = 16f;     // units/s the squad can slide
        public const float DeckMinX = -3.4f, DeckMaxX = 4.05f, DeckFarZ = 10.4f;

        // Conveyor: a belt of "+N" tiles sliding toward the player along the left edge.
        // The squad collects a tile by standing next to the belt's end when the tile arrives.
        public const float ConvMinX = -5.25f, ConvMaxX = -3.45f;
        public const float ConvX = (ConvMinX + ConvMaxX) * 0.5f;
        public const float ConvSpacing = 1.3f;       // distance between tiles
        public const float ConvSpeed = 3.6f;         // units/s toward the player (~2.8 tiles/s)
        public const float ConvEndZ = 0.35f;         // tiles are collected (or fall off) here
        public const float CatchReach = 0.75f;       // left-most soldier must be within ConvMaxX + reach (v0.4: 0.55 -> 0.75, more forgiving)
        public const int StartTileValue = 1;         // v0.4: the belt pays from the first second (owner: "soldiers for standing in front of the numbers")

        // Dock: the slot at the far edge of the deck where the upgrade gate inflates.
        // Breaking the gate upgrades every tile on the conveyor to the gate's value.
        public const float DockMinX = -2.95f, DockMaxX = -0.55f;
        public const float DockX = (DockMinX + DockMaxX) * 0.5f;
        public const float DockZ = 9.6f;
        public const float GateInflateTime = 0.75f;  // a new gate inflates, not shootable meanwhile

        // Horde lane (right): a dense carpet of enemies marching toward the squad.
        public const int HordeColumns = 14;
        public const float HordeMinX = -0.05f;
        public const float HordeColSpacing = 0.27f;
        public const float HordeRowSpacing = 0.3f;
        public const float HordeMaxX = HordeMinX + HordeColumns * HordeColSpacing;
        public const float HordeCenterX = (HordeMinX + HordeMaxX) * 0.5f;
        public const float HordeStartZ = 11.0f;

        // ---- Squad / shooting ----
        public const float FireInterval = 0.2f;      // seconds between volleys
        public const float BaseDamage = 0.25f;       // damage per soldier per volley (1.25 dps per soldier)
        public const int MaxBulletsPerVolley = 6;    // parallel tracer streams; damage is pooled into the bullets
        public const float BulletSpeed = 30f;
        public const float BulletMaxZ = 40f;
        public const int SoldierVisualCap = 36;      // drawn soldiers (the count keeps growing); also caps the squad width
        public const float FormationSpacing = 0.13f;  // sunflower packing radius factor

        // ---- Enemies ----
        public const int GruntHp = 1, BruteHp = 4;
        public const int GruntBite = 1, BruteBite = 3; // soldiers lost when it reaches the squad

        // ---- Boss (walks inside the horde) ----
        public const float BossRadius = 1.0f, BigBossRadius = 1.3f;

        // ---- Economy ----
        public const int UpgradeMaxLevel = 60;
    }
}

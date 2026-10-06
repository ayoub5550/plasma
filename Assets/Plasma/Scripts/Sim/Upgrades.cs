using System;

namespace Plasma.Sim
{
    public enum UpgradeType { Firepower = 0, FireRate = 1, StartSquad = 2, TileBonus = 3 }

    /// <summary>Permanent upgrades bought with coins between runs.</summary>
    public static class Upgrades
    {
        public const int Count = 4;
        public static readonly string[] NamesEn = { "Firepower", "Fire rate", "Start squad", "Tile bonus" };
        public static readonly string[] NamesAr = { "قوة النار", "سرعة الإطلاق", "جنود البداية", "مكافأة البطاقات" };
        static readonly int[] BaseCost = { 60, 60, 80, 70 };

        public static int Cost(UpgradeType u, int currentLevel)
            => (int)Math.Round(BaseCost[(int)u] * Math.Pow(1.25, currentLevel));

        public static float DamageMult(int lvl) => 1f + 0.15f * lvl;
        public static float FireRateMult(int lvl) => 1f + 0.08f * lvl;
        public static int StartSoldiers(int lvl) => 1 + lvl;
        public static float GateMult(int lvl) => 1f + 0.12f * lvl;
    }

    /// <summary>Persistent player state. Runtime stores it in PlayerPrefs (see Persistence.cs).</summary>
    [Serializable]
    public class PlayerProfile
    {
        public int Level = 1;
        public int Coins;
        public int[] Upg = new int[Upgrades.Count];
        public int BestEndlessWave;
        public bool SoundOn = true;
        public bool VibrationOn = true;
        public bool MusicOn = true;
        public bool Arabic = true;
        public bool LangChosen;      // false = follow the device language
        public bool TutorialDone;

        public int L(UpgradeType u) => Upg[(int)u];

        /// <summary>Buys the upgrade if affordable. Returns true on success.</summary>
        public bool TryBuy(UpgradeType u)
        {
            int lvl = Upg[(int)u];
            if (lvl >= Balance.UpgradeMaxLevel) return false;
            int c = Upgrades.Cost(u, lvl);
            if (Coins < c) return false;
            Coins -= c; Upg[(int)u] = lvl + 1; return true;
        }

        public RunModifiers Modifiers() => new RunModifiers
        {
            DamageMult = Upgrades.DamageMult(L(UpgradeType.Firepower)),
            FireRateMult = Upgrades.FireRateMult(L(UpgradeType.FireRate)),
            StartSoldiers = Upgrades.StartSoldiers(L(UpgradeType.StartSquad)),
            GateMult = Upgrades.GateMult(L(UpgradeType.TileBonus)),
        };
    }

    public struct RunModifiers
    {
        public float DamageMult, FireRateMult, GateMult; // GateMult multiplies conveyor tile values
        public int StartSoldiers;
        public static RunModifiers Default => new RunModifiers { DamageMult = 1, FireRateMult = 1, GateMult = 1, StartSoldiers = 1 };
    }
}

using Plasma.Sim;
using UnityEngine;

namespace Plasma
{
    /// <summary>Saves the PlayerProfile as JSON in PlayerPrefs (key "plasma.profile.v1").</summary>
    public static class Persistence
    {
        const string Key = "plasma.profile.v1";

        public static PlayerProfile Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return new PlayerProfile();
            try
            {
                var p = JsonUtility.FromJson<PlayerProfile>(json);
                if (p.Upg == null || p.Upg.Length != Upgrades.Count) p.Upg = new int[Upgrades.Count];
                if (p.Level < 1) p.Level = 1;
                return p;
            }
            catch { return new PlayerProfile(); }
        }

        public static void Save(PlayerProfile p)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(p));
            PlayerPrefs.Save();
        }
    }
}

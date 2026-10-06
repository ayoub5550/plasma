using Plasma.Sim;
using UnityEngine;

namespace Plasma
{
    /// <summary>All colours in one place (art direction: bright toy-like, see docs/ART_AUDIO.md).</summary>
    public static class Palette
    {
        public static readonly Color Squad = new Color(0.16f, 0.45f, 1f);
        public static readonly Color Enemy = new Color(0.95f, 0.13f, 0.12f);
        public static readonly Color Brute = new Color(0.62f, 0.05f, 0.12f);
        public static readonly Color BossJacket = new Color(0.85f, 0.12f, 0.12f);
        public static readonly Color Deck = new Color(0.6f, 0.61f, 0.66f);
        public static readonly Color Rail = new Color(0.42f, 0.44f, 0.5f);
        public static readonly Color Water = new Color(0.03f, 0.07f, 0.14f);
        public static readonly Color Slot = new Color(0.07f, 0.09f, 0.15f);
        public static readonly Color Bullet = new Color(1f, 0.93f, 0.55f, 1f);
        public static readonly Color Smoke = new Color(0.95f, 0.95f, 0.97f, 0.9f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.15f);

        public static Color Gate(GateTier t)
        {
            switch (t)
            {
                case GateTier.Grey: return new Color(0.55f, 0.57f, 0.6f);
                case GateTier.Blue: return new Color(0.1f, 0.45f, 1f);
                case GateTier.Green: return new Color(0.2f, 0.85f, 0.2f);
                case GateTier.Cyan: return new Color(0.1f, 0.85f, 0.9f);
                case GateTier.Yellow: return new Color(1f, 0.82f, 0.1f);
                case GateTier.Orange: return new Color(1f, 0.5f, 0.1f);
                default: return new Color(0.7f, 0.25f, 1f);
            }
        }
    }
}

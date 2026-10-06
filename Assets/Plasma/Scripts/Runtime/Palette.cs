using Plasma.Sim;
using UnityEngine;

namespace Plasma
{
    /// <summary>All colours in one place (art direction: bright toy-like, matched to the reference video; see docs/ART_AUDIO.md).</summary>
    public static class Palette
    {
        public static readonly Color Squad = new Color(0.14f, 0.42f, 1f);
        public static readonly Color Enemy = new Color(0.93f, 0.1f, 0.09f);
        public static readonly Color Brute = new Color(0.58f, 0.04f, 0.09f);
        public static readonly Color BossJacket = new Color(0.86f, 0.13f, 0.12f);
        public static readonly Color Deck = new Color(0.64f, 0.645f, 0.67f);
        public static readonly Color DeckSide = new Color(0.5f, 0.52f, 0.58f);
        public static readonly Color Rail = new Color(0.47f, 0.49f, 0.56f);
        public static readonly Color RailDark = new Color(0.3f, 0.32f, 0.38f);
        public static readonly Color Belt = new Color(0.36f, 0.38f, 0.45f);
        public static readonly Color Background = new Color(0.04f, 0.075f, 0.14f);
        public static readonly Color Slot = new Color(0.06f, 0.08f, 0.13f);
        public static readonly Color Smoke = new Color(0.96f, 0.96f, 0.98f, 0.95f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.12f);
        public static readonly Color Outline = new Color(0.08f, 0.08f, 0.12f, 1f);

        public static Color Gate(GateTier t)
        {
            switch (t)
            {
                case GateTier.Grey: return new Color(0.6f, 0.62f, 0.67f);
                case GateTier.Blue: return new Color(0.12f, 0.45f, 1f);
                case GateTier.Green: return new Color(0.26f, 0.86f, 0.16f);
                case GateTier.Cyan: return new Color(0.08f, 0.78f, 0.95f);
                case GateTier.Purple: return new Color(0.66f, 0.3f, 1f);
                default: return new Color(1f, 0.8f, 0.08f);
            }
        }
        public static Color Gate(int value) => Gate(LevelGenerator.TierOf(value));
    }
}

using UnityEngine;

namespace Plasma
{
    /// <summary>
    /// Procedurally synthesised sound effects (no audio assets needed) played through a small
    /// AudioSource pool. Replace with real recordings later (docs/ART_AUDIO.md) - keep the API.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public enum Id { Shot, Pop, GateHit, GateBreak, Gain, Hurt, BossHit, BossDie, Win, Lose, Click, Coin, Upgrade, Tile, Inflate }
        public static Sfx I { get; private set; }
        public bool Enabled = true;
        AudioSource _music;

        /// <summary>Starts/stops the synthesised background loop.</summary>
        public void SetMusic(bool on)
        {
            if (_music == null) return;
            if (on && !_music.isPlaying) _music.Play();
            else if (!on && _music.isPlaying) _music.Stop();
        }

        /// <summary>8-second 120 BPM loop (Am-F-C-G): bass, arpeggio, kick, snare, hats. Pure synthesis.</summary>
        static AudioClip MakeMusic()
        {
            const float step = 0.125f; // 16th note
            int n = Mathf.CeilToInt(8f * Rate);
            var data = new float[n];
            int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 } };
            uint seed = 777;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                int s16 = (int)(t / step);
                float ts = t - s16 * step;          // time inside the 16th
                int bar = (s16 / 16) % 4, inBar = s16 % 16;
                var ch = chords[bar];
                // bass: 8th notes on the root, octave jump on offbeats
                int bassNote = ch[0] - 24 + ((inBar / 2) % 2 == 1 ? 12 : 0);
                float tb = t - (s16 / 2) * step * 2;
                float bass = (Mathf.Sin(2 * Mathf.PI * Midi(bassNote) * t) * 0.7f + Mathf.Sign(Mathf.Sin(2 * Mathf.PI * Midi(bassNote) * t)) * 0.12f) * Env(tb, 0.005f, 0.22f);
                // arpeggio: up-down chord tones, triangle-ish
                int[] pat = { 0, 1, 2, 1 };
                int note = ch[pat[inBar % 4]] + 12 + (inBar >= 8 ? 12 : 0) * (bar % 2);
                float ph = Midi(note) * t; float tri = 1 - 4 * Mathf.Abs(ph - Mathf.Floor(ph + 0.5f));
                float arp = tri * Env(ts, 0.003f, 0.11f) * 0.22f;
                // drums
                float beatT = t % 0.5f;
                float kick = Mathf.Sin(2 * Mathf.PI * (55 + 120 * Mathf.Exp(-beatT * 30)) * beatT) * Mathf.Exp(-beatT * 9) * 0.8f;
                seed = seed * 1664525u + 1013904223u; float nz = ((seed >> 9) / 4194304f) - 1f;
                float snT = (t + 0.5f) % 1f;
                float snare = nz * Mathf.Exp(-snT * 22) * 0.35f;
                float hatT = (t + 0.25f) % 0.5f;
                float hat = nz * Mathf.Exp(-hatT * 90) * 0.12f;
                data[i] = Mathf.Clamp((bass * 0.5f + arp + kick * 0.6f + snare + hat) * 0.7f, -1f, 1f);
            }
            var c = AudioClip.Create("music", n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }
        static float Midi(int m) => 440f * Mathf.Pow(2f, (m - 69) / 12f);

        AudioClip[] _clips;
        AudioSource[] _pool;
        int _next;
        float[] _last;
        const int Rate = 44100;

        void Awake()
        {
            I = this;
            _clips = new AudioClip[15];
            _clips[(int)Id.Shot] = Make("shot", 0.07f, (t, i) => Noise(i) * Env(t, 0.002f, 0.06f) * 0.35f + Sq(t, 900 - t * 6000) * Env(t, 0.001f, 0.04f) * 0.15f);
            _clips[(int)Id.Pop] = Make("pop", 0.09f, (t, i) => Sine(t, 520 - t * 3000) * Env(t, 0.002f, 0.08f) * 0.5f + Noise(i) * Env(t, 0.001f, 0.03f) * 0.2f);
            _clips[(int)Id.GateHit] = Make("gatehit", 0.06f, (t, i) => Sq(t, 220) * Env(t, 0.001f, 0.05f) * 0.18f + Noise(i) * Env(t, 0.001f, 0.02f) * 0.15f);
            _clips[(int)Id.GateBreak] = Make("gatebreak", 0.45f, (t, i) => (Sine(t, 523) * Step(t, 0) + Sine(t, 659) * Step(t, 0.08f) + Sine(t, 784) * Step(t, 0.16f) + Sine(t, 1046) * Step(t, 0.24f)) * Env(t, 0.005f, 0.42f) * 0.3f);
            _clips[(int)Id.Gain] = Make("gain", 0.18f, (t, i) => Sine(t, 880 + t * 2400) * Env(t, 0.003f, 0.17f) * 0.35f);
            _clips[(int)Id.Hurt] = Make("hurt", 0.16f, (t, i) => (Sine(t, 140 - t * 300) * 0.6f + Noise(i) * 0.3f) * Env(t, 0.002f, 0.15f) * 0.5f);
            _clips[(int)Id.BossHit] = Make("bosshit", 0.1f, (t, i) => (Sq(t, 110) * 0.3f + Noise(i) * 0.4f) * Env(t, 0.001f, 0.09f) * 0.4f);
            _clips[(int)Id.BossDie] = Make("bossdie", 0.9f, (t, i) => (Noise(i) * 0.5f + Sine(t, 70 - t * 30) * 0.8f) * Env(t, 0.005f, 0.88f) * 0.6f);
            _clips[(int)Id.Win] = Make("win", 1.0f, (t, i) => (Sine(t, 523) * Step(t, 0) + Sine(t, 659) * Step(t, 0.15f) + Sine(t, 784) * Step(t, 0.3f) + Sine(t, 1046) * Step(t, 0.45f)) * Env(t, 0.01f, 0.98f) * 0.28f);
            _clips[(int)Id.Lose] = Make("lose", 0.9f, (t, i) => (Sine(t, 392 - t * 150) + Sine(t, 311 - t * 120)) * Env(t, 0.01f, 0.88f) * 0.25f);
            _clips[(int)Id.Click] = Make("click", 0.05f, (t, i) => Sine(t, 1200) * Env(t, 0.001f, 0.045f) * 0.3f);
            _clips[(int)Id.Coin] = Make("coin", 0.25f, (t, i) => (Sine(t, 1318) * Step(t, 0) + Sine(t, 1760) * Step(t, 0.07f)) * Env(t, 0.002f, 0.24f) * 0.25f);
            _clips[(int)Id.Upgrade] = Make("upgrade", 0.6f, (t, i) => (Sine(t, 440 + t * 1400) * 0.5f + Sine(t, 660 + t * 2100) * 0.3f) * Env(t, 0.01f, 0.58f) * 0.3f + Noise(i) * Env(t, 0.001f, 0.25f) * 0.08f);
            _clips[(int)Id.Tile] = Make("tile", 0.08f, (t, i) => (Sine(t, 1568) * 0.6f + Sine(t, 2093) * 0.4f) * Env(t, 0.001f, 0.075f) * 0.25f);
            _clips[(int)Id.Inflate] = Make("inflate", 0.35f, (t, i) => (Noise(i) * 0.4f * (1 - t / 0.35f) + Sine(t, 180 + t * 900) * 0.5f) * Env(t, 0.02f, 0.33f) * 0.25f);
            _last = new float[_clips.Length];
            _music = gameObject.AddComponent<AudioSource>();
            _music.clip = MakeMusic(); _music.loop = true; _music.volume = 0.22f; _music.playOnAwake = false;
            _pool = new AudioSource[16];
            for (int k = 0; k < _pool.Length; k++) { _pool[k] = gameObject.AddComponent<AudioSource>(); _pool[k].playOnAwake = false; }
        }

        /// <summary>Plays a sound; minGap throttles very frequent sounds (shots, pops).</summary>
        public void Play(Id id, float volume = 1f, float pitch = 1f, float minGap = 0f)
        {
            if (!Enabled) return;
            float now = Time.unscaledTime;
            if (now - _last[(int)id] < minGap) return;
            _last[(int)id] = now;
            var s = _pool[_next]; _next = (_next + 1) % _pool.Length;
            s.clip = _clips[(int)id]; s.volume = volume; s.pitch = pitch; s.Play();
        }

        delegate float Gen(float t, int i);
        static AudioClip Make(string name, float seconds, Gen g)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(g((float)i / Rate, i), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static uint _seed = 12345;
        static float Noise(int i) { _seed = _seed * 1664525u + 1013904223u; return ((_seed >> 9) / 4194304f) - 1f; }
        static float Sine(float t, float f) => Mathf.Sin(2 * Mathf.PI * f * t);
        static float Sq(float t, float f) => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * t));
        static float Step(float t, float at) => t >= at ? 1f : 0f;
        static float Env(float t, float a, float d) => t < a ? t / a : Mathf.Clamp01(1f - (t - a) / Mathf.Max(0.0001f, d - a));
    }
}

using UnityEngine;

namespace Plasma
{
    /// <summary>Short vibration pulses on Android (Handheld.Vibrate is ~400 ms - far too long for game feel).</summary>
    public static class Haptics
    {
        public static bool Enabled = true;
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static bool _init;
        static float _last;
        public static void Pulse(long ms)
        {
            if (!Enabled || Time.unscaledTime - _last < 0.06f) return;
            _last = Time.unscaledTime;
            try
            {
                if (!_init)
                {
                    _init = true;
                    using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = act.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                _vibrator?.Call("vibrate", ms);
            }
            catch { _vibrator = null; }
        }
#else
        public static void Pulse(long ms) { }
#endif
    }
}

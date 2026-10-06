using UnityEngine;
using UnityEngine.Rendering;

namespace Plasma
{
    /// <summary>
    /// Two graphics levels, switchable in Settings and picked automatically on first run:
    ///  * High: real-time directional shadows (squad, boss, tiles, gate on the deck), HDR camera + bloom,
    ///          HDR-boosted tracers/sparks, 3D horde models out to LodZ.
    ///  * Low:  blob shadows, no post-processing, 3D horde models only for the front rows.
    /// A frame-rate watchdog (Game.cs) drops to Low once if a phone can't hold ~38 fps, unless the player chose.
    /// </summary>
    public static class QualityManager
    {
        public static bool High { get; private set; } = true;

        /// <summary>Direction *towards* the shadow light: from behind-left, so shadows fall towards the camera and to the right like the reference.</summary>
        public static readonly Vector3 ShadowLightDir = new Vector3(-0.42f, 0.82f, 0.38f).normalized;

        static Light _light;
        static PlasmaBloom _bloom;

        /// <summary>Heuristic for the first launch: mid/high-end phones get High.</summary>
        public static bool AutoHigh()
        {
            if (Application.isEditor || !Application.isMobilePlatform) return true;
            return SystemInfo.systemMemorySize >= 3500 && SystemInfo.processorCount >= 6 && SystemInfo.graphicsShaderLevel >= 35;
        }

        public static void Apply(bool high, Camera cam)
        {
            High = high;
            if (_light == null)
            {
                _light = new GameObject("ShadowLight").AddComponent<Light>();
                _light.type = LightType.Directional;
                _light.transform.rotation = Quaternion.LookRotation(-ShadowLightDir, Vector3.forward);
                _light.color = Color.white; _light.intensity = 1f;
                _light.shadowStrength = 1f;          // Plasma/Lit tints shadows itself (_ShadowTint)
                _light.shadowBias = 0.04f; _light.shadowNormalBias = 0.3f;
                _light.renderMode = LightRenderMode.ForcePixel;
                Object.DontDestroyOnLoad(_light.gameObject);
            }
            _light.shadows = high ? LightShadows.Soft : LightShadows.None;
            QualitySettings.shadows = high ? ShadowQuality.All : ShadowQuality.Disable;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadowDistance = 34f;
            QualitySettings.pixelLightCount = 1;

            if (cam != null)
            {
                cam.allowHDR = high;
                if (_bloom == null) _bloom = cam.GetComponent<PlasmaBloom>();
                if (_bloom == null) _bloom = cam.gameObject.AddComponent<PlasmaBloom>();
                _bloom.enabled = high;
            }
            Shader.SetGlobalFloat("_PlasmaGlowBoost", high ? 1.4f : 0f);
        }
    }
}

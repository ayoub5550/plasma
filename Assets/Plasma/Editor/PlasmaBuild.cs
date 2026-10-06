using System;
using System.IO;
using System.Linq;
using Plasma.Sim;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Plasma.EditorTools
{
    /// <summary>
    /// Project setup + build entry points. Everything is callable from the "Plasma" menu or headless:
    ///   Unity -batchmode -nographics -projectPath . -buildTarget Android -executeMethod Plasma.EditorTools.PlasmaBuild.BuildAndroid -quit
    /// Env vars: PLASMA_VERSION (default 0.1.0), PLASMA_VERSION_CODE (default 1),
    ///           PLASMA_KEYSTORE / PLASMA_KEYSTORE_PASS / PLASMA_KEYALIAS / PLASMA_KEYALIAS_PASS (store build only).
    /// </summary>
    public static class PlasmaBuild
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string BundleId = "com.ayoubteke.plasma";
        const string IconPath = "Assets/Plasma/Branding/icon.png";

        static string Env(string k, string d) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }

        [MenuItem("Plasma/1. Setup project (settings + scene)")]
        public static void Setup()
        {
            ConfigurePlayerSettings();
            EnsureScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Plasma] Setup done");
        }

        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Ayoub Teke";
            PlayerSettings.productName = "Plasma";
            PlayerSettings.bundleVersion = Env("PLASMA_VERSION", "0.1.0");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleId);
            PlayerSettings.Android.bundleVersionCode = int.Parse(Env("PLASMA_VERSION_CODE", "1"));
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)int.Parse(Env("PLASMA_TARGET_SDK", "36"));
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.03f, 0.07f, 0.14f);
            PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 1170;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false;
            QualitySettings.antiAliasing = 2;
            ApplyIcon();
            KeepInstancingVariants();
        }

        /// <summary>
        /// Materials are created at runtime, so Unity's default "strip unused instancing variants" removes the
        /// GPU-instancing shader variants and every DrawMeshInstanced call silently renders nothing. Keep them all.
        /// </summary>
        static void KeepInstancingVariants()
        {
            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (gs == null || gs.Length == 0) return;
            var so = new SerializedObject(gs[0]);
            var p = so.FindProperty("m_InstancingStripping");
            if (p != null && p.intValue != 2) { p.intValue = 2; so.ApplyModifiedProperties(); Debug.Log("[Plasma] instancing stripping = KeepAll"); }
        }

        static void ApplyIcon()
        {
            if (!File.Exists(IconPath)) return;
            AssetDatabase.ImportAsset(IconPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            if (imp != null && (imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled))
            {
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.mipmapEnabled = false; imp.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (tex == null) return;
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { tex });
            // Android adaptive + round + legacy slots all use the same art
            foreach (var kind in new[] { UnityEditor.Android.AndroidPlatformIconKind.Legacy, UnityEditor.Android.AndroidPlatformIconKind.Round })
            {
                var icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
                foreach (var ic in icons) ic.SetTexture(tex);
                PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, kind, icons);
            }
            var adaptive = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Plasma/Branding/icon_bg.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Plasma/Branding/icon_fg.png");
            if (bg != null && fg != null)
            {
                foreach (var ic in adaptive) { ic.SetTexture(bg, 0); ic.SetTexture(fg, 1); }
                PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, adaptive);
            }
        }

        /// <summary>Main.unity contains just a camera and the Game component; everything else is built in code.</summary>
        public static void EnsureScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();
            new GameObject("Game").AddComponent<Game>();
            RenderSettings.ambientLight = Color.white;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void Build(BuildTarget target, string path, BuildOptions opts = BuildOptions.None)
        {
            Setup();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = path, target = target, options = opts });
            var s = report.summary;
            Debug.Log($"[Plasma] BUILD RESULT {s.result} target={target} size={s.totalSize} errors={s.totalErrors} time={s.totalTime} -> {path}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Plasma/Build Android APK (test)")]
        public static void BuildAndroid()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.useCustomKeystore = false;
            Build(BuildTarget.Android, "Builds/Plasma.apk");
        }

        /// <summary>Google Play bundle signed with the upload key from env vars. Keystore fields are cleared afterwards.</summary>
        [MenuItem("Plasma/Build Android AAB (store)")]
        public static void BuildAndroidStore()
        {
            string ks = Env("PLASMA_KEYSTORE", "");
            if (string.IsNullOrEmpty(ks) || !File.Exists(ks)) { Debug.LogError("[Plasma] PLASMA_KEYSTORE not set / missing"); if (Application.isBatchMode) EditorApplication.Exit(2); return; }
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks;
            PlayerSettings.Android.keystorePass = Env("PLASMA_KEYSTORE_PASS", "");
            PlayerSettings.Android.keyaliasName = Env("PLASMA_KEYALIAS", "plasma");
            PlayerSettings.Android.keyaliasPass = Env("PLASMA_KEYALIAS_PASS", Env("PLASMA_KEYSTORE_PASS", ""));
            try { Build(BuildTarget.Android, "Builds/Plasma.aab"); }
            finally
            {
                PlayerSettings.Android.keystoreName = ""; PlayerSettings.Android.keystorePass = ""; PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.useCustomKeystore = false; EditorUserBuildSettings.buildAppBundle = false; AssetDatabase.SaveAssets();
            }
        }

        /// <summary>Linux player used for rendered screenshots / gameplay video in a GPU-less sandbox (see TESTING.md).</summary>
        [MenuItem("Plasma/Build Linux player (capture)")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/linux/Plasma.x86_64");

        /// <summary>Writes the career balance report (same code as tools/simharness).</summary>
        [MenuItem("Plasma/Balance sweep (report)")]
        public static void Sweep()
        {
            Directory.CreateDirectory("Builds/sim");
            foreach (var skill in new[] { 0.3f, 0.6f, 1f })
                File.WriteAllText($"Builds/sim/career_{skill:0.0}.md", BalanceSweep.Career(100, skill));
            Debug.Log("[Plasma] sweep written to Builds/sim");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}

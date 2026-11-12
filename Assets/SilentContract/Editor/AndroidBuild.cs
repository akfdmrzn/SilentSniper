using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilentContract.EditorTools
{
    /// <summary>
    /// One click Android APK (a test build to send to a friend: install it on the phone with "unknown sources"). Builds the
    /// game content for Android, applies the player settings (IL2CPP, ARM64, landscape, Vulkan + OpenGL ES 3) and writes
    /// Builds/Android/SilentContract.apk, signed with Unity's debug key. The store / ad / analytics SDKs stay switched off
    /// on Android for now (test store and placeholder ads, like the iOS test build).
    /// </summary>
    public static class AndroidBuild
    {
        public const string OutputDir = "Builds/Android";
        public const string ApkName = "SilentContract.apk";

        [MenuItem("Silent Contract/Android APK Al", priority = 10)]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[SC] Android Build Support modülü kurulu değil.");
                if (Application.isBatchMode) { EditorApplication.Exit(2); return; }
                EditorUtility.DisplayDialog("Silent Contract",
                    "Unity'de 'Android Build Support' modülü kurulu değil.\n\nUnity Hub → Installs → Unity 6 yanındaki ⚙ → Add modules → " +
                    "'Android Build Support' (OpenJDK ve Android SDK & NDK Tools ile) işaretle ve kur.", "Tamam");
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            SilentContractSetup.EnsureProjectSettings();
            try { SdkSetup.Apply(true); } catch (Exception e) { Debug.LogException(e); }
            ApplyPlayerSettings();

            if (SilentContractSetup.ContentOutdated() || Array.IndexOf(Environment.GetCommandLineArgs(), "-rebuildContent") >= 0)
            {
                if (!AssetPipeline.BuildAll())
                {
                    Debug.LogError("[SC] content build failed");
                    if (Application.isBatchMode) { EditorApplication.Exit(3); return; }
                }
                SilentContractSetup.MarkContentBuilt();
            }
            SilentContractSetup.EnsureScene();

            Directory.CreateDirectory(OutputDir);
            string apk = Path.Combine(OutputDir, ApkName);
            EditorUserBuildSettings.buildAppBundle = false;            // an .apk, not the Play Store's .aab
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.development = false;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SilentContractSetup.ScenePath },
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log("[SC] Android build: " + report.summary.result + " → " + Path.GetFullPath(apk) +
                      "  (" + (report.summary.totalSize / (1024 * 1024)) + " MB, " + report.summary.totalTime.TotalMinutes.ToString("0.0") + " dk)");
            if (Application.isBatchMode) { EditorApplication.Exit(ok ? 0 : 1); return; }
            if (ok) EditorUtility.RevealInFinder(apk);
            EditorUtility.DisplayDialog("Silent Contract", ok ? "APK hazır:\n" + Path.GetFullPath(apk) : "Build başarısız: " + report.summary.result + "\nConsole'daki kırmızı satırlara bak.", "Tamam");
        }

        static void ApplyPlayerSettings()
        {
            var t = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Akif";
            PlayerSettings.productName = "Silent Sniper";
            PlayerSettings.SetApplicationIdentifier(t, "com.akif.silentcontract");
            PlayerSettings.bundleVersion = "2.0";
            PlayerSettings.Android.bundleVersionCode = Mathf.Max(PlayerSettings.Android.bundleVersionCode, 20);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.Minimal);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.Android.useCustomKeystore = false;            // Unity's debug key: fine for a test APK
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = false;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SilentContract/Art/UI/app_icon.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }
    }
}

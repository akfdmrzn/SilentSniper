using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilentContract.EditorTools
{
    /// <summary>
    /// One click iPhone build: builds the game content (prefabs/materials/animations), applies release player
    /// settings (IL2CPP, Metal, linear colour, landscape, no development console) and writes the Xcode project
    /// to Builds/iOS (or Builds/iOS-Simulator with -simulator). "-autotest" adds the SC_AUTOTEST define, which
    /// makes the app play itself and save screenshots of every screen.
    /// </summary>
    public static class IOSBuild
    {
        static bool simulatorOverride, autotestOverride;
        static bool HasArg(string a) => Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0;
        static bool Simulator => simulatorOverride || HasArg("-simulator");
        static bool AutoTest => autotestOverride || HasArg("-autotest");
        static string OutputPath => Simulator ? "Builds/iOS-Simulator" : "Builds/iOS";

        [MenuItem("Silent Contract/iPhone Build Al (Xcode projesi)", priority = 0)]
        public static void BuildMenu() => Build();

        [MenuItem("Silent Contract/iOS Simulator Build Al (Xcode projesi)", priority = 1)]
        public static void BuildSimulator()
        {
            simulatorOverride = true;
            try { Build(); } finally { simulatorOverride = false; }
        }

        [MenuItem("Silent Contract/iOS Simulator Otomatik Test Build", priority = 3)]
        public static void BuildSimulatorAutoTest()
        {
            simulatorOverride = true;
            autotestOverride = true;
            try { Build(); } finally { simulatorOverride = false; autotestOverride = false; }
        }

        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                Debug.LogError("[SC] iOS Build Support modülü kurulu değil.");
                if (Application.isBatchMode) { EditorApplication.Exit(2); return; }
                EditorUtility.DisplayDialog("Silent Contract",
                    "Unity'de 'iOS Build Support' modülü kurulu değil.\n\nUnity Hub → Installs → Unity 6 yanındaki ⚙ → Add modules → 'iOS Build Support' işaretle ve kur.", "Tamam");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

            SilentContractSetup.EnsureProjectSettings();
            ApplyPlayerSettings();
            ConfigureGraphics();
            SetDefines();

            // content (prefabs, materials, animation bank) — rebuilt when the art changed
            if (SilentContractSetup.ContentOutdated() || HasArg("-rebuildContent"))
            {
                if (!AssetPipeline.BuildAll())
                {
                    Debug.LogError("[SC] content build failed");
                    if (Application.isBatchMode) { EditorApplication.Exit(3); return; }
                }
                SilentContractSetup.MarkContentBuilt();
            }
            SilentContractSetup.EnsureScene();

            Directory.CreateDirectory(OutputPath);
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.allowDebugging = false;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SilentContractSetup.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log("[SC] Build sonucu: " + report.summary.result + " → " + Path.GetFullPath(OutputPath) +
                      "  (" + (report.summary.totalSize / (1024 * 1024)) + " MB, " + report.summary.totalTime.TotalMinutes.ToString("0.0") + " dk)");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
                return;
            }
            if (ok)
            {
                EditorUtility.RevealInFinder(Path.Combine(OutputPath, "Unity-iPhone.xcodeproj"));
                EditorUtility.DisplayDialog("Silent Contract",
                    "Xcode projesi hazır: " + Path.GetFullPath(OutputPath) +
                    "\n\n1) Unity-iPhone.xcodeproj dosyasını aç\n2) Signing & Capabilities → Team seç\n3) iPhone'u seç → ▶ Run", "Tamam");
            }
            else EditorUtility.DisplayDialog("Silent Contract", "Build başarısız: " + report.summary.result + "\nConsole'daki kırmızı satırlara bak.", "Tamam");
        }

        static void SetDefines()
        {
            // the SDK defines first (RevenueCat, AdMob, Firebase, Meta: on when their package is installed)
            try { SdkSetup.Apply(true); } catch (Exception e) { Debug.LogException(e); }
            var target = NamedBuildTarget.iOS;
            var defs = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(d => !string.IsNullOrWhiteSpace(d) && d != "SC_AUTOTEST").ToList();
            if (AutoTest) defs.Add("SC_AUTOTEST");
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defs));
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Akif";
            PlayerSettings.productName = "Silent Sniper";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.akif.silentcontract");
            PlayerSettings.bundleVersion = "1.1";      // App Store Connect: version 1.1
            PlayerSettings.iOS.buildNumber = "26";
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.sdkVersion = Simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = true;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.iOS.appInBackgroundBehavior = iOSAppInBackgroundBehavior.Suspend;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Minimal);
            PlayerSettings.stripEngineCode = false;          // never strip MeshCollider & co. (see link.xml too)
            PlayerSettings.bakeCollisionMeshes = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            // landscape only
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.runInBackground = false;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SilentContract/Art/UI/app_icon.png");
            if (icon != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            }
        }

        /// <summary>Keeps the exp² fog variants (fog is only switched on at runtime) and instancing variants.</summary>
        static void ConfigureGraphics()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            void SetInt(string name, int v) { var p = so.FindProperty(name); if (p != null) p.intValue = v; }
            void SetBool(string name, bool v) { var p = so.FindProperty(name); if (p != null) p.boolValue = v; }
            SetInt("m_FogStripping", 1);       // custom
            SetBool("m_FogKeepLinear", false);
            SetBool("m_FogKeepExp", false);
            SetBool("m_FogKeepExp2", true);
            SetInt("m_LightmapStripping", 1);  // custom: no lightmaps in this game
            SetBool("m_LightmapKeepPlain", false);
            SetBool("m_LightmapKeepDirCombined", false);
            SetBool("m_LightmapKeepDynamicPlain", false);
            SetBool("m_LightmapKeepDynamicDirCombined", false);
            SetBool("m_LightmapKeepShadowMask", false);
            SetBool("m_LightmapKeepSubtractive", false);
            SetInt("m_InstancingStripping", 0);
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}

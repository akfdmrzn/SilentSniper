using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SilentContract.EditorTools
{
    /// <summary>
    /// Switches the game's SDK code on by itself: when a package is installed its scripting define is added
    /// (SC_RC RevenueCat, SC_ADMOB Google Mobile Ads, SC_FIREBASE Firebase + GoogleService-Info.plist, SC_FB Meta), when
    /// it is missing the define goes, so the project always compiles. Also writes the ids from MonetizationConfig into
    /// the SDKs' own settings assets (AdMob app id, tracking text, Meta app id).
    /// Lives in its own assembly: it keeps working even while the game scripts do not compile.
    /// </summary>
    [InitializeOnLoad]
    public static class SdkSetup
    {
        static readonly string[] Managed = { "SC_RC", "SC_ADMOB", "SC_FIREBASE", "SC_FB", "SC_IAP" };

        static SdkSetup()
        {
            EditorApplication.delayCall += () => { try { Apply(false); } catch (Exception e) { Debug.LogException(e); } };
        }

        [MenuItem("Silent Contract/SDK Ayarlarını Uygula", priority = 40)]
        public static void ApplyMenu() => Apply(true);

        static bool HasType(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { if (a.GetType(fullName, false) != null) return true; } catch { }
            }
            return false;
        }

        static Type FindType(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = a.GetType(fullName, false); if (t != null) return t; } catch { }
            }
            return null;
        }

        public static string FirebasePlist()
        {
            foreach (var f in Directory.GetFiles("Assets", "GoogleService-Info*.plist", SearchOption.AllDirectories))
                return f.Replace('\\', '/');
            return null;
        }

        /// <summary>A public constant of the game's MonetizationConfig (read by reflection: no compile-time link).</summary>
        static string Config(string name)
        {
            var t = FindType("SilentContract.MonetizationConfig");
            var f = t != null ? t.GetField(name, BindingFlags.Public | BindingFlags.Static) : null;
            if (f == null) return null;
            try { return (f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null)) as string; } catch { return null; }
        }

        public static void Apply(bool verbose)
        {
            var want = new List<string>();
            if (HasType("Purchases")) want.Add("SC_RC");
            if (HasType("GoogleMobileAds.Api.MobileAds")) want.Add("SC_ADMOB");
            if (HasType("Firebase.Analytics.FirebaseAnalytics") && FirebasePlist() != null) want.Add("SC_FIREBASE");
            if (HasType("Facebook.Unity.FB") && !string.IsNullOrEmpty(Config("FacebookAppId"))) want.Add("SC_FB");

            foreach (var target in new[] { NamedBuildTarget.iOS, NamedBuildTarget.Standalone, NamedBuildTarget.Android })
            {
                var defs = PlayerSettings.GetScriptingDefineSymbols(target).Split(';')
                    .Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
                // Android (for now a test APK): no store / ad / analytics code — those SDKs are set up for iOS first
                var add = target == NamedBuildTarget.Android ? new List<string>() : want;
                var next = defs.Where(d => Array.IndexOf(Managed, d) < 0).Concat(add).Distinct().ToList();
                if (!next.OrderBy(x => x).SequenceEqual(defs.OrderBy(x => x)))
                {
                    PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", next));
                    Debug.Log("[SC] SDK tanımları (" + target.TargetName + "): " + (want.Count > 0 ? string.Join(", ", want) : "yok"));
                }
            }
            ConfigureAdMob();
            ConfigureFacebook();
            if (verbose)
                Debug.Log("[SC] SDK durumu: RevenueCat " + (want.Contains("SC_RC") ? "✓" : "—") + ", AdMob " + (want.Contains("SC_ADMOB") ? "✓" : "—") +
                          ", Firebase " + (want.Contains("SC_FIREBASE") ? "✓" : (HasType("Firebase.Analytics.FirebaseAnalytics") ? "paket var, GoogleService-Info.plist yok" : "—")) +
                          ", Meta " + (want.Contains("SC_FB") ? "✓" : "—"));
        }

        /// <summary>AdMob's settings asset: the iOS app id (without it the app stops at launch) and the tracking text.</summary>
        static void ConfigureAdMob()
        {
            var t = FindType("GoogleMobileAds.Editor.GoogleMobileAdsSettings");
            if (t == null) return;
            string appId = Config("AdMobAppIdIOS"), att = Config("TrackingUsageTR");
            if (string.IsNullOrEmpty(appId)) return;
            var load = t.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var inst = load != null ? load.Invoke(null, null) as ScriptableObject : null;
            if (inst == null) return;
            var so = new SerializedObject(inst);
            bool changed = false;
            void Set(string field, string v)
            {
                var p = so.FindProperty(field);
                if (p != null && v != null && p.stringValue != v) { p.stringValue = v; changed = true; }
            }
            Set("adMobIOSAppId", appId);
            // the AdMob plugin refuses an Android build without an app id: Google's test app id until Android is set up
            var andr = so.FindProperty("adMobAndroidAppId");
            if (andr != null && string.IsNullOrEmpty(andr.stringValue)) { andr.stringValue = "ca-app-pub-3940256099942544~3347511713"; changed = true; }
            Set("userTrackingUsageDescription", att);
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(inst);
                AssetDatabase.SaveAssets();
                Debug.Log("[SC] AdMob ayarları yazıldı: " + appId);
            }
        }

        /// <summary>Meta's settings asset (app id, client token): read by its own iOS build step for Info.plist.</summary>
        static void ConfigureFacebook()
        {
            var t = FindType("Facebook.Unity.Settings.FacebookSettings");
            if (t == null) return;
            string id = Config("FacebookAppId"), token = Config("FacebookClientToken");
            if (string.IsNullOrEmpty(id)) return;
            try
            {
                var instProp = t.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var inst = instProp != null ? instProp.GetValue(null) as ScriptableObject : null;
                const string path = "Assets/FacebookSDK/SDK/Resources/FacebookSettings.asset";
                var onDisk = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (onDisk != null) inst = onDisk;
                if (inst == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(inst)))
                {
                    // the settings must be an asset in Resources (the game reads them at run time, Meta's iOS step at build)
                    Directory.CreateDirectory("Assets/FacebookSDK/SDK/Resources");
                    if (inst == null) inst = ScriptableObject.CreateInstance(t);
                    AssetDatabase.CreateAsset(inst, path);
                }
                var so = new SerializedObject(inst);
                bool changed = false;
                void SetList(string field, string v)
                {
                    var p = so.FindProperty(field);
                    if (p == null || !p.isArray) return;
                    if (p.arraySize == 1 && p.GetArrayElementAtIndex(0).stringValue == v) return;
                    p.arraySize = 1;
                    p.GetArrayElementAtIndex(0).stringValue = v;
                    changed = true;
                }
                SetList("appIds", id);
                SetList("clientTokens", token ?? "");
                SetList("appLabels", "Silent Sniper");
                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(inst);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[SC] Meta ayarları yazıldı: " + id);
                }
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>Every build runs the SDK check first (a define that changed only takes effect after the next compile:
    /// the build log then says so).</summary>
    public class SdkPreBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) => SdkSetup.Apply(true);
    }
}

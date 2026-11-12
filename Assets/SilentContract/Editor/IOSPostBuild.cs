#if UNITY_IOS
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace SilentContract.EditorTools
{
    /// <summary>
    /// The Xcode project's store settings: In-App Purchase capability, the tracking (ATT) text, the ad networks' SKAdNetwork
    /// ids (Google's come from the AdMob plugin, Meta's are added here), Firebase's SKAdNetwork conversion support, the
    /// export-compliance flag (no prompt in App Store Connect) and the frameworks the native code links.
    /// </summary>
    public static class IOSPostBuild
    {
        static readonly string[] MetaSkan = { "v9wttpbfk9.skadnetwork", "n38lu8286q.skadnetwork" };

        /// <summary>Firebase DebugView for builds run from Xcode (the store build sends its events normally either way).</summary>
        static bool DebugEvents => true;

        [PostProcessBuild(900)]
        public static void OnPostProcess(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            Plist(path);
            Project(path);
            Localizations(path);
            PrivacyManifest(path);
            if (DebugEvents) DebugScheme(path);
        }

        /// <summary>
        /// ITMS-91064 (App Store, builds 21 and 22): App Store Connect rejects UnityFramework's merged manifest unless its
        /// tracking keys agree — NSPrivacyTracking true needs a non-empty NSPrivacyTrackingDomains (true with none was
        /// build 22's rejection), domains need tracking true, and even an empty domains key with tracking false is refused.
        /// UnityFramework's own code reaches no tracking domain (the Meta frameworks declare theirs, ep1.facebook.com, in
        /// their own manifests), so it is declared as not tracking, with no domains key at all.
        /// </summary>
        static void PrivacyManifest(string path)
        {
            foreach (var f in new[] { Path.Combine(path, "UnityFramework/PrivacyInfo.xcprivacy"), Path.Combine(path, "PrivacyInfo.xcprivacy") })
            {
                if (!File.Exists(f)) continue;
                try
                {
                    var doc = new PlistDocument();
                    doc.ReadFromFile(f);
                    var root = doc.root;
                    if (root.values.ContainsKey("NSPrivacyTrackingDomains")) root.values.Remove("NSPrivacyTrackingDomains");
                    root.SetBoolean("NSPrivacyTracking", false);
                    doc.WriteToFile(f);
                    Debug.Log("[SC] privacy manifest checked: " + f);
                }
                catch (System.Exception e) { Debug.LogError("[SC] privacy manifest " + f + ": " + e.Message); }
            }
        }

        /// <summary>
        /// The pods (AdMob, Google's consent form, Meta) are built with the game's iOS version (15.0): some ship 12.0 in their
        /// podspecs, which the current Xcode refuses. Runs after the resolver wrote the Podfile (40) and Meta edited it (45),
        /// before the pods are installed (50).
        /// </summary>
        [PostProcessBuild(46)]
        public static void PodfileDeploymentTarget(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string podfile = Path.Combine(path, "Podfile");
            if (!File.Exists(podfile)) return;
            string s = File.ReadAllText(podfile);
            if (s.Contains("SC_DEPLOYMENT_TARGET")) return;
            if (s.Contains("post_install do"))
            {
                Debug.LogWarning("[SC] Podfile'da başka bir post_install var: dağıtım hedefi eklenmedi");
                return;
            }
            s += "\n# SC_DEPLOYMENT_TARGET: every pod builds for iOS 15.0 (some podspecs still say 12.0)\n" +
                 "post_install do |installer|\n" +
                 "  installer.pods_project.targets.each do |t|\n" +
                 "    t.build_configurations.each do |c|\n" +
                 "      c.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'\n" +
                 "    end\n" +
                 "  end\n" +
                 "end\n";
            File.WriteAllText(podfile, s);
            Debug.Log("[SC] Podfile: pod'ların iOS hedefi 15.0");
        }

        /// <summary>Test builds run from Xcode with Firebase's debug mode: every event shows up within seconds in Firebase →
        /// DebugView (release builds send in batches, the reports fill within minutes / a day).</summary>
        static void DebugScheme(string path)
        {
            string scheme = Path.Combine(path, "Unity-iPhone.xcodeproj/xcshareddata/xcschemes/Unity-iPhone.xcscheme");
            if (!File.Exists(scheme)) { Debug.LogWarning("[SC] Xcode şeması bulunamadı: " + scheme); return; }
            string x = File.ReadAllText(scheme);
            if (x.Contains("-FIRDebugEnabled")) return;
            const string arg = "\n      <CommandLineArguments>\n         <CommandLineArgument argument = \"-FIRDebugEnabled\" isEnabled = \"YES\">\n         </CommandLineArgument>\n      </CommandLineArguments>";
            int i = x.IndexOf("<LaunchAction", System.StringComparison.Ordinal);
            int close = i >= 0 ? x.IndexOf(">", i, System.StringComparison.Ordinal) : -1;
            if (close < 0) return;
            if (x[close - 1] == '/')
                x = x.Substring(0, close - 1) + ">" + arg + "\n   </LaunchAction>" + x.Substring(close + 1);
            else
                x = x.Substring(0, close + 1) + arg + x.Substring(close + 1);
            File.WriteAllText(scheme, x);
            Debug.Log("[SC] Xcode şeması: -FIRDebugEnabled (Firebase DebugView)");
        }

        /// <summary>The game's 16 languages: the App Store lists the languages it finds as .lproj folders in the app, and
        /// the tracking (ATT) question is asked in the phone's language.</summary>
        static readonly string[][] Langs =
        {
            new[] { "en", "Used to show you more relevant ads and keep the game free to play." },
            new[] { "tr", "Sana ilgi alanına uygun reklamlar gösterebilmemiz ve oyunu ücretsiz tutabilmemiz için kullanılır." },
            new[] { "de", "Wird verwendet, um dir relevantere Werbung zu zeigen und das Spiel kostenlos zu halten." },
            new[] { "fr", "Utilisé pour te montrer des publicités plus pertinentes et garder le jeu gratuit." },
            new[] { "es", "Se usa para mostrarte anuncios más relevantes y mantener el juego gratis." },
            new[] { "pt-BR", "Usado para mostrar anúncios mais relevantes para você e manter o jogo gratuito." },
            new[] { "it", "Serve a mostrarti annunci più pertinenti e a mantenere il gioco gratuito." },
            new[] { "ru", "Используется, чтобы показывать вам более подходящую рекламу и сохранять игру бесплатной." },
            new[] { "ja", "より関連性の高い広告を表示し、ゲームを無料で提供し続けるために使用されます。" },
            new[] { "ko", "더 관련성 높은 광고를 보여 드리고 게임을 무료로 유지하기 위해 사용됩니다." },
            new[] { "zh-Hant", "用於向你顯示更相關的廣告，並讓遊戲保持免費。" },
            new[] { "zh-Hans", "用于向你展示更相关的广告，并让游戏保持免费。" },
            new[] { "id", "Digunakan untuk menampilkan iklan yang lebih relevan dan menjaga game tetap gratis." },
            new[] { "pl", "Służy do wyświetlania trafniejszych reklam i utrzymania gry za darmo." },
            new[] { "nl", "Wordt gebruikt om je relevantere advertenties te tonen en de game gratis te houden." },
            new[] { "vi", "Được dùng để hiển thị quảng cáo phù hợp hơn với bạn và giữ trò chơi miễn phí." },
        };

        static void Localizations(string path)
        {
            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string main = proj.GetUnityMainTargetGuid();
            foreach (var l in Langs)
            {
                string dirName = l[0] + ".lproj";
                string dir = Path.Combine(path, dirName);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "InfoPlist.strings"),
                    "\"NSUserTrackingUsageDescription\" = \"" + l[1].Replace("\"", "\\\"") + "\";\n" +
                    "\"CFBundleDisplayName\" = \"Silent Sniper\";\n", new System.Text.UTF8Encoding(false));
                if (proj.FindFileGuidByProjectPath(dirName) == null)
                {
                    string guid = proj.AddFile(dir, dirName, PBXSourceTree.Source);
                    proj.AddFileToBuild(main, guid);
                }
            }
            proj.WriteToFile(projPath);

            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("CFBundleDevelopmentRegion", "en");
            var arr = plist.root.CreateArray("CFBundleLocalizations");
            foreach (var l in Langs) arr.AddString(l[0]);
            plist.WriteToFile(plistPath);
            Debug.Log("[SC] Xcode: " + Langs.Length + " dil (.lproj + izin metni)");
        }

        static void Plist(string path)
        {
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetString("NSUserTrackingUsageDescription", SilentContract.MonetizationConfig.TrackingUsageTR);
            // Firebase Analytics reports SKAdNetwork conversions to Google Ads
            root.SetBoolean("GOOGLE_ANALYTICS_REGISTRATION_WITH_AD_NETWORK_ENABLED", true);
            // Firebase Analytics on (also the default), the game logs its own screen_view (the automatic one only sees Unity's view)
            root.SetBoolean("FIREBASE_ANALYTICS_COLLECTION_ENABLED", true);
            root.SetBoolean("FirebaseAutomaticScreenReportingEnabled", false);
            root.SetBoolean("FirebaseCrashlyticsCollectionEnabled", true);
            // SKAdNetwork ids: keep what the plugins wrote, add the missing ones
            var have = new HashSet<string>();
            PlistElementArray items = root.values.TryGetValue("SKAdNetworkItems", out var el) ? el as PlistElementArray : null;
            if (items == null) items = root.CreateArray("SKAdNetworkItems");
            foreach (var it in items.values)
            {
                var d = it as PlistElementDict;
                if (d != null && d.values.TryGetValue("SKAdNetworkIdentifier", out var idEl)) have.Add(idEl.AsString().ToLowerInvariant());
            }
            foreach (var id in MetaSkan)
                if (!have.Contains(id)) items.AddDict().SetString("SKAdNetworkIdentifier", id);
            // Meta: the SDK stops the app at launch without its app id in Info.plist (its own build step writes it too)
            string fb = SilentContract.MonetizationConfig.FacebookAppId;
            if (!string.IsNullOrEmpty(fb))
            {
                root.SetString("FacebookAppID", fb);
                root.SetString("FacebookClientToken", SilentContract.MonetizationConfig.FacebookClientToken);
                root.SetString("FacebookDisplayName", "Silent Sniper");
            root.SetString("CFBundleDisplayName", "Silent Sniper");
                // off at launch: the game turns them on after Apple's tracking question (App Review 5.1.2 / ATT)
                root.SetBoolean("FacebookAutoLogAppEventsEnabled", false);
                root.SetBoolean("FacebookAdvertiserIDCollectionEnabled", false);
                PlistElementArray urlTypes = root.values.TryGetValue("CFBundleURLTypes", out var ut) ? ut as PlistElementArray : null;
                if (urlTypes == null) urlTypes = root.CreateArray("CFBundleURLTypes");
                bool hasScheme = false;
                foreach (var e in urlTypes.values)
                {
                    var d = e as PlistElementDict;
                    if (d != null && d.values.TryGetValue("CFBundleURLSchemes", out var sc) && sc is PlistElementArray arr)
                        foreach (var x in arr.values) if (x.AsString() == "fb" + fb) hasScheme = true;
                }
                if (!hasScheme) urlTypes.AddDict().CreateArray("CFBundleURLSchemes").AddString("fb" + fb);
                PlistElementArray q = root.values.TryGetValue("LSApplicationQueriesSchemes", out var qs) ? qs as PlistElementArray : null;
                if (q == null) q = root.CreateArray("LSApplicationQueriesSchemes");
                var haveQ = new HashSet<string>();
                foreach (var x in q.values) haveQ.Add(x.AsString());
                foreach (var sch in new[] { "fbapi", "fb-messenger-share-api" }) if (!haveQ.Contains(sch)) q.AddString(sch);
            }
            plist.WriteToFile(plistPath);
        }

        static void Project(string path)
        {
            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string main = proj.GetUnityMainTargetGuid();
            string fw = proj.GetUnityFrameworkTargetGuid();
            proj.AddFrameworkToProject(fw, "AppTrackingTransparency.framework", true);
            proj.AddFrameworkToProject(fw, "AdServices.framework", true);
            proj.AddFrameworkToProject(fw, "StoreKit.framework", false);
            proj.SetBuildProperty(main, "ENABLE_BITCODE", "NO");
            proj.SetBuildProperty(fw, "ENABLE_BITCODE", "NO");
            // Crashlytics: upload the debug symbols after every build (readable crash reports). Firebase comes in through
            // Swift Package Manager, so the script lives in the package checkout.
            proj.SetBuildProperty(main, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");
            proj.SetBuildProperty(fw, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");
            const string phase = "Crashlytics dSYM";
            if (!File.ReadAllText(projPath).Contains(phase))
                proj.AddShellScriptBuildPhase(main, phase, "/bin/sh",
                    "RUN=\"${BUILD_DIR%/Build/*}/SourcePackages/checkouts/firebase-ios-sdk/Crashlytics/run\"\n" +
                    "[ -x \"$RUN\" ] || RUN=\"${PODS_ROOT}/FirebaseCrashlytics/run\"\n" +
                    "if [ -x \"$RUN\" ]; then \"$RUN\" || echo \"warning: Crashlytics dSYM upload failed\"; else echo \"warning: Crashlytics run script not found\"; fi\n",
                    new List<string>
                    {
                        "${DWARF_DSYM_FOLDER_PATH}/${DWARF_DSYM_FILE_NAME}",
                        "${DWARF_DSYM_FOLDER_PATH}/${DWARF_DSYM_FILE_NAME}/Contents/Resources/DWARF/${PRODUCT_NAME}",
                        "${DWARF_DSYM_FOLDER_PATH}/${DWARF_DSYM_FILE_NAME}/Contents/Info.plist",
                        "$(TARGET_BUILD_DIR)/$(UNLOCALIZED_RESOURCES_FOLDER_PATH)/GoogleService-Info.plist",
                        "$(TARGET_BUILD_DIR)/$(EXECUTABLE_PATH)",
                    });
            proj.WriteToFile(projPath);

            // In-App Purchase capability (the purchases fail without it)
            var cap = new ProjectCapabilityManager(projPath, "Unity-iPhone/silentcontract.entitlements", null, main);
            cap.AddInAppPurchase();
            cap.WriteToFile();
            Debug.Log("[SC] Xcode: In-App Purchase, ATT, SKAdNetwork (" + MetaSkan.Length + " Meta) ayarlandı");
        }
    }
}
#endif

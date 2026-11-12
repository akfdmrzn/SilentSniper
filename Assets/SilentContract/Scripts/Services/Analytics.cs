using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#if SC_FIREBASE
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;
#endif
#if SC_FB
using Facebook.Unity;
#endif

namespace SilentContract
{
    /// <summary>
    /// One door for every game event: Firebase Analytics (funnels, retention, Google Ads conversions), Firebase
    /// Crashlytics (crashes with the last steps before them) and the Meta SDK (install attribution and app events for
    /// Facebook / Instagram campaigns). The SDKs are optional: without them (editor, test builds before the ids are
    /// filled in) events are only written to the log.
    /// Event names follow Firebase's recommended events where one exists (level_start, level_end, level_up,
    /// earn_virtual_currency, spend_virtual_currency, tutorial_begin, tutorial_complete, ad_impression, screen_view).
    /// </summary>
    public static class Analytics
    {
        static bool inited, firebaseReady, fbReady;
        static readonly List<KeyValuePair<string, object[]>> queue = new List<KeyValuePair<string, object[]>>();
        static readonly Dictionary<string, string> userProps = new Dictionary<string, string>();
        /// <summary>Firebase app instance id (RevenueCat links purchases to it); null until known.</summary>
        public static string FirebaseInstanceId { get; private set; }
        public static event Action Ready;
        public static bool LogToConsole = Game.TestBuild;

        public static void Init()
        {
            if (inited) return;
            inited = true;
            Application.logMessageReceived += OnLog;
#if SC_FIREBASE
            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(t =>
                {
                    if (t.IsFaulted || t.Result != DependencyStatus.Available)
                    {
                        Debug.LogWarning("[SC] Firebase hazır değil: " + (t.IsFaulted ? t.Exception?.Message : t.Result.ToString()));
                        return;
                    }
                    try
                    {
                        var app = FirebaseApp.DefaultInstance;
                        Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                        FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                        firebaseReady = true;
                        foreach (var kv in userProps) FirebaseAnalytics.SetUserProperty(kv.Key, kv.Value);
                        ApplyConsent();
                        FlushQueue();
                        Tuning.FetchRemote();
                        FirebaseAnalytics.GetAnalyticsInstanceIdAsync().ContinueWithOnMainThread(id =>
                        {
                            if (!id.IsFaulted && !id.IsCanceled) { FirebaseInstanceId = id.Result; Ready?.Invoke(); }
                        });
                    }
                    catch (Exception e) { Debug.LogException(e); }
                });
            }
            catch (Exception e) { Debug.LogException(e); }
#endif
#if SC_FB
            // Meta waits for Apple's tracking answer on iOS (App Review: nothing that could track before it)
            if (Application.platform != RuntimePlatform.IPhonePlayer || Privacy.AttStatus != 0) StartMeta();
#endif
        }

#if SC_FB
        static bool metaStarted;
        static void StartMeta()
        {
            if (metaStarted || string.IsNullOrEmpty(MonetizationConfig.FacebookAppId)) return;
            metaStarted = true;
            try
            {
                if (!FB.IsInitialized) FB.Init(() => { if (FB.IsInitialized) MetaReady(); });
                else MetaReady();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void MetaReady()
        {
            fbReady = true;
            try
            {
                if (consent >= 0)
                {
                    FB.Mobile.SetAdvertiserTrackingEnabled(consent == 1);
                    FB.Mobile.SetAdvertiserIDCollectionEnabled(consent == 1);
                }
                FB.Mobile.SetAutoLogAppEventsEnabled(true);
                FB.ActivateApp();
            }
            catch (Exception e) { Debug.LogException(e); }
        }
#endif

        /// <summary>The app came back from the background (Meta counts sessions with it).</summary>
        public static void Resumed()
        {
#if SC_FB
            try { if (fbReady && FB.IsInitialized) FB.ActivateApp(); } catch (Exception e) { Debug.LogException(e); }
#endif
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            // keep the last steps in the crash report (only our own lines, and every error)
#if SC_FIREBASE
            if (!firebaseReady) return;
            if (type == LogType.Log && !condition.StartsWith("[SC]")) return;
            try { Crashlytics.Log(condition.Length > 300 ? condition.Substring(0, 300) : condition); } catch { }
#endif
        }

        // ------------------------------------------------------------------ events
        /// <summary>An event with parameters given as name, value pairs: Event("contract_start", "contract_id", "h01", "attempt", 2).</summary>
        public static void Event(string name, params object[] kv)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (LogToConsole) Debug.Log("[SC][an] " + name + " " + Describe(kv));
#if SC_FIREBASE
            if (!firebaseReady)
            {
                if (queue.Count < 200) queue.Add(new KeyValuePair<string, object[]>(name, kv));
                return;
            }
            Send(name, kv);
#endif
        }

        static string Describe(object[] kv)
        {
            if (kv == null || kv.Length == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i + 1 < kv.Length; i += 2) sb.Append(kv[i]).Append('=').Append(Format(kv[i + 1])).Append(' ');
            return sb.ToString();
        }

        static string Format(object v) => v is float f ? f.ToString("0.##", CultureInfo.InvariantCulture)
                                         : (v is double d ? d.ToString("0.##", CultureInfo.InvariantCulture) : (v == null ? "" : v.ToString()));

#if SC_FIREBASE
        static void FlushQueue()
        {
            var q = queue.ToArray();
            queue.Clear();
            foreach (var e in q) Send(e.Key, e.Value);
        }

        static void Send(string name, object[] kv)
        {
            try
            {
                var ps = new List<Parameter>();
                if (kv != null)
                    for (int i = 0; i + 1 < kv.Length && ps.Count < 25; i += 2)
                    {
                        string k = kv[i] as string;
                        object v = kv[i + 1];
                        if (string.IsNullOrEmpty(k) || v == null) continue;
                        switch (v)
                        {
                            case int n: ps.Add(new Parameter(k, n)); break;
                            case long l: ps.Add(new Parameter(k, l)); break;
                            case float f: ps.Add(new Parameter(k, (double)f)); break;
                            case double d: ps.Add(new Parameter(k, d)); break;
                            case bool b: ps.Add(new Parameter(k, b ? 1L : 0L)); break;
                            default:
                                string s = v.ToString();
                                ps.Add(new Parameter(k, s.Length > 100 ? s.Substring(0, 100) : s));
                                break;
                        }
                    }
                FirebaseAnalytics.LogEvent(name.Length > 40 ? name.Substring(0, 40) : name, ps.ToArray());
            }
            catch (Exception e) { Debug.LogException(e); }
        }
#endif

        public static void Screen(string screen)
        {
            if (string.IsNullOrEmpty(screen)) return;
            Event("screen_view", "screen_name", screen, "screen_class", screen);
        }

        /// <summary>A user property (player_level, payer, no_ads …): segments every report and audience.</summary>
        public static void SetUser(string prop, object value)
        {
            string v = Format(value);
            if (userProps.TryGetValue(prop, out var old) && old == v) return;
            userProps[prop] = v;
#if SC_FIREBASE
            if (firebaseReady) try { FirebaseAnalytics.SetUserProperty(prop, v); Crashlytics.SetCustomKey(prop, v); } catch (Exception e) { Debug.LogException(e); }
#endif
        }

        /// <summary>The career at a glance on every report (called at boot and after every contract).</summary>
        public static void RefreshUser()
        {
            try
            {
                SetUser("player_level", Progress.Level);
                SetUser("contracts_done", Progress.CompletedCount);
                SetUser("no_ads", Save.NoAds ? 1 : 0);
                SetUser("payer", Save.Flag("payer") ? 1 : 0);
                SetUser("rifle", Save.EquippedWeapon);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Paid ad impression (AdMob's paid event): Firebase "ad_impression" feeds the Google Ads value bidding.</summary>
        public static void AdImpression(string format, string unit, string source, double value, string currency)
        {
            Event("ad_impression", "ad_platform", "AdMob", "ad_format", format, "ad_unit_name", unit, "ad_source", source ?? "",
                  "value", value, "currency", currency ?? "USD");
#if SC_FB
            try { if (fbReady) FB.LogAppEvent("AdImpression", (float)value, new Dictionary<string, object> { { "ad_type", format }, { "fb_currency", currency ?? "USD" } }); }
            catch (Exception e) { Debug.LogException(e); }
#endif
        }

        /// <summary>A real-money purchase went through (Meta needs it client side for its purchase optimisation; Firebase
        /// logs App Store purchases by itself as in_app_purchase).</summary>
        public static void Purchase(string productId, double price, string currency, string transaction)
        {
            Event("iap_success", "product_id", productId, "value", price, "currency", currency ?? "", "transaction_id", transaction ?? "");
            // Firebase's standard purchase event (revenue in Analytics, the conversion Google Ads imports). Logged by the game
            // itself: App Store purchases made through StoreKit 2 (RevenueCat) are not always picked up automatically.
            Event("purchase", "value", price, "currency", string.IsNullOrEmpty(currency) ? "USD" : currency,
                  "transaction_id", transaction ?? ("rc_" + DateTime.UtcNow.Ticks), "item_id", productId, "item_name", productId);
            if (!Save.Flag("payer")) { Save.SetFlag("payer"); SetUser("payer", 1); Event("first_purchase", "product_id", productId); }
#if SC_FB
            try
            {
                if (fbReady && price > 0)
                    FB.LogPurchase((decimal)price, string.IsNullOrEmpty(currency) ? "USD" : currency,
                                   new Dictionary<string, object> { { AppEventParameterName.ContentID, productId } });
            }
            catch (Exception e) { Debug.LogException(e); }
#endif
        }

        /// <summary>Meta standard events for the campaign optimisation (tutorial done, level reached, diamonds spent).</summary>
        public static void MetaEvent(string kind, float value = 0f, string detail = null)
        {
#if SC_FB
            if (!fbReady) return;
            try
            {
                switch (kind)
                {
                    case "tutorial": FB.LogAppEvent(AppEventName.CompletedTutorial); break;
                    case "level": FB.LogAppEvent(AppEventName.AchievedLevel, null, new Dictionary<string, object> { { AppEventParameterName.Level, detail ?? "" } }); break;
                    case "spent": FB.LogAppEvent(AppEventName.SpentCredits, value, new Dictionary<string, object> { { AppEventParameterName.ContentID, detail ?? "" } }); break;
                    default: FB.LogAppEvent(kind, value == 0f ? (float?)null : value, detail == null ? null : new Dictionary<string, object> { { "detail", detail } }); break;
                }
            }
            catch (Exception e) { Debug.LogException(e); }
#endif
        }

        static int consent = -1;                  // the tracking answer, applied to Firebase once it is ready

        /// <summary>The tracking answer (ATT) for the SDKs; <paramref name="justAnswered"/>: the player answered it now (logged).</summary>
        public static void TrackingAnswered(bool allowed, bool justAnswered)
        {
            if (justAnswered) Event("att_result", "allowed", allowed ? 1 : 0);
            SetUser("att", allowed ? "allowed" : "denied");
            consent = allowed ? 1 : 0;
            ApplyConsent();
#if SC_FB
            try
            {
                if (fbReady) { FB.Mobile.SetAdvertiserTrackingEnabled(allowed); FB.Mobile.SetAdvertiserIDCollectionEnabled(allowed); }
                else StartMeta();
            }
            catch (Exception e) { Debug.LogException(e); }
#endif
        }

        static void ApplyConsent()
        {
            // no answer yet: no ad storage on iOS until Apple's question is answered (elsewhere the SDK defaults stand)
            if (consent < 0 && Application.platform != RuntimePlatform.IPhonePlayer) return;
            bool allowed = consent == 1;
#if SC_FIREBASE
            try
            {
                if (firebaseReady)
                    FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
                    {
                        { ConsentType.AnalyticsStorage, ConsentStatus.Granted },
                        { ConsentType.AdStorage, allowed ? ConsentStatus.Granted : ConsentStatus.Denied },
                        { ConsentType.AdUserData, allowed ? ConsentStatus.Granted : ConsentStatus.Denied },
                        { ConsentType.AdPersonalization, allowed ? ConsentStatus.Granted : ConsentStatus.Denied },
                    });
            }
            catch (Exception e) { Debug.LogException(e); }
#endif
        }

        /// <summary>Breadcrumb in the next crash report.</summary>
        public static void Crumb(string text) => Debug.Log("[SC] " + text);

        public static void Exception(Exception e)
        {
            Debug.LogException(e);
#if SC_FIREBASE
            if (firebaseReady) try { Crashlytics.LogException(e); } catch { }
#endif
        }
    }

    /// <summary>
    /// Numbers the live game can change without an update (Firebase Remote Config): ad frequency, free videos a day,
    /// replay pay … The defaults below are what the game uses offline or before the first fetch.
    /// </summary>
    public static class Tuning
    {
        static readonly Dictionary<string, object> Defaults = new Dictionary<string, object>
        {
            { "interstitial_every", 3 },          // full-screen ad after every N finished contracts
            { "interstitial_grace", 4 },          // none until this many contracts are done
            { "interstitial_cooldown_s", 90 },    // none within this many seconds of another ad
            { "free_gems_per_day", 5 },           // free-diamond videos a day
            { "free_gems_amount", 5 },
            { "shortfall_ads_per_day", 3 },       // "watch a video for the missing cash" in the armory
            { "shortfall_max_pct", 35 },          // … when at most this share of the price is missing
            { "replay_pay_pct", 40 },             // replays of a finished contract pay this share
            { "fail_refund_pct", 25 },            // a failed contract: this share of the fee for a video
            { "fail_refund_per_day", 3 },         // … at most this many times a day
            { "fail_refund_min_s", 45 },          // … and only after this many seconds in the contract
            { "starter_after", 3 },               // the starter pack is offered after this many contracts
            { "noads_nudge_after", 2 },           // the "no ads" offer after this many full-screen ads in a day
            { "pay_scale_pct", 100 },             // every contract fee (global economy knob)
            { "review_1", 5 },                    // App Store rating sheet after the first clear of these contracts
            { "review_2", 10 },
            { "review_3", 20 },
            { "review_4", 28 },
            { "review_gap_days", 4 },             // … and never within this many days of the last ask
            { "aim_assist_pct", 100 },            // the scope's help (how close a graze is pulled onto the target)
            { "enemy_threat_pct", 100 },          // how well the enemy shoots back
            { "enemy_damage_pct", 100 },          // how hard his rounds hit
        };

        static readonly Dictionary<string, object> remote = new Dictionary<string, object>();

        public static int Int(string key)
        {
            if (remote.TryGetValue(key, out var v)) try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); } catch { }
            return Defaults.TryGetValue(key, out var d) ? Convert.ToInt32(d, CultureInfo.InvariantCulture) : 0;
        }

        public static float Pct(string key) => Int(key) / 100f;

        internal static void FetchRemote()
        {
#if SC_FIREBASE
            try
            {
                var rc = FirebaseRemoteConfig.DefaultInstance;
                rc.SetConfigSettingsAsync(new ConfigSettings { MinimumFetchIntervalInMilliseconds = Game.TestBuild ? 0UL : 3600000UL });
                rc.SetDefaultsAsync(Defaults).ContinueWithOnMainThread(_ =>
                    rc.FetchAndActivateAsync().ContinueWithOnMainThread(t =>
                    {
                        if (t.IsFaulted || t.IsCanceled) return;
                        foreach (var k in Defaults.Keys)
                        {
                            var v = rc.GetValue(k);
                            if (v.Source == ValueSource.RemoteValue) remote[k] = v.LongValue;
                        }
                        Debug.Log("[SC] Remote Config: " + remote.Count + " değer");
                    }));
            }
            catch (Exception e) { Debug.LogException(e); }
#endif
        }
    }
}

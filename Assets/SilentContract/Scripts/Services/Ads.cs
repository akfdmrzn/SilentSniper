using System;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// Ads behind one small interface: rewarded videos the player chooses to watch (double pay, free diamonds, the
    /// missing cash for an upgrade, a refund for a failed job, rounds for an armoured target, double daily / level
    /// rewards) and a full-screen ad between contracts every few finished contracts (never in the first contracts, never
    /// right after another ad, never for players who bought "no ads"). Frequencies come from <see cref="Tuning"/>
    /// (Remote Config). AdMob is the network on devices (<see cref="AdMobProvider"/>); the editor and test builds without
    /// AdMob use a test provider that shows a short placeholder screen.
    /// </summary>
    public static class Ads
    {
        public interface IProvider
        {
            bool RewardedReady { get; }
            bool InterstitialReady { get; }
            void ShowRewarded(string placement, Action<bool> done);   // true: watched to the end, reward it
            void ShowInterstitial(string placement, Action done);
        }

        public static int InterstitialEvery => Mathf.Max(1, Tuning.Int("interstitial_every"));
        public static int GraceContracts => Tuning.Int("interstitial_grace");
        public static int FreeGems => Tuning.Int("free_gems_amount");
        public static int FreeGemsPerDay => Tuning.Int("free_gems_per_day");

        static IProvider provider;
        static float lastAdTime = -9999f;
        static bool showing;
        public static IProvider Provider
        {
            get
            {
                if (provider == null && (Game.TestBuild || Application.isEditor)) provider = new TestProvider();
                return provider;
            }
            set => provider = value;
        }

        public static bool IsTest => Provider is TestProvider;
        public static bool RewardedReady => Provider != null && Provider.RewardedReady && !showing;
        /// <summary>A full-screen ad is on screen (the game must not react to taps or pause music twice).</summary>
        public static bool Showing => showing;

        /// <summary>Boot: the consent questions (GDPR form where required) and then the ad network.</summary>
        public static void Init()
        {
            Privacy.Begin(() =>
            {
#if SC_ADMOB && !SC_AUTOTEST
                // (the self-playing test build keeps the placeholder ads: a real ad would wait for a tap forever)
                try { AdMobProvider.Init(); } catch (Exception e) { Analytics.Exception(e); }
#endif
            });
        }

        public static void ShowRewarded(string placement, Action<bool> done)
        {
            var p = Provider;
            if (p == null || !p.RewardedReady || showing)
            {
                Analytics.Event("ad_rewarded_unavailable", "placement", placement);
                if (!showing && UIRoot.I != null && placement != "briefing_ap" && placement != "daily_double") UIRoot.I.Confirm("VİDEO YOK", "Şu an gösterilecek video yok. Biraz sonra tekrar dene.", "TAMAM", null, "KAPAT");
                done?.Invoke(false);
                return;
            }
            Analytics.Event("ad_rewarded_start", "placement", placement);
            showing = true;
            Sfx.Mute(true);
            p.ShowRewarded(placement, ok =>
            {
                showing = false;
                lastAdTime = Time.unscaledTime;
                Sfx.Mute(false);
                Analytics.Event(ok ? "ad_rewarded_complete" : "ad_rewarded_skip", "placement", placement);
                if (ok) { Save.SetFlag("watched_ad"); PlayerPrefs.SetInt("sc2_rewarded_total", PlayerPrefs.GetInt("sc2_rewarded_total", 0) + 1); }
                done?.Invoke(ok);
            });
        }

        /// <summary>
        /// Called when the player leaves a result screen: counts the contract and, every few contracts, shows a full-screen
        /// ad before <paramref name="then"/> runs.
        /// </summary>
        public static void AfterContract(Action then)
        {
            if (Save.NoAds || Progress.CompletedCount < GraceContracts) { then?.Invoke(); return; }
            int n = Save.AdCounter + 1;
            if (n < InterstitialEvery) { Save.AdCounter = n; then?.Invoke(); return; }
            // a video was just watched (double pay): this one waits for the next contract
            if (Time.unscaledTime - lastAdTime < Tuning.Int("interstitial_cooldown_s")) { Save.AdCounter = n; then?.Invoke(); return; }
            var p = Provider;
            if (p == null || !p.InterstitialReady || showing) { Save.AdCounter = n; then?.Invoke(); return; }   // try again next time
            Save.AdCounter = 0;
            showing = true;
            Sfx.Mute(true);
            Analytics.Event("ad_interstitial_show", "placement", "between_contracts", "contracts_done", Progress.CompletedCount);
            int today = InterstitialsToday + 1;
            PlayerPrefs.SetInt("sc2_" + DayKey("inter"), today);
            p.ShowInterstitial("between_contracts", () =>
            {
                showing = false;
                lastAdTime = Time.unscaledTime;
                Sfx.Mute(false);
                then?.Invoke();
            });
        }

        // ---------------- daily counters
        static string DayKey(string what) => what + "_" + DateTime.Now.ToString("yyyyMMdd");
        static int Today(string what) => PlayerPrefs.GetInt("sc2_" + DayKey(what), 0);
        static void UseToday(string what) { PlayerPrefs.SetInt("sc2_" + DayKey(what), Today(what) + 1); PlayerPrefs.Save(); }

        public static int InterstitialsToday => Today("inter");

        /// <summary>The "no ads" offer once a day after a few full-screen ads (the moment the ads are noticed most).</summary>
        public static bool NoAdsNudgeDue => !Save.NoAds && InterstitialsToday >= Tuning.Int("noads_nudge_after") && Today("noads_nudge") == 0;
        public static void NoAdsNudged() => UseToday("noads_nudge");

        // free diamonds for a video (a few a day)
        public static int FreeGemsLeft => Mathf.Max(0, FreeGemsPerDay - Today("adgems"));
        public static void UseFreeGems() => UseToday("adgems");

        // the missing cash of an upgrade for a video (a few a day, only for a small gap)
        public static int ShortfallLeft => Mathf.Max(0, Tuning.Int("shortfall_ads_per_day") - Today("shortfall"));
        public static bool ShortfallOffer(int price, int have) =>
            ShortfallLeft > 0 && have < price && (price - have) <= price * Tuning.Pct("shortfall_max_pct") && RewardedReady;
        public static void UseShortfall() => UseToday("shortfall");

        // part of the fee back after a failed contract (a few a day, only after a real try)
        public static bool RefundOffer(float elapsed) => Today("refund") < Tuning.Int("fail_refund_per_day") && elapsed >= Tuning.Int("fail_refund_min_s");
        public static void UseRefund() => UseToday("refund");

        /// <summary>Development stand-in: a dark screen with a 3 second countdown ("TEST REKLAMI").</summary>
        class TestProvider : IProvider
        {
            public bool RewardedReady => true;
            public bool InterstitialReady => true;
            public void ShowRewarded(string placement, Action<bool> done) => AdOverlay.Run("ÖDÜLLÜ TEST REKLAMI  ·  " + placement, 3f, true, done);
            public void ShowInterstitial(string placement, Action done) => AdOverlay.Run("TEST REKLAMI  ·  her " + InterstitialEvery + " görevde bir", 2.5f, false, _ => done?.Invoke());
        }
    }

    /// <summary>The test provider's placeholder ad (full screen, above every page).</summary>
    public class AdOverlay : MonoBehaviour
    {
        UnityEngine.UI.Text count;
        float left;
        Action<bool> done;
        UnityEngine.UI.Button close;

        public static void Run(string label, float seconds, bool rewarded, Action<bool> done)
        {
            var root = UIRoot.I != null ? UIRoot.I.Root : null;
            if (root == null) { done?.Invoke(true); return; }
            var rt = UIK.Rect(root, "AdOverlay");
            UIK.Stretch(rt);
            rt.SetAsLastSibling();
            // its own canvas above every page (a level-up card opening behind it must not cover the close button)
            var cv = rt.gameObject.AddComponent<Canvas>();
            cv.overrideSorting = true;
            cv.sortingOrder = 100;
            rt.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var bg = UIK.Img(rt, "Bg", null, new Color(0.02f, 0.02f, 0.03f, 1f), false, true);
            UIK.Stretch(bg.rectTransform);
            var ov = rt.gameObject.AddComponent<AdOverlay>();
            var t = UIK.Txt(rt, "Label", label, Res.FontBold, 48, Theme.TextDim, TextAnchor.MiddleCenter, false);
            UIK.Place(t.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1600, 70));
            ov.count = UIK.Txt(rt, "Count", "", Res.FontBold, 120, Theme.Accent, TextAnchor.MiddleCenter, false);
            UIK.Place(ov.count.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(400, 140));
            var note = UIK.Txt(rt, "Note", rewarded ? "Reklam sonunda ödül verilir" : "Reklamsız paketle bu ekran kalkar",
                               Res.FontSemi, 30, Theme.TextMute, TextAnchor.MiddleCenter, false);
            UIK.Place(note.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(1200, 44));
            ov.close = UIK.Btn(rt, "Close", null, BtnStyle.Icon, () => ov.Finish(true), "close");
            UIK.Place((RectTransform)ov.close.transform, UIK.TR, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(88, 88));
            ov.close.gameObject.SetActive(false);
            ov.left = seconds;
            ov.done = done;
        }

        void Update()
        {
            if (left <= 0f) return;
            left -= Time.unscaledDeltaTime;
            count.text = Mathf.CeilToInt(Mathf.Max(0f, left)).ToString();
            if (left <= 0f) { count.text = ""; close.gameObject.SetActive(true); }
        }

        void Finish(bool ok)
        {
            var d = done;
            done = null;
            Destroy(gameObject);
            d?.Invoke(ok);
        }
    }
}

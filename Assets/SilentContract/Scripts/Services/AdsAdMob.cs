#if SC_ADMOB
using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// Google AdMob: one rewarded and one interstitial ad kept loaded at all times (reloaded after every show, retried
    /// with a growing pause after a failed load). Paid events go to analytics as ad_impression (ad revenue per user in
    /// Firebase and the Google Ads / Meta value optimisation).
    /// </summary>
    public class AdMobProvider : MonoBehaviour, Ads.IProvider
    {
        static AdMobProvider inst;
        // the reward slot: a "Rewarded" or a "Rewarded interstitial" ad unit (AdMob's two reward formats; the unit decides,
        // MonetizationConfig says which, and a format mismatch reported by AdMob switches over by itself)
        RewardedAd rewardedR;
        RewardedInterstitialAd rewardedI;
        bool useRI;

        void Awake() => useRI = PlayerPrefs.GetInt("sc2_admob_ri", MonetizationConfig.AdMobRewardedIsInterstitial ? 1 : 0) == 1;
        InterstitialAd interstitial;
        bool loadingR, loadingI, initDone;
        int failR, failI;
        float retryR = -1f, retryI = -1f, loadedR, loadedI, nextCheck;
        const float MaxAge = 3300f;                              // AdMob ads expire after an hour: replace them a bit before
        Action<bool> rewardDone;
        bool earned;
        Action interDone;

        static string RewardedId => MonetizationConfig.AdMobRewardedIOS;
        static string InterstitialId => MonetizationConfig.AdMobInterstitialIOS;

        public static void Init()
        {
            if (inst != null) return;
            if (string.IsNullOrEmpty(MonetizationConfig.AdMobAppIdIOS)) return;
            var go = new GameObject("AdMob");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<AdMobProvider>();
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.SetiOSAppPauseOnBackground(true);
            MobileAds.Initialize(status =>
            {
                inst.initDone = true;
                inst.LoadRewarded();
                inst.LoadInterstitial();
            });
            Ads.Provider = inst;
        }

        bool HasRewarded => rewardedR != null || rewardedI != null;
        bool RewardedCanShow => (rewardedR != null && rewardedR.CanShowAd()) || (rewardedI != null && rewardedI.CanShowAd());
        void DropRewarded()
        {
            if (rewardedR != null) { rewardedR.Destroy(); rewardedR = null; }
            if (rewardedI != null) { rewardedI.Destroy(); rewardedI = null; }
        }

        public bool RewardedReady => RewardedCanShow;
        public bool InterstitialReady => interstitial != null && interstitial.CanShowAd();

        void Update()
        {
            if (!initDone) return;
            float t = Time.unscaledTime;
            if (t > nextCheck)
            {
                nextCheck = t + 15f;
                if (HasRewarded && rewardDone == null && (!RewardedCanShow || t - loadedR > MaxAge)) { DropRewarded(); LoadRewarded(); }
                if (interstitial != null && interDone == null && (!interstitial.CanShowAd() || t - loadedI > MaxAge)) { interstitial.Destroy(); interstitial = null; LoadInterstitial(); }
            }
            if (!HasRewarded && !loadingR && retryR >= 0f && t > retryR) { retryR = -1f; LoadRewarded(); }
            if (interstitial == null && !loadingI && retryI >= 0f && t > retryI) { retryI = -1f; LoadInterstitial(); }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused || !initDone) return;
            nextCheck = 0f;                                      // expired while in the background: checked on the next frame
            if (!HasRewarded && !loadingR) LoadRewarded();
            if (interstitial == null && !loadingI) LoadInterstitial();
        }

        static float Backoff(int fails) => Mathf.Min(120f, 4f * Mathf.Pow(2f, Mathf.Min(fails, 5)));

        // ------------------------------------------------------------------ rewarded
        void LoadRewarded()
        {
            if (loadingR) return;
            loadingR = true;
            bool ri = useRI;
            Action<AdError, bool> failed = (error, nullAd) =>
            {
                loadingR = false;
                string msg = error != null ? error.GetMessage() : "null";
                // the unit is the other reward format: switch (once) and load again at once
                if (error != null && msg.ToLowerInvariant().Contains("format") && ri == useRI)
                {
                    useRI = !useRI;
                    PlayerPrefs.SetInt("sc2_admob_ri", useRI ? 1 : 0);
                    Debug.LogWarning("[SC] AdMob ödül birimi diğer biçimde, geçiliyor: " + (useRI ? "rewarded interstitial" : "rewarded"));
                    LoadRewarded();
                    return;
                }
                failR++;
                retryR = Time.unscaledTime + Backoff(failR);
                Debug.LogWarning("[SC] AdMob ödüllü yüklenemedi: " + msg);
            };
            if (ri)
                RewardedInterstitialAd.Load(RewardedId, new AdRequest(), (ad, error) =>
                {
                    if (error != null || ad == null) { failed(error, ad == null); return; }
                    loadingR = false; failR = 0; rewardedI = ad; loadedR = Time.unscaledTime;
                    ad.OnAdPaid += v => Paid("rewarded_interstitial", RewardedId, ad.GetResponseInfo(), v);
                    ad.OnAdFullScreenContentClosed += () => RewardedClosed();
                    ad.OnAdFullScreenContentFailed += e => { Debug.LogWarning("[SC] AdMob ödüllü gösterilemedi: " + e.GetMessage()); RewardedClosed(); };
                });
            else
                RewardedAd.Load(RewardedId, new AdRequest(), (ad, error) =>
                {
                    if (error != null || ad == null) { failed(error, ad == null); return; }
                    loadingR = false; failR = 0; rewardedR = ad; loadedR = Time.unscaledTime;
                    ad.OnAdPaid += v => Paid("rewarded", RewardedId, ad.GetResponseInfo(), v);
                    ad.OnAdFullScreenContentClosed += () => RewardedClosed();
                    ad.OnAdFullScreenContentFailed += e => { Debug.LogWarning("[SC] AdMob ödüllü gösterilemedi: " + e.GetMessage()); RewardedClosed(); };
                });
        }

        public void ShowRewarded(string placement, Action<bool> done)
        {
            if (!RewardedReady) { done?.Invoke(false); return; }
            rewardDone = done;
            earned = false;
            try
            {
                if (rewardedR != null && rewardedR.CanShowAd()) rewardedR.Show(reward => earned = true);
                else rewardedI.Show(reward => earned = true);
            }
            catch (Exception e) { Analytics.Exception(e); RewardedClosed(); }
        }

        void RewardedClosed()
        {
            var d = rewardDone;
            rewardDone = null;
            DropRewarded();
            LoadRewarded();
            // the reward callback can land a moment after the close: give it one frame
            if (d != null) StartCoroutine(Later(() => d(earned)));
        }

        // ------------------------------------------------------------------ interstitial
        void LoadInterstitial()
        {
            if (loadingI) return;
            loadingI = true;
            InterstitialAd.Load(InterstitialId, new AdRequest(), (ad, error) =>
            {
                loadingI = false;
                if (error != null || ad == null)
                {
                    failI++;
                    retryI = Time.unscaledTime + Backoff(failI);
                    Debug.LogWarning("[SC] AdMob interstitial yüklenemedi: " + (error != null ? error.GetMessage() : "null"));
                    return;
                }
                failI = 0;
                interstitial = ad;
                loadedI = Time.unscaledTime;
                ad.OnAdPaid += v => Paid("interstitial", InterstitialId, ad.GetResponseInfo(), v);
                ad.OnAdFullScreenContentClosed += () => InterstitialClosed();
                ad.OnAdFullScreenContentFailed += e => { Debug.LogWarning("[SC] AdMob interstitial gösterilemedi: " + e.GetMessage()); InterstitialClosed(); };
            });
        }

        public void ShowInterstitial(string placement, Action done)
        {
            if (!InterstitialReady) { done?.Invoke(); return; }
            interDone = done;
            try { interstitial.Show(); }
            catch (Exception e) { Analytics.Exception(e); InterstitialClosed(); }
        }

        void InterstitialClosed()
        {
            var d = interDone;
            interDone = null;
            if (interstitial != null) { interstitial.Destroy(); interstitial = null; }
            LoadInterstitial();
            if (d != null) StartCoroutine(Later(d));
        }

        System.Collections.IEnumerator Later(Action a)
        {
            yield return null;
            try { a(); } catch (Exception e) { Analytics.Exception(e); }
        }

        static void Paid(string format, string unit, ResponseInfo info, AdValue v)
        {
            if (v == null) return;
            string source = null;
            try { var r = info != null ? info.GetLoadedAdapterResponseInfo() : null; source = r != null ? r.AdSourceName : null; } catch { }
            Analytics.AdImpression(format, unit, source, v.Value / 1000000.0, v.CurrencyCode);
        }
    }
}
#endif

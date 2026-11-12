using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
#if SC_ADMOB
using GoogleMobileAds.Ump.Api;
#endif

namespace SilentContract
{
    /// <summary>
    /// The privacy questions in the order the stores expect them:
    /// 1. Google's consent form (UMP) at launch, only where the law asks for it (EEA, UK, Switzerland …);
    /// 2. Apple's tracking question (ATT) right after it, at the first launch (App Review wants it before any data that
    ///    could track is collected): the Meta SDK starts only once it is answered, and Firebase keeps its ad consent
    ///    denied until then.
    /// Ads, Firebase and Meta follow the answers. The settings page has a "privacy options" button where the form is
    /// required.
    /// </summary>
    public static class Privacy
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SC_AttStatus();
        [DllImport("__Internal")] static extern void SC_AttRequest();
        [DllImport("__Internal")] static extern int SC_AttAnswered();
#else
        static int SC_AttAnswered() => 1;
        static int SC_AttStatus() => 3;
        static void SC_AttRequest() { }
#endif
        /// <summary>0 not asked, 1 restricted, 2 denied, 3 allowed.</summary>
        public static int AttStatus { get { try { return SC_AttStatus(); } catch { return 3; } } }
        public static bool TrackingAllowed => AttStatus == 3;

        static bool begun, adsStarted, attAskedThisSession, umpDone;
        /// <summary>Apple's tracking question is on screen (nothing else asks meanwhile, e.g. the rating sheet).</summary>
        public static bool Asking { get; private set; }
        static Action pendingAds;

        /// <summary>Boot: the consent form where required, then <paramref name="adsAllowed"/> (ads may be requested).</summary>
        public static void Begin(Action adsAllowed)
        {
            if (adsStarted) return;
            if (adsAllowed != null) pendingAds = adsAllowed;
            if (begun) return;
            begun = true;
            Action ready = () => { if (adsStarted) return; adsStarted = true; var a = pendingAds; pendingAds = null; a?.Invoke(); };
            adsAllowed = ready;
            // the tracking answer from an earlier session goes to the SDKs right away (not logged as a new answer)
            int att = AttStatus;
            if (att != 0) Analytics.TrackingAnswered(att == 3, false);
#if SC_ADMOB
            try
            {
                var req = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
                ConsentInformation.Update(req, err =>
                {
                    if (err != null)
                    {
                        Debug.LogWarning("[SC] UMP: " + err.Message);
                        umpDone = true;
                        if (ConsentInformation.CanRequestAds()) adsAllowed?.Invoke();
                        else begun = false;                    // offline: asked again when the app comes back (Retry)
                        return;
                    }
                    ConsentForm.LoadAndShowConsentFormIfRequired(formErr =>
                    {
                        if (formErr != null) Debug.LogWarning("[SC] UMP form: " + formErr.Message);
                        umpDone = true;
                        Analytics.Event("consent_done", "can_request_ads", ConsentInformation.CanRequestAds() ? 1 : 0);
                        if (ConsentInformation.CanRequestAds()) adsAllowed?.Invoke();
                        else begun = false;
                    });
                });
                // an earlier session's consent is valid at once (no wait for the network)
                if (ConsentInformation.CanRequestAds()) adsAllowed?.Invoke();
            }
            catch (Exception e) { umpDone = true; Analytics.Exception(e); adsAllowed?.Invoke(); }
#else
            umpDone = true;
            adsAllowed?.Invoke();
#endif
        }

        /// <summary>The app came back to the front: the consent check again if it could not finish (no network at launch).</summary>
        public static void Retry()
        {
            if (!adsStarted && !begun && pendingAds != null) Begin(null);
        }

        /// <summary>Does the settings page need a "privacy options" button (GDPR regions)?</summary>
        public static bool OptionsRequired
        {
            get
            {
#if SC_ADMOB
                try { return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; } catch { }
#endif
                return false;
            }
        }

        public static void ShowOptions()
        {
#if SC_ADMOB
            try { ConsentForm.ShowPrivacyOptionsForm(err => { if (err != null) Debug.LogWarning("[SC] UMP: " + err.Message); }); }
            catch (Exception e) { Analytics.Exception(e); }
#endif
        }

        /// <summary>The tracking question is due (iOS 14+, never asked, Google's consent form finished or not needed).</summary>
        public static bool AttDue => AttPending && umpDone;

        /// <summary>The tracking question is still to be asked this launch (the menu holds its own pop-ups until then).</summary>
        public static bool AttPending
        {
            get
            {
#if SC_AUTOTEST
                return false;                                   // the self-playing test build never stops for a system dialog
#else
                return !attAskedThisSession && Application.platform == RuntimePlatform.IPhonePlayer && AttStatus == 0
                    && Time.realtimeSinceStartup < 90f;
#endif
            }
        }

        /// <summary>
        /// Launch: Apple's tracking question as soon as Google's consent form is done (or not needed) and the app is
        /// active — a request made while the app is still starting up is ignored by iOS.
        /// </summary>
        public static IEnumerator AskAtLaunch()
        {
            if (!AttPending) yield break;
            float t = 0f;
            while (!umpDone && t < 12f) { t += Time.unscaledDeltaTime; yield return null; }
            umpDone = true;                                     // no answer from Google in time: ask anyway
            t = 0f;
            while (!Application.isFocused && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.8f);
            if (AttDue) yield return AskTracking(null);
        }

        /// <summary>Asks Apple's tracking question and reports the answer to the SDKs (<paramref name="done"/> after it).</summary>
        public static IEnumerator AskTracking(Action done)
        {
            if (AttStatus != 0 || attAskedThisSession) { done?.Invoke(); yield break; }
            attAskedThisSession = true;                         // once a session: an ignored request must not loop
            Analytics.Event("att_prompt");
            Asking = true;
            try { SC_AttRequest(); } catch (Exception e) { Asking = false; Analytics.Exception(e); done?.Invoke(); yield break; }
            float t = 0f;
            while (SC_AttAnswered() == 0 && t < 300f) { t += Time.unscaledDeltaTime; yield return null; }
            Asking = false;
            int st = AttStatus;
            if (st != 0) Analytics.TrackingAnswered(st == 3, true);
            done?.Invoke();
        }
    }
}

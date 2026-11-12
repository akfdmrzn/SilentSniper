using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// App Store ratings. Apple's own rating sheet (SKStoreReviewController) is asked for at the happy moments only:
    /// right after the FIRST clear of a milestone contract (5, 10, 20, 28 by default; Remote Config "review_1".."review_4")
    /// with at least two stars, a few seconds after the stars and the pay have landed. Never after a failure, a replay,
    /// a purchase or an ad, at most once a session and never within a few days of the last ask. Apple itself shows the sheet
    /// at most 3 times a year and may skip a request, so the milestones are spread over the campaign.
    /// Settings → "Puanla" opens the App Store review page directly (always allowed).
    /// </summary>
    public static class Review
    {
        const string Key = "sc2_review_";
        static bool pending, askedThisSession;
        static int pendingContract;

        /// <summary>Called by the contract when it ends.</summary>
        public static void OnContractEnd(MissionDef def, bool success, bool firstClear, int stars)
        {
            if (def == null || !success || !firstClear || stars < 2) return;
            for (int i = 1; i <= 4; i++)
            {
                if (def.number != Tuning.Int("review_" + i)) continue;
                if (PlayerPrefs.GetInt(Key + "m" + i, 0) == 1) return;
                PlayerPrefs.SetInt(Key + "m" + i, 1);
                pending = true; pendingContract = def.number;
                return;
            }
        }

        /// <summary>Shows the rating sheet when one is due and nothing else is on screen. Returns true when it asked.</summary>
        public static bool TryShow(UIRoot ui)
        {
            if (!pending || askedThisSession) return false;
            if (ui != null && ((ui.Dialog != null && ui.Dialog.Visible) || (ui.LevelUp != null && ui.LevelUp.Visible) ||
                               (ui.Daily != null && ui.Daily.Visible) || (ui.Shop != null && ui.Shop.Visible))) return false;
            if (Ads.Showing || Store.Busy) return false;
            long last = long.Parse(PlayerPrefs.GetString(Key + "last", "0"));
            long now = System.DateTime.UtcNow.Ticks;
            if (last > 0 && System.TimeSpan.FromTicks(now - last).TotalDays < Tuning.Int("review_gap_days")) { pending = false; return false; }
            pending = false; askedThisSession = true;
            PlayerPrefs.SetString(Key + "last", now.ToString());
            PlayerPrefs.SetInt(Key + "count", PlayerPrefs.GetInt(Key + "count", 0) + 1);
            PlayerPrefs.Save();
            Analytics.Event("review_prompt", "contract_no", pendingContract, "ask", PlayerPrefs.GetInt(Key + "count", 0));
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.RequestStoreReview();
#else
            Debug.Log("[SC] Puanlama penceresi istendi (sözleşme " + pendingContract + ")");
#endif
            return true;
        }

        /// <summary>Settings: the App Store "write a review" page.</summary>
        public static void OpenStorePage()
        {
            Analytics.Event("review_open_store");
            Application.OpenURL("itms-apps://itunes.apple.com/app/id" + MonetizationConfig.AppStoreId + "?action=write-review");
        }
    }
}

namespace SilentContract
{
    /// <summary>
    /// Every id the store, ad and analytics SDKs need, in one place. Only PUBLIC client keys belong here (they ship
    /// inside every app anyway); secret server keys never go into the project.
    /// Empty values switch that service off: the game falls back to its test store / test ads in test builds.
    /// </summary>
    public static class MonetizationConfig
    {
        // ---------------- RevenueCat (Silent Contract project → API keys)
        /// <summary>Public Apple SDK key (appl_…). Empty: no RevenueCat (test store in test builds).</summary>
        public const string RevenueCatAppleKey = "appl_EhxLwdLFRSrkhBRIGmvuGtWBtpR";
        /// <summary>RevenueCat Test Store key (test_…): purchases are simulated by RevenueCat, no App Store needed.
        /// Used instead of the Apple key in the editor, and on devices when <see cref="UseRevenueCatTestStore"/>.</summary>
        public const string RevenueCatTestKey = "test_GXqEAzCZswSloWEfeHWlLhMnICU";
        public const bool UseRevenueCatTestStore = false;

        // ---------------- AdMob (iOS app "Silent Contract")
        public const string AdMobAppIdIOS = "ca-app-pub-6875060834963859~4684966911";
        public const string AdMobRewardedIOS = "ca-app-pub-6875060834963859/5914288102";
        /// <summary>The reward unit is a "Rewarded interstitial" unit (if AdMob reports a format mismatch the game switches by itself).</summary>
        public const bool AdMobRewardedIsInterstitial = true;
        public const string AdMobInterstitialIOS = "ca-app-pub-6875060834963859/9388618352";
        public static bool AdMobIsTest => AdMobAppIdIOS.StartsWith("ca-app-pub-3940256099942544");

        // ---------------- Meta (Facebook) app: install attribution and app events for the ad campaigns
        public const string FacebookAppId = "4624343374443741";
        public const string FacebookClientToken = "ca0327e5825116afbeb679ca1307e217";

        // ---------------- App Store
        public const string AppStoreId = "6829282024";
        /// <summary>Privacy policy page (the same link as on the App Store page). Empty: the links are hidden.</summary>
        public const string PrivacyUrl = "https://www.freeprivacypolicy.com/live/a566c1ec-b750-479d-a3c6-7988749cd0c5";
        public const string TermsUrl = "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/";

        /// <summary>The shown purpose string of the iOS tracking permission (ATT).</summary>
        public const string TrackingUsageTR = "Sana ilgi alanına uygun reklamlar gösterebilmemiz ve oyunu ücretsiz tutabilmemiz için kullanılır.";
        public const string TrackingUsageEN = "Used to show you more relevant ads and keep the game free to play.";
    }
}

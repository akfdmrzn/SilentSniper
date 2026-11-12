using UnityEngine;

namespace SilentContract
{
    /// <summary>Player progress + settings (PlayerPrefs).</summary>
    public static class Save
    {
        /// <summary>Key prefix of the career. A progress reset starts a new generation of keys instead of deleting the
        /// app's whole preference store (that would also wipe the SDKs' data: RevenueCat's user, Google's consent).</summary>
        public static string K
        {
            get
            {
                if (gen < 0) gen = PlayerPrefs.GetInt("sc2gen", 0);
                return gen == 0 ? "sc2_" : "sc2_g" + gen + "_";
            }
        }
        static int gen = -1;

        public static int Cash
        {
            get => PlayerPrefs.GetInt(K + "cash", 0);
            set { PlayerPrefs.SetInt(K + "cash", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>Number of missions unlocked (at least 1).</summary>
        public static int Unlocked
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt(K + "unlocked", 1));
            set { PlayerPrefs.SetInt(K + "unlocked", value); PlayerPrefs.Save(); }
        }

        /// <summary>Stars of a contract = star challenges completed so far (each one counts once, over all runs).</summary>
        public static int Stars(string missionId)
        {
            int n = 0;
            for (int i = 0; i < 3; i++) if (Challenge(missionId, i)) n++;
            return n;
        }

        /// <summary>Has this star challenge of the contract ever been completed? (Saves from before the challenge system
        /// kept only a star count: those count as the first challenges.)</summary>
        public static bool Challenge(string missionId, int index)
        {
            int v = PlayerPrefs.GetInt(K + "ch_" + missionId + "_" + index, -1);
            if (v >= 0) return v == 1;
            return index < PlayerPrefs.GetInt(K + "stars_" + missionId, 0);
        }

        public static void SetChallenge(string missionId, int index)
        {
            // keep the legacy stars of this contract before the first per-challenge record replaces them
            for (int i = 0; i < 3; i++)
                if (PlayerPrefs.GetInt(K + "ch_" + missionId + "_" + i, -1) < 0)
                    PlayerPrefs.SetInt(K + "ch_" + missionId + "_" + i, i < PlayerPrefs.GetInt(K + "stars_" + missionId, 0) ? 1 : 0);
            PlayerPrefs.SetInt(K + "ch_" + missionId + "_" + index, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Map the game was last on (the menu opens there).</summary>
        public static string Location
        {
            get => PlayerPrefs.GetString(K + "location", "harbor");
            set { PlayerPrefs.SetString(K + "location", value); PlayerPrefs.Save(); }
        }

        /// <summary>Test switch (long press on the version line in the menu): every map and contract open.</summary>
        public static bool UnlockAll
        {
            // only in test builds (and the self-playing test): the store build always plays the campaign in order
            get
            {
#if SC_AUTOTEST
                return true;
#else
                return Game.UnlockAllContracts || (Game.TestBuild && PlayerPrefs.GetInt(K + "unlock_all_v2", 1) == 1);
#endif
            }
            set { PlayerPrefs.SetInt(K + "unlock_all_v2", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Completed(string missionId) => PlayerPrefs.GetInt(K + "done_" + missionId, 0) == 1 || Stars(missionId) > 0;
        public static void SetCompleted(string missionId) { PlayerPrefs.SetInt(K + "done_" + missionId, 1); PlayerPrefs.Save(); }

        // ---------------- armory: owned items, equipped rifle, special ammunition
        public static bool Owns(string key) => PlayerPrefs.GetInt(K + "own_" + key, 0) == 1;
        public static void SetOwned(string key) { PlayerPrefs.SetInt(K + "own_" + key, 1); PlayerPrefs.Save(); }

        public static string EquippedWeapon
        {
            get => PlayerPrefs.GetString(K + "weapon", "k98");
            set { PlayerPrefs.SetString(K + "weapon", value); PlayerPrefs.Save(); }
        }

        public static int AmmoCount(string kind) => PlayerPrefs.GetInt(K + "ammo_" + kind, 0);
        public static void SetAmmoCount(string kind, int n) { PlayerPrefs.SetInt(K + "ammo_" + kind, Mathf.Max(0, n)); PlayerPrefs.Save(); }

        /// <summary>Has the player seen this new item / feature badge yet ("YENİ" ribbons in the armory).</summary>
        public static bool Seen(string key) => PlayerPrefs.GetInt(K + "seen_" + key, 0) == 1;
        public static void SetSeen(string key) { PlayerPrefs.SetInt(K + "seen_" + key, 1); PlayerPrefs.Save(); }

        public static float BestTime(string missionId) => PlayerPrefs.GetFloat(K + "best_" + missionId, 0f);

        public static void SetBestTime(string missionId, float t)
        {
            float b = BestTime(missionId);
            if (b <= 0 || t < b) PlayerPrefs.SetFloat(K + "best_" + missionId, t);
        }

        /// <summary>V2: the longest kill on this contract so far (the result screen's "REKOR").</summary>
        public static float BestDistance(string missionId) => PlayerPrefs.GetFloat(K + "dist_" + missionId, 0f);
        public static void SetBestDistance(string missionId, float d) { PlayerPrefs.SetFloat(K + "dist_" + missionId, d); PlayerPrefs.Save(); }

        /// <summary>V2: finished as VETERAN (no aim help, hard wind, no target marks).</summary>
        public static bool Veteran(string missionId) => PlayerPrefs.GetInt(K + "vet_" + missionId, 0) == 1;
        public static void SetVeteran(string missionId) { PlayerPrefs.SetInt(K + "vet_" + missionId, 1); PlayerPrefs.Save(); }

        /// <summary>V2: the contract of the day is done (one a day).</summary>
        public static bool DailyDone(string dayKey) => PlayerPrefs.GetInt(K + "daily_" + dayKey, 0) == 1;
        public static void SetDailyDone(string dayKey) { PlayerPrefs.SetInt(K + "daily_" + dayKey, 1); PlayerPrefs.Save(); }

        /// <summary>V2: the best grade (S=5 … D=1) the contract was finished with.</summary>
        public static int BestGrade(string missionId) => PlayerPrefs.GetInt(K + "grade_" + missionId, 0);
        public static void SetBestGrade(string missionId, int g) { if (g > BestGrade(missionId)) { PlayerPrefs.SetInt(K + "grade_" + missionId, g); PlayerPrefs.Save(); } }

        public static int TotalStars
        {
            get
            {
                int s = 0;
                foreach (var m in MissionDefs.All) s += Stars(m.id);
                return s;
            }
        }

        // ---------------- upgrades (0..3)
        public static int Upgrade(string key) => PlayerPrefs.GetInt(K + "up_" + key, 0);
        public static void SetUpgrade(string key, int lvl) { PlayerPrefs.SetInt(K + "up_" + key, Mathf.Clamp(lvl, 0, 3)); PlayerPrefs.Save(); }

        // ---------------- career: experience, diamonds, rifle upgrades
        public static int Xp
        {
            get => PlayerPrefs.GetInt(K + "xp", 0);
            set { PlayerPrefs.SetInt(K + "xp", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>The premium currency (ELMAS). A new career starts with a few.</summary>
        public static int Gems
        {
            get => PlayerPrefs.GetInt(K + "gems", Progress.StartGems);
            set { PlayerPrefs.SetInt(K + "gems", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>Upgrade level (0..5) of one stat line of one rifle.</summary>
        public static int WeaponUp(string weaponId, int stat) => PlayerPrefs.GetInt(K + "wup_" + weaponId + "_" + stat, 0);
        public static void SetWeaponUp(string weaponId, int stat, int lvl) { PlayerPrefs.SetInt(K + "wup_" + weaponId + "_" + stat, Mathf.Clamp(lvl, 0, 5)); PlayerPrefs.Save(); }

        /// <summary>Contracts finished since the last full-screen ad (the ad service shows one every few contracts).</summary>
        public static int AdCounter
        {
            get => PlayerPrefs.GetInt(K + "ad_counter", 0);
            set { PlayerPrefs.SetInt(K + "ad_counter", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>The player bought "no ads": no interstitials (rewarded ads stay optional).</summary>
        public static bool NoAds
        {
            get => PlayerPrefs.GetInt(K + "no_ads", 0) == 1;
            set { PlayerPrefs.SetInt(K + "no_ads", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>A one-off flag (first S of a contract, starter pack bought, tutorial bits).</summary>
        public static bool Flag(string key) => PlayerPrefs.GetInt(K + "flag_" + key, 0) == 1;
        public static void SetFlag(string key) { PlayerPrefs.SetInt(K + "flag_" + key, 1); PlayerPrefs.Save(); }

        // ---------------- settings
        public static float Sensitivity
        {
            get => PlayerPrefs.GetFloat(K + "sens", 1f);
            set { PlayerPrefs.SetFloat(K + "sens", Mathf.Clamp(value, 0.3f, 2.5f)); PlayerPrefs.Save(); }
        }

        public static bool AimAssist
        {
            // v2: the impact dot is part of the campaign's difficulty; the setting is the "easy aim" mode (off by default,
            // it costs the third star)
            get => PlayerPrefs.GetInt(K + "assist_v2", 0) == 1;
            set { PlayerPrefs.SetInt(K + "assist_v2", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt(K + "vib", 1) == 1;
            set { PlayerPrefs.SetInt(K + "vib", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(K + "vol", 0.9f);
            set { PlayerPrefs.SetFloat(K + "vol", Mathf.Clamp01(value)); PlayerPrefs.Save(); AudioListener.volume = value; }
        }

        /// <summary>Graphics quality: always the highest (2) — the game is shown at its best on every device; there is no
        /// setting for it any more. (0 = performance, 1 = balanced were the old choices.)</summary>
        public static int Quality
        {
            get => 2;
            set { }
        }

        public static bool KillCam
        {
            get => PlayerPrefs.GetInt(K + "killcam", 1) == 1;
            set { PlayerPrefs.SetInt(K + "killcam", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool SeenTutorial
        {
            get => PlayerPrefs.GetInt(K + "tut", 0) == 1;
            set { PlayerPrefs.SetInt(K + "tut", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void ResetAll()
        {
            float sens = Sensitivity; bool assist = AimAssist; float vol = Volume; int q = Quality; bool all = UnlockAll;
            // what was paid for survives a progress reset: "no ads", the one-time packs, the diamonds
            bool noAds = NoAds; int gems = Gems;
            var keep = new System.Collections.Generic.List<string>();
            foreach (var p in Store.AllRealMoney())
                foreach (var f in new[] { "iap_" + p.id, "gift_" + p.id })
                    if (Flag(f)) keep.Add(f);
            if (Flag("payer")) keep.Add("payer");
            gen = PlayerPrefs.GetInt("sc2gen", 0) + 1;
            PlayerPrefs.SetInt("sc2gen", gen);
            Sensitivity = sens; AimAssist = assist; Volume = vol; Quality = q; UnlockAll = all;
            NoAds = noAds; Gems = gems;
            foreach (var k in keep) SetFlag(k);
            // the rifle packs come back with their rifles (the store re-applies them at the next launch as well)
            foreach (var p in Store.Permanent) if (keep.Contains("iap_" + p.id)) Store.ApplyEntitlements(new[] { p.id });
            PlayerPrefs.Save();
        }
    }
}

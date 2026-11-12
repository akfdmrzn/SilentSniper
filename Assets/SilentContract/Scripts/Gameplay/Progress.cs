using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// The player's career: experience and level (rank), the premium currency (ELMAS) and the rewards a level brings.
    /// Every finished contract gives experience (a failed one a little); each level pays cash and diamonds.
    /// </summary>
    public static class Progress
    {
        public const int StartGems = 25;

        // ------------------------------------------------------------------ level curve
        /// <summary>Experience needed to go from <paramref name="level"/> to the next one.</summary>
        public static int Need(int level) => 250 + 110 * (Mathf.Max(1, level) - 1);

        public static int Level => Split(Save.Xp, out _, out _);

        /// <summary>Level for a total of <paramref name="xp"/>, plus the experience into that level and what it needs.</summary>
        public static int Split(int xp, out int into, out int need)
        {
            int lvl = 1;
            int left = Mathf.Max(0, xp);
            while (lvl < 99 && left >= Need(lvl)) { left -= Need(lvl); lvl++; }
            into = left;
            need = Need(lvl);
            return lvl;
        }

        /// <summary>0..1 progress inside the current level.</summary>
        public static float Fraction(int xp)
        {
            Split(xp, out int into, out int need);
            return need > 0 ? Mathf.Clamp01(into / (float)need) : 0f;
        }

        static readonly string[] Ranks = { "ÇAYLAK", "TETİKÇİ", "NİŞANCI", "KESKİN NİŞANCI", "GÖLGE", "HAYALET", "EFSANE" };

        public static string RankName(int level)
        {
            int i = level <= 2 ? 0 : level <= 5 ? 1 : level <= 9 ? 2 : level <= 14 ? 3 : level <= 19 ? 4 : level <= 29 ? 5 : 6;
            return Ranks[i];
        }

        /// <summary>Rank colour: grey → blue → purple → gold.</summary>
        public static Color RankColor(int level)
        {
            if (level <= 2) return Util.Hex("#9AA6B2");
            if (level <= 5) return Util.Hex("#46C8F2");
            if (level <= 9) return Util.Hex("#36D47C");
            if (level <= 14) return Util.Hex("#B07CFF");
            if (level <= 19) return Util.Hex("#FF8A2B");
            return Util.Hex("#FFB21E");
        }

        // ------------------------------------------------------------------ rewards
        public struct LevelReward { public int level, cash, gems; public string unlock; }

        public static LevelReward RewardFor(int level)
        {
            // economy: a level pays a little cash and a few diamonds (every fifth a bigger handful)
            var r = new LevelReward { level = level, cash = 300 + 150 * level, gems = level % 5 == 0 ? 20 : 5 };
            switch (level)
            {
                case 3: r.unlock = "Günün sözleşmesi"; break;
                case 5: r.unlock = "Elmasla son geliştirme kademeleri"; break;
                case 10: r.unlock = "Efsane rütbe yolu"; break;
            }
            return r;
        }

        /// <summary>Adds experience; pays every level crossed and returns those levels' rewards (empty: no level up).</summary>
        public static List<LevelReward> AddXp(int amount)
        {
            var gained = new List<LevelReward>();
            if (amount <= 0) return gained;
            int before = Level;
            Save.Xp = Save.Xp + amount;
            int after = Level;
            for (int l = before + 1; l <= after; l++)
            {
                var r = RewardFor(l);
                AddCash(r.cash, "level_up");
                AddGems(r.gems, "level_up");
                gained.Add(r);
                Analytics.Event("level_up", "level", l, "character", RankName(l));
                Analytics.MetaEvent("level", 0f, l.ToString());
            }
            return gained;
        }

        // ------------------------------------------------------------------ diamonds
        public static int Gems => Save.Gems;
        /// <summary>Diamonds in; <paramref name="source"/> names where they came from (analytics: earn_virtual_currency).</summary>
        public static void AddGems(int n, string source = null)
        {
            if (n <= 0) return;
            Save.Gems = Save.Gems + n;
            Analytics.Event("earn_virtual_currency", "virtual_currency_name", "gems", "value", n, "source", source ?? "other");
        }

        public static void AddCash(int n, string source = null)
        {
            if (n <= 0) return;
            Save.Cash += n;
            Analytics.Event("earn_virtual_currency", "virtual_currency_name", "cash", "value", n, "source", source ?? "other");
        }

        public static bool SpendGems(int n, string item = null)
        {
            if (n <= 0) return true;
            if (Save.Gems < n) return false;
            Save.Gems = Save.Gems - n;
            Analytics.Event("spend_virtual_currency", "virtual_currency_name", "gems", "value", n, "item_name", item ?? "other");
            Analytics.MetaEvent("spent", n, item);
            return true;
        }

        public static bool SpendCash(int n, string item = null)
        {
            if (n <= 0) return true;
            if (Save.Cash < n) return false;
            Save.Cash -= n;
            Analytics.Event("spend_virtual_currency", "virtual_currency_name", "cash", "value", n, "item_name", item ?? "other");
            return true;
        }

        /// <summary>Contracts finished at least once.</summary>
        public static int CompletedCount
        {
            get
            {
                int n = 0;
                foreach (var m in MissionDefs.All) if (Save.Completed(m.id)) n++;
                return n;
            }
        }

        // ------------------------------------------------------------------ experience of a contract
        /// <summary>Experience for a finished (or failed) contract; filled into the result's ledger by the runtime.</summary>
        public static int ContractXp(MissionRuntime r)
        {
            if (r == null || r.Def == null) return 0;
            if (!r.Success) return 25 + 8 * Mathf.Min(6, r.Hits);
            float xp = 110 + 8 * r.Def.number + 40 * r.Stars;
            switch (r.Grade) { case "S": xp += 120; break; case "A": xp += 70; break; case "B": xp += 35; break; }
            xp += 15 * Mathf.Min(6, r.Headshots);
            xp += Mathf.Min(80f, r.LongestKill / 8f);
            if (r.Veteran) xp *= 1.5f;
            if (r.Daily) xp *= 1.5f;
            return Mathf.RoundToInt(xp / 5f) * 5;
        }

        /// <summary>Diamonds a contract pays besides levels: the first S of a contract, the daily contract.</summary>
        public static int ContractGems(MissionRuntime r, bool firstS)
        {
            if (r == null || !r.Success) return 0;
            int g = 0;
            if (firstS) g += 5;
            if (r.Daily) g += 10;
            return g;
        }

        /// <summary>A career from before experience existed gets the experience its finished contracts are worth (once,
        /// silently — no level rewards).</summary>
        public static void Migrate()
        {
            if (Save.Flag("xp_v1")) return;
            Save.SetFlag("xp_v1");
            if (Save.Xp > 0) return;
            int xp = 0;
            foreach (var m in MissionDefs.All)
                if (Save.Completed(m.id)) xp += 110 + 8 * m.number + 40 * Save.Stars(m.id);
            if (xp > 0) Save.Xp = xp;
        }

        // ------------------------------------------------------------------ recommended rifle power
        /// <summary>The rifle power a contract is balanced for (shown on the briefing; below it the job is harder).</summary>
        public static int Recommended(MissionDef d) => d == null ? 100 : 100 + (d.number - 1) * 20;
    }
}

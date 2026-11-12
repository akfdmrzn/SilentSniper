using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// Three small jobs a day (two contracts, three head shots, a long shot, a clean job ...): progress comes from every
    /// finished contract, each pays diamonds or cash when it is collected on the menu. A new set every day.
    /// </summary>
    public static class DailyTasks
    {
        public class Task
        {
            public string id, text;
            public int goal, gems, cash;
            public Func<MissionRuntime, int> count;
        }

        static readonly Task[] Pool =
        {
            new Task { id = "win2", text = "2 sözleşme tamamla", goal = 2, cash = 1500, count = r => r.Success ? 1 : 0 },
            new Task { id = "head3", text = "3 kafadan vuruş yap", goal = 3, gems = 5, count = r => r.Headshots },
            new Task { id = "long", text = "250 m'den uzak bir isabet", goal = 1, gems = 5, count = r => r.LongestKill >= 250f ? 1 : 0 },
            new Task { id = "silent", text = "Alarm vermeden bir iş bitir", goal = 1, cash = 2000, count = r => r.Success && !r.AlarmEver ? 1 : 0 },
            new Task { id = "sgrade", text = "S derecesi al", goal = 1, gems = 10, count = r => r.Success && r.Grade == "S" ? 1 : 0 },
            new Task { id = "stars5", text = "5 yıldız topla", goal = 5, cash = 2500, count = r => r.Success ? r.Stars : 0 },
            new Task { id = "armor2", text = "2 zırhlı adamı sustur", goal = 2, gems = 8, count = r => r.ArmoredKilled },
            new Task { id = "vet", text = "VETERAN olarak bir iş bitir", goal = 1, gems = 10, count = r => r.Success && r.Veteran ? 1 : 0 },
            new Task { id = "clean", text = "Sivillere dokunmadan 2 iş bitir", goal = 2, cash = 2000, count = r => r.Success && r.CiviliansKilled + r.CiviliansHurt == 0 ? 1 : 0 },
        };

        static string Day => DateTime.Now.ToString("yyyyMMdd");
        static string Key(Task t, string what) => "sc2_dt_" + Day + "_" + t.id + "_" + what;

        /// <summary>Today's three (picked once and saved: the same all day even when the player levels up, a different
        /// mix tomorrow).</summary>
        public static List<Task> Today()
        {
            string saved = PlayerPrefs.GetString("sc2_dt_" + Day + "_ids", "");
            if (!string.IsNullOrEmpty(saved))
            {
                var keep = new List<Task>();
                foreach (var id in saved.Split(','))
                    foreach (var t in Pool) if (t.id == id) keep.Add(t);
                if (keep.Count == 3) return keep;
            }
            var pick = Pick();
            var ids = new List<string>();
            foreach (var t in pick) ids.Add(t.id);
            PlayerPrefs.SetString("sc2_dt_" + Day + "_ids", string.Join(",", ids.ToArray()));
            PlayerPrefs.Save();
            return pick;
        }

        static List<Task> Pick()
        {
            int seed = int.Parse(Day) * 7919;
            var rng = new System.Random(seed);
            var idx = new List<int>();
            for (int i = 0; i < Pool.Length; i++) idx.Add(i);
            // VETERAN and armour jobs only once they make sense
            if (Progress.Level < 3) idx.Remove(7);
            if (!MissionDefs.All.Exists(m => m.number >= 9 && MissionDefs.IsUnlocked(m))) idx.Remove(6);
            for (int i = idx.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = idx[i]; idx[i] = idx[j]; idx[j] = t; }
            var list = new List<Task>();
            for (int i = 0; i < 3 && i < idx.Count; i++) list.Add(Pool[idx[i]]);
            return list;
        }

        public static int Count(Task t) => Mathf.Min(t.goal, PlayerPrefs.GetInt(Key(t, "n"), 0));
        public static bool Done(Task t) => Count(t) >= t.goal;
        public static bool Claimed(Task t) => PlayerPrefs.GetInt(Key(t, "c"), 0) == 1;
        public static int Ready { get { int n = 0; foreach (var t in Today()) if (Done(t) && !Claimed(t)) n++; return n; } }

        /// <summary>A contract ended: today's jobs count what it did.</summary>
        public static void Record(MissionRuntime r)
        {
            if (r == null) return;
            foreach (var t in Today())
            {
                if (Claimed(t)) continue;
                int add = 0;
                try { add = t.count(r); } catch (Exception e) { Debug.LogException(e); }
                if (add <= 0) continue;
                PlayerPrefs.SetInt(Key(t, "n"), Mathf.Min(t.goal, PlayerPrefs.GetInt(Key(t, "n"), 0) + add));
            }
            PlayerPrefs.Save();
        }

        /// <summary>Pays a finished job (once).</summary>
        public static bool Claim(Task t)
        {
            if (t == null || !Done(t) || Claimed(t)) return false;
            PlayerPrefs.SetInt(Key(t, "c"), 1);
            PlayerPrefs.Save();
            if (t.gems > 0) Progress.AddGems(t.gems, "daily_task");
            if (t.cash > 0) Progress.AddCash(t.cash, "daily_task");
            Analytics.Event("daily_task_claim", "task", t.id);
            return true;
        }
    }
}

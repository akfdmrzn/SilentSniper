using UnityEngine;

namespace SilentContract
{
    /// <summary>How much the scope helps.</summary>
    public enum AssistLevel
    {
        /// <summary>No impact dot: the shooter reads range and wind and holds over on the reticle.</summary>
        None,
        /// <summary>The impact dot shows only while the breath is held (a steady rifle earns the help).</summary>
        Breath,
        /// <summary>The impact dot always (the first contracts, or the "easy aim" setting).</summary>
        Full,
    }

    /// <summary>
    /// The shooting side of a contract's difficulty, from its place in the campaign: how much the scope helps, how much
    /// the rifle sways, how gusty the wind is and how exactly the wind is reported. A new skill comes in every few
    /// contracts and stays: the first four teach the basics with full help, from the fifth the help needs a held breath,
    /// from the thirteenth (the city) the shooter is on his own, on the mountain the wind gusts hard and is only felt.
    /// A contract may override any of it (<see cref="MissionDef.assist"/> and friends).
    /// </summary>
    public class Difficulty
    {
        /// <summary>0 training (1-4), 1 harbour (5-12), 2 city (13-20), 3 mountain (21-28).</summary>
        public int tier;
        public AssistLevel assist;
        /// <summary>Scope sway multiplier.</summary>
        public float sway;
        /// <summary>Wind gusts: fraction of the base strength the wind swings by (0 = steady).</summary>
        public float gust;
        /// <summary>Gust tempo: seconds for one swell.</summary>
        public float gustPeriod;
        /// <summary>The wind panel: 2 = speed, side and the hold-off in mils, 1 = speed and side, 0 = only "light / fresh / strong".</summary>
        public int windInfo;
        /// <summary>Bullet-drop marks on the reticle for 200, 300, 400 m.</summary>
        public bool dropMarks;
        /// <summary>The scope's help (metres at the target): a round passing this close to a fair target's body is
        /// pulled onto him (<see cref="Ballistics.PredictAssisted"/>). Generous while the basics are taught, a few
        /// centimetres on the mountain, almost nothing on a veteran run.</summary>
        public float magnet;
        /// <summary>… plus this much per metre of range (what a steady hand still cannot hold far out).</summary>
        public float magnetPerM;
        /// <summary>The "easy aim" setting is on: full help, the third star is not awarded.</summary>
        public bool easy;
        /// <summary>V2 replay mode on a finished contract: no impact dot, harder and gustier wind, more sway, no target
        /// marks, ×1.5 pay.</summary>
        public bool veteran;

        /// <summary>The contract the briefing's VETERAN switch is on for (null = none).</summary>
        public static string VeteranId;
        public static bool IsVeteran(MissionDef m) => m != null && VeteranId != null && VeteranId == m.id;

        /// <summary>V2 "contract of the day": one finished contract a day, played as VETERAN for double pay.</summary>
        public static string DailyId;
        public static bool IsDaily(MissionDef m) => m != null && DailyId != null && DailyId == m.id && IsVeteran(m);
        public static string TodayKey => System.DateTime.Now.ToString("yyyyMMdd");

        /// <summary>Today's pick among the finished contracts (the same all day), null before the first one is done.</summary>
        public static MissionDef DailyPick()
        {
            // fixed for the whole day once picked (finishing another contract must not change it)
            string key = Save.K + "daily_pick_" + TodayKey;
            string id = PlayerPrefs.GetString(key, "");
            var m = !string.IsNullOrEmpty(id) ? MissionDefs.All.Find(x => x.id == id) : null;
            if (m != null) return m;
            var done = MissionDefs.All.FindAll(x => Save.Completed(x.id));
            if (done.Count == 0) return null;
            long seed = System.DateTime.Now.Year * 400L + System.DateTime.Now.DayOfYear;
            int h = (int)((seed * 7919L) % 104729L);
            m = done[h % done.Count];
            PlayerPrefs.SetString(key, m.id);
            PlayerPrefs.Save();
            return m;
        }

        public static Difficulty For(MissionDef m)
        {
            int n = m != null ? Mathf.Max(1, m.number) : 1;
            var d = new Difficulty();
            d.tier = n <= 4 ? 0 : (n <= 12 ? 1 : (n <= 20 ? 2 : 3));
            switch (d.tier)
            {
                case 0: d.assist = AssistLevel.Full; d.sway = 0.85f; d.gust = 0.08f; d.gustPeriod = 9f; d.windInfo = 2; d.magnet = 0.25f; d.magnetPerM = 0.00025f; break;
                case 1: d.assist = AssistLevel.Breath; d.sway = 1.0f; d.gust = 0.22f; d.gustPeriod = 7f; d.windInfo = 2; d.magnet = 0.14f; d.magnetPerM = 0.00012f; break;
                case 2: d.assist = AssistLevel.None; d.sway = 1.12f; d.gust = 0.35f; d.gustPeriod = 6f; d.windInfo = 1; d.magnet = 0.10f; d.magnetPerM = 0.0001f; break;
                default: d.assist = AssistLevel.None; d.sway = 1.25f; d.gust = 0.5f; d.gustPeriod = 4.5f; d.windInfo = 0; d.magnet = 0.07f; d.magnetPerM = 0.0001f; break;
            }
            // remote knob (percent) for the whole curve. The help forgives a thumb's shake on the glass, not the
            // ballistics: from the harbour's second half on, a round the wind or the drop carried off still misses.
            float mk = Mathf.Clamp(Tuning.Int("aim_assist_pct"), 0, 300) / 100f;
            d.magnet *= mk;
            d.magnetPerM *= mk;
            d.dropMarks = true;
            if (m != null)
            {
                if (m.assist >= 0) d.assist = (AssistLevel)m.assist;
                if (m.gust >= 0f) d.gust = m.gust;
                if (m.windInfo >= 0) d.windInfo = m.windInfo;
            }
            // the wind panel always says which way and how hard it blows (the "feel only" panel was too vague)
            d.windInfo = Mathf.Max(d.windInfo, 1);
            if (IsVeteran(m))
            {
                d.veteran = true;
                d.assist = AssistLevel.None;
                d.sway *= 1.15f;
                d.gust = Mathf.Max(d.gust * 1.5f, 0.35f);
                d.gustPeriod = Mathf.Min(d.gustPeriod, 5f);
                d.windInfo = Mathf.Min(d.windInfo, 1);
                d.magnet *= 0.5f;
                d.magnetPerM *= 0.5f;
                return d;                                   // easy aim does not apply to a veteran run
            }
            if (Save.AimAssist && d.assist != AssistLevel.Full)
            {
                d.assist = AssistLevel.Full;
                d.easy = true;
            }
            if (Save.AimAssist) { d.magnet = Mathf.Max(d.magnet, 0.3f); d.magnetPerM = Mathf.Max(d.magnetPerM, 0.00025f); }
            return d;
        }

        /// <summary>
        /// The contract's difficulty, 1-10, the way the briefing shows it: the campaign stage (how much the scope helps,
        /// sway, gusts), the range, the wind, two targets, a clock, a take-alive or protect job, finding the target
        /// oneself.
        /// </summary>
        public static int Score(MissionDef m)
        {
            if (m == null) return 1;
            var d = For(m);
            float s = 1f + d.tier * 1.6f;
            s += Mathf.Clamp((m.Distance - 100f) / 60f, 0f, 2.5f);
            s += Mathf.Clamp((m.WindSpeed - 1f) / 1.5f, 0f, 2f) * (d.windInfo <= 0 ? 1.3f : 1f);
            if (m.targets.Count > 1) s += 0.7f;
            if (m.timeLimit > 0f) s += 0.6f;
            if (m.kind == "CANLI YAKALA" || m.kind == "KORUMA") s += 0.5f;
            if (m.number >= 5 && m.markTargets != 1) s += 0.6f;
            if (d.easy) s -= 1.5f;
            if (d.veteran) s += 1.5f;
            return Mathf.Clamp(Mathf.RoundToInt(s), 1, 10);
        }

        /// <summary>The skill this contract brings in (every few contracts something new, then it stays): shown in
        /// the briefing as "YENİ".</summary>
        public static string NewSkill(MissionDef m)
        {
            if (m == null) return null;
            switch (m.number)
            {
                case 1: return "Dürbün, nefes tutma ve ilk atış.";
                case 2: return "Sabır: hedef dışarı çıkana kadar bekle, nefesini tut ve kafaya nişan al.";
                case 3: return "MENZİL ayarı: uzak hedefte mermi düşer. Dikkat dağıtma: yakına düşen kurşun hedefi açığa çıkarır.";
                case 4: return "Gece ve gözcü: önce seni görebilecek olanı düşün.";
                case 5: return "Hedefi kendin bul. İsabet noktası artık sadece nefes tutarken görünür.";
                case 6: return "Hareketli hedef: yürüyüşüne göre önden nişan al.";
                case 7: return "Uzun atış ve sert rüzgâr: rüzgâr panelindeki mil değerine göre kaydır.";
                case 9: return "Koruma: kalabalıktaki tetikçileri bul, tanık yaşasın.";
                case 10: return "Canlı yakala: bacaktan vur, öldürme.";
                case 11: return "Karanlık: ışığı kesmek korumaları kör eder.";
                case 12: return "Uyuşturucu iğne: kimse ölmeyecek.";
                case 13: return "Şehir: isabet noktası yok. Iskalarsan hedef kaçar; iş bitince yerin biliniyorsa çıkış var.";
                case 14: return "Dublör: yüze iyi bak, yanlış kişiyi vurursan iş biter.";
                case 16: return "Karşı nişancı: önce o seni bulmasın.";
                case 18: return "Çift hedef: biri düşünce öteki kaçar, ikinci atış hazır olsun.";
                case 19: return "Zaman penceresi: helikopterin kapısı kapanınca iş biter.";
                case 21: return "Dağ: rüzgâr sert ve değişken. Paneldeki yön ve gücü izle, dindiği anı bekle.";
                case 25: return "Kaza: kurşun değil, çığ.";
                case 27: return "Tipi: termal dürbün olmadan göremezsin.";
                case 28: return "Ses örtüsü: havai fişek patlarken atış duyulmaz.";
            }
            return null;
        }

        public static string WindWord(float speed) => speed < 1.5f ? "HAFİF" : (speed < 3.5f ? "ORTA" : (speed < 6f ? "SERT" : "FIRTINA"));

        /// <summary>V2: from contract 9 on, part of the gunmen who come for the nest wear plate carriers (all of them from
        /// 22, and every one on VETERAN from 9).</summary>
        public static bool Armored(MissionDef m, int index)
        {
            if (m == null || m.number < 9) return false;
            if (m.number >= 22 || IsVeteran(m)) return true;
            if (m.number >= 16) return index % 2 == 0;
            return index == 0;
        }

        /// <summary>Does the contract send armoured gunmen (briefing note)?</summary>
        public static bool HasArmor(MissionDef m) => m != null && m.number >= 9;

        /// <summary>Height of the bullet over the line of sight at <paramref name="range"/> metres with the rifle zeroed at
        /// <paramref name="zero"/> (no drag in this model: the arc is a parabola through the muzzle and the zero).</summary>
        public static float DropAt(float range, float zero, float velocity)
        {
            float v2 = Mathf.Max(1f, velocity * velocity);
            return Ballistics.Gravity * range * (zero - range) / (2f * v2);
        }

        /// <summary>Sideways drift at <paramref name="range"/> metres in a crosswind of <paramref name="cross"/> m/s.</summary>
        public static float DriftAt(float range, float cross, float velocity)
        {
            float t = range / Mathf.Max(1f, velocity);
            return 0.5f * Ballistics.WindFactor * cross * t * t;
        }
    }
}

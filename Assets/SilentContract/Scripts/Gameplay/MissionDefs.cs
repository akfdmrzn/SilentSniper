using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    public class MissionTarget
    {
        public string charId;
        public string name;
        public string alias;
        public string desc;      // what to look for (clothing)
    }

    public class MissionDef
    {
        public string id;
        public int number;
        public string title;
        public string location;
        public string brief;
        public string tip;
        public string preset = "day";
        public Vector3 wind;
        public int reward = 1500;
        public float timeLimit;
        public int maxCivilians = 1;
        public string focus;
        public string coverHint;        // hint shown when the player keeps looking at the target hidden behind cover
        /// <summary>V3 (device test: "behind which window is she?"): a pin over the room the target is in — a lit office
        /// behind glass, a penthouse. The layout marker and how high above it the pin floats; the HUD labels it with
        /// HEDEF and the contract's <see cref="location"/>. It shows the place, not the person.</summary>
        public string pin;
        public float pinUp = 3.2f;
        public string icon = "target";
        public readonly List<MissionTarget> targets = new List<MissionTarget>();
        public Action<MissionRuntime> setup;
        // ---- contract presentation
        public string region = "harbor";        // location (level layout)
        public string kind = "SUİKAST";         // contract type badge: SUİKAST, CANLI YAKALA, KORUMA, SABOTAJ...
        public string clock;                    // local time in the intro card ("06:40")
        public string introLine;                // the handler on the radio during the intro
        public string handler = "KONTROL";
        public bool noIntro;
        public string gearHint;                 // briefing: recommended / handed-out gear
        public string introNote;                // intro: a red note pinned over the scene ("there is a marksman somewhere")
        /// <summary>How well the enemy shoots back once the nest is found (0 = nobody fires at it; below 0 = by the
        /// contract's place in the campaign).</summary>
        public float threat = -1f;
        /// <summary>Overrides of the campaign's shooting difficulty (<see cref="Difficulty"/>; -1 = by the campaign).</summary>
        public int assist = -1, windInfo = -1;
        /// <summary>V2: the targets are marked from the start (no identification): a car, a helicopter, a man alone on a
        /// deck. -1 = by the campaign (from the 5th contract the player identifies the target himself).</summary>
        public int markTargets = -1;
        /// <summary>V2: gadgets the job cannot be done without (bought in the armory, or handed out in <see cref="loan"/>).</summary>
        public string[] requires;

        /// <summary>The required gadgets the player has neither bought nor been lent (names).</summary>
        public List<string> MissingGear()
        {
            var miss = new List<string>();
            if (requires == null) return miss;
            foreach (var r in requires)
            {
                var g = Gear.Gadget(r);
                if (g == null || Gear.Owns(g) || loan.Exists(l => l == r || l.StartsWith(r + ":"))) continue;
                miss.Add(g.name);
            }
            return miss;
        }
        public float gust = -1f;
        public readonly List<string> goals = new List<string>();             // briefing objective lines
        public readonly List<string> loan = new List<string>();              // gear handed out for this contract
        readonly List<ChallengeDef> challenges = new List<ChallengeDef>();
        List<ChallengeDef> defaults;

        /// <summary>The three star challenges (defaults: complete, no civilians, headshot).</summary>
        public List<ChallengeDef> Challenges
        {
            get
            {
                if (challenges.Count > 0) return challenges;
                return defaults ?? (defaults = ChallengeDef.Defaults());
            }
        }

        public MissionDef Target(string charId, string name, string alias, string desc)
        {
            targets.Add(new MissionTarget { charId = charId, name = name, alias = alias, desc = desc });
            return this;
        }

        public MissionDef Goal(string text) { goals.Add(text); return this; }
        public MissionDef Loan(params string[] items) { loan.AddRange(items); return this; }

        public MissionDef Star(ChallengeKind k, string text, float value = 0f, Func<MissionRuntime, bool> check = null)
        {
            challenges.Add(new ChallengeDef(k, text, value, check));
            return this;
        }

        /// <summary>Approximate distance from the sniper nest to the mission area (from the map's layout data).</summary>
        public float Distance => Locations.Distance(region, focus);

        public LocationDef Location => Locations.Get(region);

        public float WindSpeed => new Vector2(wind.x, wind.z).magnitude;
    }

    /// <summary>The campaign: contracts per map (harbour, city centre), each with its own time of day, weather and routine.</summary>
    public static partial class MissionDefs
    {
        public static readonly List<MissionDef> All = Build();

        public static MissionDef Get(string id) => All.Find(m => m.id == id);

        /// <summary>Contracts of one map in campaign order.</summary>
        public static List<MissionDef> InLocation(string locationId) => All.FindAll(m => m.region == locationId);

        /// <summary>The next contract on the same map (null after the last one).</summary>
        public static MissionDef Next(MissionDef m)
        {
            if (m == null) return null;
            var list = InLocation(m.region);
            int i = list.IndexOf(m);
            return i >= 0 && i + 1 < list.Count ? list[i + 1] : null;
        }

        /// <summary>Open when its map is open and the previous contract on that map is done (the first one opens with the map).</summary>
        public static bool IsUnlocked(MissionDef m)
        {
            if (m == null) return false;
            if (Save.UnlockAll) return true;
            if (!Locations.IsOpen(m.Location)) return false;
            var list = InLocation(m.region);
            int i = list.IndexOf(m);
            if (i <= 0) return true;
            return Save.Completed(list[i - 1].id) || (m.region == "harbor" && m.number <= Save.Unlocked);
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static List<MissionDef> Build()
        {
            var all = BuildHarbor();
            AddCity(all);
            AddMountain(all);
            return all;
        }

        static List<MissionDef> BuildHarbor()
        {
            var list = new List<MissionDef>();

            // ------------------------------------------------------------------ 1. customs gate (tutorial)
            list.Add(new MissionDef
            {
                id = "m1", number = 1, title = "GÜMRÜK KAPISI", location = "Liman giriş kapısı",
                preset = "day", wind = V(1.2f, 0, 0.4f), reward = 1500, focus = "gate_booth", icon = "shield",
                clock = "08:20", introLine = "Selim Kaya kapıda denetimde. Kimliğini tara, sonra işini bitir.",
                brief = "Gümrük müdürü Selim Kaya, rüşvet karşılığında kaçakçılara liman kapısını açıyor. Her sabah kapıdaki güvenlik noktasını denetliyor.\n\nKapıdan ayrılmadan onu etkisiz hale getir. Alarm verilirse otoparktaki aracına koşar.",
                tip = "Dürbünü aç ve nişangahı birinin üzerinde tut: kimliği otomatik taranır. Kırmızı işaret hedefi gösterir.",
                setup = rt =>
                {
                    var A = V(-0.3f, 0, 6.2f); var B = V(4.0f, 0, 5.4f); var C = V(8.6f, 0, 9.4f);
                    var t = rt.AddTarget("businessman", A, 190f);
                    t.SetPlan(Act.IdleFacing("phone_talk", 190, 11), Act.Move(new[] { A, B }), Act.IdleFacing("talk", 150, 9),
                              Act.Move(new[] { B, C }), Act.IdleFacing("smoke", 230, 13), Act.Move(new[] { C, B, A }), Act.Loop(0));
                    t.FleePath = new[] { V(8, 0, 12), V(28, 0, 13.5f), V(35.6f, 0, 14.6f) };
                    t.HideOnFlee = true;
                    var g1 = rt.Spawn("guard_a", V(2.4f, 0, 4.2f), 170, Role.Guard, "Kapı Nöbetçisi");
                    rt.Idler(g1, 170, "guard_pose", "idle_look", "idle");
                    var g2 = rt.Spawn("guard_b", rt.M("gate_booth"), 180, Role.Guard, "Kulübe Görevlisi");
                    rt.Idler(g2, 180, "idle_look", "phone", "idle_arms");
                    var w1 = rt.Spawn("worker_a", rt.P("gate_in")[0], 90, Role.Worker, "Liman İşçisi");
                    rt.Walker(w1, rt.P("gate_in"), "walk", 0.9f, 6f);
                    var w2 = rt.Spawn("worker_c", V(-9, 0, 13), 140, Role.Worker, "Temizlik Görevlisi");
                    rt.Idler(w2, 140, "sweep", "idle", "sweep");
                    var c1 = rt.Spawn("civilian_m", V(36.5f, 0, 18.6f), 270, Role.Civilian, "Şoför");
                    rt.Idler(c1, 270, "phone_talk", "idle_wait", "phone");
                    var c2 = rt.Spawn("civilian_f", V(-120, 0.12f, -28.4f), 90, Role.Civilian, "Yaya");
                    rt.Walker(c2, new[] { V(-120, 0.12f, -28.4f), V(110, 0.12f, -28.4f) }, "walk", 1.6f, 3f);
                    var c3 = rt.Spawn("guest_m", V(90, 0.12f, -30.2f), 270, Role.Civilian, "Yaya");
                    rt.Walker(c3, new[] { V(90, 0.12f, -30.2f), V(-140, 0.12f, -30.2f) }, "walk", 1f, 2f);
                }
            }.Target("businessman", "Selim Kaya", "Gümrük Müdürü", "Lacivert takım elbise, kırmızı kravat, gözlük.")
             .Goal("Gümrük müdürü Selim Kaya'yı etkisiz hale getir"));

            // ------------------------------------------------------------------ 2. port office smoke break
            list.Add(new MissionDef
            {
                id = "m2", number = 2, title = "LİMAN OFİSİ", location = "Liman yönetim binası",
                preset = "sunset", wind = V(-2.2f, 0, 1.0f), reward = 2000, focus = "office_door", icon = "user_search",
                clock = "18:05", introLine = "Aydın sigara için dışarı çıkacak. Kapıdan uzaklaştığı an senin anın.",
                brief = "Liman müdür yardımcısı Can Aydın, kaçak sevkiyatların kayıtlarını siliyor. Binadan sadece sigara molalarında çıkıyor.\n\nKapıdan çıktığında fırsatı kaçırma: alarm verilirse binaya kapanır ve görev başarısız olur.",
                tip = "Hedef bina içindeyken bekle. Kapıdan çıkınca sigara içtiği noktada sabit durur — nefesini tut ve kafasına nişan al.",
                setup = rt =>
                {
                    var door = rt.M("office_door"); var smoke = rt.M("office_smoke");
                    var mid = Vector3.Lerp(door, smoke, 0.5f) + V(0, 0, -1.2f);
                    var t = rt.AddTarget("guest_m", door, 180f);
                    t.SetHidden(true);
                    t.SetPlan(Act.Idle("idle", 4), Act.Do(() => t.SetHidden(false)), Act.Move(new[] { door, mid, smoke }),
                              Act.IdleFacing("smoke", 200, 15), Act.IdleFacing("phone_talk", 165, 10), Act.IdleFacing("smoke", 210, 8),
                              Act.Move(new[] { smoke, mid, door }), Act.Hide(), Act.Idle("idle", 10), Act.Do(() => t.SetHidden(false)), Act.Loop(2));
                    t.FleePath = new[] { door };
                    t.HideOnFlee = true;
                    t.SecondChance = true;
                    var a = rt.Spawn("civilian_f", smoke + V(-1.6f, 0, -0.6f), 60, Role.Civilian, "Sekreter");
                    rt.Idler(a, 60, "talk", "phone", "idle", "talk");
                    var g = rt.Spawn("guard_b", door + V(3.2f, 0, -1.0f), 185, Role.Guard, "Bina Güvenliği");
                    rt.Idler(g, 185, "guard_pose", "idle_look");
                    var g2 = rt.Spawn("guard_a", V(-82, 0, 7.8f), 90, Role.Guard, "Devriye");
                    rt.PingPong(g2, new[] { V(-82, 0, 7.8f), V(-34, 0, 7.8f) }, "walk", 0.85f, "idle_look", 4f);
                    var w = rt.Spawn("worker_b", rt.P("gate_in")[0], 90, Role.Worker, "Liman İşçisi");
                    rt.Walker(w, rt.P("gate_in"), "walk", 1f, 8f);
                    var w2 = rt.Spawn("worker_c", rt.M("office_bench"), 170, Role.Worker, "Formen");
                    rt.Idler(w2, 170, "coffee", "idle_wait", "coffee");
                    var c = rt.Spawn("civilian_m", V(60, 0.12f, -29), 270, Role.Civilian, "Yaya");
                    rt.Walker(c, new[] { V(60, 0.12f, -29), V(-160, 0.12f, -29) }, "walk", 1f, 2f);
                }
            }.Target("guest_m", "Can Aydın", "Müdür Yardımcısı", "Mavi polo tişört, siyah pantolon. Sigara molasında dışarı çıkar.")
             .Goal("Can Aydın'ı binaya geri girmeden vur")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.NoAlarm, "Alarm verdirme")
             .Star(ChallengeKind.AllHeadshots, "Hedefi kafadan vur"));

            // ------------------------------------------------------------------ 3. container deal (rain)
            // Tarık drives in through the gate, parks at the mouth of the container lane and walks to the buyer, who
            // waits between the stacks where the roof cannot see. The player gets him on the walk, or lures him out:
            // a shot into the lane (he walks to the open gap to look), his car alarm (he comes back to check the car),
            // or waits for the deal to end (he walks back to the car). A second scare: he runs for the car and drives
            // off.
            list.Add(new MissionDef
            {
                id = "m3", number = 3, title = "KONTEYNER TAKASI", location = "Konteyner sahası",
                preset = "overcast", wind = V(3.2f, 0, -0.8f), reward = 2600, focus = "deal_spot", icon = "map",
                clock = "11:30", introLine = "Kuzgun limana giriyor. Takas yeri çatıdan görünmüyor — onu dışarı çekmen gerekecek.",
                brief = "Kuzgun lakaplı kaçakçı Tarık Bal arabasıyla limana gelecek ve konteynerlerin arasında bir alıcıyla buluşacak. Takas bitince arabasına binip gidecek.\n\nTakas yeri çatıdan görünmüyor. Ya yürürken vur ya da onu açığa çıkar: arabasının alarmı ya da yakınına düşen bir kurşun. İkinci kez ürkerse arabasına koşar.",
                tip = "Hedef görünmüyorsa yakınına ateş et: sese bakmak için açığa çıkar. Arabasını vurursan alarm çalar, kontrol etmeye gelir.",
                coverHint = "Tarık konteynerlerin arasında, buradan görünmüyor. Arabasına ateş et: alarm çalınca kontrol etmeye gelir. Ya da ara yolun açık kısmına ateş et, sese bakmak için oraya yürür.",
                setup = rt =>
                {
                    var car = Level.I.Tagged("deal_car");
                    var carIn = rt.P("deal_car_in");
                    var carOut = rt.P("deal_car_out");
                    var spot = rt.M("deal_spot");                        // between the stacks, hidden from the roof
                    var gap = V(-78f, 0, 116.2f);                        // the open window of the lane (seen from the roof)
                    var laneIn = V(-84.5f, 0, 115.8f);

                    var t = rt.AddTarget("smuggler", carIn[0], 270f);
                    t.SetHidden(true);                                   // in the car while it drives in (marker on the car)
                    t.MarkWhenHidden = true;
                    t.HiddenStatus = "Arabasında — limana geliyor";
                    t.InvestigatePath = new[] { laneIn, gap };
                    t.EscapeText = "Hedef arabasıyla kaçtı";
                    t.EscapeFailDelay = 3.6f;
                    t.FleeEndPause = 1.2f;
                    bool parked = false, leaving = false, pendingLeave = false;
                    Vector3[] toCar = null;
                    float faceCar = 0f;
                    // in the car: it backs out of the spot and leaves through the gate
                    t.OnArrivedEscape += n =>
                    {
                        if (!parked) return;
                        rt.StopCarAlarm(car);
                        rt.MoveProp("deal_car", carOut, 9f, false, 4);
                    };
                    System.Action leaveNow = () =>
                    {
                        if (leaving || toCar == null || t == null || t.Dead || t.Escaped || t.Panicking || t.Incapacitated) return;
                        leaving = true;
                        t.EndInvestigation();
                        rt.Say("Takas bitti — hedef arabasına dönüyor!", Theme.Danger, "run");
                        t.SetPlan(Act.Move(toCar, "walk_confident"), Act.IdleFacing("idle_look", faceCar, 1.0f), Act.Escape());
                    };
                    // the deal is over: back to the car — or, if he is out checking a noise, straight after that
                    System.Action leave = () =>
                    {
                        if (t != null && t.Investigating)
                        {
                            pendingLeave = true;
                            rt.When(() => leaving || t == null || !t.Investigating, leaveNow);     // however the look-round ends
                            return;
                        }
                        leaveNow();
                    };
                    t.OnInvestigateEnd = n =>
                    {
                        rt.StopCarAlarm(car);
                        if (pendingLeave) rt.At(rt.Elapsed + 0.05f, leaveNow);      // after he turns away from the noise
                    };
                    var deal = new[]
                    {
                        Act.Do(() => { rt.Countdown("Takasın bitmesine", 60f); rt.At(rt.Elapsed + 60f, leave); }),
                        Act.IdleFacing("talk", 270, 12), Act.IdleFacing("handshake", 270, 5.5f), Act.IdleFacing("argue", 270, 9),
                        Act.IdleFacing("pay", 270, 6.5f), Act.IdleFacing("idle_wait", 260, 10), Act.IdleFacing("talk", 270, 10),
                        Act.IdleFacing("phone_talk", 250, 7),
                        Act.Loop(3)                                      // index in the full plan: look, walk, countdown, talk...
                    };

                    // the car comes along the road, turns in at the gate and parks at the mouth of the lane
                    // (from a little way up the road, at a harbour-yard pace: no long wait for him to arrive)
                    carIn[0] = Vector3.Lerp(carIn[0], carIn[1], 0.8f);       // V3: a little closer and quicker (~12 s)
                    var mover = rt.MoveProp("deal_car", carIn, 12.5f, false);
                    if (car != null)
                    {
                        t.transform.SetPositionAndRotation(car.transform.TransformPoint(new Vector3(-0.42f, 0.35f, 0.1f)), car.transform.rotation);
                        rt.AttachToProp(t, "deal_car");
                    }
                    rt.At(4.5f, () => { if (!parked) rt.Say("Hedefin arabası kapıdan girdi", Theme.Accent, "target"); });
                    System.Action arrive = () =>
                    {
                        if (t == null || t.Dead) return;
                        if (t.Escaped)
                        {
                            // the alarm went off while he was still driving in: he does not stop
                            parked = true;
                            rt.MoveProp("deal_car", carOut, 9f, false, 4);
                            return;
                        }
                        parked = true;
                        var park = carIn[carIn.Length - 1];
                        var c = car != null ? car.transform.position : park;
                        c.y = 0f;
                        var fwd = car != null ? car.transform.forward : park - carIn[carIn.Length - 2];
                        fwd.y = 0f;
                        fwd.Normalize();
                        var left = new Vector3(-fwd.z, 0f, fwd.x);
                        var door = c + left * 1.5f + fwd * 0.35f;          // driver's side faces the roof
                        var corner = c + fwd * 2.95f + left * 1.45f;
                        faceCar = MissionRuntime.Yaw(door, c);
                        var toSpot = new[] { door, corner, V(-55, 0, 114.8f), V(-62, 0, 115.6f), gap, laneIn, spot };
                        toCar = new[] { laneIn, gap, V(-62, 0, 115.6f), V(-55, 0, 114.8f), corner, door };
                        t.CarPath = toCar;
                        t.FleePath = toCar;
                        t.FleeEndYaw = faceCar;
                        t.DistractSpot = c + Vector3.up * 1.1f;            // the HUD marks the car itself
                        rt.DetachFromProp(t);
                        t.transform.SetPositionAndRotation(door, Quaternion.Euler(0, faceCar + 180f, 0));
                        t.MarkWhenHidden = false;
                        t.HiddenStatus = null;
                        t.SetHidden(false);
                        var plan = new List<Act> { Act.IdleFacing("idle_look", 250, 1.4f), Act.Move(toSpot, "walk_confident") };
                        plan.AddRange(deal);
                        t.SetPlan(plan);
                        // shooting his car sets off the alarm: he comes back to check it (the alarm is the only scare)
                        rt.MakeShootable("deal_car", "car", (s, p) =>
                        {
                            if (s.Hits == 1) rt.Say("Araç alarmı çalıyor!", Theme.Accent, "sound");
                            rt.CarAlarm(car, 14f);
                        }, true);
                    };
                    if (mover != null && mover.Running) mover.Arrived = arrive;
                    else arrive();                                      // no car to ride in: he is simply there

                    var b = rt.Spawn("bodyguard_a", rt.M("deal_buyer"), 90, Role.Bodyguard, "Alıcı", "SİLAHLI");
                    b.SetPlan(Act.IdleFacing("idle_wait", 90, 14), Act.IdleFacing("phone_talk", 90, 9), Act.IdleFacing("talk2", 90, 12),
                              Act.IdleFacing("handshake", 90, 5.5f), Act.IdleFacing("argue", 90, 8), Act.IdleFacing("idle_arms", 90, 10), Act.Loop(0));
                    var g1 = rt.Spawn("guard_a", rt.M("deal_guard1"), rt.MY("deal_guard1"), Role.Guard, "Alıcının Adamı");
                    rt.Idler(g1, rt.MY("deal_guard1"), "guard_pose", "idle_look");
                    var g2 = rt.Spawn("guard_b", rt.M("deal_guard2"), rt.MY("deal_guard2"), Role.Guard, "Alıcının Adamı");
                    rt.Idler(g2, rt.MY("deal_guard2"), "idle_look", "guard_pose");
                    var w = rt.Spawn("worker_b", rt.P("yard_patrol_b")[0], 0, Role.Worker, "Saha İşçisi");
                    rt.Patrol(w, rt.P("yard_patrol_b"), "walk", 0.9f);
                    var w2 = rt.Spawn("worker_a", V(-131, 0, 101.6f), 0, Role.Worker, "Halatçı");
                    rt.Idler(w2, 0, "coil_rope", "idle");
                    var w3 = rt.Spawn("worker_c", V(-60, 0, 86.5f), 90, Role.Worker, "Forklift Ustası");
                    rt.Idler(w3, 90, "idle_wait", "signal", "idle");
                }
            }.Target("smuggler", "Tarık Bal", "Kuzgun", "Gri tişört, kot pantolon, kırmızı bere. Kolları dövmeli.")
             .Goal("Kuzgun'u takas bitmeden etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.NoAlarm, "Alarm verdirme")
             .Star(ChallengeKind.MaxShots, "En fazla 3 atış", 3));

            // ------------------------------------------------------------------ 4. night fire barrel (watchtower)
            list.Add(new MissionDef
            {
                id = "m4", number = 4, title = "GECE NÖBETİ", location = "Depolar ve gözetleme kulesi",
                preset = "night", wind = V(-1.6f, 0, -2.4f), reward = 3000, focus = "stove", icon = "moon",
                clock = "23:50", introLine = "Rico ve adamları ateşin başında. Kuledeki gözcü her şeyi görür — önce onu düşün.",
                brief = "Paralı asker Rico Vargas ve adamları depoları koruyor. Gece soğuğunda ateş varilinin başında toplanıyorlar.\n\nKuledeki gözcü cesetleri görürse alarm verir. Önce onu susturmak işini kolaylaştırır.",
                tip = "Ateşin yanındaki kırmızı yakıt bidonu tek atışta patlar ve yanındakileri götürür — ama yanlarındaki bekçi de ölür (sivil kayıp, yıldız kaybı) ve patlama sessiz iş primini yakar. Temiz iş için Rico'yu doğrudan vur.",
                setup = rt =>
                {
                    var stove = rt.M("stove");
                    var t = rt.AddTarget("rico", stove + V(0.2f, 0, 1.4f), 185f);
                    t.SetPlan(Act.IdleFacing("idle_arms", 185, 12), Act.IdleFacing("talk2", 225, 10), Act.IdleFacing("coffee", 190, 10),
                              Act.IdleFacing("argue", 140, 7), Act.Loop(0));
                    t.FleePath = new[] { stove + V(4, 0, 6), rt.M("wh_c_door1") };
                    var w = rt.Spawn("worker_a", stove + V(-1.4f, 0, -0.4f), 70, Role.Worker, "Bekçi");
                    rt.Idler(w, 70, "talk", "idle_arms", "coffee");
                    var g = rt.Spawn("guard_b", stove + V(1.5f, 0, -0.7f), 300, Role.Guard, "Rico'nun Adamı");
                    rt.Idler(g, 300, "coffee", "idle", "talk");
                    var tw = rt.Spawn("guard_a", rt.M("tower_top"), rt.MY("tower_top"), Role.Guard, "Kule Gözcüsü");
                    // the lookout turns between the yard (towards the player) and the fire barrel: bodies there will be seen
                    tw.SetPlan(Act.IdleFacing("flashlight", 70, 8), Act.IdleFacing("idle_look", 200, 7), Act.IdleFacing("flashlight", 110, 7),
                               Act.IdleFacing("idle_look", 245, 6), Act.Loop(0));
                    tw.SightRange = 60f;
                    var p = rt.Spawn("guard_a", rt.P("wh_patrol_b")[0], 0, Role.Guard, "Devriye");
                    rt.Patrol(p, rt.P("wh_patrol_b"), "walk", 0.8f, "idle_look", 3f);
                    var p2 = rt.Spawn("guard_b", rt.P("wh_patrol_a")[0], 0, Role.Guard, "Devriye");
                    rt.Patrol(p2, rt.P("wh_patrol_a"), "walk", 0.8f);
                    rt.Searchlight(rt.M("tower_top") + V(-0.8f, 1.1f, 1.2f), 200f, 60f);
                    rt.MakeExplosive("fuel_drum", 6.5f, new Color(0.85f, 0.14f, 0.08f));
                }
            }.Target("rico", "Rico Vargas", "Paralı Asker", "Kel ve sakallı. Taktik yelek, kırmızı tişört.")
             .Goal("Rico Vargas'ı etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.NoAlarm, "Alarm verdirme")
             .Star(ChallengeKind.NoCivilians, "Bekçiye zarar verme"));

            // ------------------------------------------------------------------ 5. warehouse mole (decoy worker)
            list.Add(new MissionDef
            {
                id = "m5", number = 5, title = "DEPODAKİ KÖSTEBEK", location = "A deposu yükleme alanı",
                preset = "day", wind = V(2.6f, 0, 1.8f), reward = 3400, focus = "wh_a_door1", icon = "user_search",
                clock = "14:10", introLine = "Köstebek kasa taşıyanlardan biri. Masumları vurma: önce tara.",
                brief = "Depoda çalışan Kerim, örgütün sevkiyat bilgilerini rakiplere satıyor. Diğer işçilerle birlikte kasa taşıyor.\n\nİşçiler masum: hedefi kıyafetinden tanı ve ateş etmeden önce mutlaka tara.",
                tip = "Hareket eden hedefi vururken kurşunun uçuş süresini hesaba kat: yürüdüğü yönün biraz önüne nişan al.",
                setup = rt =>
                {
                    var route = rt.P("carry_route");
                    var back = MissionRuntime.Rev(route);
                    var t = rt.AddTarget("worker_b", route[0], 270f);
                    t.SetPlan(Act.Move(route, "walk_carry"), Act.IdleFacing("idle", 225, 3), Act.Move(back, "walk"), Act.IdleFacing("idle_wait", 90, 4), Act.Loop(0));
                    t.FleePath = new[] { rt.M("wh_a_door1") };
                    t.HideOnFlee = true;
                    t.SecondChance = true;
                    // the second carrier uses a parallel lane so the two never walk through each other
                    var droute = System.Array.ConvertAll(route, q => q + V(-1.3f, 0, 1.3f));
                    var dback = MissionRuntime.Rev(droute);
                    var d = rt.Spawn("worker_a", droute[droute.Length - 1], 90, Role.Worker, "Depo İşçisi");
                    d.SetPlan(Act.IdleFacing("idle", 90, 6), Act.Move(dback, "walk"), Act.IdleFacing("idle_wait", 90, 3), Act.Move(droute, "walk_carry"), Act.Loop(0));
                    var w = rt.Spawn("worker_c", rt.M("wh_a_door3") + V(-1.5f, 0, 0), 270, Role.Worker, "Temizlikçi");
                    rt.Idler(w, 270, "sweep", "idle", "sweep");
                    var w2 = rt.Spawn("civilian_m", V(30, 0, 44), 90, Role.Civilian, "Kamyon Şoförü");
                    rt.Idler(w2, 90, "phone_talk", "idle_wait", "smoke");
                    var g = rt.Spawn("guard_b", rt.M("wh_a_door2") + V(-1.2f, 0, 0), 270, Role.Guard, "Depo Güvenliği");
                    rt.Idler(g, 270, "guard_pose", "idle_look");
                    var p = rt.Spawn("guard_a", rt.P("wh_patrol_a")[0], 0, Role.Guard, "Devriye");
                    rt.Patrol(p, rt.P("wh_patrol_a"), "walk", 0.8f, "idle_look", 3f);
                }
            }.Target("worker_b", "Kerim Usta", "Köstebek", "Kırmızı kazak, siyah pantolon, bere. Kasa taşıyor.")
             .Goal("Köstebeği bul ve etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.MaxShots, "Tek atış", 1)
             .Star(ChallengeKind.NoCivilians, "Sivillere zarar verme"));

            // ------------------------------------------------------------------ 6. quay runner (moving target, speedboat)
            list.Add(new MissionDef
            {
                id = "m6", number = 6, title = "RIHTIM KAÇAĞI", location = "Rıhtım ve iskele",
                preset = "sunset", wind = V(-3.4f, 0, 0.6f), reward = 3800, focus = "runner_meet", icon = "run",
                clock = "19:20", introLine = "Tilki belgeleri teslim edip tekneye koşacak. Tekneyi vurursan yaya kalır.",
                brief = "Tilki lakaplı Deniz Kara, çalınan belgeleri rıhtımda bir adama teslim edecek. Ardından iskeledeki sürat teknesiyle kaçacak.\n\nTekneye binerse onu kaybederiz. Hareketli hedefe önden nişan al.",
                tip = "300 metrede kurşun yaklaşık 0,4 saniye uçar. Koşan hedefte nişanı yaklaşık bir gövde önüne al. İskeledeki sürat teknesini önceden vurursan motoru çalışmaz: tekneyle kaçamaz, iskeleden koşmak zorunda kalır.",
                setup = rt =>
                {
                    var esc = rt.P("runner_escape");
                    var t = rt.AddTarget("runner", rt.M("runner_start"), 90f);
                    t.SetPlan(Act.IdleFacing("talk", 90, 12), Act.IdleFacing("pay", 90, 6.5f), Act.IdleFacing("phone_talk", 100, 6),
                              Act.Do(() => rt.Say("Hedef tekneye doğru yürüyor!", Theme.Danger, "run")),
                              Act.Move(esc, "walk", 1.15f), Act.Escape());
                    t.FleePath = esc;
                    bool boatDead = false, onFoot = false;
                    // boat out of order: back onto the pier, along the quay towards the sheds (open ground, seen from the roof)
                    var pierRun = new[] { V(140.5f, -1.03f, 300), V(145.4f, 0, 300), V(148, 0, 297.5f), V(148, 0, 262.5f), V(120, 0, 256),
                                          V(75, 0, 252), rt.M("exit_5") };
                    t.OnArrivedEscape += n =>
                    {
                        if (onFoot) return;                              // got away on foot
                        if (boatDead)
                        {
                            onFoot = true;
                            n.Reappear(true);
                            n.FleePath = new[] { pierRun[pierRun.Length - 1] };
                            rt.Say("Tekne çalışmıyor — hedef iskeleden kaçıyor!", Theme.Danger, "run");
                            n.SetPlan(Act.IdleFacing("idle_look", 90, 1.3f), Act.Move(pierRun, "run_panic", 0.92f), Act.Escape());
                            return;
                        }
                        n.SetHidden(false);
                        n.Play("idle", 0.2f);
                        rt.AttachToProp(n, "speedboat");
                        rt.MoveProp("speedboat", rt.P("speedboat_out"), 9f, false);
                    };
                    // a bullet in the boat puts the engine out of order (only before he is aboard)
                    rt.MakeShootable("speedboat", "boat", (s, p) =>
                    {
                        if (s.Disabled || t.Escaped || onFoot) return;
                        s.Disabled = true;
                        boatDead = true;
                        rt.SetFlag("boat");
                        FX.Smoke(p);
                        rt.Say("Tekne vuruldu — motor çalışmıyor, tekneyle kaçamaz!", Theme.Success, "boat");
                    });
                    var c = rt.Spawn("smuggler", rt.M("runner_meet"), 270, Role.Bodyguard, "Bağlantı", "SUÇLU");
                    c.SetPlan(Act.IdleFacing("talk2", 270, 12), Act.IdleFacing("handshake", 270, 6), Act.IdleFacing("idle", 270, 4),
                              Act.Move(new[] { rt.M("runner_meet"), V(-60, 0, 244), V(-120, 0, 246) }, "walk"), Act.IdleFacing("phone", 180, 0));
                    var w1 = rt.Spawn("worker_a", rt.M("quay_worker1"), 0, Role.Worker, "Halatçı");
                    rt.Idler(w1, 0, "coil_rope", "idle");
                    var w2 = rt.Spawn("worker_c", rt.M("quay_worker2"), 90, Role.Worker, "Rıhtım İşçisi");
                    rt.Idler(w2, 90, "sweep", "idle_wait");
                    var w3 = rt.Spawn("worker_b", rt.M("quay_signal"), 0, Role.Worker, "İşaretçi");
                    rt.Idler(w3, 0, "signal", "idle");
                    var g = rt.Spawn("guard_a", rt.M("pier_head"), 180, Role.Guard, "İskele Bekçisi");
                    rt.Idler(g, 180, "idle_look", "guard_pose");
                    var cv = rt.Spawn("civilian_m", rt.P("quay_walk")[1], 270, Role.Civilian, "Balıkçı");
                    rt.Walker(cv, MissionRuntime.Rev(rt.P("quay_walk")), "walk", 1f, 5f);
                }
            }.Target("runner", "Deniz Kara", "Tilki", "Kahverengi kapüşonlu, kasket, koyu pantolon.")
             .Goal("Tilki'yi tekneyle kaçmadan durdur")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.Custom, "Önce teknesini vur", 0, r => r.Flag("boat"))
             .Star(ChallengeKind.AllHeadshots, "Hedefi kafadan vur"));

            // ------------------------------------------------------------------ 7. ship captain (long range, strong wind)
            list.Add(new MissionDef
            {
                id = "m7", number = 7, title = "KAPTAN KÖŞKÜ", location = "Meridian Star köprüüstü",
                preset = "overcast", wind = V(4.6f, 0, -1.2f), reward = 4300, focus = "ship_bridge", icon = "boat",
                clock = "16:45", introLine = "Kaptan köprüüstünün kanadına çıkacak. Uzun atış, sert rüzgar — rüzgar okuna bak.",
                brief = "Meridian Star'ın kaptanı Orhan Demir, örgütün silah yükünü teslim etmeden limandan ayrılmak üzere. Köprüüstünün açık kanadına çıkıyor.\n\nMesafe uzun ve rüzgar sert. Kaçırırsan köşke kapanır.",
                tip = "Uzun mesafede kurşun düşer. Nişan yardımı açıksa dürbündeki kırmızı nokta kurşunun gideceği yeri gösterir.",
                setup = rt =>
                {
                    var b1 = rt.M("ship_bridge"); var b2 = rt.M("ship_bridge2");
                    var t = rt.AddTarget("civilian_m", b1, 180f);
                    t.SetPlan(Act.IdleFacing("smoke", 180, 13), Act.IdleFacing("phone_talk", 165, 10), Act.Move(new[] { b1, b2 }, "walk_slow"),
                              Act.IdleFacing("idle_look", 150, 8), Act.Move(new[] { b2, b1 }, "walk_slow"), Act.Loop(0));
                    t.FleePath = new[] { b1 + V(-1.5f, 0, 2.2f) };
                    t.HideOnFlee = true;
                    t.SecondChance = true;
                    var o = rt.Spawn("worker_c", b2 + V(0.8f, 0, 0.8f), 200, Role.Worker, "Güverte Zabiti");
                    rt.Idler(o, 200, "talk", "idle", "idle_wait");
                    var w = rt.Spawn("worker_a", rt.M("ship_gangway"), 0, Role.Worker, "Tayfa");
                    rt.Idler(w, 0, "idle_wait", "coil_rope");
                    var g = rt.Spawn("guard_b", V(-92, 0, 255), 30, Role.Guard, "Gemi Güvenliği");
                    rt.Idler(g, 30, "idle_look", "guard_pose");
                    var c = rt.Spawn("worker_b", rt.P("quay_walk")[0], 90, Role.Worker, "Rıhtım İşçisi");
                    rt.Walker(c, rt.P("quay_walk"), "walk", 1f, 4f);
                    var s = rt.Spawn("worker_c", rt.M("quay_signal"), 0, Role.Worker, "İşaretçi");
                    rt.Idler(s, 0, "signal", "idle");
                    rt.MoveProp("tug", new[] { V(-250, -4.2f, 300), V(-160, -4.2f, 320), V(-20, -4.2f, 330), V(120, -4.2f, 360) }, 2.4f, false);
                }
            }.Target("civilian_m", "Orhan Demir", "Kaptan", "Yeşil mont, kot pantolon. Köprüüstü kanadında.")
             .Goal("Kaptan Orhan Demir'i köşke kapanmadan vur")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.MaxShots, "Tek atış", 1)
             .Star(ChallengeKind.AllHeadshots, "Hedefi kafadan vur"));

            // ------------------------------------------------------------------ 8. yacht party (decoy, bodyguards, leaving boss)
            list.Add(new MissionDef
            {
                id = "m8", number = 8, title = "YAT PARTİSİ", location = "İskeledeki yat",
                preset = "night", wind = V(2.0f, 0, 3.0f), reward = 6000, focus = "party_0", icon = "boss",
                clock = "00:40", introLine = "Kral'ın partisi. Beyaz smokinli iki adam var — bıyıklı ve gözlüklü olan bizim adam.",
                brief = "Örgütün başı Viktor Kral yatında kutlama yapıyor. Korumaları her yerde.\n\nKonuklardan biri de beyaz smokin giymiş: taramadan ateş etme. Kral bir süre sonra iskeleden ayrılıp depolara yürüyecek.",
                tip = "Gerçek hedef bıyıklı ve güneş gözlüklü. Emin değilsen nişangahı üzerinde tut ve kimlik kartını oku.",
                setup = rt =>
                {
                    rt.WanderIdlers = false;                          // a crowded yacht deck
                    var leave = rt.P("boss_leave");
                    var t = rt.AddTarget("boss", rt.M("party_0"), 30f);
                    t.SetPlan(Act.IdleFacing("drink", 30, 13), Act.IdleFacing("talk", 60, 11), Act.IdleFacing("phone_talk", 20, 8),
                              Act.IdleFacing("talk2", 50, 10), Act.IdleFacing("drink", 30, 9),
                              Act.Do(() => rt.Say("Viktor Kral yattan ayrılıyor!", Theme.Danger, "boss")),
                              Act.Move(leave, "walk_strut"), Act.Escape());
                    t.FleePath = leave;
                    rt.Countdown("Kral'ın ayrılmasına", 51f);
                    var d = rt.Spawn("boss_decoy", rt.M("party_3"), rt.MY("party_3"), Role.Decoy, "Konuk", "SİVİL", "Beyaz smokin — bıyıksız, gözlüksüz.");
                    rt.Idler(d, rt.MY("party_3"), "talk", "drink", "talk2");
                    var g1 = rt.Spawn("guest_f_a", rt.M("party_1"), rt.MY("party_1"), Role.Civilian, "Konuk");
                    rt.Idler(g1, rt.MY("party_1"), "drink", "talk", "idle");
                    var g2 = rt.Spawn("guest_f_b", rt.M("party_2"), rt.MY("party_2"), Role.Civilian, "Konuk");
                    rt.Idler(g2, rt.MY("party_2"), "talk", "drink");
                    var g3 = rt.Spawn("guest_f_c", rt.M("party_4"), rt.MY("party_4"), Role.Civilian, "Konuk");
                    rt.Idler(g3, rt.MY("party_4"), "drink", "idle2", "talk");
                    var g4 = rt.Spawn("businessman", rt.M("party_5"), rt.MY("party_5"), Role.Civilian, "İş İnsanı");
                    rt.Idler(g4, rt.MY("party_5"), "talk2", "drink", "phone");
                    var bg1 = rt.Spawn("bodyguard_a", rt.M("yacht_aft"), rt.MY("yacht_aft"), Role.Bodyguard, "Koruma");
                    rt.Idler(bg1, rt.MY("yacht_aft"), "guard_pose", "idle_look");
                    var bg2 = rt.Spawn("bodyguard_b", rt.M("yacht_gangway"), rt.MY("yacht_gangway"), Role.Bodyguard, "Koruma");
                    rt.Idler(bg2, rt.MY("yacht_gangway"), "idle_look", "guard_pose");
                    var fl = rt.Spawn("guard_a", rt.M("yacht_mid"), rt.MY("yacht_mid"), Role.Guard, "Gözcü");
                    rt.Idler(fl, rt.MY("yacht_mid"), "flashlight", "idle_look");
                    var ph = rt.Spawn("guard_b", rt.M("pier_head"), 180, Role.Guard, "İskele Bekçisi");
                    rt.Idler(ph, 180, "idle_look", "guard_pose");
                }
            }.Target("boss", "Viktor Kral", "Örgüt Lideri", "Beyaz smokin, papyon. Bıyıklı ve güneş gözlüklü.")
             .Goal("Viktor Kral'ı yattan ayrılmadan etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.NoCivilians, "Konuklara zarar verme")
             .Star(ChallengeKind.NoGuards, "Korumalara dokunma"));

            // ------------------------------------------------------------------ 9. the witness (protect, hitmen hidden in the crowd)
            list.Add(new MissionDef
            {
                id = "m9", number = 9, title = "TANIK", location = "Giriş kapısı ve yönetim binası",
                preset = "day", wind = V(1.6f, 0, -0.6f), reward = 6500, focus = "gate_lane", icon = "shield",
                kind = "KORUMA", clock = "07:10",
                introLine = "Ayten Şahin bugün örgüte karşı ifade verecek. Polis onu ofise götürüyor. Kalabalıkta iki tetikçi var.",
                brief = "Gümrük memuru Ayten Şahin, örgütün rüşvet ağına karşı tanıklık edecek. Polis onu sabah liman yönetim binasına getiriyor.\n\nÖrgüt iki tetikçi yolladı: işçi kılığında kalabalığa karışmışlar. Herkesi dürbünle tara — silahlılar kimlik kartında görünür. Ayten binaya girene kadar hayatta kalmalı.\n\nÖrgüt yalnız gelmedi: gözetleme kulesinde dürbünlü bir gözcü, kapının iki yanında silahlı adamları var. Yerini bulurlarsa sana ateş ederler.",
                tip = "Tetikçi silah çekince işaretçisinin etrafındaki kırmızı halka dolmadan vur. Kolundan vurursan silahını düşürür. Kuledeki gözcünün dürbünü sana dönünce eğil (SİPER) ya da önce onu indir.",
                gearHint = "Termal dürbün kalabalıkta işe yarar",
                maxCivilians = 1,
                setup = rt =>
                {
                    var walk = rt.P("witness_walk");
                    float y0 = MissionRuntime.Yaw(walk[0], walk[1]);
                    var w = rt.AddProtected("civilian_f", walk[0], y0, "Ayten Şahin", "TANIK", "Bej trençkot — polis eşliğinde");
                    var cop = rt.Spawn("guard_b", walk[0] + V(0.9f, 0, 0.4f), y0, Role.Worker, "Polis Memuru", "POLİS", "Tanığın koruması — ateş etme");
                    w.SetHidden(true);
                    cop.SetHidden(true);
                    w.FleeAlong = walk;                                  // panic: run the rest of the way to the door
                    w.HideOnFlee = true;
                    w.CanFlee = true;
                    NPC hA = null, hB = null;
                    System.Func<NPC, bool> stopped = h => h == null || h.Dead || h.Sedated || h.Down || (h.Wounded && h.WoundZone == BodyZone.Arm);
                    // safe at the office door — or out of the car with both killers stopped (nothing left to fear on the way)
                    System.Func<NPC, bool> gone = h => stopped(h) || h.Escaped || h.Panicking;
                    var line = rt.Protect(w, "Tanığı ofise kadar koru", () => !w.IsHidden && !w.Incapacitated && hA != null && hB != null && gone(hA) && gone(hB));
                    line.walksToSafety = true;                           // hurt or asleep on the way: lost all the same

                    hA = rt.AddHitman("worker_c", rt.M("hit_a"), rt.MY("hit_a"), "Bilinmeyen İşçi", "Temizlikçi kılığında — silahlı");
                    rt.Idler(hA, rt.MY("hit_a"), "sweep", "idle_wait", "sweep");
                    hB = rt.AddHitman("worker_a", rt.M("hit_b"), rt.MY("hit_b"), "Bilinmeyen İşçi", "İşçi kılığında — silahlı");
                    rt.Idler(hB, rt.MY("hit_b"), "smoke", "idle_wait", "phone");
                    var bonus = rt.Custom("Tetikçileri durdur", () => stopped(hA) && stopped(hB), null, null, true);
                    bonus.liveText = () => "Tetikçileri durdur  " + ((stopped(hA) ? 1 : 0) + (stopped(hB) ? 1 : 0)) + "/2";
                    rt.When(() => stopped(hA) && stopped(hB), () => rt.SetFlag("both"));

                    // the organisation's back-up: a lookout on the watchtower sweeping the rooftops with binoculars, and two
                    // armed men on either side of the gate — they turn on the nest once they know where the shots come from
                    var tw = rt.M("tower_top");
                    var look = rt.Spawn("guard_a", tw, MissionRuntime.Yaw(tw, rt.NestPoint), Role.Guard, "Örgüt Gözcüsü", "GÖZCÜ",
                                        "Dürbünle çatıları tarıyor — seni görürse yerini telsizle bildirir");
                    Spotter.Make(look, MissionRuntime.Yaw(tw, rt.NestPoint), 62f, 9f);
                    var o1 = rt.Spawn("guard_b", V(31.5f, 0, 11.6f), 215, Role.Guard, "Örgüt Adamı", "SİLAHLI", "Örgütün silahlı adamı — tetikçileri koruyor");
                    o1.NoRadio = true;
                    // a rifle on his shoulder (the pistol he would carry by default stays out of sight)
                    var o1gun = GunHold.Swap(o1, GunKind.Rifle);
                    if (o1gun != null) Gunner.Attach(o1, o1gun);
                    rt.Idler(o1, 215, "smoke", "phone", "idle_wait");
                    var o2 = rt.Spawn("guard_a", V(-29.5f, 0, 17.5f), 160, Role.Guard, "Örgüt Adamı", "SİLAHLI", "Örgütün silahlı adamı — tetikçileri koruyor");
                    o2.NoRadio = true;
                    rt.Idler(o2, 160, "idle_look", "phone_talk", "idle_arms");
                    rt.At(8f, () =>
                    {
                        if (look.Incapacitated || look.Lookout == null || look.Lookout.Reporting || look.Lookout.Reported) return;
                        rt.Radio("KONTROL", "Kulede dürbünlü biri var. Dürbünü sana döndüğünde eğil — seni görürse telsizle herkese bildirir.", 5f);
                    });

                    // innocent look-alikes: the same overalls, no guns
                    var w1 = rt.Spawn("worker_a", rt.M("m9_worker1"), rt.MY("m9_worker1"), Role.Worker, "Liman İşçisi");
                    rt.Idler(w1, rt.MY("m9_worker1"), "phone_talk", "idle_wait", "smoke");
                    var w2 = rt.Spawn("worker_c", rt.M("m9_worker2"), rt.MY("m9_worker2"), Role.Worker, "Temizlik Görevlisi");
                    rt.Idler(w2, rt.MY("m9_worker2"), "sweep", "idle", "sweep");
                    var w3 = rt.Spawn("worker_b", rt.P("m9_walk")[0], 270, Role.Worker, "Forklift Ustası");
                    rt.Walker(w3, rt.P("m9_walk"), "walk", 0.9f, 5f);
                    var gk = rt.Spawn("guard_a", rt.M("gate_booth"), 180, Role.Worker, "Kapı Görevlisi", "GÖREVLİ", "Silahsız kapı görevlisi");
                    rt.Idler(gk, 180, "idle_look", "phone", "idle_arms");
                    var c1 = rt.Spawn("civilian_m", V(-120, 0.12f, -28.4f), 90, Role.Civilian, "Yaya");
                    rt.Walker(c1, new[] { V(-120, 0.12f, -28.4f), V(110, 0.12f, -28.4f) }, "walk", 1.2f, 3f);

                    // the police car comes along the road (out of sight below the roof), she walks in through the gate
                    // (V3 device test: waiting for the car) from half way along the road, ~7 s to the gate
                    var policeIn = rt.P("police_in");
                    if (policeIn.Length >= 2) policeIn[0] = Vector3.Lerp(policeIn[0], policeIn[1], 0.45f);
                    var mover = rt.MoveProp("police_car", policeIn, 14f, false);
                    var car = Level.I.Tagged("police_car");
                    if (car != null)
                    {
                        w.transform.SetPositionAndRotation(car.transform.TransformPoint(new Vector3(0.45f, 0.35f, 0.2f)), car.transform.rotation);
                        cop.transform.SetPositionAndRotation(car.transform.TransformPoint(new Vector3(-0.45f, 0.35f, 0.2f)), car.transform.rotation);
                        rt.AttachToProp(w, "police_car");
                        rt.AttachToProp(cop, "police_car");
                        w.MarkWhenHidden = true;
                        w.HiddenStatus = "Polis arabasında — yolda";
                    }
                    System.Action arrive = () =>
                    {
                        if (w == null || w.Dead) return;
                        rt.DetachFromProp(w);
                        rt.DetachFromProp(cop);
                        w.transform.SetPositionAndRotation(walk[0], Quaternion.Euler(0, y0, 0));
                        cop.transform.SetPositionAndRotation(walk[0] + V(0.9f, 0, 0.5f), Quaternion.Euler(0, y0, 0));
                        w.MarkWhenHidden = false;
                        w.HiddenStatus = null;
                        w.SetHidden(false);
                        cop.SetHidden(false);
                        w.SetPlan(Act.Idle("idle_look", 1.2f), Act.Move(walk, "walk", 0.9f), Act.Escape());
                        cop.SetPlan(Act.Idle("idle_look", 1.2f), Act.Move(MissionRuntime.Offset(walk, 0.9f), "walk", 0.9f), Act.Idle("guard_pose", 0));
                        rt.Radio("KONTROL", "Tanık arabadan indi, kapıya yürüyor. Gözünü dört aç.", 4f);
                    };
                    if (mover != null && mover.Running) mover.Arrived = arrive;
                    else arrive();

                    // the hitmen strike when she comes close
                    rt.When(() => !w.IsHidden && !w.Dead && w.transform.position.z > 4f && w.transform.position.x > 0f, () =>
                    {
                        if (!stopped(hB) && !hB.Panicking && !hB.Escaped) hB.Attack(w, new[] { rt.M("hit_b_fire") }, "walk", 5f, rt.AttackerFires);
                    });
                    // a gun comes out: the officer draws his own and covers it, she crouches behind him; the threat gone,
                    // she gets up and walks on (he keeps his pistol out) — for each of the two
                    var copGun = GunHold.Give(cop, GunKind.Pistol);
                    cop.Armed = false;                                 // his card says POLİS, not SİLAHLI
                    cop.MarkAlerted();                                 // on duty: gunfire does not send him running
                    NPC covering = null;
                    System.Action<NPC> coverFrom = h =>
                    {
                        covering = h;
                        float cy = cop.YawTo(h.transform.position);
                        if (!cop.Incapacitated && !cop.Panicking)
                        {
                            if (copGun != null)
                            {
                                copGun.Drawn = true;
                                copGun.Aiming = true;
                                copGun.Aim = () => h != null && !h.Dead ? h.ChestPoint : cop.transform.position + cop.transform.forward * 10f;
                            }
                            cop.EndInvestigation();
                            cop.SetPlan(Act.IdleFacing("pull_gun", cy, 0.6f), Act.IdleFacing(copGun != null ? copGun.AimClip(false) : "guard_pose", cy, 0f));
                            rt.Radio("POLİS", "Silah! Arkamda kal, eğil!", 2.5f);
                        }
                        if (!w.IsHidden && !w.Panicking && !w.Escaped && !w.Incapacitated)
                        {
                            w.EndInvestigation();
                            w.SetPlan(Act.IdleFacing("duck", w.YawTo(h.transform.position), 0.8f), Act.Idle("cower", 0f));
                        }
                    };
                    System.Action walkOn = () =>
                    {
                        covering = null;
                        if (copGun != null) copGun.Aiming = false;
                        if (w.Dead || w.Escaped || w.Panicking || w.IsHidden || w.Incapacitated) return;
                        var rest = MissionRuntime.From(walk, w.transform.position);
                        w.SetPlan(Act.Idle("idle_look", 1.2f), Act.Move(rest, "walk", 1.05f), Act.Escape());
                        if (!cop.Incapacitated && !cop.Panicking)
                            cop.SetPlan(Act.Idle("idle_look", 1.2f), Act.Move(MissionRuntime.From(MissionRuntime.Offset(walk, 0.9f), cop.transform.position), "walk", 1.05f), Act.Idle("guard_pose", 0));
                    };
                    System.Action watchGuns = null;
                    watchGuns = () =>
                    {
                        if (!rt.IsPlaying || w.Dead || w.Escaped) return;
                        var h = hA.Aiming ? hA : (hB.Aiming ? hB : null);
                        if (h != null && covering != h) coverFrom(h);
                        else if (h == null && covering != null) walkOn();
                        rt.After(0.2f, watchGuns);
                    };
                    rt.After(0.2f, watchGuns);
                    rt.When(() => !w.IsHidden && !w.Dead && w.transform.position.x < -12f, () =>
                    {
                        if (!stopped(hA) && !hA.Panicking && !hA.Escaped) hA.Attack(w, new[] { rt.M("hit_a_fire") }, "walk", 5.5f, rt.AttackerFires);
                    });
                    rt.RadioAt(14f, "KONTROL", "Herkesi tara. Kartında SİLAHLI yazan adam bizim adamımız değil.", 4.5f);
                }
            }.Target("civilian_f", "Ayten Şahin", "Tanık", "Bej trençkot, kahverengi saç. Polis memuruyla yürüyor.")
             .Goal("Tanığı ofise girene kadar hayatta tut")
             .Goal("Kalabalıktaki iki tetikçiyi bul ve durdur (bonus)")
             .Star(ChallengeKind.Complete, "Tanığı koru")
             .Star(ChallengeKind.Custom, "İki tetikçiyi de silah çekmeden durdur", 0, r => r.Flag("both") && !r.Flag("aimed"))
             .Star(ChallengeKind.NoCivilians, "Sivillere zarar verme"));

            // ------------------------------------------------------------------ 10. the courier (wound in the leg, keep him alive)
            list.Add(new MissionDef
            {
                id = "m10", number = 10, title = "KURYE", location = "A deposu yükleme alanı",
                preset = "sunset", wind = V(-2.4f, 0, 1.4f), reward = 7000, focus = "wh_a_door2", icon = "hand",
                kind = "CANLI YAKALA", clock = "18:40",
                introLine = "Kurye çantayla depodan çıkacak. Müşteri onu canlı istiyor: bacağından vur, gerisini ekip halleder.",
                brief = "Emre Doğan, örgütün banka hesaplarını taşıyan kurye. Birazdan A deposundan çıkıp arabasına yürüyecek.\n\nMüşteri onu CANLI istiyor: bacağından vur, yere düşsün. Ekibimiz arabayla gelip onu alacak. Öldürürsen bilgi kaybolur. Dikkat: örgüt, kuryesinin konuşmasındansa ölmesini tercih eder — korumaları ona silah çekecek, depodaki adamları da dışarı çıkıp onu susturmaya çalışacak.",
                tip = "Bacağından vurulan yere düşer. Korumalar onu susturmak için silah çekerse kırmızı halka dolmadan indir — kolundan vurursan silahını düşürür. Iskalarsan kurye arabasına koşar.",
                setup = rt =>
                {
                    var walk = rt.P("courier_walk");
                    var door = rt.M("courier_door");
                    var c = rt.AddTarget("guest_m", walk[0], 270f);
                    c.SetHidden(true);                                   // still inside the warehouse
                    c.NoCrawl = true;
                    c.FleePath = new[] { walk[walk.Length - 1], door };
                    c.FleeEndPause = 0.8f;
                    c.FleeEndYaw = 270f;
                    c.EscapeText = "Kurye arabasıyla kaçtı";
                    c.EscapeFailDelay = 3.6f;
                    var wound = rt.MustWound(c, BodyZone.Leg, "Kuryeyi BACAĞINDAN vur — ölmemeli");
                    bool teamHere = false;
                    var keep = rt.Protect(c, "Ekip gelene kadar kuryeyi hayatta tut", () => teamHere);
                    keep.hidden = true;

                    var g1 = rt.Spawn("bodyguard_a", walk[0] + V(0, 0, 1.0f), 270, Role.Bodyguard, "Koruma");
                    g1.SetHidden(true);
                    var g2 = rt.Spawn("bodyguard_b", rt.M("courier_guard2"), rt.MY("courier_guard2"), Role.Bodyguard, "Şoför");
                    rt.Idler(g2, rt.MY("courier_guard2"), "smoke", "phone_talk", "idle_wait");
                    var w1 = rt.Spawn("worker_c", V(60.5f, 0, 90.5f), 270, Role.Worker, "Depo İşçisi");
                    rt.Idler(w1, 270, "sweep", "idle", "sweep");
                    var w2 = rt.Spawn("worker_a", rt.P("wh_patrol_a")[0], 0, Role.Worker, "Forklift Ustası");
                    rt.Patrol(w2, rt.P("wh_patrol_a"), "walk", 0.85f, "idle_look", 2f);

                    // he gets in: the driver takes him away
                    c.OnArrivedEscape += n =>
                    {
                        if (g2 != null && !g2.Dead && !g2.Incapacitated) g2.SetHidden(true);
                        rt.MoveProp("courier_car", rt.P("courier_out"), 10f, false);
                    };
                    c.SetPlan(Act.Idle("idle", 6f),
                              Act.Do(() => { c.SetHidden(false); g1.SetHidden(false); rt.Radio("KONTROL", "Kurye dışarıda. Bacağına nişan al.", 3.5f); }),
                              Act.Move(walk, "walk_confident"), Act.Move(new[] { door }, "walk"), Act.IdleFacing("idle_look", 270f, 0.8f), Act.Escape());
                    g1.SetPlan(Act.Idle("idle", 6f), Act.Move(MissionRuntime.Offset(walk, -1.1f), "walk_confident"), Act.IdleFacing("guard_pose", 300f, 0));

                    // the moment he is down: the organisation wants him silenced, our team races in
                    bool started = false;
                    System.Func<NPC, bool> able = g => g != null && !g.Dead && !g.Incapacitated && !g.Escaped && !(g.Wounded && g.WoundZone == BodyZone.Arm);
                    // more of the organisation's men in the warehouse: they come out when he goes down
                    var crew = new List<NPC>();
                    foreach (var (whDoor, ch, nm) in new[] { ("wh_a_door1", "guard_a", "Depodaki Adam"), ("wh_a_door3", "guard_b", "Depodaki Adam"), ("wh_a_door2", "guard_a", "Depodaki Adam") })
                    {
                        var r = rt.Spawn(ch, rt.M(whDoor) + V(-1.2f, 0, 0), 270, Role.Guard, nm, "SİLAHLI", "Örgütün adamı — kuryeyi susturmak istiyor");
                        r.NoRadio = true;
                        r.SetHidden(true);
                        crew.Add(r);
                    }
                    int pendingWaves = 0;
                    // he runs out to where he can see the courier and shoots him (again and again if the player drives him off)
                    System.Action<NPC> rush = null;
                    rush = r =>
                    {
                        if (teamHere || c == null || c.Dead || !rt.IsPlaying || !able(r) || r.Aiming) return;
                        var body = c.transform.position;
                        var from = r.transform.position;
                        var d = from - body; d.y = 0f;
                        var stand = body + (d.sqrMagnitude > 1f ? d.normalized : Vector3.right) * 8f;
                        r.EndInvestigation();
                        if ((stand - from).sqrMagnitude <= 4f) { r.BeginAim(c, 4.6f, rt.AttackerFires); return; }
                        if (Level.WalkableLine(from, stand, out float sy)) { r.Attack(c, new[] { new Vector3(stand.x, sy, stand.z) }, "run", 4.6f, rt.AttackerFires); return; }
                        // no straight way: round by the warehouse door and along the courier's own walk, 8 m short of him
                        if (Level.WalkableLine(from, walk[0], out _))
                        {
                            var way = new List<Vector3> { walk[0] };
                            way.AddRange(Slice(walk, 0f, Mathf.Max(0f, AlongPath(walk, body) - 8f)));
                            r.Attack(c, way.ToArray(), "run", 4.6f, rt.AttackerFires);
                            return;
                        }
                        // close enough for a pistol from where he is; otherwise he tries again in a few seconds
                        if (d.magnitude < 28f) r.BeginAim(c, 4.6f, rt.AttackerFires);
                    };
                    System.Action<NPC> wave = r =>
                    {
                        pendingWaves--;
                        if (teamHere || c == null || c.Dead || !rt.IsPlaying || r.Dead) return;
                        r.SetHidden(false);
                        rt.Say("Depodan silahlı biri çıktı — kuryeye gidiyor!", Theme.Danger, "alert");
                        rush(r);
                        // driven off (cover, a near miss) but still able: he tries again a few seconds later
                        System.Action again = null;
                        again = () =>
                        {
                            if (teamHere || c.Dead || !rt.IsPlaying || !able(r)) return;
                            if (!r.Aiming) rush(r);
                            rt.After(UnityEngine.Random.Range(5f, 7f), again);
                        };
                        rt.After(6f, again);
                    };
                    System.Action nextShooter = null;
                    nextShooter = () =>
                    {
                        if (teamHere || c == null || c.Dead || !rt.IsPlaying) return;
                        var s = able(g1) ? g1 : (able(g2) ? g2 : null);
                        if (s == null) return;
                        if (g2.IsHidden) g2.SetHidden(false);
                        s.BeginAim(c, s == g1 ? 4.5f : 5f, rt.AttackerFires);
                        rt.When(() => !s.Aiming || c.Dead, () => rt.After(1.6f, nextShooter));
                    };
                    System.Action team = () =>
                    {
                        if (started || c == null || c.Dead) return;
                        started = true;
                        rt.Reveal(keep);
                        rt.After(1.2f, nextShooter);
                        // the warehouse men come out one after the other
                        pendingWaves = crew.Count;
                        for (int i = 0; i < crew.Count; i++) { var r = crew[i]; rt.After(3f + i * 6.5f, () => wave(r)); }
                        var mover = rt.MoveProp("team_car", rt.P("team_in"), 11.5f, false);
                        // nobody left to stop: the team floors it (no waiting with nothing to do)
                        bool hurried = false;
                        rt.When(() => teamHere || c.Dead || (pendingWaves <= 0 && !able(g1) && !able(g2) && !crew.Exists(x => able(x))), () =>
                        {
                            if (teamHere || c.Dead || mover == null || !mover.Running || hurried) return;
                            hurried = true;
                            mover.SpeedScale = 3.2f;
                            rt.Radio("KONTROL", "Yol temiz — ekip gaza bastı, geliyorlar!", 3f);
                        });
                        System.Action pick = () =>
                        {
                            if (c == null || c.Dead || !rt.IsPlaying) return;
                            teamHere = true;
                            rt.ClearCountdown();
                            foreach (var g in new[] { g1, g2 }) if (g != null) g.StopAiming();
                            foreach (var r in crew) if (r != null) r.StopAiming();
                            c.SetHidden(true);
                            rt.Say("Ekip kuryeyi aldı!", Theme.Success, "check");
                        };
                        if (mover != null && mover.Running)
                        {
                            mover.Arrived = () => rt.After(1.5f, pick);
                            rt.Countdown("Ekibin gelmesine", () => teamHere ? 0f : MissionRuntime.EtaOf(mover, 11.5f * mover.SpeedScale, 1.5f));
                        }
                        else
                        {
                            rt.After(25f, pick);
                            rt.Countdown("Ekibin gelmesine", 25f);
                        }
                        int eta = Mathf.RoundToInt(mover != null && mover.Running ? MissionRuntime.EtaOf(mover, 11.5f, 1.5f) : 25f);
                        rt.Radio("KONTROL", "Kurye yerde! Ekip yolda — " + eta + " saniye. Örgütün adamları onu susturmaya çalışacak, depodan da çıkacaklar!", 4.5f);
                    };
                    c.OnWound = (n, z) =>
                    {
                        if (z != BodyZone.Leg || started) return;
                        if (rt.Shots == 1) rt.SetFlag("firstLeg");
                        team();
                    };
                    rt.When(() => c.Sedated, team);
                }
            }.Target("guest_m", "Emre Doğan", "Kurye", "Mavi polo tişört, siyah pantolon. Elinde siyah bir çanta.")
             .Goal("Kuryeyi BACAĞINDAN vur — ölmemeli")
             .Goal("Ekip gelene kadar onu silahlı adamlardan koru")
             .Star(ChallengeKind.Complete, "Kuryeyi canlı teslim et")
             .Star(ChallengeKind.Custom, "İlk atışta bacağından vur", 0, r => r.Flag("firstLeg"))
             .Star(ChallengeKind.Custom, "Kimse kuryeye nişan alamasın", 0, r => !r.Flag("aimed")));

            // ------------------------------------------------------------------ 11. blackout (night vision, power cut)
            list.Add(new MissionDef
            {
                id = "m11", number = 11, title = "KARARTMA", location = "Merkez saha — gece sevkiyatı",
                preset = "night", wind = V(2.2f, 0, 1.2f), reward = 7500, focus = "kartal_spot", icon = "moon",
                kind = "SUİKAST", clock = "02:15",
                introLine = "Kartal sevkiyatı bizzat denetliyor. Işıklar onun lehine — ama elektrik panosu direğin dibinde.",
                brief = "Silah tüccarı Hakan Er, namıdiğer Kartal, gece yarısı limanın merkez sahasında bir sevkiyatı denetliyor. Etrafı aydınlık ve korumalarla dolu.\n\nIşık direğinin dibindeki elektrik panosunu vurursan bütün liman karanlığa gömülür: korumalar kör kalır, ama Kartal da arabasına yürür. Gece görüşü dürbünü bu iş için sende.",
                tip = "Gece görüşünü dürbündeki göz tuşuyla aç. Karanlıkta korumalar yakından geçmedikçe cesetleri göremez. Kartal arabasına binerse kaçar.",
                setup = rt =>
                {
                    var spot = rt.M("kartal_spot");
                    var k = rt.AddTarget("businessman", spot, rt.MY("kartal_spot"));
                    rt.Idler(k, rt.MY("kartal_spot"), "talk", "argue", "phone_talk", "talk_passion");
                    k.FleePath = new[] { rt.M("kartal_door") };
                    k.FleeEndPause = 0.8f;
                    k.FleeEndYaw = 270f;
                    k.EscapeText = "Kartal arabasıyla kaçtı";
                    k.EscapeFailDelay = 3.6f;
                    k.OnArrivedEscape += n => rt.MoveProp("kartal_car", rt.P("kartal_out"), 10f, false);
                    rt.Eliminate(k, "Kartal'ı etkisiz hale getir");
                    var buyer = rt.Spawn("smuggler", rt.M("kartal_buyer"), rt.MY("kartal_buyer"), Role.Bodyguard, "Satıcı", "SİLAHLI");
                    rt.Idler(buyer, rt.MY("kartal_buyer"), "talk2", "idle_arms", "pay");
                    var guards = new List<NPC>();
                    foreach (var m in new[] { "kartal_g1", "kartal_g2", "kartal_g3" })
                    {
                        var g = rt.Spawn(guards.Count % 2 == 0 ? "guard_a" : "guard_b", rt.M(m), rt.MY(m), Role.Guard, "Kartal'ın Adamı");
                        rt.Idler(g, rt.MY(m), "guard_pose", "idle_look", "flashlight");
                        guards.Add(g);
                    }
                    var tw = rt.Spawn("guard_a", rt.M("tower_top"), rt.MY("tower_top"), Role.Guard, "Kule Gözcüsü");
                    tw.SetPlan(Act.IdleFacing("flashlight", 200, 8), Act.IdleFacing("idle_look", 160, 7), Act.IdleFacing("flashlight", 230, 7), Act.Loop(0));
                    tw.SightRange = 60f;
                    guards.Add(tw);
                    rt.Searchlight(rt.M("tower_top") + V(-0.8f, 1.1f, 1.2f), 200f, 70f);
                    var p = rt.Spawn("guard_b", rt.P("yard_patrol_b")[0], 0, Role.Guard, "Devriye");
                    rt.Patrol(p, rt.P("yard_patrol_b"), "walk", 0.8f, "idle_look", 3f);
                    guards.Add(p);

                    // done in 75 s: he leaves anyway
                    System.Action leave = () =>
                    {
                        if (k == null || k.Dead || k.Escaped || k.Panicking || k.Incapacitated) return;
                        k.EndInvestigation();
                        rt.Say("Kartal arabasına yürüyor!", Theme.Danger, "run");
                        k.SetPlan(Act.Move(new[] { rt.M("kartal_door") }, "walk_confident"), Act.IdleFacing("idle_look", 270f, 1.0f), Act.Escape());
                    };
                    rt.Countdown("Sevkiyatın bitmesine", 75f);
                    rt.At(75f, leave);

                    // the power cut
                    var cut = rt.DisableProp("power_box", "power", "Elektrik panosunu vur (karartma)", 1, (s, pt) =>
                    {
                        rt.SetFlag("blackout");
                        Level.I.SetNight(false);
                        foreach (var l in rt.Root.GetComponentsInChildren<Light>()) l.enabled = false;
                        Sfx.Play(Sfx.PowerDown, 0.9f);
                        rt.Say("KARARTMA — liman karanlığa gömüldü", Theme.Info, "moon");
                        rt.Radio("KONTROL", "Işıklar gitti! Gece görüşünü aç — Kartal arabasına gidiyor!", 4.5f);
                        foreach (var g in guards)
                        {
                            if (g == null || g.Dead || g.Incapacitated) continue;
                            g.SightRange = 7f;
                            if (!g.Alerted && g != p) rt.Idler(g, g.transform.eulerAngles.y, "flashlight", "idle_look");
                        }
                        if (k != null && !k.Dead && !k.Escaped && !k.Panicking && !k.Incapacitated)
                        {
                            k.EndInvestigation();
                            k.SetPlan(Act.IdleFacing("idle_look", 60f, 2.2f), Act.Move(new[] { rt.M("kartal_door") }, "walk_careful", 1.3f),
                                      Act.IdleFacing("idle_look", 270f, 1.0f), Act.Escape());
                        }
                    });
                    cut.optional = true;
                }
            }.Target("businessman", "Hakan Er", "Kartal", "Lacivert takım, kırmızı kravat, gözlük. Sevkiyatı denetliyor.")
             .Loan("nv")
             .Goal("Silah tüccarı Kartal'ı etkisiz hale getir")
             .Goal("İstersen önce elektrik panosunu vurup limanı karart")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.Custom, "Işıkları söndür ve karanlıkta vur", 0, r => r.Flag("blackout"))
             .Star(ChallengeKind.NoAlarm, "Alarm verdirme"));

            // ------------------------------------------------------------------ 12. the needle (tranquilizer, no one dies)
            list.Add(new MissionDef
            {
                id = "m12", number = 12, title = "İĞNE", location = "Rıhtım — Meridian Star iskelesi",
                preset = "overcast", wind = V(2.2f, 0, -1.2f), reward = 8000, focus = "m12_focus", icon = "breath",
                kind = "CANLI YAKALA", clock = "05:30",
                introLine = "Kimyager gemiye binmeden onu uyut. Kimse ölmeyecek — bu iş temiz olmalı.",
                brief = "Dr. Selin Aras, örgütün sentetik uyuşturucu laboratuvarını yöneten kimyager. Şafak sökmeden iki korumasıyla rıhtım boyunca yürüyüp Meridian Star'a binecek.\n\nİstihbarat onu canlı istiyor. Sana yedi uyuşturucu iğne veriyoruz: kimyageri bayılt. Korumaları da bayıltırsan ekibimiz tekneyle gelip onu sessizce alır. Korumalar — ya da gemiden inen biri — baygın kadına ulaşırsa onu gemiye taşırlar.",
                tip = "Mermi seçici tuşuyla UYUŞTURUCU İĞNE'ye geç. İğne yavaş uçar ve çok düşer: kırmızı nokta isabet yerini gösterir. Önce korumaları bayıltmak işi kolaylaştırır.",
                setup = rt =>
                {
                    var walk = rt.P("chemist_walk");
                    var s = rt.AddTarget("guest_f_b", walk[0], 270f);
                    s.FleePath = new[] { walk[walk.Length - 1] };
                    s.HideOnFlee = true;
                    s.EscapeText = "Kimyager gemiye bindi";
                    s.EscapeFailDelay = 2.2f;
                    var g1 = rt.Spawn("bodyguard_a", walk[0] + V(2.0f, 0, -1.2f), 270, Role.Bodyguard, "Koruma");
                    var g2 = rt.Spawn("bodyguard_b", walk[0] + V(2.0f, 0, 1.2f), 270, Role.Bodyguard, "Koruma");
                    s.SetPlan(Act.IdleFacing("phone_conv", 270f, 7f), Act.Move(walk, "walk", 0.85f), Act.Escape());
                    var back = new Vector3[walk.Length];
                    for (int i = 0; i < walk.Length; i++) back[i] = walk[i] + V(2.2f, 0, 0);
                    g1.SetPlan(Act.IdleFacing("idle_look", 270f, 7f), Act.Move(MissionRuntime.Offset(back, -1.3f), "walk", 0.85f), Act.Idle("guard_pose", 0));
                    g2.SetPlan(Act.IdleFacing("idle_look", 270f, 7f), Act.Move(MissionRuntime.Offset(back, 1.3f), "walk", 0.85f), Act.Idle("guard_pose", 0));
                    var off = rt.Spawn("worker_c", rt.M("m12_officer"), rt.MY("m12_officer"), Role.Worker, "Gemi Zabiti");
                    rt.Idler(off, rt.MY("m12_officer"), "idle_wait", "phone", "idle_look");
                    var q = rt.Spawn("worker_b", rt.P("quay_walk")[0], 90, Role.Worker, "Rıhtım İşçisi");
                    rt.Walker(q, rt.P("quay_walk"), "walk", 1f, 4f);

                    var sedate = rt.MustSedate(s, "Kimyageri uyuşturucu iğneyle bayılt");
                    bool boatHere = false;
                    var keep = rt.Protect(s, "Ekip tekneyle gelene kadar onu korumalardan uzak tut", () => boatHere);
                    keep.hidden = true;
                    System.Func<NPC, bool> out_ = g => g == null || g.Dead || g.Incapacitated || g.Escaped;
                    rt.When(() => g1 != null && g2 != null && g1.Sedated && g2.Sedated && !s.Sedated, () => rt.SetFlag("guardsFirst"));
                    // the ship's crew: armed, aboard until they see her on the ground
                    var crew = new List<NPC>();
                    for (int i = 0; i < 2; i++)
                    {
                        var cr = rt.Spawn(i == 0 ? "guard_a" : "guard_b", rt.M("ship_gangway") + V(0.8f * i, 0, 0), 90, Role.Guard, "Gemi Mürettebatı", "SİLAHLI",
                                          "Meridian Star'ın adamı — kimyageri gemiye taşımak isteyecek");
                        cr.NoRadio = true;
                        cr.SetHidden(true);
                        crew.Add(cr);
                    }
                    System.Func<Vector3> body = () => s.Pelvis != null ? new Vector3(s.Pelvis.position.x, s.transform.position.y, s.Pelvis.position.z) : s.transform.position;

                    float nearSince = -1f;
                    bool called = false;
                    System.Action callBoat = () =>
                    {
                        if (called) return;
                        called = true;
                        var boat = Level.I.Tagged("speedboat");
                        var start = boat != null ? boat.transform.position : V(140.5f, -2.5f, 300f);
                        float x = Mathf.Max(body().x, -28f);
                        var mover = rt.MoveProp("speedboat", new[] { start, V(122f, start.y, 292f), V(x + 14f, start.y, 272f), V(x, start.y, 267.4f) }, 11f, false);
                        System.Action pick = () =>
                        {
                            if (!rt.IsPlaying || s == null || s.Dead) return;
                            boatHere = true;
                            rt.ClearCountdown();
                            s.SetHidden(true);
                            rt.Say("Ekip kimyageri tekneye aldı!", Theme.Success, "boat");
                        };
                        int eta = 20;
                        if (mover != null && mover.Running)
                        {
                            mover.Arrived = () => rt.After(1.8f, pick);
                            rt.Countdown("Teknenin gelmesine", () => boatHere ? 0f : MissionRuntime.EtaOf(mover, 11f * mover.SpeedScale, 1.8f));
                            eta = Mathf.RoundToInt(MissionRuntime.EtaOf(mover, 11f, 1.8f));
                            // the crew have seen her: down the gangway to carry her aboard (they must be stopped too)
                            rt.After(3f, () =>
                            {
                                if (boatHere || s.Dead || !rt.IsPlaying) return;
                                var b0 = body();
                                rt.Say("Gemiden iki kişi iniyor — kimyagere gidiyorlar!", Theme.Danger, "alert");
                                int k = 0;
                                foreach (var cr in crew)
                                {
                                    if (cr == null || cr.Dead || cr.Incapacitated) continue;
                                    cr.SetHidden(false);
                                    var to = b0 + V(k == 0 ? 0.8f : -0.8f, 0f, 0.6f);
                                    k++;
                                    cr.MarkAlerted();
                                    if (!Level.WalkableLine(cr.transform.position, to, out float ty)) to = b0 + Vector3.right * 0.8f;
                                    // close to the ship they come down carefully, looking round (time for two darts)
                                    bool closeBy = (b0 - rt.M("ship_gangway")).magnitude < 16f;
                                    var crewPlan = new List<Act>();
                                    if (closeBy) crewPlan.Add(Act.IdleFacing("idle_look", MissionRuntime.Yaw(cr.transform.position, b0), 1.2f));
                                    crewPlan.Add(Act.Move(new[] { cr.transform.position + V(4f, 0, -2f), to }, closeBy ? "walk_careful" : "run"));
                                    crewPlan.Add(Act.IdleFacing("duck", MissionRuntime.Yaw(to, b0), 0));
                                    cr.SetPlan(crewPlan);
                                }
                            });
                            // all of them asleep: the boat comes in fast
                            rt.When(() => boatHere || s.Dead || crew.TrueForAll(x => out_(x)), () =>
                            {
                                if (boatHere || s.Dead || !mover.Running) return;
                                mover.SpeedScale = 2.6f;
                                rt.Radio("KONTROL", "Herkes yerde. Tekne hızlanıyor!", 3f);
                            });
                        }
                        else
                        {
                            rt.After(20f, pick);
                            rt.Countdown("Teknenin gelmesine", 20f);
                        }
                        rt.Radio("KONTROL", "Korumalar temiz. Tekne geliyor — " + eta + " saniye.", 4f);
                    };
                    rt.When(() => s.Sedated, () =>
                    {
                        rt.Reveal(keep);
                        rt.Radio("KONTROL", "İğne tuttu! Korumaları durdur, sonra ekibi gönderiyorum.", 4f);
                        // the guards stop and draw at once; they run to her when she lies on the ground
                        foreach (var g in new[] { g1, g2 })
                        {
                            if (out_(g)) continue;
                            g.MarkAlerted();
                            g.SetPlan(Act.IdleFacing("pull_gun", g.YawTo(s.transform.position), 1.0f), Act.IdleFacing("guard_pose", g.YawTo(s.transform.position), 0));
                        }
                        rt.When(() => s.Down, () =>
                        {
                            var b = body();
                            foreach (var g in new[] { g1, g2 })
                            {
                                if (out_(g)) continue;
                                var side = g.transform.position - b; side.y = 0f;
                                var to = b + (side.sqrMagnitude > 0.01f ? side.normalized : Vector3.right) * 0.8f;
                                if (!Level.WalkableLine(g.transform.position, to, out float ty)) to = b + Vector3.right * 0.8f;
                                g.SetPlan(Act.Move(new[] { to }, "run"), Act.IdleFacing("duck", MissionRuntime.Yaw(to, b), 0));
                            }
                        });
                        rt.When(() => out_(g1) && out_(g2), callBoat);
                    });
                    // a bodyguard kneeling next to her for a few seconds gets her onto the ship
                    rt.When(() =>
                    {
                        if (!s.Sedated || boatHere) { nearSince = -1f; return false; }
                        bool near = false, guardNear = false;
                        var b = body();
                        foreach (var g in new[] { g1, g2 })
                        {
                            if (out_(g)) continue;
                            var d = g.transform.position - b; d.y = 0f;
                            if (d.magnitude < 2.0f) near = guardNear = true;
                        }
                        foreach (var g in crew)
                        {
                            if (out_(g) || g.IsHidden) continue;
                            var d = g.transform.position - b; d.y = 0f;
                            if (d.magnitude < 2.0f) near = true;
                        }
                        if (!near) { nearSince = -1f; return false; }
                        if (nearSince < 0f)
                        {
                            nearSince = rt.Elapsed;
                            rt.Say(guardNear ? "Koruma kimyagere ulaştı — onu taşıyacak!" : "Mürettebat kimyagere ulaştı — onu kaldırıyorlar!", Theme.Danger, "alert");
                        }
                        // her own guards have her up in moments; the ship's men need a little longer
                        return rt.Elapsed - nearSince > (guardNear ? 8f : 9f);     // test 5: 4.5 s left no time for two slow darts at 320 m
                    }, () => rt.Fail("Kimyageri gemiye taşıdılar", 2f));
                }
            }.Target("guest_f_b", "Dr. Selin Aras", "Kimyager", "Siyah mont, sarı saç. İki korumayla yürüyor.")
             .Loan("tranq:7")
             .Goal("Kimyageri uyuşturucu iğneyle bayılt")
             .Goal("Korumaları durdur, ekip onu tekneyle alsın")
             .Star(ChallengeKind.Complete, "Kimyageri canlı teslim et")
             .Star(ChallengeKind.NoKills, "Kimseyi öldürme")
             .Star(ChallengeKind.Custom, "Önce korumaları bayılt", 0, r => r.Flag("guardsFirst")));

            return list;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// Runs one contract: spawns and scripts the characters, resolves shots (kills, limb wounds, darts, explosive rounds,
    /// shots through thin cover), tracks objectives, witnesses and the alarm, decides the kill cam, and ends the contract
    /// with star challenges and cash.
    /// </summary>
    public class MissionRuntime : MonoBehaviour
    {
        public static MissionRuntime I;

        public enum Phase { Idle, Intro, Playing, Ending, Done }

        public MissionDef Def { get; private set; }
        public Phase State { get; private set; } = Phase.Idle;
        public bool IsPlaying => State == Phase.Playing;
        public float Elapsed { get; private set; }
        public bool Alarm { get; private set; }
        public float AlarmCountdown { get; private set; } = -1f;
        public NPC AlarmGuard { get; private set; }
        public string CountdownLabel { get; private set; }
        public float CountdownLeft => countdownEnd > 0 ? Mathf.Max(0, countdownEnd - Elapsed) : -1f;
        public int Shots { get; private set; }
        public int Hits { get; private set; }
        public int Headshots { get; private set; }
        public int Kills { get; private set; }
        public int CiviliansKilled { get; private set; }
        public int CiviliansHurt { get; private set; }
        public int GuardsKilled { get; private set; }
        /// <summary>V2: armoured gunmen the player put down (a daily job counts them).</summary>
        public int ArmoredKilled { get; private set; }
        public int TargetsKilled { get; private set; }
        public float LongestKill { get; private set; }
        public bool AllTargetHeadshots { get; private set; } = true;
        public bool AlarmEver { get; private set; }
        public bool Success { get; private set; }
        public string FailReason { get; private set; }
        /// <summary>Star challenges met in the last run (result screen).</summary>
        public int Stars { get; private set; }
        /// <summary>The third star was withheld because easy aim was on.</summary>
        public bool EasyStarLost { get; private set; }
        public bool[] ChallengeMet { get; private set; } = new bool[0];
        public bool[] ChallengeNew { get; private set; } = new bool[0];
        public int CashEarned { get; private set; }
        bool armorTold;
        /// <summary>A plate carrier stopped a round in this contract (the armour lesson).</summary>
        public bool ArmorStopped => armorTold;
        float lastCallAt = -100f;
        /// <summary>Career: experience and diamonds this contract paid, the levels it crossed (with their rewards).</summary>
        public int XpEarned { get; private set; }
        public int GemsEarned { get; private set; }
        public int XpBefore { get; private set; }
        public readonly List<Progress.LevelReward> LevelUps = new List<Progress.LevelReward>();
        /// <summary>V2: what the pay is made of, line by line (result screen).</summary>
        public readonly List<KeyValuePair<string, int>> Ledger = new List<KeyValuePair<string, int>>();
        /// <summary>V2: S / A / B / C / D for a finished contract ("—" when it failed).</summary>
        public string Grade { get; private set; } = "—";
        /// <summary>V2: the longest kill beat this contract's record.</summary>
        public bool NewDistanceRecord { get; private set; }
        /// <summary>V2: a VETERAN run (no aim help, hard wind, no marks, ×1.5 pay).</summary>
        public bool Veteran { get; private set; }
        /// <summary>V2: today's contract of the day (veteran rules, double pay once a day).</summary>
        public bool Daily { get; private set; }
        /// <summary>V2 recon: threats (guards, gunmen, lookouts) scanned before the first shot, out of those in sight.</summary>
        public int ReconMarked { get; private set; }
        public int ReconTotal { get; private set; }
        /// <summary>The first shot has not been fired yet: scanning still counts as recon.</summary>
        public bool ReconOpen { get; private set; }
        /// <summary>V2 time window: seconds the target he knows of stays in reach, as far as the target's orders say
        /// (-1 = no end in sight, or nothing to tell).</summary>
        public float Window { get; private set; } = -1f;
        float nextWindowCheck, nextReconCheck;
        public bool DroneUsed { get; private set; }
        public readonly List<NPC> Targets = new List<NPC>();
        public readonly List<Objective> Objectives = new List<Objective>();
        /// <summary>Script flags for custom star challenges ("blackout", "aimed"...).</summary>
        readonly HashSet<string> flags = new HashSet<string>();
        public void SetFlag(string f) => flags.Add(f);
        /// <summary>A louder sound covers the shots right now (fireworks): their crack and impact are heard much nearer.</summary>
        public Func<bool> NoiseMask;
        public bool Flag(string f) => flags.Contains(f);
        public List<ChallengeDef> Challenges => Def != null ? Def.Challenges : ChallengeDef.Defaults();

        public event Action<string, Color, string> Toast;          // text, colour, icon
        public event Action<string, float> Tip;                    // longer hint shown in the coach box (text, seconds)
        public event Action<string, string, float> RadioLine;      // speaker, text, seconds
        public event Action<NPC, bool> Killed;                    // npc, headshot
        public event Action<NPC, BodyZone> Wounded;               // npc, zone (non-lethal hit or dart: zone Torso)
        public event Action<bool> Finished;                       // success
        public event Action Started;
        public event Action ObjectivesChanged;

        Game game;
        Transform root;
        float countdownEnd = -1f;
        bool countdownWarned;
        float nextVisCheck, nextHint;
        /// <summary>Where the HUD suggests a distraction shot (null = none).</summary>
        public Vector3? DistractPoint { get; private set; }
        /// <summary>V3: where the HUD pins the target's room (<see cref="MissionDef.pin"/>; null: no pin).</summary>
        public Vector3? PinPoint { get; private set; }
        /// <summary>The pin is up while a target is still to be dealt with.</summary>
        public bool PinShown => PinPoint.HasValue && State == Phase.Playing &&
                                Targets.Exists(t => t != null && !t.Dead && !t.Escaped && !t.Incapacitated);
        float distractUntil;
        public bool IsNight => game != null && game.Env != null && game.Env.Current != null && game.Env.Current.night;
        readonly List<KeyValuePair<float, Action>> timeline = new List<KeyValuePair<float, Action>>();
        Projectile killCamShot;
        bool completing;
        public float DroneUntil { get; private set; } = -1f;

        public void Init(Game g)
        {
            I = this;
            game = g;
            game.Sniper.Fired += OnFired;
            game.Sniper.Scanned += OnScanned;
            var ff = GetComponent<Firefight>();
            if (ff == null) ff = gameObject.AddComponent<Firefight>();
            ff.Init(this, game.Sniper);
        }

        /// <summary>A hint in the coach box (first time under fire, a lookout ...).</summary>
        public void Hint(string text, float seconds) => Tip?.Invoke(text, seconds);

        // ------------------------------------------------------------------ lifecycle
        public void Begin(MissionDef def)
        {
            Abort();
            Def = def;
            Elapsed = 0;
            Alarm = false; AlarmEver = false; AlarmCountdown = -1; AlarmGuard = null;
            Shots = Hits = Headshots = Kills = CiviliansKilled = CiviliansHurt = GuardsKilled = TargetsKilled = ArmoredKilled = 0;
            LongestKill = 0; AllTargetHeadshots = true; Success = false; FailReason = null; Stars = 0; CashEarned = 0; EasyStarLost = false;
            WrongTags = 0; tagLockUntil = 0f;
            Ledger.Clear(); Grade = "—"; NewDistanceRecord = false;
            armorTold = false; XpEarned = 0; GemsEarned = 0; LevelUps.Clear(); XpBefore = Save.Xp;
            Veteran = Difficulty.IsVeteran(def) && Save.Completed(def.id);
            if (!Veteran && Difficulty.IsVeteran(def)) Difficulty.VeteranId = null;
            Daily = Veteran && Difficulty.IsDaily(def) && !Save.DailyDone(Difficulty.TodayKey);
            ReconMarked = ReconTotal = 0; ReconOpen = true; Window = -1f; nextWindowCheck = nextReconCheck = 0f;
            reinforcements = 0; missesOnScene = 0;
            ChallengeMet = new bool[0]; ChallengeNew = new bool[0];
            countdownEnd = -1; CountdownLabel = null; countdownWarned = false;
            Targets.Clear();
            Objectives.Clear();
            timeline.Clear();
            watchers.Clear();
            flags.Clear();
            killCamShot = null;
            DistractPoint = null;
            NoiseMask = null;
            nextHint = 12f;
            nextAimWarning = 0f;
            completing = false;
            DroneUsed = false;
            DroneUntil = -1f;

            Gear.SetLoans(def.loan);
            game.Env.Apply(def.preset, game.Cam, game.Post);
            ChimneySmoke.SetAll(1f);                     // smoke colour and drift for this contract's weather
            // V2: the place picked in the briefing (the main nest unless another one sees this contract)
            Nests = Level.I.NestsFor(def);
            Nest = Nests.Find(x => x.id == PreferredNest) ?? Nests[0];
            PreferredNest = null;
            Relocations = 0;
            game.Sniper.Place(Nest);
            game.Sniper.ResetForMission(Nest, def.wind, def);
            Sfx.SetAlarm(false);
            Sfx.SetRain(game.Env.Current != null && game.Env.Current.rain);

            root = new GameObject("Mission_" + def.id).transform;
            nestFrame = NestFrame.Build(root, Nest);
            WanderIdlers = true;
            Firefight.I?.Begin(def);
            pendingWander.Clear();
            inSetup = true;
            try { def.setup?.Invoke(this); }
            catch (Exception e) { Debug.LogException(e); }
            inSetup = false;
            try { FinishWander(); }
            catch (Exception e) { Debug.LogException(e); }
            try { PutUpFlags(def); }
            catch (Exception e) { Debug.LogException(e); }
            try { CheckScoutedNest(); }
            catch (Exception e) { Debug.LogException(e); }
            // contracts without a main objective (none, or only bonus lines): eliminate every target, main lines first
            if (!Objectives.Exists(o => !o.optional))
            {
                var bonus = new List<Objective>(Objectives);
                Objectives.Clear();
                foreach (var t in Targets) if (t != null) Eliminate(t);
                Objectives.AddRange(bonus);
            }
            if (!string.IsNullOrEmpty(def.focus) && Level.I.HasMarker(def.focus))
                game.Sniper.LookAt(Level.I.Marker(def.focus) + Vector3.up * 1.2f);
            PinPoint = !string.IsNullOrEmpty(def.pin) && Level.I.HasMarker(def.pin) ? Level.I.Marker(def.pin) + Vector3.up * def.pinUp : (Vector3?)null;
            State = def.noIntro ? Phase.Playing : Phase.Intro;
            if (State == Phase.Playing) { StartCoroutine(ShootingCoach()); StartCoroutine(SightCoach()); }
            Started?.Invoke();
            ObjectivesChanged?.Invoke();
            try
            {
                Attempt = PlayerPrefs.GetInt(Save.K + "att_" + def.id, 0) + 1;
                PlayerPrefs.SetInt(Save.K + "att_" + def.id, Attempt);
                var rifle = Gear.Carried;
                Analytics.Crumb("contract " + def.id + " #" + Attempt);
                Analytics.Event("level_start", "level_name", def.id);
                Analytics.Event("contract_start", "contract_id", def.id, "contract_no", def.number, "region", def.region,
                                "attempt", Attempt, "replay", Save.Completed(def.id) ? 1 : 0, "veteran", Veteran ? 1 : 0, "daily", Daily ? 1 : 0,
                                "rifle", rifle != null ? rifle.id : "", "power", Gear.Power(rifle), "recommended", Progress.Recommended(def),
                                "player_level", Progress.Level);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>V3: the contract's own starting place (NestScout) was found before its people were put out. Now that
        /// they stand where they stand: if it sees none of the targets, or a gunman stands almost on top of it, the
        /// contract starts on the roof after all.</summary>
        void CheckScoutedNest()
        {
            if (Nest == null || Nest.id != "scout") return;
            var eye = new Vector3(Nest.x, Nest.y, Nest.z);
            int world = (1 << Layers.Default) | (1 << Layers.Reflect);
            int targets = 0, seen = 0;
            bool crowded = false;
            foreach (var n in NPC.All)
            {
                if (n == null || n.Dead) continue;
                float d = Vector3.Distance(n.transform.position, eye);
                if (d < 14f) crowded = true;
                if (!n.IsTarget || n.IsHidden) continue;
                targets++;
                if (!Physics.Linecast(eye, n.ChestPoint, world, QueryTriggerInteraction.Ignore) ||
                    !Physics.Linecast(eye, n.AimPoint, world, QueryTriggerInteraction.Ignore)) seen++;
            }
            if (!crowded && (targets == 0 || seen > 0)) return;
            var roof = Nests.Find(x => x.id == "main");
            if (roof == null) return;
            Debug.Log("[SC] scouted nest dropped for " + Def.id + (crowded ? " (people too close)" : " (no target in sight)"));
            Nests.Remove(Nest);
            Nest = roof;
            game.Sniper.Place(Nest);
            if (nestFrame != null) Destroy(nestFrame.gameObject);
            nestFrame = NestFrame.Build(root, Nest);
        }

        /// <summary>How many times this contract has been started (this one included).</summary>
        public int Attempt { get; private set; }

        /// <summary>The intro cinematic is over: the contract starts for real (clock, timeline, input).</summary>
        public void EndIntro()
        {
            if (State == Phase.Intro) { State = Phase.Playing; StartCoroutine(ShootingCoach()); StartCoroutine(SightCoach()); }
        }

        /// <summary>
        /// The target is out in the open but this place has no line to him (a parapet, a tower, a car in between) for a
        /// good while: the handler points at the other places, once per contract. Test 3 showed the bot finding the line
        /// only by moving (m3, m15, m19) — a player needs to be told the option exists.
        /// </summary>
        IEnumerator SightCoach()
        {
            var def0 = Def;
            float blocked = 0f;
            int world = (1 << Layers.Default) | (1 << Layers.Reflect);
            while (State == Phase.Playing && Def == def0)
            {
                yield return new WaitForSeconds(1f);
                var sn = game != null ? game.Sniper : null;
                if (sn == null || sn.Cam == null || Relocating || sn.InCover || sn.CoverBlend > 0.1f) { blocked = 0f; continue; }
                NPC t = Targets.Find(n => n != null && !n.Incapacitated && !n.IsHidden && !n.Escaped);
                if (t == null) { blocked = 0f; continue; }
                var eye = sn.Cam.transform.position;
                bool Seen(Vector3 p)
                {
                    var d = p - eye;
                    return !Physics.Raycast(eye, d.normalized, d.magnitude - 0.6f, world, QueryTriggerInteraction.Ignore);
                }
                if (Seen(t.ChestPoint) || Seen(t.AimPoint)) { blocked = 0f; continue; }
                blocked += 1f;
                if (blocked >= 8f && CanRelocate)
                {
                    Radio("KONTROL", "Bu açıdan " + t.DisplayName + " görünmüyor. YER DEĞİŞTİR ile başka bir yere geç.", 5f);
                    yield break;
                }
            }
        }

        /// <summary>The first time a new shooting skill is asked for, the handler says so (once per skill).</summary>
        IEnumerator ShootingCoach()
        {
            var sn = game != null ? game.Sniper : null;
            if (sn == null || Def == null) yield break;
            if (IdMode && !Save.Seen("v2_id") && Targets.Exists(t => t != null && t.NeedsId))
            {
                yield return new WaitForSeconds(2.5f);
                if (State != Phase.Playing) yield break;
                Save.SetSeen("v2_id");
                Hint("Bundan sonra hedef işaretli gelmez. Dürbüne gir: sağdaki DOSYA'daki yüzü kalabalıkta bul, kişiyi tara, emin olunca İŞARETLE. Yanlış kişiyi vurursan sözleşme iptal.", 11f);
                yield return new WaitForSeconds(13f);
                if (State != Phase.Playing) yield break;
            }
            string key = null, text = null;
            var d = sn.Diff;
            if (d.easy) yield break;
            if (Def.number >= 2 && d.tier == 0) { key = "v2_zero"; text = "Uzak hedefte mermi düşer. Sol alttaki MENZİL ile dürbünü hedefin mesafesine ayarla (telemetre dürbünün altında) ya da sarı çizgilerden tut: 2 = 200 m, 3 = 300 m."; }
            else if (d.tier == 1) { key = "v2_breath"; text = "Bundan sonra isabet noktası sadece NEFES tutarken görünür. Nefesini doğru ana sakla."; }
            else if (d.tier == 2) { key = "v2_noassist"; text = "Şehirde isabet noktası yok. Menzili ayarla, rüzgâr panelindeki yöne göre sarı noktalardan (1 nokta = 1 mil) tut."; }
            else if (d.tier == 3) { key = "v2_gusts"; text = "Dağda rüzgâr sert ve değişken; paneldeki ok ve güç anlık değişir. Okun yönünü ve gücünü izle, rüzgârın dindiği anı bekle."; }
            if (key == null || Save.Seen(key)) yield break;
            yield return new WaitForSeconds(3f);
            if (State != Phase.Playing) yield break;
            Save.SetSeen(key);
            Hint(text, 9f);
        }

        /// <summary>Removes everything the mission spawned (characters, effects, moving props).</summary>
        public void Abort()
        {
            StopAllCoroutines();
            Projectile.ClearAll();
            Firefight.I?.Clear();
            if (game != null && game.Sniper != null) game.Sniper.ResetPose();     // not left ducked or fallen over behind the menu
            countdownFn = null;
            if (KillCam.Active) KillCam.ForceStop();
            if (Cinematic.Active) Cinematic.ForceStop();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            foreach (var n in NPC.All.ToArray()) if (n != null) Destroy(n.gameObject);
            NPC.All.Clear();
            if (root != null) Destroy(root.gameObject);
            root = null;
            FX.ClearAll();
            // shootables first: ResetDynamic hides the contract-only props, and a hidden one leaves Shootable.All
            foreach (var sh in Shootable.All.ToArray()) if (sh != null) sh.ResetState();
            if (Level.I != null) Level.I.ResetDynamic();
            foreach (var e in ExplosiveBarrel.All.ToArray()) if (e != null) e.ResetState();
            foreach (var c in CarAlarmFX.All.ToArray()) if (c != null) c.Stop();
            DistractPoint = null;
            PinPoint = null;
            Sfx.SetAlarm(false);
            Sfx.SetCrowd(0f);
            Gear.SetLoans(null);
            Objectives.Clear();
            State = Phase.Idle;
        }

        void Update()
        {
            if (State != Phase.Playing) return;
            float dt = Time.deltaTime;
            Elapsed += dt;
            for (int i = timeline.Count - 1; i >= 0; i--)
            {
                if (i >= timeline.Count || Elapsed < timeline[i].Key) continue;
                var a = timeline[i].Value;
                timeline.RemoveAt(i);
                try { a?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                if (State != Phase.Playing) return;
            }
            if (AlarmCountdown > 0)
            {
                if (AlarmGuard == null || AlarmGuard.Dead || AlarmGuard.Sedated)
                {
                    AlarmCountdown = -1;
                    AlarmGuard = null;
                    Say("Alarm engellendi", Theme.Success, "check");
                }
                else
                {
                    // a story cutaway: the player cannot stop him meanwhile, so the call waits
                    if (!KillCam.IsCutaway) AlarmCountdown -= dt;
                    if (AlarmCountdown <= 0)
                    {
                        // a lookout who saw the nest gives it away (his side fires on it); any other call (a guard, a
                        // lookout who heard a shot or saw a body) raises the alarm
                        if (alarmIsSpotting) LookoutCalled();
                        else RaiseAlarm();
                    }
                }
            }
            UpdateVisibility(dt);
            UpdateReconAndWindow();
            if (countdownFn != null)
            {
                float left = SafeSeconds(countdownFn);
                if (left <= 0.02f) ClearCountdown();
                else countdownEnd = Elapsed + left;
            }
            if (DistractPoint.HasValue && Elapsed > distractUntil) DistractPoint = null;
            if (countdownEnd > 0 && !countdownWarned && CountdownLeft <= 10f)
            {
                countdownWarned = true;
                Say("Son 10 saniye!", Theme.Accent, "timer");
            }
            if (Def.timeLimit > 0 && Elapsed >= Def.timeLimit) { Fail("Süre doldu"); return; }
            PollWatchers();
            if (State != Phase.Playing) return;
            PollObjectives();
        }

        // ------------------------------------------------------------------ V2: a mistake has a price (reinforcements)
        // The user: "every contract ends with one bullet". A miss next to the target, a raised alarm, a wrong body: the
        // organisation's gunmen come out of the buildings around the target, take cover and fire at the nest. The heat
        // jumps, so the nest is found soon after; the job is then finished under fire, from behind the parapet, or from
        // another place. Up to two waves per contract, bigger later in the campaign.
        int reinforcements, missesOnScene;

        static string GunmanFor(string region, int i)
        {
            switch (region)
            {
                case "city": return i % 2 == 0 ? "bodyguard_a" : "bodyguard_b";
                case "mountain": return i % 2 == 0 ? "w_guard" : "w_guard2";
                default: return i % 2 == 0 ? "guard_a" : "guard_b";
            }
        }

        public void Reinforce(string why)
        {
            if (State != Phase.Playing || Def == null || Def.number < 2 || reinforcements >= 2 || game == null || game.Sniper == null) return;
            if (Firefight.I == null || Firefight.I.Threat <= 0f) return;
            reinforcements++;
            int able = 0;
            foreach (var n in NPC.All) if (n != null && !n.Dead && !n.Incapacitated && n.Combat != null && n.Combat.CanFight) able++;
            int want = Def.number < 8 ? 2 : (Def.number < 16 ? 3 : 4);
            if (reinforcements == 2) want += 1;
            int count = Mathf.Clamp(want - able / 2, 1, 4);
            // around the target (or the contract's focus), in the open, where they can see the nest
            Vector3 centre = Level.I.HasMarker(Def.focus) ? Level.I.Marker(Def.focus) : game.Sniper.transform.position + game.Sniper.transform.forward * 120f;
            var t0 = Targets.Find(t => t != null && !t.Dead && !t.Escaped && !t.IsHidden);
            if (t0 != null) centre = t0.transform.position;
            var nest = game.Sniper.transform.position;
            int world = (1 << Layers.Default) | (1 << Layers.Reflect);
            var spots = new List<Vector3>();
            for (int k = 0; k < 60 && spots.Count < count; k++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f, r = UnityEngine.Random.Range(14f, 42f);
                var p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                float y = Level.SurfaceY(p, float.NaN, 40f, 90f);
                if (float.IsNaN(y) || y < Level.WaterY + 0.4f || Mathf.Abs(y - centre.y) > 12f) continue;
                p.y = y;
                if (spots.Exists(q => (q - p).sqrMagnitude < 25f)) continue;
                var eye = p + Vector3.up * 1.5f;
                bool sees = !Physics.Linecast(eye, nest, world, QueryTriggerInteraction.Ignore);
                if (!sees && k < 40) continue;                      // prefer spots with a line to the nest
                spots.Add(p);
            }
            if (spots.Count == 0) return;
            int i = 0;
            foreach (var p in spots)
            {
                bool armored = Difficulty.Armored(Def, i);
                // an armoured man looks it: plate carrier over the clothes (harbour / city: the tactical guards)
                string model = armored ? (Def.region == "mountain" ? "w_guard" : (i % 2 == 0 ? "guard_a" : "guard_b")) : GunmanFor(Def.region, i);
                i++;
                var g = Spawn(model, p, Yaw(p, nest), Role.Guard, armored ? "Zırhlı Adam" : "Silahlı Adam", armored ? "ZIRHLI" : "SİLAHLI",
                              armored ? "Zırhlı — gövde atışını durdurur: kafaya nişan al" : "Takviye — mevzi alıp yuvana ateş edecek");
                g.Armored = armored;
                g.NoRadio = true;
                g.Identified = true;                                  // the player sees them coming (marked)
                if (g.Combat != null) g.Combat.Engage(nest);
            }
            Firefight.I.AddHeat(why == "alarm" ? 0.6f : 0.4f);
            Say(spots.Count + " silahlı adam mevzi alıyor — yuvanı arıyorlar!", Theme.Danger, "guard");
            Radio("KONTROL", why == "alarm" ? "Alarm çaldı, takviye geliyor. Başını eğik tut, önce silahlıları sustur."
                           : why == "kill" ? "Hedef düştü ama adamları seni arıyor. Siper al ya da silahlıları sustur — sonra çık."
                                           : "Iskalaman dikkat çekti — silahlılar dışarı çıkıyor. Siper al, sonra işini bitir.", 4.5f);
        }

        // ------------------------------------------------------------------ V2: recon and time windows
        void UpdateReconAndWindow()
        {
            if (ReconOpen && Elapsed >= nextReconCheck) { nextReconCheck = Elapsed + 0.5f; UpdateRecon(); }
            if (Elapsed < nextWindowCheck) return;
            nextWindowCheck = Elapsed + 0.25f;
            float w = -1f;
            foreach (var t in Targets)
            {
                if (t == null || t.Incapacitated || t.Escaped || t.IsHidden) continue;
                // only a man the player knows about: marked, or found and confirmed
                if (!(t.MarkedTarget || t.Confirmed || (!IdMode && t.Identified))) continue;
                float g = t.SecondsUntilGone();
                if (g >= 0f && (w < 0f || g < w)) w = g;
            }
            Window = w;
        }

        /// <summary>Recon: the threats in sight now and how many of them the scope has already been over.</summary>
        void UpdateRecon()
        {
            int total = 0, marked = 0;
            var sn = game != null ? game.Sniper : null;
            var eye = sn != null && sn.Cam != null ? sn.Cam.transform.position : Vector3.zero;
            int world = (1 << Layers.Default) | (1 << Layers.Reflect);
            foreach (var n in NPC.All)
            {
                if (n == null || !n.IsThreat || n.Dead || n.Escaped) continue;
                if (n.IsHidden && !n.Identified) continue;
                // only those the player could find from here: in rifle range and in the open
                if (!n.Identified && sn != null)
                {
                    var c = n.ChestPoint;
                    var d = c - eye;
                    if (d.magnitude > 480f) continue;
                    if (Physics.Raycast(eye, d.normalized, d.magnitude - 0.6f, world, QueryTriggerInteraction.Ignore)) continue;
                }
                total++;
                if (n.Identified) marked++;
            }
            ReconTotal = Mathf.Max(ReconTotal, total);
            ReconMarked = Mathf.Max(ReconMarked, marked);
        }

        // ------------------------------------------------------------------ scripting helpers (used by MissionDefs)
        public Transform Root => root;
        public Vector3 M(string marker) => Level.I.Marker(marker);
        public float MY(string marker) => Level.I.MarkerYaw(marker);
        public Vector3[] P(string path) => (Vector3[])Level.I.Path(path).Clone();

        public static Vector3[] Rev(Vector3[] p)
        {
            var r = (Vector3[])p.Clone();
            Array.Reverse(r);
            return r;
        }

        public static float Yaw(Vector3 from, Vector3 to)
        {
            var d = to - from;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        static string DefaultLabel(Role r)
        {
            switch (r)
            {
                case Role.Target: return "HEDEF";
                case Role.Guard: return "KORUMA";
                case Role.Bodyguard: return "KORUMA";
                case Role.Worker: return "İŞÇİ";
                default: return "SİVİL";
            }
        }

        public NPC Spawn(string charId, Vector3 pos, float yaw, Role role, string name, string label = null, string hint = null)
        {
            var n = NPC.Spawn(charId, pos, yaw, root);
            n.Role = role;
            n.DisplayName = name;
            n.RoleLabel = label ?? DefaultLabel(role);
            n.Hint = hint;
            n.CanFlee = role != Role.Guard && role != Role.Bodyguard;
            if (role == Role.Guard || role == Role.Bodyguard)
            {
                n.Armed = true;
                // a real gun (slung, holstered or under the jacket until he draws it) and the will to use it
                var g = GunHold.Give(n, Guns.DefaultFor(charId, role));
                if (g != null) Gunner.Attach(n, g);
            }
            return n;
        }

        public NPC AddTarget(string charId, Vector3 pos, float yaw)
        {
            var info = Def.targets.Find(t => t.charId == charId) ?? (Def.targets.Count > 0 ? Def.targets[0] : null);
            var n = Spawn(charId, pos, yaw, Role.Target, info != null ? info.name : "Hedef", "HEDEF", info != null ? info.desc : null);
            n.NeedsId = IdRequired;
            n.IsTarget = true;
            n.Alias = info != null ? info.alias : null;
            Targets.Add(n);
            return n;
        }

        /// <summary>A target by name (contracts with several targets or a target that is not in the dossier list).</summary>
        public NPC AddTarget(string charId, Vector3 pos, float yaw, string name, string alias, string hint)
        {
            var n = Spawn(charId, pos, yaw, Role.Target, name, "HEDEF", hint);
            n.IsTarget = true;
            n.Alias = alias;
            n.NeedsId = IdRequired;
            Targets.Add(n);
            return n;
        }

        /// <summary>An armed killer hidden in the crowd: marked on the HUD only once the scan identified him (or he aims).</summary>
        public NPC AddHitman(string charId, Vector3 pos, float yaw, string name, string hint)
        {
            var n = Spawn(charId, pos, yaw, Role.Target, name, "SUİKASTÇI", hint);
            n.IsTarget = true;
            n.HiddenTarget = true;
            n.Armed = true;
            GunHold.Give(n, GunKind.Pistol);                      // under his jacket until he draws
            n.CanFlee = true;
            Targets.Add(n);
            return n;
        }

        /// <summary>Someone the player must keep alive (blue marker).</summary>
        public NPC AddProtected(string charId, Vector3 pos, float yaw, string name, string label, string hint = null)
        {
            var n = Spawn(charId, pos, yaw, Role.Civilian, name, label, hint);
            n.Protected = true;
            n.Identified = true;
            return n;
        }

        /// <summary>Idlers drift between a few spots close to where they stand (pacing on the phone, shifting their
        /// post) unless the contract turns it off (a packed crowd, a narrow deck).</summary>
        public bool WanderIdlers = true;

        /// <summary>Cycles through idle clips facing one direction (random durations, starts at a random clip). Unless
        /// seated, he takes a few steps to another spot nearby now and then and turns a little: nobody stands frozen to
        /// one spot for minutes.</summary>
        public void Idler(NPC n, float yaw, params string[] clips)
        {
            int start = UnityEngine.Random.Range(0, clips.Length);
            bool seated = false;
            foreach (var c in clips) if (c.StartsWith("sit") || c == "sleep_desk") seated = true;
            n.SetPlan(IdlePlan(n, yaw, clips, start, null));
            // a stroll to a spot or two nearby between the poses: worked out once the whole contract is set up (the
            // people placed after him, everybody's walks)
            if (inSetup && WanderIdlers && !seated && !n.Riding) pendingWander.Add(new PendingIdler { npc = n, yaw = yaw, clips = clips, start = start, version = n.PlanVersion });
        }

        class PendingIdler { public NPC npc; public float yaw; public string[] clips; public int start, version; }
        bool inSetup;
        readonly List<PendingIdler> pendingWander = new List<PendingIdler>();

        static List<Act> IdlePlan(NPC n, float yaw, string[] clips, int start, List<Vector3> spots)
        {
            var acts = new List<Act>();
            var home = n.transform.position;
            int moved = 0;
            float y = yaw;
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[(start + i) % clips.Length];
                float len = Mathf.Max(n.ClipLength(c), 2f);
                float secs = Mathf.Clamp(len * Mathf.Ceil(UnityEngine.Random.Range(5f, 10f) / len), 4f, 16f);
                acts.Add(Act.IdleFacing(c, y, secs));
                if (spots != null && moved < spots.Count && (i % 2 == 0 || clips.Length < 3))
                {
                    acts.Add(Act.Move(new[] { spots[moved++] }, "walk", 0.75f));
                    y = yaw + UnityEngine.Random.Range(-28f, 28f);
                }
            }
            if (moved > 0) { acts.Add(Act.Move(new[] { home }, "walk", 0.75f)); }
            acts.Add(Act.Loop(0));
            return acts;
        }

        /// <summary>Flags near the target area that show the wind (see <see cref="WindFlag"/>).</summary>
        void PutUpFlags(MissionDef def)
        {
            if (def.wind.magnitude < 0.3f || string.IsNullOrEmpty(def.focus) || !Level.I.HasMarker(def.focus)) return;
            var routes = new List<Vector3[]>();
            foreach (var o in NPC.All) if (o != null) o.PlannedRoutes(routes);
            var spots = new List<Vector3>();
            foreach (var m in Level.I.Data.markers) if (m != null) spots.Add(new Vector3(m.x, m.y, m.z));
            foreach (var o in NPC.All) if (o != null) spots.Add(o.transform.position);
            var pl = Nest != null ? Nest : Level.I.Data.player;
            var eye = new Vector3(pl.x, pl.y, pl.z);
            WindFlag.Spawn(root, Level.I.Marker(def.focus), eye, routes, spots, 2);
        }

        /// <summary>After the contract's setup: the idlers get their strolls (unless the script has given them other
        /// orders since), clear of everyone's place and everyone's walks.</summary>
        void FinishWander()
        {
            if (pendingWander.Count == 0) return;
            if (!WanderIdlers) { pendingWander.Clear(); return; }
            var routes = new List<Vector3[]>();
            foreach (var o in NPC.All) if (o != null) o.PlannedRoutes(routes);
            foreach (var w in pendingWander)
            {
                var n = w.npc;
                if (n == null || n.Dead || n.IsHidden || n.Riding || n.PlanVersion != w.version) continue;
                var spots = NearbySpots(n, n.transform.position, 2, routes);
                if (spots.Count == 0) continue;
                n.SetPlan(IdlePlan(n, w.yaw, w.clips, w.start, spots));
            }
            pendingWander.Clear();
        }

        /// <summary>Up to <paramref name="count"/> spots 1.6-3.4 m from <paramref name="home"/> a man can walk to and back,
        /// on the same floor, not where somebody else stands or walks.</summary>
        static List<Vector3> NearbySpots(NPC self, Vector3 home, int count, List<Vector3[]> routes)
        {
            var res = new List<Vector3>();
            float a0 = UnityEngine.Random.value * 360f;
            var prev = home;
            for (int k = 0; k < 10 && res.Count < count; k++)
            {
                float a = (a0 + k * 137.5f) * Mathf.Deg2Rad;
                float r = UnityEngine.Random.Range(1.6f, 3.4f);
                var p = home + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                if (!Level.WalkableLine(prev, p, out float ey, 0.3f) || !Level.WalkableLine(p, home, out _, 0.3f)) continue;
                p.y = ey;
                if (Mathf.Abs(ey - home.y) > 0.3f || ey < Level.WaterY + 0.5f) continue;
                bool crowded = false;
                foreach (var o in NPC.All)
                {
                    if (o == null || o == self) continue;
                    var d = o.transform.position - p; d.y = 0f;
                    if (d.sqrMagnitude < 1.4f * 1.4f) { crowded = true; break; }
                    if (DistToSegmentXZ(o.transform.position, prev, p) < 0.7f) { crowded = true; break; }
                }
                // not standing in somebody's way (a target's walk, a patrol)
                if (!crowded && routes != null)
                    foreach (var route in routes)
                    {
                        for (int i = 1; i < route.Length && !crowded; i++)
                            if (Mathf.Abs(route[i].y - p.y) < 2f && DistToSegmentXZ(p, route[i - 1], route[i]) < 1.2f) crowded = true;
                        if (crowded) break;
                    }
                if (crowded) continue;
                res.Add(p);
                prev = p;
            }
            return res;
        }

        static float DistToSegmentXZ(Vector3 q, Vector3 a, Vector3 b)
        {
            var ab = new Vector2(b.x - a.x, b.z - a.z);
            var aq = new Vector2(q.x - a.x, q.z - a.z);
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(aq, ab) / ab.sqrMagnitude) : 0f;
            return (aq - ab * t).magnitude;
        }

        /// <summary>
        /// A wait with a clock (our team on its way): <paramref name="seconds"/> of contract time, but whenever
        /// <paramref name="busy"/> says there is nothing left for the player to do it runs several times faster — no
        /// sitting idle while a timer counts down. Shown as the countdown; <paramref name="done"/> when it runs out.
        /// </summary>
        public WaitClock Clock(string label, float seconds, Func<bool> busy, Action done, float fast = 5f)
        {
            var c = new WaitClock { Left = seconds, Busy = busy, Fast = fast };
            Countdown(label, () => c.Left);
            Action tick = null;
            tick = () =>
            {
                if (!IsPlaying || c.Stopped) return;
                bool b;
                try { b = busy == null || busy(); } catch (Exception e) { Debug.LogException(e); b = true; }
                c.Left -= 0.1f * (b ? 1f : c.Fast);
                if (!b && !c.Hurried) { c.Hurried = true; c.OnHurry?.Invoke(); }
                if (c.Left <= 0f) { c.Stopped = true; ClearCountdown(); done?.Invoke(); return; }
                After(0.1f, tick);
            };
            After(0.1f, tick);
            return c;
        }

        /// <summary>Ambient traffic: walks the path, disappears at the end, comes back at the start.</summary>
        public void Walker(NPC n, Vector3[] path, string clip = "walk", float speedMul = 1f, float restAtEnd = 3f)
        {
            if (path == null || path.Length < 2) return;
            var start = path[0];
            float y0 = Yaw(path[0], path[1]);
            n.SetPlan(Act.Move(path, clip, speedMul), Act.Hide(), Act.Idle("idle", restAtEnd),
                      Act.Do(() => { n.transform.position = start; n.transform.rotation = Quaternion.Euler(0, y0, 0); n.SetHidden(false); }),
                      Act.Loop(0));
        }

        /// <summary>Loops around a closed path, optionally pausing at every corner.</summary>
        public void Patrol(NPC n, Vector3[] loop, string clip = "walk", float speedMul = 1f, string pauseClip = null, float pause = 0f)
        {
            if (loop == null || loop.Length < 2) return;
            var acts = new List<Act>();
            for (int i = 0; i < loop.Length; i++)
            {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Length];
                acts.Add(Act.Move(new[] { a, b }, clip, speedMul));
                if (!string.IsNullOrEmpty(pauseClip) && pause > 0) acts.Add(Act.IdleFacing(pauseClip, Yaw(a, b), pause));
            }
            acts.Add(Act.Loop(0));
            n.SetPlan(acts);
        }

        /// <summary>Walks back and forth between the ends of a path.</summary>
        public void PingPong(NPC n, Vector3[] pts, string clip = "walk", float speedMul = 1f, string pauseClip = "idle_look", float pause = 3f)
        {
            var back = Rev(pts);
            n.SetPlan(Act.Move(pts, clip, speedMul), Act.IdleFacing(pauseClip, Yaw(pts[pts.Length - 1], pts[0]), pause),
                      Act.Move(back, clip, speedMul), Act.IdleFacing(pauseClip, Yaw(pts[0], pts[pts.Length - 1]), pause), Act.Loop(0));
        }

        public void At(float seconds, Action a) => timeline.Add(new KeyValuePair<float, Action>(seconds, a));

        /// <summary>Runs <paramref name="a"/> <paramref name="seconds"/> from now (contract clock).</summary>
        public void After(float seconds, Action a) => At(Elapsed + seconds, a);

        readonly List<KeyValuePair<Func<bool>, Action>> watchers = new List<KeyValuePair<Func<bool>, Action>>();

        /// <summary>Runs <paramref name="then"/> once, the first frame <paramref name="cond"/> is true (checked while playing).</summary>
        public void When(Func<bool> cond, Action then) => watchers.Add(new KeyValuePair<Func<bool>, Action>(cond, then));

        void PollWatchers()
        {
            for (int i = watchers.Count - 1; i >= 0; i--)
            {
                if (i >= watchers.Count) continue;
                var w = watchers[i];
                bool fire;
                try { fire = w.Key(); } catch (Exception e) { Debug.LogException(e); fire = false; watchers.RemoveAt(i); continue; }
                if (!fire) continue;
                watchers.RemoveAt(i);
                try { w.Value?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                if (State != Phase.Playing) return;
            }
        }

        /// <summary>A path shifted sideways (walking next to someone, a parallel lane).</summary>
        public static Vector3[] Offset(Vector3[] pts, float side)
        {
            var r = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[Mathf.Max(0, i - 1)];
                var b = pts[Mathf.Min(pts.Length - 1, i + 1)];
                var d = b - a; d.y = 0f;
                var right = d.sqrMagnitude > 1e-4f ? new Vector3(d.z, 0f, -d.x).normalized : Vector3.right;
                r[i] = pts[i] + right * side;
            }
            return r;
        }

        /// <summary>The rest of a path from the point closest to <paramref name="from"/>.</summary>
        public static Vector3[] From(Vector3[] pts, Vector3 from)
        {
            if (pts == null || pts.Length == 0) return pts;
            int k = 0;
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                float d = (pts[i] - from).sqrMagnitude;
                if (d < best) { best = d; k = i; }
            }
            // already on the route (within 2 m of it) part of the way along a stretch: on to the end of that stretch,
            // not back to the point behind him
            if (pts.Length > 1)
            {
                int seg = -1;
                float segBest = float.MaxValue, segT = 0f;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    var a = pts[i];
                    var ab = pts[i + 1] - a; ab.y = 0f;
                    var ap = from - a; ap.y = 0f;
                    float l2 = ab.sqrMagnitude;
                    float t = l2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / l2) : 0f;
                    var q = ap - ab * t;
                    float d = q.sqrMagnitude;
                    if (d < segBest) { segBest = d; seg = i; segT = t; }
                }
                if (seg >= 0 && segBest < 4f) k = segT > 0.05f ? seg + 1 : seg;
            }
            var r = new Vector3[pts.Length - k];
            Array.Copy(pts, k, r, 0, r.Length);
            return r;
        }

        public void Countdown(string label, float seconds)
        {
            countdownFn = null;
            CountdownLabel = label;
            countdownEnd = Elapsed + seconds;
            countdownWarned = false;
            float end = countdownEnd;
            At(end, () => { if (countdownFn == null && Mathf.Approximately(countdownEnd, end)) { countdownEnd = -1; CountdownLabel = null; } });
        }

        /// <summary>A countdown that follows a live estimate (a car or boat on its way): <paramref name="secondsLeft"/> is
        /// polled every frame; it clears itself once the estimate stays at zero or the script calls ClearCountdown.</summary>
        public void Countdown(string label, Func<float> secondsLeft)
        {
            CountdownLabel = label;
            countdownFn = secondsLeft;
            countdownWarned = false;
            countdownEnd = Elapsed + Mathf.Max(0.01f, SafeSeconds(secondsLeft));
        }

        static float SafeSeconds(Func<float> f)
        {
            try { return f(); } catch (Exception e) { Debug.LogException(e); return 0f; }
        }

        public void ClearCountdown() { countdownEnd = -1; CountdownLabel = null; countdownFn = null; }

        Func<float> countdownFn;

        /// <summary>Seconds a moving prop still needs to the end of its path (rough: remaining distance / cruise speed).</summary>
        /// <summary>Seconds until <paramref name="m"/> stops at the end of its path (it brakes for corners and the stop,
        /// and hurries when its SpeedScale goes up); <paramref name="speed"/> is only used for a mover that is not
        /// running its path.</summary>
        public static float EtaOf(PathMover m, float speed, float extra = 0f)
        {
            if (m == null || !m.Running) return extra;
            return m.SecondsLeft() + 0.3f + extra;
        }

        public void Say(string text, Color c, string icon = null) => Toast?.Invoke(text, c, icon);

        /// <summary>A line on the radio (handler, spotter, intercepted call) in the subtitle box.</summary>
        public void Radio(string who, string text, float seconds = 5f) => RadioLine?.Invoke(who, text, seconds);

        /// <summary>Radio line at a point of the contract clock.</summary>
        public void RadioAt(float t, string who, string text, float seconds = 5f) => At(t, () => Radio(who, text, seconds));

        /// <summary>Drives a tagged prop along a path. <paramref name="reverseSegments"/>: the first segments are driven
        /// backwards (backing out of a parking spot).</summary>
        public PathMover MoveProp(string tag, Vector3[] pts, float speed, bool pingPong, int reverseSegments = 0)
        {
            var go = Level.I.Tagged(tag);
            if (go == null) return null;
            var m = go.GetComponent<PathMover>();
            if (m == null) m = go.AddComponent<PathMover>();
            m.Run(pts, speed, pingPong, reverseSegments);
            return m;
        }

        public void AttachToProp(NPC n, string tag)
        {
            var go = Level.I.Tagged(tag);
            if (go == null || n == null) return;
            n.transform.SetParent(go.transform, true);
            n.EndInvestigation();
            n.Riding = true;                  // from now on he reacts in his seat (see NPC.Riding)
        }

        /// <summary>Gets off a prop the character was riding (back under the mission root).</summary>
        public void DetachFromProp(NPC n)
        {
            if (n == null) return;
            n.transform.SetParent(root, true);
            n.Riding = false;
            n.EndInvestigation();
        }

        public Light Searchlight(Vector3 pos, float yawCenter, float sweep, float pitch = 14f)
        {
            var go = new GameObject("Searchlight");
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = 95f;
            l.spotAngle = 16f;
            l.intensity = 7f;
            l.color = new Color(1f, 0.95f, 0.85f);
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            var s = go.AddComponent<Sweeper>();
            s.yawCenter = yawCenter;
            s.sweep = sweep;
            s.pitch = pitch;
            s.speed = 0.22f;
            l.enabled = game.Env.Current != null && game.Env.Current.night;
            return l;
        }

        // ------------------------------------------------------------------ objectives
        Objective AddObjective(Objective o)
        {
            o.State = ObjectiveState.Active;
            o.changedAt = Elapsed;
            Objectives.Add(o);
            ObjectivesChanged?.Invoke();
            return o;
        }

        public Objective Eliminate(NPC n, string text = null) =>
            AddObjective(new Objective { kind = ObjectiveKind.Eliminate, npc = n, text = text ?? "Etkisiz hale getir: " + (n != null ? n.DisplayName : "hedef"),
                                         failText = n != null && n.HideOnFlee ? "Hedef saklandı" : "Hedef kaçtı" });

        /// <summary>Stop him any way: kill, put down, dart, or shoot the gun out of his hand (arm).</summary>
        public Objective Neutralize(NPC n, string text) =>
            AddObjective(new Objective { kind = ObjectiveKind.Neutralize, npc = n, text = text, failText = "Silahlı adam kaçtı" });

        /// <summary>He must survive with a bullet in the given limb (<paramref name="anyLimb"/>: arm or leg).</summary>
        public Objective MustWound(NPC n, BodyZone zone, string text, bool anyLimb = false) =>
            AddObjective(new Objective { kind = ObjectiveKind.Wound, npc = n, zone = zone, anyLimb = anyLimb, text = text,
                                         failText = "Hedef öldü — onu canlı istiyorduk" });

        public Objective MustSedate(NPC n, string text) =>
            AddObjective(new Objective { kind = ObjectiveKind.Sedate, npc = n, text = text, failText = "Hedef öldü — onu canlı istiyorduk" });

        /// <summary>Keep him alive; completes when <paramref name="doneWhen"/> is true, when he reaches safety (escape) or
        /// when the script calls <see cref="Complete"/>.</summary>
        public Objective Protect(NPC n, string text, Func<bool> doneWhen = null) =>
            AddObjective(new Objective { kind = ObjectiveKind.Protect, npc = n, text = text, doneWhen = doneWhen,
                                         failText = (n != null ? n.DisplayName : "Korunan kişi") + " öldürüldü" });

        /// <summary>Shoot a tagged prop out of order (<paramref name="hits"/> bullets, or one explosive round).</summary>
        public Objective DisableProp(string tag, string kind, string text, int hits = 1, Action<Shootable, Vector3> onDisabled = null)
        {
            var o = new Objective { kind = ObjectiveKind.Disable, text = text, failText = "Hedef kaçtı" };
            var sh = MakeShootable(tag, kind, (s, p) =>
            {
                if (s.Disabled) return;
                bool boom = s.LastAmmo == AmmoKind.Explosive;
                if (s.Hits >= hits || boom)
                {
                    s.Disabled = true;
                    FX.Smoke(p);
                    onDisabled?.Invoke(s, p);
                    if (o.State == ObjectiveState.Active) Complete(o);
                }
                else Say("İsabet — " + (hits - s.Hits) + " atış daha", Theme.Accent, "crosshair");
            });
            o.prop = sh;
            return AddObjective(o);
        }

        public Objective Custom(string text, Func<bool> doneWhen, Func<bool> failWhen = null, string failText = null, bool optional = false) =>
            AddObjective(new Objective { kind = ObjectiveKind.Custom, text = text, doneWhen = doneWhen, failWhen = failWhen, failText = failText ?? "Görev başarısız", optional = optional });

        public void Reveal(Objective o)
        {
            if (o == null || !o.hidden) return;
            o.hidden = false;
            o.changedAt = Elapsed;
            Say("YENİ HEDEF: " + o.text, Theme.Accent, o.Icon);
            ObjectivesChanged?.Invoke();
        }

        public void Complete(Objective o)
        {
            if (o == null || o.State != ObjectiveState.Active) return;
            o.State = ObjectiveState.Done;
            o.changedAt = Elapsed;
            o.hidden = false;
            ObjectivesChanged?.Invoke();
            if (!o.optional) Sfx.Play(Sfx.Confirm, 0.7f, 1.2f);
            EvaluateObjectives();
        }

        public void FailObjective(Objective o, string text = null)
        {
            if (o == null || o.State != ObjectiveState.Active) return;
            o.State = ObjectiveState.Failed;
            o.changedAt = Elapsed;
            if (text != null) o.failText = text;
            ObjectivesChanged?.Invoke();
            EvaluateObjectives();
        }

        void PollObjectives()
        {
            foreach (var o in Objectives.ToArray())
            {
                if (o.State != ObjectiveState.Active) continue;
                if (o.failWhen != null && SafeCheck(o.failWhen)) { FailObjective(o); if (State != Phase.Playing) return; continue; }
                if (o.doneWhen != null && !o.hidden && SafeCheck(o.doneWhen)) { Complete(o); if (State != Phase.Playing) return; }
            }
        }

        static bool SafeCheck(Func<bool> f)
        {
            try { return f(); }
            catch (Exception e) { Debug.LogException(e); return false; }
        }

        /// <summary>Done when every non-optional line is done; a failed line fails the contract.</summary>
        void EvaluateObjectives()
        {
            if (State != Phase.Playing || completing) return;
            bool all = Objectives.Exists(o => !o.optional);
            foreach (var o in Objectives)
            {
                if (o.optional) continue;
                if (o.State == ObjectiveState.Failed) { Fail(o.failText ?? "Görev başarısız"); return; }
                if (o.State != ObjectiveState.Done) all = false;
            }
            if (all)
            {
                completing = true;
                // flags that hang on the deciding kill (a moving target, the second of two...): watchers only run while
                // playing, so they get one last look before the curtain
                PollWatchers();
                if (State != Phase.Playing) return;
                if (ExfilNeeded) StartCoroutine(ExfilRoutine());
                else StartCoroutine(CompleteRoutine());
            }
        }

        /// <summary>Lines still open (not optional, not hidden).</summary>
        public int PendingObjectives()
        {
            int n = 0;
            foreach (var o in Objectives) if (!o.optional && o.State == ObjectiveState.Active) n++;
            return n;
        }

        /// <summary>First open line that is about a person (the HUD status line follows him).</summary>
        public Objective FocusObjective
        {
            get
            {
                foreach (var o in Objectives)
                    if (o.Active && !o.optional && o.npc != null && o.kind != ObjectiveKind.Protect) return o;
                foreach (var o in Objectives) if (o.Active && !o.optional) return o;
                return null;
            }
        }

        /// <summary>Where on the body a round lands: the lower edge of the hip box (below the hip joints) is the top of the
        /// thigh, a leg wound.</summary>
        public static BodyZone ZoneOf(BodyPart part, Vector3 point)
        {
            if (part == null) return BodyZone.Torso;
            if (part.zone == BodyZone.Pelvis && point.y < part.transform.position.y - 0.02f) return BodyZone.Leg;
            return part.zone;
        }

        /// <summary>Head, chest and belly kill; an arm or a leg is a wound, whatever the rifle (the take-alive contracts
        /// must work with every rifle, the anti-materiel ones too).</summary>
        static bool Lethal(BodyZone zone, ShotSpec spec) =>
            zone == BodyZone.Head || zone == BodyZone.Torso || zone == BodyZone.Pelvis || spec.ammo == AmmoKind.Explosive;

        /// <summary>V2: a torso round that his body armour stops (light rifle, ordinary rounds).</summary>
        static bool Stopped(NPC victim, BodyZone zone, ShotSpec spec) =>
            victim != null && victim.Armored && zone == BodyZone.Torso && spec.ammo != AmmoKind.ArmorPiercing &&
            spec.ammo != AmmoKind.Explosive && spec.ammo != AmmoKind.Tranquilizer && spec.power < Gear.ArmorPower;

        static bool Lethal(NPC victim, BodyZone zone, ShotSpec spec) => !Stopped(victim, zone, spec) && Lethal(zone, spec);

        /// <summary>Would this hit settle the last open line of the contract? (kill cam)</summary>
        bool IsDecisive(NPC victim, BodyZone zone, ShotSpec spec)
        {
            if (victim == null || victim.Dead) return false;
            bool tranq = spec.ammo == AmmoKind.Tranquilizer;
            bool lethal = !tranq && Lethal(victim, zone, spec);
            Objective resolves = null;
            int open = 0;
            foreach (var o in Objectives)
            {
                if (o.optional || o.State != ObjectiveState.Active) continue;
                if (o.hidden) { open++; continue; }                                    // more of the contract is still to come
                if (o.kind == ObjectiveKind.Protect && o.doneWhen != null) continue;   // completes by itself once the threat is gone
                open++;
                if (o.npc != victim) continue;
                switch (o.kind)
                {
                    case ObjectiveKind.Eliminate: if (lethal) resolves = o; break;
                    case ObjectiveKind.Neutralize: if (tranq || lethal || zone == BodyZone.Leg || zone == BodyZone.Arm) resolves = o; break;
                    case ObjectiveKind.Wound: if (!tranq && !lethal && LimbMatches(o, zone)) resolves = o; break;
                    case ObjectiveKind.Sedate: if (tranq) resolves = o; break;
                }
            }
            return resolves != null && open == 1;
        }

        /// <summary>V3: how the scope's help may pull a shot onto <paramref name="n"/> with this round — not at all (a man
        /// under protection, a Sedate job hit with anything but a dart, a take-alive job hit with a dart or an explosive
        /// round), onto any part, or only onto the limb his Wound line asks for.</summary>
        public AssistZone AssistRule(NPC n, AmmoKind ammo)
        {
            if (n == null || UnderProtection(n)) return AssistZone.None;
            var rule = AssistZone.Any;
            foreach (var o in Objectives)
            {
                if (o.npc != n || o.State != ObjectiveState.Active) continue;
                if (o.kind == ObjectiveKind.Sedate && ammo != AmmoKind.Tranquilizer) return AssistZone.None;
                if (o.kind == ObjectiveKind.Wound)
                {
                    if (ammo == AmmoKind.Explosive || ammo == AmmoKind.Tranquilizer) return AssistZone.None;
                    rule = o.anyLimb ? AssistZone.Limb : (o.zone == BodyZone.Arm ? AssistZone.Arm : AssistZone.Leg);
                }
            }
            return rule;
        }

        /// <summary>Someone the player is keeping alive right now (a protected person, or anyone under an open Protect line).</summary>
        bool UnderProtection(NPC n)
        {
            if (n == null) return false;
            if (n.Protected) return true;
            foreach (var o in Objectives)
                if (o.kind == ObjectiveKind.Protect && o.npc == n && o.State == ObjectiveState.Active) return true;
            return false;
        }

        static bool LimbMatches(Objective o, BodyZone zone) =>
            o.anyLimb ? (zone == BodyZone.Arm || zone == BodyZone.Leg) : zone == o.zone;

        // ------------------------------------------------------------------ shots
        public int AliveTargets()
        {
            int c = 0;
            foreach (var t in Targets) if (t != null && !t.Dead && !t.Escaped) c++;
            return c;
        }

        void OnFired(Projectile p, ShotResult predicted)
        {
            if (State != Phase.Playing) return;
            Shots++;
            if (ReconOpen) { UpdateRecon(); ReconOpen = false; }    // recon ends with the first shot
            // the cover is judged when the trigger is pulled (and the bullet keeps it until it lands)
            p.masked = NoiseMask != null && NoiseMask();
            if (NoiseMask != null && !p.masked) SetFlag("unmasked");
            p.OnImpact = OnImpact;
            // every shot tells the enemy a little more about where the nest is
            Firefight.I?.OnPlayerShot(p.weapon != null && p.weapon.silenced, p.masked, p.spec.ammo == AmmoKind.Tranquilizer);
            // the prediction already follows a walking or riding target to where the bullet meets him
            var victim = predicted.hit ? predicted.Npc : null;
            var part = predicted.Part;
            var zone = ZoneOf(part, predicted.point);
            // the decisive shot of the contract, or the shot that wins a duel with a marksman aiming at the nest
            bool duel = victim != null && victim.AimingAtPlayer && p.spec.ammo != AmmoKind.Tranquilizer && Lethal(victim, zone, p.spec);
            bool save = victim != null && !duel && victim.Aiming && victim.AimVictim != null && !victim.AimVictim.Dead && UnderProtection(victim.AimVictim) &&
                        p.spec.ammo != AmmoKind.Tranquilizer && Lethal(victim, zone, p.spec);
            // a predicted hit on a person lands where the dot showed: over the last metres the bullet is nudged (a hand's
            // breadth at most) onto that spot of his body, so a swinging arm or a stride does not turn it into a miss
            if (victim != null && part != null)
            {
                var col = predicted.collider;
                var aimAt = col != null ? Vector3.Lerp(predicted.bodyPoint, col.bounds.center, 0.3f) : predicted.bodyPoint;
                p.guide = part.transform;
                p.guideLocal = part.transform.InverseTransformPoint(aimAt);
            }
            if (victim != null && Save.KillCam && (IsDecisive(victim, zone, p.spec) || duel || save))
            {
                killCamShot = p;
                bool moving = predicted.lead.magnitude > 0.35f;
                string label = Mathf.RoundToInt(predicted.distance) + " m" + (duel ? "  ·  DÜELLO" : save ? "  ·  SON ANDA" : "");
                if (predicted.walls > 0) label += "  ·  SİPERİN İÇİNDEN";
                else if (predicted.glass > 0) label += "  ·  CAMIN İÇİNDEN";
                else if (moving && !duel && !save) label += "  ·  HAREKETLİ HEDEF";
                game.KillCam.Run(p, victim, label, null, predicted.time, predicted.point);
            }
        }

        /// <summary>Gunmen near <paramref name="p"/> duck (a round landed close, a man fell next to them).</summary>
        public void SuppressNear(Vector3 p, float radius)
        {
            foreach (var n in NPC.All)
            {
                if (n == null || n.Combat == null || n.Dead || n.Incapacitated) continue;
                if ((n.transform.position - p).sqrMagnitude < radius * radius) n.Combat.Suppress(p);
            }
        }

        void OnImpact(Projectile p, ShotResult r)
        {
            // a round that met nobody (a wall, the ground, water, the sky): the handler calls the miss
            if (State == Phase.Playing && (!r.hit || r.Npc == null)) SpotterCall(p, r);
            if (!r.hit || State == Phase.Idle || State == Phase.Done) return;
            if (State == Phase.Playing) SuppressNear(r.point, 3.5f);
            var w = p.weapon;
            var spec = p.spec;
            bool quiet = w != null && w.silenced;
            if (r.water)
            {
                FX.Splash(r.point);                 // plays its own sound
                if (State == Phase.Playing) Noise(r.point, quiet ? 13f : 22f, NoiseKind.Splash);
                if (spec.ammo == AmmoKind.Explosive && State == Phase.Playing) ExplodeRound(r.point + Vector3.up * 0.2f, null);
                return;
            }
            var npc = r.Npc;
            // V2: a round that hits nobody while the job is still open (no trap, no machine): the second one brings the
            // gunmen out; landing within a few metres of a living target counts double
            if (npc == null && State == Phase.Playing && r.collider != null && r.collider.GetComponentInParent<Shootable>() == null &&
                Targets.Exists(t => t != null && !t.Dead && !t.Incapacitated && !t.Escaped))
            {
                bool close = Targets.Exists(t => t != null && !t.Dead && !t.IsHidden && Vector3.Distance(t.transform.position, r.point) < 6f);
                missesOnScene += close ? 2 : 1;
                if (missesOnScene >= 2) Reinforce("miss");
            }
            if (npc != null)
            {
                if (npc.Dead) { FX.Blood(r.point, r.normal, false); return; }
                if (State != Phase.Playing) return;
                Hits++;
                var part = r.Part;
                var zone = ZoneOf(part, r.point);
                if (spec.ammo == AmmoKind.Tranquilizer)
                {
                    npc.Sedate(r.point, p.velocity);
                    return;                                  // a dart makes no sound worth hearing
                }
                if (spec.ammo == AmmoKind.Explosive)
                {
                    ExplodeRound(r.point, npc);
                    return;
                }
                if (Stopped(npc, zone, spec))
                {
                    npc.ArmorStop(r.point, p.velocity);
                    if (!armorTold)
                    {
                        armorTold = true;
                        Say("ZIRH DURDURDU — kafaya nişan al, zırh delici kullan ya da HASAR geliştir", Theme.Guard, "shield");
                    }
                    if (w != null && w.report > 0f && State == Phase.Playing) Noise(r.point, w.report, NoiseKind.Shot, p.masked);
                    return;
                }
                if (Lethal(zone, spec))
                {
                    bool head = zone == BodyZone.Head;
                    if (head) Headshots++;
                    LongestKill = Mathf.Max(LongestKill, r.distance);
                    directKill = true;
                    try { npc.Kill(r.point, p.velocity, part, head); }
                    finally { directKill = false; }
                }
                else npc.Wound(r.point, p.velocity, part, zone);
                // the crack of the shot: people right next to it know there is a shooter
                if (w != null && w.report > 0f && State == Phase.Playing) Noise(r.point, w.report, NoiseKind.Shot, p.masked);
                return;
            }
            bool metal = false, ownSound = false;
            if (r.collider != null)
            {
                var pi = r.collider.GetComponentInParent<PropInfo>();
                if (pi != null && pi.id != null)
                    metal = pi.id.StartsWith("sc_container") || pi.id.StartsWith("sc_sts") || pi.id.StartsWith("sr_drum") || pi.id.StartsWith("ai_") ||
                            pi.id.StartsWith("sc_cargo") || pi.id.Contains("barrel") || pi.id.Contains("forklift") || pi.id.Contains("lamp") || pi.id.Contains("generator");
                if (State == Phase.Playing)
                {
                    var barrel = r.collider.GetComponentInParent<ExplosiveBarrel>();
                    if (barrel != null) barrel.Explode();
                    var sh = r.collider.GetComponentInParent<Shootable>();
                    if (sh != null && !sh.Disabled)
                    {
                        ownSound = sh.OwnSound;
                        sh.Hits++;
                        sh.LastAmmo = spec.ammo;
                        sh.OnShot?.Invoke(sh, r.point);
                    }
                }
            }
            if (spec.ammo == AmmoKind.Explosive && State == Phase.Playing)
            {
                ExplodeRound(r.point + r.normal * 0.1f, null);
                return;
            }
            FX.Dust(r.point, r.normal, metal);
            Sfx.PlayAt(metal ? Sfx.ImpactMetal : Sfx.Impact, r.point, 0.8f);
            // a car alarm already tells everyone what happened: the bullet itself must not count as a second scare;
            // a dart that misses only makes a little tick
            if (!ownSound && State == Phase.Playing) Noise(r.point, spec.ammo == AmmoKind.Tranquilizer ? 5f : (quiet ? 19f : 32f), NoiseKind.Impact, p.masked);
            if (State == Phase.Playing) NearMiss(p.origin, r.point);
            if (DistractPoint.HasValue && Vector3.Distance(DistractPoint.Value, r.point) < 15f) DistractPoint = null;
        }

        /// <summary>Explosive round: a small blast that kills anyone within ~3 m, wrecks machines (shootables count it as
        /// a decisive hit) and sets off fuel drums.</summary>
        void ExplodeRound(Vector3 p, NPC direct)
        {
            FX.ExplosionSmall(p);
            const float radius = 3.2f;
            if (direct != null && !direct.Dead)
            {
                directKill = true;
                try { direct.Kill(direct.ChestPoint, (direct.ChestPoint - game.Sniper.transform.position).normalized * 3f, null, false); }
                finally { directKill = false; }
            }
            foreach (var n in NPC.All.ToArray())
            {
                if (n == null || n.Dead || n.Escaped || n == direct || n.IsHidden) continue;
                var d = n.ChestPoint - p;
                if (d.magnitude < radius) n.Kill(n.ChestPoint, (d.normalized + Vector3.up * 0.5f).normalized * 3f, null, false);
            }
            var done = new HashSet<Shootable>();
            foreach (var c in Physics.OverlapSphere(p, radius, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                var sh = c.GetComponentInParent<Shootable>();
                if (sh != null && !sh.Disabled && done.Add(sh))
                {
                    sh.Hits += 3;
                    sh.LastAmmo = AmmoKind.Explosive;
                    sh.OnShot?.Invoke(sh, p);
                }
                var barrel = c.GetComponentInParent<ExplosiveBarrel>();
                if (barrel != null) barrel.Explode();
            }
            OnExplosion(p, radius);
        }

        /// <summary>
        /// A sound: everyone within earshot hears it. Targets walk out to check it (the player's chance to lure them
        /// out of cover) and run when it happens again; guards investigate, then radio; bystanders glance or panic.
        /// </summary>
        public void Noise(Vector3 p, float radius, NoiseKind kind) => Noise(p, radius, kind, NoiseMask != null && NoiseMask());

        /// <summary><paramref name="masked"/>: a louder sound covers it (a shot fired under the fireworks).</summary>
        public void Noise(Vector3 p, float radius, NoiseKind kind, bool masked)
        {
            if ((kind == NoiseKind.Shot || kind == NoiseKind.Impact) && masked) radius *= 0.3f;
            foreach (var n in NPC.All.ToArray())
            {
                if (n == null || n.Dead || n.Escaped) continue;
                float d = Vector3.Distance(n.transform.position, p);
                bool listener = n.IsTarget || n.Role == Role.Decoy || n.Role == Role.Guard || n.Role == Role.Bodyguard;
                if (d > radius * (listener || kind == NoiseKind.Shot ? 1f : 0.65f)) continue;
                n.Hear(p, kind);
            }
        }

        void OnScanned(NPC n)
        {
            if (n == null || State != Phase.Playing) return;
            if (n.HiddenTarget) { Say("SİLAHLI ADAM BULUNDU: " + UIK.Up(n.DisplayName), Theme.Danger, "alert"); Sfx.Play(Sfx.Alert, 0.6f); }
            else if (IdMode) return;                                   // the player compares him with the dossier himself
            else if (n.IsTarget) Say("HEDEF DOĞRULANDI: " + UIK.Up(n.DisplayName), Theme.Danger, "target");
            else if (n.Role == Role.Decoy) Say(string.IsNullOrEmpty(n.ScanNote) ? "Hedef değil — masum konuk" : n.ScanNote, Theme.Info, "civilian");
        }

        // ------------------------------------------------------------------ V2: identifying the target
        /// <summary>From the 5th contract the player recognises the target himself: the scan shows a face, not a name.</summary>
        public bool IdMode => Def != null && game != null && game.Sniper != null &&
                              (Def.markTargets >= 0 ? Def.markTargets == 0 : (game.Sniper.Diff.tier >= 1 || Veteran));
        bool IdRequired => IdMode;
        /// <summary>A target still has to be recognised: strangers are faces, shooting the wrong one ends the contract.</summary>
        public bool PendingId => IdMode && Targets.Exists(t => t != null && t.NeedsId && !t.Confirmed && !t.Dead && !t.Escaped);

        /// <summary>The player hurt or put to sleep someone while the target was still unknown: if it was the target he is
        /// found, if not it counts like a wrong call.</summary>
        bool JudgeHit(NPC n)
        {
            if (!PendingId || n == null) return false;
            if (n.IsTarget && n.NeedsId) { if (!n.Confirmed) { n.Confirmed = true; ObjectivesChanged?.Invoke(); } return false; }
            if (n.Protected || n.HiddenTarget || n.Role == Role.Guard || n.Role == Role.Bodyguard || n.Armed) return false;
            WrongTags++;
            if (WrongTags >= 3) { Fail("Üç yanlış teşhis — sözleşme iptal"); return true; }
            Say(WrongTags == 1 ? "KONTROL: O değildi — masum birini vurdun!" : "KONTROL: Yine yanlış kişi. Bir daha olursa iptal.", Theme.Danger, "alert");
            return true;
        }
        public int WrongTags { get; private set; }
        float tagLockUntil;
        public bool TagLocked => Time.time < tagLockUntil;

        /// <summary>Could the player tag this person as the target now (scanned, a stranger, not yet judged)?</summary>
        public bool CanTag(NPC n) => IdMode && State == Phase.Playing && n != null && !n.Dead && !n.Escaped && n.Identified &&
                                     !n.Confirmed && !n.TaggedWrong && !n.Protected && !n.HiddenTarget &&
                                     n.Role != Role.Guard && n.Role != Role.Bodyguard && !(n.Armed && !n.IsTarget) &&
                                     Targets.Exists(t => t != null && t.NeedsId && !t.Confirmed && !t.Dead && !t.Escaped);

        /// <summary>The player says "that's him". Right: he is marked. Wrong: the handler says no, the tagging locks for a
        /// few seconds and the fee drops; the third wrong call ends the contract.</summary>
        public void Tag(NPC n)
        {
            if (!CanTag(n) || TagLocked) return;
            if (n.IsTarget && n.NeedsId)
            {
                n.Confirmed = true;
                Say("KİMLİK DOĞRULANDI: " + UIK.Up(n.DisplayName), Theme.Danger, "target");
                Sfx.Play(Sfx.Confirm, 0.8f);
                ObjectivesChanged?.Invoke();
                return;
            }
            n.TaggedWrong = true;
            WrongTags++;
            tagLockUntil = Time.time + 6f;
            Sfx.Play(Sfx.Back, 0.8f);
            if (WrongTags >= 3) { Fail("Üç yanlış teşhis — sözleşme iptal"); return; }
            Say(WrongTags == 1 ? "KONTROL: Hayır, o değil. Dosyaya tekrar bak." : "KONTROL: O da değil. Bir daha yanılırsan iptal.", Theme.Danger, "alert");
        }

        // ------------------------------------------------------------------ V2: where the player shoots from
        /// <summary>The nest picked in the briefing for the next contract (null: the main one).</summary>
        public static string PreferredNest;
        public List<LayoutNest> Nests { get; private set; } = new List<LayoutNest>();
        public LayoutNest Nest { get; private set; }
        public int Relocations { get; private set; }
        public const int MaxRelocations = 2;
        public bool Relocating { get; private set; }
        /// <summary>Moving to another place: seconds it takes (the world goes on meanwhile).</summary>
        public event Action<LayoutNest, float> RelocateStarted;
        public event Action<LayoutNest> RelocateDone;
        Transform nestFrame;

        public bool CanRelocate => State == Phase.Playing && !Relocating && Relocations < MaxRelocations && Nests.Count > 1 &&
                                   !KillCam.Active && !Cinematic.Active && (Firefight.I == null || !Firefight.I.Dead);

        /// <summary>Packs up and moves to another place: the screen goes dark for a moment (about 2 s in all — the walk
        /// used to take 5-11 s and players found it far too long), the contract carries on meanwhile; at the new place
        /// nobody knows where the shooter is.</summary>
        public void Relocate(LayoutNest to)
        {
            if (!CanRelocate || to == null || to == Nest) return;
            StartCoroutine(RelocateRoutine(to));
        }

        IEnumerator RelocateRoutine(LayoutNest to)
        {
            Relocating = true;
            Relocations++;
            var sn = game.Sniper;
            sn.SetScoped(false);
            sn.InputEnabled = false;
            // dark for 1.4 s; with the fades the move takes ~2 s whatever the distance
            const float secs = 1.4f;
            RelocateStarted?.Invoke(to, secs);
            Sfx.Play(Sfx.Equip, 0.8f);
            float t = 0f;
            while (t < secs)
            {
                if (State != Phase.Playing) { Relocating = false; yield break; }
                if (!KillCam.Active) t += Time.deltaTime;
                yield return null;
            }
            Nest = to;
            if (nestFrame != null) Destroy(nestFrame.gameObject);
            nestFrame = NestFrame.Build(root, Nest);
            sn.Place(Nest);
            sn.ResetPose();
            Firefight.I?.Relocated();
            sn.InputEnabled = true;
            Relocating = false;
            RelocateDone?.Invoke(to);
            Say("YENİ YER: " + UIK.Up(to.name), Theme.Info, "map");
        }

        /// <summary>The player's bullet went straight into this person (the wrong-person rule needs to know).</summary>
        bool directKill;

        /// <summary>Recon drone (gadget): everybody near the contract area is identified and marked for a while.</summary>
        public bool UseDrone()
        {
            if (State != Phase.Playing || DroneUsed || !Gear.HasGadget("drone")) return false;
            DroneUsed = true;
            DroneUntil = Time.time + 15f;
            var center = !string.IsNullOrEmpty(Def.focus) && Level.I.HasMarker(Def.focus) ? Level.I.Marker(Def.focus) : game.Sniper.transform.position;
            int found = 0;
            foreach (var n in NPC.All)
            {
                if (n == null || n.Dead || n.Escaped) continue;
                if (Vector3.Distance(n.transform.position, center) > 160f) continue;
                if (!n.Identified) { n.Identified = true; found++; }
                if (n.IsTarget && n.NeedsId) n.Confirmed = true;     // the drone's camera recognises the face
            }
            Sfx.Play(Sfx.Drone, 0.7f);
            Say("Dron bölgeyi taradı — " + found + " kişi tanımlandı", Theme.Info, "scan");
            foreach (var n in NPC.All)
                if (n != null && n.HiddenTarget && n.Identified && !n.Dead) { Say("Dron silahlı birini işaretledi!", Theme.Danger, "alert"); break; }
            return true;
        }

        // ------------------------------------------------------------------ callbacks from NPCs
        public void OnKilled(NPC n, bool headshot, BodyZone zone, bool byPlayer = true)
        {
            if (State != Phase.Playing && State != Phase.Ending) return;
            Killed?.Invoke(n, headshot);
            if (byPlayer) Kills++;
            if (byPlayer && n != null && n.Armored) ArmoredKilled++;
            if (State == Phase.Playing) SuppressNear(n.transform.position, 6f);
            bool protectedLost = false;
            foreach (var o in Objectives)
            {
                if (o.npc != n || o.State != ObjectiveState.Active) continue;
                switch (o.kind)
                {
                    case ObjectiveKind.Eliminate:
                    case ObjectiveKind.Neutralize:
                        o.State = ObjectiveState.Done; o.changedAt = Elapsed; break;
                    case ObjectiveKind.Wound:
                    case ObjectiveKind.Sedate:
                        o.State = ObjectiveState.Failed; o.changedAt = Elapsed; break;
                    case ObjectiveKind.Protect:
                        o.State = ObjectiveState.Failed; o.changedAt = Elapsed; protectedLost = true; break;
                }
            }
            ObjectivesChanged?.Invoke();
            if (n.IsTarget && !byPlayer)
            {
                Say(n.DisplayName + " vuruldu!", Theme.Danger, "alert");
            }
            else if (n.IsTarget)
            {
                TargetsKilled++;
                if (!headshot) AllTargetHeadshots = false;
                // V2: his people do not just stand there — the armed ones near him go for cover and look for the shooter
                // (from the 2nd contract): with the nest found, the job ends with getting out under fire
                if (Def != null && Def.number >= 2 && State == Phase.Playing)
                {
                    var nest = game != null && game.Sniper != null ? game.Sniper.transform.position : n.transform.position;
                    int woke = 0;
                    foreach (var g in NPC.All)
                        if (g != null && g != n && !g.Dead && !g.Incapacitated && g.Combat != null && g.Combat.CanFight &&
                            Vector3.Distance(g.transform.position, n.transform.position) < 60f) { g.Combat.Engage(nest); woke++; }
                    bool quiet = game != null && game.Sniper != null && game.Sniper.Weapon != null && game.Sniper.Weapon.silenced;
                    if (woke > 0 && Firefight.I != null) Firefight.I.AddHeat(quiet ? 0.15f : 0.35f);
                    // from the 5th contract nobody important dies unanswered: with no gunman near him, his people send some
                    else if (Def.number >= 5 && !quiet && TargetsKilled >= Targets.Count) Reinforce("kill");
                }
                if (n.HiddenTarget) Say(headshot ? "SUİKASTÇI ETKİSİZ — KAFADAN" : "SUİKASTÇI ETKİSİZ", Theme.Danger, headshot ? "headshot" : "skull");
                else Say(headshot ? "KAFADAN VURUŞ — HEDEF ETKİSİZ" : "HEDEF ETKİSİZ", Theme.Danger, headshot ? "headshot" : "skull");
            }
            else if (n.Protected)
            {
                if (!protectedLost) Say(n.DisplayName + " öldü!", Theme.Danger, "alert");
            }
            else if (n.Role == Role.Guard || n.Role == Role.Bodyguard)
            {
                if (byPlayer && !n.IsLookout) GuardsKilled++;           // a lookout is fair game for the guards' star
                Say(headshot ? "Koruma etkisiz — kafadan" : "Koruma etkisiz", Theme.Guard, "guard");
            }
            else if (byPlayer && directKill && PendingId && !n.Armed)
            {
                // V2: shooting someone who is not the target is the end of the contract
                CiviliansKilled++;
                Fail(n.Role == Role.Decoy ? "Yanlış kişi — o bir dublördü" : "Yanlış kişi — masum birini vurdun");
                return;
            }
            else if (byPlayer)
            {
                CiviliansKilled++;
                if (CiviliansKilled > Def.maxCivilians) { Fail("Çok fazla sivil kayıp"); return; }
                Say("SİVİL KAYIP! Bir daha olursa görev iptal.", Theme.Danger, "alert");
            }
            EvaluateObjectives();
            if (State == Phase.Playing && n.IsTarget && PendingObjectives() > 0)
            {
                int left = 0;
                foreach (var o in Objectives) if (o.Active && !o.optional && o.npc != null && o.npc.IsTarget) left++;
                if (left > 0) Say(left + " hedef kaldı", Theme.Accent, "target");
            }
        }

        /// <summary>A non-lethal hit (limb) by the player.</summary>
        public void OnWounded(NPC n, BodyZone zone)
        {
            if (State != Phase.Playing) return;
            Wounded?.Invoke(n, zone);
            bool resolved = false;
            foreach (var o in Objectives)
            {
                if (o.npc != n || o.State != ObjectiveState.Active) continue;
                if (o.kind == ObjectiveKind.Wound && LimbMatches(o, zone)) { o.State = ObjectiveState.Done; o.changedAt = Elapsed; resolved = true; }
                else if (o.kind == ObjectiveKind.Wound) Say(o.zone == BodyZone.Leg ? "Yanlış yer — BACAĞINDAN vurmalısın" : "Yanlış yer — KOLUNDAN vurmalısın", Theme.Accent, "hand");
                else if (o.kind == ObjectiveKind.Neutralize) { o.State = ObjectiveState.Done; o.changedAt = Elapsed; resolved = true; }
            }
            if (resolved) { ObjectivesChanged?.Invoke(); Sfx.Play(Sfx.Confirm, 0.7f, 1.2f); }
            if (n.Protected && n.Down) FailWalkingProtect(n, n.DisplayName + " yaralandı — artık yürüyemiyor");
            if (State != Phase.Playing) return;
            if (JudgeHit(n)) { CiviliansHurt++; EvaluateObjectives(); return; }
            if (n.HiddenTarget && zone == BodyZone.Arm) Say("Silahını düşürdü!", Theme.Success, "hand");
            else if (n.IsTarget) Say(zone == BodyZone.Leg ? "HEDEF YERE DÜŞTÜ" : "HEDEF YARALANDI", resolved ? Theme.Success : Theme.Danger, "hand");
            else if (n.Role == Role.Guard || n.Role == Role.Bodyguard) { Say("Koruma yaralandı — hâlâ telsize uzanabilir", Theme.Guard, "guard"); OnGuardAlert(n, true); }
            else if (!n.Protected)
            {
                CiviliansHurt++;
                Say("Masum biri yaralandı!", Theme.Danger, "alert");
            }
            EvaluateObjectives();
        }

        public void OnSedated(NPC n)
        {
            if (State != Phase.Playing) return;
            Wounded?.Invoke(n, BodyZone.Torso);
            bool resolved = false;
            foreach (var o in Objectives)
            {
                if (o.npc != n || o.State != ObjectiveState.Active) continue;
                // taken alive: a dart does the job of a limb shot too
                if (o.kind == ObjectiveKind.Sedate || o.kind == ObjectiveKind.Neutralize || o.kind == ObjectiveKind.Wound)
                { o.State = ObjectiveState.Done; o.changedAt = Elapsed; resolved = true; }
            }
            if (resolved) { ObjectivesChanged?.Invoke(); Sfx.Play(Sfx.Confirm, 0.7f, 1.2f); }
            if (n.Protected) FailWalkingProtect(n, n.DisplayName + " bayıldı — güvenli yere ulaşamaz");
            if (State != Phase.Playing) return;
            if (JudgeHit(n)) { CiviliansHurt++; EvaluateObjectives(); return; }
            if (n.IsTarget) Say("İĞNE İSABET — hedef birkaç saniyede bayılacak", resolved ? Theme.Success : Theme.Info, "breath");
            else if (n.Role == Role.Guard || n.Role == Role.Bodyguard) Say("Koruma bayılıyor", Theme.Guard, "breath");
            else if (!n.Protected) { CiviliansHurt++; Say("Masum birini bayılttın", Theme.Accent, "civilian"); }
            EvaluateObjectives();
        }

        /// <summary>A protected person who has to walk to safety (Protect line without its own completion) can't anymore.</summary>
        void FailWalkingProtect(NPC n, string text)
        {
            foreach (var o in Objectives)
                if (o.npc == n && o.kind == ObjectiveKind.Protect && o.State == ObjectiveState.Active && !o.hidden && (o.doneWhen == null || o.walksToSafety))
                {
                    FailObjective(o, text);
                    return;
                }
        }

        /// <summary>A hitman drew his gun: he reveals himself and the clock starts.</summary>
        float nextAimWarning;

        public void OnAttackerAiming(NPC n)
        {
            if (State != Phase.Playing || n == null) return;
            n.Identified = true;
            if (n.AimingAtPlayer)
            {
                SetFlag("spotted");
                // the first aim of a duel gets the warning; his quick re-aims after each shot only the HUD ring
                if (Time.time >= nextAimWarning)
                {
                    Say("KARŞI NİŞANCI SANA NİŞAN ALDI — ÖNCE SEN VUR!", Theme.Danger, "alert");
                    Sfx.Play(Sfx.Alert, 0.9f, 1.15f);
                    Sfx.Play(Sfx.Heartbeat, 0.7f);
                }
                nextAimWarning = Time.time + 12f;
                return;
            }
            // the first time in a contract the line says it; after that the red ring and "! NİŞAN ALIYOR" on his marker
            if (!Flag("aimed")) Say("SİLAHLI ADAM NİŞAN ALIYOR — ŞİMDİ VUR!", Theme.Danger, "alert");
            SetFlag("aimed");
            Sfx.Play(Sfx.Alert, 0.8f);
        }

        /// <summary>The sniper nest itself: where enemy marksmen aim.</summary>
        public Vector3 NestPoint => game.Sniper.transform.position;

        /// <summary>The player has been shot (an enemy marksman fired first): the crack, the scope jolts, the contract
        /// is lost.</summary>
        /// <summary>An enemy marksman fires at the nest from <paramref name="muzzle"/>: a heavy round that hurts badly
        /// (it can kill a wounded player) unless he is down behind the parapet. True when it hit.</summary>
        public bool PlayerShot(NPC by, Vector3 muzzle, string reason, float damage = 70f)
        {
            if (State != Phase.Playing || Firefight.I == null) return false;
            bool hit = Firefight.I.HeavyRound(by, muzzle, damage, reason);
            if (hit) PlayerHit?.Invoke();
            return hit;
        }

        /// <summary>A lookout saw the nest and reaches for his radio: stop him in time or every gunman knows where the
        /// player is (and the alarm goes up).</summary>
        public void SpotterReport(NPC spotter, float seconds)
        {
            if (State != Phase.Playing || spotter == null) return;
            SetFlag("spotted");
            Sfx.Play(Sfx.Alert, 0.85f, 1.1f);
            if (Alarm || AlarmCountdown > 0f)
            {
                // the radio is busy already: he passes on the position a moment later
                Say("GÖZCÜ SENİ GÖRDÜ!", Theme.Danger, "eye");
                After(seconds * 0.7f, () => { if (spotter != null && !spotter.Dead && !spotter.Incapacitated) Firefight.I?.SetLocated(true); });
                return;
            }
            AlarmGuard = spotter;
            AlarmCountdown = seconds;
            alarmIsSpotting = true;
            Say("GÖZCÜ SENİ GÖRDÜ — telsize uzanıyor, onu durdur!", Theme.Danger, "eye");
        }

        bool alarmIsSpotting;

        /// <summary>Raised when an enemy marksman hits the player (HUD: red flash).</summary>
        public event Action PlayerHit;

        /// <summary>A short live shot of an event (limo at the steps, helicopter landing), see <see cref="KillCam.Cutaway"/>.</summary>
        public void Cutaway(Func<Vector3> focus, Vector3 from, Vector3 to, float seconds, string label)
        {
            if (State != Phase.Playing || game.KillCam == null || !Save.KillCam) return;
            game.KillCam.Cutaway(focus, from, to, seconds, label);
        }

        /// <summary>Default shot of an armed attacker: muzzle flash, report, the victim falls.</summary>
        public void AttackerFires(NPC attacker)
        {
            if (attacker == null || attacker.AimVictim == null) return;
            var v = attacker.AimVictim;
            var g = attacker.Gun;
            var from = g != null ? g.Muzzle : attacker.HandPoint;
            var to = v.ChestPoint;
            FX.MuzzleFlashWorld(from, (to - from).normalized);
            CombatFX.MuzzleFlash(from, to - from, 0.8f);
            CombatFX.Tracer(from, to, 300f, 0.3f);
            Sfx.PlayAt(g != null ? g.spec.Report : Sfx.Pistol, from, 1f, Vector3.Distance(from, NestPoint) / 340f);
            if (!v.Dead) v.Kill(to, (to - from).normalized * 2f, null, false, false);
            Noise(from, 40f, NoiseKind.Explosion);
        }

        public void OnBodySeen(NPC watcher, NPC body)
        {
            if (State != Phase.Playing || watcher == null) return;
            if (watcher.Role == Role.Guard || watcher.Role == Role.Bodyguard) OnGuardAlert(watcher, true);
        }

        public void OnGuardAlert(NPC guard, bool sawBody)
        {
            if (State != Phase.Playing || Alarm || AlarmCountdown > 0 || guard == null || guard.Dead || guard.Sedated || guard.Scripted || guard.NoRadio) return;
            AlarmGuard = guard;
            AlarmCountdown = sawBody ? 3.0f : 4.5f;
            alarmIsSpotting = false;
            Say("Koruma telsize uzanıyor — onu durdur!", Theme.Guard, "alert");
        }

        /// <summary>A lookout got his call through: every gunman knows where the nest is (no general alarm).</summary>
        void LookoutCalled()
        {
            AlarmCountdown = -1;
            AlarmGuard = null;
            Firefight.I?.SetLocated(true);
            Say("GÖZCÜ YERİNİ BİLDİRDİ!", Theme.Danger, "radio");
        }

        public void RaiseAlarm()
        {
            if (Alarm || State != Phase.Playing) return;
            Alarm = true;
            AlarmEver = true;
            AlarmCountdown = -1;
            Sfx.SetAlarm(true);
            Say("ALARM VERİLDİ — HEDEF KAÇIYOR", Theme.Danger, "alert");
            Reinforce("alarm");
            var src = AlarmGuard != null ? AlarmGuard.transform.position : game.Sniper.transform.position;
            foreach (var n in NPC.All.ToArray())
            {
                if (n == null || n.Dead || n.Escaped || n.Incapacitated) continue;
                // hidden targets bolt, except the ones the contract will bring out itself (riding in, still aboard, on the stairs)
                if (n.IsHidden) { if (n.IsTarget && !n.HiddenTarget && !n.MarkWhenHidden) n.ForceEscape(); continue; }
                if (n.Aiming) continue;
                if (n.IsTarget || n.Role == Role.Guard || n.Role == Role.Bodyguard || UnityEngine.Random.value < 0.6f) n.React(src, false);
            }
        }

        public void OnTargetSpooked(NPC t)
        {
            if (State != Phase.Playing || t == null || !t.IsTarget || t.HiddenTarget) return;
            Say("Hedef tedirgin oldu ve kaçıyor!", Theme.Danger, "run");
        }

        static readonly float[] FleeDash = { 22f, 12f, 6f };

        /// <summary>Where a panicking civilian runs: the closest exit that does not lead towards the danger and can be
        /// reached on foot in a straight line, otherwise a short dash away. False = nowhere to run.</summary>
        public bool TryFleeTarget(Vector3 from, Vector3 danger, out Vector3 dest)
        {
            dest = from;
            var away = from - danger; away.y = 0;
            var awayDir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            var cands = new List<KeyValuePair<float, Vector3>>();
            for (int i = 0; i < 10; i++)
            {
                string m = "exit_" + i;
                if (!Level.I.HasMarker(m)) continue;
                var p = Level.I.Marker(m);
                var to = p - from; to.y = 0;
                float d = to.magnitude;
                if (d > 140f || d < 2f) continue;
                float toward = Vector3.Dot(to / d, -awayDir);          // prefer close exits that do not lead towards the danger
                cands.Add(new KeyValuePair<float, Vector3>(d * (1.5f + toward), p));
            }
            cands.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < cands.Count && i < 3; i++)
            {
                var p = cands[i].Value;
                if (Level.WalkableLine(from, p, out float ey)) { dest = new Vector3(p.x, ey, p.z); return true; }
            }
            foreach (float dist in FleeDash)
            {
                var p = from + awayDir * dist;
                if (Level.WalkableLine(from, p, out float ey)) { dest = new Vector3(p.x, ey, p.z); return true; }
            }
            return false;
        }

        public void OnEscaped(NPC n)
        {
            if (n == null || !n.Escaped || State != Phase.Playing) return;
            // a protected person reaching safety completes his line
            if (n.Protected)
            {
                foreach (var o in Objectives)
                    if (o.npc == n && o.kind == ObjectiveKind.Protect && o.State == ObjectiveState.Active) { Complete(o); return; }
                return;
            }
            if (!n.IsTarget) return;
            if (n.HideOnFlee && n.SecondChance && !n.UsedSecondChance && !Alarm)
            {
                StartCoroutine(SecondChanceRoutine(n));
                return;
            }
            string text = !string.IsNullOrEmpty(n.EscapeText) ? n.EscapeText : (n.HideOnFlee ? "Hedef saklandığı yerden çıkmadı" : "Hedef kaçtı");
            bool anyLine = false;
            foreach (var o in Objectives)
            {
                if (o.npc != n || o.State != ObjectiveState.Active || o.optional) continue;
                anyLine = true;
                o.State = ObjectiveState.Failed;
                o.failText = text;
            }
            if (anyLine || Objectives.Count == 0) Fail(text, n.EscapeFailDelay);
        }

        /// <summary>The target hid inside: after a tense wait he peeks out once more to see if it is safe.</summary>
        IEnumerator SecondChanceRoutine(NPC n)
        {
            n.UsedSecondChance = true;
            Say("Hedef içeri saklandı — bekle, bakmak için tekrar çıkabilir", Theme.Accent, "eye");
            float t = 0f;
            while (t < 10f)
            {
                if (State != Phase.Playing || n == null) yield break;
                if (Alarm) { Fail("Hedef saklandığı yerden çıkmadı"); yield break; }
                t += Time.deltaTime;
                yield return null;
            }
            if (n == null || n.Dead) yield break;
            n.Reappear();
            // look out the way he came in from
            var flee = n.FleePath;
            float yaw = flee != null && flee.Length >= 2 ? Yaw(flee[flee.Length - 1], flee[flee.Length - 2]) : n.transform.eulerAngles.y + 180f;
            Say("Hedef kapıda göründü — şimdi!", Theme.Danger, "target");
            n.SetPlan(Act.IdleFacing("idle_look", yaw, 3.2f), Act.IdleFacing("phone_talk", yaw, 4.5f), Act.Escape());
        }

        public void OnExplosion(Vector3 p, float radius = 7f)
        {
            AlarmEver = true;                     // loud: no "silent job" bonus, even when it ended the contract
            Noise(p, radius * 5f, NoiseKind.Explosion);
            if (!Alarm && State == Phase.Playing) { AlarmGuard = null; RaiseAlarm(); }
        }

        // ------------------------------------------------------------------ visibility, hints, interactive props
        /// <summary>
        /// Keeps track of whether each target can be seen from the sniper nest. When the player keeps looking at a
        /// target hidden behind cover, a hint suggests a distraction shot and the HUD marks a good spot for it.
        /// </summary>
        void UpdateVisibility(float dt)
        {
            if (Time.time < nextVisCheck || KillCam.Active) return;
            float step = 0.25f;
            nextVisCheck = Time.time + step;
            var cam = game.Cam;
            if (cam == null) return;
            var eye = cam.transform.position;
            foreach (var t in Targets)
            {
                if (t == null || t.Dead || t.Escaped) continue;
                bool vis = !t.IsHidden && (!Physics.Linecast(eye, t.AimPoint, Layers.WorldMask, QueryTriggerInteraction.Ignore) ||
                                           !Physics.Linecast(eye, t.ChestPoint, Layers.WorldMask, QueryTriggerInteraction.Ignore));
                t.VisibleToPlayer = vis;
                t.HiddenTime = vis ? 0f : t.HiddenTime + step;
                if (vis || t.IsHidden || t.HiddenTarget || t.Panicking || t.Investigating || t.Incapacitated || Elapsed < nextHint || t.HiddenTime < 6f) continue;
                // is the player actually looking for this target?
                var to = t.ChestPoint - eye;
                float ang = Vector3.Angle(cam.transform.forward, to);
                if (ang > Mathf.Max(8f, cam.fieldOfView * 0.6f)) continue;
                ShowCoverHint(t, eye);
            }
        }

        void ShowCoverHint(NPC t, Vector3 eye)
        {
            if (t != null && t.NeedsId && !t.Confirmed) return;           // it would point at the unknown target
            nextHint = Elapsed + 30f;
            var spot = DistractSpotFor(t, eye);
            DistractPoint = spot;
            distractUntil = Elapsed + 16f;
            string text = !string.IsNullOrEmpty(Def.coverHint) ? Def.coverHint
                : "Hedef siperin arkasında, görünmüyor. Yakınına ateş et: sesi duyunca bakmak için açığa çıkar. Bir daha ateş edersen kaçar — o sırada vur.";
            Tip?.Invoke(text, 10f);
        }

        /// <summary>A spot the player can shoot that will pull the target into view (authored route end, or searched).</summary>
        Vector3? DistractSpotFor(NPC t, Vector3 eye)
        {
            if (t.DistractSpot.HasValue) return t.DistractSpot.Value;
            if (t.InvestigatePath != null && t.InvestigatePath.Length > 0)
            {
                var e = t.InvestigatePath[t.InvestigatePath.Length - 1];
                return e + (e - t.transform.position).normalized * 4f;
            }
            var from = t.transform.position;
            foreach (float r in new[] { 6f, 10f, 14f, 18f })
            {
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2f / 16f;
                    var c = from + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * r;
                    if (!Physics.Raycast(c + Vector3.up * 2f, Vector3.down, out var h, 4f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    var g = h.point;
                    if (Physics.Linecast(eye, g + Vector3.up * 0.3f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;      // player can hit it
                    if (Physics.Linecast(eye, g + Vector3.up * 1.35f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;     // target would be seen there
                    var walkTo = from + (g - from) * 0.8f;
                    if (!Level.WalkableLine(from, walkTo, out _)) continue;
                    return g;
                }
            }
            return null;
        }

        /// <summary>Makes a tagged prop react to bullets (car alarm, engine, light...).</summary>
        /// <param name="ownSound">the prop makes its own noise when hit (a car alarm): no bullet-impact noise on top</param>
        public Shootable MakeShootable(string tag, string kind, Action<Shootable, Vector3> onShot, bool ownSound = false)
        {
            var go = Level.I.Tagged(tag);
            if (go == null) return null;
            var s = go.GetComponent<Shootable>();
            if (s == null) s = go.AddComponent<Shootable>();
            s.kind = kind;
            s.Disabled = false;
            s.Hits = 0;
            s.OwnSound = ownSound;
            s.LastAmmo = AmmoKind.Standard;
            s.OnShot = onShot;
            return s;
        }

        /// <summary>Car alarm: loud two-tone horn + blinking indicators; owners and guards come to look.</summary>
        public void CarAlarm(GameObject car, float seconds)
        {
            if (car == null) return;
            var b = car.GetComponent<CarAlarmFX>();
            if (b == null) b = car.AddComponent<CarAlarmFX>();
            b.Run(seconds);
            Noise(car.transform.position, 75f, NoiseKind.Car);
        }

        public void StopCarAlarm(GameObject car)
        {
            if (car == null) return;
            var b = car.GetComponent<CarAlarmFX>();
            if (b != null) b.Stop();
        }

        /// <summary>A fuel drum that explodes when shot (kills whoever stands next to it).</summary>
        public void MakeExplosive(string tag, float radius, Color tint)
        {
            var go = Level.I.Tagged(tag);
            if (go == null) return;
            var e = go.GetComponent<ExplosiveBarrel>();
            if (e == null) e = go.AddComponent<ExplosiveBarrel>();
            e.exploded = false;
            e.armed = true;
            e.radius = radius;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", tint);
                r.SetPropertyBlock(mpb);
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = true;
        }

        /// <summary>This run replayed a contract already finished (it paid the replay share).</summary>
        public bool WasReplay { get; private set; }

        /// <summary>Analytics: how the contract ended (the progression funnel, difficulty and economy reports).</summary>
        void Report(bool success)
        {
            Analytics.Event("level_end", "level_name", Def.id, "success", success ? 1 : 0);
            if (success)
            {
                Analytics.Event("contract_complete", "contract_id", Def.id, "contract_no", Def.number, "attempt", Attempt,
                                "grade", Grade, "stars", Stars, "time_s", Mathf.RoundToInt(Elapsed), "shots", Shots, "hits", Hits,
                                "headshots", Headshots, "longest_m", Mathf.RoundToInt(LongestKill), "alarm", AlarmEver ? 1 : 0,
                                "cash", CashEarned, "xp", XpEarned, "gems", GemsEarned, "replay", WasReplay ? 1 : 0, "veteran", Veteran ? 1 : 0);
                // the first clear of each contract: the campaign funnel (and Meta's "reached contract N" optimisation)
                bool firstClear = !Save.Flag("first_" + Def.id);
                Review.OnContractEnd(Def, true, firstClear && !WasReplay, Stars);
                if (firstClear)
                {
                    Save.SetFlag("first_" + Def.id);
                    Analytics.Event("contract_first_clear", "contract_no", Def.number, "attempts", Attempt);
                    Analytics.MetaEvent("contract_" + Def.number.ToString("00"));
                }
            }
            else
                Analytics.Event("contract_fail", "contract_id", Def.id, "contract_no", Def.number, "attempt", Attempt,
                                "reason", FailReason ?? "", "time_s", Mathf.RoundToInt(Elapsed), "shots", Shots, "hits", Hits,
                                "targets_down", TargetsKilled, "alarm", AlarmEver ? 1 : 0);
            Analytics.RefreshUser();
        }

        // ------------------------------------------------------------------ ending
        public void Fail(string reason, float delay = 1.4f)
        {
            if (State != Phase.Playing) return;
            State = Phase.Ending;
            FailReason = reason;
            Success = false;
            StartCoroutine(FinishRoutine(false, Mathf.Max(0.5f, delay)));
        }

        // ------------------------------------------------------------------ V2: a miss has a price, so does being found
        public int NearMisses { get; private set; }

        /// <summary>A bullet that went past a target (within a metre and a half of him) without touching him: from the
        /// 5th contract he knows he is being shot at and runs for it.</summary>
        static string Fm(string src, params object[] args)
        {
            try { return string.Format(Loc.Culture, Loc.Tr(src), args); }
            catch { return string.Format(Loc.Culture, src, args); }
        }

        /// <summary>
        /// The handler calls the miss the way a spotter talks his shooter on: how far off it went, measured against the
        /// nearest part of the man it was meant for (from the scope's own prediction at the trigger), and the correction
        /// that would have put it in — the range dial when it fell short or flew over, the wind when it was carried, the
        /// lead when a walker left it behind, a held breath when it was the shake. A shot his cover took is called too.
        /// </summary>
        void SpotterCall(Projectile p, ShotResult landed)
        {
            if (p == null || game == null || game.Sniper == null || Def == null) return;
            if (Time.time - lastCallAt < 2.2f) return;
            var pr = p.predicted;
            var sn = game.Sniper;
            // the scope said "hit" on a man the shot was meant for and it was not: he moved at the last moment
            if (pr.hit && pr.Npc != null && !pr.Npc.Dead && landed.Npc == null && Ballistics.FairGame(pr.Npc))
            {
                lastCallAt = Time.time;
                Radio(Def.handler, "Son anda kıpırdadı. Nişanı tazele, tekrar dene.", 3.5f);
                return;
            }
            var n = pr.nearNpc;
            if (n == null || n.Dead) return;
            // a shot meant for something else: a machine, a fuel drum, the spot that lures him out, or nowhere near him
            if (pr.missBy.magnitude > 1.1f && !pr.nearBlocked) return;
            if (landed.collider != null && (landed.collider.GetComponentInParent<Shootable>() != null || landed.collider.GetComponentInParent<ExplosiveBarrel>() != null)) return;
            if (DistractPoint.HasValue && landed.hit && Vector3.Distance(DistractPoint.Value, landed.point) < 5f) return;
            lastCallAt = Time.time;
            if (pr.nearBlocked)
            {
                Radio(Def.handler, "Siperine takıldı. Açığa çıkmasını bekle ya da başka açıdan dene.", 4f);
                return;
            }
            int dx = Mathf.RoundToInt(pr.missBy.x * 100f), dy = Mathf.RoundToInt(pr.missBy.y * 100f);
            if (Mathf.Abs(dx) < 3 && Mathf.Abs(dy) < 3) return;
            string call = Mathf.Abs(dy) >= Mathf.Abs(dx)
                ? (dy < 0 ? Fm("Iska: {0} cm alçak.", -dy) : Fm("Iska: {0} cm yüksek.", dy))
                : (dx < 0 ? Fm("Iska: {0} cm solda.", -dx) : Fm("Iska: {0} cm sağda.", dx));
            float range = pr.nearRange;
            int zero = sn.LastShotZero;
            float vel = p.spec.velocity;
            float drop = Difficulty.DropAt(range, zero, vel);              // where the dial put it (- low)
            // the shooter's frame and the man's pace as they were when the trigger broke
            var right = p.shotRight;
            float cross = Vector3.Dot(p.wind, right);
            float drift = Difficulty.DriftAt(range, cross, vel);
            var mv = p.nearVel; mv.y = 0f;
            float side = Vector3.Dot(mv, right);
            string fix;
            if (Mathf.Abs(dy) >= Mathf.Abs(dx) && dy < 0 && drop < -0.08f)
                fix = Fm("Mermi düştü: hedef {0} m, MENZİL {1}. MENZİL'i hedefin mesafesine getir.", Mathf.RoundToInt(range), zero);
            else if (Mathf.Abs(dy) >= Mathf.Abs(dx) && dy > 0 && drop > 0.08f)
                fix = Fm("Fazla yüksek: hedef {0} m, MENZİL {1}. MENZİL'i düşür.", Mathf.RoundToInt(range), zero);
            else if (Mathf.Abs(dx) > Mathf.Abs(dy) && mv.magnitude > 0.6f && Mathf.Abs(side) > 0.4f && Mathf.Sign(dx) != Mathf.Sign(side))
                fix = Loc.Tr("Arkasından geçti: yürüdüğü yöne daha önden nişan al.");
            else if (Mathf.Abs(dx) > Mathf.Abs(dy) && mv.magnitude > 0.6f && Mathf.Abs(side) > 0.4f)
                fix = Loc.Tr("Önünden geçti: payı biraz azalt.");
            else if (Mathf.Abs(dx) > Mathf.Abs(dy) && Mathf.Abs(drift) > 0.06f && Mathf.Sign(dx) == Mathf.Sign(drift))
                fix = drift > 0f ? Loc.Tr("Rüzgâr sağa itti: biraz sola tut.") : Loc.Tr("Rüzgâr sola itti: biraz sağa tut.");
            else if (!p.shotBreath)
                fix = Loc.Tr("Nefesini tut: nişan sabitlenince ateş et.");
            else
                fix = Loc.Tr("Nişangahı vücudun ortasına al.");
            Radio(Def.handler, call + " " + fix, 4.5f);
        }

        void NearMiss(Vector3 from, Vector3 to)
        {
            if (game == null || game.Sniper == null || Def == null || Def.number < 2) return;
            var seg = to - from;
            float len = seg.magnitude;
            if (len < 1f) return;
            var dir = seg / len;
            foreach (var t in Targets)
            {
                if (t == null || t.Dead || t.Escaped || t.Incapacitated || t.HiddenTarget || t.IsHidden || t.Panicking) continue;
                var c = t.ChestPoint;
                float k = Mathf.Clamp(Vector3.Dot(c - from, dir), 0f, len);
                if (Vector3.Distance(from + dir * k, c) > 1.6f) continue;
                NearMisses++;
                t.Kick(c, dir, BodyZone.Torso, 0.3f);              // a flinch at the crack past his head
                t.React(from);
                // the crack past his head tells his people which way the shot came from
                Firefight.I?.AddHeat(0.12f);
                foreach (var n in NPC.All)
                    if (n != null && !n.Dead && (n.Role == Role.Guard || n.Role == Role.Bodyguard) && Vector3.Distance(n.transform.position, c) < 14f) n.React(from);
                Say(t.MarkedTarget ? "ISKALADIN — " + UIK.Up(t.DisplayName) + " KAÇIYOR!" : "Iskaladın — biri ürktü ve kaçıyor!", Theme.Danger, "run");
                Sfx.Play(Sfx.Alert, 0.6f);
                Reinforce("miss");
                break;
            }
        }

        /// <summary>The job is done but the enemy knows where the nest is and still has guns: the shooter has to get
        /// away first (from the 5th contract).</summary>
        bool ExfilNeeded
        {
            get
            {
                var ff = Firefight.I;
                if (game == null || game.Sniper == null || Def == null || Def.number < 2 || ff == null || ff.Threat <= 0f) return false;
                // V2: the nest found, or gunmen already in the fight within rifle reach of it
                var nest = game.Sniper.transform.position;
                foreach (var n in NPC.All)
                    if (n != null && !n.Dead && !n.Incapacitated && n.Combat != null && n.Combat.CanFight &&
                        (ff.Located || (n.Combat.Engaged && Vector3.Distance(n.transform.position, nest) < 450f))) return true;
                return false;
            }
        }

        public bool Exfiltrating { get; private set; }

        /// <summary>
        /// V2 (user): the target is down but men are shooting at the nest — the job is not over until the last of them is
        /// silenced. Every gunman in the fight is marked, an objective counts them down, latecomers join the count. If some
        /// cannot be reached for two minutes they fall back (the contract then ends).
        /// </summary>
        IEnumerator ExfilRoutine()
        {
            Exfiltrating = true;
            Firefight.I?.SetLocated(true);                     // they have the muzzle flash: rounds start coming
            var nest = game.Sniper.transform.position;
            var hostiles = new List<NPC>();
            System.Func<NPC, bool> fighting = n => n != null && !n.Dead && !n.Incapacitated && !n.Escaped && n.Combat != null && n.Combat.CanFight;
            System.Action gather = () =>
            {
                foreach (var n in NPC.All)
                {
                    if (!fighting(n) || hostiles.Contains(n)) continue;
                    if (!n.Combat.Engaged && Vector3.Distance(n.transform.position, nest) > 450f) continue;
                    hostiles.Add(n);
                    n.Identified = true;                      // marked: the player can find every one of them
                    n.Combat.Engage(nest);
                }
            };
            gather();
            System.Func<int> left = () => { int c = 0; foreach (var n in hostiles) if (fighting(n)) c++; return c; };
            var clear = Custom("Sana ateş edenleri sustur", () => left() == 0);
            clear.liveText = () => "Sana ateş edenleri sustur  ·  " + left() + " kaldı";
            Say("HEDEF TAMAM — AMA SENİ BULDULAR: SİLAHLILARI SUSTUR", Theme.Accent, "guard");
            Radio("KONTROL", "Hedef düştü ama yerin belli. Kimse arkandan gelmesin — " + left() + " silahlı var, hepsini sustur. Gerekirse siper al.", 5f);
            Sfx.Play(Sfx.Alert, 0.7f, 0.9f);
            float start = Elapsed;
            while (State == Phase.Playing)
            {
                gather();                                       // reinforcements that join late count too
                if (left() == 0) break;
                if (Elapsed - start > 120f)
                {
                    foreach (var n in hostiles) if (fighting(n)) n.ForceEscape();
                    Say("Kalan silahlılar geri çekildi", Theme.Accent, "run");
                    break;
                }
                yield return null;
            }
            Exfiltrating = false;
            if (State != Phase.Playing) yield break;
            if (clear.State == ObjectiveState.Active) { clear.State = ObjectiveState.Done; clear.changedAt = Elapsed; ObjectivesChanged?.Invoke(); }
            Say("BÖLGE TEMİZ — GÖREV TAMAM", Theme.Success, "check");
            yield return new WaitForSeconds(1.2f);
            yield return CompleteRoutine();
        }

        IEnumerator CompleteRoutine()
        {
            State = Phase.Ending;
            Success = true;
            yield return null;
            while (KillCam.Active) yield return null;
            yield return FinishRoutine(true, 1.1f);
        }

        IEnumerator FinishRoutine(bool success, float delay)
        {
            while (KillCam.Active) yield return null;
            yield return new WaitForSecondsRealtime(delay);
            Success = success;
            Sfx.SetAlarm(false);
            var ch = Challenges;
            ChallengeMet = new bool[ch.Count];
            ChallengeNew = new bool[ch.Count];
            if (success)
            {
                int met = 0;
                for (int i = 0; i < ch.Count; i++)
                {
                    bool ok = false;
                    try { ok = ch[i].Evaluate(this); } catch (Exception e) { Debug.LogException(e); }
                    // easy aim was on: the hardest star is not awarded
                    if (ok && i == ch.Count - 1 && ch.Count >= 2 && game != null && game.Sniper != null && game.Sniper.EasyUsed)
                    {
                        ok = false;
                        EasyStarLost = true;
                    }
                    ChallengeMet[i] = ok;
                    if (!ok) continue;
                    met++;
                    if (!Save.Challenge(Def.id, i)) { ChallengeNew[i] = true; Save.SetChallenge(Def.id, i); }
                }
                Stars = met;
                // the pay, line by line (the result screen counts them up one after the other)
                Ledger.Clear();
                void Line(string label, float amount) { int a = Mathf.RoundToInt(amount / 10f) * 10; if (a != 0) Ledger.Add(new KeyValuePair<string, int>(label, a)); }
                // economy: the fee scales with the live tuning; a replay of a finished job pays only part of it (VETERAN pays full)
                float fee = Def.reward * Tuning.Pct("pay_scale_pct");
                // a VETERAN run pays its ×1.5 in full the first time; after that it is a replay like any other (no farming)
                WasReplay = Save.Completed(Def.id) && (!Veteran || Save.Veteran(Def.id)) && !Daily;
                Line("Sözleşme ücreti", fee);
                int starLines = 0;
                for (int i = 0; i < ch.Count; i++) if (ChallengeMet[i] && ch[i].kind != ChallengeKind.Complete) starLines++;
                if (starLines > 0) Line("Yıldız hedefleri  ×" + starLines, fee * 0.2f * starLines);
                if (!AlarmEver) Line("Sessiz iş — alarm yok", fee * 0.15f);
                if (LongestKill > 0f) Line("Uzak atış  ·  " + Mathf.RoundToInt(LongestKill) + " m", LongestKill * 2f);
                if (TargetsKilled > 0 && Headshots > 0 && AllTargetHeadshots) Line("Kafadan vuruş", fee * 0.1f);
                if (ReconMarked > 0) Line("Keşif  ·  " + ReconMarked + "/" + Mathf.Max(ReconMarked, ReconTotal) + " tehdit",
                                          ReconMarked * 150f + (ReconMarked >= ReconTotal && ReconTotal >= 2 ? 400f : 0f));
                if (CiviliansKilled > 0) Line("Sivil kaybı  ×" + CiviliansKilled, -300f * CiviliansKilled);
                if (CiviliansHurt > 0) Line("Yaralı sivil  ×" + CiviliansHurt, -100f * CiviliansHurt);
                int sum = 0;
                foreach (var l in Ledger) sum += l.Value;
                if (WasReplay && sum > 0)
                {
                    float keep = Mathf.Clamp01(Tuning.Pct("replay_pay_pct"));
                    Line("Tekrar oynanış  ×%" + Mathf.RoundToInt(keep * 100f), -sum * (1f - keep));
                    sum = 0;
                    foreach (var l in Ledger) sum += l.Value;
                }
                if (Veteran && sum > 0) Line("VETERAN  ×1,5", sum * 0.5f);
                if (Daily && sum > 0) { Line("Günün sözleşmesi (+%33: ücretin 2 katı)", sum / 3f); Save.SetDailyDone(Difficulty.TodayKey); }
                sum = 0;
                foreach (var l in Ledger) sum += l.Value;
                CashEarned = Mathf.Max(0, sum);
                // the grade: S is the clean job (every star, no alarm, nobody else hurt, every round on target)
                bool allStars = met >= ch.Count && ch.Count > 0;
                int g = met >= 3 ? 4 : (met == 2 ? 3 : (met == 1 ? 2 : 1));
                bool objectShots = Def.kind == "KAZA" || Def.kind == "SABOTAJ";     // rounds into a trap / a machine are not misses
                if (allStars && !AlarmEver && CiviliansKilled + CiviliansHurt == 0 && Shots > 0 && (Hits >= Shots || objectShots)) g = 5;
                Grade = g == 5 ? "S" : (g == 4 ? "A" : (g == 3 ? "B" : (g == 2 ? "C" : "D")));
                Save.SetBestGrade(Def.id, g);
                NewDistanceRecord = LongestKill > 0f && LongestKill > Save.BestDistance(Def.id) + 0.5f && Save.Completed(Def.id);
                if (LongestKill > Save.BestDistance(Def.id)) Save.SetBestDistance(Def.id, LongestKill);
                if (Veteran) Save.SetVeteran(Def.id);
                Progress.AddCash(CashEarned, Daily ? "contract_daily" : "contract");
                Save.SetCompleted(Def.id);
                Save.SetBestTime(Def.id, Elapsed);
                if (Def.region == "harbor") Save.Unlocked = Mathf.Max(Save.Unlocked, Def.number + 1);
                Sfx.Play(Sfx.Success, 0.9f);
            }
            else
            {
                Stars = 0;
                CashEarned = 0;
                Ledger.Clear();
                Grade = "—";
                Sfx.Play(Sfx.Fail, 0.9f);
            }
            // career: experience for every contract (a little for a failed one), diamonds for a first S / the daily job
            try
            {
                XpBefore = Save.Xp;
                XpEarned = Progress.ContractXp(this);
                bool firstS = success && Grade == "S" && !Save.Flag("s_" + Def.id);
                if (firstS) Save.SetFlag("s_" + Def.id);
                GemsEarned = Progress.ContractGems(this, firstS);
                Progress.AddGems(GemsEarned, "contract");
                LevelUps.Clear();
                LevelUps.AddRange(Progress.AddXp(XpEarned));
                DailyTasks.Record(this);
            }
            catch (Exception e) { Debug.LogException(e); }
            try { Report(success); } catch (Exception e) { Debug.LogException(e); }
            State = Phase.Done;
            Finished?.Invoke(success);
        }
    }

    /// <summary>A countdown that runs fast while the player has nothing to do (see <see cref="MissionRuntime.Clock"/>).</summary>
    public class WaitClock
    {
        public float Left, Fast = 5f;
        public Func<bool> Busy;
        public bool Stopped, Hurried;
        /// <summary>Called once, the first time the clock starts running fast (a radio line: "they are nearly there").</summary>
        public Action OnHurry;
        public void Stop() => Stopped = true;
    }

    /// <summary>
    /// Drives a prop along a path (car, forklift, speedboat, tug) like a vehicle: it steers towards a point a little
    /// ahead (round corners instead of sharp turns), slows down for bends, brakes to a stop at the end and can drive
    /// the first part of the path in reverse (backing out of a parking spot). Ping-pong movers drive the way back in
    /// reverse (forklift style).
    /// </summary>
    public class PathMover : MonoBehaviour
    {
        Vector3[] pts;
        float[] cum, limit;
        float total, speed, accel, s, v, pauseLeft, reverseS;
        int dir = 1;
        bool pingPong, running;
        Rigidbody rb;
        /// <summary>Called once when a one-way run reaches the end of its path.</summary>
        public Action Arrived;
        public bool Running => running;
        /// <summary>Multiplies the cruise speed (1 = as set): a team car racing in once the way is clear.</summary>
        public float SpeedScale = 1f;
        /// <summary>Distance driven along the path (m) and the path length.</summary>
        public float Travelled => s;
        public float Length => total;

        public void Run(Vector3[] path, float spd, bool pp, int reverseSegments = 0)
        {
            Arrived = null;
            if (path == null || path.Length < 2) return;
            pts = path;
            int n = pts.Length;
            cum = new float[n];
            for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            total = cum[n - 1];
            speed = spd;
            pingPong = pp;
            accel = Mathf.Max(0.8f, speed * 0.25f);
            reverseS = reverseSegments > 0 ? cum[Mathf.Min(reverseSegments, n - 1)] : 0f;
            // speed limit at every corner from its turning radius (gentle sideways acceleration), full stop at the
            // ends and where reversing ends
            limit = new float[n];
            for (int i = 1; i < n - 1; i++)
            {
                var a = pts[i] - pts[i - 1]; a.y = 0;
                var b = pts[i + 1] - pts[i]; b.y = 0;
                float ang = a.sqrMagnitude > 1e-4f && b.sqrMagnitude > 1e-4f ? Vector3.Angle(a, b) : 0f;
                if (ang < 1f) { limit[i] = speed; continue; }
                float radius = Mathf.Min(a.magnitude, b.magnitude) / (2f * Mathf.Sin(ang * 0.5f * Mathf.Deg2Rad));
                limit[i] = Mathf.Clamp(Mathf.Sqrt(2.6f * radius), 1.4f, speed);
            }
            limit[0] = 0f;
            limit[n - 1] = 0f;
            if (reverseSegments > 0 && reverseSegments < n - 1) limit[reverseSegments] = 0f;
            s = 0f; v = 0f; dir = 1; pauseLeft = 0f;
            SpeedScale = 1f;
            running = true;
            transform.position = pts[0];
            transform.rotation = Quaternion.Euler(0, FacingYaw(), 0);
            if (rb == null)
            {
                rb = gameObject.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    // a moving body may not carry a concave mesh collider whose mesh Unity cannot read (the tugboat):
                    // swap those for a box of the same bounds before the rigidbody goes on
                    foreach (var mc in GetComponentsInChildren<MeshCollider>(true))
                    {
                        if (mc.convex || mc.sharedMesh == null || mc.sharedMesh.isReadable) continue;
                        var b = mc.sharedMesh.bounds;
                        var go = mc.gameObject;
                        bool trig = mc.isTrigger;
                        DestroyImmediate(mc);
                        var bc = go.AddComponent<BoxCollider>();
                        bc.center = b.center;
                        bc.size = b.size;
                        bc.isTrigger = trig;
                    }
                    rb = gameObject.AddComponent<Rigidbody>();
                }
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        public void Stop() { running = false; v = 0f; }

        /// <summary>Waits <paramref name="seconds"/> before moving off (after <see cref="Run"/>: a rotor spinning up).</summary>
        public void Delay(float seconds) { pauseLeft = Mathf.Max(pauseLeft, seconds); }

        bool Reversing => dir < 0 || s < reverseS - 1e-3f;

        Vector3 PointAt(float d)
        {
            d = Mathf.Clamp(d, 0f, total);
            int i = 1;
            while (i < pts.Length - 1 && cum[i] < d) i++;
            float seg = cum[i] - cum[i - 1];
            return seg > 1e-5f ? Vector3.Lerp(pts[i - 1], pts[i], (d - cum[i - 1]) / seg) : pts[i];
        }

        /// <summary>Yaw the prop should face: towards a point a little ahead on the path (backwards when reversing).</summary>
        float FacingYaw()
        {
            float look = Mathf.Clamp(v * 0.6f, 1.6f, 4f);
            // stay on the reversing part of the path while backing out (do not look past the turning point)
            float lo = 0f, hi = total;
            if (dir > 0 && s < reverseS) hi = reverseS; else if (dir > 0) lo = reverseS;
            var a = PointAt(Mathf.Clamp(s - dir * look * 0.35f, lo, hi));
            var b = PointAt(Mathf.Clamp(s + dir * look, lo, hi));
            var d = b - a; d.y = 0;
            if (d.sqrMagnitude < 1e-4f) return transform.eulerAngles.y;
            if (Reversing) d = -d;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        /// <summary>Speed limit at point <paramref name="i"/> for cruise speed <paramref name="spd"/>: a hurrying
        /// driver takes the corners faster too (by the square root of the hurry: tyres have their limits).</summary>
        float Lim(int i, float spd)
        {
            float l = limit[i];
            if (l <= 0f) return 0f;
            if (l >= speed - 1e-3f) return spd;
            return Mathf.Min(spd, l * Mathf.Sqrt(Mathf.Max(1f, SpeedScale)));
        }

        /// <summary>Seconds until it stops at the end of the path, at the current speed setting: speeding up, braking
        /// for each corner and for the stop (plus a wait before it moves off).</summary>
        public float SecondsLeft()
        {
            if (!running || pts == null) return 0f;
            float spd = speed * Mathf.Max(0.1f, SpeedScale);
            float a = Mathf.Max(0.8f, spd * 0.25f);
            float t = Mathf.Max(0f, pauseLeft);
            if (dir < 0) return t + s / Mathf.Max(0.5f, spd * 0.6f) + 1.2f + total / Mathf.Max(0.5f, spd * 0.6f);   // ping-pong: rough
            float ve = v, at = s;
            for (int i = 1; i < pts.Length; i++)
            {
                if (cum[i] <= at + 1e-4f) continue;
                float L = cum[i] - at;
                bool rev = at < reverseS - 1e-3f;
                float vmax = rev ? Mathf.Min(spd, Mathf.Max(2.2f, spd * 0.45f)) : spd;
                float vx = Lim(i, spd);
                if (rev && Mathf.Abs(cum[i] - reverseS) < 1e-3f) vx = 0f;
                ve = Mathf.Min(ve, vmax);
                vx = Mathf.Min(vx, Mathf.Min(vmax, Mathf.Sqrt(ve * ve + 2f * a * L)));
                float d1 = (vmax * vmax - ve * ve) / (2f * a), d2 = (vmax * vmax - vx * vx) / (2f * a);
                if (d1 + d2 <= L) t += (vmax - ve) / a + (vmax - vx) / a + (L - d1 - d2) / vmax;
                else
                {
                    float vp = Mathf.Sqrt(Mathf.Max(0f, (2f * a * L + ve * ve + vx * vx) * 0.5f));
                    vp = Mathf.Max(vp, Mathf.Max(ve, vx));
                    t += Mathf.Max(0f, vp - ve) / a + Mathf.Max(0f, vp - vx) / a;
                }
                if (rev && Mathf.Abs(cum[i] - reverseS) < 1e-3f) t += 0.45f;
                ve = vx;
                at = cum[i];
            }
            return t;
        }

        void Update()
        {
            if (!running || pts == null) return;
            float dt = Time.deltaTime;
            if (pauseLeft > 0f) { pauseLeft -= dt; return; }
            float spd = speed * Mathf.Max(0.1f, SpeedScale);
            accel = Mathf.Max(0.8f, spd * 0.25f);
            float vmax = Reversing ? Mathf.Min(spd, Mathf.Max(2.2f, spd * 0.45f)) : spd;
            // brake in time for the next corners and stops ahead
            float allowed = vmax;
            float reach = v * v / (2f * accel) + 3f;
            if (dir > 0)
            {
                for (int i = 1; i < pts.Length; i++)
                {
                    float d = cum[i] - s;
                    if (d < 0f) continue;
                    if (d > reach) break;
                    float lim = Lim(i, spd);
                    if (s < reverseS && Mathf.Abs(cum[i] - reverseS) < 1e-3f) lim = 0f;
                    allowed = Mathf.Min(allowed, Mathf.Sqrt(lim * lim + 2f * accel * d) + 0.3f);
                }
            }
            else
            {
                for (int i = pts.Length - 2; i >= 0; i--)
                {
                    float d = s - cum[i];
                    if (d < 0f) continue;
                    if (d > reach) break;
                    float lim = Lim(i, spd);
                    allowed = Mathf.Min(allowed, Mathf.Sqrt(lim * lim + 2f * accel * d) + 0.3f);
                }
            }
            v = Mathf.Min(Mathf.MoveTowards(v, vmax, accel * dt), allowed);
            float ns = s + dir * v * dt;
            // reached the point where reversing ends: stop, then drive forward
            if (dir > 0 && s < reverseS && ns >= reverseS)
            {
                s = reverseS; v = 0f; pauseLeft = 0.45f;
                Place(dt, true);
                return;
            }
            if (ns >= total && dir > 0)
            {
                s = total; v = 0f;
                Place(dt, true);
                if (pingPong) { dir = -1; pauseLeft = 1.2f; return; }
                running = false;
                var cb = Arrived;
                Arrived = null;
                cb?.Invoke();
                return;
            }
            if (ns <= 0f && dir < 0)
            {
                s = 0f; v = 0f; dir = 1; pauseLeft = 1.2f;
                Place(dt, true);
                return;
            }
            s = ns;
            Place(dt, false);
        }

        void Place(float dt, bool snapStop)
        {
            transform.position = PointAt(s);
            var want = Quaternion.Euler(0, FacingYaw(), 0);
            float rate = snapStop ? 0f : Mathf.Max(30f, v * 22f);
            if (rate > 0f) transform.rotation = Quaternion.RotateTowards(transform.rotation, want, rate * dt);
        }
    }

    /// <summary>Sweeps a light (tower searchlight) left and right.</summary>
    public class Sweeper : MonoBehaviour
    {
        public float yawCenter, sweep = 60f, pitch = 12f, speed = 0.25f;
        float t;

        void Update()
        {
            t += Time.deltaTime * speed;
            float y = yawCenter + Mathf.Sin(t) * sweep * 0.5f;
            transform.rotation = Quaternion.Euler(pitch, y, 0);
        }
    }
}

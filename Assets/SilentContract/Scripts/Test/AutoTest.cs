using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// Self-playing test run (simulator builds with SC_AUTOTEST or the "-autotest" argument): visits every screen,
    /// plays every contract (scans and shoots the target) and saves screenshots + a log to
    /// persistentDataPath/autotest. test_sim.sh copies them back next to the project.
    /// The log is written as it goes (every contract, every new error), so a crash or a hang still leaves it behind,
    /// and a step that throws is logged and skipped instead of ending the whole run.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        string dir;
        readonly StringBuilder log = new StringBuilder();
        readonly Dictionary<string, int> seen = new Dictionary<string, int>();
        int shot;
        int errors;
        bool dirty;
        float nextFlush;

        void Awake()
        {
            dir = Path.Combine(Application.persistentDataPath, "autotest");
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            log.AppendLine("start " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  unity " + Application.unityVersion);
            Flush();
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors++;
                // the same error every frame would bury everything else: each distinct one (message + where it came
                // from) is written twice, after that only counted
                string top = stack ?? "";
                int nl = top.IndexOf('\n');
                if (nl >= 0) top = top.Substring(0, nl);
                string key = "[" + type + "] " + msg + "  @ " + top;
                seen.TryGetValue(key, out int n);
                seen[key] = n + 1;
                if (n < 2) log.AppendLine("[" + type + "] " + msg + "\n" + stack);
                dirty = true;
            }
            else if (msg.StartsWith("[SC]")) { log.AppendLine(msg); dirty = true; }
        }

        void Update()
        {
            if (dirty && Time.realtimeSinceStartup >= nextFlush) Flush();
        }

        /// <summary>Writes everything so far to log.txt (plus how often each repeated error happened).</summary>
        void Flush()
        {
            dirty = false;
            nextFlush = Time.realtimeSinceStartup + 2f;
            try
            {
                var sb = new StringBuilder(log.ToString());
                bool head = false;
                foreach (var kv in seen)
                {
                    if (kv.Value <= 2) continue;
                    if (!head) { sb.AppendLine("--- tekrarlanan hatalar"); head = true; }
                    sb.AppendLine(kv.Value + "x  " + kv.Key);
                }
                File.WriteAllText(Path.Combine(dir, "log.txt"), sb.ToString());
            }
            catch { }
        }

        bool Try(string what, Action a)
        {
            try { a(); return true; }
            catch (Exception e)
            {
                errors++;
                log.AppendLine("[AutoTest] " + what + ": " + e);
                Flush();
                return false;
            }
        }

        /// <summary>Runs a step; if it throws, logs it, goes back to the menu and lets the run carry on.</summary>
        IEnumerator Guard(IEnumerator step, string what)
        {
            bool failed = false;
            while (true)
            {
                object cur;
                try
                {
                    if (!step.MoveNext()) break;
                    cur = step.Current;
                }
                catch (Exception e)
                {
                    errors++;
                    log.AppendLine("[AutoTest] " + what + " yarıda kaldı: " + e);
                    Flush();
                    failed = true;
                    break;
                }
                yield return cur;
            }
            if (failed)
            {
                Try("menüye dönüş", () => Game.I.GoMenu());
                yield return Wait(1.5f);
            }
        }

        void Start() { StartCoroutine(Run()); }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex != null)
            {
                var png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(dir, (++shot).ToString("00") + "_" + name + ".png"), png);
                Destroy(tex);
            }
            log.AppendLine("shot " + name + "  fps≈" + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0"));
        }

        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        /// <summary>Opens a page, waits for it to settle, takes its picture.</summary>
        IEnumerator Visit(string name, Action open, float wait = 1f)
        {
            if (!Try(name, open)) yield break;
            yield return Wait(wait);
            yield return Shot(name);
        }

        IEnumerator Run()
        {
            float boot = Time.realtimeSinceStartup;
            var g = Game.I;
            while (g == null || !g.Ready) { yield return null; g = Game.I; }
            log.AppendLine("device " + SystemInfo.deviceModel + "  screen " + Screen.width + "x" + Screen.height + "  safe " + Screen.safeArea +
                           "  mem " + SystemInfo.systemMemorySize + "  ready in " + (Time.realtimeSinceStartup - boot).ToString("0.0") + "s");
            Flush();
            Save.Unlocked = MissionDefs.All.Count;
            Save.KillCam = true;
            Firefight.God = true;                    // the bot never takes cover: rounds must not end its runs
            Save.AimAssist = false;                 // V2: the real help levels (the bot aims with its own ballistics)
            if (Save.Cash < 5000) Save.Cash = 5000;
            yield return Wait(2.5f);
            yield return Shot("menu");
            yield return Visit("missions", () => g.UI.ShowMissions());
            yield return Visit("briefing_m1", () => g.UI.ShowBriefing(MissionDefs.All[0]));
            if (MissionDefs.All.Count > 7) yield return Visit("briefing_m8", () => g.UI.ShowBriefing(MissionDefs.All[7]));
            yield return Visit("armory", () => g.UI.ShowArmory(null));
            yield return Visit("settings", () => g.UI.ShowSettings(false));
            Try("settings close", () => g.UI.CloseSettings());
            yield return Wait(0.5f);
            yield return Visit("credits", () => g.UI.ShowCredits());
            yield return Visit("caseboard_start", () => g.UI.ShowCaseBoard());
            // V2 career and shop screens
            yield return Visit("shop_offers", () => g.UI.ShowShop(ShopScreen.Tab.Offers));
            yield return Visit("shop_packs", () => g.UI.ShowShop(ShopScreen.Tab.Packs));
            yield return Visit("shop_gems", () => g.UI.ShowShop(ShopScreen.Tab.Gems));
            yield return Visit("shop_cash", () => g.UI.ShowShop(ShopScreen.Tab.Cash));
            Try("menu", () => g.UI.ShowMenu(false));
            yield return Wait(0.8f);
            yield return Visit("daily_reward", () => g.UI.Daily.Show(0.1f));
            Try("daily close", () => g.UI.Daily.Hide(0.1f));
            yield return Wait(0.4f);
            yield return Visit("level_up", () => g.UI.LevelUp.Play(new List<Progress.LevelReward> { Progress.RewardFor(5) }, null), 1.2f);
            Try("level up close", () => g.UI.LevelUp.Hide(0.1f));
            yield return Wait(0.4f);
            if (Gear.Weapons.Count > 3)
            {
                Save.SetOwned("w_" + Gear.Weapons[3].id);
                Gear.Equip(Gear.Weapons[3]);
                yield return Visit("armory_lr", () => g.UI.ShowArmory(null), 1.2f);
                Gear.Equip(Gear.Weapons[0]);
            }
            yield return Visit("missions_city", () => { g.UI.ShowMissions(); g.UI.Missions.Show("city"); });
            yield return Visit("missions_mountain", () => g.UI.Missions.Show("mountain"));
            Try("menu", () => g.UI.ShowMenu(false));
            yield return Wait(0.8f);
            yield return Shot("menu_again");
            Flush();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-ui") >= 0)
            {
                log.AppendLine("ui only: " + shot + " shots");
                log.AppendLine("errors " + errors);
                Flush();
                File.WriteAllText(Path.Combine(dir, "DONE"), "ok");
                yield break;
            }

            int done = 0;
            foreach (var m in MissionDefs.All)
            {
                yield return Guard(PlayMission(m), m.id);
                done++;
                if (m.number == 1)
                {
                    // retry M1 once more to capture the pause menu
                    if (Try("m1 again", () => g.StartMission(m)))
                    {
                        yield return Wait(3f);
                        if (Try("pause", () => g.UI.ShowPause())) { yield return Wait(0.6f); yield return Shot("pause"); }
                        Try("pause close", () => g.UI.ClosePause());
                    }
                    Try("menu", () => g.GoMenu());
                    yield return Wait(1f);
                }
            }
            // V2: the first contract again as VETERAN (switch on the briefing, harder run, ×1.5 pay, medal on the card)
            {
                var m1 = MissionDefs.All[0];
                Try("menu", () => g.GoMenu());
                yield return Wait(1f);
                Difficulty.VeteranId = m1.id;
                log.AppendLine("--- veteran run");
                yield return Guard(PlayMission(m1), m1.id + "_veteran");
                Difficulty.VeteranId = null;
                log.AppendLine("veteran medal=" + Save.Veteran(m1.id) + " best grade=" + Save.BestGrade(m1.id));
                yield return Visit("missions_end", () => g.UI.ShowMissions());
            }
            Try("menu", () => g.GoMenu());
            yield return Wait(1.5f);
            yield return Shot("menu_end");
            yield return Visit("caseboard_end", () => g.UI.ShowCaseBoard());
            Try("menu", () => g.UI.ShowMenu(false));
            log.AppendLine("contracts " + done + "/" + MissionDefs.All.Count + "  total " + ((Time.realtimeSinceStartup - boot) / 60f).ToString("0.0") + " min");
            log.AppendLine("errors " + errors);
            Flush();
            File.WriteAllText(Path.Combine(dir, "DONE"), "ok");
            yield return Wait(0.5f);
            Application.Quit();
        }

        readonly HashSet<string> movedOn = new HashSet<string>();
        System.Action<Projectile, ShotResult> lastOnFired;

        IEnumerator PlayMission(MissionDef m)
        {
            var g = Game.I;
            log.AppendLine("=== " + m.id + " " + m.title);
            Flush();
            float start = Time.realtimeSinceStartup;
            if (m.number == 1 || m.number == 13 || m.number == 21 || m.number % 4 == 0)
            {
                Try("briefing", () => g.UI.ShowBriefing(m));
                yield return Wait(1.2f);
                yield return Shot(m.id + "_briefing");
                if (m.number == 1 || m.number == 13 || m.number == 21 || m.number == 8)
                {
                    bool shown = false;
                    Try("map", () => shown = g.UI.ShowMap(m));
                    if (shown) { yield return Wait(0.8f); yield return Shot(m.id + "_map"); }
                    Try("map close", () => g.UI.Map.Hide(0.1f));
                    yield return Wait(0.3f);
                }
            }
            g.StartMission(m);
            // a contract on the other map builds that map first
            float tw = Time.realtimeSinceStartup;
            while ((g.Switching || g.Runtime.Def != m) && Time.realtimeSinceStartup - tw < 90f) yield return null;
            if (g.Runtime.Def != m) log.AppendLine("map/contract did not start within 90 s");
            yield return Wait(4.0f);
            // the surveillance intro: let it run a moment (picture), then tap through it like a player
            {
                float ti0 = Time.realtimeSinceStartup;
                while (g.Runtime.State == MissionRuntime.Phase.Intro && Time.realtimeSinceStartup - ti0 < 12f)
                {
                    if (Time.realtimeSinceStartup - ti0 > 1.5f) Cinematic.SkipRequested = true;
                    yield return null;
                }
                if (g.Runtime.State == MissionRuntime.Phase.Intro) log.AppendLine("intro did not end within 16 s");
            }
            int f0 = Time.frameCount;
            float s0 = Time.realtimeSinceStartup;
            yield return Shot(m.id + "_hip");
            var sn = g.Sniper;
            var rt = g.Runtime;
            NPC target = rt.Targets.Count > 0 ? rt.Targets[0] : null;
            // wait until the target is visible (some leave a building first)
            float t0 = Time.realtimeSinceStartup;
            while (target != null && (target.IsHidden || target.Dead) && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            if (target == null) { log.AppendLine("no target"); g.GoMenu(); yield return Wait(1.2f); yield break; }
            sn.SetScoped(true);
            sn.SetZoom(sn.MaxZoom * 0.8f);
            float t1 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t1 < 3.0f)
            {
                if (!target.Dead) sn.LookAt(target.AimPoint);
                yield return null;
            }
            yield return Shot(m.id + "_scope");
            log.AppendLine("target " + target.DisplayName + " identified=" + target.Identified + " range=" + sn.RangeToCenter.ToString("0") +
                           " tier=" + sn.Diff.tier + " assist=" + sn.Diff.assist + " idMode=" + rt.IdMode + " nests=" + rt.Nests.Count +
                           " difficulty=" + Difficulty.Score(m) + " window=" + rt.Window.ToString("0.0") + " recon=" + rt.ReconMarked + "/" + rt.ReconTotal);
            // V2: say who he is (the bot knows), see the dossier turn to DOĞRULANDI
            if (rt.IdMode && target.NeedsId && !target.Confirmed)
            {
                float ti = Time.realtimeSinceStartup;
                while (!target.Identified && Time.realtimeSinceStartup - ti < 3f) { if (!target.Dead) sn.LookAt(target.AimPoint); yield return null; }
                Try("tag", () => rt.Tag(target));
                yield return Wait(0.6f);
                yield return Shot(m.id + "_tagged");
                log.AppendLine("tagged confirmed=" + target.Confirmed);
            }
            // V2: move to the second place once per map (first contract that has one), picture the view from there
            if (rt.Nests.Count > 1 && !movedOn.Contains(m.region))
            {
                movedOn.Add(m.region);
                var to = rt.Nests[1];
                Try("relocate", () => { sn.SetScoped(false); rt.Relocate(to); });
                yield return Wait(1.0f);
                yield return Shot(m.id + "_relocating");
                float tr = Time.realtimeSinceStartup;
                while (rt.Relocating && Time.realtimeSinceStartup - tr < 20f) yield return null;
                yield return Wait(0.8f);
                yield return Shot(m.id + "_nest_" + to.id);
                log.AppendLine("relocated to " + to.id + " " + to.name);
                sn.SetScoped(true);
                sn.SetZoom(sn.MaxZoom * 0.8f);
            }
            // aim with drop compensation: aim at the head, then fire; then the next living target (two-target contracts)
            int attempt = 0;
            var triedNests = new HashSet<string>();
            // take-alive contracts: the dart if the loadout has one, otherwise a leg (like the briefing says)
            bool alive = m.kind == "CANLI YAKALA";
            if (alive)
                for (int c = 0; c < 4 && sn.AmmoType != AmmoKind.Tranquilizer; c++) Try("ammo", () => sn.CycleAmmo());
            bool dart = sn.AmmoType == AmmoKind.Tranquilizer;
            if (alive) log.AppendLine("take alive: " + (dart ? "dart" : "leg"));
            System.Func<NPC, Vector3> spot = n => !alive || dart ? (dart ? n.ChestPoint : n.AimPoint)
                                                             : (n.IsTarget ? n.transform.position + Vector3.up * 0.4f : n.AimPoint);   // shin/knee (0.55 caught the hip)
            int waits = 0;
            // an "accident" contract: the round goes into the trap (the avalanche charge), at the moment the briefing gives
            if (m.kind == "KAZA")
            {
                var trap = Shootable.All.Find(x => x != null && !x.Disabled && x.kind == "exploder");
                if (trap != null)
                {
                    float tk = Time.realtimeSinceStartup;
                    while (rt.State == MissionRuntime.Phase.Playing && Time.realtimeSinceStartup - tk < 90f &&
                           !(rt.CountdownLeft > 0f && rt.CountdownLeft <= 6.2f)) yield return null;
                    var rr = trap.GetComponentInChildren<Renderer>();
                    var tp = rr != null ? rr.bounds.center : trap.transform.position;
                    sn.SetScoped(true);
                    sn.SetBreathButton(true);
                    float ta = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - ta < 0.5f)
                    {
                        float d = Vector3.Distance(sn.transform.position, tp);
                        float mv = Mathf.Max(50f, sn.CurrentSpec.velocity);
                        float tof = d / mv, t0z = sn.ZeroRange / mv;
                        float drop = 0.5f * Ballistics.Gravity * tof * tof - d * (0.5f * Ballistics.Gravity * t0z * t0z / sn.ZeroRange);
                        sn.LookAt(tp - 0.5f * sn.Wind * Ballistics.WindFactor * tof * tof + Vector3.up * drop);
                        yield return null;
                    }
                    log.AppendLine("  accident: shooting the " + trap.kind + " at " + rt.CountdownLeft.ToString("0.0") + " s");
                    sn.Fire();
                    sn.SetBreathButton(false);
                    yield return Wait(8f);
                }
            }
            // where each round really went (against the prediction above)
            System.Action<Projectile, ShotResult> onFired = (pj, pred) =>
            {
                if (pj != null) pj.OnImpact += (q, r) =>
                {
                    var who = r.collider != null ? r.collider.GetComponentInParent<NPC>() : null;
                    log.AppendLine("    impact: " + (r.hit ? (who != null ? who.DisplayName + (who.Dead ? " (dead)" : who.Down ? " (down)" : who.Wounded ? " (wounded)" : "") : (r.collider != null ? r.collider.name : "water")) : "nothing") +
                                   " @" + r.distance.ToString("0") + (r.glass > 0 ? " glass" + r.glass : ""));
                };
            };
            if (lastOnFired != null) sn.Fired -= lastOnFired;
            lastOnFired = onFired;
            sn.Fired += onFired;
            // done with one: alive jobs stop at "down" (a second round on a man on the ground kills him)
            System.Func<NPC, bool> handled = n => n == null || n.Dead || n.Escaped || (alive && n.Incapacitated);
            while (attempt < 8 && rt.State == MissionRuntime.Phase.Playing)
            {
                // someone drawing on a person (an executioner over the downed courier, a gunman in a protect job) comes first
                var gunman = NPC.All.Find(n => n != null && !n.Dead && !n.Incapacitated && !n.IsHidden && n.Aiming && !n.Protected && n != target);
                if (gunman != null && (handled(target) || !target.Aiming))
                {
                    if (gunman != target) log.AppendLine("  stopping " + gunman.DisplayName + " (aiming)");
                    target = gunman;
                }
                else if (handled(target))
                {
                    target = rt.Targets.Find(t => !handled(t) && !t.IsHidden);
                    // a dart job: the guards who would carry her off go to sleep too
                    if (target == null && dart)
                        target = NPC.All.Find(n => n != null && !n.IsHidden && !n.Incapacitated && (n.Role == Role.Bodyguard || n.Role == Role.Guard) &&
                                                   Vector3.Distance(n.transform.position, rt.Targets[0].transform.position) < 40f);
                    if (target == null)
                    {
                        // one still to show up (the third gunman, the man in the car): wait for him
                        if (rt.Targets.Exists(t => !handled(t) && t.IsHidden))
                        {
                            float tw2 = Time.realtimeSinceStartup;
                            while (rt.State == MissionRuntime.Phase.Playing && Time.realtimeSinceStartup - tw2 < 25f &&
                                   !rt.Targets.Exists(t => !handled(t) && !t.IsHidden)) yield return null;
                            continue;
                        }
                        // a gunman may still come for the man he has to keep alive (the courier on the ground)
                        float tg = Time.realtimeSinceStartup;
                        System.Predicate<NPC> drawing = n => n != null && !n.Dead && !n.Incapacitated && !n.IsHidden && n.Aiming && !n.Protected;
                        while (rt.State == MissionRuntime.Phase.Playing && Time.realtimeSinceStartup - tg < 10f && !NPC.All.Exists(drawing)) yield return null;
                        if (NPC.All.Exists(drawing)) continue;
                        break;
                    }
                }
                attempt++;
                float t2 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t2 < 0.6f)
                {
                    if (!target.Dead)
                    {
                        var at = spot(target);
                        float d = Vector3.Distance(sn.transform.position, at);
                        float mv = Mathf.Max(50f, sn.CurrentSpec.velocity);
                        float tof = d / mv;
                        var lead = target.Velocity * tof;
                        var wind = 0.5f * sn.Wind * Ballistics.WindFactor * tof * tof;
                        float t0z = sn.ZeroRange / mv;
                        float drop = 0.5f * Ballistics.Gravity * tof * tof - d * (0.5f * Ballistics.Gravity * t0z * t0z / sn.ZeroRange);
                        sn.LookAt(at + lead - wind + Vector3.up * (drop - 0.05f));
                    }
                    yield return null;
                }
                float tb = Time.realtimeSinceStartup;
                while (sn.Busy && Time.realtimeSinceStartup - tb < 10f) yield return null;
                sn.SetBreathButton(true);
                // hold the breath and squeeze when the game's own flight model says the round lands on him (a good
                // player waits for the sway to settle the same way); give up waiting after 2.5 s and take the shot
                string predicted = "miss";
                bool onHim = false, blockedByWorld = false;
                float th = Time.realtimeSinceStartup;
                yield return Wait(0.25f);
                while (Time.realtimeSinceStartup - th < 2.5f)
                {
                    var spec = sn.CurrentSpec;
                    var cam = sn.Cam.transform;
                    var pv = Ballistics.InitialVelocity(cam.forward, cam.right, spec.velocity, sn.ZeroRange);
                    var pr = Ballistics.PredictMoving(cam.position, pv, sn.Wind, Layers.ShotMask, spec.pierce);
                    var who = pr.collider != null ? pr.collider.GetComponentInParent<NPC>() : null;
                    predicted = pr.hit ? (who != null ? who.DisplayName : (pr.collider != null ? pr.collider.name : "?")) + " @" + pr.distance.ToString("0") : "miss";
                    onHim = who == target;
                    // stopped short by a wall / car / parapet well before him
                    blockedByWorld = pr.hit && who == null && pr.distance < Vector3.Distance(cam.position, target.ChestPoint) - 3f;
                    if (onHim) break;
                    if (!target.Dead)
                    {
                        // keep the hold on him while waiting (he walks)
                        var at = spot(target);
                        float mv = Mathf.Max(50f, spec.velocity);
                        float d = Vector3.Distance(sn.transform.position, at);
                        float tof = d / mv;
                        float t0z = sn.ZeroRange / mv;
                        float drop = 0.5f * Ballistics.Gravity * tof * tof - d * (0.5f * Ballistics.Gravity * t0z * t0z / sn.ZeroRange);
                        sn.LookAt(at + target.Velocity * tof - 0.5f * sn.Wind * Ballistics.WindFactor * tof * tof + Vector3.up * (drop - 0.05f));
                    }
                    yield return null;
                }
                log.AppendLine("  shot " + attempt + " at " + target.DisplayName + " d=" + Vector3.Distance(sn.Cam.transform.position, target.AimPoint).ToString("0") +
                               " predicted=" + predicted + (onHim ? "" : "  (no clean line in 2.5 s)") +
                               " zero=" + sn.ZeroRange + " wind=" + sn.Wind.magnitude.ToString("0.0") + " moving=" + target.Velocity.magnitude.ToString("0.0"));
                // no clean line and nothing to move for: a patient player holds fire and waits for him to step clear
                if (!onHim && !blockedByWorld && waits < 5 && !target.IsHidden)
                {
                    waits++;
                    sn.SetBreathButton(false);
                    log.AppendLine("  holding fire (" + predicted + ")");
                    yield return Wait(1.5f);
                    attempt--;
                    continue;
                }
                if (!onHim && blockedByWorld)
                {
                    // the line is blocked by something that is not a person: a player would move to another place
                    var next = rt.CanRelocate ? rt.Nests.Find(n => n != rt.Nest && !triedNests.Contains(n.id)) : null;
                    if (next != null)
                    {
                        sn.SetBreathButton(false);
                        triedNests.Add(rt.Nest != null ? rt.Nest.id : "main");
                        triedNests.Add(next.id);
                        log.AppendLine("  blocked (" + predicted + ") — moving to " + next.id + " " + next.name);
                        Try("relocate", () => { sn.SetScoped(false); rt.Relocate(next); });
                        float tr2 = Time.realtimeSinceStartup;
                        yield return Wait(0.5f);
                        while (rt.Relocating && Time.realtimeSinceStartup - tr2 < 20f) yield return null;
                        yield return Wait(0.6f);
                        sn.SetScoped(true);
                        sn.SetZoom(sn.MaxZoom * 0.8f);
                        attempt--;
                        continue;
                    }
                }
                sn.Fire();
                sn.SetBreathButton(false);
                yield return Wait(0.25f);
                if (KillCam.Active)
                {
                    yield return Wait(0.9f);
                    yield return Shot(m.id + "_killcam");
                }
                float t3 = Time.realtimeSinceStartup;
                while (KillCam.Active && Time.realtimeSinceStartup - t3 < 12f) yield return null;
                yield return Wait(1.4f);
            }
            sn.Fired -= onFired;
            float t4 = Time.realtimeSinceStartup;
            while (rt.State != MissionRuntime.Phase.Done && Time.realtimeSinceStartup - t4 < 25f) yield return null;
            if (rt.State != MissionRuntime.Phase.Done) log.AppendLine("not finished after 25 s: state " + rt.State + ", objectives " +
                string.Join(", ", rt.Objectives.ConvertAll(o => o.text + "=" + o.State)));
            float fps = (Time.frameCount - f0) / Mathf.Max(0.01f, Time.realtimeSinceStartup - s0);
            yield return Wait(2.2f);
            if (g.UI.Result != null && g.UI.Result.IntelShown)
            {
                yield return Shot(m.id + "_intel");
                Try("intel", () => g.UI.Result.CloseIntel(2));
                yield return Wait(0.6f);
                if (g.UI.Result.EpilogueShown)
                {
                    yield return Shot(m.id + "_epilogue");
                    log.AppendLine("epilogue shown");
                    Try("epilogue", () => g.UI.Result.CloseEpilogue());
                    yield return Wait(0.5f);
                }
            }
            if (rt.Success) yield return Wait(3.2f);          // the pay lines and the grade are counted in
            yield return Shot(m.id + "_result");
            log.AppendLine("  career: xp+" + rt.XpEarned + " gems+" + rt.GemsEarned + " levels+" + rt.LevelUps.Count + " → level " + Progress.Level +
                           " gems " + Save.Gems + " cash " + Save.Cash + " power " + Gear.Power(Gear.Equipped));
            log.AppendLine("  grade=" + rt.Grade + " veteran=" + rt.Veteran + " recon=" + rt.ReconMarked + "/" + rt.ReconTotal + " pay=" + rt.CashEarned +
                           " [" + string.Join("; ", rt.Ledger.ConvertAll(l => l.Key + " " + l.Value).ToArray()) + "]");
            log.AppendLine("result success=" + rt.Success + " stars=" + rt.Stars + " shots=" + rt.Shots + " hits=" + rt.Hits + " fail=" + rt.FailReason +
                           "  avg fps " + fps.ToString("0") + "  " + (Time.realtimeSinceStartup - start).ToString("0") + "s");
            Flush();
            g.GoMenu();
            yield return Wait(1.2f);
        }
    }
}

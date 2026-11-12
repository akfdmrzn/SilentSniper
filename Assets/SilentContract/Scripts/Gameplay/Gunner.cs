using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// An armed man in a firefight with the nest. When something scares him (a shot nearby, a body, the alarm) he draws,
    /// runs for the nearest cover the nest cannot see into and crouches there. While nobody knows where the shooter is
    /// he peeks out and scans the rooftops; once the nest is located he pops up, takes aim (the HUD marks him) and fires
    /// a burst or two, then ducks again — now and then he moves to other cover or reloads. Rounds landing close make
    /// him duck at once. Out of range he stays down. Mission scripts that give him their own orders take him over.
    /// V3 (user: "the enemy only stands and watches"): the first man into a fight calls it with a hand signal or a shout,
    /// rounds landing close make him flinch before he ducks, once the shots have given the nest roughly away he fires
    /// back at where they came from (wild, but the cracks shake the scope), he shifts along his cover between peeks so
    /// he never pops up at the same spot twice, and out of reach of the nest he bounds forward from cover to cover —
    /// sharper and quicker on the trigger with every region of the campaign.
    /// </summary>
    public class Gunner : MonoBehaviour
    {
        public NPC npc;
        public GunHold gun;
        /// <summary>His marksmanship (multiplies the gun's accuracy).</summary>
        public float Skill = 1f;
        public bool Engaged { get; private set; }
        /// <summary>Where he has taken cover (or where he stands).</summary>
        public Vector3 CoverSpot => haveCover ? cover : (npc != null ? npc.transform.position : transform.position);

        enum S { Draw, Run, Hide, Aim, Fire, Reload, Search, Exposed }
        S s;
        float t, stateTime, aimTime, nextRound;
        int ammo, burstLeft, bursts, cycles;
        bool newBurst;
        Vector3 cover, threat;
        bool haveCover, kneel, peekable;
        int myPlan;
        float suppressedUntil, nextCoverSearch, searchHeatAt;
        Vector3 aimJitter;
        static int coverFrame = -1;
        static float lastCallAt = -100f;
        bool blind, runFiring, runNoise;
        float drawTime = 1f, nextShift, nextAdvance;

        MissionRuntime Rt => MissionRuntime.I;
        Firefight FF => Firefight.I;

        public static Gunner Attach(NPC n, GunHold g)
        {
            if (n == null || g == null) return null;
            var c = n.GetComponent<Gunner>();
            if (c == null) c = n.gameObject.AddComponent<Gunner>();
            c.npc = n;
            c.gun = g;
            c.ammo = g.spec.mag;
            c.Skill = BaseSkill();
            n.Combat = c;
            return c;
        }

        /// <summary>V3: marksmanship by the contract's place in the campaign (a mission may still set its own).</summary>
        static float BaseSkill()
        {
            var d = MissionRuntime.I != null ? MissionRuntime.I.Def : null;
            int n = d != null ? d.number : 1;
            return Mathf.Lerp(0.95f, 1.4f, Mathf.Clamp01((n - 1) / 27f));
        }

        /// <summary>0 harbour start … 1 the last mountain contract (how bold the tactics get).</summary>
        static float Grade
        {
            get
            {
                var d = MissionRuntime.I != null ? MissionRuntime.I.Def : null;
                return Mathf.Clamp01(((d != null ? d.number : 1) - 1) / 27f);
            }
        }

        /// <summary>Alive, awake, free and with a gun in his hand.</summary>
        public bool CanFight => Able;

        bool Able => npc != null && !npc.Dead && !npc.Incapacitated && !npc.Escaped && !npc.IsHidden && !npc.Riding &&
                     !(npc.Wounded && npc.WoundZone == BodyZone.Arm);

        /// <summary>Something frightening happened (towards <paramref name="source"/>): into the fight.</summary>
        public void Engage(Vector3 source)
        {
            if (!Able || npc.Aiming) return;
            threat = source;
            if (Engaged) return;
            Engaged = true;
            cycles = 0;
            blind = false;
            runFiring = false;
            float yaw = npc.YawTo(Nest);
            string draw = npc.HasClip("pull_gun") ? "pull_gun" : "alert";
            // the first man into the fight calls it: a hand signal or a shout to the others, then into cover
            string call = UnityEngine.Random.value < 0.5f ? "signal" : "shout";
            if (Time.time - lastCallAt > 5f && npc.HasClip(call))
            {
                lastCallAt = Time.time;
                drawTime = 2.2f;
                Plan(Act.IdleFacing(draw, yaw, 0.9f), Act.IdleFacing(call, yaw, 0));
            }
            else
            {
                drawTime = 1f;
                // turns towards the nest while he draws
                Plan(Act.IdleFacing(draw, yaw, 0), null);
            }
            Enter(S.Draw);
        }

        /// <summary>Already alerted and something happens again: back into the fight if a script has not taken him over.</summary>
        public void Poke(Vector3 source)
        {
            if (Engaged || npc.Scripted || npc.Aiming) return;
            Engage(source);
        }

        /// <summary>The enemy knows where the nest is now: whoever can still fight joins in.</summary>
        public void OnNestLocated()
        {
            if (!Able || npc.Scripted || npc.Aiming) return;
            if (!Engaged) { npc.MarkCombat(); Engage(Nest); }
            else if (s == S.Search) { t = 0f; Enter(S.Hide); stateTime = UnityEngine.Random.Range(0.3f, 1.2f); }
        }

        /// <summary>A round landed close (<paramref name="point"/>) or a man went down next to him: he ducks.</summary>
        public void Suppress(Vector3 point)
        {
            if (!Engaged || !Able) return;
            suppressedUntil = Time.time + UnityEngine.Random.Range(2.2f, 3.6f);
            if (s == S.Aim || s == S.Fire || s == S.Search)
            {
                npc.ThreatToPlayer = 0f;
                GoHide(UnityEngine.Random.Range(1.5f, 3f), true);
            }
        }

        public void Disengage()
        {
            Engaged = false;
            if (npc != null) npc.ThreatToPlayer = 0f;
            if (gun != null) gun.Aiming = false;
        }

        Vector3 Nest => FF != null ? FF.Nest : (Sniper.I != null ? Sniper.I.transform.position : transform.position + Vector3.up * 50f);

        void Plan(Act a, Act b)
        {
            if (b != null) npc.SetPlan(a, b); else npc.SetPlan(a);
            myPlan = npc.PlanVersion;
        }

        void Enter(S next)
        {
            s = next;
            t = 0f;
        }

        void Update()
        {
            if (!Engaged) return;
            if (!Able) { Disengage(); return; }
            // a mission script gave him other orders (or a hitman's aim): it is in charge now
            if (npc.Aiming || npc.PlanVersion != myPlan) { Disengage(); return; }
            var rt = Rt;
            if (rt == null || !rt.IsPlaying) return;
            // a story cutaway: the player can neither duck nor shoot, so nobody gets closer to shooting him
            if (KillCam.IsCutaway) return;
            float dt = Time.deltaTime;
            t += dt;
            switch (s)
            {
                case S.Draw:
                    if (t > 0.45f && !gun.Drawn) gun.Drawn = true;
                    if (t > drawTime) PickCover(false);
                    break;
                case S.Run:
                    // covering fire on the move: wild rounds at the nest every few steps, only while he can see it
                    if (runFiring && npc.Moving && ammo > 0 && t >= nextRound && !KillCam.Active)
                    {
                        nextRound = t + gun.spec.roundGap * UnityEngine.Random.Range(2.2f, 3.4f);
                        var mz = gun.Muzzle;
                        if (!Physics.Linecast(mz, Nest, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                        {
                            if (!runNoise) { runNoise = true; Rt?.Noise(mz, 40f, NoiseKind.Gunfire, false); }
                            ammo--;
                            gun.Kick(0.5f);
                            FF?.Fire(npc, gun.spec, mz, 0.1f * Skill);
                        }
                    }
                    if (!npc.Moving || t > 14f) { runFiring = false; GoHide(UnityEngine.Random.Range(0.6f, 1.6f)); }
                    break;
                case S.Hide:
                    if (t < stateTime) break;
                    if (ammo <= 0) { StartReload(); break; }
                    if (Time.time < suppressedUntil) { stateTime = t + 0.6f; break; }
                    Decide();
                    break;
                case S.Exposed:
                    // no cover: down on one knee in the open, firing from there
                    if (t < stateTime) break;
                    if (ammo <= 0) { StartReload(); break; }
                    if (Time.time < suppressedUntil) { stateTime = t + 0.6f; break; }
                    Decide();
                    break;
                case S.Search:
                    Searching(dt);
                    break;
                case S.Aim:
                    Aiming();
                    break;
                case S.Fire:
                    Firing();
                    break;
                case S.Reload:
                    if (t >= gun.spec.reload) { ammo = gun.spec.mag; GoHide(UnityEngine.Random.Range(0.3f, 0.9f)); }
                    break;
            }
        }

        // ------------------------------------------------------------------ cover
        void PickCover(bool move, bool advance = false)
        {
            // one search per frame across all gunmen (each is a few hundred ray casts)
            if (coverFrame == Time.frameCount) return;
            coverFrame = Time.frameCount;
            var nest = Nest;
            // a failed search must not spoil what he knows about the cover he is in
            bool found = advance
                ? CoverFinder.Find(npc, nest, 16f, cover, nest, out var spot, out bool kn, out bool pk)
                : CoverFinder.Find(npc, nest, move ? 15f : 12f, move ? cover : (Vector3?)null, out spot, out kn, out pk);
            if (found)
            {
                kneel = kn;
                peekable = pk;
                haveCover = true;
                cover = spot;
                var d = spot - npc.transform.position; d.y = 0f;
                if (d.magnitude > 0.6f)
                {
                    string clip = d.magnitude < 3.5f && npc.HasClip("crouch_walk") ? "crouch_walk" : gun.RunClip;
                    // bounding forward under fire: sprinting, the rifle at the hip spitting rounds at the nest
                    runFiring = false;
                    runNoise = false;
                    if (advance && spec2Rifle && npc.HasClip("rifle_run_fire") && Firefight.I != null && Firefight.I.Located && UnityEngine.Random.value < 0.5f)
                    {
                        clip = "rifle_run_fire";
                        runFiring = true;
                        nextRound = 0.3f;
                    }
                    float mul = clip == "rifle_run" ? 0.75f : (clip == "crouch_walk" ? 1.1f : 0.95f);
                    Plan(Act.Move(new[] { spot }, clip, mul), Act.IdleFacing(HideClip, npc.YawTo(nest), 0));
                    Enter(S.Run);
                    return;
                }
                GoHide(UnityEngine.Random.Range(0.8f, 1.8f));
                return;
            }
            if (move) { GoHide(UnityEngine.Random.Range(1f, 2f)); return; }
            // nowhere to hide: down on one knee where he is (a smaller target), firing from there
            haveCover = false;
            kneel = true;
            peekable = true;
            Plan(Act.IdleFacing(HideClip, npc.YawTo(nest), 0), null);
            gun.Aiming = false;
            Enter(S.Exposed);
            stateTime = UnityEngine.Random.Range(0.8f, 1.6f);
        }

        string HideClip => npc.HasClip("crouch_hide") ? "crouch_hide" : "duck";
        bool spec2Rifle => gun != null && gun.spec.twoHanded;

        void GoHide(float seconds, bool flinch = false)
        {
            npc.ThreatToPlayer = 0f;
            gun.Aiming = false;
            blind = false;
            runFiring = false;
            // a round cracking past: a flinch first, then down
            if (flinch && npc.HasClip("flinch") && UnityEngine.Random.value < 0.75f)
                Plan(Act.Once("flinch", 0.5f), Act.IdleFacing(HideClip, npc.YawTo(Nest), 0));
            else
                Plan(Act.IdleFacing(HideClip, npc.YawTo(Nest), 0), null);
            Enter(haveCover ? S.Hide : S.Exposed);
            stateTime = seconds + (flinch ? 0.4f : 0f);
        }

        void StartReload()
        {
            npc.ThreatToPlayer = 0f;
            gun.Aiming = false;
            string clip = gun.ReloadClip(kneel || haveCover);
            if (!npc.HasClip(clip)) clip = HideClip;
            Plan(Act.IdleFacing(clip, npc.YawTo(Nest), 0), null);
            Enter(S.Reload);
        }

        // ------------------------------------------------------------------ what next, from cover
        void Decide()
        {
            var ff = FF;
            var nest = Nest;
            float dist = Vector3.Distance(npc.transform.position, nest);
            bool canReach = dist < gun.spec.range;
            cycles++;
            // now and then: off to other cover (a moving man is harder to hit)
            if (haveCover && cycles > 2 && UnityEngine.Random.value < 0.3f && Time.time > nextCoverSearch)
            {
                nextCoverSearch = Time.time + 8f;
                cycles = 0;
                PickCover(true);
                return;
            }
            // out of reach of the nest and it is known: bound forward from cover to cover
            if (!canReach && ff != null && ff.Located && ff.Threat > 0f && Time.time > nextAdvance)
            {
                nextAdvance = Time.time + UnityEngine.Random.Range(4f, 8f) * Mathf.Lerp(1.2f, 0.7f, Grade);
                PickCover(true, true);
                return;
            }
            if (ff != null && ff.Located && ff.Threat > 0f && canReach && peekable)
            {
                // never twice from the same spot: a step or two along the cover first
                if (haveCover && cycles > 1 && Time.time > nextShift && UnityEngine.Random.value < Mathf.Lerp(0.3f, 0.55f, Grade) && TryShift()) return;
                StartAim();
                return;
            }
            // the shots gave the nest roughly away: fire back at where they came from (wild)
            if (ff != null && !ff.Located && ff.Threat > 0f && ff.Heat >= Firefight.BlindHeat && canReach && peekable &&
                UnityEngine.Random.value < Mathf.Lerp(0.4f, 0.7f, Grade))
            {
                StartAim(true);
                return;
            }
            // nobody knows where the shooter is (or he cannot reach the nest): look over the cover for him
            if (peekable)
            {
                string clip = npc.HasClip("look_around") ? "look_around" : "idle_look";
                float yaw = npc.YawTo(nest) + UnityEngine.Random.Range(-35f, 35f);
                Plan(Act.IdleFacing(clip, yaw, 0), null);
                gun.Aiming = false;
                Enter(S.Search);
                stateTime = UnityEngine.Random.Range(2.5f, 5f);
                return;
            }
            GoHide(UnityEngine.Random.Range(1.5f, 3f));
        }

        void Searching(float dt)
        {
            var ff = FF;
            if (ff != null && ff.Located && ff.Threat > 0f && Vector3.Distance(npc.transform.position, Nest) < gun.spec.range) { StartAim(); return; }
            // looking right at the nest while the player shows himself: he may spot him
            if (ff != null && Time.time >= searchHeatAt)
            {
                searchHeatAt = Time.time + 0.5f;
                var eye = npc.EyePos;
                var nest = Nest;
                float d = Vector3.Distance(eye, nest);
                if (d < 320f && ff.Exposure > 0f && !Physics.Linecast(eye, nest, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    var f = npc.transform.forward; f.y = 0f;
                    var to = nest - eye; to.y = 0f;
                    // the naked eye at a hundred metres and more: slow, slower further off and at night
                    if (Vector3.Angle(f, to) < 40f) ff.AddHeat(0.007f * ff.Exposure * Mathf.Lerp(1f, 0.25f, d / 320f) * (Rt.IsNight ? 0.4f : 1f));
                }
            }
            if (t >= stateTime) GoHide(UnityEngine.Random.Range(1.2f, 2.5f));
        }

        /// <summary>A step or two along his cover (still hidden when he crouches, still able to fire over it), so the next
        /// peek comes from somewhere else.</summary>
        bool TryShift()
        {
            nextShift = Time.time + UnityEngine.Random.Range(5f, 9f);
            var nest = Nest;
            var to = nest - cover; to.y = 0f;
            if (to.sqrMagnitude < 1f) return false;
            var side = Vector3.Cross(Vector3.up, to.normalized);
            float sgn = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            for (int k = 0; k < 2; k++, sgn = -sgn)
            {
                var p = cover + side * sgn * UnityEngine.Random.Range(1.3f, 2.3f);
                if (!CoverFinder.Check(npc, p, nest, out var gp, out bool kn, out bool pk) || !pk) continue;
                if (!Level.WalkableLine(npc.transform.position, gp, out _)) continue;
                cover = gp;
                kneel = kn;
                peekable = pk;
                string clip = npc.HasClip("crouch_walk") ? "crouch_walk" : gun.RunClip;
                Plan(Act.Move(new[] { gp }, clip, 1.05f), Act.IdleFacing(HideClip, npc.YawTo(nest), 0));
                Enter(S.Run);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ shooting
        void StartAim(bool wild = false)
        {
            var nest = Nest;
            blind = wild;
            // can he see the nest from up here at all? (the same eye heights the cover search used; kneeling first,
            // standing up if the wall is too high for that)
            var basePos = npc.transform.position;
            bool clear = kneel && !Physics.Linecast(basePos + Vector3.up * CoverFinder.KneelEye, nest, Layers.WorldMask, QueryTriggerInteraction.Ignore);
            if (!clear && !Physics.Linecast(basePos + Vector3.up * CoverFinder.StandEye, nest, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                clear = true;
                kneel = false;
            }
            if (!clear)
            {
                peekable = false;
                if (Time.time > nextCoverSearch) { nextCoverSearch = Time.time + 6f; PickCover(true); }
                else GoHide(2f);
                return;
            }
            Plan(Act.IdleFacing(gun.AimClip(kneel), npc.YawTo(nest), 0), null);
            gun.Drawn = true;
            gun.Aiming = true;
            gun.Aim = AimPoint;
            // blind: at the rooftops the shots came from, metres off
            aimJitter = Random.insideUnitSphere * (blind ? 9f : 2.2f);
            aimTime = UnityEngine.Random.Range(0.8f, 1.45f) / Mathf.Max(0.4f, Skill) * (bursts == 0 ? 1.25f : 1f) * (blind ? 0.7f : 1f);
            Enter(S.Aim);
        }

        /// <summary>Where his barrel points: the nest, with a wobble that settles while he aims.</summary>
        Vector3 AimPoint()
        {
            float settle = s == S.Aim ? Mathf.Clamp01(1f - t / Mathf.Max(0.1f, aimTime)) : 0.15f;
            return Nest + aimJitter * settle + Vector3.down * 0.3f;
        }

        void Aiming()
        {
            // the nest found while he was spraying blind: he takes proper aim
            if (blind && FF != null && FF.Located) { blind = false; t = 0f; aimJitter = Random.insideUnitSphere * 2.2f; }
            npc.ThreatToPlayer = blind ? 0f : Mathf.Clamp01(t / aimTime);
            if (KillCam.Active) return;
            if (t < aimTime) return;
            burstLeft = Mathf.Max(1, gun.spec.burst + UnityEngine.Random.Range(-1, 2));
            newBurst = true;
            nextRound = 0f;
            Enter(S.Fire);
        }

        void Firing()
        {
            npc.ThreatToPlayer = blind ? 0.5f : 1f;
            if (KillCam.Active) return;
            if (t < nextRound) return;
            if (burstLeft <= 0 || ammo <= 0)
            {
                bursts++;
                if (ammo <= 0) { StartReload(); return; }
                // another burst from up here, or back down (bolder later in the campaign)
                if (UnityEngine.Random.value < Mathf.Lerp(0.3f, 0.5f, Grade))
                {
                    burstLeft = Mathf.Max(1, gun.spec.burst + UnityEngine.Random.Range(-1, 2));
                    newBurst = true;
                    nextRound = t + gun.spec.burstGap * UnityEngine.Random.Range(1.2f, 1.9f);
                    return;
                }
                GoHide(haveCover ? UnityEngine.Random.Range(1.4f, 3.2f) : UnityEngine.Random.Range(1.6f, 2.8f));
                return;
            }
            // the first round of a burst: everybody near him hears a gun going off (bystanders panic, guards join in,
            // the people the contract is about crouch for a moment)
            if (newBurst) { newBurst = false; Rt?.Noise(gun.Muzzle, gun.spec.twoHanded ? 40f : 28f, NoiseKind.Gunfire, false); }
            burstLeft--;
            ammo--;
            nextRound = t + gun.spec.roundGap * UnityEngine.Random.Range(1.2f, 1.6f);
            var muzzle = gun.Muzzle;
            // settled aim, not under fire himself, kneeling steadies the gun; blind fire seldom finds the nest
            float aim = Skill * (bursts == 0 ? 0.65f : 1f) * (kneel ? 1.1f : 1f) * (Time.time < suppressedUntil ? 0.35f : 1f) * (blind ? 0.12f : 1f);
            gun.Kick(gun.spec.kind == GunKind.Pistol ? 1f : 0.7f);
            FF?.Fire(npc, gun.spec, muzzle, aim);
        }
    }

    /// <summary>
    /// Finds a spot near a man where the nest cannot see him when he crouches, preferably one he can pop up from to
    /// fire (kneeling or standing), that he can walk to in a straight line, and not already taken by another gunman.
    /// </summary>
    public static class CoverFinder
    {
        struct Cand { public Vector3 p; public float score; public bool kneel, peek; }
        static readonly List<Cand> cands = new List<Cand>();
        static readonly float[] Rings = { 0f, 1.6f, 3f, 4.5f, 6.5f, 9f, 12f, 15f };
        /// <summary>Eye heights of a man firing over cover: kneeling, standing.</summary>
        public const float KneelEye = 1.25f, StandEye = 1.6f;
        static int walkTries;
        static readonly float[] QuickRings = { 2f, 4f, 6.5f };
        static int hideFrame = -1, hidesThisFrame;

        /// <param name="avoid">a spot to move away from (relocating)</param>
        public static bool Find(NPC n, Vector3 threat, float maxDist, Vector3? avoid, out Vector3 spot, out bool kneel, out bool peek) =>
            Find(n, threat, maxDist, avoid, Rings, 12, 16, null, out spot, out kneel, out peek);

        /// <summary>V3: cover a bound closer to <paramref name="toward"/> (at least a few metres nearer than where he is).</summary>
        public static bool Find(NPC n, Vector3 threat, float maxDist, Vector3? avoid, Vector3 toward, out Vector3 spot, out bool kneel, out bool peek) =>
            Find(n, threat, maxDist, avoid, Rings, 12, 16, toward, out spot, out kneel, out peek);

        /// <summary>Is <paramref name="p"/> (near <paramref name="n"/>'s floor) a spot where the nest cannot see him crouched
        /// — and can he fire over it kneeling (<paramref name="kneel"/>) or standing (<paramref name="peek"/>)?</summary>
        public static bool Check(NPC n, Vector3 p, Vector3 threat, out Vector3 gp, out bool kneel, out bool peek)
        {
            gp = p;
            kneel = peek = false;
            var from = n.transform.position;
            if (!Physics.Raycast(new Vector3(p.x, from.y + 1.3f, p.z), Vector3.down, out var g, 2.8f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(g.point.y - from.y) > 0.8f || g.point.y < Level.WaterY + 0.2f || g.normal.y < 0.75f) return false;
            gp = g.point;
            if (Physics.CheckSphere(gp + Vector3.up * 0.9f, 0.3f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            if (!Physics.Linecast(threat, gp + Vector3.up * 1.0f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            if (!Physics.Linecast(threat, gp + Vector3.up * 0.55f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            kneel = !Physics.Linecast(threat, gp + Vector3.up * KneelEye, Layers.WorldMask, QueryTriggerInteraction.Ignore);
            peek = kneel || !Physics.Linecast(threat, gp + Vector3.up * StandEye, Layers.WorldMask, QueryTriggerInteraction.Ignore);
            return true;
        }

        static bool Find(NPC n, Vector3 threat, float maxDist, Vector3? avoid, float[] rings, int dirsNear, int dirsFar, Vector3? toward, out Vector3 spot, out bool kneel, out bool peek)
        {
            spot = n.transform.position;
            kneel = false;
            peek = false;
            var from = n.transform.position;
            cands.Clear();
            walkTries = 12;
            for (int ri = 0; ri < rings.Length; ri++)
            {
                float r = rings[ri];
                if (r > maxDist) break;
                int dirs = r <= 0f ? 1 : (r < 5f ? dirsNear : dirsFar);
                for (int k = 0; k < dirs; k++)
                {
                    float a = (k + (ri % 2) * 0.5f) * Mathf.PI * 2f / dirs;
                    var p = from + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                    // relocating: not the cover he is leaving (cheap, before any ray)
                    if (avoid.HasValue)
                    {
                        float ax = p.x - avoid.Value.x, az = p.z - avoid.Value.z;
                        if (ax * ax + az * az < 9f) continue;
                    }
                    // advancing: only spots a real bound nearer the nest
                    float gain = 0f;
                    if (toward.HasValue)
                    {
                        var tw = toward.Value;
                        float d0 = new Vector2(tw.x - from.x, tw.z - from.z).magnitude, d1 = new Vector2(tw.x - p.x, tw.z - p.z).magnitude;
                        gain = d0 - d1;
                        if (gain < 4f) continue;
                    }
                    if (!Physics.Raycast(new Vector3(p.x, from.y + 1.3f, p.z), Vector3.down, out var g, 2.8f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    if (Mathf.Abs(g.point.y - from.y) > 0.8f || g.point.y < Level.WaterY + 0.2f || g.normal.y < 0.75f) continue;
                    var gp = g.point;
                    // not inside a building or a wall (the ground ray passes through a box it starts in)
                    if (Physics.CheckSphere(gp + Vector3.up * 0.9f, 0.3f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    // hidden when crouched (head and chest), and how he can fire from there
                    if (!Physics.Linecast(threat, gp + Vector3.up * 1.0f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    if (!Physics.Linecast(threat, gp + Vector3.up * 0.55f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    bool kn = !Physics.Linecast(threat, gp + Vector3.up * KneelEye, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                    bool st = kn || !Physics.Linecast(threat, gp + Vector3.up * StandEye, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                    float score = r + (st ? 0f : 5f);
                    if (avoid.HasValue && !toward.HasValue) score += Mathf.Abs(r - 7f) * 0.3f;            // relocating: a few metres on
                    if (toward.HasValue) score -= gain * 1.2f;                                            // advancing: the further the better
                    // already taken by another gunman
                    foreach (var o in NPC.All)
                    {
                        if (o == null || o == n || o.Dead || o.Combat == null || !o.Combat.Engaged) continue;
                        if ((o.Combat.CoverSpot - gp).sqrMagnitude < 2.6f) { score += 8f; break; }
                    }
                    cands.Add(new Cand { p = gp, score = score, kneel = kn, peek = st });
                }
                // nothing further out can beat a free spot he can fire from in this ring (scores only grow with the
                // distance): take it now instead of testing the rings beyond
                if (!avoid.HasValue && !toward.HasValue && cands.Count > 0)
                {
                    cands.Sort((x, y) => x.score.CompareTo(y.score));
                    if (cands[0].score <= r + 0.01f && Pick(from, r + 0.01f, out spot, out kneel, out peek)) return true;
                }
            }
            cands.Sort((x, y) => x.score.CompareTo(y.score));
            return Pick(from, float.MaxValue, out spot, out kneel, out peek);
        }

        /// <summary>The best candidate (score up to <paramref name="maxScore"/>) he can walk to in a straight line; the
        /// ones that cannot be walked to are dropped, the rest stay for a later look.</summary>
        static bool Pick(Vector3 from, float maxScore, out Vector3 spot, out bool kneel, out bool peek)
        {
            spot = from;
            kneel = false;
            peek = false;
            while (cands.Count > 0 && walkTries > 0 && cands[0].score <= maxScore)
            {
                var c = cands[0];
                cands.RemoveAt(0);
                if ((c.p - from).sqrMagnitude > 0.36f)
                {
                    walkTries--;
                    if (!Level.WalkableLine(from, c.p, out _)) continue;
                }
                spot = c.p;
                kneel = c.kneel;
                peek = c.peek;
                return true;
            }
            return false;
        }

        /// <summary>Somewhere close a scared bystander can crouch out of the nest's sight (null = none).</summary>
        public static Vector3? HidingPlace(NPC n, Vector3 threat, float maxDist)
        {
            // a whole crowd panicking in one frame: only the first few look for a hiding place (the others cower)
            if (hideFrame != Time.frameCount) { hideFrame = Time.frameCount; hidesThisFrame = 0; }
            if (++hidesThisFrame > 3) return null;
            if (Find(n, threat, maxDist, null, QuickRings, 8, 10, null, out var spot, out _, out _)) return spot;
            return null;
        }
    }
}

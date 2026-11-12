using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    public enum Role { Civilian, Worker, Guard, Bodyguard, Target, Decoy }

    /// <summary>What made a sound: a bullet hitting something, a splash, a car alarm, an explosion, the general alarm,
    /// the crack of a rifle shot close by.</summary>
    /// <summary>Gunfire: the enemy's own gunmen firing at the nest (their side knows what it is).</summary>
    public enum NoiseKind { Impact, Splash, Car, Explosion, Alarm, Shot, Gunfire }

    /// <summary>One step of an NPC's routine.</summary>
    public class Act
    {
        public enum Kind { Idle, Face, Move, Call, Loop, Escape, Hide }
        public Kind kind;
        public string clip;          // animation (without gender prefix)
        public float seconds;        // idle duration (<=0 = forever)
        public Vector3[] points;     // movement path
        public float yaw;            // facing (degrees) for Face
        public bool hasYaw;
        public Action call;
        public int loopTo;
        public float speedMul = 1f;
        public bool once;            // one-shot clip: plays from the start, the body ends where the clip moved it
        public bool hold;            // one-shot clip that stays on its last frame (collapsed, unconscious)

        public static Act Idle(string clip, float seconds = 0) => new Act { kind = Kind.Idle, clip = clip, seconds = seconds };
        public static Act IdleFacing(string clip, float yaw, float seconds = 0) => new Act { kind = Kind.Idle, clip = clip, seconds = seconds, yaw = yaw, hasYaw = true };
        public static Act Face(float yaw) => new Act { kind = Kind.Face, yaw = yaw, hasYaw = true };
        public static Act Move(Vector3[] pts, string clip = "walk", float speedMul = 1f) => new Act { kind = Kind.Move, points = pts, clip = clip, speedMul = speedMul };
        public static Act Do(Action a) => new Act { kind = Kind.Call, call = a };
        public static Act Loop(int to = 0) => new Act { kind = Kind.Loop, loopTo = to };
        public static Act Escape() => new Act { kind = Kind.Escape };
        public static Act Hide() => new Act { kind = Kind.Hide };
        /// <summary>A one-shot clip (fall, hit reaction, bow): played once from the start. <paramref name="seconds"/> &lt;= 0 =
        /// the clip's own length. <paramref name="hold"/>: stay on the last frame for good.</summary>
        public static Act Once(string clip, float seconds = 0, bool hold = false) => new Act { kind = Kind.Idle, clip = clip, seconds = seconds, once = true, hold = hold };
    }

    /// <summary>
    /// A character: MPFB model + legacy Animation (speed-synced locomotion, cross fades), procedural ragdoll hit zones,
    /// scripted routine and reactions (panic, alert, flee, wounded, sedated, armed attacker).
    /// </summary>
    [DefaultExecutionOrder(-100)]          // the hips are fitted (LateUpdate) before props are put in the hands
    public class NPC : MonoBehaviour
    {
        // ---- identity
        public string Id;
        public string CharId;
        public string DisplayName;
        public string RoleLabel;
        public string Hint;            // short clothing hint shown when scanned
        public string ScanNote;        // toast when the scan proves he is not the target (a body double)
        public Role Role;
        public bool IsTarget;
        public bool Identified;
        /// <summary>V2: a target the player has to recognise himself (dossier photo and clues): unmarked until he confirms
        /// him (<see cref="Confirmed"/>).</summary>
        public bool NeedsId;
        public bool Confirmed;
        /// <summary>The player tagged him as the target and he was not.</summary>
        public bool TaggedWrong;
        /// <summary>Shown on the HUD as the contract's target (found and confirmed, or no identification needed).</summary>
        public bool MarkedTarget => IsTarget && (!NeedsId || Confirmed);
        public string Gender = "m";
        public float Height = 1.78f;

        // ---- state
        public bool Dead { get; private set; }
        public bool Escaped { get; private set; }
        public bool Alerted { get; private set; }
        public bool Panicking { get; private set; }
        public bool Headshot { get; private set; }
        /// <summary>Hit in an arm or a leg and alive.</summary>
        public bool Wounded { get; private set; }
        public BodyZone WoundZone { get; private set; }
        /// <summary>Hit by a tranquilizer dart (collapses after a few steps, sleeps).</summary>
        public bool Sedated { get; private set; }
        /// <summary>On the ground: leg wound or collapsed from the dart.</summary>
        public bool Down { get; private set; }
        /// <summary>Out of the game for a while: dead, sedated or down.</summary>
        public bool Incapacitated => Dead || Sedated || Down;
        public float DownTime { get; private set; }
        public Vector3 Velocity { get; private set; }
        /// <summary>Measured world-space velocity, including being carried by a moving boat.</summary>
        public Vector3 WorldVelocity { get; private set; }
        Vector3 lastWorldPos; bool hasLastWorldPos;
        public float DeathTime { get; private set; }
        public Transform HeadBone { get; private set; }
        public Transform Pelvis { get; private set; }
        public Transform RightHand { get; private set; }
        public Ragdoll Ragdoll => ragdoll;
        public bool CanFlee = true;
        public string Alias;                   // nickname shown on the ID card
        public bool HideOnFlee;                // disappears into a building / car when the flight ends
        public float SightRange = 26f;         // how far this character notices bodies
        public bool IsHidden => hidden;
        public string FleeMarker;              // target escapes here when alerted
        public Vector3[] FleePath;
        /// <summary>When set, a scare makes him run along the rest of this route (from the closest point) instead of FleePath.</summary>
        public Vector3[] FleeAlong;
        public float AwareRadius = 9f;
        public event Action<NPC> OnArrivedEscape;

        // ---- distraction: a sound makes people look; targets walk out to check it (and panic the second time)
        public Vector3[] InvestigatePath;      // authored walk from the usual spot to an open place (towards the noise)
        public Vector3[] CarPath;              // owner of a car: walks to it when its alarm goes off
        public float Suspicion;                // 0 calm, 1 heard something, a second scare makes targets run
        public bool Investigating => investigating;
        public bool VisibleToPlayer;           // kept up to date by MissionRuntime for targets
        public float HiddenTime;               // seconds the target has been out of the player's sight
        public bool SecondChance;              // after hiding in a building, peeks out once more before giving up
        public bool UsedSecondChance;
        public Action<NPC> OnInvestigateEnd;   // mission hook (e.g. switch off the car alarm)
        public Vector3? DistractSpot;          // where the hint tells the player to shoot to lure this target out
        public float FleeEndPause;             // seconds spent at the end of the flight before vanishing (getting into a car)
        public float FleeEndYaw;
        public string EscapeText;              // failure text when this target gets away (null = default)
        public bool MarkWhenHidden;            // keep the HUD marker while hidden (riding in a car)
        public string HiddenStatus;            // objective line while hidden (null = default)
        public float EscapeFailDelay = 1.4f;   // seconds the player sees the getaway before the result screen

        // ---- contract roles
        public bool Armed;                     // carries a weapon: the scan says so
        public bool HiddenTarget;              // a target hidden in the crowd: the HUD marks him only once identified
        public bool Protected;                 // the player must keep this one alive (blue marker)
        public bool NoCrawl;                   // stays where he fell (no crawling away when wounded)
        /// <summary>On a moving prop (gondola cabin, sled, snowcat, car): set by MissionRuntime.AttachToProp. Nowhere to
        /// walk to — noises make him glance, scares make him duck and cower in his seat, a leg wound drops him where he is,
        /// and his body rides on with the vehicle.</summary>
        public bool Riding;
        /// <summary>Playing a seated clip (a sled, the snowcat's cab, a chair).</summary>
        public bool Seated => currentEntry != null && currentEntry.name != null && (currentEntry.name.StartsWith("sit") || currentEntry.name == "sleep_desk");
        /// <summary>Mission hook: called after a non-lethal hit (zone) — a script can give the wounded new orders.</summary>
        public Action<NPC, BodyZone> OnWound;
        /// <summary>Armed attacker: aiming at <see cref="AimVictim"/>; 0..1 until he fires.</summary>
        public bool Aiming { get; private set; }
        public float AimProgress { get; private set; }
        public NPC AimVictim { get; private set; }
        /// <summary>Aiming at a place rather than a person (an enemy marksman drawing a bead on the sniper nest).</summary>
        public bool AimingAtPlayer => Aiming && aimAtPoint;
        float aimLeft, aimTotal;
        Action<NPC> aimFire;
        bool aimAtPoint;
        Vector3 aimPoint;

        public static readonly List<NPC> All = new List<NPC>();

        /// <summary>His fighting brain when he is armed (null for bystanders).</summary>
        public Gunner Combat;
        /// <summary>0..1 while he is taking aim at / firing on the nest (HUD threat marker).</summary>
        public float ThreatToPlayer;
        /// <summary>A lookout with binoculars (HUD label, his radio call reveals the nest).</summary>
        public Spotter Lookout;
        public bool IsLookout => Lookout != null;
        /// <summary>An armed man who does not radio the alarm when he sees a body (the organisation's own gunmen: they
        /// fight, they do not call for help).</summary>
        public bool NoRadio;
        GunHold gunHold;
        bool gunChecked;
        /// <summary>The gun he carries (null = unarmed).</summary>
        public GunHold Gun
        {
            get
            {
                if (!gunChecked || gunHold == null) { gunHold = GetComponent<GunHold>(); gunChecked = gunHold != null; }
                return gunHold;
            }
        }
        /// <summary>Changes whenever he gets a new plan (the gunfight notices when a script takes him over).</summary>
        public int PlanVersion => planVersion;

        /// <summary>Adds the walks of his routine to <paramref name="into"/> (each starting where he stands now):
        /// wandering bystanders keep their spots off them.</summary>
        public void PlannedRoutes(List<Vector3[]> into)
        {
            var at = transform.position;
            foreach (var a in plan)
            {
                if (a == null || a.kind != Act.Kind.Move || a.points == null || a.points.Length == 0) continue;
                var r = new Vector3[a.points.Length + 1];
                r[0] = at;
                Array.Copy(a.points, 0, r, 1, a.points.Length);
                into.Add(r);
                at = a.points[a.points.Length - 1];
            }
        }
        /// <summary>Walking or running somewhere right now.</summary>
        public bool Moving => movePts != null;
        /// <summary>Alerted for the gunfight (unlike <see cref="MarkAlerted"/> the mission script has not taken him over).</summary>
        public void MarkCombat() { Alerted = true; investigating = false; savedPlan = null; }

        Animation anim;
        Ragdoll ragdoll;
        GameObject model;
        string currentClip;
        AnimEntry currentEntry;

        readonly List<Act> plan = new List<Act>();
        int planIndex;
        float actTimer;
        int pathIndex;
        Vector3[] movePts;
        float moveSpeed;
        float targetYaw;
        bool hasTargetYaw;
        bool hidden;
        float stuckTime;
        float nextLookCheck;
        int planVersion;
        bool investigating;
        List<Act> savedPlan;
        int savedIndex;
        Vector3 savedPos;
        float calmAt;
        // root glide after a one-shot clip that moved the body (fall, stagger): the root follows during the cross fade
        Vector3 glideOffset;
        float glideLeft, glideTotal;
        const float OnceFade = 0.35f;

        // ------------------------------------------------------------------ spawn
        public static NPC Spawn(string charId, Vector3 pos, float yaw, Transform parent)
        {
            var prefab = Res.Character(charId);
            var root = new GameObject("NPC_" + charId);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var npc = root.AddComponent<NPC>();
            npc.CharId = charId;
            npc.Gender = charId.StartsWith("guest_f") || charId.EndsWith("_f") || charId.Contains("_f_") ? "f" : "m";
            GameObject m;
            if (prefab != null)
            {
                m = Instantiate(prefab, root.transform);
                m.transform.localPosition = Vector3.zero;
                m.transform.localRotation = Quaternion.identity;
                var info = m.GetComponent<CharInfo>();
                if (info != null) { npc.Gender = info.gender; npc.Height = info.height; }
            }
            else
            {
                m = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                m.transform.SetParent(root.transform, false);
                m.transform.localPosition = Vector3.up;
            }
            npc.model = m;
            npc.Setup();
            All.Add(npc);
            return npc;
        }

        void Setup()
        {
            anim = model.GetComponentInChildren<Animation>();
            if (anim == null) anim = model.AddComponent<Animation>();
            anim.playAutomatically = false;
            anim.cullingType = AnimationCullingType.BasedOnRenderers;
            HeadBone = Util.FindDeep(model.transform, "mixamorig:Head");
            kickBones = new[] { Util.FindDeep(model.transform, "mixamorig:Spine"), Util.FindDeep(model.transform, "mixamorig:Spine1"),
                                Util.FindDeep(model.transform, "mixamorig:Spine2"), Util.FindDeep(model.transform, "mixamorig:Neck"), HeadBone };
            kickArmL = Util.FindDeep(model.transform, "mixamorig:LeftArm");
            kickArmR = Util.FindDeep(model.transform, "mixamorig:RightArm");
            Pelvis = Util.FindDeep(model.transform, "mixamorig:Hips");
            RightHand = Util.FindDeep(model.transform, "mixamorig:RightHand");
            restHips = Pelvis != null ? Pelvis.position.y - transform.position.y : -1f;          // bind pose: nothing played yet
            ragdoll = model.AddComponent<Ragdoll>();
            if (!ragdoll.Build(this))
            {
                Destroy(ragdoll);
                ragdoll = null;
                var cc = gameObject.AddComponent<CapsuleCollider>();
                cc.center = Vector3.up * Height * 0.5f;
                cc.height = Height;
                cc.radius = 0.25f;
                gameObject.layer = Layers.Body;
                var bp = gameObject.AddComponent<BodyPart>();
                bp.owner = this;
                bp.zone = BodyZone.Torso;
            }
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                r.updateWhenOffscreen = false;
                r.quality = SkinQuality.Bone4;
            }
            targetYaw = transform.eulerAngles.y;
            Play("idle", 0f);
        }

        void OnDestroy() { All.Remove(this); }

        Renderer[] rendererCache;
        List<KeyValuePair<Renderer, int>> heatParts;

        /// <summary>Renderers of the character (thermal view, outlines).</summary>
        public Renderer[] Renderers => rendererCache ?? (rendererCache = model != null ? model.GetComponentsInChildren<Renderer>(true) : new Renderer[0]);

        /// <summary>(renderer, sub-mesh) pairs that glow in the thermal scope: everything but cut-out cards (hair, lashes).</summary>
        public List<KeyValuePair<Renderer, int>> HeatParts
        {
            get
            {
                if (heatParts != null) return heatParts;
                heatParts = new List<KeyValuePair<Renderer, int>>();
                foreach (var r in Renderers)
                {
                    if (r == null) continue;
                    var mats = r.sharedMaterials;
                    for (int s = 0; s < mats.Length; s++)
                    {
                        var m = mats[s];
                        if (m != null && m.shader != null && m.shader.name.Contains("Cutout")) continue;
                        heatParts.Add(new KeyValuePair<Renderer, int>(r, s));
                    }
                }
                return heatParts;
            }
        }

        // ------------------------------------------------------------------ animation
        public bool HasClip(string name) => Res.Anims != null && Res.Anims.Get(Gender, name) != null;

        /// <summary>Close substitutes for newer clips (older content packs, missing female versions).</summary>
        static string Fallback(string name)
        {
            switch (name)
            {
                case "fall_down": case "wounded_side": case "wounded_prone": case "pain_head": return "cower";
                case "crawl": return "walk_careful";
                case "hit": case "hit_gunshot": return "duck";
                case "walk_limp": case "walk_injured": case "walk_stumble": case "walk_sneaky": return "walk_careful";
                case "walk_texting": return "walk";
                case "alert": case "hand_on_gun": return "guard_pose";
                case "drink_stand": case "sit_drink": return "drink";
                case "chat": case "talk_passion": case "talk_open": case "shout": case "scheme": case "shrug": case "sit_talk": return "talk";
                case "phone_conv": return "phone_talk";
                case "dance1": case "dance2": case "dance3": case "cheer": case "cheer2": case "cheer_up": return "talk2";
                case "wave_big": case "wave_help": case "bow": return "wave";
                case "catch_breath": case "open_door": case "sit_idle": case "sleep_desk": return "idle";
            }
            if (name.StartsWith("run")) return "run";
            if (name.StartsWith("walk")) return "walk";
            return "idle";
        }

        public void Play(string name, float fade = 0.3f, bool restart = false)
        {
            if (anim == null || Res.Anims == null) return;
            var e = Res.Anims.Get(Gender, name);
            if (e == null)
            {
                if (name == "idle") return;
                var fb = Fallback(name);
                Play(fb == name ? "idle" : fb, fade, restart);
                return;
            }
            string key = e.gender + "_" + e.name;
            if (anim[key] == null)
            {
                e.clip.legacy = true;
                anim.AddClip(e.clip, key);
                var st = anim[key];
                st.wrapMode = e.loop ? WrapMode.Loop : WrapMode.ClampForever;
                states.Add(st);
            }
            if (currentClip == key && !restart) return;
            currentClip = key;
            currentEntry = e;
            var s = anim[key];
            s.speed = 1f;
            if (restart || !e.loop) s.time = 0f;
            else if (s.weight <= 0.001f) s.normalizedTime = UnityEngine.Random.value;
            if (fade <= 0f) anim.Play(key);
            else anim.CrossFade(key, fade);
        }

        /// <summary>Bank name of the clip playing now ("walk", "rifle_kneel" ...).</summary>
        public string ClipName => currentEntry != null ? currentEntry.name : null;

        public float ClipLength(string name)
        {
            var e = Res.Anims != null ? Res.Anims.Get(Gender, name) : null;
            if (e == null && Res.Anims != null) e = Res.Anims.Get(Gender, Fallback(name));
            return e != null ? e.length : 1f;
        }

        public float ClipSpeed(string name)
        {
            var e = Res.Anims != null ? Res.Anims.Get(Gender, name) : null;
            if (e == null || e.speed <= 0.05f)
            {
                if (name == "crawl") return 0.4f;
                if (name == "walk_limp" || name == "walk_injured" || name == "walk_stumble") return 0.7f;
                return name.StartsWith("run") ? 3.2f : 1.3f;
            }
            return e.speed;
        }

        // ------------------------------------------------------------------ routine
        public void SetPlan(params Act[] acts)
        {
            planVersion++;
            flinchLeft = 0f;                      // new orders end a flinch
            plan.Clear();
            plan.AddRange(acts);
            planIndex = -1;
            NextAct();
        }

        public void SetPlan(List<Act> acts) => SetPlan(acts.ToArray());

        /// <summary>
        /// V2 time windows: how long he stays where a shot can reach him, as far as his orders say — the rest of the
        /// current step and every step up to the one that takes him out of sight (Hide / Escape). -1 when the orders never
        /// end that way (a loop, an endless idle, a held pose) or he is already out of reach.
        /// </summary>
        public float SecondsUntilGone()
        {
            if (Dead || Escaped || hidden || Down || Sedated) return -1f;
            if (planIndex < 0 || planIndex >= plan.Count) return -1f;
            float t = 0f;
            for (int i = planIndex, k = 0; i < plan.Count && k < 32; i++, k++)
            {
                var a = plan[i];
                switch (a.kind)
                {
                    case Act.Kind.Idle:
                        if (i == planIndex) { if (float.IsInfinity(actTimer)) return -1f; t += Mathf.Max(0f, actTimer); }
                        else if (a.hold) return -1f;
                        else if (a.seconds > 0f) t += a.seconds;
                        else if (a.once) t += ClipLength(a.clip);
                        else return -1f;
                        break;
                    case Act.Kind.Move:
                    {
                        float spd = Mathf.Max(0.3f, ClipSpeed(a.clip ?? "walk") * a.speedMul);
                        float len = 0f;
                        if (i == planIndex && movePts != null)
                        {
                            var p = transform.position;
                            for (int j = Mathf.Max(0, pathIndex); j < movePts.Length; j++) { len += Vector3.Distance(p, movePts[j]); p = movePts[j]; }
                        }
                        else if (a.points != null)
                            for (int j = 1; j < a.points.Length; j++) len += Vector3.Distance(a.points[j - 1], a.points[j]);
                        t += len / spd;
                        break;
                    }
                    case Act.Kind.Loop: return -1f;
                    case Act.Kind.Escape: case Act.Kind.Hide: return t;
                }
            }
            return -1f;
        }

        /// <summary>Someone who can raise the alarm or shoot back: what the recon phase is about.</summary>
        public bool IsThreat => !IsTarget && (Role == Role.Guard || Role == Role.Bodyguard || Armed || Lookout != null);

        Act CurrentAct => planIndex >= 0 && planIndex < plan.Count ? plan[planIndex] : null;

        void NextAct()
        {
            if (Dead || Escaped) return;
            // a one-shot clip that moved the body (fall, stagger) ended: the root follows during the next cross fade
            var done = CurrentAct;
            if (done != null && done.once && !done.hold && !Riding) BeginGlide(done.clip);       // riders stay in their seat
            planIndex++;
            if (planIndex >= plan.Count) { StartIdle("idle", 0); return; }
            var a = plan[planIndex];
            switch (a.kind)
            {
                case Act.Kind.Idle:
                    if (a.hasYaw && !Riding) { targetYaw = a.yaw; hasTargetYaw = true; }
                    if (a.once) StartOnce(a);
                    else StartIdle(a.clip ?? "idle", a.seconds);
                    break;
                case Act.Kind.Face:
                    if (!Riding) { targetYaw = a.yaw; hasTargetYaw = true; }
                    NextAct();
                    break;
                case Act.Kind.Move:
                    StartMove(a.points, a.clip ?? "walk", a.speedMul);
                    break;
                case Act.Kind.Call:
                    {
                        int ver = planVersion;
                        a.call?.Invoke();
                        if (ver == planVersion && !Dead && !Escaped) NextAct();   // unless the call replaced the plan
                        break;
                    }
                case Act.Kind.Loop:
                    planIndex = a.loopTo - 1;
                    NextAct();
                    break;
                case Act.Kind.Escape:
                    Escape();
                    break;
                case Act.Kind.Hide:
                    SetHidden(true);
                    NextAct();
                    break;
            }
        }

        void StartIdle(string clip, float seconds)
        {
            movePts = null;
            actTimer = seconds > 0 ? seconds : float.PositiveInfinity;
            Play(clip, glideLeft > 0f ? OnceFade : 0.3f);
        }

        void StartOnce(Act a)
        {
            movePts = null;
            float len = ClipLength(a.clip);
            actTimer = a.hold ? float.PositiveInfinity : (a.seconds > 0 ? a.seconds : Mathf.Max(0.2f, len - 0.05f));
            Play(a.clip, 0.18f, true);
        }

        /// <summary>The body was moved by the clip that just ended: slide the root the same way while the next clip
        /// fades in (the blended hips stay in place), unless the new spot has no floor at the same height.</summary>
        void BeginGlide(string clip)
        {
            var e = Res.Anims != null && clip != null ? Res.Anims.Get(Gender, clip) : null;
            if (e == null || e.offset.sqrMagnitude < 1e-4f) return;
            var off = transform.rotation * e.offset;
            off.y = 0f;
            var to = transform.position + off;
            float gy = Level.GroundY(to + Vector3.up * 0.5f, float.NaN);
            if (float.IsNaN(gy) || Mathf.Abs(gy - transform.position.y) > 0.35f) return;
            off.y = gy - transform.position.y;
            glideOffset = off;
            glideTotal = glideLeft = OnceFade;
        }

        void StartMove(Vector3[] pts, string clip, float speedMul)
        {
            if (pts == null || pts.Length == 0) { NextAct(); return; }
            movePts = pts;
            pathIndex = 0;
            // start from the closest point ahead
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                float d = (pts[i] - transform.position).sqrMagnitude;
                if (d < best) { best = d; pathIndex = i; }
            }
            if (pathIndex < pts.Length - 1 && best < 0.25f) pathIndex++;
            // already on the route part of the way along a stretch (resuming after a glance, a flinch, an
            // investigation): on to the end of that stretch, not back to the point he passed
            if (pts.Length > 1)
            {
                var pos = transform.position;
                int seg = -1;
                float segBest = float.MaxValue, segT = 0f;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    var ab = pts[i + 1] - pts[i]; ab.y = 0f;
                    var ap = pos - pts[i]; ap.y = 0f;
                    float l2 = ab.sqrMagnitude;
                    if (l2 < 1e-4f) continue;
                    float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / l2);
                    float d = (ap - ab * t).sqrMagnitude;
                    if (d < segBest) { segBest = d; seg = i; segT = t; }
                }
                if (seg >= 0 && segBest < 1f && segT > 0.05f && segT < 0.98f) pathIndex = seg + 1;
            }
            Play(clip, glideLeft > 0f ? OnceFade : 0.3f);
            moveSpeed = ClipSpeed(clip) * speedMul;
            hasTargetYaw = false;
            stuckTime = 0;
        }

        public void SetHidden(bool h)
        {
            hidden = h;
            if (model != null) model.SetActive(!h);
        }

        // ------------------------------------------------------------------ update
        // ---- leg length: every clip was made on one man's or one woman's skeleton (hips 0.999 m / 0.804 m above the
        // feet). Played on someone else as is, it puts their hips at that height: a short man or a woman on a man's clip
        // would float a hand's breadth above the ground, a tall man sink into it. After the clips are applied, the hips'
        // offset from the feet is scaled to this body (blended by the weights of the clips playing); seated clips keep
        // the seat's height.
        const float ClipHipsM = 0.9993f, ClipHipsF = 0.8043f;
        float restHips = -1f;
        readonly List<AnimationState> states = new List<AnimationState>();
        Vector3 hipsWritten;
        bool hipsValid;

        void FitHips()
        {
            if (Pelvis == null || restHips < 0.3f || anim == null || !anim.enabled) return;
            var lp = Pelvis.localPosition;
            if (hipsValid && (lp - hipsWritten).sqrMagnitude < 1e-12f) return;      // not animated since we last set it
            float num = 0f, den = 0f;
            for (int i = 0; i < states.Count; i++)
            {
                var st = states[i];
                if (st == null || !st.enabled || st.weight <= 0.0001f) continue;
                // seated: the seat sets the height of the hips, not the legs
                string n = st.name;
                bool seated = n.IndexOf("_sit_", StringComparison.Ordinal) == 1 || n.EndsWith("sleep_desk", StringComparison.Ordinal);
                num += st.weight * (seated ? 1f : restHips / (n.Length > 0 && n[0] == 'f' ? ClipHipsF : ClipHipsM));
                den += st.weight;
            }
            if (den <= 0.0001f) return;
            float k = Mathf.Clamp(num / den, 0.75f, 1.3f);
            hipsWritten = lp * k;
            hipsValid = true;
            Pelvis.localPosition = hipsWritten;
        }

        void LateUpdate()
        {
            if (!Dead || deathClip != null) FitHips();
            ApplyKick();                                   // before the death clip measures the bones: the jolt carries into the fall
            if (Dead && deathClip != null) UpdateDeathClip();
            var p = transform.position;
            float dt = Time.deltaTime;
            if (hasLastWorldPos && dt > 1e-5f) WorldVelocity = (p - lastWorldPos) / dt;
            lastWorldPos = p;
            hasLastWorldPos = true;
        }

        void Update()
        {
            if (Dead || Escaped) return;
            float dt = Time.deltaTime;
            if (glideLeft > 0f && dt > 0f)
            {
                float step = Mathf.Min(dt, glideLeft);
                transform.position += glideOffset * (step / glideTotal);
                glideLeft -= step;
            }
            if (Aiming) UpdateAim(dt);
            if (flinchLeft > 0f)
            {
                // crouched for a moment (gunfire close by): the walk and the idle's clock wait, then carry on
                flinchLeft -= dt;
                Velocity = Vector3.zero;
                if (!Down && !Riding)
                    transform.rotation = Quaternion.Euler(0, Mathf.MoveTowardsAngle(transform.eulerAngles.y, flinchYaw, 220f * dt), 0);
                if (flinchLeft <= 0f)
                {
                    Play(flinchResume ?? "idle", 0.35f);
                    // back to the way the routine had him facing
                    var cur = CurrentAct;
                    if (cur != null && cur.kind == Act.Kind.Idle && cur.hasYaw && !Riding && !Down) { targetYaw = cur.yaw; hasTargetYaw = true; }
                }
            }
            else if (movePts != null)
            {
                var target = movePts[pathIndex];
                var pos = transform.position;
                var to = target - pos;
                var flat = new Vector3(to.x, 0, to.z);
                float step = moveSpeed * dt;
                if (flat.magnitude <= Mathf.Max(0.05f, step))
                {
                    transform.position = target;
                    pathIndex++;
                    if (pathIndex >= movePts.Length)
                    {
                        movePts = null;
                        Velocity = Vector3.zero;
                        NextAct();
                        return;
                    }
                }
                else
                {
                    var dir = flat.normalized;
                    // follow height of the path segment
                    var prev = pathIndex > 0 ? movePts[pathIndex - 1] : pos;
                    float segLen = new Vector3(target.x - prev.x, 0, target.z - prev.z).magnitude;
                    var next = pos + dir * step;
                    float t = segLen > 0.001f ? 1f - new Vector3(target.x - next.x, 0, target.z - next.z).magnitude / segLen : 1f;
                    next.y = Mathf.Lerp(prev.y, target.y, Mathf.Clamp01(t));
                    Velocity = (next - pos) / Mathf.Max(1e-4f, dt);
                    transform.position = next;
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    float turnRate = moveSpeed > 2.5f ? 540f : (moveSpeed < 0.6f ? 120f : 300f);
                    transform.rotation = Quaternion.Euler(0, Mathf.MoveTowardsAngle(transform.eulerAngles.y, yaw, turnRate * dt), 0);
                    // sync locomotion clip speed with the actual speed (no foot sliding)
                    if (anim != null && currentClip != null && currentEntry != null && currentEntry.speed > 0.05f)
                        anim[currentClip].speed = Mathf.Clamp(moveSpeed / currentEntry.speed, 0.6f, 1.6f);
                }
            }
            else
            {
                Velocity = Vector3.zero;
                if (hasTargetYaw && !Down && !Riding)
                {
                    float y = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, 160f * dt);
                    transform.rotation = Quaternion.Euler(0, y, 0);
                    if (Mathf.Abs(Mathf.DeltaAngle(y, targetYaw)) < 0.5f) hasTargetYaw = false;
                }
                if (!float.IsInfinity(actTimer))
                {
                    actTimer -= dt;
                    if (actTimer <= 0) NextAct();
                }
            }
            if (!investigating && Suspicion > 0f && !Panicking && Time.time > calmAt) Suspicion = 0f;
            // look for bodies / threats a few times per second
            if (Time.time >= nextLookCheck)
            {
                nextLookCheck = Time.time + 0.35f + UnityEngine.Random.value * 0.2f;
                CheckForBodies();
            }
        }

        void CheckForBodies()
        {
            if (Panicking || hidden || Incapacitated || Aiming || Scripted || (Combat != null && Combat.Engaged) || IsLookout) return;
            foreach (var other in All)
            {
                if (other == this || other.Escaped || other.hidden) continue;
                bool body = other.Dead || other.Down;
                if (!body) continue;
                float since = Time.time - (other.Dead ? other.DeathTime : other.DownTime);
                if (since < 0.4f) continue;
                var p = other.Pelvis != null ? other.Pelvis.position : other.transform.position;
                if (other.Dead && p.y < Level.WaterY + 0.25f) continue;      // V2: a body in the water is gone (hidden)
                if (CanSee(p, SightRange))
                {
                    MissionRuntime.I?.OnBodySeen(this, other);
                    React(p, true);
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ where he will be (ballistics)
        Collider[] hitCols;

        /// <summary>His hit zones (ragdoll colliders, or the single capsule of a character without a skeleton).</summary>
        public Collider[] HitColliders
        {
            get
            {
                if (hitCols == null) hitCols = ragdoll != null ? ragdoll.colliders.ToArray() : GetComponents<Collider>();
                return hitCols;
            }
        }

        /// <summary>
        /// How far his body will have moved <paramref name="t"/> seconds from now if he keeps to what he is doing: along
        /// the route he is walking (round its corners, stopping at its end), with whatever carries him (a car, a cabin,
        /// a boat: its present velocity), or nowhere. A bullet in flight meets him there.
        /// </summary>
        public Vector3 PredictOffset(float t)
        {
            // a body in a moving car or cabin still travels with it
            if (t <= 0f || Escaped || hidden || (Dead && !Riding)) return Vector3.zero;
            // crouched for a moment (a flinch): he stays put until it is over, then walks on
            if (flinchLeft > 0f && !Dead)
            {
                t -= flinchLeft;
                if (t <= 0f) return Vector3.zero;
            }
            if (movePts == null || moveSpeed < 0.01f || pathIndex >= movePts.Length)
            {
                var wv = WorldVelocity;
                return wv.sqrMagnitude > 0.04f ? wv * t : Vector3.zero;
            }
            float left = moveSpeed * t;
            var p = transform.position;
            for (int i = pathIndex; i < movePts.Length && left > 1e-4f; i++)
            {
                var d = movePts[i] - p;
                float len = new Vector3(d.x, 0f, d.z).magnitude;
                if (len <= left) { p = movePts[i]; left -= len; continue; }
                p += d * (left / Mathf.Max(1e-4f, len));
                left = 0f;
            }
            return p - transform.position;
        }

        public Vector3 EyePos => HeadBone != null ? HeadBone.position + Vector3.up * 0.08f : transform.position + Vector3.up * Height * 0.93f;
        public Vector3 AimPoint => HeadBone != null ? HeadBone.position + Vector3.up * 0.06f : transform.position + Vector3.up * (Height - 0.1f);
        public Vector3 ChestPoint => Pelvis != null ? Pelvis.position + Vector3.up * (Down || Sedated ? 0.05f : 0.3f) : transform.position + Vector3.up * Height * 0.62f;
        public Vector3 HandPoint => RightHand != null ? RightHand.position : ChestPoint + transform.forward * 0.4f;

        public bool CanSee(Vector3 p, float range)
        {
            var eye = EyePos;
            var d = p - eye;
            if (d.sqrMagnitude > range * range) return false;
            var flat = new Vector3(d.x, 0, d.z);
            if (flat.sqrMagnitude > 4f && Vector3.Angle(transform.forward, flat) > 75f) return false;
            return !Physics.Linecast(eye, p + Vector3.up * 0.2f, Layers.WorldMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ hearing / investigating
        /// <summary>
        /// A sound at <paramref name="source"/>. Targets first walk out to see what it was (that is the player's chance
        /// to lure them out of cover) and run only when it happens again or very close; guards investigate before they
        /// radio it in; bystanders glance over and only panic when it is right next to them. A rifle crack right beside
        /// someone scares everybody.
        /// </summary>
        public void Hear(Vector3 source, NoiseKind kind)
        {
            if (Dead || Escaped || hidden || Incapacitated || Aiming) return;
            var d = source - transform.position; d.y = 0;
            float dist = d.magnitude;
            bool guard = Role == Role.Guard || Role == Role.Bodyguard;
            if (Riding)
            {
                // on a moving cabin / vehicle: only a loud or close sound gets a reaction (in the seat)
                if (kind == NoiseKind.Explosion || kind == NoiseKind.Alarm || kind == NoiseKind.Shot || dist < 6f) React(source);
                return;
            }
            if (kind == NoiseKind.Gunfire)
            {
                // their own men firing at the nest: gunmen join in, hitmen carry on with their job, the people the
                // contract is about crouch where they are for a moment (the fire is not aimed at them: they do not run
                // off), bystanders panic
                if (guard) { React(source); return; }
                if (HiddenTarget || Scripted) return;
                if (IsTarget || Protected || Role == Role.Decoy) { Flinch(source); return; }
                if (Panicking) return;
                React(source);
                return;
            }
            if (kind == NoiseKind.Explosion || kind == NoiseKind.Alarm) { React(source); return; }
            if (Panicking) return;
            if (kind == NoiseKind.Shot)
            {
                // the crack of the shot close by: there is a shooter
                if (guard) { React(source); return; }
                if (IsTarget || Role == Role.Decoy || dist < 7f) { React(source); return; }
                Glance(source);
                return;
            }
            if (guard)
            {
                if (Alerted) return;
                if (dist < 6f || Suspicion >= 1f) { React(source); return; }
                Investigate(source, null, 5f);
                return;
            }
            if (IsTarget || Role == Role.Decoy)
            {
                if (kind == NoiseKind.Car && CarPath != null && CarPath.Length > 0 && !investigating) { Investigate(source, CarPath, 7f); return; }
                if (dist < 3.5f || Suspicion >= 1f) { React(source); return; }
                Investigate(source, InvestigatePath, 6.5f);
                return;
            }
            if (dist < 6.5f) { React(source); return; }
            Glance(source);
        }

        /// <summary>Stop, look towards the sound, walk a few careful steps / the authored route, look around, go back.</summary>
        public void Investigate(Vector3 source, Vector3[] route, float lookSeconds)
        {
            if (Dead || Escaped || Panicking || Incapacitated) return;
            if (Riding) return;
            Suspicion = Mathf.Max(Suspicion, 1f);
            if (!investigating)
            {
                savedPlan = new List<Act>(plan);
                savedIndex = Mathf.Max(0, planIndex);
                savedPos = transform.position;
            }
            investigating = true;
            float yaw = YawTo(source);
            var path = route != null && route.Length > 0 ? route : InvestigateRoute(source);
            var acts = new List<Act> { Act.IdleFacing("idle_look", yaw, 1.0f) };
            var end = transform.position;
            if (path != null && path.Length > 0)
            {
                // authored routes are long (walking out to look, checking the car): a normal walk; a few careful
                // steps otherwise
                acts.Add(Act.Move(path, route != null && route.Length > 0 ? "walk" : "walk_careful"));
                end = path[path.Length - 1];
            }
            var dd = source - end; dd.y = 0;
            float lookYaw = dd.sqrMagnitude > 1f ? Mathf.Atan2(dd.x, dd.z) * Mathf.Rad2Deg : yaw;
            acts.Add(Act.IdleFacing(MissionRuntime.I != null && MissionRuntime.I.IsNight ? "flashlight" : "idle_look", lookYaw, lookSeconds));
            if (Role != Role.Guard && Role != Role.Bodyguard) acts.Add(Act.IdleFacing("phone_talk", lookYaw, 2.5f));
            acts.Add(Act.Do(() => ReturnFromInvestigation(path)));
            SetPlan(acts);
        }

        /// <summary>Drops the routine saved by an investigation so a mission script can give new orders.</summary>
        public void EndInvestigation()
        {
            investigating = false;
            savedPlan = null;
            calmAt = Time.time + 25f;
        }

        float flinchUntil, flinchLeft, flinchYaw;
        string flinchResume;

        /// <summary>Gunfire close by that is not aimed at him: down on his haunches for a moment, then on with what he
        /// was doing — his routine only waits (an idle keeps the time it had left, a walk carries on), so a long
        /// firefight does not keep him from his next step. Once every few seconds at most; seated people stay seated.</summary>
        public void Flinch(Vector3 source)
        {
            if (Dead || Escaped || Incapacitated || Panicking || Riding || Aiming || flinchLeft > 0f || Time.time < flinchUntil) return;
            var cur = CurrentAct;
            if (cur != null && cur.once) return;                  // in the middle of a stagger or a fall
            flinchUntil = Time.time + 7f;
            if (Seated) return;
            flinchResume = cur != null && cur.kind == Act.Kind.Move ? (cur.clip ?? "walk")
                         : (cur != null && cur.kind == Act.Kind.Idle ? (cur.clip ?? "idle") : "idle");
            flinchYaw = YawTo(source);
            flinchLeft = 1.5f;
            Velocity = Vector3.zero;
            Play(HasClip("duck") ? "duck" : "idle_look", 0.15f);
        }

        /// <summary>Turn towards a sound for a moment without leaving the routine.</summary>
        public void Glance(Vector3 source)
        {
            if (investigating || Dead || Escaped || Incapacitated) return;
            savedPlan = new List<Act>(plan);
            savedIndex = Mathf.Max(0, planIndex);
            savedPos = transform.position;
            investigating = true;
            SetPlan(Act.IdleFacing("idle_look", YawTo(source), 2.5f), Act.Do(ResumeRoutine));
        }

        void ReturnFromInvestigation(Vector3[] path)
        {
            OnInvestigateEnd?.Invoke(this);
            if (Riding) { ResumeRoutine(); return; }
            var back = new List<Vector3>();
            if (path != null && path.Length > 1)
            {
                // walk the route back only as far as the point closest to where he left his routine
                int k = 0;
                float best = float.MaxValue;
                for (int i = 0; i < path.Length - 1; i++)
                {
                    float d = (path[i] - savedPos).sqrMagnitude;
                    if (d < best) { best = d; k = i; }
                }
                for (int i = path.Length - 2; i >= k; i--) back.Add(path[i]);
            }
            back.Add(savedPos);
            SetPlan(Act.Move(back.ToArray(), "walk"), Act.Do(ResumeRoutine));
        }

        void ResumeRoutine()
        {
            investigating = false;
            calmAt = Time.time + 25f;
            if (savedPlan != null && savedPlan.Count > 0)
            {
                planVersion++;
                plan.Clear();
                plan.AddRange(savedPlan);
                planIndex = Mathf.Clamp(savedIndex, 0, plan.Count) - 1;
                savedPlan = null;
                NextAct();
            }
            else
            {
                savedPlan = null;
                planVersion++;
                StartIdle("idle", 0);
            }
        }

        /// <summary>Straight, walkable steps towards the sound (stops short of it).</summary>
        Vector3[] InvestigateRoute(Vector3 source)
        {
            var from = transform.position;
            var d = source - from; d.y = 0;
            float dist = d.magnitude;
            if (dist < 1.5f) return null;
            var dir = d / dist;
            float want = Mathf.Min(dist - 2.5f, 18f);
            foreach (float k in new[] { 1f, 0.7f, 0.45f })
            {
                float len = want * k;
                if (len < 2.5f) break;
                var p = from + dir * len;
                if (Level.WalkableLine(from, p, out float ey)) return new[] { new Vector3(p.x, ey, p.z) };
            }
            return null;
        }

        public float YawTo(Vector3 p)
        {
            var d = p - transform.position; d.y = 0;
            return d.sqrMagnitude > 0.01f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
        }

        /// <summary>Comes back out after hiding in a building (second chance for the player). <paramref name="panicked"/>:
        /// still fleeing (ignores further noises).</summary>
        public void Reappear(bool panicked = false)
        {
            Escaped = false;
            Panicking = panicked;
            if (panicked) Alerted = true;
            investigating = false;
            savedPlan = null;
            SetHidden(false);
        }

        // ------------------------------------------------------------------ reactions
        /// <summary>Something scary happened near this character (impact, body, explosion).</summary>
        public void React(Vector3 source, bool sawBody = false)
        {
            if (Dead || Escaped || Incapacitated) return;
            investigating = false;
            savedPlan = null;
            if (Aiming) return;                                  // a hitman taking aim does not stop for noises
            if (Role == Role.Guard || Role == Role.Bodyguard)
            {
                if (Alerted)
                {
                    if (Combat != null && !Scripted && !Riding)
                    {
                        if (Combat.CanFight) Combat.Poke(source);
                        else if (Wounded && WoundZone == BodyZone.Arm && !Down && !Combat.Engaged && PlanVersion != hidPlan) HideFrom(source);
                    }
                    return;
                }
                Alerted = true;
                if (Riding)
                {
                    // in a cabin: draws where he stands; in a seat: stays put
                    if (!Seated) SetPlan(Act.Idle("pull_gun", 1.4f), Act.Idle(HasClip("alert") ? "alert" : "guard_pose", 0));
                    MissionRuntime.I?.OnGuardAlert(this, sawBody);
                    return;
                }
                // armed: into the gunfight (cover, then looking for the shooter / firing on the nest); his gun shot out of
                // his hand: into cover
                if (Combat != null && !Scripted)
                {
                    if (Combat.CanFight) Combat.Engage(source);
                    else HideFrom(source);
                    MissionRuntime.I?.OnGuardAlert(this, sawBody);
                    return;
                }
                var dir = source - transform.position; dir.y = 0;
                float yaw = dir.sqrMagnitude > 0.01f ? Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
                // look towards the danger, draw the weapon, then a few careful steps (same floor, nothing in the way)
                var g0 = Gun;
                var plan = new List<Act> { Act.IdleFacing("pull_gun", yaw, 0.5f), Act.Do(() => { if (g0 != null) g0.Drawn = true; }),
                                           Act.IdleFacing("pull_gun", yaw, 0.9f), Act.Idle("guard_pose", 2.5f) };
                if (dir.sqrMagnitude > 1f && !Riding)
                {
                    var step = transform.position + Vector3.ClampMagnitude(dir * 0.6f, 4f);
                    if (Level.WalkableLine(transform.position, step, out float sy)) { step.y = sy; plan.Add(Act.Move(new[] { step }, "walk_careful")); }
                }
                plan.Add(Act.IdleFacing(HasClip("alert") ? "alert" : "idle_look", yaw, 0));
                SetPlan(plan);
                MissionRuntime.I?.OnGuardAlert(this, sawBody);
                return;
            }
            if (Panicking) return;
            Panicking = true;
            Alerted = true;
            if (Riding)
            {
                // trapped on a moving cabin (ducks and cowers on the floor) or in a seat (stays in it)
                if (!Seated) SetPlan(Act.Idle("duck", 1.0f), Act.Idle("cower", 0));
                return;
            }
            if (IsTarget || Role == Role.Decoy || Protected)
            {
                if (IsTarget) MissionRuntime.I?.OnTargetSpooked(this);
                var flee = FleeAlong != null && FleeAlong.Length > 0 ? MissionRuntime.From(FleeAlong, transform.position) : FleePath;
                if (flee != null && flee.Length > 0)
                {
                    string run = Wounded && WoundZone == BodyZone.Leg ? "walk_limp" : "run_panic";
                    if (FleeEndPause > 0f)
                        SetPlan(Act.Idle("duck", 0.9f), Act.Move(flee, run), Act.IdleFacing("idle_look", FleeEndYaw, FleeEndPause), Act.Escape());
                    else
                        SetPlan(Act.Idle("duck", 0.9f), Act.Move(flee, run), Act.Escape());
                    return;
                }
            }
            // run to the nearest reachable exit; or into cover close by (out of the nest's sight); nowhere to go (deck,
            // tower, boxed in) -> duck and cower where he is
            Vector3 exit = transform.position;
            bool hide = UnityEngine.Random.value < 0.35f;
            if (!CanFlee || hide || MissionRuntime.I == null || !MissionRuntime.I.TryFleeTarget(transform.position, source, out exit))
            {
                var spot = CanFlee && Firefight.I != null ? CoverFinder.HidingPlace(this, Firefight.I.Nest, 9f) : null;
                if (spot.HasValue && (spot.Value - transform.position).sqrMagnitude > 0.5f)
                {
                    SetPlan(Act.Idle("duck", 0.6f), Act.Move(new[] { spot.Value }, "run_panic"), Act.Idle(HasClip("crouch_hide") ? "crouch_hide" : "cower", 0));
                    return;
                }
                SetPlan(Act.Idle("duck", 1.2f), Act.Idle("cower", 0));
                return;
            }
            SetPlan(Act.Idle("duck", 0.7f), Act.Move(new[] { exit }, "run_panic"), Act.Idle("cower", 0));
        }

        public void ForceEscape() => Escape();

        /// <summary>Gets out of the nest's sight: cover close by (crouching), else down where he is.</summary>
        void HideFrom(Vector3 source)
        {
            var spot = Firefight.I != null ? CoverFinder.HidingPlace(this, Firefight.I.Nest, 9f) : null;
            if (spot.HasValue && (spot.Value - transform.position).sqrMagnitude > 0.5f)
                SetPlan(Act.Idle("duck", 0.5f), Act.Move(new[] { spot.Value }, "run_panic"), Act.Idle(HasClip("crouch_hide") ? "crouch_hide" : "cower", 0));
            else SetPlan(Act.Idle("duck", 1.0f), Act.Idle(HasClip("crouch_hide") ? "crouch_hide" : "cower", 0));
            hidPlan = PlanVersion;             // hiding already: the next noise does not send him running again
        }

        int hidPlan = -1;

        /// <summary>For mission scripts that give a guard his own orders: he counts as alerted (no automatic reaction, no
        /// radio alarm when he sees a body).</summary>
        public void MarkAlerted() { Alerted = true; Scripted = true; investigating = false; savedPlan = null; }

        /// <summary>Acting on the mission script's orders (rushing to someone, executing someone): ignores bodies and does
        /// not radio the alarm.</summary>
        public bool Scripted;

        void Escape()
        {
            if (Dead || Escaped) return;
            Escaped = true;
            Aiming = false;
            movePts = null;
            Velocity = Vector3.zero;
            SetHidden(true);
            OnArrivedEscape?.Invoke(this);
            MissionRuntime.I?.OnEscaped(this);
        }

        // ------------------------------------------------------------------ armed attacker
        /// <summary>
        /// A hitman going for <paramref name="victim"/>: walks the approach, draws, takes aim for
        /// <paramref name="aimSeconds"/> and fires (<paramref name="fire"/>). Killing, wounding or sedating him first stops it;
        /// an arm hit makes him drop the gun.
        /// </summary>
        public void Attack(NPC victim, Vector3[] approach, string walkClip, float aimSeconds, Action<NPC> fire)
        {
            Armed = true;
            // on the script's orders from now on: the victim lying there is no "body" to report, a guard does not stop
            // for noises to take cover, nobody radios the alarm
            Scripted = true;
            if (Role == Role.Guard || Role == Role.Bodyguard) Alerted = true;
            investigating = false;
            savedPlan = null;
            var acts = new List<Act>();
            if (approach != null && approach.Length > 0) acts.Add(Act.Move(approach, string.IsNullOrEmpty(walkClip) ? "walk" : walkClip));
            acts.Add(Act.Do(() => BeginAim(victim, aimSeconds, fire)));
            SetPlan(acts);
        }

        /// <summary>Starts aiming right away (from where he stands).</summary>
        public void BeginAim(NPC victim, float aimSeconds, Action<NPC> fire, string holdClip = null)
        {
            if (Incapacitated || Escaped || victim == null) return;
            Armed = true;
            Aiming = true;
            Scripted = true;
            aimAtPoint = false;
            AimVictim = victim;
            aimTotal = aimLeft = Mathf.Max(0.5f, aimSeconds);
            AimProgress = 0f;
            aimFire = fire;
            float yaw = YawTo(victim.transform.position);
            var g = Gun;
            if (g != null)
            {
                g.Aim = () => victim != null ? victim.ChestPoint : transform.position + transform.forward * 10f;
                g.Aiming = true;
            }
            if (!string.IsNullOrEmpty(holdClip)) SetPlan(Act.IdleFacing(holdClip, yaw, 0));
            else if (g != null)
            {
                if (g.Drawn) SetPlan(Act.IdleFacing(g.AimClip(false), yaw, 0));
                else SetPlan(Act.IdleFacing("pull_gun", yaw, 0.45f), Act.Do(() => g.Drawn = true), Act.IdleFacing("pull_gun", yaw, 0.55f), Act.IdleFacing(g.AimClip(false), yaw, 0));
            }
            else SetPlan(Act.IdleFacing("pull_gun", yaw, 1.1f), Act.IdleFacing(HasClip("hand_on_gun") ? "hand_on_gun" : "guard_pose", yaw, 0));
            MissionRuntime.I?.OnAttackerAiming(this);
        }

        /// <summary>Takes aim at a point (the sniper nest): the same countdown and HUD warning as <see cref="BeginAim"/>,
        /// without a victim; <paramref name="fire"/> decides what the shot does.</summary>
        public void BeginAimAtPoint(Vector3 point, float aimSeconds, Action<NPC> fire, string holdClip = null)
        {
            if (Incapacitated || Escaped) return;
            Armed = true;
            Aiming = true;
            Scripted = true;
            aimAtPoint = true;
            aimPoint = point;
            AimVictim = null;
            aimTotal = aimLeft = Mathf.Max(0.5f, aimSeconds);
            AimProgress = 0f;
            aimFire = fire;
            float yaw = YawTo(point);
            var g = Gun;
            if (g != null && string.IsNullOrEmpty(holdClip))
            {
                g.Drawn = true;
                g.Aiming = true;
                g.Aim = () => aimPoint;
                SetPlan(Act.IdleFacing(g.AimClip(false), yaw, 0));
            }
            else SetPlan(Act.IdleFacing(string.IsNullOrEmpty(holdClip) ? (HasClip("hand_on_gun") ? "hand_on_gun" : "guard_pose") : holdClip, yaw, 0));
            MissionRuntime.I?.OnAttackerAiming(this);
        }

        void UpdateAim(float dt)
        {
            if (Incapacitated || (!aimAtPoint && (AimVictim == null || AimVictim.Dead || AimVictim.Escaped)))
            {
                Aiming = false;
                if (aimAtPoint) ThreatToPlayer = 0f;
                return;
            }
            targetYaw = aimAtPoint ? YawTo(aimPoint) : YawTo(AimVictim.transform.position);
            hasTargetYaw = true;
            // aiming at the nest: his clock stops while the player cannot do anything about it (kill cam, a cutaway)
            if (!(aimAtPoint && KillCam.Active)) aimLeft -= dt;
            AimProgress = Mathf.Clamp01(1f - aimLeft / aimTotal);
            if (aimAtPoint) ThreatToPlayer = Mathf.Max(0.02f, AimProgress);
            if (aimLeft > 0f) return;
            Aiming = false;
            if (aimAtPoint) ThreatToPlayer = 0f;
            var gh = Gun;
            if (gh != null) gh.Aiming = false;
            var f = aimFire;
            aimFire = null;
            f?.Invoke(this);
        }

        public void StopAiming()
        {
            if (Aiming && aimAtPoint) ThreatToPlayer = 0f;
            Aiming = false;
            aimFire = null;
            var g = Gun;
            if (g != null) g.Aiming = false;
        }

        // ------------------------------------------------------------------ wounds, darts, death
        /// <summary>
        /// A bullet in an arm or a leg. Leg: he goes down, lies there for a moment, then crawls away (targets towards their
        /// escape, bystanders to cover). Arm: a hit reaction, then the usual panic / alert (guards drop their aim).
        /// </summary>
        public void Wound(Vector3 point, Vector3 dir, BodyPart part, BodyZone zone)
        {
            if (Dead || Escaped) return;
            Wounded = true;
            Kick(point, dir, zone, 0.9f);
            if (zone == BodyZone.Leg || WoundZone != BodyZone.Leg) WoundZone = zone;
            Aiming = false;
            ThreatToPlayer = 0f;
            aimFire = null;
            investigating = false;
            savedPlan = null;
            FX.BloodHit(point, dir, zone, false, false, Riding);
            if (!KillCam.Active || KillCam.IsCutaway) Sfx.PlayAt(Sfx.Flesh, point, 0.8f);
            Sfx.PlayAt(Gender == "f" ? Sfx.GroanF : Sfx.Groan, point, 0.7f);
            BloodTrail.Attach(this, zone == BodyZone.Leg ? 1f : 0.45f);
            if (Sedated)
            {
                // asleep (or falling asleep): he stays on the ground, no crawling away
                if (!Down) LieDown();
            }
            else if (Down)
            {
                // already on the ground after a leg wound: keeps doing what he was doing (lying / crawling)
            }
            else if (zone == BodyZone.Leg && Riding && Seated)
            {
                // hit in the leg in his seat: slumps where he sits
                Down = true;
                DownTime = Time.time;
                Panicking = true;
                Alerted = true;
                SetPlan(Act.Idle(HasClip("sleep_desk") ? "sleep_desk" : "sit_idle", 0));
            }
            else if (zone == BodyZone.Leg)
            {
                if (!Down)
                {
                    Down = true;
                    DownTime = Time.time;
                    Panicking = true;
                    Alerted = true;
                    var acts = new List<Act> { Act.Once("fall_down"), Act.Idle("wounded_side", UnityEngine.Random.Range(3.5f, 5.5f)) };
                    var crawl = Riding ? null : CrawlPath(point);
                    if (crawl != null)
                    {
                        acts.Add(Act.Move(crawl, "crawl"));
                        if (IsTarget && FleePath != null && FleePath.Length > 0 && !NoCrawl) acts.Add(Act.Escape());
                        else acts.Add(Act.Idle("wounded_prone", 0));
                    }
                    else acts.Add(Act.Idle("wounded_side", 0));
                    SetPlan(acts);
                }
            }
            else
            {
                Panicking = false;
                var src = point - dir.normalized * 30f;
                if (Riding) React(src);                          // no stagger in a cabin or a seat
                else SetPlan(Act.Once(HitClip(), 1.3f), Act.Do(() => React(src)));
            }
            OnWound?.Invoke(this, zone);
            MissionRuntime.I?.OnWounded(this, zone);
        }

        /// <summary>
        /// V2: a round stopped by his plate carrier: a hard shove and a stagger, a spark and a groan, no wound. A gunman
        /// gets back into the fight after it (the player needs the head, armour piercing rounds or a heavier rifle).
        /// </summary>
        public void ArmorStop(Vector3 point, Vector3 dir)
        {
            if (Dead || Escaped) return;
            Kick(point, dir, BodyZone.Torso, 1f);
            FX.Dust(point, -dir.normalized, true);
            if (!KillCam.Active || KillCam.IsCutaway) Sfx.PlayAt(Sfx.ImpactMetal, point, 0.9f);
            Sfx.PlayAt(Gender == "f" ? Sfx.GroanF : Sfx.Groan, point, 0.5f);
            ArmorHits++;
            investigating = false;
            savedPlan = null;
            var src = point - dir.normalized * 30f;
            if (Riding || Aiming) { React(src); return; }
            SetPlan(Act.Once(HitClip(), 1.1f), Act.Do(() => React(src)));
        }

        /// <summary>V2: wears body armour (torso shots from light rifles do not get through).</summary>
        public bool Armored;
        public int ArmorHits;

        static readonly string[] HitClips = { "hit_gunshot", "hit2", "hit_waist" };

        /// <summary>A flinch for a bullet in the arm or the shoulder (one of a few, so a crowd does not react in unison).</summary>
        string HitClip()
        {
            int k = UnityEngine.Random.Range(0, HitClips.Length);
            for (int i = 0; i < HitClips.Length; i++)
            {
                var c = HitClips[(k + i) % HitClips.Length];
                if (HasClip(c)) return c;
            }
            return "hit_gunshot";
        }

        /// <summary>Where a man shot in the leg drags himself: along his escape route (targets), a few metres away from the
        /// shooter otherwise; null = he stays where he fell.</summary>
        Vector3[] CrawlPath(Vector3 hitPoint)
        {
            if (NoCrawl || Role == Role.Guard || Role == Role.Bodyguard) return null;
            if (IsTarget && FleePath != null && FleePath.Length > 0) return FleePath;
            var away = transform.position - (Sniper.I != null ? Sniper.I.transform.position : hitPoint);
            away.y = 0;
            if (away.sqrMagnitude < 0.01f) return null;
            away.Normalize();
            foreach (float len in new[] { 5f, 3f })
            {
                var p = transform.position + away * len;
                if (Level.WalkableLine(transform.position, p, out float ey)) return new[] { new Vector3(p.x, ey, p.z) };
            }
            return null;
        }

        /// <summary>A tranquilizer dart: a few stumbling steps, then he collapses and sleeps.</summary>
        public void Sedate(Vector3 point, Vector3 dir)
        {
            if (Dead || Escaped || Sedated) return;
            Sedated = true;
            Aiming = false;
            ThreatToPlayer = 0f;
            aimFire = null;
            investigating = false;
            savedPlan = null;
            Panicking = false;
            DownTime = Time.time;
            FX.Dart(point, dir);
            if (!KillCam.Active || KillCam.IsCutaway) Sfx.PlayAt(Sfx.Dart, point, 0.8f);
            var acts = new List<Act>();
            if (!Down && Riding && Seated)
            {
                // asleep in his seat
                Down = true;
                acts.Add(Act.Idle(HasClip("sleep_desk") ? "sleep_desk" : "sit_idle", 0));
            }
            else if (!Down)
            {
                acts.Add(Act.Idle(HasClip("pain_head") ? "pain_head" : "idle_look", 0.9f));
                var fwd = transform.forward; fwd.y = 0;
                var ahead = transform.position + fwd.normalized * 1.6f;
                if (!Riding && Level.WalkableLine(transform.position, ahead, out float ey)) acts.Add(Act.Move(new[] { new Vector3(ahead.x, ey, ahead.z) }, "walk_stumble"));
                acts.Add(Act.Do(() => { Down = true; DownTime = Time.time; }));
                acts.Add(Act.Once("fall_down", 0, true));
            }
            else acts.Add(Act.Idle("wounded_prone", 0));
            SetPlan(acts);
            MissionRuntime.I?.OnSedated(this);
        }

        /// <summary>Collapses where he stands (sedated man hit before he fell): down for good.</summary>
        void LieDown()
        {
            SetPlan(Act.Do(() => { Down = true; DownTime = Time.time; }), Act.Once("fall_down", 0, true));
        }

        public void Kill(Vector3 point, Vector3 dir, BodyPart part, bool headshot) => Kill(point, dir, part, headshot, true);

        public void Kill(Vector3 point, Vector3 dir, BodyPart part, bool headshot, bool byPlayer)
        {
            if (Dead || Escaped) return;
            Kick(point, dir, part != null ? part.zone : BodyZone.Torso, headshot ? 0.7f : 0.55f);
            // what the body was doing when the bullet arrived: a runner's stride and a moving car carry on into the fall
            var carry = Riding ? Vector3.zero : WorldVelocity;
            float speed = Velocity.magnitude;
            // mid-stagger (a hit reaction moving the body, its glide still to come): a clip starting from its first
            // frame would snap him back upright where the root is
            var cur = CurrentAct;
            bool staggering = glideLeft > 0f || (cur != null && cur.once && !cur.hold);
            Dead = true;
            Aiming = false;
            ThreatToPlayer = 0f;
            aimFire = null;
            Headshot = headshot;
            DeathTime = Time.time;
            movePts = null;
            glideLeft = 0f;
            var zone = part != null ? part.zone : BodyZone.Torso;
            string clip = ragdoll != null && anim != null && anim.enabled && !staggering ? DeathClip(dir, part, headshot, speed) : null;
            if (clip != null) BeginDeathClip(clip);
            else
            {
                if (anim != null) anim.enabled = false;
                if (ragdoll != null)
                {
                    // a rifle bullet jolts the body, it does not throw it: the legs simply give way (Crumple) and the
                    // small push only decides which way he goes down
                    // V2: barely a push — the legs give way and he folds where he stood
                    float force = (headshot ? 2f : 3f) * (Riding ? 0.5f : 1f);
                    ragdoll.Activate(dir.normalized * force, point, part != null ? part.body : null, carry * 0.15f, null);
                    if (!Riding) ragdoll.Crumple(dir);
                    // a rider's body rides on with the cabin / vehicle: interpolation would pin it to its old place in the world
                    if (Riding)
                        foreach (var rb in ragdoll.bodies)
                            if (rb != null) { rb.interpolation = RigidbodyInterpolation.None; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; }
                }
                else
                {
                    transform.rotation = Quaternion.Euler(-85f, transform.eulerAngles.y, 0);
                }
            }
            FX.BloodHit(point, dir, zone, headshot, true, Riding);
            if (!KillCam.Active || KillCam.IsCutaway) Sfx.PlayAt(Sfx.Flesh, point, 0.8f);     // the kill cam plays its own, slowed down
            MissionRuntime.I?.OnKilled(this, headshot, zone, byPlayer);
            Invoke(nameof(Settle), clip != null ? Mathf.Max(2.5f, deathHandover + 1.2f) : 2.5f);
        }

        /// <summary>
        /// V2: a body never sinks through the ground (on the mountain's snow the limbs slipped through the terrain and the
        /// body vanished) and, once down, it lies still: no late twitches or slides, it is made solid where it lies.
        /// </summary>
        void KeepOnGround()
        {
            float since = Time.time - DeathTime;
            foreach (var rb in ragdoll.bodies)
            {
                if (rb == null || rb.isKinematic) continue;
                var p = rb.position;
                if (p.y < Level.WaterY + 0.3f) continue;                          // the harbour takes it (sinks slowly)
                if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out var h, 3f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    float min = h.point.y + 0.06f;
                    if (p.y < min)
                    {
                        rb.position = new Vector3(p.x, min, p.z);
                        var v = rb.GetPointVelocity(rb.worldCenterOfMass);
                        if (v.y < 0f) rb.AddForce(new Vector3(-v.x * 0.5f, -v.y, -v.z * 0.5f), ForceMode.VelocityChange);
                    }
                }
            }
            if (since > 3.5f && Pelvis.position.y > Level.WaterY + 0.3f && !frozen)
            {
                frozen = true;
                ragdoll.SetKinematic(true);
            }
        }
        bool frozen;

        void Settle()
        {
            if (Pelvis == null || Riding) return;
            var p = Pelvis.position;
            if (p.y < Level.WaterY + 0.2f)
            {
                FX.Splash(new Vector3(p.x, Level.WaterY, p.z));
                // V2: knocked into the water — nobody will find this one
                if (!IsTarget && MissionRuntime.I != null && MissionRuntime.I.IsPlaying) MissionRuntime.I.Say("Ceset suya gömüldü — kimse bulamayacak", Theme.Info, "anchor");
                return;
            }
            float g = Level.GroundY(p, p.y - 0.2f);
            FX.BloodPool(new Vector3(p.x, g, p.z));
        }

        // ------------------------------------------------------------------ V2: the jolt of the hit
        // On top of whatever clip is playing, the bullet's push goes through the skeleton for a moment: the chest is
        // knocked back along the shot (the head with a head shot, the arm with an arm hit), a hit off-centre twists the
        // torso. It peaks in a few hundredths of a second and is gone in about a third of a second; with a death clip it
        // is measured into the bones' speed, so the hand-over to the ragdoll keeps the momentum.
        Transform[] kickBones;
        Transform kickArmL, kickArmR;
        SkinnedMeshRenderer kickRenderer;
        Vector3 kickAxis;
        float kickAmp, kickTwist, kickStart = -10f;
        BodyZone kickZone;
        bool kickLeftArm;
        static readonly float[] KickWeights = { 0.26f, 0.26f, 0.22f, 0.14f, 0.12f };

        /// <summary>The bullet's jolt (also a near miss: <paramref name="strength"/> ~0.3, a flinch).</summary>
        public void Kick(Vector3 point, Vector3 dir, BodyZone zone, float strength)
        {
            if (kickBones == null || Riding && Seated) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            kickAxis = Vector3.Cross(Vector3.up, dir);
            kickZone = zone;
            float amp = zone == BodyZone.Head ? 24f : (zone == BodyZone.Arm ? 10f : (zone == BodyZone.Leg ? 6f : 17f));
            kickAmp = amp * Mathf.Clamp(strength, 0f, 1.5f);
            // off-centre: the push twists the torso round its spine
            var chest = ChestPoint;
            var r = point - chest; r.y = 0f;
            float torque = Vector3.Cross(r, dir).y;
            kickTwist = Mathf.Clamp(torque * 3.2f, -1f, 1f) * kickAmp * 0.7f;
            kickLeftArm = Vector3.Dot(point - chest, transform.right) < 0f;
            kickStart = Time.time;
        }

        void ApplyKick()
        {
            if (kickBones == null) return;
            float t = Time.time - kickStart;
            if (t < 0f || t > 0.45f || (Dead && deathClip == null)) return;
            // only on top of a freshly animated pose: a culled (off-screen) or paused body keeps last frame's bones
            if (Time.timeScale <= 0f || anim == null || !anim.enabled) return;
            if (kickRenderer == null) kickRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (kickRenderer != null && !kickRenderer.isVisible) return;
            const float tau = 0.055f;
            float f = (t / tau) * Mathf.Exp(1f - t / tau);          // 0 → 1 at tau → gone by ~0.4 s
            float a = kickAmp * f;
            var twist = Quaternion.AngleAxis(kickTwist * f, Vector3.up);
            for (int i = 0; i < kickBones.Length; i++)
            {
                var b = kickBones[i];
                if (b == null) continue;
                float w = KickWeights[i];
                if (kickZone == BodyZone.Head) w = i >= 3 ? 0.55f : w * 0.35f;      // the neck and head take a head shot
                else if (kickZone == BodyZone.Leg && i >= 3) w *= 0.3f;
                b.rotation = Quaternion.AngleAxis(a * w, kickAxis) * (i < 3 ? Quaternion.Slerp(Quaternion.identity, twist, 0.33f) : Quaternion.identity) * b.rotation;
            }
            if (kickZone == BodyZone.Arm)
            {
                var arm = kickLeftArm ? kickArmL : kickArmR;
                if (arm != null) arm.rotation = Quaternion.AngleAxis(kickAmp * 2.6f * f, kickAxis) * arm.rotation;
            }
        }

        // ------------------------------------------------------------------ dying on a clip
        // A shot man does not fold like a rag at once: he reels, clutches, drops to his knees, falls back or forwards
        // (which one depends on where the bullet came from and where it hit). The clip carries the reaction and the
        // fall; once the body is down the physics takes over with the clip's own momentum, so he settles on whatever is
        // under him. Head shots mostly drop at once; riders, the seated, the wounded and a body with no room to fall
        // go straight to the ragdoll.
        string deathClip;
        float deathStart, deathHandover;
        /// <summary>Dead and still going down on a death clip (the kill cam stays with him until he is on the ground).</summary>
        public bool DyingOnClip => deathClip != null;
        readonly Dictionary<Rigidbody, Vector3> boneVel = new Dictionary<Rigidbody, Vector3>();
        readonly Dictionary<Rigidbody, Vector3> bonePrev = new Dictionary<Rigidbody, Vector3>();
        static readonly List<string> deathCands = new List<string>();

        // only the grounded ones: a rifle bullet does not throw a man through the air or spin him round (the "blown back",
        // "knock down" and running-dive clips are not used)
        static readonly string[] FrontDeaths = { "death_back", "death_sit", "death_knees", "death_knees", "death_slow" };
        static readonly string[] BackDeaths = { "death_fwd", "death_knees" };

        /// <summary>The death clip for this hit, or null for the plain ragdoll.</summary>
        string DeathClip(Vector3 dir, BodyPart part, bool headshot, float speed)
        {
            if (part == null || Riding || Seated || Down || Sedated || hidden || Res.Anims == null) return null;
            // crouched, kneeling, cowering: he goes down from where he is (a clip would first stand him up)
            if (Pelvis != null && restHips > 0.3f && Pelvis.position.y - transform.position.y < restHips * 0.8f) return null;
            var d = dir; d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) return null;
            d.Normalize();
            var f = transform.forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) return null;
            f.Normalize();
            float front = Vector3.Dot(f, -d);                      // 1: the bullet came from straight ahead of him
            float r = UnityEngine.Random.value;
            deathCands.Clear();
            // V2 (user): a shot man sinks down where he stands — no falls that carry or throw the body. Only the clips
            // that fold him in place (to the knees, a slow slump, sitting down); otherwise the limp body crumples.
            if (r < 0.35f) return null;
            foreach (var c in new[] { r < 0.65f ? "death_knees" : "death_slow", "death_slow", "death_knees", "death_sit" })
                if (HasClip(c) && RoomToFall(c)) return c;
            return null;
#pragma warning disable 0162
            if (speed > 2.4f)
            {
                // running: his legs go and he pitches forward with the stride (the clip, or the limp body with his speed)
                if (r < 0.6f) deathCands.Add("death_fwd");
            }
            else if (headshot)
            {
                if (r < 0.55f) return null;                        // lights out: he crumples where he stands
                deathCands.Add(front > 0.3f ? (r < 0.8f ? "death_knees" : "death_sit") : "death_fwd");
            }
            else if (part.zone == BodyZone.Pelvis)
            {
                deathCands.Add(front > -0.2f ? "death_gut" : "death_fwd");
                deathCands.Add("death_knees");
            }
            else if (front > 0.35f)
            {
                int k = UnityEngine.Random.Range(0, FrontDeaths.Length);
                for (int i = 0; i < FrontDeaths.Length; i++) deathCands.Add(FrontDeaths[(k + i) % FrontDeaths.Length]);
            }
            else if (front < -0.35f)
            {
                int k = UnityEngine.Random.Range(0, BackDeaths.Length);
                for (int i = 0; i < BackDeaths.Length; i++) deathCands.Add(BackDeaths[(k + i) % BackDeaths.Length]);
                deathCands.Add("death_knees");
            }
            else
            {
                // hit from the side: he folds and goes down sideways, the way the bullet pushed him (the limp body does
                // that best); otherwise to his knees
                if (r < 0.6f) return null;
                deathCands.Add("death_knees");
            }
            foreach (var c in deathCands)
                if (HasClip(c) && RoomToFall(c)) return c;
            return null;
        }

        /// <summary>Nothing in the way where the clip throws the body (a wall, a car, a railing) and level floor where it
        /// lands (not the edge of a roof, a quay or a flight of stairs).</summary>
        bool RoomToFall(string clip)
        {
            var e = Res.Anims.Get(Gender, clip);
            if (e == null) return false;
            var off = transform.rotation * e.offset;
            off.y = 0f;
            float len = off.magnitude;
            var dirn = len > 0.05f ? off / len : -transform.forward;
            float reach = len + 0.95f;                           // the hips travel, and the head and shoulders beyond them
            var feet = transform.position;
            foreach (float h in new[] { 0.35f, 1.0f })
                if (Physics.SphereCast(feet + Vector3.up * h - dirn * 0.15f, 0.22f, dirn, out _, reach, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                    return false;
            foreach (float k in new[] { 0.5f, 1f })
            {
                var at = feet + dirn * (reach * k);
                if (!Physics.Raycast(at + Vector3.up * 0.7f, Vector3.down, out var g, 1.4f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
                if (Mathf.Abs(g.point.y - feet.y) > 0.28f) return false;
                if (g.point.y < Level.WaterY + 0.15f) return false;
            }
            return true;
        }

        void BeginDeathClip(string clip)
        {
            deathClip = clip;
            deathStart = Time.time;
            var e = Res.Anims.Get(Gender, clip);
            deathHandover = e != null ? e.length * 0.75f : 1.5f;
            // the clips are acted a little slowly for a rifle hit: played faster (the hand-over comes sooner to match)
            float spd = clip == "death_slow" ? 1.45f : 1.25f;
            if (e != null && e.tags != null)
                foreach (var t in e.tags)
                    if (t != null && t.StartsWith("ho=") && float.TryParse(t.Substring(3), System.Globalization.NumberStyles.Float,
                                                                         System.Globalization.CultureInfo.InvariantCulture, out float ho))
                        deathHandover = Mathf.Clamp(ho, 0.2f, e.length);
            deathHandover /= spd;
            boneVel.Clear();
            bonePrev.Clear();
            Play(clip, 0.08f, true);
            if (anim != null && currentClip != null && anim[currentClip] != null) anim[currentClip].speed = spd;
        }

        /// <summary>After the clip has posed the body this frame: measure how fast each part moves, hand over to the
        /// physics when the clip has brought him down.</summary>
        void UpdateDeathClip()
        {
            float dt = Time.deltaTime;
            if (ragdoll == null) { deathClip = null; return; }
            foreach (var rb in ragdoll.bodies)
            {
                if (rb == null) continue;
                if (dt > 1e-6f && bonePrev.TryGetValue(rb, out var q)) boneVel[rb] = (rb.transform.position - q) / dt;
                bonePrev[rb] = rb.transform.position;
            }
            if (Time.time - deathStart < deathHandover) return;
            deathClip = null;
            if (anim != null) anim.enabled = false;
            ragdoll.Activate(Vector3.zero, Vector3.zero, null, Vector3.zero, boneVel);
        }

        void FixedUpdate()
        {
            // bodies that fall into the harbour sink slowly instead of dropping like stones
            if (!Dead || ragdoll == null || Pelvis == null) return;
            if (ragdoll.Active && !Riding) KeepOnGround();
            if (Pelvis.position.y < Level.WaterY)
                foreach (var rb in ragdoll.bodies)
                    if (rb != null) rb.AddForce(-Physics.gravity * 0.85f * rb.mass - rb.GetPointVelocity(rb.worldCenterOfMass) * rb.mass * 1.5f, ForceMode.Force);
        }
    }
}

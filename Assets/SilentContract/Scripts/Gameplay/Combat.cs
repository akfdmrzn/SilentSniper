using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    public enum GunKind { None, Pistol, Rifle, Smg }

    /// <summary>What an armed man carries: the model, where it is held, how it fires and how far it reaches.</summary>
    public class GunSpec
    {
        public GunKind kind;
        public string prop;
        /// <summary>In the gun's own frame (barrel along -X, up +Y, +Z the shooter's right): right-hand grip, left-hand
        /// support point (two-handed guns), muzzle.</summary>
        public Vector3 grip, fore, muzzle;
        public bool twoHanded;
        public float range;          // farthest it still puts rounds near the nest
        public float acc;            // chance per round to hit a fully exposed sniper, close in
        public float damage;
        public int burst;            // rounds per trigger pull
        public int mag;
        public float roundGap, burstGap, reload;
        public float speed;          // bullet speed: a rifle's crack reaches the nest before its report
        public bool supersonic;
        public float volume;

        public AudioClip Report => kind == GunKind.Pistol ? Sfx.Pistol : (kind == GunKind.Smg ? Sfx.Smg : Sfx.Rifle);
    }

    public static class Guns
    {
        public static readonly GunSpec Pistol = new GunSpec
        {
            kind = GunKind.Pistol, prop = "ai_gun_pistol", grip = new Vector3(0.055f, -0.005f, 0f), muzzle = new Vector3(-0.096f, 0.046f, 0f),
            range = 175f, acc = 0.2f, damage = 11f, burst = 2, mag = 15, roundGap = 0.3f, burstGap = 0.8f, reload = 1.7f, speed = 360f,
            supersonic = false, volume = 0.75f,
        };
        public static readonly GunSpec Rifle = new GunSpec
        {
            kind = GunKind.Rifle, prop = "ai_gun_ak", grip = new Vector3(0.16f, -0.02f, 0f), fore = new Vector3(-0.2f, 0.055f, 0f), muzzle = new Vector3(-0.44f, 0.074f, 0f),
            twoHanded = true, range = 470f, acc = 0.24f, damage = 20f, burst = 3, mag = 30, roundGap = 0.11f, burstGap = 0.7f, reload = 2.6f, speed = 715f,
            supersonic = true, volume = 1f,
        };
        public static readonly GunSpec Smg = new GunSpec
        {
            kind = GunKind.Smg, prop = "ai_gun_smg", grip = new Vector3(0.07f, -0.02f, 0f), fore = new Vector3(-0.21f, 0.045f, 0f), muzzle = new Vector3(-0.34f, 0.064f, 0f),
            twoHanded = true, range = 260f, acc = 0.2f, damage = 12f, burst = 4, mag = 30, roundGap = 0.075f, burstGap = 0.6f, reload = 2.2f, speed = 400f,
            supersonic = true, volume = 0.85f,
        };

        public static GunSpec Of(GunKind k) => k == GunKind.Rifle ? Rifle : (k == GunKind.Smg ? Smg : Pistol);

        /// <summary>The gun a character carries by default: security in tactical vests an assault rifle, bodyguards in
        /// suits a submachine gun or a pistol, everyone else armed a pistol.</summary>
        public static GunKind DefaultFor(string charId, Role role)
        {
            if (role != Role.Guard && role != Role.Bodyguard) return GunKind.Pistol;
            switch (charId)
            {
                case "guard_a": case "rico": return GunKind.Rifle;
                case "bodyguard_a": case "w_guard": return GunKind.Smg;
                default: return GunKind.Pistol;
            }
        }
    }

    /// <summary>Two-bone limb solver (shoulder-elbow-hand), applied after the animation has posed the body.</summary>
    public static class LimbIK
    {
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, float weight = 1f)
        {
            if (upper == null || lower == null || end == null || weight <= 0f) return;
            var a = upper.position;
            float la = Vector3.Distance(a, lower.position), lb = Vector3.Distance(lower.position, end.position);
            var at = target - a;
            float d = at.magnitude;
            if (la < 1e-4f || lb < 1e-4f || d < 1e-4f) return;
            var dir = at / d;
            d = Mathf.Clamp(d, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);
            var side = Vector3.ProjectOnPlane(pole - a, dir);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(lower.position - a, dir);
            if (side.sqrMagnitude < 1e-6f) return;
            side.Normalize();
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            var elbow = a + dir * (cosA * la) + side * (sinA * la);
            var reach = a + dir * d;
            var q1 = Quaternion.FromToRotation(lower.position - a, elbow - a);
            upper.rotation = Quaternion.Slerp(Quaternion.identity, q1, weight) * upper.rotation;
            var b = lower.position;
            var q2 = Quaternion.FromToRotation(end.position - b, reach - b);
            lower.rotation = Quaternion.Slerp(Quaternion.identity, q2, weight) * lower.rotation;
        }
    }

    /// <summary>
    /// The gun of an armed man. Holstered out of sight (pistol, submachine gun under the jacket) or slung across the
    /// back (rifle) while he is at ease; drawn: at the ready (muzzle down in front of him), carried the way a rifle or
    /// pistol clip holds it, or — in an aiming pose — levelled at his mark: the spine turns so the gun lines up, the
    /// barrel points straight at the mark and the left hand takes the handguard. Falls from his hands when he goes down.
    /// </summary>
    public class GunHold : MonoBehaviour
    {
        public NPC npc;
        public GunSpec spec;
        public Transform gun;
        /// <summary>Out of the holster / off the back, in his hands.</summary>
        public bool Drawn;
        /// <summary>Levelled at <see cref="Aim"/> whenever an aiming pose plays.</summary>
        public bool Aiming;
        public Func<Vector3> Aim;
        Transform[] spine;
        Transform rUpper, rFore, lUpper, lFore, lHand;
        readonly Quaternion[] pre = new Quaternion[3], post = new Quaternion[3];
        bool primed, dropped;
        float kick;

        /// <summary>A round went off: the muzzle climbs and the gun shoves back into the shoulder, settling in a tenth of
        /// a second (a burst walks the barrel up).</summary>
        public void Kick(float amount = 1f) => kick = Mathf.Min(1.6f, kick + amount);

        static readonly HashSet<string> AimPoses = new HashSet<string>
        {
            "rifle_aim", "rifle_aim2", "rifle_kneel", "rifle_kneel_fire", "rifle_prone", "pistol_aim", "pistol_kneel",
        };

        public static GunHold Give(NPC n, GunKind kind)
        {
            if (n == null || kind == GunKind.None) return null;
            var have = n.GetComponent<GunHold>();
            if (have != null) return have;
            if (n.GetComponent<RifleHold>() != null) return null;             // a marksman with his own rifle
            var spec = Guns.Of(kind);
            var go = MakeGun(n, spec);
            if (go == null) return null;
            var h = n.gameObject.AddComponent<GunHold>();
            h.npc = n;
            h.spec = spec;
            h.gun = go.transform;
            h.spine = new[] { Util.FindDeep(n.transform, "mixamorig:Spine"), Util.FindDeep(n.transform, "mixamorig:Spine1"), Util.FindDeep(n.transform, "mixamorig:Spine2") };
            h.rUpper = Util.FindDeep(n.transform, "mixamorig:RightArm");
            h.rFore = Util.FindDeep(n.transform, "mixamorig:RightForeArm");
            h.lUpper = Util.FindDeep(n.transform, "mixamorig:LeftArm");
            h.lFore = Util.FindDeep(n.transform, "mixamorig:LeftForeArm");
            h.lHand = Util.FindDeep(n.transform, "mixamorig:LeftHand");
            n.Armed = true;
            go.SetActive(false);
            return h;
        }

        /// <summary>The gun prop for <paramref name="n"/>'s hand (no colliders, no shadows, ignored by bullets).</summary>
        static GameObject MakeGun(NPC n, GunSpec spec)
        {
            var prefab = Res.Prop(spec.prop);
            if (prefab == null) return null;
            var go = Instantiate(prefab, n.transform);
            go.name = "Gun";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Util.SetLayer(go, Layers.IgnoreRaycast);
            return go;
        }

        /// <summary>Another gun for a man who already carries one (the default holstered pistol swapped for a rifle on
        /// his shoulder): the same holder, a new weapon.</summary>
        public static GunHold Swap(NPC n, GunKind kind)
        {
            if (n == null || kind == GunKind.None) return null;
            var h = n.GetComponent<GunHold>();
            if (h == null) return Give(n, kind);
            var spec = Guns.Of(kind);
            var go = MakeGun(n, spec);
            if (go == null) return h;
            if (h.gun != null) Destroy(h.gun.gameObject);
            h.spec = spec;
            h.gun = go.transform;
            go.SetActive(false);
            return h;
        }

        /// <summary>Takes the gun away (a marksman who gets a scoped rifle instead).</summary>
        public static void Remove(NPC n)
        {
            var h = n != null ? n.GetComponent<GunHold>() : null;
            if (h == null) return;
            if (h.gun != null) Destroy(h.gun.gameObject);
            Destroy(h);
        }

        /// <summary>Where his rounds leave the barrel.</summary>
        public Vector3 Muzzle => gun != null && gun.gameObject.activeInHierarchy ? gun.TransformPoint(spec.muzzle) : npc.HandPoint;

        /// <summary>The way the barrel points now.</summary>
        public Vector3 Barrel => gun != null ? -gun.right : npc.transform.forward;

        /// <summary>The pose that levels this gun (standing, or kneeling behind low cover).</summary>
        public string AimClip(bool low) => spec.kind == GunKind.Pistol ? (low ? "pistol_kneel" : "pistol_aim") : (low ? "rifle_kneel" : (npc.HasClip("rifle_aim2") ? "rifle_aim2" : "rifle_aim"));

        /// <summary>Running with the gun drawn.</summary>
        public string RunClip => spec.twoHanded && npc.HasClip("rifle_run") ? "rifle_run" : (npc.HasClip("run_sprint") ? "run_sprint" : "run");

        public string ReloadClip(bool low) => spec.twoHanded ? (low ? "rifle_kneel_reload" : "rifle_reload") : "crouch_hide";

        bool Bones => spine != null && spine[0] != null && spine[1] != null && spine[2] != null && npc.RightHand != null;

        void LateUpdate()
        {
            if (npc == null || gun == null || dropped) return;
            bool show = !npc.IsHidden && !npc.Escaped;
            if (npc.Dead || npc.Sedated || npc.Down || (npc.Wounded && npc.WoundZone == BodyZone.Arm))
            {
                // it falls from his hands (or off his back); a holstered gun stays where it is
                if (show && (Drawn || spec.kind == GunKind.Rifle)) Drop();
                else if (gun.gameObject.activeSelf) gun.gameObject.SetActive(false);
                return;
            }
            bool visible = show && (Drawn || spec.kind == GunKind.Rifle);
            if (gun.gameObject.activeSelf != visible) gun.gameObject.SetActive(visible);
            if (!visible || !Bones) return;
            // the clip rewrites the spine every frame it is sampled; when it is culled (off screen) undo last frame's lean
            for (int i = 0; i < 3; i++)
            {
                var cur = spine[i].localRotation;
                if (primed && cur == post[i]) cur = pre[i];
                spine[i].localRotation = cur;
                pre[i] = cur;
            }
            string clip = npc.ClipName ?? "";
            bool support = false;
            float kick0 = kick;
            if (!Drawn) Sling();
            else if (Aiming && AimPoses.Contains(clip)) { Level(); Recoil(); support = true; }
            else if (spec.twoHanded && clip.StartsWith("rifle_", StringComparison.Ordinal))
            {
                HandLine();
                // firing on the run: the kick moves the gun, the left hand stays on the handguard
                bool kicked = kick > 0.01f;
                Recoil();
                support = kicked;
            }
            else if (!spec.twoHanded && clip.StartsWith("pistol_", StringComparison.Ordinal)) Forearm();
            // a hand signal or a shout: the left hand is off the rifle
            else { Ready(); support = spec.twoHanded && clip != "signal" && clip != "shout"; }
            if (!Drawn) kick = 0f;
            else if (kick > 0f && kick == kick0) kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 9f);   // not shown in this pose: it still settles
            for (int i = 0; i < 3; i++) post[i] = spine[i].localRotation;
            primed = true;
            // support hand on the handguard (a pistol: cupping the grip)
            if (support && lHand != null && lUpper != null && lFore != null)
            {
                Vector3 to = spec.twoHanded ? gun.TransformPoint(spec.fore) : gun.TransformPoint(spec.grip + new Vector3(0.01f, -0.015f, -0.04f));
                var pole = lUpper.position + Vector3.down * 0.5f - npc.transform.right * 0.35f;
                LimbIK.Solve(lUpper, lFore, lHand, to, pole);
            }
        }

        Vector3 Target => Aim != null ? Aim() : npc.RightHand.position + npc.transform.forward * 20f;

        /// <summary>The kick of the last round, on top of the levelled pose (the support hand follows the gun).</summary>
        void Recoil()
        {
            if (kick <= 0.001f) { kick = 0f; return; }
            float deg = (spec.twoHanded ? 5f : 9f) * kick;
            // the muzzle (-X) climbs: a turn about the gun's own side axis (+Z, the shooter's right)
            gun.RotateAround(npc.RightHand.position, gun.forward, -deg);
            gun.position -= Barrel * (spec.twoHanded ? 0.035f : 0.02f) * kick;
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * (spec.twoHanded ? 9f : 7f));
        }

        /// <summary>Levelled: lean so the gun's line points at the mark, then lay it along that line.</summary>
        void Level()
        {
            var rh = npc.RightHand;
            var target = Target;
            Vector3 from;
            if (spec.twoHanded && lHand != null) from = lHand.position - rh.position;
            else from = rFore != null ? rh.position - rFore.position : npc.transform.forward;
            var to = target - rh.position;
            if (from.sqrMagnitude > 1e-4f && to.sqrMagnitude > 1e-4f)
            {
                Quaternion.FromToRotation(from.normalized, to.normalized).ToAngleAxis(out float ang, out Vector3 axis);
                if (ang > 180f) ang -= 360f;
                ang = Mathf.Clamp(ang, -60f, 60f);
                if (axis.sqrMagnitude > 1e-6f && Mathf.Abs(ang) > 0.05f)
                {
                    var part = Quaternion.AngleAxis(ang / 3f, axis);
                    for (int i = 0; i < 3; i++) spine[i].rotation = part * spine[i].rotation;
                }
            }
            Place(target - rh.position);
        }

        /// <summary>A rifle clip (running, reloading, firing on the move): along the line from the right hand to the left.</summary>
        void HandLine()
        {
            var rh = npc.RightHand;
            var d = lHand != null ? lHand.position - rh.position : npc.transform.forward;
            if (d.sqrMagnitude < 0.01f) d = npc.transform.forward;
            Place(d);
        }

        /// <summary>A pistol clip: along the forearm.</summary>
        void Forearm()
        {
            var rh = npc.RightHand;
            var d = rFore != null ? rh.position - rFore.position : npc.transform.forward;
            Place(d.normalized + Vector3.down * 0.15f);
        }

        /// <summary>At the ready: muzzle down and forward.</summary>
        void Ready()
        {
            var f = npc.transform.forward; f.y = 0f;
            f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            Place(f * (spec.twoHanded ? 0.75f : 0.55f) + Vector3.down * (spec.twoHanded ? 0.6f : 0.85f));
        }

        void Place(Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-6f) dir = npc.transform.forward;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
            gun.rotation = rot;
            gun.position = npc.RightHand.position - rot * spec.grip;
        }

        /// <summary>Slung across the back, muzzle up over the left shoulder.</summary>
        void Sling()
        {
            var back = spine[2].position;
            var f = npc.transform.forward; f.y = 0f;
            f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            var left = Vector3.Cross(f, Vector3.up);       // the character's left
            var muzzleDir = (Vector3.up * 0.85f + left * 0.5f).normalized;
            var z = -f;                                    // gun +Z (its right side) against his back
            var y = Vector3.Cross(z, -muzzleDir);          // with x = -muzzleDir: y = z × x
            gun.rotation = Quaternion.LookRotation(z, y);
            gun.position = back - f * 0.17f - muzzleDir * 0.05f;
        }

        void Drop()
        {
            dropped = true;
            gun.gameObject.SetActive(true);
            gun.SetParent(npc.transform.parent, true);
            var col = gun.gameObject.AddComponent<BoxCollider>();
            col.size = spec.twoHanded ? new Vector3(spec.kind == GunKind.Rifle ? 0.88f : 0.68f, 0.18f, 0.06f) : new Vector3(0.19f, 0.13f, 0.035f);
            foreach (var c in npc.GetComponentsInChildren<Collider>(true))
                if (c != null && c != col) Physics.IgnoreCollision(col, c, true);
            var rb = gun.gameObject.AddComponent<Rigidbody>();
            rb.mass = spec.twoHanded ? 3.5f : 0.9f;
            rb.maxDepenetrationVelocity = 1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.AddForce(npc.transform.forward * 0.5f + Vector3.down * 0.3f, ForceMode.VelocityChange);
            rb.AddTorque(new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-1f, 1f)), ForceMode.VelocityChange);
            Destroy(gun.gameObject, 40f);
        }
    }
}

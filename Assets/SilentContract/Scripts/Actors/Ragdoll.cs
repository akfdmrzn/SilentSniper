using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    public enum BodyZone { Head, Torso, Pelvis, Arm, Leg }

    /// <summary>Marks a ragdoll collider as a hit zone of a character.</summary>
    public class BodyPart : MonoBehaviour
    {
        public NPC owner;
        public BodyZone zone;
        public Rigidbody body;
    }

    /// <summary>
    /// Procedural ragdoll for the MPFB/Mixamo skeleton. While the character is alive the bodies are kinematic and
    /// serve as precise hit zones; on death physics takes over (with the bullet impulse).
    /// </summary>
    public class Ragdoll : MonoBehaviour
    {
        const string P = "mixamorig:";
        public readonly List<Rigidbody> bodies = new List<Rigidbody>();
        public readonly List<Collider> colliders = new List<Collider>();
        public Rigidbody Pelvis { get; private set; }
        public Rigidbody Head { get; private set; }
        public bool Active { get; private set; }
        NPC owner;
        static readonly List<Ragdoll> all = new List<Ragdoll>();

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        Transform B(string n) => Util.FindDeep(transform, P + n);

        public bool Build(NPC npc)
        {
            owner = npc;
            var hips = B("Hips");
            var spine = B("Spine1") ?? B("Spine");
            var neck = B("Neck");
            var head = B("Head");
            if (hips == null || spine == null || head == null) return false;
            Vector3 right = transform.right, fwd = transform.forward, up = transform.up;

            Pelvis = AddBody(hips, 11f, BodyZone.Pelvis);
            var hipsL = B("LeftUpLeg"); var hipsR = B("RightUpLeg");
            float hipW = hipsL != null && hipsR != null ? Vector3.Distance(hipsL.position, hipsR.position) : 0.2f;
            var pelvisBox = hips.gameObject.AddComponent<BoxCollider>();
            float pelvisH = Vector3.Distance(hips.position, spine.position) * 0.9f;
            // the belly / hip box ends at the hip joints: below them the thighs take the hit (a shot at the upper leg is a
            // leg wound, not a lethal belly shot)
            pelvisBox.center = hips.InverseTransformPoint((hips.position + spine.position) * 0.5f + up * 0.03f);
            pelvisBox.size = AbsLocalSize(hips, right * (hipW + 0.06f) + up * pelvisH + fwd * 0.22f);
            Register(hips, pelvisBox, BodyZone.Pelvis);

            // chest: from spine1 to neck
            var chest = AddBody(spine, 14f, BodyZone.Torso);
            var chestBox = spine.gameObject.AddComponent<BoxCollider>();
            var top = neck != null ? neck.position : head.position;
            var la = B("LeftArm"); var ra = B("RightArm");
            float shoulderW = la != null && ra != null ? Vector3.Distance(la.position, ra.position) : 0.36f;
            chestBox.center = spine.InverseTransformPoint((spine.position + top) * 0.5f);
            chestBox.size = AbsLocalSize(spine, right * (shoulderW * 0.85f) + up * (Vector3.Distance(spine.position, top) * 1.05f) + fwd * 0.24f);
            Register(spine, chestBox, BodyZone.Torso);
            // limits wide enough for the poses the death clips hand over in (on the knees, sitting, curled up): a
            // tighter limit would snap the limbs straight at the hand-over
            Joint(spine, Pelvis, right, fwd, -30, 30, 15);

            // head
            Head = AddBody(head, 5f, BodyZone.Head);
            var hs = head.gameObject.AddComponent<SphereCollider>();
            hs.radius = 0.115f / Mathf.Max(0.01f, head.lossyScale.x);
            hs.center = head.InverseTransformPoint(head.position + up * 0.085f + fwd * 0.02f);
            Register(head, hs, BodyZone.Head);
            Joint(head, chest, right, fwd, -40, 25, 25);

            // arms
            foreach (var side in new[] { "Left", "Right" })
            {
                var arm = B(side + "Arm"); var fore = B(side + "ForeArm"); var hand = B(side + "Hand");
                if (arm == null || fore == null) continue;
                var ab = AddBody(arm, 2.5f, BodyZone.Arm);
                Capsule(arm, arm.position, fore.position, 0.055f, BodyZone.Arm);
                Joint(arm, chest, up, fwd, -70, 10, 50);
                var fb = AddBody(fore, 2f, BodyZone.Arm);
                var end = hand != null ? hand.position + (hand.position - fore.position).normalized * 0.09f : fore.position + (fore.position - arm.position);
                Capsule(fore, fore.position, end, 0.045f, BodyZone.Arm);
                Joint(fore, ab, fwd, up, -90, 0, 0);
            }
            // legs
            foreach (var side in new[] { "Left", "Right" })
            {
                var thigh = B(side + "UpLeg"); var shin = B(side + "Leg"); var foot = B(side + "Foot");
                if (thigh == null || shin == null) continue;
                var tb = AddBody(thigh, 7.5f, BodyZone.Leg);
                Capsule(thigh, thigh.position, shin.position, 0.08f, BodyZone.Leg);
                Joint(thigh, Pelvis, right, fwd, -25, 115, 35);
                var sb = AddBody(shin, 4f, BodyZone.Leg);
                var end = foot != null ? foot.position - up * 0.05f : shin.position - up * 0.4f;
                Capsule(shin, shin.position, end, 0.06f, BodyZone.Leg);
                Joint(shin, tb, right, fwd, -145, 0, 0);
            }
            // a body never collides with itself: a forearm across the chest (a rifle held two-handed, arms folded, a phone
            // at the ear) or a knee folded into the belly would otherwise start the fall inside another part, and the
            // physics would fling the whole body out of it — the somersaults. The joints keep the limbs where they belong.
            for (int i = 0; i < colliders.Count; i++)
                for (int j = i + 1; j < colliders.Count; j++)
                    if (colliders[i] != null && colliders[j] != null) Physics.IgnoreCollision(colliders[i], colliders[j], true);
            foreach (var rb in bodies)
            {
                rb.maxDepenetrationVelocity = 1.2f;   // a limb that starts a little inside the floor is eased out, not shot out
                rb.solverIterations = 12;
                rb.solverVelocityIterations = 4;
            }
            SetKinematic(true);
            return true;
        }

        static Vector3 AbsLocalSize(Transform t, Vector3 worldSize)
        {
            var l = t.InverseTransformVector(worldSize);
            return new Vector3(Mathf.Abs(l.x), Mathf.Abs(l.y), Mathf.Abs(l.z));
        }

        Rigidbody AddBody(Transform t, float mass, BodyZone zone)
        {
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.None;      // animated (kinematic) while alive: interpolation would fight the animation
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            bodies.Add(rb);
            return rb;
        }

        void Capsule(Transform bone, Vector3 a, Vector3 b, float radius, BodyZone zone)
        {
            var c = bone.gameObject.AddComponent<CapsuleCollider>();
            var la = bone.InverseTransformPoint(a);
            var lb = bone.InverseTransformPoint(b);
            var d = lb - la;
            int axis = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (Mathf.Abs(d.x) > Mathf.Abs(d.z) ? 0 : 2) : (Mathf.Abs(d.y) > Mathf.Abs(d.z) ? 1 : 2);
            c.direction = axis;
            c.center = (la + lb) * 0.5f;
            float s = Mathf.Max(0.001f, bone.lossyScale.x);
            c.radius = radius / s;
            c.height = d.magnitude + radius * 2f / s;
            Register(bone, c, zone);
        }

        void Register(Transform bone, Collider c, BodyZone zone)
        {
            bone.gameObject.layer = Layers.Body;
            colliders.Add(c);
            var bp = bone.gameObject.GetComponent<BodyPart>();
            if (bp == null) bp = bone.gameObject.AddComponent<BodyPart>();
            bp.owner = owner;
            bp.zone = zone;
            bp.body = bone.GetComponent<Rigidbody>();
        }

        void Joint(Transform bone, Rigidbody parent, Vector3 twistAxis, Vector3 swingAxis, float lo, float hi, float swing)
        {
            if (parent == null) return;
            var j = bone.gameObject.AddComponent<CharacterJoint>();
            j.connectedBody = parent;
            j.axis = bone.InverseTransformDirection(twistAxis).normalized;
            j.swingAxis = bone.InverseTransformDirection(swingAxis).normalized;
            j.anchor = Vector3.zero;
            j.enablePreprocessing = false;
            j.enableProjection = true;
            var l = j.lowTwistLimit; l.limit = lo; j.lowTwistLimit = l;
            var h = j.highTwistLimit; h.limit = hi; j.highTwistLimit = h;
            var s1 = j.swing1Limit; s1.limit = swing; j.swing1Limit = s1;
            var s2 = j.swing2Limit; s2.limit = 0; j.swing2Limit = s2;
        }

        public void SetKinematic(bool k)
        {
            foreach (var rb in bodies)
            {
                rb.isKinematic = k;
                rb.useGravity = !k;
                rb.detectCollisions = true;
            }
        }

        /// <summary>Switch to physics and push the struck body part.</summary>
        public void Activate(Vector3 impulse, Vector3 point, Rigidbody hitBody) => Activate(impulse, point, hitBody, Vector3.zero, null);

        /// <summary>Switch to physics: every body part keeps going the way it was moving (<paramref name="carry"/>: a runner's
        /// stride, a moving car; <paramref name="perBody"/>: the last frames of a death clip), then the bullet's push.</summary>
        public void Activate(Vector3 impulse, Vector3 point, Rigidbody hitBody, Vector3 carry, Dictionary<Rigidbody, Vector3> perBody)
        {
            if (Active) return;
            Active = true;
            // the living walk their animations through the world as kinematic bodies, which would kick a body lying in
            // their way across the street: the living and the dead do not touch
            foreach (var o in all)
            {
                if (o == null || o == this || o.Active) continue;
                foreach (var a in colliders)
                    foreach (var b in o.colliders)
                        if (a != null && b != null) Physics.IgnoreCollision(a, b, true);
            }
            SetKinematic(false);
            foreach (var rb in bodies)
            {
                rb.interpolation = RigidbodyInterpolation.Interpolate;   // smooth ragdoll in slow motion
                rb.WakeUp();
                var v = carry;
                if (perBody != null && perBody.TryGetValue(rb, out var pv)) v = pv;
                // fresh from kinematic the body is at rest: a velocity change sets its speed
                if (v.sqrMagnitude > 1e-4f) rb.AddForce(Vector3.ClampMagnitude(v, 1.6f), ForceMode.VelocityChange);
                rb.maxAngularVelocity = 4f;           // V2: limbs fold slowly, the body never spins or flips
            }
            damp = 5f;
            if (impulse.sqrMagnitude < 1e-6f) return;
            if (hitBody == null) hitBody = Pelvis;
            if (hitBody != null)
            {
                hitBody.AddForceAtPosition(impulse, point, ForceMode.Impulse);
                if (Pelvis != null && hitBody != Pelvis) Pelvis.AddForce(impulse * 0.35f, ForceMode.Impulse);
            }
        }

        /// <summary>
        /// A limp collapse instead of a stiff plank: the knees buckle (the thighs pushed forwards, the shins back), the
        /// hips drop and the upper body sags a little the way the bullet went.
        /// </summary>
        public void Crumple(Vector3 shotDir)
        {
            var f = transform.forward; f.y = 0f; f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            var d = shotDir; d.y = 0f; d = d.sqrMagnitude > 1e-4f ? d.normalized : -f;
            foreach (var rb in bodies)
            {
                if (rb == null) continue;
                var bp = rb.GetComponent<BodyPart>();
                if (bp == null) continue;
                Vector3 v;
                if (rb == Pelvis) v = Vector3.down * 1.3f + d * 0.25f;
                else if (bp.zone == BodyZone.Leg) v = rb.name.EndsWith("UpLeg") ? f * 0.9f + Vector3.down * 0.6f : -f * 0.35f;
                else v = Vector3.down * 0.7f + d * 0.35f;
                rb.AddForce(v, ForceMode.VelocityChange);
            }
            damp = 2.5f;
        }

        float damp;

        void FixedUpdate()
        {
            // the fall: muscle tone fading — joint friction that keeps arms and head from flailing or whipping round
            // and the body from rolling over once it is down, strongest at first
            if (!Active || damp <= 0f) return;
            damp -= Time.fixedDeltaTime;
            float k = 3f + 3f * Mathf.Clamp01(damp / 2.5f);
            foreach (var rb in bodies)
                if (rb != null && !rb.isKinematic) rb.AddTorque(-rb.angularVelocity * k, ForceMode.Acceleration);
        }

        /// <summary>Remove all physics (used when the character is recycled).</summary>
        public void Strip()
        {
            foreach (var j in GetComponentsInChildren<CharacterJoint>()) Destroy(j);
            foreach (var rb in bodies) if (rb != null) Destroy(rb);
            foreach (var c in colliders) if (c != null) Destroy(c);
            bodies.Clear();
            colliders.Clear();
        }
    }
}

using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// A lookout with binoculars. He sweeps a sector of the skyline; the lenses flash whenever they swing past the nest
    /// (a careful player sees them coming). If he catches the player above the parapet long enough he lowers the
    /// binoculars and reaches for his radio — a few seconds to stop him before every gunman knows where the nest is.
    /// Ducking while his sweep goes over, or dropping him first, keeps the nest hidden.
    /// </summary>
    public class Spotter : MonoBehaviour
    {
        public NPC npc;
        public float yawCenter, arc = 70f, period = 10f;
        public float range = 520f;
        /// <summary>Half-angle of what the binoculars take in (degrees).</summary>
        public float field = 8f;
        /// <summary>0..1: how sure he is that he saw someone on the roof.</summary>
        public float Meter { get; private set; }
        public bool Reporting { get; private set; }
        public bool Reported { get; private set; }
        public Vector3 Look { get; private set; } = Vector3.forward;

        Transform head, neck, chest, lUpper, lFore, lHand, rUpper, rFore, rHand;
        Transform[] bones;
        Transform bino;
        ScopeGlint glint;
        float phase;
        readonly Quaternion[] pre = new Quaternion[3], post = new Quaternion[3];
        bool primed;
        static Mesh binoMesh;
        static Material binoMat;

        /// <summary>Makes <paramref name="n"/> a lookout sweeping <paramref name="arc"/> degrees around
        /// <paramref name="yaw"/> (towards the rooftops where the nest is).</summary>
        public static Spotter Make(NPC n, float yaw, float arc, float period = 10f)
        {
            if (n == null) return null;
            var s = n.gameObject.AddComponent<Spotter>();
            s.npc = n;
            s.yawCenter = yaw;
            s.arc = arc;
            s.period = period;
            s.phase = Random.value * Mathf.PI * 2f;
            var t = n.transform;
            s.head = Util.FindDeep(t, "mixamorig:Head");
            s.neck = Util.FindDeep(t, "mixamorig:Neck");
            s.chest = Util.FindDeep(t, "mixamorig:Spine2");
            s.lUpper = Util.FindDeep(t, "mixamorig:LeftArm");
            s.lFore = Util.FindDeep(t, "mixamorig:LeftForeArm");
            s.lHand = Util.FindDeep(t, "mixamorig:LeftHand");
            s.rUpper = Util.FindDeep(t, "mixamorig:RightArm");
            s.rFore = Util.FindDeep(t, "mixamorig:RightForeArm");
            s.rHand = Util.FindDeep(t, "mixamorig:RightHand");
            s.bones = new[] { s.chest, s.neck, s.head };
            var go = new GameObject("Binoculars");
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = BinoMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = BinoMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.bino = go.transform;
            bool night = MissionRuntime.I != null && MissionRuntime.I.IsNight;
            s.glint = ScopeGlint.Attach(go.transform, new Vector3(0f, 0f, 0.07f), n, night);
            s.glint.aim = go.transform;
            s.glint.strength = 1.2f;
            s.glint.sizeMul = 0.8f;
            n.Lookout = s;
            n.SetPlan(Act.IdleFacing("idle", yaw, 0));
            return s;
        }

        bool Able => npc != null && !npc.Dead && !npc.Incapacitated && !npc.Escaped && !npc.IsHidden;

        /// <summary>Looking through the binoculars: at his post, facing his sector (not reporting, not off checking a
        /// noise, not scared off them).</summary>
        bool Watching => Able && !Reporting && !npc.Panicking && !npc.Investigating && !npc.Moving &&
                         (npc.Combat == null || !npc.Combat.Engaged) &&
                         Mathf.Abs(Mathf.DeltaAngle(npc.transform.eulerAngles.y, yawCenter)) < 50f;

        void Update()
        {
            if (!Able)
            {
                // down, asleep or dead: no more watching (his marker calms down); a call he already started is the
                // radio's business (see MissionRuntime)
                if (bino != null && bino.gameObject.activeSelf) bino.gameObject.SetActive(false);
                Meter = 0f;
                Reporting = false;
                return;
            }
            var rt = MissionRuntime.I;
            var ff = Firefight.I;
            if (rt == null || !rt.IsPlaying || ff == null) return;
            // a story cutaway: the player cannot duck, so the binoculars do not count
            if (KillCam.IsCutaway) return;
            float dt = Time.deltaTime;
            if (Reporting)
            {
                // on the radio until the alarm goes up (or someone stops him: then he is no longer Able)
                if (ff.Located || rt.Alarm) FinishReport();
                return;
            }
            if (!Watching || Reported || ff.Located) { Meter = Mathf.MoveTowards(Meter, 0f, dt * 0.3f); return; }
            var nest = ff.Nest;
            var eye = npc.EyePos;
            var to = nest - eye;
            float d = to.magnitude;
            float ang = Vector3.Angle(Look, to);
            float seeing = 0f;
            if (d < range && ang < field && !Physics.Linecast(eye, nest, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                seeing = 1f - ang / field;
            float exposure = ff.Exposure;
            if (seeing > 0f && exposure > 0f)
            {
                float night = rt.IsNight && !Sniper.I.Scoped ? 0.35f : (rt.IsNight ? 0.5f : 1f);
                // a pass or two of his sweep while the player shows himself is enough
                Meter = Mathf.Min(1f, Meter + seeing * exposure * night * dt * 1.6f * Mathf.Lerp(1f, 0.6f, d / range));
                ff.Watch(Meter);
                if (Meter >= 1f) StartReport();
            }
            else Meter = Mathf.MoveTowards(Meter, 0f, dt * 0.03f);           // "something moved up there": he keeps it in mind
        }

        void StartReport()
        {
            Reporting = true;
            var rt = MissionRuntime.I;
            float yaw = npc.YawTo(Firefight.I.Nest);
            npc.SetPlan(Act.IdleFacing(npc.HasClip("phone_talk") ? "phone_talk" : "idle_look", yaw, 0));
            rt.SpotterReport(npc, 3.4f);
        }

        void FinishReport()
        {
            Reporting = false;
            Reported = true;
            if (Able && !npc.Panicking && (npc.Combat == null || !npc.Combat.Engaged)) npc.SetPlan(Act.IdleFacing("idle_look", npc.YawTo(Firefight.I.Nest), 0));
        }

        void LateUpdate()
        {
            if (!Able || bino == null) return;
            bool use = Watching && !Reported;
            if (bino.gameObject.activeSelf != use) bino.gameObject.SetActive(use);
            if (head == null || neck == null || chest == null) return;
            // undo last frame's turn when the clip was not sampled (off screen)
            for (int i = 0; i < 3; i++)
            {
                var cur = bones[i].localRotation;
                if (primed && cur == post[i]) cur = pre[i];
                bones[i].localRotation = cur;
                pre[i] = cur;
            }
            if (use)
            {
                // the sweep: across the sector, pitched up towards the rooftops of the nest
                float yaw = yawCenter + Mathf.Sin(Time.time * Mathf.PI * 2f / period + phase) * arc * 0.5f;
                var nest = Firefight.I != null ? Firefight.I.Nest : transform.position + Vector3.up * 40f;
                var eye = npc.EyePos;
                var flat = nest - eye; flat.y = 0f;
                float pitch = Mathf.Atan2(nest.y - eye.y, Mathf.Max(1f, flat.magnitude)) * Mathf.Rad2Deg + Mathf.Sin(Time.time * 0.7f + phase) * 1.5f;
                Look = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                var body = npc.transform.forward; body.y = 0f;
                if (body.sqrMagnitude < 1e-4f) body = Vector3.forward;
                var turn = Quaternion.FromToRotation(body.normalized, Look);
                // spread the turn over chest, neck and head
                var part = Quaternion.Slerp(Quaternion.identity, turn, 0.34f);
                for (int i = 0; i < 3; i++) bones[i].rotation = part * bones[i].rotation;
                // the binoculars at his eyes, the hands on them
                var eyes = head.position + Vector3.up * 0.075f + Look * 0.11f;
                bino.SetPositionAndRotation(eyes + Look * 0.05f, Quaternion.LookRotation(Look, Vector3.up));
                var right = Vector3.Cross(Vector3.up, Look).normalized;
                var down = Vector3.Cross(Look, right);
                LimbIK.Solve(lUpper, lFore, lHand, bino.position - right * 0.04f - down * 0.03f + Look * 0.02f, lUpper.position + Vector3.down * 0.4f - right * 0.3f);
                LimbIK.Solve(rUpper, rFore, rHand, bino.position + right * 0.04f - down * 0.03f + Look * 0.02f, rUpper.position + Vector3.down * 0.4f + right * 0.3f);
            }
            else Look = npc.transform.forward;
            for (int i = 0; i < 3; i++) post[i] = bones[i].localRotation;
            primed = true;
        }

        static Material BinoMat
        {
            get
            {
                if (binoMat != null) return binoMat;
                binoMat = new Material(Shader.Find("Standard"));
                binoMat.SetColor("_Color", new Color(0.06f, 0.065f, 0.07f));
                binoMat.SetFloat("_Glossiness", 0.45f);
                binoMat.SetFloat("_Metallic", 0.3f);
                return binoMat;
            }
        }

        /// <summary>Two short barrels side by side (+Z forward), joined by a bridge.</summary>
        static Mesh BinoMesh
        {
            get
            {
                if (binoMesh != null) return binoMesh;
                var verts = new System.Collections.Generic.List<Vector3>();
                var tris = new System.Collections.Generic.List<int>();
                const int seg = 10;
                foreach (float x in new[] { -0.034f, 0.034f })
                {
                    int b = verts.Count;
                    for (int k = 0; k < seg; k++)
                    {
                        float a = k * Mathf.PI * 2f / seg;
                        var c = new Vector3(x + Mathf.Cos(a) * 0.022f, Mathf.Sin(a) * 0.022f, 0f);
                        verts.Add(c + new Vector3(0, 0, -0.06f));
                        verts.Add(c + new Vector3(0, 0, 0.06f));
                    }
                    for (int k = 0; k < seg; k++)
                    {
                        int i0 = b + k * 2, i1 = b + ((k + 1) % seg) * 2;
                        tris.AddRange(new[] { i0, i1, i0 + 1, i1, i1 + 1, i0 + 1 });
                    }
                    // end caps
                    int cb = verts.Count;
                    verts.Add(new Vector3(x, 0, -0.06f));
                    verts.Add(new Vector3(x, 0, 0.06f));
                    for (int k = 0; k < seg; k++)
                    {
                        int i0 = b + k * 2, i1 = b + ((k + 1) % seg) * 2;
                        tris.AddRange(new[] { cb, i1, i0 });
                        tris.AddRange(new[] { cb + 1, i0 + 1, i1 + 1 });
                    }
                }
                // bridge
                int q = verts.Count;
                verts.AddRange(new[] { new Vector3(-0.02f, -0.008f, -0.03f), new Vector3(0.02f, -0.008f, -0.03f), new Vector3(-0.02f, 0.008f, -0.03f), new Vector3(0.02f, 0.008f, -0.03f),
                                       new Vector3(-0.02f, -0.008f, 0.03f), new Vector3(0.02f, -0.008f, 0.03f), new Vector3(-0.02f, 0.008f, 0.03f), new Vector3(0.02f, 0.008f, 0.03f) });
                int[] box = { 0, 2, 1, 2, 3, 1, 4, 5, 6, 6, 5, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5, 2, 6, 3, 3, 6, 7, 0, 1, 4, 1, 5, 4 };
                foreach (var i in box) tris.Add(q + i);
                binoMesh = new Mesh { name = "binoculars" };
                binoMesh.SetVertices(verts);
                binoMesh.SetTriangles(tris, 0);
                binoMesh.RecalculateNormals();
                binoMesh.RecalculateBounds();
                return binoMesh;
            }
        }
    }
}

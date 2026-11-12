using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// Scope glint of an enemy marksman: a small star of light at his rifle that flares when he looks towards the nest
    /// (a trained eye's only warning). Constant size on screen, depth tested (cover hides it), gone once he is down.
    /// </summary>
    public class ScopeGlint : MonoBehaviour
    {
        public NPC owner;                 // the marksman (null = a fixed glint)
        public Transform aim;             // optional: brightest when this transform's forward points at the camera (a searchlight)
        public Transform barrel;          // optional: a rifle lying along X (muzzle at -X): brightest when it points at the camera
        public float strength = 1f;       // 0 off .. 1 scanning .. 2 aiming at the nest
        public float sizeMul = 1f;
        public Color tint = new Color(1f, 0.96f, 0.82f);
        public bool night;                // night scopes glow green instead of catching the sun
        static Material mat;
        static Texture2D star;
        Transform quad;
        MeshRenderer mr;
        MaterialPropertyBlock mpb;
        float seed;

        static Texture2D Star
        {
            get
            {
                if (star != null) return star;
                const int n = 64;
                star = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float core = Mathf.Exp(-r * r * 60f);
                        float halo = Mathf.Exp(-r * r * 9f) * 0.35f;
                        // four thin rays (a lens catching the light)
                        float rays = Mathf.Exp(-Mathf.Abs(u) * 34f) * Mathf.Exp(-Mathf.Abs(v) * 2.2f) + Mathf.Exp(-Mathf.Abs(v) * 34f) * Mathf.Exp(-Mathf.Abs(u) * 2.2f);
                        float a = Mathf.Clamp01(core + halo + rays * 0.8f) * Mathf.Clamp01(1.05f - r);
                        star.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                star.Apply(true);
                return star;
            }
        }

        static Material Mat
        {
            get
            {
                if (mat != null) return mat;
                mat = new Material(Shader.Find("SC/UnlitTint")) { mainTexture = Star };
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.renderQueue = 3100;
                return mat;
            }
        }

        /// <summary>A glint at <paramref name="localPos"/> on <paramref name="parent"/> (the marksman's root or his rifle).</summary>
        public static ScopeGlint Attach(Transform parent, Vector3 localPos, NPC owner, bool night)
        {
            var go = new GameObject("ScopeGlint");
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var g = go.AddComponent<ScopeGlint>();
            g.owner = owner;
            g.night = night;
            var q = Util.QuadObject("Flare", go.transform, Mat);
            g.quad = q.transform;
            g.mr = q.GetComponent<MeshRenderer>();
            g.mpb = new MaterialPropertyBlock();
            g.seed = Random.value * 10f;
            return g;
        }

        void LateUpdate()
        {
            var sn = Sniper.I;
            var cam = sn != null ? sn.Cam : Camera.main;
            if (cam == null || quad == null) return;
            bool gone = owner != null && (owner.Dead || owner.Incapacitated || owner.Escaped || owner.IsHidden);
            float a = 0f;
            if (!gone && strength > 0.01f)
            {
                float facing = 1f;
                if (aim != null)
                {
                    var to3 = (cam.transform.position - transform.position).normalized;
                    facing = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(aim.forward, to3)), 40f);
                }
                else if (barrel != null)
                {
                    // the scope catches the light when the rifle swings towards the nest
                    var to3 = (cam.transform.position - transform.position).normalized;
                    facing = Mathf.Lerp(0.12f, 1f, Mathf.Pow(Mathf.Clamp01(Vector3.Dot(-barrel.right, to3)), 8f));
                }
                else if (owner != null)
                {
                    // brightest when his scope points at the camera
                    var f = owner.transform.forward; f.y = 0f;
                    var to = cam.transform.position - transform.position; to.y = 0f;
                    float d = f.sqrMagnitude > 1e-4f && to.sqrMagnitude > 1e-4f ? Vector3.Dot(f.normalized, to.normalized) : 0f;
                    facing = Mathf.Lerp(0.12f, 1f, Mathf.Pow(Mathf.Clamp01(d), 6f));
                }
                float t = Time.time * (strength > 1.5f ? 7f : 1.7f) + seed;
                float pulse = aim != null ? 1f : (strength > 1.5f ? 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(t)) : Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t), 4f));
                a = Mathf.Clamp01(strength * facing * pulse);
            }
            if (a <= 0.01f) { if (mr.enabled) mr.enabled = false; return; }
            if (!mr.enabled) mr.enabled = true;
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            // about 3 % of the view height, whatever the zoom
            float size = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Lerp(0.02f, 0.034f, Mathf.Clamp01(strength - 1f)) * sizeMul;
            quad.rotation = cam.transform.rotation;
            quad.localScale = Vector3.one * size / Mathf.Max(0.001f, transform.lossyScale.x);
            mpb.SetColor("_Color", night ? new Color(0.55f, 1f, 0.65f, a) : new Color(tint.r, tint.g, tint.b, a));
            mr.SetPropertyBlock(mpb);
        }
    }

    /// <summary>
    /// Helicopter rotors: the model's blades stand still, so a spinning main disc (blurred, slightly see-through) and a
    /// tail disc are drawn over them, with the rotor sound. <see cref="SetSpin"/> spins up or down over a few seconds.
    /// </summary>
    public class RotorFX : MonoBehaviour
    {
        public float Spin { get; private set; }
        float from, to, t0, dur;
        Transform main, tail;
        MeshRenderer mainR, tailR;
        Transform blades, tailBlades;
        Renderer[] bladeRs;
        AudioSource src;
        float angle;
        MaterialPropertyBlock mpb;
        static Mesh disc;
        static Material discMat, bladeMat;

        /// <summary>Solid dark blades (the model has only the hub): seen when the rotor turns slowly or stands still.</summary>
        static Material BladeMat
        {
            get
            {
                if (bladeMat != null) return bladeMat;
                bladeMat = new Material(Shader.Find("SC/UnlitTint"));
                bladeMat.SetColor("_Color", new Color(0.05f, 0.05f, 0.055f, 1f));
                bladeMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                bladeMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                bladeMat.SetFloat("_ZWrite", 1f);
                bladeMat.renderQueue = 2000;
                return bladeMat;
            }
        }

        Transform MakeBlades(string name, Vector3 local, float radius, float width, bool vertical, List<Renderer> rs)
        {
            var root = new GameObject(name).transform;
            root.gameObject.layer = Layers.IgnoreRaycast;
            root.SetParent(transform, false);
            root.localPosition = local;
            root.localRotation = vertical ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            for (int k = 0; k < 2; k++)
            {
                var q = Util.QuadObject("Blade", root, BladeMat);
                q.transform.localRotation = Quaternion.Euler(90f, k * 90f, 0f);
                q.transform.localScale = new Vector3(radius * 2f, width, 1f);
                rs.Add(q.GetComponent<MeshRenderer>());
            }
            return root;
        }

        static Mesh Disc
        {
            get
            {
                if (disc != null) return disc;
                const int seg = 40;
                var v = new Vector3[seg + 1];
                var uv = new Vector2[seg + 1];
                var tri = new int[seg * 3];
                v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    v[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    uv[i + 1] = new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a));
                    tri[i * 3] = 0; tri[i * 3 + 1] = (i + 1) % seg + 1; tri[i * 3 + 2] = i + 1;
                }
                disc = new Mesh { name = "RotorDisc", vertices = v, uv = uv, triangles = tri };
                disc.RecalculateNormals();
                disc.RecalculateBounds();
                return disc;
            }
        }

        static Material DiscMat
        {
            get
            {
                if (discMat != null) return discMat;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, w = (y + 0.5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + w * w);
                        float ang = Mathf.Atan2(w, u);
                        // motion-blurred blades: two soft dark sweeps, darker towards the tips, a faint ring at the tip
                        float blades = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(ang * 2f), 6f);
                        float a = (0.18f + 0.55f * blades) * Mathf.SmoothStep(0.05f, 0.25f, r) * (1f - Mathf.SmoothStep(0.9f, 1f, r));
                        a += Mathf.Exp(-Mathf.Pow((r - 0.93f) * 22f, 2f)) * 0.25f;
                        tex.SetPixel(x, y, new Color(0.07f, 0.07f, 0.075f, Mathf.Clamp01(a)));
                    }
                tex.Apply(true);
                discMat = new Material(Shader.Find("SC/UnlitTint")) { mainTexture = tex };
                discMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                discMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                discMat.renderQueue = 3050;
                return discMat;
            }
        }

        /// <summary>The rotor rig of a helicopter prop (built once, reused by every contract).</summary>
        public static RotorFX On(GameObject heli)
        {
            if (heli == null) return null;
            var r = heli.GetComponent<RotorFX>();
            if (r == null) { r = heli.AddComponent<RotorFX>(); r.Build(); }
            return r;
        }

        void Build()
        {
            // the model faces +Z, pivot at the bottom: blades at the top, the mast a little ahead of the middle
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
                var mb = mf.sharedMesh.bounds;
                var c = transform.InverseTransformPoint(mf.transform.TransformPoint(mb.center));
                var e = Vector3.Scale(mb.extents, mf.transform.lossyScale);
                var lb = new Bounds(c, e * 2f);
                if (!any) { b = lb; any = true; } else b.Encapsulate(lb);
            }
            if (!any) b = new Bounds(new Vector3(0f, 2.4f, 0f), new Vector3(3.5f, 4.8f, 13f));
            float len = b.size.z;
            var hub = new Vector3(b.center.x, b.max.y - 0.12f, b.center.z + len * 0.1f);
            var tailHub = new Vector3(b.center.x + b.extents.x * 0.22f, b.min.y + b.size.y * 0.55f, b.min.z + len * 0.05f);
            float r = Mathf.Clamp(len * 0.42f, 3.5f, 6.5f), tr = Mathf.Clamp(len * 0.07f, 0.6f, 1.1f);
            main = MakeDisc("MainRotor", hub, r, false, out mainR);
            tail = MakeDisc("TailRotor", tailHub, tr, true, out tailR);
            var rs = new List<Renderer>();
            blades = MakeBlades("Blades", hub + new Vector3(0f, 0.04f, 0f), r, 0.3f, false, rs);
            tailBlades = MakeBlades("TailBlades", tailHub + new Vector3(0.03f, 0f, 0f), tr, 0.14f, true, rs);
            bladeRs = rs.ToArray();
            mpb = new MaterialPropertyBlock();
            Apply();
        }

        Transform MakeDisc(string name, Vector3 local, float radius, bool vertical, out MeshRenderer r)
        {
            var go = new GameObject(name);
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localRotation = vertical ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            go.transform.localScale = Vector3.one * radius;
            go.AddComponent<MeshFilter>().sharedMesh = Disc;
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = DiscMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>Spin up (1) or down (0) over <paramref name="seconds"/>.</summary>
        public void SetSpin(float target, float seconds)
        {
            from = Spin;
            to = Mathf.Clamp01(target);
            t0 = Time.time;
            dur = Mathf.Max(0.01f, seconds);
            if (src == null && Sfx.Rotor != null)
            {
                src = Sfx.Loop3D(transform, Sfx.Rotor, 0f, 650f);
                if (src != null) src.minDistance = 35f;
            }
        }

        /// <summary>Stopped, silent (contract reset).</summary>
        public void Stop()
        {
            from = to = Spin = 0f;
            dur = 0f;
            Apply();
        }

        void Update()
        {
            if (dur > 0f)
            {
                float k = Mathf.Clamp01((Time.time - t0) / dur);
                Spin = Mathf.Lerp(from, to, k * k * (3f - 2f * k));
                if (k >= 1f) dur = 0f;
            }
            angle += Time.deltaTime * Mathf.Lerp(0f, 1500f, Spin);
            Apply();
        }

        void Apply()
        {
            if (main == null) return;
            // slow: solid blades turning; fast: only the blurred disc
            bool on = Spin > 0.25f;
            if (mainR.enabled != on) { mainR.enabled = on; tailR.enabled = on; }
            bool solid = Spin < 0.6f;
            foreach (var br in bladeRs) if (br != null && br.enabled != solid) br.enabled = solid;
            if (solid)
            {
                blades.localRotation = Quaternion.Euler(0f, angle, 0f);
                tailBlades.localRotation = Quaternion.Euler(angle * 1.6f, 0f, 90f);
            }
            if (on)
            {
                main.localRotation = Quaternion.Euler(0f, angle, 0f);
                tail.localRotation = Quaternion.Euler(angle * 1.6f, 0f, 90f);
                mpb.SetColor("_Color", new Color(1f, 1f, 1f, Mathf.Clamp01((Spin - 0.2f) * 1.5f)));
                mainR.SetPropertyBlock(mpb);
                tailR.SetPropertyBlock(mpb);
            }
            if (src != null)
            {
                src.volume = Spin * 0.95f;
                src.pitch = 0.55f + 0.45f * Spin;
                if (Spin <= 0.01f && src.isPlaying) src.Pause();
                else if (Spin > 0.01f && !src.isPlaying) src.Play();
            }
        }
    }

    /// <summary>
    /// A rain umbrella over a character: it floats over the head (steady, as if held up in front), hides with him and
    /// drops to the ground when he falls. It sits on the character's root, not on the model: no thermal heat, no hit
    /// zone, and bullets go through the fabric.
    /// </summary>
    public class UmbrellaHolder : MonoBehaviour
    {
        NPC npc;
        Transform umb;
        bool dropped;
        float dropT;
        Vector3 dropFrom, dropTo;
        Quaternion rotFrom, rotTo;

        public static UmbrellaHolder Give(NPC n, Color tint)
        {
            if (n == null) return null;
            var prefab = Res.Prop("ai_rain_umbrella");
            if (prefab == null) return null;
            var h = n.gameObject.AddComponent<UmbrellaHolder>();
            h.npc = n;
            var go = Object.Instantiate(prefab, n.transform);
            go.name = "Umbrella";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            Util.SetLayer(go, Layers.IgnoreRaycast);
            var mpb = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", tint);
                r.SetPropertyBlock(mpb);
            }
            h.umb = go.transform;
            h.Place();
            return h;
        }

        void LateUpdate()
        {
            if (npc == null || umb == null) return;
            bool show = !npc.IsHidden && !npc.Escaped;
            if (umb.gameObject.activeSelf != show) umb.gameObject.SetActive(show);
            if (!show) return;
            if (!dropped && (npc.Dead || npc.Down || npc.Sedated))
            {
                dropped = true;
                dropT = 0f;
                dropFrom = umb.position;
                rotFrom = umb.rotation;
                var side = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                var p = dropFrom + side * Random.Range(0.6f, 1.3f);
                p.y = Level.GroundY(p + Vector3.up, npc.transform.position.y) + 0.25f;
                dropTo = p;
                rotTo = Quaternion.LookRotation(side, Vector3.up) * Quaternion.Euler(Random.Range(70f, 95f), 0f, Random.Range(-20f, 20f));
            }
            if (dropped)
            {
                if (dropT >= 1f) return;
                dropT = Mathf.Min(1f, dropT + Time.deltaTime / 0.9f);
                float k = dropT * dropT;
                umb.position = Vector3.Lerp(dropFrom, dropTo, k) + Vector3.up * Mathf.Sin(dropT * Mathf.PI) * 0.25f;
                umb.rotation = Quaternion.Slerp(rotFrom, rotTo, Util.EaseOutCubic(dropT));
                return;
            }
            Place();
        }

        void Place()
        {
            var t = npc.transform;
            var head = npc.HeadBone != null ? npc.HeadBone.position : t.position + Vector3.up * npc.Height * 0.93f;
            var fwd = t.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            // handle at chest height a little in front, canopy well over the head, tilted slightly back
            umb.position = new Vector3(head.x, head.y - 0.46f, head.z) + fwd * 0.16f;
            umb.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(-7f, 0f, 0f);
        }
    }

    /// <summary>Press photographers: camera flashes popping while <see cref="On"/> (a bright burst when the star of the
    /// evening poses). Lives on the contract's root: gone with the contract.</summary>
    public class FlashCrew : MonoBehaviour
    {
        public readonly List<NPC> crew = new List<NPC>();
        public bool On;
        public float rate = 1f;           // bursts per second, all photographers together
        float next;

        void Update()
        {
            if (!On || crew.Count == 0 || Time.time < next) return;
            next = Time.time + Random.Range(0.12f, 0.9f) / Mathf.Max(0.2f, rate);
            var n = crew[Random.Range(0, crew.Count)];
            if (n == null || n.Incapacitated || n.Panicking || n.IsHidden || n.Escaped) return;
            CityFX.CameraFlash(n.HandPoint + Vector3.up * 0.15f + n.transform.forward * 0.2f);
            // photographers often fire two or three frames in a row
            if (Random.value < 0.45f) next = Time.time + 0.09f;
        }
    }

    public static class CityFX
    {
        /// <summary>A camera flash: a very short, very bright pop of white light and a flare (from a small pool: the press
        /// fires several a second, and only one flash at a time gets a real light).</summary>
        public static void CameraFlash(Vector3 pos)
        {
            FlashPool.Pop(pos);
            Sfx.PlayAt(Sfx.Flash, pos, 0.3f);
        }
    }

    /// <summary>Four reusable flash flares and one shared light for the press photographers.</summary>
    public class FlashPool : MonoBehaviour
    {
        static FlashPool inst;
        const int Count = 4;
        readonly Transform[] flares = new Transform[Count];
        readonly MeshRenderer[] rs = new MeshRenderer[Count];
        readonly float[] age = new float[Count];
        MaterialPropertyBlock mpb;
        Light lamp;
        float lampAge = 9f;
        int next;

        public static void Pop(Vector3 pos)
        {
            if (inst == null)
            {
                var go = new GameObject("FlashPool");
                go.layer = Layers.IgnoreRaycast;
                inst = go.AddComponent<FlashPool>();
                inst.Build();
            }
            inst.Fire(pos);
        }

        void Build()
        {
            mpb = new MaterialPropertyBlock();
            for (int i = 0; i < Count; i++)
            {
                var q = Util.QuadObject("Flare", transform, FX.AddMat);
                q.transform.localScale = Vector3.one * 0.55f;
                flares[i] = q.transform;
                rs[i] = q.GetComponent<MeshRenderer>();
                rs[i].enabled = false;
                age[i] = 9f;
            }
            var lg = new GameObject("FlashLight");
            lg.transform.SetParent(transform, false);
            lamp = lg.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(0.92f, 0.95f, 1f);
            lamp.range = 9f;
            lamp.shadows = LightShadows.None;
            lamp.renderMode = LightRenderMode.ForcePixel;
            lamp.enabled = false;
        }

        void Fire(Vector3 pos)
        {
            int i = next;
            next = (next + 1) % Count;
            flares[i].position = pos;
            age[i] = 0f;
            rs[i].enabled = true;
            lamp.transform.position = pos;
            lamp.intensity = 7f;
            lamp.enabled = true;
            lampAge = 0f;
        }

        void LateUpdate()
        {
            var cam = Sniper.I != null ? Sniper.I.Cam : Camera.main;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < Count; i++)
            {
                if (!rs[i].enabled) continue;
                age[i] += dt;
                if (age[i] >= 0.16f) { rs[i].enabled = false; continue; }
                if (cam != null) flares[i].rotation = cam.transform.rotation;
                mpb.SetColor("_Color", new Color(1f, 1f, 1f, 1f - age[i] / 0.16f * 0.6f));
                rs[i].SetPropertyBlock(mpb);
            }
            if (lamp.enabled)
            {
                lampAge += dt;
                lamp.intensity = Mathf.Max(0f, 7f - lampAge * 80f);
                if (lamp.intensity <= 0.01f) lamp.enabled = false;
            }
        }
    }

    /// <summary>
    /// A rifle in a marksman's hands. A shouldered aim clip (kneeling or standing) supplies the arms; every frame the
    /// spine leans so that the line from the right hand to the left hand points at the mark, and the rifle is laid along
    /// that line with its grip in the right hand. When he goes down the rifle falls.
    /// </summary>
    public class RifleHold : MonoBehaviour
    {
        public NPC npc;
        public Transform rifle;
        public string pose;                      // the shouldered aim clip; any other clip: the rifle is carried
        public System.Func<Vector3> look;        // where he points (world)
        Transform[] spine;
        Transform leftHand;
        readonly Quaternion[] pre = new Quaternion[3], post = new Quaternion[3];
        bool primed, dropped;
        const float Grip = 0.30f;                // rifle pivot -> pistol grip, along +X (the stock side)

        public static RifleHold Give(NPC n, Transform rifle, string pose, System.Func<Vector3> look)
        {
            if (n == null || rifle == null) return null;
            var h = n.gameObject.AddComponent<RifleHold>();
            h.npc = n;
            h.rifle = rifle;
            h.pose = pose;
            h.look = look;
            h.spine = new[] { Util.FindDeep(n.transform, "mixamorig:Spine"), Util.FindDeep(n.transform, "mixamorig:Spine1"), Util.FindDeep(n.transform, "mixamorig:Spine2") };
            h.leftHand = Util.FindDeep(n.transform, "mixamorig:LeftHand");
            return h;
        }

        void LateUpdate()
        {
            if (npc == null || rifle == null || dropped) return;
            bool show = !npc.IsHidden && !npc.Escaped;
            if (rifle.gameObject.activeSelf != show) rifle.gameObject.SetActive(show);
            if (!show) return;
            if (npc.Dead || npc.Incapacitated || (npc.Wounded && npc.WoundZone == BodyZone.Arm)) { Drop(); return; }
            var rh = npc.RightHand;
            if (rh == null) return;
            bool bones = leftHand != null && spine[0] != null && spine[1] != null && spine[2] != null;
            // the clip rewrites the bones every frame it is sampled; when it is culled (off screen) undo last frame's lean
            if (bones)
            {
                for (int i = 0; i < 3; i++)
                {
                    var cur = spine[i].localRotation;
                    if (primed && cur == post[i]) cur = pre[i];
                    spine[i].localRotation = cur;
                    pre[i] = cur;
                }
            }
            Vector3 dir;
            if (npc.ClipName == pose)
            {
                var target = look != null ? look() : rh.position + npc.transform.forward * 20f;
                if (bones)
                {
                    var from = leftHand.position - rh.position;
                    var to = target - rh.position;
                    if (from.sqrMagnitude > 1e-4f && to.sqrMagnitude > 1e-4f)
                    {
                        Quaternion.FromToRotation(from.normalized, to.normalized).ToAngleAxis(out float ang, out Vector3 axis);
                        if (ang > 180f) ang -= 360f;
                        ang = Mathf.Clamp(ang, -55f, 55f);
                        if (axis.sqrMagnitude > 1e-6f && Mathf.Abs(ang) > 0.05f)
                        {
                            var part = Quaternion.AngleAxis(ang / 3f, axis);
                            for (int i = 0; i < 3; i++) spine[i].rotation = part * spine[i].rotation;
                        }
                    }
                }
                dir = target - rh.position;
            }
            else
            {
                // carried: muzzle down and forward
                var f = npc.transform.forward; f.y = 0f;
                dir = (f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward) * 0.5f + Vector3.down * 0.85f;
            }
            if (bones)
            {
                for (int i = 0; i < 3; i++) post[i] = spine[i].localRotation;
                primed = true;
            }
            if (dir.sqrMagnitude < 1e-4f) dir = npc.transform.forward;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
            rifle.rotation = rot;
            rifle.position = rh.position - rot * new Vector3(Grip, -0.04f, 0f);
        }

        void Drop()
        {
            dropped = true;
            // no more scope glint from a rifle on the floor; it stays under the contract's root (gone with the contract)
            foreach (var g in rifle.GetComponentsInChildren<ScopeGlint>(true)) Destroy(g.gameObject);
            rifle.SetParent(npc.transform.parent, true);
            var col = rifle.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1.3f, 0.16f, 0.07f);
            // it starts inside his hands and shoulder: never collide with his own body
            foreach (var c in npc.GetComponentsInChildren<Collider>(true))
                if (c != null && c != col) Physics.IgnoreCollision(col, c, true);
            var rb = rifle.gameObject.AddComponent<Rigidbody>();
            rb.mass = 6f;
            rb.maxDepenetrationVelocity = 1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.AddForce(npc.transform.forward * 0.6f + Vector3.down * 0.4f, ForceMode.VelocityChange);
            rb.AddTorque(new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f), Random.Range(-1f, 1f)), ForceMode.VelocityChange);
            Destroy(rifle.gameObject, 40f);
        }
    }
}

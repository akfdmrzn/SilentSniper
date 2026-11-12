using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// The enemy's fire as seen from the nest: a muzzle flash that stays visible however far away the gunman is (a
    /// star of light a few pixels wide at least, plus a puff of smoke), tracer streaks flying in, and rounds smacking
    /// into the parapet right in front of the player.
    /// </summary>
    public static class CombatFX
    {
        static Transform root;
        static Material flashMat, tracerMat;
        static Texture2D flashTex;
        static readonly List<TracerFX> tracers = new List<TracerFX>();
        static readonly List<Star> flashes = new List<Star>();
        static ParticleSystem dustPS, chipPS;

        static Transform Root
        {
            get
            {
                if (root == null) root = new GameObject("_CombatFX").transform;
                return root;
            }
        }

        public static void ClearAll()
        {
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
            tracers.Clear();
            flashes.Clear();
            dustPS = chipPS = null;
        }

        static Texture2D FlashTex
        {
            get
            {
                if (flashTex != null) return flashTex;
                const int n = 64;
                flashTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float ang = Mathf.Atan2(v, u);
                        float core = Mathf.Exp(-r * r * 26f);
                        float halo = Mathf.Exp(-r * r * 6f) * 0.3f;
                        // five uneven petals of flame
                        float petals = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2.5f + 0.4f)), 6f) * Mathf.Exp(-r * 3.2f) * 0.75f;
                        float a = Mathf.Clamp01(core + halo + petals) * Mathf.Clamp01(1.05f - r);
                        flashTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                flashTex.Apply(true);
                return flashTex;
            }
        }

        static Material FlashMat
        {
            get
            {
                if (flashMat != null) return flashMat;
                flashMat = new Material(Shader.Find("SC/UnlitTint")) { mainTexture = FlashTex };
                flashMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                flashMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                flashMat.renderQueue = 3100;
                return flashMat;
            }
        }

        static Material TracerMat
        {
            get
            {
                if (tracerMat != null) return tracerMat;
                tracerMat = new Material(FX.AddMat);
                tracerMat.renderQueue = 3090;
                return tracerMat;
            }
        }

        /// <summary>A gun firing at <paramref name="pos"/> towards <paramref name="dir"/>: flash (never smaller than a few
        /// pixels from the nest) and a short light. Pooled: a firefight does not create objects per round.</summary>
        public static void MuzzleFlash(Vector3 pos, Vector3 dir, float size = 1f)
        {
            Star s = null;
            foreach (var x in flashes) if (x != null && !x.Busy) { s = x; break; }
            if (s == null)
            {
                if (flashes.Count >= 16) return;
                var go = new GameObject("muzzle_star");
                go.layer = Layers.IgnoreRaycast;
                go.transform.SetParent(Root, false);
                var q = Util.QuadObject("Flash", go.transform, FlashMat);
                s = go.AddComponent<Star>();
                s.quad = q.transform;
                s.mr = q.GetComponent<MeshRenderer>();
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.72f, 0.4f);
                l.shadows = LightShadows.None;
                s.lamp = l;
                flashes.Add(s);
            }
            var night = MissionRuntime.I != null && MissionRuntime.I.IsNight;
            var d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            s.Fire(pos + d * 0.08f, 0.32f * size, night ? 9f : 5f, night ? 6f : 3f);
        }

        /// <summary>A tracer streak from <paramref name="from"/> flying past <paramref name="to"/> at
        /// <paramref name="speed"/> m/s (game time).</summary>
        public static void Tracer(Vector3 from, Vector3 to, float speed, float overshoot = 40f)
        {
            TracerFX t = null;
            foreach (var x in tracers) if (x != null && !x.busy) { t = x; break; }
            if (t == null)
            {
                if (tracers.Count >= 20) return;
                var go = new GameObject("tracer");
                go.layer = Layers.IgnoreRaycast;
                go.transform.SetParent(Root, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.sharedMaterial = TracerMat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.numCapVertices = 2;
                lr.textureMode = LineTextureMode.Stretch;
                t = go.AddComponent<TracerFX>();
                t.lr = lr;
                tracers.Add(t);
            }
            t.Launch(from, to, speed, overshoot);
        }

        /// <summary>A round smacking into the parapet (or the roof) close in front of the player: a burst of grit and
        /// dust, chips, the crack of it (<paramref name="sound"/>: rounds in quick succession share one).</summary>
        public static void ParapetHit(Vector3 pos, Vector3 normal, bool ricochet, bool sound = true)
        {
            var n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            if (dustPS == null) dustPS = MakeEmitter("parapet_dust", FX.AlphaMat, 0.25f, 160, 2.5f);
            if (chipPS == null) chipPS = MakeEmitter("parapet_chips", FX.AlphaMat, 1.5f, 120, 1f);
            Emit(dustPS, pos, n, 16, 2.2f, 0.28f, 1.0f, 0.55f, new Color(0.40f, 0.38f, 0.33f, 0.45f));
            Emit(chipPS, pos, n, 10, 5f, 0.04f, 0.5f, 0.85f, new Color(0.13f, 0.115f, 0.1f, 1f));
            if (!sound) return;
            Sfx.PlayIncoming(Sfx.Impact, 0.9f, Random.Range(0.85f, 1.15f), 0f);
            Sfx.PlayIncoming(Sfx.Debris, 0.55f, Random.Range(0.9f, 1.1f), 0f);
            if (ricochet) Sfx.PlayIncoming(Sfx.Ricochet, 0.45f, Random.Range(0.9f, 1.15f), 0f);
        }

        /// <summary>A particle system that only ever emits by hand (one per kind of debris, shared by every hit).</summary>
        static ParticleSystem MakeEmitter(string name, Material mat, float gravity, int max, float sizeGrow)
        {
            var go = new GameObject(name);
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(Root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startSize = 0.1f;
            var em = ps.emission;
            em.enabled = false;
            var sh = ps.shape;
            sh.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(0.85f, 0.35f), new GradientAlphaKey(0, 1) });
            col.color = g;
            if (sizeGrow != 1f)
            {
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, sizeGrow));
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary><paramref name="count"/> particles from <paramref name="pos"/> in a cone round
        /// <paramref name="normal"/> (<paramref name="spread"/>: tangent of its half-angle).</summary>
        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 normal, int count, float speed, float size, float life, float spread, Color color)
        {
            if (ps == null) return;
            var ep = new ParticleSystem.EmitParams { position = pos, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                var d = (normal + Random.insideUnitSphere * spread).normalized;
                ep.velocity = d * speed * Random.Range(0.3f, 1f);
                ep.startSize = size * Random.Range(0.5f, 1f);
                ep.startLifetime = life * Random.Range(0.5f, 1f);
                ep.startColor = color;
                ps.Emit(ep, 1);
            }
        }

        /// <summary>Camera-facing flash that keeps a minimum size on screen, with a short burst of light (pooled).</summary>
        class Star : MonoBehaviour
        {
            public Transform quad;
            public MeshRenderer mr;
            public Light lamp;
            float size, t, roll, lightMax;
            const float Life = 0.075f, LightLife = 0.09f;
            static readonly Color Tint = new Color(1f, 0.78f, 0.42f, 1f);
            MaterialPropertyBlock mpb;
            static readonly int idColor = Shader.PropertyToID("_Color");

            public bool Busy => gameObject.activeSelf;

            public void Fire(Vector3 pos, float s, float range, float intensity)
            {
                transform.position = pos;
                size = s;
                t = 0f;
                roll = Random.value * 360f;
                lightMax = intensity;
                if (lamp != null) { lamp.range = range; lamp.intensity = intensity; lamp.enabled = true; }
                mr.enabled = true;
                gameObject.SetActive(true);
                Place();
            }

            void LateUpdate()
            {
                t += Time.unscaledDeltaTime;
                if (t > LightLife && t > Life) { gameObject.SetActive(false); return; }
                Place();
            }

            void Place()
            {
                if (lamp != null) lamp.intensity = lightMax * Mathf.Clamp01(1f - t / LightLife);
                var cam = Sniper.I != null && Sniper.I.Cam != null ? Sniper.I.Cam : Camera.main;
                if (cam == null || quad == null) return;
                float k = Mathf.Clamp01(1f - t / Life);
                var to = cam.transform.position - transform.position;
                float d = to.magnitude;
                // at least ~9 px tall on screen
                float px = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(200f, Screen.height);
                float s = Mathf.Max(size, px * 9f) * (0.6f + 0.4f * k);
                quad.rotation = Quaternion.LookRotation(-to.normalized, cam.transform.up) * Quaternion.Euler(0f, 0f, roll);
                quad.localScale = new Vector3(s, s, s);
                if (mpb == null) mpb = new MaterialPropertyBlock();
                mpb.SetColor(idColor, new Color(Tint.r, Tint.g, Tint.b, Tint.a * k));
                mr.SetPropertyBlock(mpb);
                if (t > Life) mr.enabled = false;
            }
        }

        class TracerFX : MonoBehaviour
        {
            public LineRenderer lr;
            public bool busy;
            Vector3 from, dir;
            float speed, travelled, total;
            const float Len = 9f;

            public void Launch(Vector3 a, Vector3 b, float spd, float overshoot)
            {
                from = a;
                var d = b - a;
                total = d.magnitude + overshoot;               // it flies on past the nest
                dir = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
                speed = Mathf.Max(50f, spd);
                travelled = 0f;
                busy = true;
                lr.enabled = true;
                Step(0f);
            }

            void Update()
            {
                if (!busy) return;
                Step(Time.deltaTime);
            }

            void Step(float dt)
            {
                travelled += speed * dt;
                if (travelled > total) { busy = false; lr.enabled = false; return; }
                var head = from + dir * travelled;
                var tail = from + dir * Mathf.Max(0f, travelled - Len);
                lr.SetPosition(0, head);
                lr.SetPosition(1, tail);
                // about two and a half pixels wide on screen at any range and any zoom
                var cam = Sniper.I != null && Sniper.I.Cam != null ? Sniper.I.Cam : Camera.main;
                float d = cam != null ? Vector3.Distance(cam.transform.position, head) : 100f;
                float fov = cam != null ? cam.fieldOfView : 55f;
                float px = 2f * d * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(200f, Screen.height);
                lr.widthMultiplier = Mathf.Max(0.004f, px * 2.6f);
                var c = new Color(1f, 0.82f, 0.45f, 0.95f);
                lr.startColor = c;
                lr.endColor = new Color(1f, 0.55f, 0.2f, 0f);
            }
        }
    }
}

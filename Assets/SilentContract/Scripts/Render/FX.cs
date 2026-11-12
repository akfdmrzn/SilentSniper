using System.Collections;
using UnityEngine;

namespace SilentContract
{
    /// <summary>Particle bursts, decals and other visual effects (all built from code, mobile friendly).</summary>
    public static class FX
    {
        static Transform root;
        static Material alphaMat, addMat, decalMat;
        static Texture2D dotTex, splatTex;

        static Transform Root
        {
            get
            {
                if (root == null) root = new GameObject("_FX").transform;
                return root;
            }
        }

        public static void ClearAll()
        {
            // splashes carried by moving props are not under the root: they go too
            foreach (var d in decals) if (d != null) Object.Destroy(d);
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
            decals.Clear();
        }

        static Shader Unlit => Shader.Find("SC/UnlitTint");

        static Texture2D Dot
        {
            get
            {
                if (dotTex != null) return dotTex;
                var s = Res.Sprite("softdot");
                if (s != null) return dotTex = s.texture;
                dotTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float d = Mathf.Clamp01(1f - new Vector2(x - 15.5f, y - 15.5f).magnitude / 16f);
                        dotTex.SetPixel(x, y, new Color(1, 1, 1, d * d));
                    }
                dotTex.Apply();
                return dotTex;
            }
        }

        static Texture2D Splat
        {
            get
            {
                if (splatTex != null) return splatTex;
                const int n = 128;
                splatTex = new Texture2D(n, n, TextureFormat.RGBA32, true);
                var rnd = new System.Random(5);
                var blobs = new Vector3[14];
                for (int i = 0; i < blobs.Length; i++)
                    blobs[i] = new Vector3((float)rnd.NextDouble() * 0.6f + 0.2f, (float)rnd.NextDouble() * 0.6f + 0.2f, (float)rnd.NextDouble() * 0.16f + 0.05f);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = x / (float)n, v = y / (float)n;
                        float a = Mathf.Clamp01(1.6f - new Vector2(u - 0.5f, v - 0.5f).magnitude * 3.4f);
                        foreach (var b in blobs) a = Mathf.Max(a, Mathf.Clamp01(1.3f - new Vector2(u - b.x, v - b.y).magnitude / b.z));
                        a = Mathf.SmoothStep(0, 1, a);
                        splatTex.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                splatTex.Apply(true);
                return splatTex;
            }
        }

        public static Material AlphaMat
        {
            get
            {
                if (alphaMat != null) return alphaMat;
                alphaMat = new Material(Unlit) { mainTexture = Dot };
                alphaMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                alphaMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                return alphaMat;
            }
        }

        public static Material AddMat
        {
            get
            {
                if (addMat != null) return addMat;
                addMat = new Material(Unlit) { mainTexture = Dot };
                addMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                addMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                return addMat;
            }
        }

        static Material DecalMat
        {
            get
            {
                if (decalMat != null) return decalMat;
                decalMat = new Material(Unlit) { mainTexture = Splat };
                decalMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                decalMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                decalMat.renderQueue = 2450;
                return decalMat;
            }
        }

        static ParticleSystem Burst(Vector3 pos, Vector3 normal, Color color, int count, float speed, float size, float life, bool additive, float gravity, float cone = 35f, float sizeGrow = 1f)
            => Burst(pos, normal, color, count, speed, size, life, additive, gravity, cone, sizeGrow, false, null);

        /// <param name="exact">the particles show <paramref name="color"/> itself (the older effects were tuned with the
        /// colour applied twice, start colour times the fade: they keep that look)</param>
        static ParticleSystem Burst(Vector3 pos, Vector3 normal, Color color, int count, float speed, float size, float life, bool additive, float gravity,
                                    float cone, float sizeGrow, bool stretch, Material mat, bool exact = false)
        {
            var go = new GameObject("burst");
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0 ? normal : Vector3.up);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count * 2;
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = cone;
            sh.radius = 0.03f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            if (exact)
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                          new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0, 1) });
            else
                g.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) },
                          new[] { new GradientAlphaKey(color.a, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            if (sizeGrow != 1f)
            {
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, sizeGrow));
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat != null ? mat : (additive ? AddMat : AlphaMat);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (stretch)
            {
                // droplets: streaks along their flight (they read as liquid, not as dots, in slow motion)
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.035f;
                r.lengthScale = 1.6f;
            }
            ps.Play();
            Object.Destroy(go, life + 1f);
            return ps;
        }

        // ------------------------------------------------------------------ blood
        static Texture2D dropTex, spatterTex;
        static Material dropMat, spatterMat;
        static readonly System.Collections.Generic.Queue<GameObject> decals = new System.Collections.Generic.Queue<GameObject>();
        const int MaxDecals = 70;

        /// <summary>A round drop with a firmer edge than the soft dot (spray droplets).</summary>
        static Texture2D DropTex
        {
            get
            {
                if (dropTex != null) return dropTex;
                const int n = 32;
                dropTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = new Vector2(x - 15.5f, y - 15.5f).magnitude / 15.5f;
                        float a = Mathf.Clamp01((1f - d) * 3.2f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * a * 255f));
                    }
                dropTex.SetPixels32(px);
                dropTex.Apply(true);
                return dropTex;
            }
        }

        /// <summary>A splash on a wall: a dense patch with drops and streaks thrown out towards +u (the way the blood flew).</summary>
        static Texture2D SpatterTex
        {
            get
            {
                if (spatterTex != null) return spatterTex;
                const int n = 128;
                spatterTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                var rnd = new System.Random(11);
                var a = new float[n * n];
                System.Action<float, float, float, float, float> blob = (cx, cy, rx, ry, w) =>
                {
                    int x0 = Mathf.Max(0, (int)((cx - rx) * n) - 1), x1 = Mathf.Min(n - 1, (int)((cx + rx) * n) + 1);
                    int y0 = Mathf.Max(0, (int)((cy - ry) * n) - 1), y1 = Mathf.Min(n - 1, (int)((cy + ry) * n) + 1);
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            float u = ((x + 0.5f) / n - cx) / rx, v = ((y + 0.5f) / n - cy) / ry;
                            float q = Mathf.Clamp01(1.25f - Mathf.Sqrt(u * u + v * v)) * 4f;
                            a[y * n + x] = Mathf.Max(a[y * n + x], Mathf.Clamp01(q) * w);
                        }
                };
                // the patch where it struck (left of centre), then drops and streaks flung on to the right
                for (int i = 0; i < 9; i++)
                    blob(0.3f + (float)rnd.NextDouble() * 0.12f, 0.5f + ((float)rnd.NextDouble() - 0.5f) * 0.16f, 0.06f + (float)rnd.NextDouble() * 0.05f, 0.05f + (float)rnd.NextDouble() * 0.05f, 1f);
                for (int i = 0; i < 46; i++)
                {
                    float ang = ((float)rnd.NextDouble() - 0.5f) * 1.2f;
                    float dist = 0.08f + (float)rnd.NextDouble() * 0.5f;
                    float cx = 0.34f + Mathf.Cos(ang) * dist, cy = 0.5f + Mathf.Sin(ang) * dist;
                    float s = Mathf.Lerp(0.035f, 0.008f, dist / 0.58f) * (0.6f + (float)rnd.NextDouble() * 0.8f);
                    blob(cx, cy, s * (1.4f + dist * 3f), s, 0.95f);
                }
                var px = new Color32[n * n];
                for (int i = 0; i < n * n; i++)
                {
                    float v = Mathf.SmoothStep(0f, 1f, a[i]);
                    px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(v * 255f));
                }
                spatterTex.SetPixels32(px);
                spatterTex.Apply(true);
                return spatterTex;
            }
        }

        static Material DropMat
        {
            get
            {
                if (dropMat != null) return dropMat;
                dropMat = new Material(Unlit) { mainTexture = DropTex };
                dropMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                dropMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                return dropMat;
            }
        }

        static Material SpatterMat
        {
            get
            {
                if (spatterMat != null) return spatterMat;
                spatterMat = new Material(Unlit) { mainTexture = SpatterTex };
                spatterMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                spatterMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                spatterMat.renderQueue = 2450;
                return spatterMat;
            }
        }

        /// <summary>Blood is unlit: dimmer at night so it does not glow in the dark.</summary>
        static float BloodLight => MissionRuntime.I != null && MissionRuntime.I.IsNight ? 0.55f : 1f;

        static readonly Material[] decalMats = new Material[4];

        /// <summary>Wall / floor blood: one shared material per kind and light (no per-decal property blocks).</summary>
        static Material DecalMatFor(bool spatter, bool night)
        {
            int i = (spatter ? 2 : 0) + (night ? 1 : 0);
            if (decalMats[i] != null) return decalMats[i];
            var m = new Material(spatter ? SpatterMat : DecalMat);
            float lum = night ? 0.55f : 1f;
            m.SetColor("_Color", new Color(0.24f * lum, 0.005f, 0.005f, 0.9f));
            return decalMats[i] = m;
        }

        /// <summary>Builds the blood textures and materials ahead of time (no hitch on the first kill).</summary>
        public static void Warm()
        {
            var a = DropMat; var b = SpatterMat; var c = DecalMat;
            for (int i = 0; i < 4; i++) DecalMatFor(i >= 2, (i & 1) == 1);
        }

        /// <summary>
        /// A bullet through a body: a puff back out of the entry wound, a spray of drops thrown on the way the bullet
        /// went (streaks in slow motion), a fine red mist that hangs for a moment, and a splash on the wall or the floor
        /// behind (the drops land a beat later). Bigger for a head and for a kill, small for a limb.
        /// </summary>
        public static void BloodHit(Vector3 point, Vector3 bulletDir, BodyZone zone, bool headshot, bool lethal, bool riding = false)
        {
            var d = bulletDir.sqrMagnitude > 1e-6f ? bulletDir.normalized : Vector3.forward;
            float k = headshot ? 1.7f : (lethal ? 1.15f : (zone == BodyZone.Arm || zone == BodyZone.Leg ? 0.6f : 0.85f));
            float lum = BloodLight;
            var dark = new Color(0.36f * lum, 0.01f, 0.01f, 0.95f);
            var bright = new Color(0.55f * lum, 0.03f * lum, 0.03f * lum, 0.9f);
            // entry: a short puff back towards the rifle
            Burst(point, -d, dark, Mathf.RoundToInt(9 * k), 1.8f, 0.035f, 0.45f, false, 1.3f, 40f, 1f, true, DropMat, true);
            // exit: the spray, flung on with the bullet
            Burst(point + d * 0.1f, d, bright, Mathf.RoundToInt(34 * k), 7.5f, 0.045f, 0.85f, false, 1.15f, 16f, 1f, true, DropMat, true);
            Burst(point + d * 0.1f, d, dark, Mathf.RoundToInt(16 * k), 3.2f, 0.06f, 1.0f, false, 1.4f, 30f, 1f, true, DropMat, true);
            // mist: hangs and spreads (the heart of the slow-motion frame)
            Burst(point + d * 0.12f, d, new Color(0.6f * lum, 0.04f * lum, 0.04f * lum, headshot ? 0.5f : 0.36f), Mathf.RoundToInt(7 * k), 0.9f, 0.16f * Mathf.Sqrt(k), 1.3f, false, -0.02f, 28f, 3.2f, false, null, true);
            if (headshot) Burst(point, d, new Color(0.7f * lum, 0.08f * lum, 0.07f * lum, 0.35f), 8, 2.2f, 0.22f, 0.9f, false, 0.05f, 50f, 2.4f, false, null, true);
            // in a moving cabin or car the splashes would hang in the air where the vehicle was: only the spray
            if (riding) return;
            // the splash behind: a wall within reach takes it, and drops land on the floor along the way
            float wallAt = 99f;
            if (Physics.Raycast(point + d * 0.25f, d, out var h, 3.4f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                wallAt = h.distance + 0.25f;
                var along = Vector3.ProjectOnPlane(d, h.normal);
                if (along.sqrMagnitude < 1e-4f) along = Vector3.ProjectOnPlane(Vector3.down, h.normal);
                float size = Mathf.Lerp(0.55f, 1.25f, Mathf.Clamp01(wallAt / 3.4f)) * Mathf.Sqrt(k);
                // a moving thing (a car, a boat) carries its splash along
                var mover = h.collider.GetComponentInParent<PathMover>();
                BloodDecal(h.point, h.normal, along.normalized, size, wallAt / 14f, true, 0.92f, mover != null ? mover.transform : null);
            }
            int drops = Mathf.RoundToInt(3 * k);
            var flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
            flat.Normalize();
            for (int i = 0; i < drops; i++)
            {
                float dist = 0.35f + i * 0.55f + Random.value * 0.4f;
                if (dist > wallAt) break;
                var at = point + flat * dist + Vector3.Cross(Vector3.up, flat) * ((Random.value - 0.5f) * 0.5f);
                if (!Physics.Raycast(at + Vector3.up * 0.4f, Vector3.down, out var g, 3f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                if (g.point.y < Level.WaterY + 0.05f) continue;
                if (g.collider.GetComponentInParent<PathMover>() != null) continue;      // a deck or a car roof on the move
                BloodDecal(g.point, g.normal, flat, Random.Range(0.18f, 0.4f) * Mathf.Sqrt(k), 0.1f + dist / 6f, i % 2 == 0, 0.85f);
            }
        }

        /// <summary>A splash of blood on a surface (<paramref name="along"/>: the way it was flung, for the streaks). It
        /// appears after <paramref name="delay"/> game seconds (the drops' flight, slowed with the kill cam).</summary>
        public static void BloodDecal(Vector3 pos, Vector3 normal, Vector3 along, float size, float delay, bool spatter, float alpha, Transform parent = null)
        {
            var go = Util.QuadObject(spatter ? "bloodspatter" : "blooddrop", Root, DecalMatFor(spatter, BloodLight < 0.9f));
            go.transform.position = pos + normal * 0.015f;
            var up = Vector3.Cross(-normal, along);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.Cross(-normal, Vector3.right);
            go.transform.rotation = Quaternion.LookRotation(-normal, up);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.localScale = Vector3.zero;
            var s = go.AddComponent<SplatGrow>();
            s.size = size;
            s.wait = delay;
            decals.Enqueue(go);
            while (decals.Count > MaxDecals)
            {
                var old = decals.Dequeue();
                if (old != null) Object.Destroy(old);
            }
        }

        /// <summary>A few drops where a wounded man is (crawling, limping): his trail.</summary>
        public static void BloodDrip(Vector3 pos)
        {
            if (!Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out var g, 2.5f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return;
            if (g.point.y < Level.WaterY + 0.05f) return;
            var along = Vector3.ProjectOnPlane(Random.onUnitSphere, g.normal);
            BloodDecal(g.point, g.normal, along.sqrMagnitude > 1e-4f ? along.normalized : Vector3.forward, Random.Range(0.12f, 0.28f), 0f, false, 0.8f);
        }

        /// <summary>A decal that waits for its drops to arrive, then spreads in a blink.</summary>
        class SplatGrow : MonoBehaviour
        {
            public float size, wait;
            float t;
            void Update()
            {
                if (wait > 0f) { wait -= Time.deltaTime; return; }
                t = Mathf.Min(1f, t + Time.deltaTime / 0.12f);
                transform.localScale = Vector3.one * size * Util.EaseOutCubic(t);
                if (t >= 1f) enabled = false;
            }
        }

        public static void Blood(Vector3 pos, Vector3 normal, bool headshot = false)
        {
            int k = headshot ? 2 : 1;
            Burst(pos, normal, new Color(0.42f, 0.0f, 0.0f, 0.95f), 26 * k, 3.5f, 0.07f, 0.7f, false, 1.4f, 30f);
            Burst(pos, normal, new Color(0.35f, 0.01f, 0.01f, 0.55f), 10 * k, 0.9f, 0.32f, 1.1f, false, 0.05f, 50f, 2.2f);
            // exit spray (behind the target)
            Burst(pos, -normal, new Color(0.4f, 0.0f, 0.0f, 0.85f), 18 * k, 5f, 0.06f, 0.6f, false, 1.2f, 18f);
        }

        public static void Dust(Vector3 pos, Vector3 normal, bool metal = false)
        {
            Burst(pos, normal, new Color(0.62f, 0.6f, 0.55f, 0.65f), 16, 2.2f, 0.28f, 1.0f, false, 0.25f, 30f, 2.5f);
            Burst(pos, normal, new Color(0.35f, 0.32f, 0.28f, 1f), 10, 5f, 0.04f, 0.5f, false, 1.5f, 40f);
            if (metal) Burst(pos, normal, new Color(1f, 0.75f, 0.35f, 1f), 14, 9f, 0.035f, 0.25f, true, 1f, 45f);
        }

        public static void Splash(Vector3 pos)
        {
            Burst(pos, Vector3.up, new Color(0.85f, 0.92f, 1f, 0.85f), 36, 6f, 0.16f, 1.1f, false, 1.4f, 12f);
            Burst(pos, Vector3.up, new Color(0.9f, 0.95f, 1f, 0.45f), 10, 1.2f, 0.9f, 1.6f, false, 0.1f, 60f, 2f);
            Sfx.PlayAt(Sfx.Splash, pos, 0.7f);
        }

        /// <summary>A window pane bursting: glittering shards fly on with the bullet and rain down, a fine dust of glass.</summary>
        public static void GlassShatter(Vector3 pos, Vector3 dir, Vector3 paneSize)
        {
            var d = dir.sqrMagnitude > 0 ? dir.normalized : Vector3.forward;
            float k = Mathf.Clamp(paneSize.x * paneSize.y, 1f, 6f);
            Burst(pos, d, new Color(0.85f, 0.93f, 1f, 0.95f), Mathf.RoundToInt(26 * k), 6.5f, 0.07f, 1.4f, false, 1.6f, 38f);
            Burst(pos, -d, new Color(0.8f, 0.9f, 1f, 0.8f), Mathf.RoundToInt(10 * k), 2.5f, 0.05f, 1.2f, false, 1.8f, 60f);
            Burst(pos, d, new Color(1f, 1f, 1f, 1f), 14, 9f, 0.03f, 0.35f, true, 0.6f, 25f);
            Burst(pos, Vector3.down, new Color(0.75f, 0.85f, 0.95f, 0.35f), 8, 0.6f, 0.5f, 1.2f, false, 0.2f, 70f, 1.8f);
        }

        /// <summary>Hit on an engine / machine: sparks and a puff of dark smoke.</summary>
        public static void Smoke(Vector3 pos)
        {
            Burst(pos, Vector3.up, new Color(1f, 0.75f, 0.35f, 1f), 18, 8f, 0.035f, 0.3f, true, 1f, 50f);
            Burst(pos, Vector3.up, new Color(0.12f, 0.11f, 0.1f, 0.75f), 26, 1.6f, 1.2f, 4.5f, false, -0.08f, 25f, 3f);
        }

        public static void BloodPool(Vector3 pos)
        {
            var go = Util.QuadObject("bloodpool", Root, DecalMat);
            go.transform.position = pos + Vector3.up * 0.025f;
            go.transform.rotation = Quaternion.Euler(90, Random.value * 360f, 0);
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", new Color(0.22f, 0.0f, 0.0f, 0.92f));
            go.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
            go.AddComponent<Grow>().target = Vector3.one * Random.Range(1.3f, 1.8f);
        }

        public static void Explosion(Vector3 pos)
        {
            Burst(pos, Vector3.up, new Color(1f, 0.55f, 0.15f, 1f), 60, 13f, 1.6f, 0.9f, true, -0.2f, 70f, 1.6f);
            Burst(pos, Vector3.up, new Color(1f, 0.9f, 0.5f, 1f), 30, 25f, 0.12f, 0.6f, true, 1f, 80f);
            Burst(pos + Vector3.up, Vector3.up, new Color(0.08f, 0.07f, 0.07f, 0.8f), 34, 5f, 3.2f, 3.5f, false, -0.15f, 40f, 2.4f);
            var lgo = new GameObject("flash");
            lgo.transform.SetParent(Root, false);
            lgo.transform.position = pos + Vector3.up * 1.5f;
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.25f);
            l.range = 45;
            l.intensity = 8;
            lgo.AddComponent<FadeLight>();
            Sfx.PlayAt(Sfx.Explosion, pos, 1f);
        }

        /// <summary>Explosive round: a compact blast (fire ball, sparks, smoke, flash) — kills within ~3 m.</summary>
        public static void ExplosionSmall(Vector3 pos)
        {
            Burst(pos, Vector3.up, new Color(1f, 0.6f, 0.2f, 1f), 34, 8f, 0.9f, 0.6f, true, -0.2f, 75f, 1.5f);
            Burst(pos, Vector3.up, new Color(1f, 0.85f, 0.45f, 1f), 24, 18f, 0.08f, 0.45f, true, 1f, 85f);
            Burst(pos + Vector3.up * 0.5f, Vector3.up, new Color(0.09f, 0.08f, 0.08f, 0.75f), 20, 3f, 2f, 2.6f, false, -0.12f, 45f, 2.2f);
            var lgo = new GameObject("flash");
            lgo.transform.SetParent(Root, false);
            lgo.transform.position = pos + Vector3.up * 0.8f;
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.62f, 0.28f);
            l.range = 22;
            l.intensity = 6;
            lgo.AddComponent<FadeLight>().speed = 12f;
            Sfx.PlayAt(Sfx.Explosion, pos, 0.75f);
        }

        /// <summary>Tranquilizer dart hit: a tiny puff, no blood.</summary>
        public static void Dart(Vector3 pos, Vector3 dir)
        {
            Burst(pos, -dir, new Color(0.75f, 0.9f, 0.75f, 0.55f), 8, 0.6f, 0.06f, 0.5f, false, 0.05f, 40f, 1.8f);
        }

        /// <summary>Muzzle flash of someone else's gun (hitman, guard), in the world.</summary>
        public static void MuzzleFlashWorld(Vector3 pos, Vector3 dir)
        {
            Burst(pos, dir, new Color(1f, 0.8f, 0.45f, 1f), 10, 6f, 0.06f, 0.12f, true, 0f, 20f);
            var lgo = new GameObject("gunflash");
            lgo.transform.SetParent(Root, false);
            lgo.transform.position = pos;
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.75f, 0.45f);
            l.range = 8f;
            l.intensity = 5f;
            lgo.AddComponent<FadeLight>().speed = 50f;
        }

        public static ParticleSystem BuildRain(Transform cam)
        {
            var go = new GameObject("Rain");
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0, 6, 8);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = 0.012f; main.startSizeY = 0.5f; main.startSizeZ = 1f;
            main.startColor = new Color(0.75f, 0.8f, 0.9f, 0.32f);
            main.gravityModifier = 2.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1800;
            var em = ps.emission;
            em.rateOverTime = Save.Quality == 0 ? 700 : 1400;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(30, 1, 30);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 1f;
            r.sharedMaterial = AlphaMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        public static void MuzzleFlash(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("muzzle");
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.75f, 0.45f);
            l.range = 6f;
            l.intensity = 4f;
            go.AddComponent<FadeLight>().speed = 40f;
            var q = Util.QuadObject("flash", go.transform, AddMat, Layers.IgnoreRaycast);
            q.transform.localScale = Vector3.one * 0.35f;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", new Color(1f, 0.8f, 0.5f, 1f));
            q.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
            Object.Destroy(go, 0.12f);
        }

        class Grow : MonoBehaviour
        {
            public Vector3 target;
            float t;
            void Start() { transform.localScale = Vector3.one * 0.05f; }
            void Update()
            {
                if (t >= 1) return;
                t += Time.deltaTime * 0.25f;
                transform.localScale = Vector3.Lerp(Vector3.one * 0.05f, target, Util.EaseOutCubic(t));
            }
        }

        public class FadeLight : MonoBehaviour
        {
            public float speed = 6f;
            Light l;
            void Start() { l = GetComponent<Light>(); }
            void Update()
            {
                if (l == null) return;
                l.intensity = Mathf.MoveTowards(l.intensity, 0, Time.unscaledDeltaTime * speed);
                if (l.intensity <= 0) Destroy(gameObject);
            }
        }
    }

    /// <summary>A wounded man bleeds where he goes: drops behind him as he crawls or limps, a spreading stain where he
    /// lies still. Ends with him (a dead body gets its pool from the death itself).</summary>
    public class BloodTrail : MonoBehaviour
    {
        NPC npc;
        float rate;
        Vector3 last;
        float still, age;
        int drops;
        const int MaxDrops = 16;
        const float MaxAge = 30f;

        public static void Attach(NPC n, float rate)
        {
            if (n == null || n.Riding) return;
            var t = n.GetComponent<BloodTrail>();
            if (t == null) t = n.gameObject.AddComponent<BloodTrail>();
            t.npc = n;
            t.rate = Mathf.Max(t.rate, rate);
            t.last = n.transform.position;
            t.age = 0f;
            t.drops = Mathf.Min(t.drops, MaxDrops / 2);
            t.enabled = true;
        }

        void Update()
        {
            if (npc == null || npc.Dead || npc.Escaped || npc.IsHidden || npc.Riding) { enabled = false; return; }
            age += Time.deltaTime;
            if (age > MaxAge || drops >= MaxDrops) { enabled = false; return; }
            var p = npc.transform.position;
            float moved = (p - last).magnitude;
            if (moved > 0.8f / Mathf.Max(0.2f, rate))
            {
                last = p;
                still = 0f;
                drops++;
                FX.BloodDrip((npc.Pelvis != null ? npc.Pelvis.position : p) + Random.insideUnitSphere * 0.2f);
                return;
            }
            still += Time.deltaTime * rate;
            if (still > 3f)
            {
                still = 0f;
                drops++;
                FX.BloodDrip((npc.Pelvis != null ? npc.Pelvis.position : p) + Random.insideUnitSphere * 0.35f);
            }
        }
    }

    /// <summary>Red fuel barrel: shooting it makes an explosion that kills anyone close.</summary>
    public class ExplosiveBarrel : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<ExplosiveBarrel> All = new System.Collections.Generic.List<ExplosiveBarrel>();
        public bool armed;          // only the contract that uses the drum arms it
        public bool exploded;
        public float radius = 7f;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public void Explode()
        {
            if (exploded || !armed) return;
            exploded = true;
            var pos = transform.position + Vector3.up * 0.6f;
            FX.Explosion(pos);
            foreach (var npc in NPC.All.ToArray())
            {
                if (npc == null || npc.Dead || npc.Escaped) continue;
                var d = npc.transform.position - pos;
                if (d.magnitude < radius) npc.Kill(npc.ChestPoint, (d.normalized + Vector3.up * 0.6f).normalized * 4f, null, false);
            }
            var mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", new Color(0.08f, 0.07f, 0.06f));
                r.SetPropertyBlock(mpb);
            }
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            MissionRuntime.I?.OnExplosion(pos, radius);
            Sniper.I?.Shake(0.5f);
        }

        /// <summary>Back to an intact, ordinary (disarmed) drum for the next contract. The component stays: a
        /// deferred Destroy would kill it after the next contract re-armed it in the same frame.</summary>
        public void ResetState()
        {
            exploded = false;
            armed = false;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(null);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = true;
        }
    }

    /// <summary>A prop that does something when shot (car alarm, boat engine, lamp...). Set up by mission scripts.</summary>
    public class Shootable : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<Shootable> All = new System.Collections.Generic.List<Shootable>();
        public string kind;
        public int Hits;
        public bool Disabled;
        public bool OwnSound;       // the prop makes its own noise when hit (car alarm): no extra bullet-impact noise
        public AmmoKind LastAmmo;   // the round of the last hit (an explosive round wrecks it at once)
        public System.Action<Shootable, Vector3> OnShot;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Inert until a contract sets it up again.</summary>
        public void ResetState()
        {
            Disabled = true;
            OnShot = null;
            Hits = 0;
            OwnSound = false;
            LastAmmo = AmmoKind.Standard;
        }
    }

    /// <summary>Car alarm: looping two-tone horn and blinking indicator lights for a few seconds.</summary>
    public class CarAlarmFX : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<CarAlarmFX> All = new System.Collections.Generic.List<CarAlarmFX>();
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        AudioSource src;
        readonly Light[] lights = new Light[2];
        float until, t;

        public void Run(float seconds)
        {
            until = Time.time + seconds;
            if (src == null)
            {
                src = gameObject.AddComponent<AudioSource>();
                src.clip = Sfx.CarAlarm;
                src.loop = true;
                src.spatialBlend = 0.75f;
                src.minDistance = 40f;
                src.maxDistance = 900f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.volume = 0.8f;
            }
            if (!src.isPlaying) src.Play();
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            for (int i = 0; i < 2; i++)
            {
                if (lights[i] == null)
                {
                    var go = new GameObject("indicator");
                    go.transform.SetParent(transform, false);
                    lights[i] = go.AddComponent<Light>();
                    lights[i].type = LightType.Point;
                    lights[i].color = new Color(1f, 0.6f, 0.15f);
                    lights[i].range = 5f;
                    lights[i].shadows = LightShadows.None;
                }
                var f = transform.forward * (b.extents.magnitude * (i == 0 ? 0.62f : -0.62f));
                lights[i].transform.position = b.center + f + Vector3.up * 0.2f;
                lights[i].enabled = true;
            }
            enabled = true;
        }

        public void Stop()
        {
            until = 0f;
            if (src != null) src.Stop();
            foreach (var l in lights) if (l != null) l.enabled = false;
        }

        void Update()
        {
            if (until <= 0f) return;
            if (Time.time > until) { Stop(); return; }
            t += Time.deltaTime;
            bool on = Mathf.Repeat(t, 0.5f) < 0.25f;
            foreach (var l in lights) if (l != null) l.intensity = on ? 3.5f : 0f;
        }
    }
}

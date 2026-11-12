using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// Falling snow around the camera. The flakes live in a box in front of the view and follow the zoom: through the
    /// scope the box moves far out, so the flakes stay small specks instead of blurred discs filling the lens. A
    /// blizzard drives them sideways in streaks.
    /// </summary>
    public class SnowFX : MonoBehaviour
    {
        ParticleSystem ps;
        Camera cam;
        float amount = 0.3f;
        bool zoomed;

        public static SnowFX Build(Camera cam)
        {
            var go = new GameObject("Snow");
            go.layer = Layers.IgnoreRaycast;
            var s = go.AddComponent<SnowFX>();
            s.cam = cam;
            s.ps = go.AddComponent<ParticleSystem>();
            s.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = s.ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
            main.gravityModifier = 0.07f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 4000;
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            var sh = s.ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            var noise = s.ps.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FX.AlphaMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.minParticleSize = 0.0007f;
            s.Place(true);
            s.ps.Play();
            return s;
        }

        /// <summary>0..1: light flurries .. blizzard.</summary>
        public void SetAmount(float a)
        {
            amount = Mathf.Clamp01(a);
            bool night = Env.I != null && Env.I.Current != null && Env.I.Current.night;
            var main = ps.main;
            float lum = night ? 0.42f : 1f;
            main.startColor = new Color(lum, lum, lum * 1.04f, night ? 0.75f : 0.9f);
            main.gravityModifier = Mathf.Lerp(0.06f, 0.12f, amount);
            var em = ps.emission;
            em.rateOverTime = Mathf.Lerp(260f, 2400f, amount) * (Save.Quality == 0 ? 0.55f : 1f);
            // wind: a slow drift, a gale in a blizzard
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.3f, 7f, amount * amount), Mathf.Lerp(0.8f, 11f, amount * amount));
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.4f * amount, 0.8f * amount);
            var r = GetComponent<ParticleSystemRenderer>();
            if (amount > 0.7f)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.035f;
                r.lengthScale = 1.4f;
            }
            else r.renderMode = ParticleSystemRenderMode.Billboard;
            Place(true);
        }

        Vector3 placedFwd;

        void LateUpdate()
        {
            if (cam == null) return;
            bool z = cam.fieldOfView < 22f;
            if (z != zoomed) { Place(true); return; }
            if (zoomed)
            {
                // the scope box stays where it was put; panning the scope far enough re-places it
                if (Vector3.Angle(placedFwd, cam.transform.forward) > 12f) Place(true);
            }
            else transform.position = cam.transform.position + Vector3.up * 3f;
        }

        /// <summary>The flake box: all round the nest with the naked eye (turning never shows an empty sky); far out
        /// along the view through the scope, so the flakes stay small specks.</summary>
        void Place(bool refill)
        {
            if (cam == null || ps == null) return;
            zoomed = cam.fieldOfView < 22f;
            transform.rotation = Quaternion.identity;
            placedFwd = cam.transform.forward;
            float size;
            if (zoomed)
            {
                size = 80f;
                transform.position = cam.transform.position + placedFwd * 75f;
            }
            else
            {
                size = 52f;
                transform.position = cam.transform.position + Vector3.up * 3f;
            }
            var sh = ps.shape;
            sh.scale = new Vector3(size, size * 0.45f, size);
            if (!refill) return;
            ps.Clear(true);
            // fill the box at once so a change of zoom never shows an empty sky
            int n = Mathf.RoundToInt(Mathf.Lerp(260f, 2400f, amount) * 3f * (Save.Quality == 0 ? 0.55f : 1f));
            ps.Emit(Mathf.Min(n, ps.main.maxParticles - 50));
        }
    }

    /// <summary>Kartal Tepe's moving parts, set up when the mountain layout is built: the gondola and the chimney smoke /
    /// hot tub steam at the layout's markers (smoke_*, steam_*).</summary>
    public static class MountainFX
    {
        public static void Setup(Level level)
        {
            CableCarLine.Setup(level);
            // the snowcat's box would wrap its cab: keep only the tracks and the hull, so a man in the cab can be seen
            // and hit through the glass
            var cat = level.Tagged("snowcat");
            var bc = cat != null ? cat.GetComponentInChildren<BoxCollider>(true) : null;
            if (bc != null)
            {
                var c = bc.center;
                var sz = bc.size;
                float h = sz.y * 0.4f;
                bc.center = new Vector3(c.x, c.y - sz.y * 0.5f + h * 0.5f, c.z);
                bc.size = new Vector3(sz.x, h, sz.z);
            }
            if (level.Data == null || level.Data.markers == null) return;
            foreach (var m in level.Data.markers)
            {
                if (m.n.StartsWith("smoke_")) ChimneySmoke.Build(level.StaticRoot, new Vector3(m.x, m.y, m.z), false);
                else if (m.n.StartsWith("steam_")) ChimneySmoke.Build(level.StaticRoot, new Vector3(m.x, m.y, m.z), true);
            }
        }

        /// <summary>Back to normal after a contract: the line running again, cabins swinging no more.</summary>
        public static void Reset()
        {
            if (CableCarLine.I != null) CableCarLine.I.ResetLine();
            ChimneySmoke.SetAll(1f);
        }
    }

    /// <summary>
    /// The gondola: every cabin (layout tags gcabin_k) runs round the loop of the haul rope (paths gondola_up and
    /// gondola_down, with the same sag as the drawn cable) and slows to a crawl through the stations, where people get
    /// on and off. Stop() brakes the whole line in a few seconds: the cabins swing on their hangers like pendulums, and
    /// keep rocking gently in the wind.
    /// </summary>
    public class CableCarLine : MonoBehaviour
    {
        public static CableCarLine I;
        public const float LineSpeed = 5f;
        const float Hang = 3.1f;               // grip to the cabin's centre of mass
        const float StationZone = 27f;          // crawls through the hall, full speed from the portal on

        class Cabin { public Transform t; public GameObject shell; public float s, swing, swingV, roll, rollV; }
        readonly List<Cabin> cabins = new List<Cabin>();
        Vector3[] pts;
        float[] cum;
        float total, upEnd, speed, target, prevSpeed;
        AudioSource hum;

        public bool Stopping => target <= 0f;
        public bool Stopped => target <= 0f && speed <= 0.02f;
        public float Speed => speed;
        public int Count => cabins.Count;
        public float Length => total;
        public float TopS => upEnd;

        public static void Setup(Level level)
        {
            if (I != null) Destroy(I);
            I = null;
            if (!level.HasPath("gondola_up") || !level.HasPath("gondola_down")) return;
            I = level.gameObject.AddComponent<CableCarLine>();
            I.Build(level);
        }

        static List<Vector3> Sag(Vector3[] line)
        {
            var o = new List<Vector3>();
            for (int i = 0; i < line.Length - 1; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                float L = Vector3.Distance(a, b);
                int n = Mathf.Max(2, (int)(L / 6f));
                float sag = L * 0.012f;
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)n;
                    var p = Vector3.Lerp(a, b, t);
                    p.y -= sag * 4f * t * (1f - t);
                    o.Add(p);
                }
            }
            o.Add(line[line.Length - 1]);
            return o;
        }

        void Build(Level level)
        {
            var up = Sag(level.Path("gondola_up"));
            var down = Sag(level.Path("gondola_down"));
            var all = new List<Vector3>(up);
            all.AddRange(down);
            pts = all.ToArray();
            cum = new float[pts.Length + 1];
            for (int i = 1; i < pts.Length; i++) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            cum[pts.Length] = cum[pts.Length - 1] + Vector3.Distance(pts[pts.Length - 1], pts[0]);   // round the valley wheel
            total = cum[pts.Length];
            upEnd = cum[up.Count - 1];
            for (int k = 0; ; k++)
            {
                var go = level.Tagged("gcabin_" + k);
                if (go == null) break;
                cabins.Add(new Cabin { t = go.transform, shell = AddCabinShell(go) });
            }
            for (int k = 0; k < cabins.Count; k++) cabins[k].s = total * (k + 0.5f) / cabins.Count;
            speed = target = prevSpeed = LineSpeed;
            // the bullwheel's hum at the valley station
            if (Sfx.Gondola != null)
            {
                var go = new GameObject("GondolaHum");
                go.transform.SetParent(transform, false);
                go.transform.position = pts[0];
                hum = go.AddComponent<AudioSource>();
                hum.clip = Sfx.Gondola;
                hum.loop = true;
                hum.spatialBlend = 1f;
                hum.minDistance = 25f;
                hum.maxDistance = 400f;
                hum.rolloffMode = AudioRolloffMode.Linear;
                hum.volume = 0.35f;
                hum.Play();
            }
            Place(0f);
        }

        /// <summary>A cabin catches whoever falls inside it (a rider shot on the way up): floor and walls that only bodies
        /// collide with (layer IgnoreRaycast: bullets and sight lines pass through the glass). Off until somebody boards
        /// (SetShell): an empty cabin crawling through the hall must not sweep the bodies lying on the platform.</summary>
        static GameObject AddCabinShell(GameObject cab)
        {
            var shell = new GameObject("shell");
            shell.layer = Layers.IgnoreRaycast;
            shell.transform.SetParent(cab.transform, false);
            void Box(Vector3 c, Vector3 size)
            {
                var b = shell.AddComponent<BoxCollider>();
                b.center = c;
                b.size = size;
            }
            Box(new Vector3(0f, -4.12f, 0f), new Vector3(2.0f, 0.14f, 2.2f));
            Box(new Vector3(-1.02f, -3.2f, 0f), new Vector3(0.06f, 1.9f, 2.2f));
            Box(new Vector3(1.02f, -3.2f, 0f), new Vector3(0.06f, 1.9f, 2.2f));
            Box(new Vector3(0f, -3.2f, -1.12f), new Vector3(2.0f, 1.9f, 0.06f));
            Box(new Vector3(0f, -3.2f, 1.12f), new Vector3(2.0f, 1.9f, 0.06f));
            var rb = cab.GetComponent<Rigidbody>();
            if (rb == null) rb = cab.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            shell.SetActive(false);
            return shell;
        }

        /// <summary>The floor and walls of cabin <paramref name="k"/> (on while it carries people).</summary>
        public void SetShell(int k, bool on)
        {
            if (k >= 0 && k < cabins.Count && cabins[k].shell != null) cabins[k].shell.SetActive(on);
        }

        /// <summary>Loop position nearest to a point (1 m steps).</summary>
        public float NearestS(Vector3 p)
        {
            float best = float.MaxValue, at = 0f;
            for (float d = 0f; d < total; d += 1f)
            {
                float q = (PointAt(d) - p).sqrMagnitude;
                if (q < best) { best = q; at = d; }
            }
            return at;
        }

        Vector3 PointAt(float s)
        {
            s = Mathf.Repeat(s, total);
            int n = pts.Length;
            int lo = 0, hi = n;                    // binary search on cum[0..n]
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (cum[mid] <= s) lo = mid; else hi = mid;
            }
            var a = pts[lo];
            var b = pts[(lo + 1) % n];
            float seg = cum[lo + 1] - cum[lo];
            return seg > 1e-5f ? Vector3.Lerp(a, b, (s - cum[lo]) / seg) : a;
        }

        /// <summary>0 in a station (a crawl), 1 out on the line.</summary>
        float Factor(float s)
        {
            // the bullwheels are at loop positions 0 (valley) and upEnd (top); the halls reach ~28 m either side
            float dValley = Mathf.Min(s, total - s);
            float dTop = Mathf.Abs(s - upEnd);
            float d = Mathf.Min(dValley, dTop);
            return Mathf.Lerp(0.14f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, StationZone, d)));
        }

        public void Stop() => target = 0f;
        public void Restart() => target = LineSpeed;

        public void ResetLine()
        {
            target = speed = prevSpeed = LineSpeed;
            foreach (var c in cabins) { c.swing = c.swingV = c.roll = c.rollV = 0f; if (c.shell != null) c.shell.SetActive(false); }
            if (hum != null) { hum.pitch = 1f; hum.volume = 0.35f; }
        }

        public Transform CabinT(int k) => k >= 0 && k < cabins.Count ? cabins[k].t : null;
        public float CabinS(int k) => k >= 0 && k < cabins.Count ? cabins[k].s : 0f;

        /// <summary>Distance the cabin still has to travel to reach loop position <paramref name="s"/>.</summary>
        public float DistanceTo(int k, float s) => Mathf.Repeat(s - CabinS(k), total);

        /// <summary>The cabin that will be next to pass loop position <paramref name="s"/> (after a margin).</summary>
        public int NextCabinAt(float s, float margin = 2f)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int k = 0; k < cabins.Count; k++)
            {
                float d = DistanceTo(k, s);
                if (d < margin) d += total;
                if (d < bd) { bd = d; best = k; }
            }
            return best;
        }

        /// <summary>Rough seconds until the cabin reaches <paramref name="s"/> at line speed (crawling in stations).</summary>
        public float EtaTo(int k, float s)
        {
            float d = DistanceTo(k, s);
            float t = 0f, x = CabinS(k);
            float step = 4f;
            for (float done = 0f; done < d; done += step) { t += step / (LineSpeed * Factor(Mathf.Repeat(x + done, total))); }
            return t;
        }

        void Update()
        {
            if (pts == null) return;
            Place(Time.deltaTime);
        }

        void Place(float dt)
        {
            prevSpeed = speed;
            if (dt > 0f) speed = Mathf.MoveTowards(speed, target, (target < speed ? 1.7f : 0.45f) * dt);
            float acc = dt > 0f ? (speed - prevSpeed) / dt : 0f;
            float wind = Env.I != null && Env.I.Current != null ? Env.I.Current.wind : 0.3f;
            float tm = Time.time;
            for (int k = 0; k < cabins.Count; k++)
            {
                var c = cabins[k];
                if (c.t == null) continue;
                float f = Factor(c.s);
                c.s = Mathf.Repeat(c.s + speed * f * dt, total);
                var p = PointAt(c.s);
                var q = PointAt(c.s + 1.5f);
                var fwd = q - p;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-6f) fwd = c.t.forward;
                if (dt > 0f)
                {
                    // the hanger is a pendulum: braking swings the cabin forward, then it rocks to rest; wind rolls it
                    float a = acc * f;
                    c.swingV += (-(9.81f / Hang) * Mathf.Sin(c.swing) - (a / Hang) * Mathf.Cos(c.swing) - 0.35f * c.swingV) * dt;
                    c.swing += c.swingV * dt;
                    float gust = (Mathf.PerlinNoise(tm * 0.31f, k * 3.7f) - 0.5f) * 2f * wind * wind * 0.9f;
                    c.rollV += (-(9.81f / Hang) * Mathf.Sin(c.roll) + gust - 0.5f * c.rollV) * dt;
                    c.roll += c.rollV * dt;
                }
                var rot = Quaternion.LookRotation(fwd.normalized, Vector3.up) * Quaternion.Euler(-c.swing * Mathf.Rad2Deg, 0f, c.roll * Mathf.Rad2Deg);
                c.t.SetPositionAndRotation(p, rot);
            }
            if (hum != null)
            {
                float k01 = speed / LineSpeed;
                hum.pitch = Mathf.Lerp(0.55f, 1f, k01);
                hum.volume = 0.35f * k01;
            }
        }
    }

    /// <summary>A plume of smoke from a chimney, or the steam over a hot tub (thick and thin by turns: the mission can
    /// ask how clear it is). Drifts with the wind; darker by night.</summary>
    public class ChimneySmoke : MonoBehaviour
    {
        static readonly List<ChimneySmoke> All = new List<ChimneySmoke>();
        ParticleSystem ps;
        bool steam;
        float scale = 1f, phase;

        /// <summary>How thick the steam is now (0 thin .. 1 thick).</summary>
        public float Thickness { get; private set; }

        public static ChimneySmoke Build(Transform parent, Vector3 pos, bool steam)
        {
            var go = new GameObject(steam ? "Steam" : "ChimneySmoke");
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var c = go.AddComponent<ChimneySmoke>();
            c.steam = steam;
            c.phase = UnityEngine.Random.value * 100f;
            c.ps = go.AddComponent<ParticleSystem>();
            c.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = c.ps.main;
            main.loop = true;
            main.startLifetime = steam ? new ParticleSystem.MinMaxCurve(2.6f, 4.2f) : new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSpeed = steam ? new ParticleSystem.MinMaxCurve(0.25f, 0.6f) : new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSize = steam ? new ParticleSystem.MinMaxCurve(0.5f, 1.1f) : new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = steam ? 90 : 70;
            main.gravityModifier = -0.012f;
            // both shapes emit along their +Z: turned to point straight up (smoke from a narrow cone over the chimney,
            // steam from a flat box over the water)
            var sh = c.ps.shape;
            sh.shapeType = steam ? ParticleSystemShapeType.Box : ParticleSystemShapeType.Cone;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            if (steam) sh.scale = new Vector3(1.8f, 1.8f, 0.1f);
            else { sh.angle = 7f; sh.radius = 0.25f; }
            var sz = c.ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, steam ? 2.6f : 5.5f));
            var noise = c.ps.noise;
            noise.enabled = true;
            noise.strength = steam ? 0.35f : 0.5f;
            noise.frequency = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FX.AlphaMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            c.Apply();
            c.ps.Play();
            All.Add(c);
            return c;
        }

        void OnDestroy() => All.Remove(this);

        /// <summary>Scales every plume (a blizzard tears the smoke away, a calm night lets it climb).</summary>
        public static void SetAll(float k)
        {
            foreach (var c in All) if (c != null) { c.scale = k; c.Apply(); }
        }

        void Apply()
        {
            bool night = Env.I != null && Env.I.Current != null && Env.I.Current.night;
            float wind = Env.I != null && Env.I.Current != null ? Env.I.Current.wind : 0.3f;
            var main = ps.main;
            float lum = night ? (steam ? 0.32f : 0.16f) : (steam ? 0.95f : 0.78f);
            float alpha = (steam ? 0.30f : 0.26f) * scale;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(lum, lum, lum * 1.03f), 0f), new GradientColorKey(new Color(lum, lum, lum * 1.05f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.15f), new GradientAlphaKey(alpha * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var em = ps.emission;
            em.rateOverTime = (steam ? 9f : 2.4f) * Mathf.Max(0.2f, scale);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            float drift = Mathf.Lerp(0.3f, 4.5f, wind * wind);
            vel.x = new ParticleSystem.MinMaxCurve(drift * 0.7f, drift);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.2f * drift, 0.3f * drift);
        }

        void Update()
        {
            if (!steam) return;
            // steam comes and goes: the tub is hidden for a few seconds, then clear again
            float n = Mathf.PerlinNoise(Time.time * 0.11f, phase);
            Thickness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, n));
            var em = ps.emission;
            em.rateOverTime = Mathf.Lerp(3f, 26f, Thickness) * Mathf.Max(0.2f, scale);
        }
    }

    /// <summary>
    /// The New Year's fireworks over the frozen lake: shells climb on a spark trail and burst into coloured spheres
    /// that light the snow, the boom reaching the nest a moment later (sound travels ~340 m/s). While the bangs roll
    /// across the valley a rifle shot is lost in them (<see cref="Masking"/>).
    /// </summary>
    public class Fireworks : MonoBehaviour
    {
        Vector3 launch;
        float until, next, finaleAt;
        bool running;
        readonly List<float> booms = new List<float>();   // when each shell's bang reaches the nest (Time.time)
        int shells;
        static readonly Color[] Palette =
        {
            new Color(1f, 0.32f, 0.22f), new Color(1f, 0.82f, 0.36f), new Color(0.4f, 0.7f, 1f), new Color(0.55f, 1f, 0.5f),
            new Color(0.95f, 0.45f, 1f), new Color(1f, 0.96f, 0.9f), new Color(1f, 0.55f, 0.15f)
        };

        public bool Running => running;
        public int Shells => shells;
        /// <summary>A boom is rolling over the nest right now: a shot fired now is covered.</summary>
        public bool Masking
        {
            get
            {
                float now = Time.time;
                foreach (float b in booms) if (now >= b - 0.05f && now < b + 0.85f) return true;
                return false;
            }
        }
        public event Action<Vector3> Burst;

        public static Fireworks Build(Transform parent, Vector3 launch)
        {
            var go = new GameObject("Fireworks");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<Fireworks>();
            f.launch = launch;
            return f;
        }

        public void Show(float seconds)
        {
            booms.Clear();
            running = true;
            until = Time.time + seconds;
            finaleAt = until - 7f;
            next = Time.time + 0.3f;
        }

        public void StopShow() => running = false;

        void Update()
        {
            if (!running) return;
            if (Time.time > until) { running = false; return; }
            if (Time.time < next) return;
            bool finale = Time.time > finaleAt;
            int n = finale ? UnityEngine.Random.Range(2, 4) : 1;
            for (int i = 0; i < n; i++) StartCoroutine(Shell(finale));
            next = Time.time + (finale ? UnityEngine.Random.Range(0.22f, 0.45f) : UnityEngine.Random.Range(0.55f, 1.5f));
        }

        System.Collections.IEnumerator Shell(bool finale)
        {
            shells++;
            var from = launch + new Vector3(UnityEngine.Random.Range(-6f, 6f), 0f, UnityEngine.Random.Range(-6f, 6f));
            var to = launch + new Vector3(UnityEngine.Random.Range(-45f, 45f), UnityEngine.Random.Range(70f, finale ? 140f : 120f), UnityEngine.Random.Range(-30f, 30f));
            var col = Palette[UnityEngine.Random.Range(0, Palette.Length)];
            // the climb: a spark trail
            var trail = Sparks(from, new Color(1f, 0.8f, 0.5f), 0, 0.16f, 0.9f, 0.5f, true);
            var em = trail.emission;
            em.rateOverTime = 70f;
            float t = 0f, rise = UnityEngine.Random.Range(1.6f, 2.3f);
            Sfx.PlayAt(Sfx.Whoosh, from, 0.18f);
            while (t < rise)
            {
                t += Time.deltaTime;
                float k = t / rise;
                trail.transform.position = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            em.rateOverTime = 0f;
            Destroy(trail.gameObject, 2f);
            // the burst: a sphere of sparks, a flash on the snow
            var p = to;
            int count = finale ? 170 : 130;
            var b = Sparks(p, col, count, UnityEngine.Random.Range(0.55f, 0.85f), UnityEngine.Random.Range(1.5f, 2.4f), UnityEngine.Random.Range(17f, 27f), false);
            Destroy(b.gameObject, 3.5f);
            if (UnityEngine.Random.value < 0.45f)
            {
                var b2 = Sparks(p, Palette[UnityEngine.Random.Range(0, Palette.Length)], count / 2, 0.4f, 1.1f, 9f, false);
                Destroy(b2.gameObject, 2.5f);
            }
            StartCoroutine(Flash(p, col));
            Burst?.Invoke(p);
            // the boom arrives at the nest later; every shell's bang is booked now (several can be on their way at once)
            float dist = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, p) : 250f;
            // the clip whistles for 0.9 s before its bang: start it so that the bang lands when the sound arrives
            const float BangAt = 0.9f;
            float wait = dist / 340f - BangAt;
            float boomAt = Time.time + Mathf.Max(0f, wait) + BangAt;
            booms.RemoveAll(b => b < Time.time - 2f);
            booms.Add(boomAt);
            if (wait > 0f) yield return new WaitForSeconds(wait);
            Sfx.PlayAt(Sfx.Firework, p, finale ? 1f : 0.85f);
            yield return new WaitForSeconds(BangAt);
            yield return new WaitForSeconds(0.25f);
            if (UnityEngine.Random.value < 0.6f) Sfx.PlayAt(Sfx.FireworkCrackle, p, 0.6f);
        }

        System.Collections.IEnumerator Flash(Vector3 p, Color c)
        {
            var go = new GameObject("flash");
            go.transform.SetParent(transform, false);
            go.transform.position = p;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = 190f;
            l.intensity = 3.2f;
            l.shadows = LightShadows.None;
            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                l.intensity = 3.2f * Mathf.Exp(-t * 4f);
                yield return null;
            }
            Destroy(go);
        }

        ParticleSystem Sparks(Vector3 pos, Color col, int count, float size, float life, float speed, bool trail)
        {
            var go = new GameObject(trail ? "rocket" : "burst");
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = trail;
            main.duration = trail ? 5f : 0.1f;
            main.startLifetime = trail ? new ParticleSystem.MinMaxCurve(0.4f, 0.8f) : new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = trail ? new ParticleSystem.MinMaxCurve(0.2f, 1.2f) : new ParticleSystem.MinMaxCurve(speed * 0.85f, speed);
            main.startSize = trail ? new ParticleSystem.MinMaxCurve(0.25f, 0.45f) : new ParticleSystem.MinMaxCurve(size * 0.7f, size);
            main.startColor = col;
            main.gravityModifier = trail ? 0.05f : 0.32f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(60, count + 20);
            var em = ps.emission;
            em.rateOverTime = 0f;
            if (!trail) em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = trail ? 0.05f : 0.4f;
            var dr = ps.limitVelocityOverLifetime;
            dr.enabled = !trail;
            dr.drag = 1.6f;
            dr.dampen = 0.06f;
            var c = ps.colorOverLifetime;
            c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(col, 0.12f), new GradientColorKey(col * 0.8f, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.95f, 0.55f), new GradientAlphaKey(0f, 1f) });
            c.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FX.AddMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.minParticleSize = 0.0015f;
            ps.Play();
            return ps;
        }
    }

    /// <summary>
    /// An avalanche: the slab under the cornice breaks away and pours down the slope along a path, a churning cloud of
    /// powder ahead of the flow; where it runs out, a field of heavy debris covers the ground (the road). The mission
    /// asks whether a point has been buried.
    /// </summary>
    public class Avalanche : MonoBehaviour
    {
        Vector3[] path;
        float[] cum;
        float total, width, s, speed;
        bool running;
        ParticleSystem cloud, dust;
        Mesh debris;
        Vector3[] baseVerts;
        float[] vertS;
        float[] vertH;

        public bool Triggered { get; private set; }
        public bool Done { get; private set; }
        public float FrontS => s;
        public Vector3 Front => PointAt(s);
        public event Action Finished;

        public static Avalanche Build(Transform parent, Vector3[] path, float width)
        {
            var go = new GameObject("Avalanche");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<Avalanche>();
            a.path = path;
            a.width = width;
            a.cum = new float[path.Length];
            for (int i = 1; i < path.Length; i++) a.cum[i] = a.cum[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            a.total = a.cum[path.Length - 1];
            a.cloud = a.Cloud("cloud", 4.5f, 9f, 3.5f, 6f, 0.85f);
            a.dust = a.Cloud("dust", 1.2f, 2.6f, 1.6f, 3f, 0.7f);
            a.BuildDebris();
            return a;
        }

        Vector3 PointAt(float d)
        {
            d = Mathf.Clamp(d, 0f, total);
            int i = 1;
            while (i < path.Length - 1 && cum[i] < d) i++;
            float seg = cum[i] - cum[i - 1];
            return seg > 1e-5f ? Vector3.Lerp(path[i - 1], path[i], (d - cum[i - 1]) / seg) : path[i];
        }

        Vector3 DirAt(float d)
        {
            var a = PointAt(d - 2f);
            var b = PointAt(d + 2f);
            var v = b - a;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        ParticleSystem Cloud(string name, float size0, float size1, float life0, float life1, float alpha)
        {
            var go = new GameObject(name);
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size0, size1);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 900;
            main.gravityModifier = -0.015f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(width, 1.5f, 4f);
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.6f, 1, 2.4f));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 1.2f;
            noise.frequency = 0.25f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var c = ps.colorOverLifetime;
            c.enabled = true;
            bool night = Env.I != null && Env.I.Current != null && Env.I.Current.night;
            float lum = night ? 0.22f : 0.93f;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(lum, lum, lum * 1.03f), 0f), new GradientColorKey(new Color(lum * 0.92f, lum * 0.93f, lum * 0.98f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.12f), new GradientAlphaKey(alpha * 0.55f, 0.6f), new GradientAlphaKey(0f, 1f) });
            c.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FX.AlphaMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.Distance;
            ps.Play();
            return ps;
        }

        /// <summary>The run-out: a lumpy blanket of heavy snow over the last part of the path, grown as the flow arrives.</summary>
        void BuildDebris()
        {
            var go = new GameObject("debris");
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.9f, 0.92f, 0.95f);
            mat.SetFloat("_Glossiness", 0.22f);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            float s0 = total * 0.45f;
            int rows = 26, cols = 13;
            var verts = new List<Vector3>();
            var ss = new List<float>();
            var hs = new List<float>();
            var tris = new List<int>();
            var rnd = new System.Random(25);
            for (int i = 0; i < rows; i++)
            {
                float d = Mathf.Lerp(s0, total, i / (float)(rows - 1));
                var c = PointAt(d);
                var dir = DirAt(d);
                var side = Vector3.Cross(Vector3.up, dir).normalized;
                float w = width * Mathf.Lerp(0.8f, 1.25f, i / (float)(rows - 1));
                for (int j = 0; j < cols; j++)
                {
                    float u = j / (float)(cols - 1) - 0.5f;
                    var p = c + side * u * w;
                    p.y = Level.SurfaceY(p, p.y);
                    float edge = 1f - Mathf.Pow(Mathf.Abs(u) * 2f, 2.5f);
                    float tail = Mathf.Sin(Mathf.Clamp01(i / (float)(rows - 1)) * Mathf.PI * 0.95f + 0.1f);
                    float h = (1.4f + (float)rnd.NextDouble() * 1.6f) * Mathf.Max(0f, edge) * tail;
                    verts.Add(p + Vector3.up * -0.3f);
                    ss.Add(d);
                    hs.Add(h);
                }
            }
            for (int i = 0; i < rows - 1; i++)
                for (int j = 0; j < cols - 1; j++)
                {
                    int a = i * cols + j;
                    tris.AddRange(new[] { a, a + cols, a + 1, a + 1, a + cols, a + cols + 1 });
                }
            debris = new Mesh { name = "avalanche_debris" };
            baseVerts = verts.ToArray();
            vertS = ss.ToArray();
            vertH = hs.ToArray();
            debris.SetVertices(verts);
            debris.SetTriangles(tris, 0);
            debris.RecalculateNormals();
            debris.RecalculateBounds();
            mf.sharedMesh = debris;
            go.SetActive(false);
        }

        public void Trigger()
        {
            if (Triggered) return;
            Triggered = true;
            running = true;
            s = 0f;
            speed = 3f;
            debris.name = "avalanche_debris";
            transform.Find("debris").gameObject.SetActive(true);
            Sfx.Play(Sfx.Avalanche, 0.9f);
            Sfx.PlayAt(Sfx.Avalanche, path[0], 1f);
        }

        public float Total => total;

        /// <summary>Horizontal distance from p to the path, and where along the path that is.</summary>
        float Nearest(Vector3 p, out float at)
        {
            float best = float.MaxValue;
            at = 0f;
            for (float d = 0f; d <= total; d += 2f)
            {
                var q = PointAt(d);
                float h = new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude;
                if (h < best) { best = h; at = d; }
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>Is this point in the run-out, where the flow will bury whatever is there when it comes down?</summary>
        public bool InRunout(Vector3 p) => Nearest(p, out float at) < width * 0.62f && at > total * 0.35f;

        /// <summary>Has the flow reached and buried this point (within the path's width, on the part already passed)?</summary>
        public bool Buries(Vector3 p)
        {
            if (!Triggered) return false;
            float d = Nearest(p, out float at);
            return at <= s && d < width * 0.62f && at > total * 0.35f;
        }

        void Update()
        {
            if (!running) return;
            float dt = Time.deltaTime;
            float slope = Mathf.Clamp01(-DirAt(s).y * 2.2f);
            speed = Mathf.Clamp(speed + (slope * 9f - 1.6f) * dt, 2f, 26f);
            s += speed * dt;
            var front = PointAt(s);
            var dir = DirAt(s);
            float k = Mathf.Clamp01(speed / 20f);
            foreach (var ps in new[] { cloud, dust })
            {
                ps.transform.SetPositionAndRotation(front + Vector3.up * (ps == cloud ? 2.5f : 0.8f), Quaternion.LookRotation(dir));
                var em = ps.emission;
                em.rateOverTime = ps == cloud ? Mathf.Lerp(40f, 180f, k) : Mathf.Lerp(30f, 120f, k);
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(dir.x * speed * 0.35f - 1f, dir.x * speed * 0.55f + 1f);
                vel.y = new ParticleSystem.MinMaxCurve(1.5f, ps == cloud ? 5.5f : 2.5f);
                vel.z = new ParticleSystem.MinMaxCurve(dir.z * speed * 0.35f - 1f, dir.z * speed * 0.55f + 1f);
            }
            // the debris rises where the flow has passed
            var v = new Vector3[baseVerts.Length];
            for (int i = 0; i < v.Length; i++) v[i] = baseVerts[i] + Vector3.up * vertH[i] * Mathf.Clamp01((s - vertS[i]) / 7f);
            debris.SetVertices(v);
            debris.RecalculateNormals();
            debris.RecalculateBounds();
            if (s >= total)
            {
                running = false;
                Done = true;
                foreach (var ps in new[] { cloud, dust })
                {
                    var em = ps.emission;
                    em.rateOverTime = 0f;
                }
                Finished?.Invoke();
            }
        }
    }
}

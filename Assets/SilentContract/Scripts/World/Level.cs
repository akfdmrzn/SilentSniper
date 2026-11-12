using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    // ------------------------------------------------------------------ layout data (exported by tools/level/make_layout.py)
    [Serializable] public class LayoutPlayer { public float x, y, z, yaw, pitch, yawMin = -80, yawMax = 80, pitchMin = -40, pitchMax = 12; }
    /// <summary>V2: another place to shoot from (a window, a crane cab, a slope). <see cref="missions"/>: the contracts it
    /// sees well. kind: roof | window | crane | slope | container.</summary>
    [Serializable] public class LayoutNest : LayoutPlayer { public string id, name, desc, kind; public string[] missions; public float dist; }
    [Serializable] public class LayoutObj { public string p; public float x, y, z, r, s = 1f; public int dyn; public string tag; public int hide; public float sx, sy, sz; public int indoor; }
    [Serializable] public class LayoutMarker { public string n; public float x, y, z, r; }
    [Serializable] public class LayoutPath { public string n; public float[] pts; public int loop; }
    [Serializable] public class LayoutBox { public float x, y, z, sx, sy, sz, r; public int water; }
    [Serializable] public class LayoutLight { public float x, y, z, cr = 1, cg = 0.85f, cb = 0.6f, range = 18, intensity = 1.6f; public int night = 1; }
    [Serializable]
    public class LayoutFile
    {
        public int version;
        public string name;
        public float waterY = -2.5f;
        public float quayZ = 262f;
        public int sea = 1;             // 0: no sea (mountain)
        public int snow;                // 1: snow lies on everything (SC/SnowStandard materials)
        public float envY;              // height of the environment capture (0 = default 22 m)
        public string groundMaps;       // ground map texture (Resources/SC/Textures) + rect json (Resources/SC/Level): B = top height
        public LayoutPlayer player;
        public string playerName;       // the main nest's name in the nest picker
        public LayoutNest[] nests;
        public LayoutObj[] objects;
        public LayoutMarker[] markers;
        public LayoutPath[] paths;
        public LayoutBox[] colliders;
        public LayoutLight[] lights;
    }

    /// <summary>
    /// Builds the harbour from the layout exported by the Blender level tool: static props (batched),
    /// dynamic props (vehicles, boats), collision volumes, lights, markers and NPC paths.
    /// </summary>
    public class Level : MonoBehaviour
    {
        public static Level I;
        public static float WaterY = -2.5f;

        public LayoutFile Data { get; private set; }
        public Transform StaticRoot { get; private set; }
        public Transform DynamicRoot { get; private set; }
        public Transform ColliderRoot { get; private set; }

        readonly Dictionary<string, LayoutMarker> markers = new Dictionary<string, LayoutMarker>();
        readonly Dictionary<string, Vector3[]> paths = new Dictionary<string, Vector3[]>();
        readonly Dictionary<string, List<GameObject>> tagged = new Dictionary<string, List<GameObject>>();
        readonly List<Light> nightLights = new List<Light>();
        readonly List<Renderer> nightEmissive = new List<Renderer>();
        // night-only emission: one runtime copy per emissive material (original colour kept), switched by SetNight
        readonly Dictionary<Material, Color> emissiveCopies = new Dictionary<Material, Color>();
        // objects that only some contracts show (rally stage, ...): hidden after every contract
        readonly List<GameObject> hiddenByDefault = new List<GameObject>();
        // indoor furniture: never gets the snow layer
        readonly HashSet<GameObject> indoor = new HashSet<GameObject>();
        public string LayoutName { get; private set; }

        public Vector3 PlayerPosition => Data?.player != null ? new Vector3(Data.player.x, Data.player.y, Data.player.z) : new Vector3(0, 30, -60);

        static readonly string[] NoCastIds = { "sc_level_ground", "sc_far_shore", "sc_breakwater", "sc_city_ground" };

        public IEnumerator Build(string layoutName, Action<float, string> progress, string label = "Liman kuruluyor")
        {
            I = this;
            LayoutName = layoutName;
            var ta = Res.Text("Level/" + layoutName);
            if (ta == null)
            {
                Debug.LogError("[SC] layout missing: " + layoutName);
                Data = new LayoutFile { objects = new LayoutObj[0], markers = new LayoutMarker[0], paths = new LayoutPath[0], colliders = new LayoutBox[0], lights = new LayoutLight[0], player = new LayoutPlayer() };
            }
            else Data = JsonUtility.FromJson<LayoutFile>(ta.text);
            WaterY = Data.waterY;

            StaticRoot = new GameObject("Static").transform;
            StaticRoot.SetParent(transform, false);
            DynamicRoot = new GameObject("Dynamic").transform;
            DynamicRoot.SetParent(transform, false);
            ColliderRoot = new GameObject("Colliders").transform;
            ColliderRoot.SetParent(transform, false);

            foreach (var m in Data.markers ?? new LayoutMarker[0]) markers[m.n] = m;
            foreach (var p in Data.paths ?? new LayoutPath[0])
            {
                var pts = new Vector3[p.pts.Length / 3];
                for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3(p.pts[i * 3], p.pts[i * 3 + 1], p.pts[i * 3 + 2]);
                paths[p.n] = pts;
            }

            // ---- objects
            var objs = Data.objects ?? new LayoutObj[0];
            float tLast = Time.realtimeSinceStartup;
            int missing = 0;
            for (int i = 0; i < objs.Length; i++)
            {
                var o = objs[i];
                var prefab = Res.Prop(o.p);
                if (prefab == null) { missing++; continue; }
                var go = Instantiate(prefab, new Vector3(o.x, o.y, o.z), Quaternion.Euler(0, o.r, 0), o.dyn != 0 ? DynamicRoot : StaticRoot);
                go.name = string.IsNullOrEmpty(o.tag) ? o.p : o.tag;
                if (o.sx > 0f || o.sy > 0f || o.sz > 0f)
                    go.transform.localScale = new Vector3(o.sx > 0f ? o.sx : 1f, o.sy > 0f ? o.sy : 1f, o.sz > 0f ? o.sz : 1f);
                else if (Mathf.Abs(o.s - 1f) > 0.001f && o.s > 0) go.transform.localScale = Vector3.one * o.s;
                if (o.p == "sc_c_pane") go.AddComponent<GlassPane>();
                // V3: the harbour's ground, far shore and breakwaters are a few huge triangles; with a low sun the shadow
                // pancaking of such triangles shades the whole yard (no shadow of anything shows). They receive shadows
                // but cast none (they are flat, or beyond the shadow distance).
                if (System.Array.IndexOf(NoCastIds, o.p) >= 0)
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (o.hide != 0) hiddenByDefault.Add(go);
                if (o.indoor != 0) indoor.Add(go);
                if (!string.IsNullOrEmpty(o.tag))
                {
                    if (!tagged.TryGetValue(o.tag, out var list)) tagged[o.tag] = list = new List<GameObject>();
                    list.Add(go);
                }
                if (Time.realtimeSinceStartup - tLast > 0.05f)
                {
                    tLast = Time.realtimeSinceStartup;
                    progress?.Invoke(0.15f + 0.6f * i / Mathf.Max(1, objs.Length), label);
                    yield return null;
                }
            }
            if (missing > 0) Debug.LogWarning("[SC] " + missing + " layout objects reference missing props");

            // ---- collision volumes (ground, quay, decks, roofs)
            foreach (var b in Data.colliders ?? new LayoutBox[0])
            {
                var go = new GameObject("col");
                go.transform.SetParent(ColliderRoot, false);
                go.transform.position = new Vector3(b.x, b.y, b.z);
                go.transform.rotation = Quaternion.Euler(0, b.r, 0);
                go.layer = Layers.Default;
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(b.sx, b.sy, b.sz);
            }

            // ---- lights
            foreach (var l in Data.lights ?? new LayoutLight[0])
            {
                var go = new GameObject("lamp");
                go.transform.SetParent(StaticRoot, false);
                go.transform.position = new Vector3(l.x, l.y, l.z);
                var li = go.AddComponent<Light>();
                li.type = LightType.Point;
                li.color = new Color(l.cr, l.cg, l.cb);
                li.range = l.range;
                li.intensity = l.intensity;
                li.shadows = LightShadows.None;
                li.renderMode = LightRenderMode.Auto;
                if (l.night != 0) nightLights.Add(li);
            }
            if (Data.snow != 0) SnowMaterials();
            foreach (var root in new[] { StaticRoot, DynamicRoot })
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                        if (m != null && m.IsKeywordEnabled("_EMISSION")) { nightEmissive.Add(r); break; }
                }
            PrepareNightEmission();
            foreach (var h in hiddenByDefault) if (h != null) h.SetActive(false);
            // the city's buildings share their materials: combine them into a few large batches (draw calls)
            CombineBuildings();

            // remember where moving props start, give boats a gentle swell
            foreach (Transform t in DynamicRoot)
            {
                dynStart.Add(new KeyValuePair<Transform, Pose>(t, new Pose(t.position, t.rotation)));
                if (t.name == "speedboat" || t.name == "tug" || t.name.StartsWith("ph_ocean_buoy"))
                {
                    var model = t.childCount > 0 ? t.GetChild(0) : null;
                    if (model != null) model.gameObject.AddComponent<Bobber>().amplitude = t.name == "tug" ? 0.12f : 0.07f;
                }
            }
            if (Data.sea != 0) MarkReflective();
            GroundMask();
            MountainFX.Setup(this);
            progress?.Invoke(0.85f, "Su ve gökyüzü");
            yield return null;
        }

        /// <summary>
        /// Props at the waterfront, on the water or far away are moved to the Reflect layer: the planar water
        /// reflection only renders that layer (the yard behind the quay can never be seen in the water from the roof).
        /// </summary>
        void MarkReflective()
        {
            float quay = Data.quayZ;
            int n = 0;
            var items = new List<Transform>();
            foreach (var root in new[] { StaticRoot, DynamicRoot })
                foreach (Transform t in root)
                {
                    if (t.name == "Buildings") { foreach (Transform b in t) items.Add(b); }   // combined city buildings: one by one
                    else items.Add(t);
                }
            {
                foreach (Transform t in items)
                {
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    if (rs.Length == 0) continue;
                    var b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    float horiz = new Vector2(b.center.x, b.center.z).magnitude;
                    if (b.max.z > quay - 10f || b.min.y < WaterY + 0.3f || horiz > 650f)
                    {
                        Util.SetLayer(t.gameObject, Layers.Reflect);
                        n++;
                    }
                }
            }
            ReflectiveCount = n;
        }

        public int ReflectiveCount { get; private set; }

        static readonly int idMask = Shader.PropertyToID("_SC_GroundMask"), idMaskRect = Shader.PropertyToID("_SC_GroundMaskRect"),
            idMaskK = Shader.PropertyToID("_SC_GroundMaskK");

        /// <summary>V3: the map's ground detail mask for SC/Ground (asphalt repairs, stains, tyre tracks, panel joints) and a
        /// second scale of the ground textures against visible tiling. Only maps that have one (the harbour).</summary>
        void GroundMask()
        {
            Shader.SetGlobalVector(idMaskRect, Vector4.zero);
            Shader.SetGlobalVector(idMaskK, Vector4.zero);
            string name = LayoutName + "_ground_mask";
            var tex = Resources.Load<Texture2D>("SC/Textures/" + name);
            if (tex == null) return;                       // maps without a mask (no missing-resource warning)
            var js = Res.Text("Level/" + name);
            if (js == null) return;
            var r = JsonUtility.FromJson<TopMapMeta>(js.text);
            Shader.SetGlobalTexture(idMask, tex);
            Shader.SetGlobalVector(idMaskRect, new Vector4(r.xMin, r.zMin, 1f / Mathf.Max(1f, r.xMax - r.xMin), 1f / Mathf.Max(1f, r.zMax - r.zMin)));
            Shader.SetGlobalVector(idMaskK, new Vector4(1f, 0.85f, 0.23f, 0.6f));
        }

        readonly List<KeyValuePair<Transform, Pose>> dynStart = new List<KeyValuePair<Transform, Pose>>();

        /// <summary>Puts moving props (forklift, boats) back where the layout placed them.</summary>
        public void ResetDynamic()
        {
            MountainFX.Reset();
            foreach (var h in hiddenByDefault) if (h != null && h.activeSelf) h.SetActive(false);
            GlassPane.ResetAll();
            foreach (var kv in dynStart)
            {
                if (kv.Key == null) continue;
                var mover = kv.Key.GetComponent<PathMover>();
                if (mover != null) mover.Stop();
                kv.Key.SetPositionAndRotation(kv.Value.position, kv.Value.rotation);
                // characters attached to a boat during the last mission
                foreach (var n in kv.Key.GetComponentsInChildren<NPC>(true)) Destroy(n.gameObject);
            }
        }

        public void SetNight(bool night)
        {
            foreach (var l in nightLights) if (l != null) l.enabled = night;
            foreach (var kv in emissiveCopies)
                if (kv.Key != null) kv.Key.SetColor(EmissionId, night ? kv.Value : Color.black);
        }

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>Emissive materials (signs, lamps, lit rooms, clock faces) glow only at night: every renderer gets a
        /// runtime copy of them so switching never touches the material assets.</summary>
        void PrepareNightEmission()
        {
            var map = new Dictionary<Material, Material>();
            foreach (var r in nightEmissive)
            {
                if (r == null) continue;
                var sm = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < sm.Length; i++)
                {
                    var m = sm[i];
                    if (m == null || !m.IsKeywordEnabled("_EMISSION")) continue;
                    if (!map.TryGetValue(m, out var copy))
                    {
                        copy = new Material(m) { name = m.name + " (night)" };
                        map[m] = copy;
                        emissiveCopies[copy] = m.GetColor(EmissionId);
                    }
                    sm[i] = copy;
                    changed = true;
                }
                if (changed) r.sharedMaterials = sm;
            }
        }

        static Shader snowShader;

        // materials that are snow already (or the snowy ground itself): no second layer
        static readonly string[] SnowyMaterials = { "ai_fir_", "ai_snowman", "ai_ice_hut", "ai_firewood", "shared__mtn_snow", "shared__mtn_rock",
                                                    "shared__mtn_road", "shared__mtn_bank", "shared__mtn_ice", "shared__mtn_fircard", "shared__mtn_roof_snow" };

        [Serializable] class TopMapMeta { public float xMin, zMin, xMax, zMax, yMin, yMax; }

        /// <summary>The village's top-down height map for SC/SnowStandard (no snow under a roof).</summary>
        void SnowCoverMap()
        {
            Shader.SetGlobalVector("_SC_TopRect", Vector4.zero);
            if (string.IsNullOrEmpty(Data.groundMaps)) return;
            var tex = Resources.Load<Texture2D>("SC/Textures/" + Data.groundMaps);
            var js = Res.Text("Level/" + Data.groundMaps);
            if (tex == null || js == null) return;
            var r = JsonUtility.FromJson<TopMapMeta>(js.text);
            Shader.SetGlobalTexture("_SC_TopTex", tex);
            Shader.SetGlobalVector("_SC_TopRect", new Vector4(r.xMin, r.zMin, 1f / Mathf.Max(1f, r.xMax - r.xMin), 1f / Mathf.Max(1f, r.zMax - r.zMin)));
            Shader.SetGlobalVector("_SC_TopRange", new Vector4(r.yMin, r.yMax - r.yMin, 0f, 0f));
        }

        /// <summary>
        /// Snow on the mountain map: every opaque Standard material of the level gets a copy with SC/SnowStandard (same
        /// textures and keywords) that lays snow on up-facing surfaces (global _SC_Snow) out in the open. Glass, cut-outs,
        /// the ground, things that are snowy already and the furniture indoors keep their own materials.
        /// </summary>
        void SnowMaterials()
        {
            SnowCoverMap();
            if (snowShader == null) snowShader = Shader.Find("SC/SnowStandard");
            if (snowShader == null || !snowShader.isSupported) return;
            var map = new Dictionary<Material, Material>();
            foreach (var root in new[] { StaticRoot, DynamicRoot })
                foreach (Transform item in root)
                foreach (var r in item.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (indoor.Contains(item.gameObject)) continue;
                    var sm = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < sm.Length; i++)
                    {
                        var m = sm[i];
                        if (m == null || m.shader == null || m.shader.name != "Standard") continue;
                        if (m.renderQueue >= 2450 || m.GetTag("RenderType", false) == "Transparent") continue;   // glass
                        bool snowy = false;
                        foreach (var pre in SnowyMaterials) if (m.name.StartsWith(pre)) { snowy = true; break; }
                        if (snowy) continue;
                        if (!map.TryGetValue(m, out var copy))
                        {
                            copy = new Material(m) { name = m.name + " (snow)" };
                            copy.shader = snowShader;
                            map[m] = copy;
                        }
                        sm[i] = copy;
                        changed = true;
                    }
                    if (changed) r.sharedMaterials = sm;
                }
        }

        /// <summary>Static batching for the procedural city buildings (their meshes are readable): one batch per shared
        /// material instead of one draw call per building and material.</summary>
        void CombineBuildings()
        {
            var group = new GameObject("Buildings");
            group.transform.SetParent(StaticRoot, false);
            var list = new List<Transform>();
            foreach (Transform t in StaticRoot)
            {
                var pi = t.GetComponent<PropInfo>();
                if (pi != null && pi.id != null && (pi.id.StartsWith("sc_c_") || pi.id.StartsWith("sc_m_")) && pi.id != "sc_c_pane" && !hiddenByDefault.Contains(t.gameObject))
                    list.Add(t);
            }
            if (list.Count < 2) { Destroy(group); return; }
            foreach (var t in list) t.SetParent(group.transform, true);
            try { StaticBatchingUtility.Combine(group); }
            catch (Exception e) { Debug.LogWarning("[SC] static batching skipped: " + e.Message); }
        }

        /// <summary>Shows or hides the layout objects with this tag (contract props such as the rally stage).</summary>
        public void ShowTagged(string tag, bool on)
        {
            foreach (var go in AllTagged(tag)) if (go != null) go.SetActive(on);
        }

        /// <summary>Where this contract can be shot from: the main nest first, then the other places that see it.</summary>
        public List<LayoutNest> NestsFor(MissionDef def)
        {
            Vector3? focus = def != null && !string.IsNullOrEmpty(def.focus) && HasMarker(def.focus) ? Marker(def.focus) : (Vector3?)null;
            var list = BuildNests(Data.player, Data.playerName, Data.nests, def, focus);
            // V3: most contracts start closer in — on the ground, a low roof, halfway up (NestScout); the roof stays a choice
            if (focus.HasValue && def != null)
            {
                LayoutNest scout = null;
                try { Physics.SyncTransforms(); scout = NestScout.For(this, def, focus.Value, Data.player); }
                catch (Exception e) { Debug.LogException(e); }
                if (scout != null) list.Insert(0, scout);
            }
            return list;
        }

        /// <summary>Within <paramref name="r"/> metres (flat) of any authored marker or route point: where people of the
        /// contracts stand and walk.</summary>
        public bool NearAuthored(Vector3 p, float r)
        {
            float r2 = r * r;
            foreach (var m in markers.Values) { float dx = m.x - p.x, dz = m.z - p.z; if (dx * dx + dz * dz < r2 && Mathf.Abs(m.y - p.y) < 4f) return true; }
            foreach (var path in paths.Values)
                foreach (var q in path) { float dx = q.x - p.x, dz = q.z - p.z; if (dx * dx + dz * dz < r2 && Mathf.Abs(q.y - p.y) < 4f) return true; }
            return false;
        }

        public static List<LayoutNest> BuildNests(LayoutPlayer p, string playerName, LayoutNest[] nests, MissionDef def, Vector3? focus)
        {
            var list = new List<LayoutNest>();
            if (p == null) return list;
            list.Add(new LayoutNest
            {
                id = "main", name = string.IsNullOrEmpty(playerName) ? "Ana yuva" : playerName, kind = "roof",
                desc = "Planlanan yer: yüksek, geniş açı.", x = p.x, y = p.y, z = p.z, yaw = p.yaw, pitch = p.pitch,
                yawMin = p.yawMin, yawMax = p.yawMax, pitchMin = p.pitchMin, pitchMax = p.pitchMax
            });
            if (nests != null && def != null)
                foreach (var n in nests)
                    if (n != null && n.missions != null && Array.IndexOf(n.missions, def.id) >= 0) list.Add(n);
            if (focus.HasValue)
                foreach (var n in list) n.dist = Vector3.Distance(new Vector3(n.x, n.y, n.z), focus.Value);
            return list;
        }

        public bool HasMarker(string n) => markers.ContainsKey(n);

        public Vector3 Marker(string n)
        {
            if (markers.TryGetValue(n, out var m)) return new Vector3(m.x, m.y, m.z);
            Debug.LogWarning("[SC] missing marker " + n);
            return Vector3.zero;
        }

        public float MarkerYaw(string n) => markers.TryGetValue(n, out var m) ? m.r : 0f;

        public Vector3[] Path(string n)
        {
            if (paths.TryGetValue(n, out var p)) return p;
            Debug.LogWarning("[SC] missing path " + n);
            return new[] { Vector3.zero };
        }

        public bool HasPath(string n) => paths.ContainsKey(n);

        public GameObject Tagged(string tag, int index = 0)
        {
            if (tagged.TryGetValue(tag, out var l) && index < l.Count) return l[index];
            return null;
        }

        public List<GameObject> AllTagged(string tag) => tagged.TryGetValue(tag, out var l) ? l : new List<GameObject>();

        /// <summary>Ground height under a point (raycast against world colliders).</summary>
        public static float GroundY(Vector3 p, float fallback)
        {
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var h, 30f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return h.point.y;
            return fallback;
        }

        /// <summary>The highest surface at p's x/z, wherever it is (a long cast from far above: a hillside well above or
        /// below the guess is found too). For routes worked out across open, sloping land.</summary>
        public static float SurfaceY(Vector3 p, float fallback, float above = 80f, float depth = 200f)
        {
            if (Physics.Raycast(p + Vector3.up * above, Vector3.down, out var h, depth, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return h.point.y;
            return fallback;
        }

        /// <summary>True when a person can walk straight from a to b: nothing in the way at knee and chest height and the
        /// floor continues without a step or drop (nobody walks through walls, into the water or off a deck / tower).
        /// endY = floor height at b.</summary>
        public static bool WalkableLine(Vector3 a, Vector3 b, out float endY, float maxStep = 0.45f)
        {
            endY = a.y;
            var flat = new Vector3(b.x - a.x, 0, b.z - a.z);
            float len = flat.magnitude;
            if (len < 0.05f) return true;
            // step along the floor (2 m at a time): the body line from each step to the next must be free (this follows a
            // sloping snowfield as well as a flat quay) and the floor must not jump by more than a step
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
            float y = a.y;
            var prev = a;
            for (int i = 1; i <= n; i++)
            {
                var p = a + flat * (i / (float)n);
                if (!Physics.Raycast(new Vector3(p.x, y + 1f, p.z), Vector3.down, out var h, 2f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
                if (Mathf.Abs(h.point.y - y) > maxStep) return false;
                var cur = new Vector3(p.x, h.point.y, p.z);
                for (int k = 0; k < 2; k++)
                {
                    var up = Vector3.up * (k == 0 ? 0.6f : 1.3f);
                    if (Physics.Linecast(prev + up, cur + up, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
                }
                prev = cur;
                y = h.point.y;
            }
            endY = y;
            return true;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// Editor-only preview renderer (started by Editor/PreviewShots.cs in Play mode): renders the requested views
    /// through the real game pipeline (lighting presets, water mirror, AgX post) into PNG files in
    /// &lt;project&gt;/Previews, so what the previews show is exactly what the game shows. Not active in device builds.
    /// </summary>
    public class PreviewCapture : MonoBehaviour
    {
        [Serializable]
        public class Shot
        {
            public string name;
            public string preset = "sunset";
            public string location;           // map for shots without a mission (null = harbour)
            public string mission;            // optional: run this contract's setup (its NPCs, weather) first
            public string view = "player";    // player | look | zoom | orbit
            public float yaw, pitch = -9f, fov = 55f;
            public float[] target;            // zoom / orbit target (world)
            public float dist = 6f, orbitYaw = 200f, orbitPitch = 12f;
            public float wait = 1.5f;
            /// <summary>Contract shots: end the intro right away so the contract clock (timeline) runs during the wait.</summary>
            public bool play;
            public bool rifle = true;
            public bool npcs = true;          // showcase characters when no mission is given
            /// <summary>Tuning: JSON of Env.Preset fields laid over the preset (as this map shows it) for this shot.</summary>
            public string envJson;
            /// <summary>Debug switches for this shot only: "nearOffset=50;shadowDist=300;cascades=1;hide=sc_far_shore,Water;nocast=sc_x".</summary>
            public string debug;
        }

        [Serializable]
        public class Request
        {
            public string id = "manual";
            public string stamp;
            public int width = 1600, height = 740;
            public int quality = 2;
            public Shot[] shots;
        }

        [Serializable]
        class Result
        {
            public string id, stamp, started, finished;
            public bool ok;
            public List<string> files = new List<string>();
            public List<string> log = new List<string>();
        }

        public const string RequestFile = "Library/SC_preview_request.json";
        public static string ProjectDir => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string OutDir => Path.Combine(ProjectDir, "Previews");

        public static bool Requested
        {
            get
            {
#if UNITY_EDITOR
                return File.Exists(Path.Combine(ProjectDir, RequestFile));
#else
                return false;
#endif
            }
        }

        Result result;
        readonly List<GameObject> showcase = new List<GameObject>();

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }

        void OnLog(string msg, string stack, LogType type)
        {
            if (result == null || result.log.Count > 200) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning)
                result.log.Add("[" + type + "] " + msg + (type == LogType.Exception ? "\n" + stack : ""));
        }

        void Start() { StartCoroutine(Run()); }

        IEnumerator Run()
        {
            result = new Result { started = DateTime.Now.ToString("s") };
            Request req = null;
            try
            {
                req = JsonUtility.FromJson<Request>(File.ReadAllText(Path.Combine(ProjectDir, RequestFile)));
                File.Delete(Path.Combine(ProjectDir, RequestFile));
            }
            catch (Exception e) { result.log.Add("request: " + e.Message); }
            if (req == null || req.shots == null || req.shots.Length == 0) req = DefaultRequest();
            result.id = req.id;
            result.stamp = req.stamp;
            Directory.CreateDirectory(OutDir);
            float deadline = Time.realtimeSinceStartup + 600f;

            var g = Game.I;
            while (g == null || !g.Ready) { yield return null; g = Game.I; if (Time.realtimeSinceStartup > deadline) break; }
            if (g != null && g.Ready)
            {
                g.ApplyQuality(req.quality);
                if (g.UI != null) g.UI.gameObject.SetActive(false);
                g.Sniper.InputEnabled = false;
                var cam = BuildCaptureCamera(g.Cam);
                string lastSetup = null;
                foreach (var s in req.shots)
                {
                    if (Time.realtimeSinceStartup > deadline) { result.log.Add("timeout"); break; }
                    var def = !string.IsNullOrEmpty(s.mission) ? MissionDefs.Get(s.mission) : null;
                    string loc = def != null ? def.region : (string.IsNullOrEmpty(s.location) ? "harbor" : s.location);
                    string setup = (def != null ? "m:" + s.mission : "p:" + s.preset + (s.npcs ? "+npc" : "") + "|" + s.envJson) + "@" + loc;
                    if (loc != g.LocationId)
                    {
                        // another map: build it (the loading screen needs the UI for a moment)
                        ClearShowcase();
                        g.Runtime.Abort();
                        if (g.UI != null) g.UI.gameObject.SetActive(true);
                        yield return g.SwitchLocation(loc, false);
                        if (g.UI != null) g.UI.gameObject.SetActive(false);
                        g.ApplyQuality(req.quality);
                        lastSetup = null;
                    }
                    if (setup != lastSetup)
                    {
                        ClearShowcase();
                        g.Runtime.Abort();
                        if (def != null) { g.Runtime.Begin(def); if (s.play) g.Runtime.EndIntro(); }
                        else
                        {
                            if (!string.IsNullOrEmpty(s.envJson))
                            {
                                var p = Env.Clone(Env.Localize(Env.Find(s.preset)));
                                try { JsonUtility.FromJsonOverwrite(s.envJson, p); }
                                catch (Exception e) { result.log.Add("envJson " + s.name + ": " + e.Message); }
                                g.Env.ApplyPreset(p, g.Cam, g.Post);
                            }
                            else g.Env.Apply(s.preset, g.Cam, g.Post);
                            if (s.npcs) SpawnShowcase();
                        }
                        g.Sniper.InputEnabled = false;
                        lastSetup = setup;
                        yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, s.wait));
                    }
                    else yield return null;
                    bool ok = false;
                    var undo = ApplyDebug(s.debug);
                    try { ok = Capture(g, cam, s, req.width, req.height); }
                    catch (Exception e) { result.log.Add("shot " + s.name + ": " + e); }
                    finally { undo?.Invoke(); }
                    if (ok) result.files.Add(s.name + ".png");
                    yield return null;
                }
                g.Runtime.Abort();
                ClearShowcase();
                result.ok = result.files.Count > 0;
            }
            else result.log.Add("game did not boot");
            result.finished = DateTime.Now.ToString("s");
            try { File.WriteAllText(Path.Combine(OutDir, "result.json"), JsonUtility.ToJson(result, true)); }
            catch (Exception e) { Debug.LogException(e); }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        Camera BuildCaptureCamera(Camera main)
        {
            var go = new GameObject("PreviewCaptureCam");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.CopyFrom(main);
            cam.allowMSAA = false;
            var fx = go.AddComponent<PostFX>();
            fx.manualSRGB = true;
            return cam;
        }

        bool Capture(Game g, Camera cam, Shot s, int w, int h)
        {
            var main = g.Cam;
            var fx = cam.GetComponent<PostFX>();
            var mfx = g.Post;
            fx.CopyFrom(mfx);
            fx.extraVignette = 0f;
            var eye = g.Level.PlayerPosition;
            float baseYaw = g.Level.Data.player != null ? g.Level.Data.player.yaw : 0f;
            cam.fieldOfView = s.fov;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 6000f;
            switch (s.view)
            {
                case "zoom":
                    {
                        var t = Target(s, eye);
                        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(t - eye));
                        break;
                    }
                case "orbit":
                    {
                        var t = Target(s, eye);
                        var dir = Quaternion.Euler(-s.orbitPitch, s.orbitYaw, 0) * Vector3.forward;
                        var p = t - dir * s.dist;
                        cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(t - p));
                        break;
                    }
                default:
                    {
                        // "player" = the opening view of a contract (its focus), "look" = explicit yaw / pitch from the roof
                        if (s.view == "player" && !string.IsNullOrEmpty(s.mission))
                        {
                            cam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
                            if (s.yaw != 0f) cam.transform.Rotate(Vector3.up, s.yaw, Space.World);
                        }
                        else cam.transform.SetPositionAndRotation(eye, Quaternion.Euler(-s.pitch, baseYaw + s.yaw, 0));
                        break;
                    }
            }
            // view model rifle: the main camera's rifle follows the main camera; show it only on the roof views
            g.Sniper.ShowRifle = false;
            Transform rifle = null;
            if (s.rifle && (s.view == "player" || s.view == "look")) rifle = Sniper.CreateRifleView(cam.transform);

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false, true);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            if (rifle != null) Destroy(rifle.gameObject);
            File.WriteAllBytes(Path.Combine(OutDir, s.name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            return true;
        }

        /// <summary>Debug switches (quality, hidden objects); returns what puts them back.</summary>
        Action ApplyDebug(string dbg)
        {
            if (string.IsNullOrEmpty(dbg)) return null;
            float near = QualitySettings.shadowNearPlaneOffset, dist = QualitySettings.shadowDistance;
            int casc = QualitySettings.shadowCascades;
            var proj = QualitySettings.shadowProjection;
            var sun = Env.I != null ? Env.I.Sun : null;
            float bias = sun != null ? sun.shadowBias : 0f, nbias = sun != null ? sun.shadowNormalBias : 0f;
            var sh = sun != null ? sun.shadows : LightShadows.Soft;
            float sunWas = -1f;
            var hidden = new List<GameObject>();
            bool maskSaved = false;
            float reflWas = -1f;
            bool poolSaved = false;
            Vector4 poolWas = Vector4.zero;
            var lightsOff = new List<Light>();
            Vector4 maskWas = Vector4.zero;
            var casting = new List<KeyValuePair<Renderer, UnityEngine.Rendering.ShadowCastingMode>>();
            foreach (var part in dbg.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                string k = kv[0].Trim(), v = kv[1].Trim();
                float f;
                if (k == "nearOffset" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) QualitySettings.shadowNearPlaneOffset = f;
                else if (k == "shadowDist" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) QualitySettings.shadowDistance = f;
                else if (k == "cascades" && int.TryParse(v, out var n)) QualitySettings.shadowCascades = n;
                else if (k == "proj") QualitySettings.shadowProjection = v == "close" ? ShadowProjection.CloseFit : ShadowProjection.StableFit;
                else if (k == "bias" && sun != null && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) sun.shadowBias = f;
                else if (k == "nbias" && sun != null && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) sun.shadowNormalBias = f;
                else if (k == "hard" && sun != null) sun.shadows = LightShadows.Hard;
                else if (k == "noshadows" && sun != null) sun.shadows = LightShadows.None;
                else if (k == "sunoff" && sun != null) { sunWas = sun.intensity; sun.intensity = 0f; }
                else if (k == "maskK")
                {
                    // ground detail mask strengths "tint,stain,scale2,strength2" (SC/Ground), put back after the shot
                    var parts = v.Split(',');
                    var mk = Shader.GetGlobalVector("_SC_GroundMaskK");
                    if (!maskSaved) { maskWas = mk; maskSaved = true; }
                    for (int pi = 0; pi < 4 && pi < parts.Length; pi++)
                        if (float.TryParse(parts[pi], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) mk[pi] = f;
                    Shader.SetGlobalVector("_SC_GroundMaskK", mk);
                }
                else if (k == "refl" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f))
                {
                    if (reflWas < 0f) reflWas = RenderSettings.reflectionIntensity;
                    RenderSettings.reflectionIntensity = f;
                }
                else if (k == "envdump") DumpEnv(v);
                else if (k == "pool" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f))
                {
                    // the baked lamp pools of SC/Ground scaled (0 = off), put back after the shot
                    var pv = Shader.GetGlobalVector("_SC_NightPool");
                    if (!poolSaved) { poolWas = pv; poolSaved = true; }
                    Shader.SetGlobalVector("_SC_NightPool", pv * f);
                }
                else if (k == "lightsoff" && Level.I != null)
                {
                    // every point / spot light of the level off (the sun stays), to see what the real lamps light
                    foreach (var li in Level.I.GetComponentsInChildren<Light>(true))
                        if (li != null && li.enabled && li.type != LightType.Directional) { li.enabled = false; lightsOff.Add(li); }
                    result.log.Add("lightsoff: " + lightsOff.Count);
                }
                else if (k == "hide" || k == "nocast")
                {
                    var names = v.Split(',');
                    var all = new List<Renderer>();
                    if (Level.I != null) all.AddRange(Level.I.GetComponentsInChildren<Renderer>(true));
                    if (Env.I != null) all.AddRange(Env.I.GetComponentsInChildren<Renderer>(true));
                    foreach (var r in all)
                    {
                        if (r == null) continue;
                        Transform t = r.transform; bool match = false;
                        while (t != null && !match) { foreach (var nm in names) if (nm.Length > 0 && t.name.StartsWith(nm)) { match = true; break; } t = t.parent; }
                        if (!match) continue;
                        if (k == "hide") { if (r.gameObject.activeSelf) { r.gameObject.SetActive(false); hidden.Add(r.gameObject); } }
                        else { casting.Add(new KeyValuePair<Renderer, UnityEngine.Rendering.ShadowCastingMode>(r, r.shadowCastingMode)); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                    }
                    result.log.Add(k + " " + v + ": " + (k == "hide" ? hidden.Count : casting.Count) + " renderers");
                }
            }
            return () =>
            {
                QualitySettings.shadowNearPlaneOffset = near; QualitySettings.shadowDistance = dist; QualitySettings.shadowCascades = casc;
                QualitySettings.shadowProjection = proj;
                if (sun != null) { sun.shadowBias = bias; sun.shadowNormalBias = nbias; sun.shadows = sh; if (sunWas >= 0f) sun.intensity = sunWas; }
                foreach (var go in hidden) if (go != null) go.SetActive(true);
                if (maskSaved) Shader.SetGlobalVector("_SC_GroundMaskK", maskWas);
                if (reflWas >= 0f) RenderSettings.reflectionIntensity = reflWas;
                if (poolSaved) Shader.SetGlobalVector("_SC_NightPool", poolWas);
                foreach (var li in lightsOff) if (li != null) li.enabled = true;
                foreach (var kv in casting) if (kv.Key != null) kv.Key.shadowCastingMode = kv.Value;
            };
        }

        /// <summary>Writes the six faces of the reflection capture (post exposure, Reinhard, sRGB) and logs the mean and
        /// peak luminance of each face, to find what bright thing glossy surfaces are reflecting.</summary>
        void DumpEnv(string tag)
        {
            var rt = Env.I != null ? Env.I.EnvCapture : null;
            if (rt == null) { result.log.Add("envdump: no capture"); return; }
            float ex = Env.I.Current != null ? Env.I.Current.exposure : 1f;
            var tmp = RenderTexture.GetTemporary(rt.width, rt.height, 0, rt.format);
            var face = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false, true);
            var outTex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            string[] names = { "px", "nx", "py", "ny", "pz", "nz" };
            result.log.Add("envdump at " + Env.I.EnvCapturePos + " refl " + RenderSettings.reflectionIntensity + " mode " + RenderSettings.defaultReflectionMode);
            for (int fi = 0; fi < 6; fi++)
            {
                Graphics.CopyTexture(rt, fi, 0, tmp, 0, 0);
                var prev = RenderTexture.active;
                RenderTexture.active = tmp;
                face.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                face.Apply();
                RenderTexture.active = prev;
                var px = face.GetPixels();
                double sum = 0; float peak = 0f; int bright = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                    sum += l; if (l > peak) peak = l; if (l > 1f) bright++;
                    var t = c * ex;
                    t = new Color(t.r / (1f + t.r), t.g / (1f + t.g), t.b / (1f + t.b));
                    px[i] = new Color(Mathf.LinearToGammaSpace(t.r), Mathf.LinearToGammaSpace(t.g), Mathf.LinearToGammaSpace(t.b), 1f);
                }
                outTex.SetPixels(px);
                outTex.Apply();
                string fn = "env_" + tag + "_" + names[fi] + ".png";
                File.WriteAllBytes(Path.Combine(OutDir, fn), outTex.EncodeToPNG());
                result.files.Add(fn);
                result.log.Add("env " + names[fi] + " mean " + (sum / px.Length).ToString("0.000") + " peak " + peak.ToString("0.00") + " >1: " + bright);
            }
            RenderTexture.ReleaseTemporary(tmp);
            Destroy(face);
            Destroy(outTex);
        }

        static Vector3 Target(Shot s, Vector3 fallback)
        {
            if (s.target != null && s.target.Length >= 3) return new Vector3(s.target[0], s.target[1], s.target[2]);
            return fallback + Vector3.forward * 100f;
        }

        // a few characters at their usual spots so the previews show people under each light
        void SpawnShowcase()
        {
            var lv = Level.I;
            if (lv == null) return;
            var spots = new (string marker, string ch)[]
            {
                ("gate_post", "guard_a"), ("gate_lane", "businessman"), ("office_smoke", "worker_a"), ("deal_spot", "smuggler"),
                ("deal_buyer", "rico"), ("deal_guard1", "bodyguard_a"), ("quay_worker1", "worker_b"), ("quay_worker2", "worker_c"),
                ("yacht_mid", "boss"), ("party_0", "guest_f_a"), ("party_1", "guest_m"), ("party_2", "guest_f_b"), ("tower_top", "guard_b"),
            };
            foreach (var sp in spots)
            {
                if (!lv.HasMarker(sp.marker)) continue;
                var n = NPC.Spawn(sp.ch, lv.Marker(sp.marker), lv.MarkerYaw(sp.marker), null);
                if (n != null) showcase.Add(n.gameObject);
            }
        }

        void ClearShowcase()
        {
            foreach (var go in showcase) if (go != null) Destroy(go);
            showcase.Clear();
        }

        public static Request DefaultRequest()
        {
            var shots = new List<Shot>();
            foreach (var p in new[] { "sunset", "day", "overcast", "night" })
            {
                shots.Add(new Shot { name = p + "_1_roof", preset = p, view = "look", yaw = 0, pitch = -9, fov = 55 });
                shots.Add(new Shot { name = p + "_2_gate", preset = p, view = "zoom", target = new[] { 2f, 2f, 10f }, fov = 16 });
                shots.Add(new Shot { name = p + "_3_quay", preset = p, view = "zoom", target = new[] { -110f, 10f, 262f }, fov = 22 });
                shots.Add(new Shot { name = p + "_4_yacht", preset = p, view = "zoom", target = new[] { 156f, 2f, 300f }, fov = 12 });
                shots.Add(new Shot { name = p + "_5_close", preset = p, view = "orbit", target = new[] { 2f, 1.2f, 10f }, dist = 4.5f, orbitYaw = 200f, orbitPitch = 8f, fov = 40 });
            }
            // the city centre: the square from the hotel roof, the opera steps, the lit office floor, the penthouse, the pier
            foreach (var p in new[] { "night", "rain_night", "sunset", "day" })
            {
                shots.Add(new Shot { name = "city_" + p + "_1_roof", location = "city", preset = p, view = "look", yaw = 0, pitch = -14, fov = 60, npcs = false });
                shots.Add(new Shot { name = "city_" + p + "_2_opera", location = "city", preset = p, view = "zoom", target = new[] { -96f, 4f, 60f }, fov = 14, npcs = false });
                shots.Add(new Shot { name = "city_" + p + "_3_tower", location = "city", preset = p, view = "zoom", target = new[] { 96f, 44.5f, 16f }, fov = 12, npcs = false });
                shots.Add(new Shot { name = "city_" + p + "_4_palas", location = "city", preset = p, view = "zoom", target = new[] { 108f, 22f, 82f }, fov = 16, npcs = false });
                shots.Add(new Shot { name = "city_" + p + "_5_pier", location = "city", preset = p, view = "zoom", target = new[] { 4f, 3f, 180f }, fov = 18, npcs = false });
                shots.Add(new Shot { name = "city_" + p + "_6_clock", location = "city", preset = p, view = "orbit", target = new[] { -40f, 14f, 70f }, dist = 48f, orbitYaw = 150f, orbitPitch = 14f, fov = 40, npcs = false });
            }
            // Kartal Tepe: the valley from the weather station, the hotel lounge, the glass villa, the gondola, the lake,
            // the avalanche slope, the station close up
            foreach (var p in new[] { "snow_sunset", "snow_day", "snow_night", "blizzard" })
            {
                shots.Add(new Shot { name = "mtn_" + p + "_1_nest", location = "mountain", preset = p, view = "look", yaw = 0, pitch = -9, fov = 60, npcs = false });
                shots.Add(new Shot { name = "mtn_" + p + "_2_hotel", location = "mountain", preset = p, view = "zoom", target = new[] { 20f, 5f, 140f }, fov = 12, npcs = false });
                shots.Add(new Shot { name = "mtn_" + p + "_3_villa", location = "mountain", preset = p, view = "zoom", target = new[] { 192f, 20f, 50f }, fov = 9, npcs = false });
                shots.Add(new Shot { name = "mtn_" + p + "_4_gondola", location = "mountain", preset = p, view = "zoom", target = new[] { -120f, 60f, 480f }, fov = 22, npcs = false });
                shots.Add(new Shot { name = "mtn_" + p + "_5_lake", location = "mountain", preset = p, view = "zoom", target = new[] { 145f, 0f, 112f }, fov = 20, npcs = false });
                shots.Add(new Shot { name = "mtn_" + p + "_6_west", location = "mountain", preset = p, view = "look", yaw = -62, pitch = -6, fov = 50, npcs = false });
                shots.Add(new Shot { name = "mtn_" + p + "_7_station", location = "mountain", preset = p, view = "orbit", target = new[] { -88f, 6f, 205f }, dist = 46f, orbitYaw = 150f, orbitPitch = 18f, fov = 40, npcs = false });
            }
            foreach (var m in MissionDefs.All)
                shots.Add(new Shot { name = "mission_" + m.id, mission = m.id, view = "player", fov = 55, wait = 2.5f });
            return new Request { id = "default", shots = shots.ToArray() };
        }
    }
}

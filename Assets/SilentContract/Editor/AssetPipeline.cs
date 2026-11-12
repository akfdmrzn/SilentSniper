using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SilentContract.EditorTools
{
    // ------------------------------------------------------------------ JSON metadata written by the Blender tools
    [Serializable]
    public class PropMatMeta
    {
        public string name;
        public float[] color;
        public float metallic, smoothness;
        public string albedo, normal, ms;
        public bool cutout, double_sided;
        public string src;              // material name in the Blender source ("win_res", "emit_signs", "glass_pane" ...)
        public bool window;             // lit-interior window atlas -> SC/Window
        public float[] emission;        // night light (linear rgb x strength), albedo used as emission map
    }

    [Serializable]
    public class PropMeta
    {
        public string id, category, collider, credit;
        public int tris, lods;
        public float[] size, center;
        public PropMatMeta[] materials;
        public bool @static = true;
        public string[] tags;
    }

    [Serializable]
    public class CharMatMeta { public string name, albedo, normal, ms; public bool cutout; }

    [Serializable]
    public class CharMeta
    {
        public string id, gender;
        public int tris;
        public float height;
        public CharMatMeta[] materials;
    }

    [Serializable]
    public class AnimMeta
    {
        public string name, source;
        public int frames;
        public float fps, length, speed;
        public bool loop;
        public string[] tags;
        public float[] offset;      // one-shot clips: [right, forward] root displacement
    }

    [Serializable] class BorderEntry { public string name; public int[] border; }

    /// <summary>
    /// Import rules for everything under Assets/SilentContract: textures (UI sprites with 9-slice borders, PBR maps,
    /// skies), models (props, MPFB characters, retargeted legacy animation clips) and clip curve clean-up.
    /// </summary>
    public class ImportRules : AssetPostprocessor
    {
        public override uint GetVersion() => 7;

        static Dictionary<string, Vector4> borders;

        static Vector4 UIBorder(string file)
        {
            if (borders == null)
            {
                borders = new Dictionary<string, Vector4>();
                const string metaPath = "Assets/SilentContract/Art/UI/ui_meta.json";
                if (File.Exists(metaPath))
                {
                    // ui_meta.json: {"rr8": {"border": [l, b, r, t]}, ...} — tiny hand parser (JsonUtility has no dictionaries)
                    var txt = File.ReadAllText(metaPath);
                    int i = 0;
                    while (true)
                    {
                        int q0 = txt.IndexOf('"', i);
                        if (q0 < 0) break;
                        int q1 = txt.IndexOf('"', q0 + 1);
                        string key = txt.Substring(q0 + 1, q1 - q0 - 1);
                        int b0 = txt.IndexOf('[', q1);
                        int b1 = txt.IndexOf(']', b0);
                        int next = txt.IndexOf('}', q1);
                        if (b0 < 0 || b1 < 0 || next < 0 || b0 > next) { i = q1 + 1; continue; }
                        var nums = txt.Substring(b0 + 1, b1 - b0 - 1).Split(',').Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        if (key != "border" && nums.Length == 4) borders[key] = new Vector4(nums[0], nums[1], nums[2], nums[3]);
                        i = next + 1;
                    }
                }
            }
            return borders.TryGetValue(file, out var v) ? v : Vector4.zero;
        }

        static void IOS(TextureImporter ti, TextureImporterFormat fmt, int max)
        {
            var s = ti.GetPlatformTextureSettings("iPhone");
            s.overridden = true;
            s.maxTextureSize = max;
            s.format = fmt;
            s.compressionQuality = 50;
            ti.SetPlatformTextureSettings(s);
            // Android: the same sizes and ASTC formats (every Android phone the game targets reads ASTC)
            var a = ti.GetPlatformTextureSettings("Android");
            a.overridden = true;
            a.maxTextureSize = max;
            a.format = fmt;
            a.compressionQuality = 50;
            ti.SetPlatformTextureSettings(a);
        }

        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/SilentContract/")) return;
            string file = Path.GetFileNameWithoutExtension(p);
            ti.GetSourceTextureWidthAndHeight(out int w, out int h);

            if (p.Contains("/Resources/SC/UI/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.sRGBTexture = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.spritePixelsPerUnit = 100;
                ti.spriteBorder = UIBorder(file);
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 2048;
                if (w * h >= 512 * 512) IOS(ti, TextureImporterFormat.ASTC_4x4, 2048);
                else IOS(ti, TextureImporterFormat.RGBA32, 1024);
                return;
            }
            if (p.Contains("/Resources/SC/Portraits/") || p.Contains("/Resources/SC/Art/") || p.Contains("/Resources/SC/Maps/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                IOS(ti, TextureImporterFormat.ASTC_5x5, 2048);
                return;
            }
            if (p.Contains("/Resources/SC/Textures/") && file.StartsWith("mtn_ground_ao"))
            {
                // the mountain's ground maps: R ambient occlusion, G trodden snow, B height of the highest surface (snow
                // cover): exact values, no compression
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 4;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                IOS(ti, TextureImporterFormat.RGBA32, 2048);
                return;
            }
            if (p.Contains("/Resources/SC/Textures/") && file.EndsWith("_ground_mask"))
            {
                // V3 ground detail masks (R tint, G stains): linear data, clamped to the map rect
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 4;
                ti.maxTextureSize = 2048;
                IOS(ti, TextureImporterFormat.ASTC_4x4, 2048);
                return;
            }
            if (p.Contains("/Resources/SC/Textures/") && (file.StartsWith("ground_ao") || file.StartsWith("city_ground_ao")))
            {
                ti.textureType = TextureImporterType.SingleChannel;
                var tis = new TextureImporterSettings();
                ti.ReadTextureSettings(tis);
                tis.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
                ti.SetTextureSettings(tis);
                ti.sRGBTexture = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 4;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                IOS(ti, TextureImporterFormat.R8, 2048);
                return;
            }
            if (p.Contains("/Resources/SC/Textures/"))
            {
                bool waterNormal = file.StartsWith("water_normal");
                ti.textureType = file.Contains("normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.mipmapEnabled = true;
                // the water is mostly seen at grazing angles: trilinear (no mip bands) + anisotropic filtering
                ti.filterMode = waterNormal ? FilterMode.Trilinear : FilterMode.Bilinear;
                ti.anisoLevel = waterNormal ? 4 : 2;
                IOS(ti, TextureImporterFormat.ASTC_4x4, 1024);
                return;
            }
            if (p.Contains("/Art/Sky/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.textureShape = TextureImporterShape.Texture2D;
                ti.mipmapEnabled = false;
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.sRGBTexture = true;
                ti.maxTextureSize = 4096;
                IOS(ti, TextureImporterFormat.ASTC_6x6, 4096);
                return;
            }
            if (p.Contains("/Art/Props/") || p.Contains("/Art/Chars/"))
            {
                bool normal = file.EndsWith("_normal");
                bool ms = file.EndsWith("_ms");
                bool cutout = file.EndsWith("_cutout");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !(normal || ms);
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.filterMode = FilterMode.Trilinear;
                ti.anisoLevel = file.StartsWith("sc_level_ground") || file.StartsWith("sc_city_ground") || file.StartsWith("shared__mtn_snow") ||
                                file.StartsWith("shared__mtn_rock") || file.StartsWith("shared__mtn_road") ? 8 : 2;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                if (cutout || (!normal && !ms && p.EndsWith(".png") && !p.Contains("/Art/Chars/")))
                {
                    ti.alphaIsTransparency = cutout;
                    ti.mipMapsPreserveCoverage = true;
                    ti.alphaTestReferenceValue = 0.45f;
                }
                int max = Mathf.Clamp(Mathf.Max(w, h), 64, 2048);
                if (p.Contains("/Art/Chars/")) max = normal ? 1024 : (cutout ? 1024 : 2048);
                ti.maxTextureSize = max;
                IOS(ti, normal ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6, max);
            }
        }

        /// <summary>The game's recorded sounds: mono, short ones decoded at load (no CPU while playing), the long beds kept
        /// compressed in memory.</summary>
        void OnPreprocessAudio()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/SC/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = true;
            var st = ai.defaultSampleSettings;
            var fi = new System.IO.FileInfo(p);
            bool big = fi.Exists && fi.Length > 60000;
            st.loadType = big ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            st.compressionFormat = AudioCompressionFormat.Vorbis;
            st.quality = 0.6f;
            ai.defaultSampleSettings = st;
        }

        void OnPreprocessModel()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/SilentContract/Art/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.addCollider = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.bakeAxisConversion = false;

            if (p.Contains("/Art/Props/"))
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.preserveHierarchy = false;
                mi.isReadable = AssetPipeline.NeedsReadableMesh(Path.GetFileNameWithoutExtension(p));
            }
            else if (p.Contains("/Art/Chars/"))
            {
                mi.animationType = ModelImporterAnimationType.Legacy;
                mi.importAnimation = false;
                mi.preserveHierarchy = true;
                mi.skinWeights = ModelImporterSkinWeights.Standard;
            }
            else if (p.Contains("/Art/Anims/"))
            {
                mi.animationType = ModelImporterAnimationType.Legacy;
                mi.importAnimation = true;
                mi.preserveHierarchy = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
                mi.animationRotationError = 0.3f;
                mi.animationPositionError = 0.3f;
                mi.resampleCurves = true;
            }
        }

        /// <summary>Retargeted clips are in place: only the hips may translate; scale keys are never needed.</summary>
        void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/SilentContract/Art/Anims/")) return;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                bool pos = b.propertyName.StartsWith("m_LocalPosition");
                bool scale = b.propertyName.StartsWith("m_LocalScale");
                bool hips = b.path.EndsWith("mixamorig:Hips");
                if (scale || (pos && !hips)) AnimationUtility.SetEditorCurve(clip, b, null);
            }
        }
    }

    /// <summary>
    /// Builds the runtime content from the imported art: materials, prop prefabs (with LODs and colliders),
    /// character prefabs (legacy Animation), the animation bank, skybox materials and small shared materials.
    /// Menu: Silent Contract → İçeriği Oluştur. Also called automatically by the iOS build.
    /// </summary>
    public static class AssetPipeline
    {
        public const string Base = "Assets/SilentContract";
        const string ArtProps = Base + "/Art/Props";
        const string ArtChars = Base + "/Art/Chars";
        const string ArtAnims = Base + "/Art/Anims";
        const string ArtSky = Base + "/Art/Sky";
        const string Gen = Base + "/Generated";
        const string ResSC = Base + "/Resources/SC";

        // props whose collision should follow the real geometry (open doors, windows, decks, thin poles)
        static readonly string[] MeshColliderPrefixes =
        {
            "sc_office", "sc_guard_booth", "sc_watchtower", "sc_lighthouse", "sc_warehouse_", "sc_cargo_ship_", "sc_sts_crane_",
            "sc_mast_light", "ai_yacht", "ai_speedboat", "ai_tugboat", "ph_street_lamp", "ph_rocks_land", "ph_searchlight",
            // v8: cars, carts and street furniture are anything but boxes. Their bounding box wrapped the air over a
            // bonnet, under a table, between chair legs, so a gunman crouched beside an SUV was hit "in" the car while
            // the reticle sat on his chest. Their own hull now stops the bullet, the air around it does not.
            // (The snowcat keeps its trimmed box: MountainFX cuts it below the cab glass so its driver can be shot.)
            "ai_suv", "ai_limo", "ai_lux_sedan", "ai_police_car", "ai_taxi", "ph_covered_car", "ai_heli", "ai_snowmobile", "ai_scooter",
            "sr_forklift", "ai_simit_cart", "ph_coffee_cart", "ph_welding_cart", "sr_pallet_jack", "ai_cafe_set", "ph_table_set",
            "ph_picnic_table", "ai_sofa", "ph_sofa", "ph_armchair", "ph_rocking_chair", "ph_bar_stool", "ph_green_chair", "ai_office_desk",
            "ph_coffee_table", "ph_bench", "ai_press_cam", "ai_kiosk", "ai_ad_column", "ai_statue", "ph_fire_hydrant", "ph_road_barrier",
            "sr_barrier_orange", "ph_wooden_barrels", "ph_sea_marker", "ph_ocean_buoy", "ai_firewood",
        };
        /// <summary>Vehicles and carts a contract may drive (readable meshes, see <see cref="NeedsReadableMesh"/>).</summary>
        public static readonly string[] MovingHulls =
        {
            "ai_suv", "ai_limo", "ai_lux_sedan", "ai_police_car", "ai_taxi", "ph_covered_car", "ai_heli", "ai_snowmobile", "ai_scooter",
            "sr_forklift", "ai_simit_cart", "ph_coffee_cart",
        };
        static readonly string[] NoColliderPrefixes = { "sc_city_", "sc_far_shore", "ph_cliff", "sc_level_ground", "ph_fence", "ph_security_camera", "ph_binoculars", "ph_palm", "ph_shrub" };
        // glass that must stay see-through (people stand behind it)
        static readonly string[] TransparentGlass = { "sc_guard_booth", "sc_watchtower" };

        // retargeted clip file -> bank name
        static readonly Dictionary<string, string> AnimMap = new Dictionary<string, string>
        {
            {"m_idle", "idle"}, {"m_idle2", "idle2"}, {"m_idle_arms", "idle_arms"}, {"m_idle_bus", "idle_bus"}, {"m_idle_look", "idle_look"},
            {"m_idle_shift", "idle_shift"}, {"m_idle_wait", "idle_wait"},
            {"m_walk2", "walk"}, {"m_walk", "walk_slow"}, {"m_walk_careful", "walk_careful"}, {"m_walk_carry", "walk_carry"},
            {"m_walk_confident", "walk_confident"}, {"m_walk_patrol", "walk_patrol"}, {"m_walk_strut", "walk_strut"},
            {"m_run2", "run"}, {"m_run", "run_panic"},
            {"m_talk", "talk"}, {"m_talk2", "talk2"}, {"m_phone2", "phone"}, {"m_phone", "phone_talk"}, {"m_smoke", "smoke"},
            {"m_drink", "drink"}, {"m_coffee", "coffee"}, {"m_handshake", "handshake"}, {"m_pay", "pay"}, {"m_signal", "signal"},
            {"m_sweep", "sweep"}, {"m_wave", "wave"}, {"m_argue", "argue"}, {"m_coil_rope", "coil_rope"}, {"m_cower", "cower"},
            {"m_duck", "duck"}, {"m_flashlight", "flashlight"}, {"m_guard_pose", "guard_pose"}, {"m_pull_gun", "pull_gun"},
            {"f_idle_std", "idle"}, {"f_idle_shift", "idle2"}, {"f_walk", "walk"}, {"f_run", "run"}, {"f_phone2", "phone"},
            {"f_phone", "phone_talk"}, {"f_talk", "talk"}, {"f_drink", "drink"}, {"f_smoke", "smoke"}, {"f_wave", "wave"},
            {"f_duck", "duck"}, {"f_cower", "cower"},
            // Meshy animation library (retargeted): wounds, darts, attackers, parties, offices
            {"m_hit_gunshot", "hit_gunshot"}, {"m_hit", "hit"}, {"m_fall_down", "fall_down"}, {"m_wounded_side", "wounded_side"},
            {"m_wounded_prone", "wounded_prone"}, {"m_crawl", "crawl"}, {"m_walk_injured", "walk_injured"}, {"m_walk_limp", "walk_limp"},
            {"m_pain_head", "pain_head"}, {"m_alert", "alert"}, {"m_hand_on_gun", "hand_on_gun"}, {"m_drink_stand", "drink_stand"},
            {"m_dance1", "dance1"}, {"m_dance2", "dance2"}, {"m_dance3", "dance3"}, {"m_chat", "chat"}, {"m_talk_passion", "talk_passion"},
            {"m_talk_open", "talk_open"}, {"m_phone_conv", "phone_conv"}, {"m_walk_texting", "walk_texting"}, {"m_bow", "bow"},
            {"m_wave_big", "wave_big"}, {"m_cheer_up", "cheer_up"}, {"m_cheer", "cheer"}, {"m_sit_idle", "sit_idle"}, {"m_sit_drink", "sit_drink"},
            {"m_sit_talk", "sit_talk"}, {"m_sleep_desk", "sleep_desk"}, {"m_shout", "shout"}, {"m_scheme", "scheme"}, {"m_shrug", "shrug"},
            {"m_walk_sneaky", "walk_sneaky"}, {"m_walk_stumble", "walk_stumble"}, {"m_wave_help", "wave_help"}, {"m_open_door", "open_door"},
            {"m_catch_breath", "catch_breath"}, {"m_cheer2", "cheer2"},
            // marksmen: shouldered rifle, kneeling and standing (the rifle itself is laid in the hands at runtime)
            {"m_rifle_kneel", "rifle_kneel"}, {"m_rifle_aim", "rifle_aim"},
        };
        // female clips that also serve another bank name
        static readonly Dictionary<string, string> AnimAlias = new Dictionary<string, string> { {"f_run", "run_panic"} };

        public static bool NeedsReadableMesh(string id)
        {
            // meshes with glass sub-meshes get a filtered collision copy, which needs CPU access while building;
            // city and mountain buildings are combined at runtime (static batching across buildings sharing materials)
            // v8: the cars, carts and the helicopter too — a mission may drive one (PathMover puts a kinematic body on
            // it), and a moving concave collider must be readable or it is swapped back for the bounding box
            return id.StartsWith("sc_office") || id.StartsWith("sc_guard_booth") || id.StartsWith("sc_watchtower") || id.StartsWith("sc_warehouse_") ||
                   id.StartsWith("sc_cargo_ship_") || id.StartsWith("sc_sts_crane_") || id.StartsWith("sc_lighthouse") || id.StartsWith("sc_c_") ||
                   id.StartsWith("ai_ferry") || id.StartsWith("sc_m_") || Array.IndexOf(MovingHulls, id) >= 0;
        }

        [MenuItem("Silent Contract/İçeriği Oluştur (prefab + materyal + animasyon)", priority = 2)]
        public static void BuildMenu()
        {
            bool ok = BuildAll();
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Silent Contract", ok ? "İçerik hazır." : "İçerik oluşturulurken hata oluştu — Console'a bak.", "Tamam");
        }

        public static bool BuildAll()
        {
            var t0 = DateTime.Now;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var f in new[] { Gen, Gen + "/Materials", Gen + "/Materials/Props", Gen + "/Materials/Chars", Gen + "/Meshes",
                                      ResSC, ResSC + "/Props", ResSC + "/Chars", ResSC + "/Sky", ResSC + "/Materials" })
                EnsureFolder(f);

            int props = 0, chars = 0, errors = 0;
            try
            {
                foreach (var json in Directory.GetFiles(ArtProps, "*.json"))
                {
                    try { if (BuildProp(json)) props++; }
                    catch (Exception e) { errors++; Debug.LogError("[SC] prop " + Path.GetFileName(json) + ": " + e); }
                }
                foreach (var json in Directory.GetFiles(ArtChars, "*.json"))
                {
                    try { if (BuildChar(json)) chars++; }
                    catch (Exception e) { errors++; Debug.LogError("[SC] char " + Path.GetFileName(json) + ": " + e); }
                }
                BuildAnimBank();
                BuildSkies();
                BuildMisc();
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Debug.Log(string.Format("[SC] content built: {0} props, {1} characters, {2} errors in {3:0.0}s", props, chars, errors, (DateTime.Now - t0).TotalSeconds));
            // a single broken asset must not stop the build: it is logged above and simply missing in the level
            return props > 0 && chars > 0;
        }

        // ------------------------------------------------------------------ helpers
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Texture2D Tex(string folder, string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + file);
            if (t == null)
            {
                // the packer may have converted png <-> jpg
                string stem = Path.GetFileNameWithoutExtension(file);
                foreach (var ext in new[] { ".png", ".jpg" })
                {
                    t = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + stem + ext);
                    if (t != null) break;
                }
            }
            return t;
        }

        static Material LoadOrCreate(string path, Shader sh)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != sh) m.shader = sh;
            return m;
        }

        static Color ToColor(float[] c) => c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1f) : Color.white;

        static readonly string[] FacadePrefixes = { "wall_", "palas_wall", "op_stone", "op_base", "ct_stone", "term_wall", "trim_stone", "tw_stone", "op_marble", "ct_trim", "sky_wall" };

        static bool IsGlass(PropMatMeta m) => string.IsNullOrEmpty(m.albedo) && m.smoothness >= 0.84f && m.metallic >= 0.5f;

        static bool Starts(string id, string[] prefixes) => prefixes.Any(p => id.StartsWith(p));

        static string ColliderRule(PropMeta m)
        {
            if (Starts(m.id, NoColliderPrefixes) || m.category == "city") return "none";
            if (Starts(m.id, MeshColliderPrefixes)) return "mesh";
            if (m.collider == "none") return "none";
            if (m.collider == "custom" || m.collider == "mesh") return "mesh";
            return "box";
        }

        static void SetupStandardTransparent(Material mat)
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.One);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        static void SetupStandardOpaque(Material mat)
        {
            mat.SetFloat("_Mode", 0f);
            mat.SetOverrideTag("RenderType", "");
            mat.SetInt("_SrcBlend", (int)BlendMode.One);
            mat.SetInt("_DstBlend", (int)BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }

        [System.Serializable] class GroundAOMeta { public float xMin, zMin, xMax, zMax; }

        /// <summary>The yard / quay ground uses SC/Ground: Standard lighting + the baked top-down ambient occlusion.</summary>
        /// <summary>Ground props with a baked top-down AO map (texture + rect json in Resources/SC).</summary>
        static string GroundAOName(string id)
        {
            if (id == "sc_level_ground") return "ground_ao";
            if (id == "sc_city_ground") return "city_ground_ao";
            if (id == "sc_mtn_ground") return "mtn_ground_ao";
            return null;
        }

        static bool IsTerrain(string id) => id == "sc_mtn_ground" || id == "sc_mtn_far";

        /// <summary>
        /// The mountain terrain (SC/Terrain): material 0 of the prop is the snow, material 1 the rock that shows on crags
        /// (vertex colour R); every sub-mesh gets the one terrain material. The ground maps (R AO, G trodden snow) cover the
        /// village; the far mountains lie outside their rect.
        /// </summary>
        static Material TerrainMaterial(PropMeta meta)
        {
            var sh = Shader.Find("SC/Terrain");
            if (sh == null || ShaderUtil.ShaderHasError(sh) || meta.materials == null || meta.materials.Length == 0) return null;
            var snow = meta.materials[0];
            var rock = meta.materials.Length > 1 ? meta.materials[1] : null;
            var mat = LoadOrCreate(Gen + "/Materials/Props/" + meta.id + "_terrain.mat", sh);
            mat.SetColor("_Color", ToColor(snow.color));
            mat.SetTexture("_MainTex", Tex(ArtProps, snow.albedo));
            mat.SetTexture("_BumpMap", Tex(ArtProps, snow.normal));
            if (rock != null)
            {
                mat.SetColor("_RockColor", ToColor(rock.color));
                mat.SetTexture("_RockTex", Tex(ArtProps, rock.albedo));
                mat.SetTexture("_RockBump", Tex(ArtProps, rock.normal));
            }
            mat.SetFloat("_RockTile", 0.55f);
            mat.SetFloat("_Sparkle", meta.id == "sc_mtn_ground" ? 0.8f : 0f);
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(ResSC + "/Textures/mtn_ground_ao.png");
            var metaPath = ResSC + "/Level/mtn_ground_ao.json";
            if (ao != null && File.Exists(metaPath))
            {
                var r = JsonUtility.FromJson<GroundAOMeta>(File.ReadAllText(metaPath));
                mat.SetTexture("_AOTex", ao);
                mat.SetVector("_AORect", new Vector4(r.xMin, r.zMin, 1f / Mathf.Max(1f, r.xMax - r.xMin), 1f / Mathf.Max(1f, r.zMax - r.zMin)));
                mat.SetFloat("_AOStrength", 1f);
            }
            else
            {
                mat.SetTexture("_AOTex", null);
                mat.SetVector("_AORect", Vector4.zero);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material GroundMaterial(PropMatMeta m, string aoName)
        {
            var sh = Shader.Find("SC/Ground");
            if (sh == null || ShaderUtil.ShaderHasError(sh)) return null;
            var mat = LoadOrCreate(Gen + "/Materials/Props/" + m.name + ".mat", sh);
            mat.SetColor("_Color", ToColor(m.color));
            mat.SetTexture("_MainTex", Tex(ArtProps, m.albedo));
            var normal = Tex(ArtProps, m.normal);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1f);
            var ms = Tex(ArtProps, m.ms);
            mat.SetTexture("_MetallicGlossMap", ms);
            mat.SetFloat("_UseMS", ms != null ? 1f : 0f);
            mat.SetFloat("_Metallic", Mathf.Clamp01(m.metallic));
            mat.SetFloat("_Glossiness", Mathf.Clamp01(m.smoothness));
            mat.SetFloat("_GlossMapScale", ms != null ? Mathf.Clamp01(m.smoothness <= 0 ? 1f : m.smoothness) : 1f);
            // V3: lawns and flower beds under the baked lamp pools glowed bright green at night
            mat.SetFloat("_PoolGain", m.src == "g_lawn" ? 0.42f : (m.src == "g_flowers" ? 0.55f : 1f));
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(ResSC + "/Textures/" + aoName + ".png");
            var metaPath = ResSC + "/Level/" + aoName + ".json";
            if (ao != null && File.Exists(metaPath))
            {
                var r = JsonUtility.FromJson<GroundAOMeta>(File.ReadAllText(metaPath));
                mat.SetTexture("_AOTex", ao);
                mat.SetVector("_AORect", new Vector4(r.xMin, r.zMin, 1f / Mathf.Max(1f, r.xMax - r.xMin), 1f / Mathf.Max(1f, r.zMax - r.zMin)));
                mat.SetFloat("_AOStrength", 1f);
            }
            else
            {
                mat.SetTexture("_AOTex", null);
                mat.SetVector("_AORect", Vector4.zero);
            }
            mat.enableInstancing = false;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material PropMaterial(string id, PropMatMeta m)
        {
            var aoName = GroundAOName(id);
            if (aoName != null && !m.cutout && !IsGlass(m))
            {
                var g = GroundMaterial(m, aoName);
                if (g != null) return g;
            }
            if (id.StartsWith("sc_mtn_forest"))
            {
                // impostor cards of the far forest (atlas with alpha, a tint per card in the vertex colour)
                var fmat = LoadOrCreate(Gen + "/Materials/Props/" + m.name + ".mat", Shader.Find("SC/ForestCard"));
                fmat.SetTexture("_MainTex", Tex(ArtProps, m.albedo));
                fmat.SetFloat("_Cutoff", 0.45f);
                fmat.SetColor("_Color", Color.white);
                fmat.enableInstancing = false;
                EditorUtility.SetDirty(fmat);
                return fmat;
            }
            if (m.cutout && m.src != null && m.src.StartsWith("foliage_"))
            {
                // V3 trees: leaf-cluster cards with crown normals, light through the leaves, a little sway (SC/Foliage)
                var lmat = LoadOrCreate(Gen + "/Materials/Props/" + m.name + ".mat", Shader.Find("SC/Foliage"));
                bool palm = m.src.Contains("palm");
                lmat.SetTexture("_MainTex", Tex(ArtProps, m.albedo));
                lmat.SetColor("_Color", Color.white);
                lmat.SetFloat("_Cutoff", 0.42f);
                lmat.SetFloat("_Glossiness", 0.16f);
                lmat.SetFloat("_Translucency", palm ? 0.55f : 0.8f);
                lmat.SetFloat("_DeepShade", palm ? 0.2f : 0.55f);
                lmat.SetFloat("_Sway", palm ? 0.16f : 0.07f);
                lmat.enableInstancing = true;
                EditorUtility.SetDirty(lmat);
                return lmat;
            }
            bool glass = IsGlass(m);
            bool seeThrough = glass && (Starts(id, TransparentGlass) || (m.src != null && m.src.StartsWith("glass_")));
            if (m.window)
            {
                // lit interiors behind glass (parallax room, reflections, lights at night: global _SC_Night)
                var wmat = LoadOrCreate(Gen + "/Materials/Props/" + m.name + ".mat", Shader.Find("SC/Window"));
                wmat.SetTexture("_MainTex", Tex(ArtProps, m.albedo));
                wmat.SetFloat("_Depth", m.src != null && m.src.Contains("shop") ? 0.9f : 1.6f);
                // V3: office towers have coated, mirror-like glass that catches the sun; homes, hotels, shops plain glass
                bool coated = m.src != null && (m.src.Contains("office") || m.src.Contains("tower"));
                wmat.SetFloat("_F0", coated ? 0.24f : 0.08f);
                wmat.SetFloat("_Glint", coated ? 28f : 6f);
                // by day a room behind clear glass is dim, not black (shops are lit inside)
                wmat.SetFloat("_DayLight", m.src != null && m.src.Contains("shop") ? 0.42f : (coated ? 0.18f : 0.27f));
                wmat.enableInstancing = true;
                EditorUtility.SetDirty(wmat);
                return wmat;
            }
            // V3 city: the walls of the procedural buildings weather (SC/Facade: dirt at the foot, rain streaks, drift)
            bool facade = !m.cutout && !glass && (m.emission == null || m.emission.Length < 3) && m.src != null && Starts(m.src, FacadePrefixes);
            var fsh = facade ? Shader.Find("SC/Facade") : null;
            if (fsh == null || ShaderUtil.ShaderHasError(fsh)) facade = false;
            var sh = m.cutout ? Shader.Find("SC/CharacterCutout") : (facade ? fsh : Shader.Find("Standard"));
            var mat = LoadOrCreate(Gen + "/Materials/Props/" + m.name + ".mat", sh);
            if (facade)
            {
                bool trimLike = m.src.Contains("trim") || m.src.Contains("marble");
                mat.SetFloat("_Grime", trimLike ? 0.12f : 0.32f);
                mat.SetFloat("_Streaks", trimLike ? 0.08f : 0.24f);
                mat.SetFloat("_Drift", trimLike ? 0.05f : 0.11f);
            }
            var col = ToColor(m.color);
            var albedo = Tex(ArtProps, m.albedo);
            var normal = Tex(ArtProps, m.normal);
            var ms = Tex(ArtProps, m.ms);
            if (m.cutout)
            {
                mat.SetColor("_Color", col);
                mat.SetTexture("_MainTex", albedo);
                mat.SetFloat("_Cutoff", 0.45f);
                mat.SetFloat("_Glossiness", Mathf.Clamp(m.smoothness, 0.05f, 0.6f));
            }
            else
            {
                SetupStandardOpaque(mat);
                mat.SetColor("_Color", col);
                mat.SetTexture("_MainTex", albedo);
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.SetFloat("_BumpScale", 1f);
                    mat.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    mat.SetTexture("_BumpMap", null);
                    mat.DisableKeyword("_NORMALMAP");
                }
                if (ms != null)
                {
                    mat.SetTexture("_MetallicGlossMap", ms);
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                    mat.SetFloat("_GlossMapScale", Mathf.Clamp01(m.smoothness <= 0 ? 1f : m.smoothness));
                    mat.SetFloat("_SmoothnessTextureChannel", 0f);
                    // the G channel of the source packs is not a reliable occlusion map (some are black / roughness) -> no AO
                    mat.SetTexture("_OcclusionMap", null);
                }
                else
                {
                    mat.SetTexture("_MetallicGlossMap", null);
                    mat.DisableKeyword("_METALLICGLOSSMAP");
                    mat.SetTexture("_OcclusionMap", null);
                    mat.SetFloat("_Metallic", Mathf.Clamp01(m.metallic));
                    mat.SetFloat("_Glossiness", Mathf.Clamp01(m.smoothness));
                }
                if (glass)
                {
                    mat.SetFloat("_Metallic", seeThrough ? 0.1f : 0.85f);
                    mat.SetFloat("_Glossiness", 0.95f);
                    if (seeThrough)
                    {
                        col.a = 0.14f;      // V3: a lighter film (the room behind it is what the player looks for)
                        mat.SetColor("_Color", col);
                        SetupStandardTransparent(mat);
                    }
                }
                mat.SetFloat("_SpecularHighlights", 1f);
                // V3 (device test: "behind which window is she?"): clear glass people stand behind (office storey,
                // penthouse, villa, booths) mirrored the night sky at the angle the nest sees it and read as a grey band;
                // no environment reflection on it any more, only the lights' highlights — the lit room shows through
                bool clearGlass = glass && seeThrough;
                mat.SetFloat("_GlossyReflections", clearGlass ? 0f : 1f);
                if (clearGlass) mat.EnableKeyword("_GLOSSYREFLECTIONS_OFF"); else mat.DisableKeyword("_GLOSSYREFLECTIONS_OFF");
                if (m.emission != null && m.emission.Length >= 3)
                {
                    // night lights: the level switches the emission off by day (Level.SetNight)
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(m.emission[0], m.emission[1], m.emission[2], 1f));
                    mat.SetTexture("_EmissionMap", albedo);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.SetTexture("_EmissionMap", null);
                }
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static List<Renderer> Lod0Renderers(GameObject go)
        {
            var lg = go.GetComponentInChildren<LODGroup>();
            if (lg != null)
            {
                var lods = lg.GetLODs();
                if (lods.Length > 0) return lods[0].renderers.Where(r => r != null).ToList();
            }
            return go.GetComponentsInChildren<Renderer>(true).ToList();
        }

        static Bounds LocalBounds(Transform root, IEnumerable<Renderer> rs)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                Mesh mesh = mf != null ? mf.sharedMesh : (r is SkinnedMeshRenderer smr ? smr.sharedMesh : null);
                if (mesh == null) continue;
                var mb = mesh.bounds;
                var m = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Copy of a mesh without the sub-meshes that bullets should pass through (glass, cut-out cards).</summary>
        static Mesh CollisionMesh(string id, Mesh src, bool[] keep)
        {
            string path = Gen + "/Meshes/" + id + "__" + src.name.Replace(':', '_') + "_col.asset";
            var verts = src.vertices;
            var tris = new List<int>();
            for (int s = 0; s < src.subMeshCount && s < keep.Length; s++)
                if (keep[s]) tris.AddRange(src.GetTriangles(s));
            if (tris.Count == 0) return null;
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = m == null;
            if (isNew) m = new Mesh();
            m.Clear();
            m.name = id + "_col";
            m.indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.vertices = verts;
            m.triangles = tris.ToArray();
            m.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ props
        static bool BuildProp(string jsonPath)
        {
            var meta = JsonUtility.FromJson<PropMeta>(File.ReadAllText(jsonPath));
            if (meta == null || string.IsNullOrEmpty(meta.id)) return false;
            string fbx = ArtProps + "/" + meta.id + ".fbx";
            // the import rules changed for this prop (v8: moving hulls are readable): import it again first
            if (AssetImporter.GetAtPath(fbx) is ModelImporter mimp && mimp.isReadable != NeedsReadableMesh(meta.id))
            {
                mimp.isReadable = NeedsReadableMesh(meta.id);
                mimp.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) { Debug.LogWarning("[SC] missing model " + fbx); return false; }

            var mats = new Dictionary<string, Material>();
            var ordered = new List<Material>();
            Material terrain = IsTerrain(meta.id) ? TerrainMaterial(meta) : null;
            foreach (var pm in meta.materials ?? new PropMatMeta[0])
            {
                var mat = terrain != null ? terrain : PropMaterial(meta.id, pm);
                mats[pm.name] = mat;
                ordered.Add(mat);
            }

            var root = new GameObject(meta.id);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);

            bool far = meta.category == "city" || meta.id.StartsWith("ph_cliff") || meta.id.StartsWith("sc_mtn_forest") || meta.id == "sc_mtn_far";
            bool forest = meta.id.StartsWith("sc_mtn_forest");
            bool smallProp = meta.size != null && meta.size.Length == 3 && Mathf.Max(meta.size[0], Mathf.Max(meta.size[1], meta.size[2])) < 0.6f;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var sm = r.sharedMaterials;
                for (int i = 0; i < sm.Length; i++)
                {
                    string n = sm[i] != null ? sm[i].name : "";
                    if (mats.TryGetValue(n, out var m)) sm[i] = m;
                    else
                    {
                        // fall back to "<id>__m<i>" naming or slot order
                        var hit = mats.Keys.FirstOrDefault(k => n.StartsWith(k) || k.StartsWith(n));
                        sm[i] = hit != null ? mats[hit] : (i < ordered.Count ? ordered[i] : (ordered.Count > 0 ? ordered[0] : sm[i]));
                    }
                }
                r.sharedMaterials = sm;
                r.shadowCastingMode = far || smallProp ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.receiveShadows = !far || forest || meta.id == "sc_mtn_far";      // the woods and the far slopes do get the mountains' shadows
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                r.allowOcclusionWhenDynamic = true;
            }

            var lg = inst.GetComponentInChildren<LODGroup>();
            if (lg != null)
            {
                var lods = lg.GetLODs();
                float[] h = lods.Length <= 2 ? new[] { 0.05f, 0.0025f } : new[] { 0.1f, 0.03f, 0.0025f };
                for (int i = 0; i < lods.Length; i++) lods[i].screenRelativeTransitionHeight = h[Mathf.Min(i, h.Length - 1)];
                lg.SetLODs(lods);
                lg.fadeMode = LODFadeMode.None;
                lg.RecalculateBounds();
            }

            string rule = ColliderRule(meta);
            var lod0 = Lod0Renderers(inst);
            if (rule == "box")
            {
                var b = LocalBounds(root.transform, lod0);
                if (b.size.sqrMagnitude > 0.0001f)
                {
                    var bc = root.AddComponent<BoxCollider>();
                    bc.center = b.center;
                    bc.size = Vector3.Max(b.size, new Vector3(0.05f, 0.05f, 0.05f));
                }
            }
            else if (rule == "mesh")
            {
                foreach (var r in lod0)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mesh = mf.sharedMesh;
                    var rm = r.sharedMaterials;
                    var keep = new bool[mesh.subMeshCount];
                    bool filtered = false;
                    for (int s = 0; s < keep.Length; s++)
                    {
                        var mat = s < rm.Length ? rm[s] : null;
                        var pm = meta.materials != null ? meta.materials.FirstOrDefault(x => mat != null && x.name == mat.name) : null;
                        bool skip = pm != null && (IsGlass(pm) || pm.cutout);
                        keep[s] = !skip;
                        filtered |= skip;
                    }
                    Mesh col = mesh;
                    if (filtered)
                    {
                        if (mesh.isReadable) col = CollisionMesh(meta.id, mesh, keep);
                        else Debug.LogWarning("[SC] " + meta.id + ": mesh not readable, glass keeps blocking shots");
                    }
                    if (col == null) continue;
                    var mc = r.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = col;
                    mc.convex = false;
                    // default cooking only: anything else needs a CPU-readable mesh at runtime, and most props are not
                    // readable ("Mesh Collider ... doesn't have Read/Write enabled" on lamps, parasols, the tugboat)
                }
            }

            var info = root.AddComponent<PropInfo>();
            info.id = meta.id;
            info.category = meta.category;
            info.size = meta.size != null && meta.size.Length == 3 ? new Vector3(meta.size[0], meta.size[1], meta.size[2]) : Vector3.one;
            info.credit = meta.credit;

            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            PrefabUtility.SaveAsPrefabAsset(root, ResSC + "/Props/" + meta.id + ".prefab");
            Object.DestroyImmediate(root);
            return true;
        }

        // ------------------------------------------------------------------ characters
        static bool BuildChar(string jsonPath)
        {
            var meta = JsonUtility.FromJson<CharMeta>(File.ReadAllText(jsonPath));
            if (meta == null || string.IsNullOrEmpty(meta.id)) return false;
            string fbx = ArtChars + "/" + meta.id + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) { Debug.LogWarning("[SC] missing character " + fbx); return false; }

            var mats = new Dictionary<string, Material>();
            foreach (var cm in meta.materials ?? new CharMatMeta[0])
            {
                Material mat;
                if (cm.cutout)
                {
                    mat = LoadOrCreate(Gen + "/Materials/Chars/" + cm.name + ".mat", Shader.Find("SC/CharacterCutout"));
                    mat.SetTexture("_MainTex", Tex(ArtChars, cm.albedo));
                    mat.SetColor("_Color", Color.white);
                    mat.SetFloat("_Cutoff", 0.42f);
                    mat.SetFloat("_Glossiness", 0.3f);
                }
                else
                {
                    mat = LoadOrCreate(Gen + "/Materials/Chars/" + cm.name + ".mat", Shader.Find("Standard"));
                    SetupStandardOpaque(mat);
                    mat.SetColor("_Color", Color.white);
                    mat.SetTexture("_MainTex", Tex(ArtChars, cm.albedo));
                    var n = Tex(ArtChars, cm.normal);
                    mat.SetTexture("_BumpMap", n);
                    if (n != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
                    mat.SetFloat("_BumpScale", 1f);
                    // Meshy characters bring a metallic / smoothness map (leather, buckles and wet eyes shine, cloth does not)
                    var ms = string.IsNullOrEmpty(cm.ms) ? null : Tex(ArtChars, cm.ms);
                    mat.SetTexture("_MetallicGlossMap", ms);
                    if (ms != null)
                    {
                        mat.EnableKeyword("_METALLICGLOSSMAP");
                        mat.SetFloat("_GlossMapScale", 1f);
                        mat.SetFloat("_SmoothnessTextureChannel", 0f);
                    }
                    else
                    {
                        mat.DisableKeyword("_METALLICGLOSSMAP");
                        mat.SetFloat("_Metallic", 0f);
                        mat.SetFloat("_Glossiness", 0.28f);
                    }
                }
                mat.enableInstancing = false;
                EditorUtility.SetDirty(mat);
                mats[cm.name] = mat;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = meta.id;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var sm = r.sharedMaterials;
                for (int i = 0; i < sm.Length; i++)
                {
                    string n = sm[i] != null ? sm[i].name : "";
                    if (mats.TryGetValue(n, out var m)) sm[i] = m;
                    else if (n.Contains("cutout") && mats.Values.Any(x => x.name.EndsWith("_cutout"))) sm[i] = mats.Values.First(x => x.name.EndsWith("_cutout"));
                    else if (mats.Values.Any(x => x.name.EndsWith("_opaque"))) sm[i] = mats.Values.First(x => x.name.EndsWith("_opaque"));
                }
                r.sharedMaterials = sm;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off;
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = false;
                    smr.quality = SkinQuality.Bone4;
                    smr.skinnedMotionVectors = false;
                }
            }
            // V3: distance LOD (tools/bl/char_lod.py adds "Body_LOD1", ~3.5k triangles): full detail close up and in the
            // scope (its narrow view makes people big on screen), the light mesh for the far crowd
            var all = inst.GetComponentsInChildren<Renderer>(true);
            var lod1 = all.Where(r => r.name.EndsWith("_LOD1")).ToArray();
            var old = inst.GetComponent<LODGroup>();
            if (lod1.Length > 0)
            {
                var lod0 = all.Where(r => !r.name.EndsWith("_LOD1")).ToArray();
                var lg = old != null ? old : inst.AddComponent<LODGroup>();
                lg.fadeMode = LODFadeMode.None;
                lg.SetLODs(new[] { new LOD(0.075f, lod0), new LOD(0.0008f, lod1) });
                lg.RecalculateBounds();
            }
            else if (old != null) Object.DestroyImmediate(old);
            var anim = inst.GetComponent<Animation>();
            if (anim == null) anim = inst.AddComponent<Animation>();
            anim.playAutomatically = false;
            anim.cullingType = AnimationCullingType.BasedOnRenderers;
            anim.clip = null;
            var ci = inst.GetComponent<CharInfo>();
            if (ci == null) ci = inst.AddComponent<CharInfo>();
            ci.id = meta.id;
            ci.gender = string.IsNullOrEmpty(meta.gender) ? "m" : meta.gender;
            ci.height = meta.height > 1f ? meta.height : 1.78f;
            PrefabUtility.SaveAsPrefabAsset(inst, ResSC + "/Chars/" + meta.id + ".prefab");
            Object.DestroyImmediate(inst);
            return true;
        }

        // ------------------------------------------------------------------ animation bank
        static void BuildAnimBank()
        {
            string path = ResSC + "/AnimBank.asset";
            var bank = AssetDatabase.LoadAssetAtPath<AnimBank>(path);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<AnimBank>();
                AssetDatabase.CreateAsset(bank, path);
            }
            bank.entries = new List<AnimEntry>();
            foreach (var g in new[] { "m", "f" })
            {
                string dir = ArtAnims + "/" + g;
                if (!Directory.Exists(dir)) continue;
                foreach (var json in Directory.GetFiles(dir, "*.json"))
                {
                    string src = Path.GetFileNameWithoutExtension(json);
                    if (!AnimMap.TryGetValue(src, out var dst)) continue;
                    var meta = JsonUtility.FromJson<AnimMeta>(File.ReadAllText(json));
                    string fbx = dir + "/" + src + ".fbx";
                    var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                    if (clip == null) { Debug.LogWarning("[SC] no clip in " + fbx); continue; }
                    var off = meta.offset != null && meta.offset.Length >= 2 ? new Vector3(meta.offset[0], 0f, meta.offset[1]) : Vector3.zero;
                    var e = new AnimEntry { name = dst, gender = g, clip = clip, loop = meta.loop, speed = meta.speed, length = clip.length, tags = meta.tags, offset = off };
                    bank.entries.Add(e);
                    if (AnimAlias.TryGetValue(src, out var alias))
                        bank.entries.Add(new AnimEntry { name = alias, gender = g, clip = clip, loop = meta.loop, speed = meta.speed, length = clip.length, tags = meta.tags, offset = off });
                }
            }
            EditorUtility.SetDirty(bank);
            Debug.Log("[SC] animation bank: " + bank.entries.Count + " clips");
        }

        // ------------------------------------------------------------------ skies + shared materials
        static void BuildSkies()
        {
            if (!Directory.Exists(ArtSky)) return;
            var sh = Shader.Find("Skybox/Panoramic");
            foreach (var f in Directory.GetFiles(ArtSky, "sky_*.jpg"))
            {
                string name = Path.GetFileNameWithoutExtension(f);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace('\\', '/'));
                var mat = LoadOrCreate(ResSC + "/Sky/" + name + ".mat", sh);
                mat.SetTexture("_MainTex", tex);
                mat.SetFloat("_Mapping", 1f);
                mat.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
                mat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
                mat.SetFloat("_ImageType", 0f);
                mat.SetFloat("_MirrorOnBack", 0f);
                mat.SetFloat("_Layout", 0f);
                mat.SetFloat("_Exposure", 1f);
                mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                mat.SetFloat("_Rotation", 0f);
                EditorUtility.SetDirty(mat);
            }
        }

        static void BuildMisc()
        {
            var bullet = LoadOrCreate(ResSC + "/Materials/bullet.mat", Shader.Find("Standard"));
            SetupStandardOpaque(bullet);
            bullet.SetColor("_Color", new Color(0.80f, 0.5f, 0.3f));      // copper jacket
            bullet.SetFloat("_Metallic", 1f);
            bullet.SetFloat("_Glossiness", 0.82f);
            EditorUtility.SetDirty(bullet);
        }
    }
}

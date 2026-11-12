using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilentContract
{
    /// <summary>
    /// Entry point (the only object in the scene). Builds the camera, audio, UI and the harbour once, then moves
    /// between the menu (live harbour background) and contracts.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I;
        /// <summary>The store version (bundle version, "1.1"): the menu shows the same number as the App Store.</summary>
        public static string Version => Application.version;
        /// <summary>Test build: every map and contract starts open (the long press on the version line can still close it).
        /// Set to false for the store build.</summary>
        public const bool TestBuild = false;
        /// <summary>Video / demo build (App Review screen recording): every map and contract open, everything else exactly
        /// as in the store build (real purchases, real ads, no test label). false for the store build.</summary>
        public const bool UnlockAllContracts = false;

        public Camera Cam { get; private set; }
        public Sniper Sniper { get; private set; }
        public KillCam KillCam { get; private set; }
        public Cinematic Cinematic { get; private set; }
        public PostFX Post { get; private set; }
        public Level Level { get; private set; }
        public Env Env { get; private set; }
        public MissionRuntime Runtime { get; private set; }
        public UIRoot UI { get; private set; }
        public bool Ready { get; private set; }
        public bool InMenu { get; private set; }
        public bool Paused { get; private set; }
        public MissionDef Current { get; private set; }
        /// <summary>The map that is built right now (harbour, city centre ...).</summary>
        public string LocationId { get; private set; } = "harbor";
        public LocationDef Location => Locations.Get(LocationId);
        public bool Switching { get; private set; }

        float menuT, bootStart;
        string menuPreset => Location.menuPreset;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            bootStart = Time.realtimeSinceStartup;
            // analytics and crash reports first: a crash during loading is reported too
            try { Analytics.Init(); } catch (System.Exception e) { Debug.LogException(e); }
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = true;
            AudioListener.volume = Save.Volume;
            BuildCamera();
            ApplyQuality(Save.Quality);
            Sfx.Init(gameObject);
            Debug.Log("[SC] Dil: " + Loc.Lang + " (cihaz: " + Application.systemLanguage + ")");
            UI = UIRoot.Create(this);
            Runtime = gameObject.AddComponent<MissionRuntime>();
            StartCoroutine(Boot());
        }

        void BuildCamera()
        {
            var rig = new GameObject("SniperRig");
            Sniper = rig.AddComponent<Sniper>();
            KillCam = rig.AddComponent<KillCam>();
            Cinematic = rig.AddComponent<Cinematic>();
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            Cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            Cam.cullingMask = ~((1 << Layers.UI) | (1 << Layers.ViewModel));
            Cam.allowHDR = true;
            Cam.clearFlags = CameraClearFlags.Skybox;
            Cam.backgroundColor = Color.black;
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 6000f;
            Cam.fieldOfView = 55f;
            Cam.depthTextureMode = DepthTextureMode.None;
            Post = camGo.AddComponent<PostFX>();
            camGo.AddComponent<VisionFX>();
            KillCam.Init(Cam);
            Cinematic.Init(Cam, Sniper);
        }

        IEnumerator Boot()
        {
            var start = Locations.Get(Save.Location);
            if (!Locations.IsOpen(start)) start = Locations.All[0];
            LocationId = start.id;
            UI.ShowLoading();
            UI.Loading.SetLocation(start);
            UI.SetLoading(0.05f, "Başlatılıyor");
            // the store (RevenueCat) and the ads (consent questions, then AdMob) start while the map loads
            try { Store.Init(); } catch (System.Exception e) { Analytics.Exception(e); }
            try { Ads.Init(); } catch (System.Exception e) { Analytics.Exception(e); }
            StartCoroutine(Privacy.AskAtLaunch());              // Apple's tracking question, before Meta starts
            yield return null;
            var lvGo = new GameObject("Level");
            Level = lvGo.AddComponent<Level>();
            yield return Level.Build(start.layout, (p, msg) => UI.SetLoading(p, msg), start.loadingText);
            var envGo = new GameObject("Env");
            Env = envGo.AddComponent<Env>();
            Env.Init(Cam.transform);
            Sniper.Init(Cam, Level.Data.player);
            Runtime.Init(this);
            ApplyQuality(Save.Quality);
            Env.Apply(menuPreset, Cam, Post);
            UI.SetLoading(0.95f, "Hazırlanıyor");
            // warm up the characters and their shaders behind the loading screen (no hitch on the first mission)
            var warm = NPC.Spawn("guard_a", Level.PlayerPosition + new Vector3(0, -2f, 6f), 180, null);
            FX.Warm();                                   // blood textures and materials: no hitch at the first kill
            yield return null;
            yield return null;
            if (warm != null) { NPC.All.Remove(warm); Destroy(warm.gameObject); }
            UI.SetLoading(1f, "Hazır");
            yield return new WaitForSecondsRealtime(0.25f);
            Ready = true;
            try
            {
                Analytics.RefreshUser();
                Analytics.SetUser("language", Loc.Lang);
                int launches = PlayerPrefs.GetInt("sc2_launches", 0) + 1;
                PlayerPrefs.SetInt("sc2_launches", launches);
                Analytics.Event("app_boot", "load_ms", Mathf.RoundToInt((Time.realtimeSinceStartup - bootStart) * 1000f), "launch", launches,
                                "language", Loc.Lang, "system_language", Application.systemLanguage.ToString(), "memory_mb", SystemInfo.systemMemorySize, "device", SystemInfo.deviceModel);
            }
            catch (System.Exception e) { Debug.LogException(e); }
            GoMenu(true);
#if UNITY_EDITOR
            if (PreviewCapture.Requested) { gameObject.AddComponent<PreviewCapture>(); yield break; }
#endif
#if SC_AUTOTEST
            gameObject.AddComponent<AutoTest>();
#else
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest") >= 0) gameObject.AddComponent<AutoTest>();
#endif
        }

        // ------------------------------------------------------------------ flow
        public void GoMenu(bool first = false)
        {
            // a contract left from the pause menu (the quit point of the difficulty report)
            if (Runtime != null && Runtime.Def != null && (Runtime.State == MissionRuntime.Phase.Playing || Runtime.State == MissionRuntime.Phase.Intro))
                Analytics.Event("contract_quit", "contract_id", Runtime.Def.id, "contract_no", Runtime.Def.number,
                                "time_s", Mathf.RoundToInt(Runtime.Elapsed), "shots", Runtime.Shots);
            SetPaused(false);
            Runtime.Abort();
            InMenu = true;
            Current = null;
            Sniper.InputEnabled = false;
            Sniper.SetScoped(false);
            Sniper.ShowRifle = false;
            if (Level != null && Level.Data != null) Sniper.Place(Level.Data.player);     // back on the main roof behind the menu
            Env.Apply(menuPreset, Cam, Post);
            CalmMenuWater();
            Sfx.SetRain(false);
            menuT = 0f;
            UI.ShowMenu(first);
        }

        public void StartMission(MissionDef def)
        {
            if (def == null || Switching) return;
            StartCoroutine(StartRoutine(def));
        }

        /// <summary>
        /// Tears the current map down and builds another one behind the loading screen (contracts on another map, the
        /// map picker in the contract list). The nest, water reflection and environment capture follow the new layout.
        /// </summary>
        public IEnumerator SwitchLocation(string id, bool backToMenu)
        {
            var loc = Locations.Get(id);
            if (loc.id == LocationId || Switching) yield break;
            Switching = true;
            SetPaused(false);
            Runtime.Abort();
            Sniper.InputEnabled = false;
            Sniper.SetScoped(false);
            UI.Loading.SetLocation(loc);
            UI.ShowLoading();
            UI.SetLoading(0.02f, loc.loadingText);
            yield return null;
            if (Level != null) Destroy(Level.gameObject);
            Level = null;
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            var lvGo = new GameObject("Level");
            Level = lvGo.AddComponent<Level>();
            yield return Level.Build(loc.layout, (p, msg) => UI.SetLoading(p, msg), loc.loadingText);
            LocationId = loc.id;
            Save.Location = loc.id;
            Sniper.Place(Level.Data.player);
            ApplyQuality(Save.Quality);
            Env.Apply(loc.menuPreset, Cam, Post);
            UI.SetLoading(1f, "Hazır");
            yield return new WaitForSecondsRealtime(0.2f);
            Switching = false;
            if (backToMenu) GoMenu(true);
        }

        IEnumerator StartRoutine(MissionDef def)
        {
            if (def.region != LocationId) yield return SwitchLocation(def.region, false);
            UI.Blackout(true, 0.25f);
            yield return new WaitForSecondsRealtime(0.3f);
            SetPaused(false);
            InMenu = false;
            Current = def;
            Runtime.Begin(def);
            Sniper.ShowRifle = true;
            Sniper.InputEnabled = true;
            UI.ShowHUD(def);
            if (UI.Loading.Visible) UI.Loading.Hide(0.3f);
            yield return null;
            UI.Blackout(false, 0.45f);
            // the frozen surveillance intro (skippable); the contract clock starts when it ends
            if (Runtime.State == MissionRuntime.Phase.Intro)
            {
                Sniper.InputEnabled = false;
                Cinematic.Play(def, Runtime, () =>
                {
                    Runtime.EndIntro();
                    if (Current == def) Sniper.InputEnabled = true;
                });
            }
        }

        public void Restart() { if (Current != null) StartMission(Current); }

        public void NextMission()
        {
            var n = Current != null ? MissionDefs.Next(Current) : null;
            GoMenu();                       // close the finished mission completely (NPCs, weather, rifle, scope)
            if (n != null) UI.ShowBriefing(n);
        }

        public void SetPaused(bool on)
        {
            Paused = on;
            if (on)
            {
                Time.timeScale = 0f;
                AudioListener.pause = true;
            }
            else
            {
                if (!KillCam.Active && !Cinematic.Active) Time.timeScale = 1f;
                AudioListener.pause = false;
            }
        }

        void OnApplicationPause(bool pause)
        {
            // a full-screen ad pauses the player as well: that is not the player leaving the app
            if (!Ads.Showing)
            {
                if (pause) { Analytics.Event("app_background", "in_contract", InMenu ? 0 : 1); PlayerPrefs.Save(); }
                else { Analytics.Resumed(); Privacy.Retry(); }
            }
            if (pause && !InMenu && Ready && Runtime.State == MissionRuntime.Phase.Playing && !Paused && !KillCam.Active)
                UI.ShowPause();
        }

        void Update()
        {
            if (!Ready) return;
            PlanarReflection.FollowExactly = InMenu;
            if (InMenu && !Switching)
            {
                // the view behind the menu stands still (a moving camera made the water's reflections and waves seem to
                // slide against the shore); the life in it comes from the boats, the cranes and the small waves
                menuT += Time.unscaledDeltaTime;
                var loc = Location;
                Sniper.SetLookAngles(loc.menuYaw, loc.menuPitch);
            }
            Sfx.TickAmbience(Env != null && Env.Current != null && Env.Current.night);
        }

        /// <summary>Behind the menu the water is calmer than in a contract (a near mirror, gentle ripples): the first
        /// thing a player sees. The next Env.Apply (a contract) sets the preset's waves again.</summary>
        void CalmMenuWater()
        {
            if (Env == null || Env.WaterMat == null || Env.Current == null) return;
            Env.WaterMat.SetFloat("_NormalScale", Env.Current.waves * 0.45f);
        }

        // ------------------------------------------------------------------ quality
        public void ApplyQuality(int q)
        {
            q = Mathf.Clamp(q, 0, 2);
            QualitySettings.shadows = q == 0 ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowResolution = q == 0 ? ShadowResolution.Medium : (q == 1 ? ShadowResolution.High : ShadowResolution.VeryHigh);
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowDistance = q == 0 ? 240f : (q == 1 ? 400f : 520f);
            QualitySettings.shadowCascades = q == 0 ? 2 : 4;
            QualitySettings.shadowCascade2Split = 0.18f;
            QualitySettings.shadowCascade4Split = new Vector3(0.035f, 0.12f, 0.38f);
            QualitySettings.antiAliasing = q == 0 ? 0 : (q == 1 ? 2 : 4);
            QualitySettings.lodBias = q == 0 ? 0.9f : (q == 1 ? 1.3f : 1.8f);
            QualitySettings.pixelLightCount = q == 0 ? 2 : (q == 1 ? 3 : 5);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = true;
            QualitySettings.skinWeights = q == 0 ? SkinWeights.TwoBones : SkinWeights.FourBones;
            QualitySettings.maximumLODLevel = 0;
            if (Cam != null) Cam.allowMSAA = q > 0;
            if (Post != null) Post.bloom = q > 0;
            if (Env != null && Env.Sun != null) Env.Sun.shadows = q == 0 ? LightShadows.Hard : LightShadows.Soft;
            // water mirror: off on "performance" (environment cubemap instead), a third / half of the screen otherwise
            PlanarReflection.ResolutionScale = q == 0 ? 0f : (q == 1 ? 0.34f : 0.5f);
            PlanarReflection.Msaa = q == 2 ? 4 : 2;
            // the screen always stays at native resolution so the interface is pixel-sharp; the quality levels only
            // change shadows, anti-aliasing, lights and bloom
        }
    }
}

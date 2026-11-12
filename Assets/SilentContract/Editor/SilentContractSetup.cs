using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilentContract.EditorTools
{
    /// <summary>
    /// Project bootstrap: legacy input handling, the single game scene (one "Game" object that builds everything
    /// at runtime) and content freshness tracking for the build.
    /// </summary>
    [InitializeOnLoad]
    public static class SilentContractSetup
    {
        public const string ScenePath = "Assets/SilentContract/Scenes/Main.unity";
        const string StampPath = "Library/SC_content_v2.stamp";
        const string ArtRoot = "Assets/SilentContract/Art";

        static SilentContractSetup()
        {
            EditorApplication.delayCall += FirstRun;
        }

        static void FirstRun()
        {
            if (Application.isBatchMode) return;
            if (SessionState.GetBool("sc2_setup_done", false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool("sc2_setup_done", true);
            EnsureProjectSettings();
            if (ContentOutdated())
            {
                if (AssetPipeline.BuildAll()) MarkContentBuilt();
            }
            EnsureScene();
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != ScenePath && !active.isDirty) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Silent Contract/Sahneyi Yeniden Oluştur", priority = 20)]
        public static void RecreateScene()
        {
            CreateScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void EnsureScene()
        {
            bool ok = File.Exists(ScenePath) && File.ReadAllText(ScenePath).Contains("SC2Game");
            if (!ok) CreateScene();
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Count != 1 || list[0].path != ScenePath)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("SC2Game");
            go.AddComponent<Game>();
            // fog/ambient as used at runtime (keeps the exp² fog shader variants in the build)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0008f;
            RenderSettings.fogColor = new Color(0.66f, 0.66f, 0.7f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/SilentContract/Resources/SC/Sky/sky_industrial_sunset_02_puresky.mat");
            if (sky != null) RenderSettings.skybox = sky;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[SC] scene created: " + ScenePath);
        }

        /// <summary>Old Input Manager only (the game uses uGUI touch events + UnityEngine.Input).</summary>
        public static void EnsureProjectSettings()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var p = so.FindProperty("activeInputHandler");
            bool hasInputSystem = File.Exists("Packages/manifest.json") && File.ReadAllText("Packages/manifest.json").Contains("com.unity.inputsystem");
            // 0 = old Input Manager, 1 = new Input System only, 2 = both. The game needs the old one.
            if (p != null && p.intValue == 1)
            {
                p.intValue = hasInputSystem ? 2 : 0;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
        }

        static string Fingerprint()
        {
            if (!Directory.Exists(ArtRoot)) return "none";
            long size = 0, newest = 0;
            int count = 0;
            foreach (var f in Directory.GetFiles(ArtRoot, "*", SearchOption.AllDirectories))
            {
                if (f.EndsWith(".meta")) continue;
                var fi = new FileInfo(f);
                size += fi.Length;
                newest = System.Math.Max(newest, fi.LastWriteTimeUtc.Ticks);
                count++;
            }
            return "v12|" + count + "|" + size + "|" + newest;
        }

        public static bool ContentOutdated()
        {
            if (!Directory.Exists("Assets/SilentContract/Resources/SC/Props") ||
                Directory.GetFiles("Assets/SilentContract/Resources/SC/Props", "*.prefab").Length == 0) return true;
            if (!File.Exists("Assets/SilentContract/Resources/SC/AnimBank.asset")) return true;
            return !File.Exists(StampPath) || File.ReadAllText(StampPath) != Fingerprint();
        }

        public static void MarkContentBuilt() => File.WriteAllText(StampPath, Fingerprint());
    }
}

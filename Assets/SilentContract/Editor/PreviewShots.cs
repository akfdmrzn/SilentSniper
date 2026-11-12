using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SilentContract.EditorTools
{
    /// <summary>
    /// Preview images rendered by Unity itself (Play mode, the real game pipeline) into &lt;project&gt;/Previews.
    /// - Menu "Silent Contract/Önizleme Görüntüleri Al"
    /// - Batch: Unity -batchmode -projectPath P -executeMethod SilentContract.EditorTools.PreviewShots.RunBatch
    /// - With the editor open it also watches Previews/request.json (dropped into the project folder remotely) and
    ///   reports C# compile errors and shader errors into Previews/compile.json / shaders.json.
    /// </summary>
    [InitializeOnLoad]
    public static class PreviewShots
    {
        public const string Stamp = "2026-11-05a";
        const string Dir = "Previews";
        const string WatchFile = Dir + "/request.json";
        const string BatchKey = "SC_PreviewBatch";
        static double nextPoll;
        static readonly string[] OwnShaders = { "SC/Water", "SC/Ground", "SC/CharacterCutout", "SC/UnlitTint", "Hidden/SC/PostFX", "Hidden/SC/ReflBlur", "Hidden/SC/ThermalBody", "SC/Window", "SC/Sky", "SC/Foliage", "SC/Facade" };

        static PreviewShots()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            CompilationPipeline.assemblyCompilationFinished += OnCompiled;
            if (!Application.isBatchMode) EditorApplication.update += Poll;
        }

        [MenuItem("Silent Contract/Önizleme Görüntüleri Al (Previews klasörü)", priority = 10)]
        public static void RunMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Start(null);
        }

        /// <summary>-executeMethod entry point (batch mode): renders Previews/request.json or the default set, then quits.</summary>
        public static void RunBatch()
        {
            SessionState.SetBool(BatchKey, true);
            string req = File.Exists(WatchFile) ? File.ReadAllText(WatchFile) : null;
            if (!Start(req)) EditorApplication.Exit(2);
        }

        static bool Start(string requestJson)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                foreach (var f in Directory.GetFiles(Dir, "*.png")) File.Delete(f);
                if (File.Exists(Dir + "/result.json")) File.Delete(Dir + "/result.json");
                WriteShaderReport();
                if (SilentContractSetup.ContentOutdated())
                {
                    if (AssetPipeline.BuildAll()) SilentContractSetup.MarkContentBuilt();
                }
                SilentContractSetup.EnsureScene();
                if (EditorSceneManager.GetActiveScene().path != SilentContractSetup.ScenePath)
                {
                    if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                    EditorSceneManager.OpenScene(SilentContractSetup.ScenePath);
                }
                File.WriteAllText(PreviewCapture.RequestFile, string.IsNullOrEmpty(requestJson) ? JsonUtility.ToJson(PreviewCapture.DefaultRequest()) : requestJson);
                if (File.Exists(WatchFile)) File.Delete(WatchFile);
                Debug.Log("[SC] preview: entering play mode");
                EditorApplication.EnterPlaymode();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                File.WriteAllText(Dir + "/result.json", "{\"ok\":false,\"error\":" + JsonUtility.ToJson(new Msg { m = e.ToString() }) + "}");
                return false;
            }
        }

        [Serializable] class Msg { public string m; }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredEditMode) return;
            if (File.Exists(PreviewCapture.RequestFile)) File.Delete(PreviewCapture.RequestFile);   // capture never started
            if (SessionState.GetBool(BatchKey, false))
            {
                SessionState.EraseBool(BatchKey);
                EditorApplication.Exit(File.Exists(Dir + "/result.json") ? 0 : 3);
            }
        }

        // open editor: pick up requests written into the shared project folder
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 2.0;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists(WatchFile)) return;
            string json;
            try { json = File.ReadAllText(WatchFile); } catch { return; }
            var req = JsonUtility.FromJson<PreviewCapture.Request>(json);
            if (req != null && !string.IsNullOrEmpty(req.stamp) && req.stamp != Stamp)
            {
                // new code was copied in: import it first (the request stays until the new code runs)
                string key = "SC_PreviewRefresh_" + req.id;
                if (SessionState.GetInt(key, 0) < 3)
                {
                    SessionState.SetInt(key, SessionState.GetInt(key, 0) + 1);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return;
                }
            }
            Start(json);
        }

        [Serializable] class CompileReport { public string stamp, time; public List<string> errors = new List<string>(); }

        static void OnCompiled(string assembly, CompilerMessage[] messages)
        {
            try
            {
                var errs = messages.Where(m => m.type == CompilerMessageType.Error).Select(m => m.file + "(" + m.line + "): " + m.message).ToList();
                Directory.CreateDirectory(Dir);
                var path = Dir + "/compile_" + Path.GetFileNameWithoutExtension(assembly) + ".json";
                var rep = new CompileReport { stamp = Stamp, time = DateTime.Now.ToString("s"), errors = errs };
                File.WriteAllText(path, JsonUtility.ToJson(rep, true));
            }
            catch { }
        }

        [Serializable] class ShaderReport { public string time; public List<string> shaders = new List<string>(); }

        static void WriteShaderReport()
        {
            var rep = new ShaderReport { time = DateTime.Now.ToString("s") };
            foreach (var name in OwnShaders)
            {
                var sh = Shader.Find(name);
                if (sh == null) { rep.shaders.Add(name + ": MISSING"); continue; }
                var msgs = ShaderUtil.GetShaderMessages(sh);
                bool err = ShaderUtil.ShaderHasError(sh);
                rep.shaders.Add(name + ": " + (err ? "ERROR" : "ok") + (msgs != null && msgs.Length > 0
                    ? " | " + string.Join(" | ", msgs.Select(m => m.severity + " line " + m.line + ": " + m.message)) : ""));
            }
            File.WriteAllText(Dir + "/shaders.json", JsonUtility.ToJson(rep, true));
        }
    }
}

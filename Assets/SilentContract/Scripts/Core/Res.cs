using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>Access to the content baked by the editor pipeline into Resources/SC.</summary>
    public static class Res
    {
        static readonly Dictionary<string, Object> cache = new Dictionary<string, Object>();
        static AnimBank anims;

        static T Load<T>(string path) where T : Object
        {
            if (cache.TryGetValue(path, out var o)) return o as T;
            var r = Resources.Load<T>(path);
            cache[path] = r;
            if (r == null) Debug.LogWarning("[SC] missing resource " + path);
            return r;
        }

        public static GameObject Prop(string id) => Load<GameObject>("SC/Props/" + id);
        public static GameObject Character(string id) => Load<GameObject>("SC/Chars/" + id);
        // per-name caches: the HUD asks for sprites every frame, so no path strings are built on a hit
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        public static Sprite Sprite(string name)
        {
            if (name == null) return null;
            if (sprites.TryGetValue(name, out var s)) return s;
            return sprites[name] = Load<Sprite>("SC/UI/" + name);
        }

        public static Sprite Icon(string name)
        {
            if (name == null) return null;
            if (icons.TryGetValue(name, out var s)) return s;
            return icons[name] = Load<Sprite>("SC/UI/icons/ic_" + name);
        }
        public static Sprite Portrait(string id) => Load<Sprite>("SC/Portraits/" + id + "_portrait");
        public static Sprite FullBody(string id) => Load<Sprite>("SC/Portraits/" + id + "_full");
        public static Sprite Art(string name) => Load<Sprite>("SC/Art/" + name);
        /// <summary>V2 tactical map of a contract (overhead render with the nests and the target area drawn on it).</summary>
        public static Sprite Map(string missionId) => Load<Sprite>("SC/Maps/" + missionId);
        public static Material Material(string name) => Load<Material>("SC/Materials/" + name);
        public static Material Sky(string name) => Load<Material>("SC/Sky/" + name);
        public static TextAsset Text(string path) => Load<TextAsset>("SC/" + path);
        public static Shader Shader(string name) => UnityEngine.Shader.Find(name);

        public static AnimBank Anims
        {
            get
            {
                if (anims == null) anims = Resources.Load<AnimBank>("SC/AnimBank");
                return anims;
            }
        }

        static Font fBold, fBody, fMono, fSemi, fBodyBold;
        public static Font FontBold => fBold != null ? fBold : (fBold = LoadFont("BarlowCondensed-700"));
        public static Font FontSemi => fSemi != null ? fSemi : (fSemi = LoadFont("BarlowCondensed-600"));
        public static Font FontBody => fBody != null ? fBody : (fBody = LoadFont("Barlow-500"));
        public static Font FontBodyBold => fBodyBold != null ? fBodyBold : (fBodyBold = LoadFont("Barlow-700"));
        public static Font FontMono => fMono != null ? fMono : (fMono = LoadFont("ShareTechMono-400"));

        static Font LoadFont(string name)
        {
            var f = Load<Font>("SC/Fonts/" + name);
            if (f == null) f = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Helvetica", "Arial" }, 32);
            else Fallbacks(f);
            return f;
        }

        static readonly System.Collections.Generic.List<Font> loaded = new System.Collections.Generic.List<Font>();

        /// <summary>
        /// The game's fonts have Latin (Turkish, Vietnamese, Polish …) but no Cyrillic, Japanese, Korean or Chinese letters:
        /// those come from the phone's own fonts. The order follows the language (the same Han character looks different in
        /// Japanese and Chinese fonts).
        /// </summary>
        static void Fallbacks(Font f)
        {
            if (!loaded.Contains(f)) loaded.Add(f);
            var names = new System.Collections.Generic.List<string>();
            if (f.fontNames != null) foreach (var n in f.fontNames) if (!string.IsNullOrEmpty(n) && !names.Contains(n)) { names.Add(n); break; }
            string[] cjk;
            switch (Loc.Lang)
            {
                case "ja": cjk = new[] { "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP", "PingFang SC" }; break;
                case "ko": cjk = new[] { "Apple SD Gothic Neo", "Noto Sans CJK KR", "PingFang SC" }; break;
                case "zh-Hant": cjk = new[] { "PingFang TC", "Noto Sans CJK TC", "PingFang SC" }; break;
                default: cjk = new[] { "PingFang SC", "Noto Sans CJK SC", "Hiragino Sans", "Apple SD Gothic Neo" }; break;
            }
            foreach (var n in new[] { "Helvetica Neue", "Roboto", "Arial" }) if (!names.Contains(n)) names.Add(n);
            foreach (var n in cjk) if (!names.Contains(n)) names.Add(n);
            foreach (var n in new[] { "Arial Unicode MS", "Droid Sans Fallback", "Noto Sans" }) if (!names.Contains(n)) names.Add(n);
            try { f.fontNames = names.ToArray(); } catch (System.Exception e) { Debug.LogWarning("[SC] Yazı tipi yedekleri: " + e.Message); }
        }

        /// <summary>After a language change (the fallback order for Han characters).</summary>
        public static void RefreshFallbacks()
        {
            foreach (var f in loaded) if (f != null) Fallbacks(f);
        }

        public static GameObject Instantiate(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
        {
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, pos, rot, parent);
            go.name = prefab.name;
            return go;
        }
    }
}

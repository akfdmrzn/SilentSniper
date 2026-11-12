using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>
    /// Languages. The game is written in Turkish; every text on screen passes through <see cref="Tr"/> on its way into a
    /// <see cref="LocText"/>, which looks it up in the table of the chosen language (Resources/SC/Loc/&lt;code&gt;.txt):
    /// first the whole text, then the patterns texts are built from in code ("{0} saniye" …, the parts in the holes
    /// are translated too), then line by line and around " · " separators. Anything without a translation stays as it is.
    /// The first launch picks the phone's language when the game has it, English otherwise; Settings → Dil changes it.
    /// </summary>
    public static class Loc
    {
        public struct Language { public string code, native; public Language(string c, string n) { code = c; native = n; } }

        /// <summary>The 16 languages (code: the table file and the App Store locale family).</summary>
        public static readonly Language[] Languages =
        {
            new Language("en", "English"), new Language("tr", "Türkçe"), new Language("de", "Deutsch"), new Language("fr", "Français"),
            new Language("es", "Español"), new Language("pt", "Português (BR)"), new Language("it", "Italiano"), new Language("ru", "Русский"),
            new Language("ja", "日本語"), new Language("ko", "한국어"), new Language("zh-Hant", "繁體中文"), new Language("zh-Hans", "简体中文"),
            new Language("id", "Bahasa Indonesia"), new Language("pl", "Polski"), new Language("nl", "Nederlands"), new Language("vi", "Tiếng Việt"),
        };

        const string PrefKey = "sc2_lang";
        static string lang;
        static readonly Dictionary<string, string> table = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly List<Template> templates = new List<Template>();
        public static event Action Changed;

        class Template
        {
            public Regex rx;
            public string anchor;     // longest literal piece: a quick Contains test before the regex
            public string target;     // "{0} seconds"
            public int holes;
            public int literal;     // all fixed text: the more, the more specific
        }

        /// <summary>The current language code ("tr", "en" …).</summary>
        public static string Lang
        {
            get
            {
                if (lang == null) Load(PlayerPrefs.GetString(PrefKey, Detect()));
                return lang;
            }
        }

        public static bool IsSource => Lang == "tr";
        public static CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;

        /// <summary>The phone's language when the game has it, English otherwise.</summary>
        public static string Detect()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Russian: case SystemLanguage.Ukrainian: case SystemLanguage.Belarusian: return "ru";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                case SystemLanguage.Chinese: case SystemLanguage.ChineseSimplified: return "zh-Hans";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Vietnamese: return "vi";
                default: return "en";
            }
        }

        public static string NativeName(string code)
        {
            foreach (var l in Languages) if (l.code == code) return l.native;
            return code;
        }

        /// <summary>Switches the language (saved; every text on screen is redrawn).</summary>
        public static void Set(string code)
        {
            if (code == Lang) return;
            PlayerPrefs.SetString(PrefKey, code);
            PlayerPrefs.Save();
            Load(code);
            Res.RefreshFallbacks();
            Analytics.Event("language_change", "language", code);
            Analytics.SetUser("language", code);
            Changed?.Invoke();
        }

        static void Load(string code)
        {
            bool known = false;
            foreach (var l in Languages) if (l.code == code) known = true;
            lang = known ? code : "en";
            table.Clear();
            cache.Clear();
            upSource.Clear();
            templates.Clear();
            try { Culture = CultureInfo.GetCultureInfo(lang == "zh-Hant" ? "zh-TW" : (lang == "zh-Hans" ? "zh-CN" : (lang == "pt" ? "pt-BR" : lang))); }
            catch { Culture = CultureInfo.InvariantCulture; }
            if (lang == "tr") return;
            var ta = Resources.Load<TextAsset>("SC/Loc/" + lang);
            if (ta == null) { Debug.LogWarning("[SC] Dil tablosu yok: " + lang); return; }
            Parse(ta.text);
            Resources.UnloadAsset(ta);
        }

        /// <summary>Table format: one entry per line, "source\ttranslation", with \n, \t and \\ escaped.</summary>
        static void Parse(string text)
        {
            int start = 0;
            while (start < text.Length)
            {
                int end = text.IndexOf('\n', start);
                if (end < 0) end = text.Length;
                string line = text.Substring(start, end - start).TrimEnd('\r');
                start = end + 1;
                int tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                string src = Unescape(line.Substring(0, tab)), dst = Unescape(line.Substring(tab + 1));
                if (dst.Length == 0) continue;
                if (src.Contains("{0}")) AddTemplate(src, dst);
                else table[src] = dst;
            }
            // the most specific patterns first (most fixed text, then fewer holes)
            templates.Sort((a, b) => a.literal != b.literal ? b.literal.CompareTo(a.literal) : a.holes.CompareTo(b.holes));
        }

        static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    sb.Append(n == 'n' ? '\n' : (n == 't' ? '\t' : n));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        static void AddTemplate(string src, string dst)
        {
            var parts = Regex.Split(src, @"\{(\d+)\}");
            var rx = new StringBuilder("^");
            string anchor = "";
            int holes = 0, literal = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 0)
                {
                    string lit = parts[i].Replace("{{", "{").Replace("}}", "}");
                    rx.Append(Regex.Escape(lit));
                    literal += lit.Length;
                    if (lit.Length > anchor.Length) anchor = lit;
                }
                else
                {
                    rx.Append("(?<h" + parts[i] + ">.*?)");
                    holes++;
                }
            }
            rx.Append("$");
            if (anchor.Trim().Length < 2) return;           // "{0} {1}": nothing to recognise it by
            try
            {
                templates.Add(new Template { rx = new Regex(rx.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant), anchor = anchor, target = dst, holes = holes, literal = literal });
            }
            catch (Exception e) { Debug.LogWarning("[SC] Dil kalıbı: " + src + " — " + e.Message); }
        }

        /// <summary>The text in the current language (as it is when there is no translation).</summary>
        public static string Tr(string s) => Tr(s, 0);

        static string Tr(string s, int depth)
        {
            if (string.IsNullOrEmpty(s) || IsSource || depth > 3) return s;
            if (!HasLetters(s)) return s;
            if (cache.TryGetValue(s, out var hit)) return hit;
            string r = Lookup(s, depth);
            if (cache.Count > 4000) cache.Clear();
            cache[s] = r;
            return r;
        }

        static bool HasLetters(string s)
        {
            for (int i = 0; i < s.Length; i++) if (char.IsLetter(s[i])) return true;
            return false;
        }

        static string Lookup(string s, int depth)
        {
            if (table.TryGetValue(s, out var t)) return t;
            // the same text with its edges trimmed (texts padded with spaces in code)
            string trimmed = s.Trim();
            if (trimmed.Length != s.Length && trimmed.Length > 0 && table.TryGetValue(trimmed, out t))
                return s.Substring(0, s.IndexOf(trimmed, StringComparison.Ordinal)) + t + s.Substring(s.IndexOf(trimmed, StringComparison.Ordinal) + trimmed.Length);
            foreach (var tp in templates)
            {
                if (s.IndexOf(tp.anchor, StringComparison.Ordinal) < 0) continue;
                var m = tp.rx.Match(s);
                if (!m.Success) continue;
                var args = new object[tp.holes];
                for (int h = 0; h < tp.holes; h++)
                {
                    var g = m.Groups["h" + h];
                    args[h] = g.Success ? Tr(g.Value, depth + 1) : "";
                }
                try { return string.Format(tp.target, args); }
                catch { return s; }
            }
            // texts joined from several lines / " · " parts: each part on its own
            if (s.IndexOf('\n') >= 0) return JoinEach(s, "\n", depth);
            foreach (var sep in Separators)
                if (s.IndexOf(sep, StringComparison.Ordinal) > 0) return JoinEach(s, sep, depth);
            return s;
        }

        static readonly string[] Separators = { "   ·   ", "  ·  ", " · ", "   —   ", " — " };

        static string JoinEach(string s, string sep, int depth)
        {
            var parts = s.Split(new[] { sep }, StringSplitOptions.None);
            if (parts.Length < 2) return s;
            for (int i = 0; i < parts.Length; i++) parts[i] = Tr(parts[i], depth + 1);
            return string.Join(sep, parts);
        }

        static readonly Dictionary<string, string> upSource = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The text translated and in capitals (remembered, so a <see cref="LocText"/> showing it can redo it in
        /// another language).</summary>
        public static string Upper(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string r = Up(Tr(s));
            if (upSource.Count > 3000) upSource.Clear();
            upSource[r] = s;
            return r;
        }

        internal static bool UpperSource(string shown, out string source) => upSource.TryGetValue(shown, out source);

        /// <summary>Upper case in the current language (Turkish dotted / dotless i).</summary>
        public static string Up(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (IsSource) return s.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();
            return s.ToUpper(Culture);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>Design tokens: colours and type scale shared by every screen.</summary>
    public static class Theme
    {
        public static readonly Color Ink = Util.Hex("#07090C");
        public static readonly Color Panel = Util.Hex("#0E131A", 0.94f);
        public static readonly Color PanelSoft = Util.Hex("#0E131A", 0.88f);   // uGUI blends in linear space: higher alpha keeps panels dark over bright scenes
        public static readonly Color PanelLight = Util.Hex("#18212C");
        public static readonly Color Line = Util.Hex("#2C3746");
        public static readonly Color Text = Util.Hex("#EEF2F6");
        public static readonly Color TextDim = Util.Hex("#A3AEB9");
        public static readonly Color TextMute = Util.Hex("#6B7682");
        public static readonly Color Accent = Util.Hex("#FFB21E");
        public static readonly Color AccentDark = Util.Hex("#8A5C05");
        public static readonly Color Danger = Util.Hex("#FF4545");
        public static readonly Color Success = Util.Hex("#36D47C");
        public static readonly Color Info = Util.Hex("#46C8F2");
        public static readonly Color Guard = Util.Hex("#FF8A2B");
        public static readonly Color Shadow = new Color(0, 0, 0, 0.55f);

        // type scale (canvas units, reference height 1080)
        public const int H1 = 92, H2 = 60, H3 = 44, Title = 36, Body = 30, Small = 25, Tiny = 21;
    }

    public enum BtnStyle { Primary, Secondary, Ghost, Danger, Icon }

    /// <summary>Small factory for uGUI widgets built from code (no prefabs, no layout surprises).</summary>
    public static class UIK
    {
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = Layers.UI;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        /// <summary>Stretch to the parent with insets (left, bottom, right, top).</summary>
        public static RectTransform Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Anchor at a single point of the parent (0..1), pivot, offset and size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static readonly Vector2 TL = new Vector2(0, 1), TC = new Vector2(0.5f, 1), TR = new Vector2(1, 1),
            ML = new Vector2(0, 0.5f), MC = new Vector2(0.5f, 0.5f), MR = new Vector2(1, 0.5f),
            BL = new Vector2(0, 0), BC = new Vector2(0.5f, 0), BR = new Vector2(1, 0);

        public static Image Img(Transform parent, string name, string sprite, Color color, bool sliced = false, bool raycast = false)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            if (!string.IsNullOrEmpty(sprite)) img.sprite = Res.Sprite(sprite);
            img.color = color;
            img.raycastTarget = raycast;
            if (sliced && img.sprite != null)
            {
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
            }
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color, string sprite = "rr22", float cornerScale = 1f)
        {
            var img = Img(parent, name, sprite, color, true);
            img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.1f, cornerScale);
            return img;
        }

        public static Image Icon(Transform parent, string icon, Color color, float size)
        {
            var img = Img(parent, "ic_" + icon, null, color);
            img.sprite = Res.Icon(icon);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        public static Text Txt(Transform parent, string name, string text, Font font, int size, Color color,
                               TextAnchor align = TextAnchor.MiddleLeft, bool bestFit = true, int minSize = 0)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<LocText>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.lineSpacing = 1f;
            if (bestFit)
            {
                t.resizeTextForBestFit = true;
                t.resizeTextMaxSize = size;
                t.resizeTextMinSize = minSize > 0 ? minSize : Mathf.Max(10, Mathf.RoundToInt(size * 0.6f));
            }
            return t;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft) =>
            Txt(parent, "lbl", text, Res.FontSemi, size, color, align);

        public static Shadow AddShadow(Graphic g, float dist = 2f, float alpha = 0.6f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, alpha);
            s.effectDistance = new Vector2(dist, -dist);
            return s;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            if (g == null) g = go.AddComponent<CanvasGroup>();
            return g;
        }

        // ------------------------------------------------------------------ buttons
        public static Button Btn(Transform parent, string name, string label, BtnStyle style, Action onClick, string icon = null, int fontSize = 0)
        {
            Color bg, fg, line = Color.clear;
            switch (style)
            {
                case BtnStyle.Primary: bg = Theme.Accent; fg = Theme.Ink; break;
                case BtnStyle.Danger: bg = Theme.Danger; fg = Color.white; break;
                case BtnStyle.Ghost: bg = new Color(1, 1, 1, 0.001f); fg = Theme.Text; line = new Color(1, 1, 1, 0.28f); break;
                case BtnStyle.Icon: bg = Theme.PanelSoft; fg = Theme.Text; line = new Color(1, 1, 1, 0.14f); break;
                default: bg = Theme.PanelLight; fg = Theme.Text; line = new Color(1, 1, 1, 0.12f); break;
            }
            var img = Panel(parent, name, bg, "rr14");
            img.raycastTarget = true;
            var go = img.gameObject;
            if (line.a > 0)
            {
                var ln = Panel(img.transform, "line", line, "rr14_line");
                Stretch(ln.rectTransform);
            }
            var b = go.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = img;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            var fx = go.AddComponent<ButtonFX>();
            fx.Bind(img, bg);
            b.onClick.AddListener(() =>
            {
                Sfx.UI(style == BtnStyle.Ghost || style == BtnStyle.Icon ? Sfx.Back : Sfx.Confirm);
                onClick?.Invoke();
            });

            float pad = 26f;
            Image ic = null;
            if (!string.IsNullOrEmpty(icon))
            {
                ic = Icon(img.transform, icon, fg, 40);
                ic.name = "icon";
            }
            if (label != null)                  // "" = a label filled in later (SetBtnLabel); null = an icon-only button
            {
                var t = Txt(img.transform, "label", label, Res.FontBold, fontSize > 0 ? fontSize : 34, fg, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform, ic != null ? pad + 52 : pad, 4, pad, 4);
                if (ic != null)
                {
                    t.alignment = TextAnchor.MiddleLeft;
                    Place(ic.rectTransform, ML, new Vector2(0, 0.5f), new Vector2(pad, 0), new Vector2(40, 40));
                }
            }
            else if (ic != null) Place(ic.rectTransform, MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
            return b;
        }

        public static void SetBtnLabel(Button b, string label)
        {
            var t = b.transform.Find("label");
            if (t != null) t.GetComponent<Text>().text = label;
        }

        public static void SetInteractable(Button b, bool on)
        {
            b.interactable = on;
            UIK.Group(b.gameObject).alpha = on ? 1f : 0.4f;
        }

        // ------------------------------------------------------------------ misc widgets
        public static Image Divider(Transform parent, float y, float inset = 0)
        {
            var d = Img(parent, "divider", null, Theme.Line);
            var rt = d.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(inset, 0); rt.offsetMax = new Vector2(-inset, 0);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(-2 * inset, 2);
            return d;
        }

        /// <summary>Horizontal progress bar: background + fill (Image.Type.Filled).</summary>
        public static Image Bar(Transform parent, string name, Color bg, Color fill, out RectTransform root)
        {
            var b = Panel(parent, name, bg, "pill");
            root = b.rectTransform;
            var f = Img(b.transform, "fill", "pill", fill);
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.fillOrigin = 0;
            f.fillAmount = 0;
            Stretch(f.rectTransform, 3, 3, 3, 3);
            return f;
        }

        public static string Money(int v) => "$" + v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
        public static string Num(int v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');

        public static string Time(float s)
        {
            if (s < 0) s = 0;
            int m = Mathf.FloorToInt(s / 60f);
            return m.ToString("00") + ":" + Mathf.FloorToInt(s % 60f).ToString("00");
        }

        /// <summary>Turkish-aware upper case ("i" → "İ").</summary>
        public static string Up(string s) => Loc.Upper(s);
    }

    /// <summary>Press feedback for buttons (scale + tint), works with unscaled time (menus during pause).</summary>
    public class ButtonFX : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Graphic g;
        Color baseColor;
        float target = 1f, cur = 1f;
        bool down;

        public void Bind(Graphic graphic, Color c) { g = graphic; baseColor = c; }

        public void OnPointerDown(PointerEventData e) { down = true; target = 0.95f; if (g) g.color = baseColor * 0.85f + new Color(0, 0, 0, baseColor.a * 0.15f); }
        public void OnPointerUp(PointerEventData e) { Release(); }
        public void OnPointerExit(PointerEventData e) { if (down) Release(); }

        void Release() { down = false; target = 1f; if (g) g.color = baseColor; }

        void OnDisable() { cur = target = 1f; transform.localScale = Vector3.one; if (g) g.color = baseColor; }

        void Update()
        {
            if (Mathf.Abs(cur - target) < 0.001f) return;
            cur = Mathf.MoveTowards(cur, target, UnityEngine.Time.unscaledDeltaTime * 2.2f);
            transform.localScale = new Vector3(cur, cur, 1);
        }
    }

    /// <summary>Fires once when the element is held down for <see cref="seconds"/> (hidden switches).</summary>
    public class LongPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float seconds = 3f;
        public Action onLongPress;
        float downAt = -1f;

        public void OnPointerDown(PointerEventData e) { downAt = UnityEngine.Time.unscaledTime; }
        public void OnPointerUp(PointerEventData e) { downAt = -1f; }
        public void OnPointerExit(PointerEventData e) { downAt = -1f; }

        void Update()
        {
            if (downAt < 0f || UnityEngine.Time.unscaledTime - downAt < seconds) return;
            downAt = -1f;
            onLongPress?.Invoke();
        }
    }

    /// <summary>Hold-type button for the HUD (fire / breath / scope): reports down/up with multi-touch.</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action onDown, onUp;
        public bool Held { get; private set; }
        int pointer = int.MinValue;
        Vector3 baseScale = Vector3.one;

        void Awake() { baseScale = transform.localScale; }

        public void OnPointerDown(PointerEventData e)
        {
            if (Held) return;
            Held = true;
            pointer = e.pointerId;
            transform.localScale = baseScale * 0.93f;
            onDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!Held || e.pointerId != pointer) return;
            Release();
        }

        public void Release()
        {
            if (!Held) return;
            Held = false;
            pointer = int.MinValue;
            transform.localScale = baseScale;
            onUp?.Invoke();
        }

        void OnDisable() { if (Held) Release(); }
    }

    /// <summary>Unscaled-time tweens for UI (fade, move, scale, number count-up).</summary>
    public class Tween : MonoBehaviour
    {
        static Tween runner;
        static Tween R
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("_Tween");
                    DontDestroyOnLoad(go);
                    runner = go.AddComponent<Tween>();
                }
                return runner;
            }
        }

        static readonly Dictionary<object, Coroutine> running = new Dictionary<object, Coroutine>();

        public static void Stop(object key)
        {
            if (key != null && running.TryGetValue(key, out var c) && c != null && runner != null) runner.StopCoroutine(c);
            running.Remove(key);
        }

        public static void Run(object key, float duration, Action<float> step, Action done = null, float delay = 0f)
        {
            Stop(key);
            var c = R.StartCoroutine(Routine(key, duration, step, done, delay));
            if (key != null) running[key] = c;
        }

        static IEnumerator Routine(object key, float duration, Action<float> step, Action done, float delay)
        {
            if (delay > 0) yield return new WaitForSecondsRealtime(delay);
            float t = 0;
            while (t < duration)
            {
                t += UnityEngine.Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
                try { step?.Invoke(k); } catch (Exception e) { Debug.LogException(e); yield break; }
                yield return null;
            }
            step?.Invoke(1f);
            if (key != null) running.Remove(key);
            done?.Invoke();
        }

        public static void Fade(CanvasGroup g, float to, float duration, Action done = null, float delay = 0f)
        {
            if (g == null) return;
            float from = g.alpha;
            Run(g, duration, k => { if (g) g.alpha = Mathf.Lerp(from, to, Util.EaseInOut(k)); }, done, delay);
        }

        public static void Move(RectTransform rt, Vector2 to, float duration, float delay = 0f)
        {
            if (rt == null) return;
            Vector2 from = rt.anchoredPosition;
            Run(rt, duration, k => { if (rt) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, Util.EaseOutCubic(k)); }, null, delay);
        }

        public static void Pop(Transform t, float duration = 0.35f, float delay = 0f, float from = 0.6f)
        {
            if (t == null) return;
            t.localScale = Vector3.one * from;
            Run(t, duration, k => { if (t) t.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, Util.EaseOutBack(k)); }, null, delay);
        }
    }

    /// <summary>Keeps a RectTransform inside Screen.safeArea (notch, rounded corners, home indicator).</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        Vector2Int lastSize;

        void OnEnable() => Apply(true);
        void Update() => Apply(false);

        void Apply(bool force)
        {
            var sa = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (!force && sa == last && size == lastSize) return;
            last = sa;
            lastSize = size;
            var rt = (RectTransform)transform;
            if (size.x <= 0 || size.y <= 0) return;
            var min = new Vector2(sa.xMin / size.x, sa.yMin / size.y);
            var max = new Vector2(sa.xMax / size.x, sa.yMax / size.y);
            // keep a small minimum margin even on devices without insets
            const float m = 0.012f;
            min.x = Mathf.Max(min.x, m); max.x = Mathf.Min(max.x, 1 - m);
            min.y = Mathf.Max(min.y, m); max.y = Mathf.Min(max.y, 1 - m);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>
    /// Top bar of the meta screens (menu, contracts, armory, shop, results): the rank badge with level and experience,
    /// cash and diamonds with a "+" into the shop. Values refresh live and pop when they change.
    /// </summary>
    public class MetaBar : MonoBehaviour
    {
        public RectTransform CashChip { get; private set; }
        public RectTransform GemChip { get; private set; }
        public RectTransform Badge { get; private set; }
        public RectTransform CashIcon { get; private set; }
        public RectTransform GemIcon { get; private set; }
        Text cashT, gemT, levelT, rankT, xpT;
        Image badge, xpFill;
        int shownCash = int.MinValue, shownGems = int.MinValue, shownXp = int.MinValue;
        float next;
        /// <summary>Counts held back (the result screen lands the coins first, then lets the bar catch up).</summary>
        public bool Hold;

        public static MetaBar Build(UIRoot ui, RectTransform parent, bool rank, bool settings = false, bool shop = true)
        {
            var root = UIK.Rect(parent, "MetaBar");
            root.anchorMin = new Vector2(0, 1); root.anchorMax = new Vector2(1, 1);
            root.pivot = new Vector2(0.5f, 1);
            root.offsetMin = new Vector2(0, -116); root.offsetMax = new Vector2(0, 0);
            var mb = root.gameObject.AddComponent<MetaBar>();
            float x = -24f;
            if (settings)
            {
                var s = UIK.Btn(root, "Settings", null, BtnStyle.Icon, () => ui.ShowSettings(false), "settings");
                UIK.Place((RectTransform)s.transform, UIK.TR, new Vector2(1, 1), new Vector2(x, -22), new Vector2(76, 76));
                x -= 76f + 14f;
            }
            mb.GemChip = mb.MakeChip(ui, root, "gem", x, 236f, shop, out mb.gemT, out var gi);
            mb.GemIcon = gi;
            x -= 236f + 14f;
            mb.CashChip = mb.MakeChip(ui, root, "coin_c", x, 296f, shop, out mb.cashT, out var ci);
            mb.CashIcon = ci;
            if (rank) mb.MakeRank(root);
            mb.Refresh(true);
            return mb;
        }

        RectTransform MakeChip(UIRoot ui, RectTransform root, string sprite, float x, float w, bool shop, out Text value, out RectTransform icon)
        {
            var bg = UIK.Panel(root, "Chip_" + sprite, new Color(0.03f, 0.045f, 0.06f, 0.9f), "pill");
            UIK.Place(bg.rectTransform, UIK.TR, new Vector2(1, 1), new Vector2(x, -26), new Vector2(w, 68));
            var ln = UIK.Panel(bg.transform, "line", new Color(1, 1, 1, 0.13f), "pill_line");
            UIK.Stretch(ln.rectTransform);
            var ic = UIK.Img(bg.transform, "Icon", sprite, Color.white);
            ic.preserveAspect = true;
            UIK.Place(ic.rectTransform, UIK.ML, new Vector2(0.5f, 0.5f), new Vector2(26, 0), new Vector2(74, 74));
            icon = ic.rectTransform;
            value = UIK.Txt(bg.transform, "V", "", Res.FontBold, 36, Theme.Text, TextAnchor.MiddleRight, true, 22);
            UIK.Stretch(value.rectTransform, 70, 2, shop ? 74 : 26, 2);
            if (!shop) return bg.rectTransform;
            // "+": into the shop (diamonds or cash tab)
            bool gems = sprite == "gem";
            var plus = UIK.Panel(bg.transform, "Plus", Theme.Success, "rr14");
            UIK.Place(plus.rectTransform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-9, 0), new Vector2(52, 52));
            plus.raycastTarget = true;
            var pi = UIK.Icon(plus.transform, "plus", Color.white, 30);
            UIK.Place(pi.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            var b = bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            bg.raycastTarget = true;
            b.targetGraphic = bg;
            b.onClick.AddListener(() => { Sfx.UI(Sfx.Confirm); ui.ShowShop(gems ? ShopScreen.Tab.Gems : ShopScreen.Tab.Cash); });
            bg.gameObject.AddComponent<ButtonFX>().Bind(bg, bg.color);
            return bg.rectTransform;
        }

        void MakeRank(RectTransform root)
        {
            var hold = UIK.Rect(root, "Rank");
            UIK.Place(hold, UIK.TL, new Vector2(0, 1), new Vector2(22, -12), new Vector2(520, 100));
            badge = UIK.Img(hold, "Badge", "hexbadge", Color.white);
            badge.preserveAspect = true;
            UIK.Place(badge.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(96, 96));
            Badge = badge.rectTransform;
            levelT = UIK.Txt(badge.transform, "L", "1", Res.FontBold, 46, Color.white, TextAnchor.MiddleCenter, true, 26);
            UIK.Stretch(levelT.rectTransform, 8, 6, 8, 2);
            UIK.AddShadow(levelT, 2f, 0.75f);
            rankT = UIK.Txt(hold, "Rank", "", Res.FontBold, 32, Theme.Text, TextAnchor.MiddleLeft, true, 20);
            UIK.Place(rankT.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(110, -6), new Vector2(400, 40));
            UIK.AddShadow(rankT, 2f, 0.6f);
            xpFill = UIK.Bar(hold, "Xp", new Color(1, 1, 1, 0.14f), Theme.Info, out var bar);
            UIK.Place(bar, UIK.TL, new Vector2(0, 1), new Vector2(112, -52), new Vector2(330, 16));
            xpT = UIK.Txt(hold, "XpT", "", Res.FontSemi, 22, Theme.TextDim, TextAnchor.UpperLeft, false);
            UIK.Place(xpT.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(112, -72), new Vector2(380, 28));
            UIK.AddShadow(xpT, 1.5f, 0.6f);
        }

        void OnEnable() { next = 0f; if (!Hold) Refresh(true); }

        void Update()
        {
            if (Hold || Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.2f;
            Refresh(false);
        }

        public void Refresh(bool instant)
        {
            int c = Save.Cash, g = Save.Gems, xp = Save.Xp;
            // instant: always written (the page may have been built while its texts did not exist yet)
            if (instant || c != shownCash)
            {
                if (!instant && shownCash != int.MinValue && cashT != null) Tween.Pop(cashT.transform.parent, 0.3f, 0f, 1.12f);
                shownCash = c;
                if (cashT != null) cashT.text = UIK.Money(c);
            }
            if (instant || g != shownGems)
            {
                if (!instant && shownGems != int.MinValue && gemT != null) Tween.Pop(gemT.transform.parent, 0.3f, 0f, 1.12f);
                shownGems = g;
                if (gemT != null) gemT.text = g.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
            }
            if ((instant || xp != shownXp) && badge != null)
            {
                shownXp = xp;
                int lvl = Progress.Split(xp, out int into, out int need);
                SetRank(lvl, into, need);
            }
        }

        /// <summary>Shows a level and its experience (the result screen animates through these).</summary>
        public void SetRank(int lvl, int into, int need)
        {
            if (badge == null) return;
            var col = Progress.RankColor(lvl);
            badge.color = col;
            levelT.text = lvl.ToString();
            rankT.text = Progress.RankName(lvl) + "  <size=24><color=#A3AEB9>·  SEVİYE " + lvl + "</color></size>";
            rankT.supportRichText = true;
            rankT.color = Color.Lerp(col, Color.white, 0.35f);
            xpFill.fillAmount = need > 0 ? Mathf.Clamp01(into / (float)need) : 0f;
            xpFill.color = col;
            xpT.text = UIK.Num(into) + " / " + UIK.Num(need) + " TP";
        }

        /// <summary>Shows a cash value without reading the save (the result screen counts into it).</summary>
        public void ShowCash(int v) { shownCash = v; if (cashT != null) cashT.text = UIK.Money(v); }
        public void ShowGems(int v) { shownGems = v; if (gemT != null) gemT.text = UIK.Num(v); }
    }

    /// <summary>Coins / diamonds that fly from a point on screen into a chip of the top bar.</summary>
    public static class RewardFX
    {
        static readonly List<RectTransform> flying = new List<RectTransform>();

        /// <summary>Stops every coin still in the air (the page they were flying to has closed).</summary>
        public static void Clear()
        {
            foreach (var rt in flying)
            {
                if (rt == null) continue;
                Tween.Stop(rt);
                UnityEngine.Object.Destroy(rt.gameObject);
            }
            flying.Clear();
        }

        public static void Fly(Transform from, RectTransform to, string sprite, int count, Action onEach, Action onDone = null)
        {
            var root = UIRoot.I != null ? UIRoot.I.Root : null;
            if (root == null || from == null || to == null) { onDone?.Invoke(); return; }
            Vector2 a = root.InverseTransformPoint(from.position);
            Vector2 b = root.InverseTransformPoint(to.position);
            count = Mathf.Clamp(count, 1, 14);
            int arrived = 0;
            for (int i = 0; i < count; i++)
            {
                var img = UIK.Img(root, "Fly", sprite, Color.white);
                img.preserveAspect = true;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(64, 64);
                rt.anchoredPosition = a;
                rt.SetAsLastSibling();
                flying.Add(rt);
                var spread = UnityEngine.Random.insideUnitCircle * 110f;
                var ctrl = Vector2.Lerp(a, b, 0.35f) + new Vector2(spread.x, 160f + spread.y);
                float delay = i * 0.055f;
                float dur = 0.62f + UnityEngine.Random.value * 0.12f;
                rt.localScale = Vector3.zero;
                Tween.Run(rt, dur, k =>
                {
                    if (rt == null) return;
                    float e = Util.EaseInOut(k);
                    var p1 = Vector2.Lerp(a + spread * 0.35f, ctrl, e);
                    var p2 = Vector2.Lerp(ctrl, b, e);
                    rt.anchoredPosition = Vector2.Lerp(p1, p2, e);
                    float s = k < 0.15f ? k / 0.15f : Mathf.Lerp(1f, 0.55f, (k - 0.15f) / 0.85f);
                    rt.localScale = Vector3.one * s;
                }, () =>
                {
                    flying.Remove(rt);
                    if (rt != null) UnityEngine.Object.Destroy(rt.gameObject);
                    arrived++;
                    onEach?.Invoke();
                    if (arrived == count) onDone?.Invoke();
                }, delay);
            }
        }

        /// <summary>A light streak that sweeps over a button now and then (OYNA, offers).</summary>
        public static void Shine(RectTransform button, float every = 3.2f)
        {
            if (button == null) return;
            var mask = UIK.Rect(button, "ShineMask");
            UIK.Stretch(mask);
            mask.gameObject.AddComponent<RectMask2D>();
            var s = UIK.Img(mask, "Shine", "shine", new Color(1, 1, 1, 0.55f));
            s.rectTransform.anchorMin = new Vector2(0, 0); s.rectTransform.anchorMax = new Vector2(0, 1);
            s.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            s.rectTransform.sizeDelta = new Vector2(160, 0);
            var sw = s.gameObject.AddComponent<ShineSweep>();
            sw.every = every;
        }
    }

    /// <summary>Moves a shine image across its parent's width every few seconds (unscaled time).</summary>
    public class ShineSweep : MonoBehaviour
    {
        public float every = 3f;
        float t;

        void Update()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            t += Time.unscaledDeltaTime;
            float cycle = every + 0.7f;
            float k = (t % cycle) / 0.7f;
            float w = parent.rect.width;
            var rt = (RectTransform)transform;
            rt.anchoredPosition = new Vector2(k <= 1f ? Mathf.Lerp(-120f, w + 120f, k) : -400f, 0f);
        }
    }

    /// <summary>Slow spin for an image (light rays behind a reward).</summary>
    public class Spin : MonoBehaviour
    {
        public float speed = 12f;
        void Update() => transform.Rotate(0f, 0f, -speed * Time.unscaledDeltaTime);
    }

    /// <summary>
    /// The rifle on a turntable: one small studio far below the map with its own camera, lights and render texture,
    /// shared by the menu showcase and the armory (only one of them is on screen at a time).
    /// </summary>
    public class RifleStage : MonoBehaviour
    {
        static RifleStage inst;
        static readonly Vector3 StagePos = new Vector3(0f, -2500f, 0f);      // straight under the map, far below anything a camera sees
        Camera cam;
        RenderTexture rt;
        Transform model;
        string modelId;
        float spin, spinVel = 16f, sway;
        bool dragging;
        readonly HashSet<object> users = new HashSet<object>();
        /// <summary>Armory: free turntable you can drag. Menu: a slow sway around a three-quarter view.</summary>
        public bool Turntable = true;

        public static RifleStage Get()
        {
            if (inst != null) return inst;
            var go = new GameObject("RifleStage");
            go.transform.position = StagePos;
            inst = go.AddComponent<RifleStage>();
            inst.Build();
            return inst;
        }

        public RenderTexture Texture => rt;

        void Build()
        {
            var camGo = new GameObject("StageCam");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layers.ViewModel;
            cam.fieldOfView = 19f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 30f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.transform.localPosition = new Vector3(0f, 0.95f, -5.3f);
            cam.transform.localRotation = Quaternion.Euler(9.5f, 0f, 0f);
            rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4, name = "SC_RifleStage" };
            rt.memorylessMode = RenderTextureMemoryless.MSAA | RenderTextureMemoryless.Depth;
            rt.Create();
            cam.targetTexture = rt;
            // key, fill, warm kicker and a cool rim from behind
            AddLight(new Vector3(38f, -32f, 0f), new Color(1f, 0.96f, 0.9f), 1.3f);
            AddLight(new Vector3(18f, 150f, 0f), new Color(0.62f, 0.76f, 1f), 0.55f);
            AddLight(new Vector3(-12f, 68f, 0f), new Color(1f, 0.8f, 0.55f), 0.45f);
            AddLight(new Vector3(12f, 185f, 0f), new Color(0.85f, 0.92f, 1f), 0.9f);
            gameObject.SetActive(false);
        }

        void AddLight(Vector3 euler, Color c, float intensity)
        {
            var go = new GameObject("StageLight");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = c;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.cullingMask = 1 << Layers.ViewModel;
        }

        /// <summary>A screen starts showing the stage (the camera runs while anyone uses it).</summary>
        public void Acquire(object who)
        {
            users.Add(who);
            gameObject.SetActive(true);
            cam.enabled = true;
        }

        public void Release(object who)
        {
            users.Remove(who);
            if (users.Count > 0) return;
            cam.enabled = false;
            gameObject.SetActive(false);
        }

        public void Show(string id)
        {
            if (id == modelId && model != null) return;
            if (model != null) Destroy(model.gameObject);
            model = null;
            modelId = id;
            var prefab = Res.Prop(id);
            if (prefab == null) return;
            bool was = gameObject.activeSelf;
            gameObject.SetActive(true);
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            var m = Instantiate(prefab, pivot);
            m.transform.localRotation = Quaternion.identity;
            foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in m.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Util.SetLayer(m, Layers.ViewModel);
            // centre and fit from the meshes themselves (renderer bounds are empty on an inactive stage)
            var b = new Bounds(pivot.position, Vector3.zero);
            bool any = false;
            foreach (var mf in m.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var l2w = mf.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var wp = l2w.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(wp, Vector3.zero); any = true; } else b.Encapsulate(wp);
                }
            }
            m.transform.position -= b.center - pivot.position;
            float len = Mathf.Max(0.5f, b.size.x, b.size.z);
            // real proportions between rifles (a .50 looks bigger than a DMR), within a sensible range
            pivot.localScale = Vector3.one * (2.55f / len) * Mathf.Lerp(0.84f, 1f, Mathf.InverseLerp(1.0f, 1.45f, len));
            model = pivot;
            if (!was && users.Count == 0) gameObject.SetActive(false);
        }

        public void Drag(Vector2 d) { spin += d.x * 0.35f; spinVel = 0f; dragging = true; }
        public void EndDrag() { dragging = false; spinVel = 14f; }

        void Update()
        {
            if (model == null) return;
            float yaw;
            if (Turntable)
            {
                if (!dragging) spin += spinVel * Time.unscaledDeltaTime;
                yaw = spin + 180f;
            }
            else
            {
                sway += Time.unscaledDeltaTime;
                yaw = 180f + 24f + Mathf.Sin(sway * 0.45f) * 22f;
            }
            model.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 3f);
            model.localPosition = new Vector3(0f, Mathf.Sin(Time.unscaledTime * 1.1f) * 0.012f, 0f);
        }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (inst == this) inst = null;
        }
    }

    /// <summary>The rifle stage on a page: backdrop glow, light rays, the render and a floor shadow.</summary>
    public class StageView : MonoBehaviour
    {
        public RawImage image;
        public Image spot, rays, shadow, dark;

        public static StageView Build(Transform parent, string name, bool drag)
        {
            var root = UIK.Rect(parent, name);
            var sv = root.gameObject.AddComponent<StageView>();
            sv.dark = UIK.Img(root, "Dark", "spot", new Color(0f, 0f, 0f, 0.55f));
            UIK.Stretch(sv.dark.rectTransform, -120, -80, -120, -60);
            sv.rays = UIK.Img(root, "Rays", "rays", new Color(1f, 0.8f, 0.4f, 0.10f));
            sv.rays.rectTransform.anchorMin = new Vector2(0.1f, 0.25f); sv.rays.rectTransform.anchorMax = new Vector2(0.9f, 1.05f);
            sv.rays.rectTransform.offsetMin = sv.rays.rectTransform.offsetMax = Vector2.zero;
            sv.spot = UIK.Img(root, "Spot", "spot", new Color(1f, 0.75f, 0.3f, 0.22f));
            UIK.Stretch(sv.spot.rectTransform, 10, 30, 10, 10);
            sv.shadow = UIK.Img(root, "Shadow", "floor_shadow", new Color(0f, 0f, 0f, 0.65f));
            sv.shadow.rectTransform.anchorMin = new Vector2(0.18f, 0.06f); sv.shadow.rectTransform.anchorMax = new Vector2(0.82f, 0.2f);
            sv.shadow.rectTransform.offsetMin = sv.shadow.rectTransform.offsetMax = Vector2.zero;
            var go = new GameObject("Render", typeof(RectTransform));
            go.layer = Layers.UI;
            go.transform.SetParent(root, false);
            sv.image = go.AddComponent<RawImage>();
            sv.image.raycastTarget = drag;
            UIK.Stretch(sv.image.rectTransform);
            var fit = go.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1280f / 720f;
            if (drag)
            {
                var d = go.AddComponent<PreviewDrag>();
                d.onDrag = v => RifleStage.Get().Drag(v);
                d.onUp = () => RifleStage.Get().EndDrag();
            }
            return sv;
        }

        public void Tint(Color c)
        {
            spot.color = new Color(c.r, c.g, c.b, 0.24f);
            rays.color = new Color(c.r, c.g, c.b, 0.12f);
        }

        public void Bind(object who, string modelId, bool turntable)
        {
            var st = RifleStage.Get();
            st.Turntable = turntable;
            st.Acquire(who);
            st.Show(modelId);
            image.texture = st.Texture;
            image.color = Color.white;
        }

        public void Unbind(object who)
        {
            RifleStage.Get().Release(who);
        }
    }

    /// <summary>"Level up!": the new rank, its badge and what it pays (one card per level crossed).</summary>
    public class LevelUpScreen : UIScreen
    {
        Text title, levelT, rankT, cashT, gemT, unlockT;
        Image badge, rays;
        RectTransform card, cashRow, gemRow;
        readonly Queue<Progress.LevelReward> queue = new Queue<Progress.LevelReward>();
        Action done;
        Button go, twice;
        Progress.LevelReward current;
        bool doubledThis;

        protected override void Build()
        {
            var dim = UIK.Img(transform, "Dim", null, new Color(0, 0, 0, 0.82f), false, true);
            UIK.Stretch(dim.rectTransform);
            rays = UIK.Img(Safe, "Burst", "burst", new Color(1f, 0.75f, 0.25f, 0.35f));
            UIK.Place(rays.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1100, 1100));
            var c = UIK.Panel(Safe, "Card", new Color(0.05f, 0.07f, 0.09f, 0.97f), "ch28");
            card = c.rectTransform;
            UIK.Place(card, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(940, 560));
            var ln = UIK.Panel(c.transform, "line", new Color(1f, 0.75f, 0.2f, 0.5f), "ch28_line");
            UIK.Stretch(ln.rectTransform);
            badge = UIK.Img(Safe, "Badge", "hexbadge", Color.white);
            badge.preserveAspect = true;
            UIK.Place(badge.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(230, 230));
            levelT = UIK.Txt(badge.transform, "L", "", Res.FontBold, 118, Color.white, TextAnchor.MiddleCenter, false);
            UIK.Stretch(levelT.rectTransform, 0, 10, 0, 0);
            UIK.AddShadow(levelT, 3f, 0.8f);
            title = UIK.Txt(c.transform, "Title", "SEVİYE ATLADIN!", Res.FontBold, 76, Theme.Accent, TextAnchor.UpperCenter, false);
            UIK.Place(title.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(880, 90));
            UIK.AddShadow(title, 3f, 0.6f);
            rankT = UIK.Txt(c.transform, "Rank", "", Res.FontSemi, 36, Theme.Text, TextAnchor.UpperCenter, false);
            UIK.Place(rankT.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -240), new Vector2(880, 48));
            cashRow = Reward(c.transform, "coin_c", out cashT, new Vector2(-170, -290));
            gemRow = Reward(c.transform, "gem", out gemT, new Vector2(170, -290));
            unlockT = UIK.Txt(c.transform, "Unlock", "", Res.FontSemi, 26, Theme.Info, TextAnchor.UpperCenter, false);
            UIK.Place(unlockT.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -384), new Vector2(880, 32));
            go = UIK.Btn(c.transform, "Go", "TOPLA", BtnStyle.Primary, Next, "check", 40);
            UIK.Place((RectTransform)go.transform, UIK.BC, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(420, 104));
            RewardFX.Shine((RectTransform)go.transform, 2.4f);
            // a video doubles this level's reward
            twice = UIK.Btn(c.transform, "Twice", "2× TOPLA", BtnStyle.Secondary, Twice, "ad", 36);
            UIK.Place((RectTransform)twice.transform, UIK.BC, new Vector2(0.5f, 0), new Vector2(-220, 36), new Vector2(380, 104));
        }

        RectTransform Reward(Transform parent, string sprite, out Text value, Vector2 pos)
        {
            var row = UIK.Panel(parent, "R_" + sprite, new Color(1, 1, 1, 0.06f), "ch18");
            UIK.Place(row.rectTransform, UIK.TC, new Vector2(0.5f, 1), pos, new Vector2(300, 86));
            var ic = UIK.Img(row.transform, "I", sprite, Color.white);
            ic.preserveAspect = true;
            UIK.Place(ic.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(72, 72));
            value = UIK.Txt(row.transform, "V", "", Res.FontBold, 46, Theme.Text, TextAnchor.MiddleRight, true, 28);
            UIK.Stretch(value.rectTransform, 96, 4, 22, 4);
            return row.rectTransform;
        }

        public void Play(List<Progress.LevelReward> levels, Action onDone)
        {
            queue.Clear();
            foreach (var l in levels) queue.Enqueue(l);
            done = onDone;
            if (queue.Count == 0) { onDone?.Invoke(); return; }
            Show(0.2f);
            ShowNext();
        }

        void ShowNext()
        {
            var r = queue.Dequeue();
            current = r;
            doubledThis = false;
            bool ad = Ads.RewardedReady;
            twice.gameObject.SetActive(ad);
            var gr = (RectTransform)go.transform;
            gr.anchoredPosition = new Vector2(ad ? 220 : 0, 36);
            gr.sizeDelta = new Vector2(ad ? 380 : 420, 104);
            var col = Progress.RankColor(r.level);
            badge.color = col;
            levelT.text = r.level.ToString();
            rankT.text = Progress.RankName(r.level) + "   ·   SEVİYE " + r.level;
            rankT.color = Color.Lerp(col, Color.white, 0.4f);
            rays.color = new Color(col.r, col.g, col.b, 0.35f);
            cashT.text = "+" + UIK.Money(r.cash);
            gemT.text = "+" + r.gems;
            unlockT.text = string.IsNullOrEmpty(r.unlock) ? "" : "AÇILDI:  " + r.unlock;
            Tween.Pop(badge.transform, 0.55f, 0f, 0.2f);
            Tween.Pop(card, 0.4f, 0.05f, 0.85f);
            Tween.Pop(cashRow, 0.35f, 0.35f, 0.5f);
            Tween.Pop(gemRow, 0.35f, 0.45f, 0.5f);
            Sfx.Play(Sfx.Success, 0.9f, 1.15f);
        }

        void Twice()
        {
            if (doubledThis) return;
            var r = current;
            Ads.ShowRewarded("levelup_double", ok =>
            {
                if (!ok || doubledThis || this == null) return;
                doubledThis = true;
                Progress.AddCash(r.cash, "ad_levelup_double");
                Progress.AddGems(r.gems, "ad_levelup_double");
                cashT.text = "+" + UIK.Money(r.cash * 2);
                gemT.text = "+" + (r.gems * 2);
                Tween.Pop(cashRow, 0.35f, 0f, 0.6f);
                Tween.Pop(gemRow, 0.35f, 0.1f, 0.6f);
                Sfx.Play(Sfx.Success, 0.8f, 1.3f);
                twice.gameObject.SetActive(false);
                var gr = (RectTransform)go.transform;
                gr.anchoredPosition = new Vector2(0, 36);
                gr.sizeDelta = new Vector2(420, 104);
            });
        }

        void Next()
        {
            // the rewards fly to the bar of the page underneath (when it has one)
            var bar = UIRoot.I != null ? UIRoot.I.ActiveMetaBar() : null;
            if (bar != null)
            {
                RewardFX.Fly(cashRow, bar.CashIcon, "coin_c", 8, () => Sfx.Play(Sfx.Coin, 0.35f, 1.3f));
                RewardFX.Fly(gemRow, bar.GemIcon, "gem", 5, () => Sfx.Play(Sfx.Coin, 0.3f, 1.6f));
            }
            if (queue.Count > 0) { ShowNext(); return; }
            Hide(0.2f);
            var d = done;
            done = null;
            d?.Invoke();
        }

        void Update()
        {
            if (Visible && rays != null) rays.transform.Rotate(0f, 0f, -6f * Time.unscaledDeltaTime);
        }
    }
}

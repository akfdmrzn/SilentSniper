using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>Base for full-screen UI pages: built once from code, shown/hidden with a fade.</summary>
    public abstract class UIScreen : MonoBehaviour
    {
        protected UIRoot ui;
        protected Game game;
        public RectTransform Rt { get; private set; }
        public CanvasGroup Cg { get; private set; }
        public RectTransform Safe { get; private set; }
        public bool Visible { get; private set; }

        public void Setup(UIRoot root)
        {
            ui = root;
            game = root.Game;
            Rt = (RectTransform)transform;
            UIK.Stretch(Rt);
            Cg = UIK.Group(gameObject);
            var safe = UIK.Rect(transform, "Safe");
            safe.gameObject.AddComponent<SafeArea>();
            Safe = safe;
            // a page that fails to build must not take the whole game down with it: log it and keep going (the page
            // stays hidden until it is opened)
            try
            {
                Build();
                // page backgrounds are added to the page root after the safe area was created: keep the content on top
                Safe.SetAsLastSibling();
                AfterBuild();
            }
            catch (Exception e) { Debug.LogException(e); }
            Cg.alpha = 0;
            Cg.blocksRaycasts = false;
            Cg.interactable = false;
            gameObject.SetActive(false);
        }

        protected abstract void Build();
        /// <summary>The page's name in the analytics (menu, shop, result …).</summary>
        protected virtual string ScreenName => GetType().Name.Replace("Screen", "").ToLowerInvariant();
        /// <summary>Called after the safe-area content was moved on top (for overlays that must sit above it).</summary>
        protected virtual void AfterBuild() { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        public void Show(float fade = 0.22f)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Visible = true;
            ui.KeepOverlaysOnTop();
            Cg.blocksRaycasts = true;
            Cg.interactable = true;
            OnShow();
            Tween.Fade(Cg, 1f, fade);
            try { Analytics.Screen(ScreenName); } catch (Exception e) { Debug.LogException(e); }
        }

        public void Hide(float fade = 0.18f)
        {
            if (!Visible) return;
            Visible = false;
            Cg.blocksRaycasts = false;
            Cg.interactable = false;
            OnHide();
            Tween.Fade(Cg, 0f, fade, () => { if (!Visible && this != null) gameObject.SetActive(false); });
        }

        /// <summary>Back button + title bar used by most pages.</summary>
        protected RectTransform Header(string title, Action back, string rightIcon = null, Func<string> rightText = null)
        {
            var bar = UIK.Rect(Safe, "Header");
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.offsetMin = new Vector2(24, -120); bar.offsetMax = new Vector2(-24, -20);
            if (back != null)
            {
                var b = UIK.Btn(bar, "Back", null, BtnStyle.Icon, back, "arrow_left");
                UIK.Place((RectTransform)b.transform, UIK.ML, new Vector2(0, 0.5f), Vector2.zero, new Vector2(92, 92));
            }
            var t = UIK.Txt(bar, "Title", title, Res.FontBold, Theme.H2, Theme.Text, TextAnchor.MiddleLeft);
            t.rectTransform.anchorMin = new Vector2(0, 0); t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.offsetMin = new Vector2(back != null ? 120 : 0, 0); t.rectTransform.offsetMax = new Vector2(-420, 0);
            if (rightText != null) ui.Chip(bar, rightIcon, rightText, new Vector2(0, 0), UIK.MR, new Vector2(1, 0.5f));
            return bar;
        }
    }

    /// <summary>Canvas, safe area, page switching, blackout fades and dialogs.</summary>
    public class UIRoot : MonoBehaviour
    {
        public static UIRoot I;
        public Game Game { get; private set; }
        public Canvas Canvas { get; private set; }
        public RectTransform Root { get; private set; }

        public LoadingScreen Loading { get; private set; }
        public MenuScreen Menu { get; private set; }
        public MissionsScreen Missions { get; private set; }
        public BriefingScreen Briefing { get; private set; }
        public ArmoryScreen Armory { get; private set; }
        public SettingsScreen Settings { get; private set; }
        public MapScreen Map { get; private set; }
        public CreditsScreen Credits { get; private set; }
        public PauseScreen Pause { get; private set; }
        public ResultScreen Result { get; private set; }
        public HUD Hud { get; private set; }
        public DialogScreen Dialog { get; private set; }
        public ShopScreen Shop { get; private set; }
        public LevelUpScreen LevelUp { get; private set; }
        public DailyRewardScreen Daily { get; private set; }

        CanvasGroup blackout;
        readonly List<UIScreen> pages = new List<UIScreen>();

        public static UIRoot Create(Game g)
        {
            var go = new GameObject("UI");
            go.layer = Layers.UI;
            var ui = go.AddComponent<UIRoot>();
            ui.Init(g);
            return ui;
        }

        void Init(Game g)
        {
            I = this;
            Game = g;
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            Canvas.pixelPerfect = false;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 100;
            gameObject.AddComponent<GraphicRaycaster>();
            Root = (RectTransform)transform;

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                var sys = es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                sys.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt((Screen.dpi > 0 ? Screen.dpi : 326) * 0.1f));
            }

            Hud = Page<HUD>("HUD");
            Menu = Page<MenuScreen>("Menu");
            Missions = Page<MissionsScreen>("Missions");
            Briefing = Page<BriefingScreen>("Briefing");
            Armory = Page<ArmoryScreen>("Armory");
            Credits = Page<CreditsScreen>("Credits");
            Result = Page<ResultScreen>("Result");
            Pause = Page<PauseScreen>("Pause");
            Settings = Page<SettingsScreen>("Settings");
            Map = Page<MapScreen>("Map");
            Shop = Page<ShopScreen>("Shop");
            Daily = Page<DailyRewardScreen>("Daily");
            LevelUp = Page<LevelUpScreen>("LevelUp");
            Dialog = Page<DialogScreen>("Dialog");
            Loading = Page<LoadingScreen>("Loading");

            var bo = UIK.Img(transform, "Blackout", null, Color.black, false, true);
            UIK.Stretch(bo.rectTransform);
            blackout = UIK.Group(bo.gameObject);
            blackout.alpha = 0;
            blackout.blocksRaycasts = false;
        }

        T Page<T>(string name) where T : UIScreen
        {
            var rt = UIK.Rect(transform, name);
            var p = rt.gameObject.AddComponent<T>();
            p.Setup(this);
            pages.Add(p);
            return p;
        }

        /// <summary>Dialog, loading and blackout always stay above the pages.</summary>
        public void KeepOverlaysOnTop()
        {
            if (Settings != null && Settings.Visible) Settings.transform.SetAsLastSibling();
            if (Map != null && Map.Visible) Map.transform.SetAsLastSibling();
            if (Daily != null && Daily.Visible) Daily.transform.SetAsLastSibling();
            if (LevelUp != null && LevelUp.Visible) LevelUp.transform.SetAsLastSibling();
            if (Dialog != null && Dialog.Visible) Dialog.transform.SetAsLastSibling();
            if (Loading != null && Loading.Visible) Loading.transform.SetAsLastSibling();
            if (blackout != null) blackout.transform.SetAsLastSibling();
        }

        void HideAllExcept(params UIScreen[] keep)
        {
            foreach (var p in pages)
                if (Array.IndexOf(keep, p) < 0 && p.Visible) p.Hide();
        }

        // ------------------------------------------------------------------ navigation
        public void ShowLoading() { Loading.Show(0f); }
        public void SetLoading(float p, string msg) { Loading.Set(p, msg); }

        public void ShowMenu(bool first)
        {
            if (Game.Ready && !Game.InMenu) { Game.GoMenu(first); return; }
            HideAllExcept(Menu);
            Menu.Show(first ? 0.6f : 0.25f);
            if (Loading.Visible) Loading.Hide(0.6f);
        }

        public void ShowMissions() { HideAllExcept(Missions); Missions.Show(); }
        public void ShowBriefing(MissionDef def) { Briefing.Set(def); HideAllExcept(Briefing); Briefing.Show(); }
        /// <summary>The armory; <paramref name="backTo"/> = where its back button returns (null = main menu).</summary>
        public void ShowArmory(Action backTo) { Armory.BackTo = backTo; HideAllExcept(Armory); Armory.Show(); }
        public void ShowCredits() { Credits.CaseBoard = false; HideAllExcept(Credits); Credits.Show(); }

        /// <summary>The shop on a tab; its back button returns to the page that was open (menu by default).</summary>
        public void ShowShop(ShopScreen.Tab tab, Action backTo = null)
        {
            if (Shop.Visible) { Shop.Open(tab); return; }      // a "+" on the shop's own bar: just the tab
            if (backTo == null)
            {
                // came from a page with a top bar: go back there
                if (Armory.Visible) { var b = Armory.BackTo; backTo = () => ShowArmory(b); }
                else if (Missions.Visible) backTo = ShowMissions;
                else if (Briefing.Visible && Briefing.Def != null) { var d = Briefing.Def; backTo = () => ShowBriefing(d); }
                else if (Result.Visible) backTo = null;
            }
            if (Result.Visible || Hud.Visible) return;          // not in the middle of a contract or its results
            string from = Armory.Visible ? "armory" : (Missions.Visible ? "missions" : (Briefing.Visible ? "briefing" : "menu"));
            Analytics.Event("shop_open", "tab", tab.ToString().ToLowerInvariant(), "source", from);
            Shop.BackTo = backTo;
            Shop.Open(tab);
            HideAllExcept(Shop);
            Shop.Show();
        }

        /// <summary>The top bar of the page in front (reward animations fly into it).</summary>
        public MetaBar ActiveMetaBar()
        {
            MetaBar best = null;
            int bestIdx = -1;
            foreach (var p in pages)
            {
                if (!p.Visible || p is LevelUpScreen) continue;
                var mb = p.GetComponentInChildren<MetaBar>();
                int idx = p.transform.GetSiblingIndex();
                if (mb != null && idx > bestIdx) { best = mb; bestIdx = idx; }
            }
            return best;
        }
        /// <summary>V2: the case board (what every finished contract uncovered).</summary>
        public void ShowCaseBoard() { Credits.CaseBoard = true; HideAllExcept(Credits); Credits.Show(); }

        public void ShowSettings(bool fromPause)
        {
            Settings.FromPause = fromPause;
            if (!fromPause) HideAllExcept(Settings);
            Settings.Show();
        }

        public void CloseSettings()
        {
            Settings.Hide();
            if (!Settings.FromPause) Menu.Show();
        }

        public void ShowHUD(MissionDef def)
        {
            HideAllExcept(Hud);
            Hud.Begin(def);
            Hud.Show(0.2f);
        }

        /// <summary>V2: the contract's tactical map (briefing and pause menu); false when there is none.</summary>
        public bool ShowMap(MissionDef def)
        {
            if (def == null || !MapScreen.Has(def)) return false;
            Map.Set(def);
            Map.Show(0.15f);
            Map.transform.SetAsLastSibling();
            return true;
        }

        public void ShowPause()
        {
            if (Pause.Visible || Result.Visible) return;
            Game.SetPaused(true);
            Pause.Show(0.15f);
        }

        public void ClosePause()
        {
            Pause.Hide(0.12f);
            Game.SetPaused(false);
        }

        public void ShowResult()
        {
            if (Pause.Visible) ClosePause();
            if (Dialog.Visible) Dialog.Hide(0.1f);
            Hud.Hide(0.3f);
            Result.Set(Game.Runtime);
            Result.Show(0.35f);
        }

        public void Blackout(bool on, float duration)
        {
            blackout.transform.SetAsLastSibling();
            blackout.blocksRaycasts = on;
            Tween.Fade(blackout, on ? 1f : 0f, duration);
        }

        public void Confirm(string title, string message, string yes, Action onYes, string no = "VAZGEÇ", Action onNo = null)
        {
            Dialog.Set(title, message, yes, onYes, no, onNo);
            Dialog.Show(0.15f);
        }

        // ------------------------------------------------------------------ shared widgets
        /// <summary>Rounded chip with an icon and a live text (cash, stars).</summary>
        public Text Chip(Transform parent, string icon, Func<string> text, Vector2 pos, Vector2 anchor, Vector2 pivot, float width = 220)
        {
            var bg = UIK.Panel(parent, "Chip", Theme.PanelSoft, "pill");
            UIK.Place(bg.rectTransform, anchor, pivot, pos, new Vector2(width, 72));
            var ln = UIK.Panel(bg.transform, "line", new Color(1, 1, 1, 0.12f), "pill_line");
            UIK.Stretch(ln.rectTransform);
            if (!string.IsNullOrEmpty(icon))
            {
                var ic = UIK.Icon(bg.transform, icon, Theme.Accent, 36);
                UIK.Place(ic.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(36, 36));
            }
            var t = UIK.Txt(bg.transform, "text", text(), Res.FontBold, 34, Theme.Text, TextAnchor.MiddleRight);
            UIK.Stretch(t.rectTransform, 66, 4, 24, 4);
            var live = bg.gameObject.AddComponent<LiveText>();
            live.target = t;
            live.source = text;
            return t;
        }
    }

    /// <summary>Refreshes a text from a function a few times per second (cash, stars, timers).</summary>
    public class LiveText : MonoBehaviour
    {
        public Text target;
        public Func<string> source;
        float next;

        void OnEnable() { next = 0; }

        void Update()
        {
            if (target == null || source == null || UnityEngine.Time.unscaledTime < next) return;
            next = UnityEngine.Time.unscaledTime + 0.25f;
            var s = source();
            if (target.text != s) target.text = s;
        }
    }

    // ====================================================================== loading
    public class LoadingScreen : UIScreen
    {
        Image fill, art;
        AspectRatioFitter artFit;
        Text status, tip, subT;
        RectTransform tipRt;
        static readonly string[] Tips =
        {
            "Dürbünde nişangahı birinin üzerinde tutarak kimliğini tara — masumları vurma.",
            "Nefesini tutarken nişangah sabitlenir, ama nefes çubuğu biterse titreme artar.",
            "Uzak mesafede kurşun düşer ve rüzgarla kayar. Kırmızı nokta isabet noktasını gösterir.",
            "Bir koruma telsize uzanırsa birkaç saniyen var: onu durdurursan alarm verilmez.",
            "Kafadan vuruşlar ve sivil kayıpsız görevler daha fazla yıldız ve para kazandırır.",
            "Zırhlı adamların gövdesi kurşun tutar: kafaya nişan al ya da tüfeğinin HASAR'ını geliştir.",
            "Iskalama sessiz kalmaz: ikinci boş atış silahlıları yuvana yollar.",
            "Görev, sana ateş eden son silahlı susunca biter. Önce en yakındakini sustur.",
            "Cephanelikte her tüfeğin beş hattı var: en üst kademe elmasla açılır.",
            "Günün sözleşmesi iki kat para ve elmas öder — her gün yenisi gelir.",
        };

        protected override void Build()
        {
            art = UIK.Img(transform, "Art", null, Color.white);
            art.sprite = Res.Art("load_harbor") ?? Res.Art("menu_bg");
            UIK.Stretch(art.rectTransform);
            artFit = art.gameObject.AddComponent<AspectRatioFitter>();
            artFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            artFit.aspectRatio = art.sprite != null ? art.sprite.rect.width / art.sprite.rect.height : 16f / 9f;
            if (art.sprite == null) art.color = Theme.Ink;
            var shade = UIK.Img(transform, "Shade", "grad_v", new Color(0, 0, 0, 0.92f));
            UIK.Stretch(shade.rectTransform);
            shade.rectTransform.anchorMax = new Vector2(1, 0.55f);
            shade.rectTransform.localScale = new Vector3(1, -1, 1);
            var left = UIK.Img(transform, "Left", "grad_h", new Color(0, 0, 0, 0.55f));
            UIK.Stretch(left.rectTransform);
            left.rectTransform.anchorMax = new Vector2(0.6f, 1);
            left.rectTransform.localScale = new Vector3(-1, 1, 1);
            var vig = UIK.Img(transform, "Vignette", "vignette", new Color(0, 0, 0, 0.6f));
            UIK.Stretch(vig.rectTransform);

            var title = UIK.Txt(Safe, "Title", "SILENT SNIPER", Res.FontBold, 120, Theme.Text, TextAnchor.LowerLeft);
            UIK.Place(title.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(80, 300), new Vector2(1200, 140));
            UIK.AddShadow(title, 4f, 0.8f);
            var sub = UIK.Txt(Safe, "Sub", "LİMAN OPERASYONU", Res.FontSemi, 42, Theme.Accent, TextAnchor.UpperLeft);
            UIK.Place(sub.rectTransform, UIK.BL, new Vector2(0, 1), new Vector2(86, 296), new Vector2(900, 56));
            UIK.AddShadow(sub, 2f, 0.8f);
            subT = sub;

            fill = UIK.Bar(Safe, "Bar", new Color(1, 1, 1, 0.14f), Theme.Accent, out var barRt);
            UIK.Place(barRt, UIK.BL, new Vector2(0, 0), new Vector2(84, 150), new Vector2(760, 16));
            status = UIK.Txt(Safe, "Status", "", Res.FontSemi, 30, Theme.TextDim, TextAnchor.LowerLeft);
            UIK.Place(status.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(84, 178), new Vector2(760, 44));
            var tipBox = UIK.Panel(Safe, "TipBox", new Color(0.02f, 0.03f, 0.04f, 0.72f), "ch18");
            tipRt = tipBox.rectTransform;
            UIK.Place(tipRt, UIK.BL, new Vector2(0, 1), new Vector2(80, 126), new Vector2(1180, 96));
            var ti = UIK.Icon(tipBox.transform, "info", Theme.Info, 36);
            UIK.Place(ti.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(36, 36));
            tip = UIK.Txt(tipBox.transform, "Tip", "", Res.FontBody, 27, Theme.Text, TextAnchor.MiddleLeft, true, 18);
            UIK.Stretch(tip.rectTransform, 74, 8, 20, 8);
        }

        protected override void OnShow()
        {
            tip.text = Tips[UnityEngine.Random.Range(0, Tips.Length)];
        }

        /// <summary>Subtitle and key art of the map being built ("ŞEHİR OPERASYONU").</summary>
        public void SetLocation(LocationDef loc)
        {
            if (loc == null) return;
            if (subT != null) subT.text = loc.operation;
            var sp = Res.Art("load_" + loc.id);
            if (sp != null && art != null)
            {
                art.sprite = sp;
                art.color = Color.white;
                artFit.aspectRatio = sp.rect.width / sp.rect.height;
            }
        }

        public void Set(float p, string msg)
        {
            if (fill != null) fill.fillAmount = Mathf.Clamp01(p);
            if (status != null && msg != null) status.text = UIK.Up(msg) + "  " + Mathf.RoundToInt(Mathf.Clamp01(p) * 100) + "%";
        }

        void Update()
        {
            // a slow push-in on the key art while it loads
            if (Visible && art != null) art.rectTransform.localScale = Vector3.one * (1.02f + 0.03f * Mathf.PingPong(Time.unscaledTime * 0.02f, 1f));
        }
    }

    // ====================================================================== dialog
    /// <summary>
    /// V2: the contract's tactical map — an overhead picture of the ground with the nests, the range rings from the main
    /// nest, the target's area and the known routes (Resources/SC/Maps/&lt;contract id&gt;, made from the layout).
    /// </summary>
    public class MapScreen : UIScreen
    {
        Image map;
        Text title, legend;
        RectTransform frame;

        public static bool Has(MissionDef d) => d != null && Res.Map(d.id) != null;

        protected override void Build()
        {
            var dim = UIK.Img(transform, "Dim", null, new Color(0.01f, 0.015f, 0.02f, 1f), false, true);
            UIK.Stretch(dim.rectTransform);
            title = UIK.Txt(Safe, "Title", "", Res.FontBold, Theme.H3, Theme.Text, TextAnchor.MiddleLeft, false);
            UIK.Place(title.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(40, -24), new Vector2(1200, 64));
            var close = UIK.Btn(Safe, "Close", "KAPAT", BtnStyle.Secondary, () => Hide(0.12f), "close", 30);
            UIK.Place((RectTransform)close.transform, UIK.TR, new Vector2(1, 1), new Vector2(-32, -20), new Vector2(240, 80));
            frame = UIK.Rect(Safe, "Frame");
            frame.anchorMin = new Vector2(0, 0); frame.anchorMax = new Vector2(1, 1);
            frame.offsetMin = new Vector2(32, 76); frame.offsetMax = new Vector2(-32, -116);
            var bg = UIK.Panel(frame, "Bg", Theme.Ink, "rr14");
            UIK.Stretch(bg.rectTransform);
            map = UIK.Img(frame, "Map", null, Color.white);
            map.preserveAspect = true;
            UIK.Stretch(map.rectTransform, 6, 6, 6, 6);
            legend = UIK.Txt(Safe, "Legend", "", Res.FontSemi, 24, Theme.TextDim, TextAnchor.MiddleLeft, false);
            UIK.Place(legend.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(40, 18), new Vector2(1800, 44));
            legend.supportRichText = true;
        }

        public void Set(MissionDef d)
        {
            title.text = "TAKTİK HARİTA  ·  GÖREV " + d.number.ToString("00") + "  ·  " + d.title;
            map.sprite = Res.Map(d.id);
            legend.text = "<color=#46C8F2>●</color> Atış yerleri     <color=#FF4545>●</color> Hedef bölgesi     " +
                          "<color=#FFFFFF>- -</color> Bilinen güzergâhlar     Halkalar: ana yuvadan 100 m aralıkla     Yukarısı: ana yuvanın baktığı yön";
        }
    }

    public class DialogScreen : UIScreen
    {
        Text title, message;
        Button yes, no;
        Action onYes, onNo;

        protected override void Build()
        {
            var dim = UIK.Img(transform, "Dim", null, new Color(0, 0, 0, 0.7f), false, true);
            UIK.Stretch(dim.rectTransform);
            var card = UIK.Panel(Safe, "Card", Theme.Panel, "rr32");
            UIK.Place(card.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 440));
            var ln = UIK.Panel(card.transform, "line", new Color(1, 1, 1, 0.1f), "rr32_line");
            UIK.Stretch(ln.rectTransform);
            title = UIK.Txt(card.transform, "Title", "", Res.FontBold, Theme.H3, Theme.Text, TextAnchor.UpperLeft);
            UIK.Place(title.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(48, -40), new Vector2(724, 60));
            message = UIK.Txt(card.transform, "Message", "", Res.FontBody, Theme.Body, Theme.TextDim, TextAnchor.UpperLeft);
            UIK.Place(message.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(48, -116), new Vector2(724, 170));
            no = UIK.Btn(card.transform, "No", "VAZGEÇ", BtnStyle.Ghost, () => { Hide(0.12f); var a = onNo; onNo = null; a?.Invoke(); });
            UIK.Place((RectTransform)no.transform, UIK.BL, new Vector2(0, 0), new Vector2(48, 40), new Vector2(340, 92));
            yes = UIK.Btn(card.transform, "Yes", "TAMAM", BtnStyle.Danger, () => { Hide(0.12f); onNo = null; onYes?.Invoke(); });
            UIK.Place((RectTransform)yes.transform, UIK.BR, new Vector2(1, 0), new Vector2(-48, 40), new Vector2(340, 92));
        }

        public void Set(string t, string m, string y, Action a, string n, Action onNoAction = null)
        {
            title.text = t;
            message.text = m;
            UIK.SetBtnLabel(yes, y);
            UIK.SetBtnLabel(no, n);
            onYes = a;
            onNo = onNoAction;
        }
    }
}

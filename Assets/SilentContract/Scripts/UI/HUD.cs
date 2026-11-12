using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>
    /// In-mission interface: look pad (drag / pinch), scope overlay with rangefinder, scan ring and ID card, markers
    /// (targets, protected people, armed men taking aim), objective list, wind / alarm panels, toasts, radio subtitles,
    /// fire-scope-reload-breath controls, round / vision / drone buttons, kill cam bars, the intro overlay (letterbox,
    /// title card, intel tags) and the first-contract tutorial. Every element sits in its own reserved screen region so
    /// nothing overlaps at any aspect ratio.
    /// </summary>
    public class HUD : UIScreen
    {
        MissionDef def;
        MissionRuntime rt;
        Sniper sn;

        // groups
        CanvasGroup scopeGroup, hipGroup, controlsGroup, panelsGroup, killGroup, scopedOnlyGroup;
        LookPad look;
        // scope
        Text rangeText, visionText;
        Image impactDot, scanRing, reticle, impactRing;
        Text impactZone;
        int impactZoneKey = -1;
        // bullet-drop marks (200..500 m) and windage dots (mils) on the reticle; the elevation turret
        readonly Image[] dropTick = new Image[4];
        readonly Text[] dropLabel = new Text[4];
        readonly Image[] milDot = new Image[6];
        Text zeroText;
        int lastZero = -1;
        // V2 dossier (who to look for) and the "that's him" button
        RectTransform scopeRootRt, dossierRt;
        class DossierRow { public NPC npc; public Text status; public Image portrait; }
        readonly List<DossierRow> dossier = new List<DossierRow>();
        Image tagBtn;
        Text tagText;
        CanvasGroup tagGroup;
        // V2 relocation: the button, the picker and the dark screen of the walk
        CanvasGroup moveGroup, pickGroup, relocGroup;
        RectTransform pickList;
        Text relocText, moveCount;
        float relocEnd;
        string relocName;
        RectTransform idCard;
        CanvasGroup idCardGroup;
        Image idPortrait, idTag;
        Text idName, idTagText, idHint;
        NPC idShown;
        float idLinger;
        // panels
        Text objTitle, objStatus, countdownText, windText, windSide, alarmText;
        Image windArrow, alarmBg, objAccent, objIcon;
        RectTransform objPanel, countdownChip, alarmChip;
        Image eyeBox;
        Vector2 eyeOff;
        Vector3 lastFwd;
        bool reticleLit;
        Image countdownBg;
        const int ObjRows = 4;
        readonly RectTransform[] objRow = new RectTransform[ObjRows];
        readonly Image[] objRowIcon = new Image[ObjRows];
        readonly Text[] objRowText = new Text[ObjRows];
        float objPanelH = 88f;
        // controls
        Image fireRing, breathRing, scopeIcon, reloadBg, ammoTypeIcon, ammoTypeBg, visionIcon, visionBg;
        Text ammoText, ammoTypeText, visionLabel;
        const int MaxPips = 10;
        readonly Image[] ammoPips = new Image[MaxPips];
        Slider zoom;
        bool zoomDragging;
        RectTransform fireRt, scopeRt, breathRt, reloadRt, zoomRt, ammoTypeRt, visionRt, droneRt;
        // markers
        RectTransform markerLayer;
        readonly List<Marker> markers = new List<Marker>();
        // toasts
        RectTransform toastLayer;
        readonly List<Toast> toasts = new List<Toast>();
        // kill cam
        Text killLabel;
        // quick title card (contracts without the cinematic intro) + hit icon
        CanvasGroup intro;
        Text introTitle, introSub;
        Image hitIcon;
        // radio subtitles
        RectTransform radioBox;
        CanvasGroup radioGroup;
        Text radioWho, radioText;
        readonly Queue<RadioMsg> radioQueue = new Queue<RadioMsg>();
        RadioMsg radioNow;
        float radioT;
        // cinematic intro overlay
        CanvasGroup cineGroup;
        Text cineKind, cineTitle, cineSub, cineSkip;
        RectTransform cineTags;
        readonly List<IntelTag> intelTags = new List<IntelTag>();
        // tutorial
        RectTransform coach;
        CanvasGroup coachGroup;
        Text coachText, coachStep;
        Image coachRing;
        int tutStep = -1;
        float tutTimer, dragAccum;
        // star challenges lost during the run (told once)
        bool[] challengeWarned = new bool[0];

        class Marker { public RectTransform rt; public Image icon; public Text label; public Image arrow; public Image ring; public CanvasGroup g; public int labelKey = int.MinValue; public NPC who; }
        // V3: the pin over the target's room (MissionDef.pin)
        RectTransform pinRt;
        CanvasGroup pinGroup;
        Image pinRing, pinIcon, pinArrow;
        Text pinTitle, pinPlace;
        class Toast { public RectTransform rt; public CanvasGroup g; public float t, life = 3f; }
        class RadioMsg { public string who, text; public float seconds; }
        class IntelTag { public NPC npc; public RectTransform rt; public CanvasGroup g; public Image pin; public Text name, role; public float showAt; public bool shown; }
        RectTransform distract;
        Image distractRing;
        CanvasGroup distractGroup;
        Image coachIcon;
        float tipUntil;
        bool tipActive;
        CombatHUD combat;
        Lessons lessons;
        RectTransform zeroRt, zeroUpRt, zeroDownRt, windRt, moveRt, tagRt;

        // ---- what the lessons (Lessons.cs) point at and read
        internal Sniper Sn => sn;
        internal MissionRuntime Runtime => rt;
        internal MissionDef Def => def;
        internal CombatHUD Combat => combat;
        internal RectTransform FireRt => fireRt;
        internal RectTransform ScopeRt => scopeRt;
        internal RectTransform BreathRt => breathRt;
        internal RectTransform ZoomRt => zoomRt;
        internal RectTransform AmmoTypeRt => ammoTypeRt;
        internal RectTransform VisionRt => visionRt;
        internal RectTransform DroneRt => droneRt;
        internal RectTransform ZeroUpRt => zeroUpRt;
        internal RectTransform ZeroDownRt => zeroDownRt;
        internal RectTransform WindRt => windRt;
        internal RectTransform MoveRt => moveRt;
        internal RectTransform TagRt => tagRt;
        internal bool ScopedUiShown => scopedOnlyGroup != null && scopedOnlyGroup.alpha > 0.6f;
        internal bool TagShown => tagGroup != null && tagGroup.alpha > 0.6f;
        internal bool MoveShown => moveGroup != null && moveGroup.alpha > 0.6f;
        internal bool AmmoTypeShown => ammoTypeRt != null && ammoTypeRt.gameObject.activeInHierarchy;
        internal bool VisionShown => visionRt != null && visionRt.gameObject.activeInHierarchy;
        internal bool DroneShown => droneRt != null && droneRt.gameObject.activeInHierarchy;
        internal float DragAccum => dragAccum;
        /// <summary>Top of the toast / coach column (canvas units below the safe area's top edge).</summary>
        internal float ColumnTop { get; private set; } = 196f;

        // ------------------------------------------------------------------ build
        protected override void Build()
        {
            sn = null;
            BuildLookPad();
            BuildScope();
            combat = CombatHUD.Create(Rt, Safe);
            BuildMarkers();
            BuildHip();
            BuildPanels();
            BuildControls();
            BuildToasts();
            BuildRadio();
            BuildKillCam();
            BuildHitFlash();
            BuildIntro();
            BuildCinematic();
            BuildCoach();
            lessons = Lessons.Create(this, Rt);
        }

        protected override void AfterBuild()
        {
            // above the controls: the lesson spotlight, intro card, kill cam letterbox, intro overlay, tutorial highlight
            lessons.transform.SetAsLastSibling();
            intro.transform.SetAsLastSibling();
            hitIcon.transform.SetAsLastSibling();
            killGroup.transform.SetAsLastSibling();
            cineGroup.transform.SetAsLastSibling();
            coachRing.transform.SetAsLastSibling();
        }

        void BuildLookPad()
        {
            var img = UIK.Img(transform, "LookPad", null, new Color(0, 0, 0, 0), false, true);
            UIK.Stretch(img.rectTransform);
            look = img.gameObject.AddComponent<LookPad>();
            look.hud = this;
        }

        void BuildScope()
        {
            var root = UIK.Rect(transform, "Scope");
            UIK.Stretch(root);
            scopeRootRt = root;
            scopeGroup = UIK.Group(root.gameObject);
            scopeGroup.blocksRaycasts = false;
            scopeGroup.interactable = false;
            var left = UIK.Img(root, "Left", null, Color.black);
            left.rectTransform.anchorMin = new Vector2(0, 0); left.rectTransform.anchorMax = new Vector2(0.5f, 1);
            left.rectTransform.offsetMin = Vector2.zero; left.rectTransform.offsetMax = new Vector2(-538, 0);
            var right = UIK.Img(root, "Right", null, Color.black);
            right.rectTransform.anchorMin = new Vector2(0.5f, 0); right.rectTransform.anchorMax = new Vector2(1, 1);
            right.rectTransform.offsetMin = new Vector2(538, 0); right.rectTransform.offsetMax = Vector2.zero;
            var mask = UIK.Img(root, "Mask", "scope_mask", Color.white);
            UIK.Place(mask.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1080));
            reticle = UIK.Img(root, "Reticle", "scope_reticle", new Color(1, 1, 1, 0.95f));
            UIK.Place(reticle.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1080));
            var glare = UIK.Img(root, "Glare", "scope_glare", new Color(1, 1, 1, 0.08f));
            UIK.Place(glare.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(-170, 190), new Vector2(520, 520));
            // V2: the eye is never exactly behind the tube — a soft shadow crescent slides in when the rifle moves
            eyeBox = UIK.Img(root, "EyeBox", "scope_eyebox", Color.white);
            UIK.Place(eyeBox.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1080));

            scanRing = UIK.Img(root, "ScanRing", "ring_thick", new Color(Theme.Info.r, Theme.Info.g, Theme.Info.b, 0.9f));
            scanRing.type = Image.Type.Filled;
            scanRing.fillMethod = Image.FillMethod.Radial360;
            scanRing.fillOrigin = (int)Image.Origin360.Top;
            scanRing.fillClockwise = true;
            UIK.Place(scanRing.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132, 132));

            impactDot = UIK.Img(root, "Impact", "circle", new Color(1f, 0.25f, 0.2f, 0.95f));
            UIK.Place(impactDot.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 14));
            var dotRing = UIK.Img(impactDot.transform, "Ring", "ring", new Color(1f, 0.25f, 0.2f, 0.7f));
            UIK.Place(dotRing.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            impactRing = dotRing;
            // where on a person it lands (head / body / arm / leg): handy for the take-alive contracts
            impactZone = UIK.Txt(impactDot.transform, "Zone", "", Res.FontBold, 20, new Color(1f, 0.85f, 0.8f, 0.95f), TextAnchor.UpperCenter, false);
            UIK.Place(impactZone.rectTransform, UIK.MC, new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(160, 26));
            var zo = impactZone.gameObject.AddComponent<Outline>();
            zo.effectColor = new Color(0, 0, 0, 0.9f);
            zo.effectDistance = new Vector2(1.4f, -1.4f);

            // holdover marks: where a bullet lands at 200 / 300 / 400 / 500 m with the rifle's current zero, and windage
            // dots every mil either side (the shooter holds off by what the wind panel says)
            var markCol = new Color(1f, 0.82f, 0.35f, 0.9f);
            for (int i = 0; i < dropTick.Length; i++)
            {
                dropTick[i] = UIK.Img(root, "Drop" + i, null, markCol);
                UIK.Place(dropTick[i].rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 3));
                dropLabel[i] = UIK.Txt(dropTick[i].transform, "L", ((i + 2)).ToString(), Res.FontBold, 18, markCol, TextAnchor.MiddleLeft, false);
                UIK.Place(dropLabel[i].rectTransform, UIK.MC, new Vector2(0f, 0.5f), new Vector2(24, 0), new Vector2(30, 22));
                dropTick[i].enabled = false; dropLabel[i].enabled = false;
            }
            for (int i = 0; i < milDot.Length; i++)
            {
                milDot[i] = UIK.Img(root, "Mil" + i, "circle", markCol);
                UIK.Place(milDot[i].rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
                milDot[i].enabled = false;
            }

            var rangeBg = UIK.Panel(root, "RangeBg", new Color(0, 0, 0, 0.55f), "pill");
            UIK.Place(rangeBg.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(360, 56));
            rangeText = UIK.Txt(rangeBg.transform, "Range", "", Res.FontMono, 30, new Color(0.75f, 1f, 0.8f), TextAnchor.MiddleCenter);
            UIK.Stretch(rangeText.rectTransform, 12, 0, 12, 0);
            // vision mode / special round readout above the reticle
            visionText = UIK.Txt(root, "VisionMode", "", Res.FontMono, 26, new Color(0.75f, 1f, 0.8f, 0.95f), TextAnchor.MiddleCenter, false);
            UIK.Place(visionText.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 340), new Vector2(520, 40));

            // ID card: below the reticle, between the hit icon and the range readout (top of the screen belongs to toasts/coach)
            idCard = UIK.Rect(root, "IdCard");
            UIK.Place(idCard, UIK.MC, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(430, 150));
            idCardGroup = UIK.Group(idCard.gameObject);
            var cbg = UIK.Panel(idCard, "Bg", new Color(0.04f, 0.06f, 0.08f, 0.9f), "rr14");
            UIK.Stretch(cbg.rectTransform);
            var frame = UIK.Panel(idCard, "Frame", Theme.Ink, "rr8");
            UIK.Place(frame.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(122, 122));
            idPortrait = UIK.Img(frame.transform, "Portrait", null, Color.white);
            idPortrait.preserveAspect = true;
            UIK.Stretch(idPortrait.rectTransform, 3, 3, 3, 3);
            idName = UIK.Txt(idCard, "Name", "", Res.FontBold, 32, Theme.Text, TextAnchor.UpperLeft);
            UIK.Place(idName.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(150, -12), new Vector2(266, 42));
            idTag = UIK.Panel(idCard, "Tag", Theme.Danger, "pill");
            UIK.Place(idTag.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(150, -58), new Vector2(170, 36));
            idTagText = UIK.Txt(idTag.transform, "T", "", Res.FontBold, 22, Color.white, TextAnchor.MiddleCenter);
            UIK.Stretch(idTagText.rectTransform, 6, 0, 6, 0);
            idHint = UIK.Txt(idCard, "Hint", "", Res.FontBody, 20, Theme.TextDim, TextAnchor.UpperLeft, true, 14);
            UIK.Place(idHint.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(150, -100), new Vector2(266, 46));
            idCardGroup.alpha = 0;
        }

        void BuildMarkers()
        {
            markerLayer = UIK.Rect(transform, "Markers");
            UIK.Stretch(markerLayer);
            for (int i = 0; i < 24; i++)
            {
                var m = new Marker();
                m.rt = UIK.Rect(markerLayer, "M" + i);
                m.rt.sizeDelta = new Vector2(60, 60);
                m.g = UIK.Group(m.rt.gameObject);
                m.ring = UIK.Img(m.rt, "Threat", "ring_thick", Theme.Danger);
                m.ring.type = Image.Type.Filled;
                m.ring.fillMethod = Image.FillMethod.Radial360;
                m.ring.fillOrigin = (int)Image.Origin360.Top;
                UIK.Place(m.ring.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58, 58));
                m.ring.enabled = false;
                m.icon = UIK.Img(m.rt, "Icon", "diamond", Theme.Danger);
                UIK.Place(m.icon.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
                UIK.AddShadow(m.icon, 2, 0.5f);
                m.label = UIK.Txt(m.rt, "Label", "", Res.FontBold, 24, Color.white, TextAnchor.LowerCenter, false);
                UIK.Place(m.label.rectTransform, UIK.MC, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(220, 30));
                var lo = m.label.gameObject.AddComponent<Outline>();
                lo.effectColor = new Color(0, 0, 0, 0.85f);
                lo.effectDistance = new Vector2(1.6f, -1.6f);
                m.arrow = UIK.Img(m.rt, "Arrow", "tri_right", Theme.Danger);
                UIK.Place(m.arrow.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
                m.rt.gameObject.SetActive(false);
                markers.Add(m);
            }
            // suggested distraction shot ("shoot here to lure the target out")
            distract = UIK.Rect(markerLayer, "Distract");
            distract.sizeDelta = new Vector2(80, 80);
            distractGroup = UIK.Group(distract.gameObject);
            distractRing = UIK.Img(distract, "Ring", "ring", Theme.Accent);
            UIK.Place(distractRing.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            var di = UIK.Icon(distract, "sound", Theme.Accent, 34);
            UIK.Place(di.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
            UIK.AddShadow(di, 2, 0.6f);
            var dl = UIK.Txt(distract, "Label", "BURAYA ATEŞ ET", Res.FontBold, 22, Theme.Accent, TextAnchor.LowerCenter, false);
            UIK.Place(dl.rectTransform, UIK.MC, new Vector2(0.5f, 0), new Vector2(0, 38), new Vector2(240, 28));
            var dlo = dl.gameObject.AddComponent<Outline>();
            dlo.effectColor = new Color(0, 0, 0, 0.85f);
            dlo.effectDistance = new Vector2(1.6f, -1.6f);
            distract.gameObject.SetActive(false);

            // the target's room: a pin with HEDEF and the contract's place ("Kordon Plaza — 11. kat")
            pinRt = UIK.Rect(markerLayer, "Pin");
            pinRt.sizeDelta = new Vector2(90, 90);
            pinGroup = UIK.Group(pinRt.gameObject);
            pinGroup.blocksRaycasts = false;
            pinRing = UIK.Img(pinRt, "Ring", "ring", Theme.Danger);
            UIK.Place(pinRing.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
            pinIcon = UIK.Icon(pinRt, "pin", Theme.Danger, 40);
            UIK.Place(pinIcon.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            UIK.AddShadow(pinIcon, 2, 0.6f);
            pinArrow = UIK.Img(pinRt, "Arrow", "tri_right", Theme.Danger);
            UIK.Place(pinArrow.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 38));
            pinTitle = UIK.Txt(pinRt, "Title", "HEDEF", Res.FontBold, 24, Theme.Danger, TextAnchor.LowerCenter, false);
            UIK.Place(pinTitle.rectTransform, UIK.MC, new Vector2(0.5f, 0), new Vector2(0, 42), new Vector2(360, 30));
            pinPlace = UIK.Txt(pinRt, "Place", "", Res.FontSemi, 20, Color.white, TextAnchor.UpperCenter, false);
            UIK.Place(pinPlace.rectTransform, UIK.MC, new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(420, 28));
            foreach (var tx in new[] { pinTitle, pinPlace })
            {
                var o = tx.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.9f);
                o.effectDistance = new Vector2(1.6f, -1.6f);
            }
            pinRt.gameObject.SetActive(false);
        }

        void BuildHip()
        {
            var root = UIK.Rect(transform, "Hip");
            UIK.Stretch(root);
            hipGroup = UIK.Group(root.gameObject);
            hipGroup.blocksRaycasts = false;
            var ch = UIK.Icon(root, "crosshair", new Color(1, 1, 1, 0.85f), 46);
            UIK.Place(ch.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
            UIK.AddShadow(ch, 1.5f, 0.6f);
            hitIcon = UIK.Icon(transform, "skull", Theme.Danger, 64);
            UIK.Place(hitIcon.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -96), new Vector2(64, 64));
            hitIcon.color = new Color(1, 1, 1, 0);
        }

        void BuildPanels()
        {
            var root = UIK.Rect(Safe, "Panels");
            UIK.Stretch(root);
            panelsGroup = UIK.Group(root.gameObject);

            var pause = UIK.Btn(root, "Pause", null, BtnStyle.Icon, () => ui.ShowPause(), "pause");
            UIK.Place((RectTransform)pause.transform, UIK.TL, new Vector2(0, 1), new Vector2(20, -20), new Vector2(88, 88));

            // HUD panels sit on a bright 3D scene and uGUI blends in linear space: keep them nearly opaque so text stays crisp
            var hudPanel = new Color(0.045f, 0.06f, 0.085f, 0.9f);
            var obj = UIK.Panel(root, "Objective", hudPanel, "rr14");
            objPanel = obj.rectTransform;
            UIK.Place(objPanel, UIK.TL, new Vector2(0, 1), new Vector2(124, -20), new Vector2(560, 88));
            objAccent = UIK.Img(obj.transform, "Accent", "rr8", Theme.Danger, true);
            objAccent.rectTransform.anchorMin = new Vector2(0, 1); objAccent.rectTransform.anchorMax = new Vector2(0, 1);
            objAccent.rectTransform.pivot = new Vector2(0, 1);
            objAccent.rectTransform.anchoredPosition = new Vector2(10, -12);
            objAccent.rectTransform.sizeDelta = new Vector2(8, 64);
            objIcon = UIK.Icon(obj.transform, "target", Theme.Danger, 40);
            UIK.Place(objIcon.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(30, -24), new Vector2(40, 40));
            objTitle = UIK.Txt(obj.transform, "Title", "", Res.FontBold, 30, Theme.Text, TextAnchor.UpperLeft);
            UIK.Place(objTitle.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(86, -8), new Vector2(460, 40));
            objStatus = UIK.Txt(obj.transform, "Status", "", Res.FontSemi, 24, Theme.TextDim, TextAnchor.UpperLeft);
            UIK.Place(objStatus.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(86, -48), new Vector2(460, 32));
            // further lines of the contract (protect, disable, bonus)
            for (int i = 0; i < ObjRows; i++)
            {
                var row = UIK.Rect(obj.transform, "Line" + i);
                UIK.Place(row, UIK.TL, new Vector2(0, 1), new Vector2(30, -92 - i * 40), new Vector2(510, 36));
                objRowIcon[i] = UIK.Icon(row, "check", Theme.TextDim, 26);
                UIK.Place(objRowIcon[i].rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(26, 26));
                objRowText[i] = UIK.Txt(row, "T", "", Res.FontSemi, 24, Theme.TextDim, TextAnchor.MiddleLeft);
                UIK.Stretch(objRowText[i].rectTransform, 56, 0, 4, 0);
                objRow[i] = row;
                row.gameObject.SetActive(false);
            }

            countdownBg = UIK.Panel(root, "Countdown", new Color(0.55f, 0.08f, 0.06f, 0.92f), "rr14");
            countdownChip = countdownBg.rectTransform;
            UIK.Place(countdownChip, UIK.TL, new Vector2(0, 1), new Vector2(124, -120), new Vector2(560, 56));
            var ci = UIK.Icon(countdownChip, "timer", Color.white, 32);
            UIK.Place(ci.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(32, 32));
            countdownText = UIK.Txt(countdownChip, "T", "", Res.FontSemi, 27, Color.white, TextAnchor.MiddleLeft);
            UIK.Stretch(countdownText.rectTransform, 62, 0, 16, 0);

            var wind = UIK.Panel(root, "Wind", hudPanel, "rr14");
            UIK.Place(wind.rectTransform, UIK.TR, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(370, 88));
            windRt = wind.rectTransform;
            windArrow = UIK.Icon(wind.transform, "arrow_left", Theme.Info, 48);
            UIK.Place(windArrow.rectTransform, UIK.ML, new Vector2(0.5f, 0.5f), new Vector2(46, 0), new Vector2(48, 48));
            windText = UIK.Txt(wind.transform, "Speed", "", Res.FontBold, 30, Theme.Text, TextAnchor.UpperLeft);
            UIK.Place(windText.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(88, -8), new Vector2(270, 40));
            windSide = UIK.Txt(wind.transform, "Side", "", Res.FontSemi, 22, Theme.TextDim, TextAnchor.UpperLeft);
            UIK.Place(windSide.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(84, -48), new Vector2(280, 32));

            alarmBg = UIK.Panel(root, "Alarm", Theme.Danger, "rr14");
            alarmChip = alarmBg.rectTransform;
            UIK.Place(alarmChip, UIK.TR, new Vector2(1, 1), new Vector2(-20, -120), new Vector2(270, 56));
            var ai = UIK.Icon(alarmChip, "alert", Color.white, 32);
            UIK.Place(ai.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(32, 32));
            alarmText = UIK.Txt(alarmChip, "T", "", Res.FontBold, 27, Color.white, TextAnchor.MiddleLeft);
            UIK.Stretch(alarmText.rectTransform, 58, 0, 12, 0);

            // recon drone (gadget): one use per contract, under the wind panel
            var drone = UIK.Btn(root, "Drone", "DRON", BtnStyle.Secondary, () => { if (rt != null && rt.UseDrone()) Tween.Pop(droneRt, 0.3f); }, "drone", 26);
            droneRt = (RectTransform)drone.transform;
            UIK.Place(droneRt, UIK.TR, new Vector2(1, 1), new Vector2(-20, -188), new Vector2(170, 72));
            droneRt.gameObject.SetActive(false);
        }

        Image Circle(RectTransform parent, string name, float size, string icon, Color bg, Color fg, Vector2 anchor, Vector2 pos, out RectTransform r)
        {
            var b = UIK.Img(parent, name, "circle", bg, false, true);
            r = b.rectTransform;
            UIK.Place(r, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var ring = UIK.Img(b.transform, "Edge", "ring", new Color(1, 1, 1, 0.35f));
            UIK.Stretch(ring.rectTransform);
            if (!string.IsNullOrEmpty(icon))
            {
                var ic = UIK.Icon(b.transform, icon, fg, size * 0.42f);
                ic.name = "Icon";
                UIK.Place(ic.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.42f, size * 0.42f));
            }
            return b;
        }

        void BuildControls()
        {
            var root = UIK.Rect(Safe, "Controls");
            UIK.Stretch(root);
            controlsGroup = UIK.Group(root.gameObject);
            var dark = new Color(0.05f, 0.07f, 0.09f, 0.72f);

            // ---- right thumb: fire (big), scope toggle above, reload to the left, ammo above reload
            var fire = Circle(root, "Fire", 220, "fire", new Color(0.75f, 0.12f, 0.08f, 0.82f), Color.white, UIK.BR, new Vector2(-150, 170), out fireRt);
            fireRing = UIK.Img(fire.transform, "Bolt", "ring_thick", new Color(1, 1, 1, 0.85f));
            fireRing.type = Image.Type.Filled; fireRing.fillMethod = Image.FillMethod.Radial360; fireRing.fillOrigin = (int)Image.Origin360.Top;
            UIK.Stretch(fireRing.rectTransform, -6, -6, -6, -6);
            var fh = fire.gameObject.AddComponent<HoldButton>();
            fh.onDown = () => { if (sn != null) sn.Fire(); };

            var scope = Circle(root, "Scope", 136, "scope", dark, Color.white, UIK.BR, new Vector2(-150, 404), out scopeRt);
            scopeIcon = scope.transform.Find("Icon").GetComponent<Image>();
            var sh = scope.gameObject.AddComponent<HoldButton>();
            sh.onDown = () => { if (sn != null) { sn.ToggleScope(); OnTutorialEvent(); } };

            reloadBg = Circle(root, "Reload", 112, "reload", dark, Color.white, UIK.BR, new Vector2(-366, 104), out reloadRt);
            var rh = reloadBg.gameObject.AddComponent<HoldButton>();
            rh.onDown = () => { if (sn != null) sn.Reload(); };

            var ammo = UIK.Rect(root, "Ammo");
            UIK.Place(ammo, UIK.BR, new Vector2(0.5f, 0), new Vector2(-366, 172), new Vector2(150, 80));
            var ammoBg = UIK.Panel(ammo, "Bg", new Color(0.045f, 0.06f, 0.085f, 0.82f), "rr14");
            UIK.Stretch(ammoBg.rectTransform, -6, -4, -6, -2);
            for (int i = 0; i < MaxPips; i++)
            {
                ammoPips[i] = UIK.Img(ammo, "Pip" + i, "rr8", Theme.Accent, true);
                UIK.Place(ammoPips[i].rectTransform, UIK.BC, new Vector2(0.5f, 0), new Vector2((i - 2) * 22, 8), new Vector2(12, 32));
            }
            ammoText = UIK.Txt(ammo, "Text", "", Res.FontBold, 24, Theme.Text, TextAnchor.LowerCenter, false);
            UIK.Place(ammoText.rectTransform, UIK.BC, new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(200, 30));

            // round selector (only with special rounds): above the ammo box
            ammoTypeBg = Circle(root, "AmmoType", 104, null, dark, Color.white, UIK.BR, new Vector2(-366, 318), out ammoTypeRt);
            ammoTypeIcon = UIK.Icon(ammoTypeBg.transform, "bullet", Color.white, 40);
            UIK.Place(ammoTypeIcon.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(40, 40));
            ammoTypeText = UIK.Txt(ammoTypeBg.transform, "Count", "", Res.FontBold, 20, Color.white, TextAnchor.MiddleCenter, false);
            UIK.Place(ammoTypeText.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -26), new Vector2(110, 26));
            var ah = ammoTypeBg.gameObject.AddComponent<HoldButton>();
            ah.onDown = () => { if (sn != null) sn.CycleAmmo(); };

            // vision mode (night vision / thermal): left of the scope button
            visionBg = Circle(root, "Vision", 104, null, dark, Color.white, UIK.BR, new Vector2(-312, 430), out visionRt);
            visionIcon = UIK.Icon(visionBg.transform, "eye", Color.white, 40);
            UIK.Place(visionIcon.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(40, 40));
            visionLabel = UIK.Txt(visionBg.transform, "Mode", "", Res.FontBold, 18, Color.white, TextAnchor.MiddleCenter, false);
            UIK.Place(visionLabel.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -26), new Vector2(110, 24));
            var vh = visionBg.gameObject.AddComponent<HoldButton>();
            vh.onDown = () => { if (sn != null) sn.CycleVision(); };

            // V2: "that's him" — tag the scanned stranger as the target
            var tg = UIK.Rect(root, "Tag");
            tagRt = tg;
            UIK.Place(tg, UIK.MC, new Vector2(0f, 0.5f), new Vector2(232, -240), new Vector2(300, 84));   // beside the ID card (right): the card's hint and the radio stay readable
            tagGroup = UIK.Group(tg.gameObject);
            tagBtn = UIK.Panel(tg, "Bg", new Color(0.75f, 0.12f, 0.08f, 0.88f), "pill");
            tagBtn.raycastTarget = true;
            UIK.Stretch(tagBtn.rectTransform);
            tagText = UIK.Txt(tg, "T", "İŞARETLE — HEDEF BU", Res.FontBold, 26, Color.white, TextAnchor.MiddleCenter, false);
            UIK.Stretch(tagText.rectTransform, 10, 0, 10, 0);
            var tgh = tagBtn.gameObject.AddComponent<HoldButton>();
            tgh.onDown = () => { if (rt != null && sn != null && idShown != null && idShown == sn.Hovered && tagGroup.alpha > 0.5f) rt.Tag(idShown); };
            tagGroup.alpha = 0f;
            tagGroup.blocksRaycasts = false;

            // V2: move to another place (only when looking over the rifle, not through the scope)
            var mv = Circle(root, "Move", 112, "map", dark, Color.white, UIK.BL, new Vector2(140, 160), out moveRt);
            moveGroup = UIK.Group(mv.gameObject);
            var ml = UIK.Txt(mv.transform, "Label", "YER DEĞİŞTİR", Res.FontBold, 17, Color.white, TextAnchor.MiddleCenter, false);
            UIK.Place(ml.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -74), new Vector2(170, 24));
            moveCount = UIK.Txt(mv.transform, "Count", "", Res.FontBold, 18, Theme.Accent, TextAnchor.MiddleCenter, false);
            UIK.Place(moveCount.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(44, 44), new Vector2(40, 24));
            var mh = mv.gameObject.AddComponent<HoldButton>();
            mh.onDown = () => OpenPicker(true);
            moveGroup.alpha = 0f; moveGroup.blocksRaycasts = false;

            // ---- left thumb (scoped only): hold breath + zoom slider above it
            var scoped = UIK.Rect(root, "ScopedOnly");
            UIK.Stretch(scoped);
            scopedOnlyGroup = UIK.Group(scoped.gameObject);
            var breath = Circle(scoped, "Breath", 168, "breath", dark, Color.white, UIK.BL, new Vector2(140, 160), out breathRt);
            breathRing = UIK.Img(breath.transform, "Stamina", "ring_thick", Theme.Info);
            breathRing.type = Image.Type.Filled; breathRing.fillMethod = Image.FillMethod.Radial360; breathRing.fillOrigin = (int)Image.Origin360.Top;
            UIK.Stretch(breathRing.rectTransform, -6, -6, -6, -6);
            var bl = UIK.Txt(breath.transform, "Label", "NEFES", Res.FontBold, 22, Color.white, TextAnchor.MiddleCenter, false);
            UIK.Place(bl.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -44), new Vector2(140, 28));
            var bicon = breath.transform.Find("Icon");
            if (bicon != null) ((RectTransform)bicon).anchoredPosition = new Vector2(0, 12);
            var bh = breath.gameObject.AddComponent<HoldButton>();
            bh.onDown = () => sn?.SetBreathButton(true);
            bh.onUp = () => sn?.SetBreathButton(false);

            // elevation turret: the range the rifle is zeroed at
            var zr = UIK.Rect(scoped, "Zero");
            zeroRt = zr;
            UIK.Place(zr, UIK.BL, new Vector2(0.5f, 0), new Vector2(300, 96), new Vector2(104, 250));
            var zrbg = UIK.Panel(zr, "Bg", new Color(0.05f, 0.07f, 0.09f, 0.72f), "rr22");
            UIK.Stretch(zrbg.rectTransform);
            var zl = UIK.Txt(zr, "Label", "MENZİL", Res.FontBold, 18, Theme.TextDim, TextAnchor.MiddleCenter, false);
            UIK.Place(zl.rectTransform, UIK.TC, new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(120, 24));
            zeroText = UIK.Txt(zr, "Value", "100", Res.FontMono, 30, new Color(1f, 0.82f, 0.35f), TextAnchor.MiddleCenter, false);
            UIK.Place(zeroText.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104, 40));
            foreach (int dir in new[] { 1, -1 })
            {
                int d = dir;
                var b = UIK.Panel(zr, dir > 0 ? "Up" : "Down", new Color(1, 1, 1, 0.1f), "rr14");
                b.raycastTarget = true;
                UIK.Place(b.rectTransform, dir > 0 ? UIK.TC : UIK.BC, new Vector2(0.5f, dir > 0 ? 1f : 0f), new Vector2(0, dir > 0 ? -8 : 8), new Vector2(88, 84));
                var t = UIK.Txt(b.transform, "T", dir > 0 ? "+" : "−", Res.FontBold, 46, Color.white, TextAnchor.MiddleCenter, false);
                UIK.Stretch(t.rectTransform);
                var hb = b.gameObject.AddComponent<HoldButton>();
                hb.onDown = () => sn?.StepZero(d);
                if (dir > 0) zeroUpRt = b.rectTransform; else zeroDownRt = b.rectTransform;
            }

            zoomRt = UIK.Rect(scoped, "Zoom");
            UIK.Place(zoomRt, UIK.BL, new Vector2(0.5f, 0), new Vector2(140, 300), new Vector2(84, 440));
            var zbg = UIK.Panel(zoomRt, "Bg", new Color(0.05f, 0.07f, 0.09f, 0.6f), "pill");
            UIK.Stretch(zbg.rectTransform);
            var zin = UIK.Icon(zoomRt, "zoom_in", Color.white, 34);
            UIK.Place(zin.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(34, 34));
            var zout = UIK.Icon(zoomRt, "zoom_out", Color.white, 34);
            UIK.Place(zout.rectTransform, UIK.BC, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(34, 34));
            var track = UIK.Rect(zoomRt, "Track");
            UIK.Stretch(track, 0, 66, 0, 66);
            zoom = BuildVerticalSlider(track);
            zoom.onValueChanged.AddListener(v => { if (sn != null) sn.SetZoom(v); });
            var zev = zoom.gameObject.AddComponent<EventTrigger>();
            AddTrigger(zev, EventTriggerType.PointerDown, _ => zoomDragging = true);
            AddTrigger(zev, EventTriggerType.PointerUp, _ => zoomDragging = false);
        }

        static void AddTrigger(EventTrigger t, EventTriggerType type, System.Action<BaseEventData> a)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(d => a(d));
            t.triggers.Add(e);
        }

        static Slider BuildVerticalSlider(RectTransform parent)
        {
            var root = UIK.Rect(parent, "Slider");
            UIK.Stretch(root);
            var bg = UIK.Panel(root, "TrackBg", new Color(1, 1, 1, 0.18f), "pill");
            bg.rectTransform.anchorMin = new Vector2(0.5f, 0); bg.rectTransform.anchorMax = new Vector2(0.5f, 1);
            bg.rectTransform.sizeDelta = new Vector2(10, 0);
            bg.raycastTarget = true;
            var fillArea = UIK.Rect(root, "FillArea");
            fillArea.anchorMin = new Vector2(0.5f, 0); fillArea.anchorMax = new Vector2(0.5f, 1);
            fillArea.sizeDelta = new Vector2(10, -20);
            var fill = UIK.Panel(fillArea, "Fill", Theme.Accent, "pill");
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(1, 0);
            fill.rectTransform.sizeDelta = new Vector2(0, 20);
            var handleArea = UIK.Rect(root, "HandleArea");
            UIK.Stretch(handleArea, 0, 20, 0, 20);
            var knob = UIK.Img(handleArea, "Knob", "circle", Color.white, false, true);
            knob.rectTransform.sizeDelta = new Vector2(52, 52);
            var hit = UIK.Img(root, "HitArea", null, new Color(0, 0, 0, 0), false, true);
            UIK.Stretch(hit.rectTransform, -10, 0, -10, 0);
            hit.transform.SetAsFirstSibling();
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = knob.rectTransform;
            s.targetGraphic = knob;
            s.direction = Slider.Direction.BottomToTop;
            s.transition = Selectable.Transition.None;
            var nav = s.navigation; nav.mode = Navigation.Mode.None; s.navigation = nav;
            return s;
        }

        void BuildToasts()
        {
            toastLayer = UIK.Rect(Safe, "Toasts");
            toastLayer.anchorMin = new Vector2(0.5f, 1); toastLayer.anchorMax = new Vector2(0.5f, 1);
            toastLayer.pivot = new Vector2(0.5f, 1);
            toastLayer.anchoredPosition = new Vector2(0, -196);
            toastLayer.sizeDelta = new Vector2(900, 240);
        }

        /// <summary>Radio subtitles: bottom centre, between the thumb controls.</summary>
        void BuildRadio()
        {
            radioBox = UIK.Rect(Safe, "Radio");
            UIK.Place(radioBox, UIK.BC, new Vector2(0.5f, 0), new Vector2(-40, 30), new Vector2(820, 112));
            radioGroup = UIK.Group(radioBox.gameObject);
            radioGroup.blocksRaycasts = false;
            var bg = UIK.Panel(radioBox, "Bg", new Color(0.02f, 0.03f, 0.04f, 0.86f), "rr14");
            UIK.Stretch(bg.rectTransform);
            var edge = UIK.Img(radioBox, "Edge", "rr8", Theme.Info, true);
            edge.rectTransform.anchorMin = new Vector2(0, 0); edge.rectTransform.anchorMax = new Vector2(0, 1);
            edge.rectTransform.pivot = new Vector2(0, 0.5f);
            edge.rectTransform.offsetMin = new Vector2(10, 14); edge.rectTransform.offsetMax = new Vector2(16, -14);
            var ic = UIK.Icon(radioBox, "sound", Theme.Info, 30);
            UIK.Place(ic.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(30, -14), new Vector2(30, 30));
            radioWho = UIK.Txt(radioBox, "Who", "", Res.FontBold, 24, Theme.Info, TextAnchor.UpperLeft, false);
            UIK.Place(radioWho.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(70, -14), new Vector2(720, 30));
            radioText = UIK.Txt(radioBox, "Text", "", Res.FontBody, 27, Theme.Text, TextAnchor.UpperLeft, true, 18);
            radioText.rectTransform.anchorMin = new Vector2(0, 0); radioText.rectTransform.anchorMax = new Vector2(1, 1);
            radioText.rectTransform.offsetMin = new Vector2(30, 10); radioText.rectTransform.offsetMax = new Vector2(-22, -46);
            radioGroup.alpha = 0;
        }

        CanvasGroup hitFlash, hurtEdge;
        static Sprite redEdge;

        /// <summary>A red edge (clear in the middle, so the target stays visible): darker towards the corners.</summary>
        static Sprite RedEdge()
        {
            if (redEdge != null) return redEdge;
            const int w = 192, h = 108;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.85f + v * v * 1.15f);          // 0 middle … ~1.4 corners
                    float a = Mathf.Clamp01((d - 0.55f) / 0.6f);
                    a = a * a * (3f - 2f * a);
                    px[y * w + x] = new Color32(150, 6, 6, (byte)(a * 235f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            redEdge = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return redEdge;
        }

        /// <summary>Hit by an enemy round: the edges of the screen flash blood red (the middle stays clear); while hurt a
        /// faint red edge stays and fades as the health comes back.</summary>
        void BuildHitFlash()
        {
            var img = UIK.Img(transform, "HitFlash", null, Color.white);
            img.sprite = RedEdge();
            UIK.Stretch(img.rectTransform);
            img.raycastTarget = false;
            hitFlash = UIK.Group(img.gameObject);
            hitFlash.blocksRaycasts = false;
            hitFlash.alpha = 0f;
            var edge = UIK.Img(transform, "HurtEdge", null, new Color(1f, 1f, 1f, 1f));
            edge.sprite = RedEdge();
            UIK.Stretch(edge.rectTransform);
            edge.raycastTarget = false;
            hurtEdge = UIK.Group(edge.gameObject);
            hurtEdge.blocksRaycasts = false;
            hurtEdge.alpha = 0f;
        }

        void OnPlayerHit()
        {
            if (hitFlash == null) return;
            hitFlash.transform.SetAsLastSibling();
            Tween.Stop(hitFlash);
            hitFlash.alpha = 1f;
            Tween.Fade(hitFlash, 0f, 0.9f);
            Util.Vibrate();
        }

        void BuildKillCam()
        {
            var root = UIK.Rect(transform, "KillCam");
            UIK.Stretch(root);
            killGroup = UIK.Group(root.gameObject);
            killGroup.blocksRaycasts = false;
            var top = UIK.Img(root, "Top", null, Color.black);
            top.rectTransform.anchorMin = new Vector2(0, 1); top.rectTransform.anchorMax = new Vector2(1, 1);
            top.rectTransform.pivot = new Vector2(0.5f, 1); top.rectTransform.sizeDelta = new Vector2(0, 118);
            var bot = UIK.Img(root, "Bottom", null, Color.black);
            bot.rectTransform.anchorMin = new Vector2(0, 0); bot.rectTransform.anchorMax = new Vector2(1, 0);
            bot.rectTransform.pivot = new Vector2(0.5f, 0); bot.rectTransform.sizeDelta = new Vector2(0, 118);
            killLabel = UIK.Txt(root, "Label", "", Res.FontBold, 44, Theme.Accent, TextAnchor.MiddleLeft);
            killLabel.rectTransform.anchorMin = new Vector2(0, 1); killLabel.rectTransform.anchorMax = new Vector2(1, 1);
            killLabel.rectTransform.pivot = new Vector2(0, 1);
            killLabel.rectTransform.offsetMin = new Vector2(90, -110); killLabel.rectTransform.offsetMax = new Vector2(-90, -10);
            var skip = UIK.Txt(root, "Skip", "Geçmek için dokun", Res.FontSemi, 26, Theme.TextDim, TextAnchor.MiddleRight);
            skip.rectTransform.anchorMin = new Vector2(0, 0); skip.rectTransform.anchorMax = new Vector2(1, 0);
            skip.rectTransform.pivot = new Vector2(1, 0);
            skip.rectTransform.offsetMin = new Vector2(90, 10); skip.rectTransform.offsetMax = new Vector2(-90, 108);
            killGroup.alpha = 0;
        }

        void BuildIntro()
        {
            var root = UIK.Rect(transform, "Intro");
            UIK.Place(root, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1100, 170));
            intro = UIK.Group(root.gameObject);
            intro.blocksRaycasts = false;
            var bg = UIK.Img(root, "Bg", "shadow", new Color(0, 0, 0, 0.75f), true);
            UIK.Stretch(bg.rectTransform, -60, -40, -60, -40);
            introTitle = UIK.Txt(root, "Title", "", Res.FontBold, 64, Theme.Text, TextAnchor.UpperCenter);
            UIK.Place(introTitle.rectTransform, UIK.TC, new Vector2(0.5f, 1), Vector2.zero, new Vector2(1100, 80));
            introSub = UIK.Txt(root, "Sub", "", Res.FontSemi, 32, Theme.Accent, TextAnchor.UpperCenter);
            UIK.Place(introSub.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(1100, 48));
            intro.alpha = 0;
        }

        /// <summary>Intro overlay: letterbox, contract title card bottom-left, intel tags pinned on people.</summary>
        void BuildCinematic()
        {
            var root = UIK.Rect(transform, "Cinematic");
            UIK.Stretch(root);
            cineGroup = UIK.Group(root.gameObject);
            cineGroup.blocksRaycasts = false;
            cineTags = UIK.Rect(root, "Tags");
            UIK.Stretch(cineTags);
            var top = UIK.Img(root, "Top", null, Color.black);
            top.rectTransform.anchorMin = new Vector2(0, 1); top.rectTransform.anchorMax = new Vector2(1, 1);
            top.rectTransform.pivot = new Vector2(0.5f, 1); top.rectTransform.sizeDelta = new Vector2(0, 104);
            var bot = UIK.Img(root, "Bottom", null, Color.black);
            bot.rectTransform.anchorMin = new Vector2(0, 0); bot.rectTransform.anchorMax = new Vector2(1, 0);
            bot.rectTransform.pivot = new Vector2(0.5f, 0); bot.rectTransform.sizeDelta = new Vector2(0, 104);
            var card = UIK.Rect(root, "Card");
            card.anchorMin = new Vector2(0, 0); card.anchorMax = new Vector2(0, 0);
            card.pivot = new Vector2(0, 0);
            card.anchoredPosition = new Vector2(96, 132);
            card.sizeDelta = new Vector2(1100, 210);
            var shade = UIK.Img(card, "Shade", "shadow", new Color(0, 0, 0, 0.6f), true);
            UIK.Stretch(shade.rectTransform, -50, -30, -50, -30);
            var bar = UIK.Img(card, "Bar", null, Theme.Accent);
            UIK.Place(bar.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(0, -6), new Vector2(110, 5));
            cineKind = UIK.Txt(card, "Kind", "", Res.FontSemi, 30, Theme.Accent, TextAnchor.UpperLeft, false);
            UIK.Place(cineKind.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(0, -20), new Vector2(1100, 40));
            cineTitle = UIK.Txt(card, "Title", "", Res.FontBold, 96, Theme.Text, TextAnchor.UpperLeft, false);
            UIK.Place(cineTitle.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(-4, -58), new Vector2(1100, 112));
            UIK.AddShadow(cineTitle, 3f, 0.7f);
            cineSub = UIK.Txt(card, "Sub", "", Res.FontSemi, 30, Theme.TextDim, TextAnchor.UpperLeft, false);
            UIK.Place(cineSub.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(2, -170), new Vector2(1100, 40));
            cineSkip = UIK.Txt(root, "Skip", "Geçmek için dokun", Res.FontSemi, 26, Theme.TextDim, TextAnchor.MiddleRight, false);
            cineSkip.rectTransform.anchorMin = new Vector2(0, 1); cineSkip.rectTransform.anchorMax = new Vector2(1, 1);
            cineSkip.rectTransform.pivot = new Vector2(1, 1);
            cineSkip.rectTransform.offsetMin = new Vector2(90, -100); cineSkip.rectTransform.offsetMax = new Vector2(-90, -8);
            cineGroup.alpha = 0;
            root.gameObject.SetActive(false);
        }

        void BuildCoach()
        {
            coach = UIK.Rect(Safe, "Coach");
            coach.anchorMin = new Vector2(0.5f, 1); coach.anchorMax = new Vector2(0.5f, 1);
            coach.pivot = new Vector2(0.5f, 1);
            coach.anchoredPosition = new Vector2(0, -196);
            coach.sizeDelta = new Vector2(820, 100);
            coachGroup = UIK.Group(coach.gameObject);
            coachGroup.blocksRaycasts = false;
            var bg = UIK.Panel(coach, "Bg", new Color(0.04f, 0.06f, 0.08f, 0.92f), "rr14");
            UIK.Stretch(bg.rectTransform);
            var ln = UIK.Panel(coach, "Line", new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.6f), "rr14_line");
            UIK.Stretch(ln.rectTransform);
            coachStep = UIK.Txt(coach, "Step", "", Res.FontBold, 26, Theme.Accent, TextAnchor.MiddleCenter);
            UIK.Place(coachStep.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(70, 60));
            coachIcon = UIK.Icon(coach, "sound", Theme.Accent, 44);
            UIK.Place(coachIcon.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(44, 44));
            coachIcon.enabled = false;
            coachText = UIK.Txt(coach, "Text", "", Res.FontBody, 28, Theme.Text, TextAnchor.MiddleLeft, true, 18);
            UIK.Stretch(coachText.rectTransform, 96, 8, 20, 8);
            coachRing = UIK.Img(transform, "CoachRing", "glow_ring", Theme.Accent);
            coachRing.raycastTarget = false;
            coachGroup.alpha = 0;
            coachRing.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ lifecycle
        public void Begin(MissionDef d)
        {
            def = d;
            rt = game.Runtime;
            sn = game.Sniper;
            rt.Toast -= OnToast; rt.Toast += OnToast;
            rt.Killed -= OnKilled; rt.Killed += OnKilled;
            rt.Finished -= OnFinished; rt.Finished += OnFinished;
            rt.Tip -= OnTip; rt.Tip += OnTip;
            rt.RadioLine -= OnRadio; rt.RadioLine += OnRadio;
            rt.PlayerHit -= OnPlayerHit; rt.PlayerHit += OnPlayerHit;
            combat.Begin(rt, sn);
            sn.Changed -= OnSniperChanged; sn.Changed += OnSniperChanged;
            Cinematic.StateChanged -= OnCinematic; Cinematic.StateChanged += OnCinematic;
            tipActive = false;
            pendingTips.Clear();
            coachIcon.enabled = false;
            zoomDragging = false;
            droneCheckAt = 0f;
            ammoCountAt = 0f;
            ResetTextCache();
            KillCam.StateChanged -= OnKillCam; KillCam.StateChanged += OnKillCam;
            foreach (var t in toasts) if (t.rt != null) Destroy(t.rt.gameObject);
            toasts.Clear();
            radioQueue.Clear();
            radioNow = null;
            radioGroup.alpha = 0;
            idShown = null;
            idCardGroup.alpha = 0;
            killGroup.alpha = 0;
            if (hitFlash != null) hitFlash.alpha = 0f;
            if (hurtEdge != null) hurtEdge.alpha = 0f;
            hitIcon.color = new Color(1, 1, 1, 0);
            challengeWarned = new bool[rt.Challenges.Count];
            OnSniperChanged();
            zoom.SetValueWithoutNotify(sn.Zoom);
            var t0 = d.targets.Count > 0 ? d.targets[0] : null;
            introTitle.text = "GÖREV " + d.number.ToString("00") + "  ·  " + d.title;
            introSub.text = t0 != null ? "Hedef: " + t0.name + (string.IsNullOrEmpty(t0.alias) ? "" : "  “" + t0.alias + "”") : d.location;
            intro.alpha = 0;
            if (d.noIntro) Tween.Fade(intro, 1f, 0.35f, () => Tween.Fade(intro, 0f, 0.6f, null, 2.4f));
            // the first-use lessons replace the old six-step tutorial of contract 1 (Lessons.cs)
            tutStep = -1;
            dragAccum = 0;
            tutTimer = 0;
            ShowCoach(false);
            lessons.Begin(d);
            PrepareCinematic();
            BuildDossier();
            BuildRelocUI();
            OpenPicker(false);
            if (relocGroup != null) { relocGroup.alpha = 0f; relocGroup.gameObject.SetActive(false); }
            rt.RelocateStarted -= OnRelocateStarted; rt.RelocateStarted += OnRelocateStarted;
            rt.RelocateDone -= OnRelocateDone; rt.RelocateDone += OnRelocateDone;
        }

        /// <summary>V2: the place picker and the dark screen shown while the shooter moves.</summary>
        void BuildRelocUI()
        {
            if (pickGroup != null) return;
            var pk = UIK.Rect(Safe, "NestPicker");
            UIK.Place(pk, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(760, 520));
            pickGroup = UIK.Group(pk.gameObject);
            var bg = UIK.Panel(pk, "Bg", new Color(0.03f, 0.04f, 0.05f, 0.95f), "rr22");
            UIK.Stretch(bg.rectTransform);
            var t = UIK.Txt(pk, "Title", "YER DEĞİŞTİR", Res.FontBold, 34, Theme.Accent, TextAnchor.MiddleLeft, false);
            UIK.Place(t.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(28, -16), new Vector2(500, 48));
            var sub = UIK.Txt(pk, "Sub", "Taşınırken dünya durmaz. Yeni yerde seni kimse bilmez.", Res.FontBody, 22, Theme.TextDim, TextAnchor.MiddleLeft, false);
            UIK.Place(sub.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(28, -62), new Vector2(700, 30));
            var close = UIK.Btn(pk, "Close", "VAZGEÇ", BtnStyle.Secondary, () => OpenPicker(false), null, 26);
            UIK.Place((RectTransform)close.transform, UIK.BC, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(260, 72));
            pickList = UIK.Rect(pk, "List");
            UIK.Place(pickList, UIK.TL, new Vector2(0, 1), new Vector2(24, -104), new Vector2(712, 300));
            pickGroup.alpha = 0f; pickGroup.blocksRaycasts = false;
            pk.gameObject.SetActive(false);

            var ov = UIK.Rect(transform, "Relocating");
            UIK.Stretch(ov);
            relocGroup = UIK.Group(ov.gameObject);
            var dk = UIK.Img(ov, "Dark", null, new Color(0f, 0f, 0f, 0.92f), false, true);
            UIK.Stretch(dk.rectTransform);
            relocText = UIK.Txt(ov, "T", "", Res.FontBold, 40, Theme.Text, TextAnchor.MiddleCenter, true);
            UIK.Place(relocText.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 200));
            relocGroup.alpha = 0f; relocGroup.blocksRaycasts = false;
            ov.gameObject.SetActive(false);
        }

        void OpenPicker(bool on)
        {
            if (pickGroup == null || rt == null) return;
            if (on && !rt.CanRelocate) return;
            var go = pickGroup.gameObject;
            if (on)
            {
                for (int i = pickList.childCount - 1; i >= 0; i--) Destroy(pickList.GetChild(i).gameObject);
                float y = 0f;
                foreach (var n in rt.Nests)
                {
                    if (n == rt.Nest) continue;
                    var nn = n;
                    string label = n.name + "   ·   " + Mathf.RoundToInt(n.dist) + " m";
                    var b = UIK.Btn(pickList, "N_" + n.id, label, BtnStyle.Secondary, () => { OpenPicker(false); rt.Relocate(nn); }, NestIcon(n.kind), 28);
                    UIK.Place((RectTransform)b.transform, UIK.TL, new Vector2(0, 1), new Vector2(0, y), new Vector2(712, 64));
                    var d = UIK.Txt(pickList, "D_" + n.id, n.desc ?? "", Res.FontBody, 20, Theme.TextDim, TextAnchor.UpperLeft, true, 14);
                    UIK.Place(d.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(12, y - 68), new Vector2(690, 28));
                    y -= 104f;
                }
                go.SetActive(true);
                pickGroup.alpha = 1f; pickGroup.blocksRaycasts = true;
                sn?.SetBreathButton(false);
            }
            else
            {
                pickGroup.alpha = 0f; pickGroup.blocksRaycasts = false;
                go.SetActive(false);
            }
        }

        static string NestIcon(string kind) => kind == "window" ? "office" : (kind == "crane" ? "map" : (kind == "slope" ? "ski" : "scope"));

        void OnRelocateStarted(LayoutNest to, float secs)
        {
            relocEnd = Time.time + secs;
            relocName = to.name;
            relocGroup.gameObject.SetActive(true);
            Tween.Fade(relocGroup, 1f, 0.2f);
        }

        void OnRelocateDone(LayoutNest to)
        {
            Tween.Fade(relocGroup, 0f, 0.35f, () => { if (relocGroup != null && relocGroup.alpha <= 0.01f) relocGroup.gameObject.SetActive(false); });
        }

        void UpdateReloc(float dt)
        {
            if (moveGroup == null || rt == null) return;
            bool can = rt.CanRelocate && sn != null && !sn.Scoped;
            float a = Mathf.MoveTowards(moveGroup.alpha, can ? 1f : 0f, dt * 6f);
            moveGroup.alpha = a;
            moveGroup.blocksRaycasts = can;
            int left = MissionRuntime.MaxRelocations - rt.Relocations;
            string c = left.ToString();
            if (moveCount.text != c) moveCount.text = c;
            if (pickGroup != null && pickGroup.gameObject.activeSelf && !rt.CanRelocate) OpenPicker(false);
            if (rt.Relocating && relocGroup != null)
            {
                int s = Mathf.Max(1, Mathf.CeilToInt(relocEnd - Time.time));
                relocText.text = "YER DEĞİŞTİRİYORSUN\n<size=30>" + UIK.Up(relocName ?? "") + "  ·  " + s + " sn</size>";
            }
            else if (relocGroup != null && relocGroup.gameObject.activeSelf && relocGroup.alpha >= 0.99f) OnRelocateDone(null);
        }

        /// <summary>V2: the dossier beside the scope — the face and the clues of every target the player has to find.</summary>
        void BuildDossier()
        {
            if (dossierRt != null) Destroy(dossierRt.gameObject);
            dossier.Clear();
            dossierRt = null;
            if (rt == null || !rt.IdMode) return;
            var list = new List<NPC>();
            foreach (var t in rt.Targets) if (t != null && t.NeedsId && list.Count < 2) list.Add(t);
            if (list.Count == 0) return;
            float hw = ((RectTransform)transform).rect.width * 0.5f;
            bool narrow = hw < 1000f;
            float w = 330f, rowH = 196f;
            dossierRt = UIK.Rect(scopeRootRt, "Dossier");
            // top right, under the wind panel: the left edge belongs to the zoom slider, MENZİL and NEFES (test 1: the
            // dossier sat on the zoom knob on iPhone)
            float k = Screen.width > 0 ? ((RectTransform)transform).rect.width / Screen.width : 1f;
            float insetR = Mathf.Max(0f, (Screen.width - Screen.safeArea.xMax) * k);
            float insetT = Mathf.Max(0f, (Screen.height - Screen.safeArea.yMax) * k);
            float sc = narrow ? 0.85f : (list.Count > 1 ? 1.0f : 1.2f);   // readable on a phone (test 2: 0.9 was too small)
            UIK.Place(dossierRt, UIK.TR, new Vector2(1, 1), new Vector2(-(insetR + 40f), -(insetT + 150f)),
                      new Vector2(w, 54f + rowH * list.Count));
            dossierRt.localScale = Vector3.one * sc;
            var bg = UIK.Panel(dossierRt, "Bg", new Color(0.03f, 0.04f, 0.05f, 0.88f), "rr14");
            UIK.Stretch(bg.rectTransform);
            var title = UIK.Txt(dossierRt, "Title", "DOSYA", Res.FontBold, 24, Theme.Accent, TextAnchor.MiddleLeft, false);
            UIK.Place(title.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(16, -8), new Vector2(w - 32, 36));
            for (int i = 0; i < list.Count; i++)
            {
                var n = list[i];
                var info = def != null ? def.targets.Find(x => x.charId == n.CharId) : null;
                float y = -48f - rowH * i;
                var frame = UIK.Panel(dossierRt, "Frame" + i, Theme.Ink, "rr8");
                UIK.Place(frame.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(14, y), new Vector2(118, 118));
                var pic = UIK.Img(frame.transform, "P", null, Color.white);
                pic.sprite = Res.Portrait(n.CharId);
                pic.preserveAspect = true;
                if (pic.sprite == null) pic.color = new Color(1, 1, 1, 0);
                UIK.Stretch(pic.rectTransform, 3, 3, 3, 3);
                string alias = !string.IsNullOrEmpty(n.Alias) ? "“" + n.Alias + "”" : (info != null && !string.IsNullOrEmpty(info.alias) ? "“" + info.alias + "”" : "");
                var nm = UIK.Txt(dossierRt, "Name" + i, n.DisplayName + (alias.Length > 0 ? "\n" + alias : ""), Res.FontBold, 22, Theme.Text, TextAnchor.UpperLeft, true);
                UIK.Place(nm.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(142, y), new Vector2(w - 152, 58));
                var st = UIK.Txt(dossierRt, "Status" + i, "ARANIYOR", Res.FontBold, 18, Theme.Accent, TextAnchor.UpperLeft, false);
                UIK.Place(st.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(142, y - 62), new Vector2(w - 152, 24));
                string clue = !string.IsNullOrEmpty(n.Hint) ? n.Hint : (info != null ? info.desc : "");
                var cl = UIK.Txt(dossierRt, "Clue" + i, clue ?? "", Res.FontBody, 18, Theme.TextDim, TextAnchor.UpperLeft, true, 13);
                UIK.Place(cl.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(14, y - 124), new Vector2(w - 28, rowH - 128));
                dossier.Add(new DossierRow { npc = n, status = st, portrait = pic });
            }
        }

        void UpdateDossier(float dt)
        {
            foreach (var r in dossier)
            {
                var n = r.npc;
                string s; Color c;
                if (n == null || n.Dead) { s = "ETKİSİZ"; c = Theme.Success; }
                else if (n.Incapacitated) { s = "ETKİSİZ"; c = Theme.Success; }
                else if (n.Escaped) { s = "KAÇTI"; c = Theme.Danger; }
                else if (n.Confirmed) { s = "DOĞRULANDI"; c = Theme.Danger; }
                else { s = "ARANIYOR"; c = Theme.Accent; }
                if (r.status.text != s) { r.status.text = s; r.status.color = c; }
            }
            // the button: a scanned stranger in the scope who could be the one
            bool show = sn != null && sn.Scoped && rt != null && idShown != null && idShown == sn.Hovered && idCardGroup.alpha > 0.5f && rt.CanTag(idShown);
            float a = Mathf.MoveTowards(tagGroup.alpha, show ? 1f : 0f, dt * 8f);
            tagGroup.alpha = a;
            tagGroup.blocksRaycasts = show;
            if (show)
            {
                bool locked = rt.TagLocked;
                tagBtn.color = locked ? new Color(0.3f, 0.32f, 0.35f, 0.88f) : new Color(0.75f, 0.12f, 0.08f, 0.88f);
                string want = locked ? "BEKLE…" : "İŞARETLE — HEDEF BU";
                if (tagText.text != want) tagText.text = want;
            }
        }

        /// <summary>Rifle, round or vision mode changed: reticle, zoom range, buttons.</summary>
        void OnSniperChanged()
        {
            if (sn == null) return;
            var w = sn.Weapon;
            var rs = Res.Sprite(w.reticle);
            if (rs == null) rs = Res.Sprite("scope_reticle");
            // V2: at night the middle of the reticle is lit red (normal sight only; the night and thermal views have their own)
            var env = game != null && game.Env != null ? game.Env.Current : null;
            reticleLit = env != null && env.night && sn.Vision == VisionMode.Normal;
            var lit = reticleLit ? Res.Sprite((w.reticle ?? "scope_reticle") + "_lit") : null;
            reticle.sprite = lit != null ? lit : rs;
            zoom.minValue = sn.MinZoom;
            zoom.maxValue = sn.MaxZoom;
            zoom.SetValueWithoutNotify(sn.Zoom);
            var a = Gear.AmmoOf(sn.AmmoType);
            ammoTypeIcon.sprite = Res.Icon(sn.AmmoType == AmmoKind.Standard ? "bullet" : a.icon);
            ammoTypeIcon.color = a.tint;
            ammoTypeBg.color = sn.AmmoType == AmmoKind.Standard ? new Color(0.05f, 0.07f, 0.09f, 0.72f) : new Color(a.tint.r * 0.35f, a.tint.g * 0.35f, a.tint.b * 0.35f, 0.88f);
            visionIcon.sprite = Res.Icon(sn.Vision == VisionMode.Night ? "nightvision" : (sn.Vision == VisionMode.Thermal ? "thermal" : "eye"));
            visionLabel.text = sn.Vision == VisionMode.Night ? "GECE" : (sn.Vision == VisionMode.Thermal ? "TERMAL" : "NORMAL");
            visionBg.color = sn.Vision == VisionMode.Normal ? new Color(0.05f, 0.07f, 0.09f, 0.72f) :
                             (sn.Vision == VisionMode.Night ? new Color(0.06f, 0.32f, 0.1f, 0.9f) : new Color(0.38f, 0.38f, 0.4f, 0.9f));
        }

        /// <summary>Mission hint in the coach box (the first-time tutorial keeps priority).</summary>
        // hints that came while a lesson had the top of the screen: shown once it is over (most are one-time hints)
        readonly List<KeyValuePair<string, float>> pendingTips = new List<KeyValuePair<string, float>>();

        void OnTip(string text, float seconds)
        {
            if (rt == null || rt.State != MissionRuntime.Phase.Playing) return;
            if (tutStep >= 0 || (lessons != null && lessons.Active) || Hot)
            {
                if (pendingTips.Count < 4) pendingTips.Add(new KeyValuePair<string, float>(text, seconds));
                return;
            }
            coachStep.text = "";
            coachIcon.enabled = true;
            coachText.text = text;
            coach.sizeDelta = new Vector2(860, 118);
            tipActive = true;
            tipUntil = Time.unscaledTime + seconds;
            Tween.Fade(coachGroup, 1f, 0.25f);
            Sfx.UI(Sfx.Scan);
        }

        void UpdateTip()
        {
            bool lesson = lessons != null && lessons.Active;
            if (!tipActive)
            {
                // a lesson is over: the hints it held back, one after the other
                if (!lesson && !Hot && pendingTips.Count > 0 && rt != null && rt.State == MissionRuntime.Phase.Playing && !KillCam.Active && !Cinematic.Active)
                {
                    var t = pendingTips[0];
                    pendingTips.RemoveAt(0);
                    OnTip(t.Key, t.Value);
                }
                return;
            }
            // a lesson takes the top of the screen (or the shooting starts): the hint makes way and comes back later
            if (lesson || Hot)
            {
                tipActive = false;
                Tween.Fade(coachGroup, 0f, 0.2f);
                if (pendingTips.Count < 4) pendingTips.Insert(0, new KeyValuePair<string, float>(coachText.text, Mathf.Max(4f, tipUntil - Time.unscaledTime)));
                return;
            }
            if (Time.unscaledTime > tipUntil || rt.State != MissionRuntime.Phase.Playing || KillCam.Active)
            {
                tipActive = false;
                Tween.Fade(coachGroup, 0f, 0.3f);
            }
        }

        void OnFinished(bool success)
        {
            tipActive = false;
            ShowCoach(false);
            if (lessons != null) lessons.End();
            if (sn != null)
            {
                sn.SetBreathButton(false);
                sn.SetScoped(false);
                sn.InputEnabled = false;
            }
            ui.ShowResult();
        }

        void OnKillCam(bool on, string label)
        {
            killLabel.text = on ? (KillCam.IsCutaway ? label : "KURŞUN KAMERASI  ·  " + label) : "";
            Tween.Fade(killGroup, on ? 1f : 0f, on ? 0.2f : 0.3f);
        }

        void OnKilled(NPC n, bool headshot)
        {
            hitIcon.sprite = Res.Icon(headshot ? "headshot" : "skull");
            hitIcon.color = n.IsTarget ? Theme.Danger : (n.Role == Role.Guard || n.Role == Role.Bodyguard ? Theme.Guard : Color.white);
            var c = hitIcon.color;
            Tween.Pop(hitIcon.transform, 0.3f, 0f, 1.6f);
            Tween.Run(hitIcon, 1.4f, k => { if (hitIcon) hitIcon.color = new Color(c.r, c.g, c.b, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f); });
        }

        // V3 (device test): in a firefight a pile of lines came up over the middle of the screen. Now: the same line
        // never twice within a few seconds; while under fire one short line at a time (the newest replaces the last) and
        // no "guard down" news; the hint box waits until the shooting is over; the column stays above the reticle.
        readonly Dictionary<string, float> toastSeen = new Dictionary<string, float>();

        /// <summary>The nest is found or rounds are coming in: the middle of the screen belongs to the fight.</summary>
        static bool Hot => Firefight.I != null && !Firefight.I.Dead && (Firefight.I.Located || Firefight.I.UnderFire);

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.06f;

        void OnToast(string text, Color color, string icon)
        {
            if (string.IsNullOrEmpty(text)) return;
            float now = Time.unscaledTime;
            if (toastSeen.TryGetValue(text, out var seenAt) && now - seenAt < 6f) return;
            toastSeen[text] = now;
            bool hot = Hot;
            // a kill confirmation of a guard is the one piece of side news dropped in a fight (the hit icon says it)
            if (hot && Near(color, Theme.Guard) && text.Contains("etkisiz")) return;
            // a first-time lesson card holds the top of the screen: only the urgent lines join it
            if (lessons != null && lessons.CardVisible && !Near(color, Theme.Danger)) return;
            if (hot)
            {
                foreach (var o in toasts) if (o.rt != null) Destroy(o.rt.gameObject);
                toasts.Clear();
            }
            var r = UIK.Rect(toastLayer, "Toast");
            r.anchorMin = new Vector2(0.5f, 1); r.anchorMax = new Vector2(0.5f, 1);
            r.pivot = new Vector2(0.5f, 1);
            var bg = UIK.Panel(r, "Bg", new Color(0.03f, 0.05f, 0.07f, 0.9f), "pill");
            UIK.Stretch(bg.rectTransform);
            var edge = UIK.Panel(r, "Edge", new Color(color.r, color.g, color.b, 0.75f), "pill_line");
            UIK.Stretch(edge.rectTransform);
            float x = 26;
            if (!string.IsNullOrEmpty(icon))
            {
                var ic = UIK.Icon(r, icon, color, 34);
                UIK.Place(ic.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(34, 34));
                x = 70;
            }
            var t = UIK.Txt(r, "T", text, Res.FontSemi, 26, Color.white, TextAnchor.MiddleLeft, true, 18);
            float w = Mathf.Min(860, x + t.preferredWidth + 34);
            r.sizeDelta = new Vector2(Mathf.Max(240, w), 54);
            UIK.Stretch(t.rectTransform, x, 0, 24, 0);
            var g = UIK.Group(r.gameObject);
            g.alpha = 0;
            Tween.Fade(g, 1f, 0.18f);
            toasts.Insert(0, new Toast { rt = r, g = g, t = 0, life = hot ? 2.2f : 2.8f });
            while (toasts.Count > (hot ? 1 : 2))
            {
                var old = toasts[toasts.Count - 1];
                toasts.RemoveAt(toasts.Count - 1);
                if (old.rt != null) Destroy(old.rt.gameObject);
            }
        }

        void OnRadio(string who, string text, float seconds)
        {
            radioQueue.Enqueue(new RadioMsg { who = who, text = Loc.Tr(text), seconds = Mathf.Max(2.5f, seconds) });
            while (radioQueue.Count > 3) radioQueue.Dequeue();
        }

        protected override void OnHide()
        {
            ShowCoach(false);
            if (lessons != null) lessons.End();
            if (sn != null) sn.SetBreathButton(false);
            cineGroup.gameObject.SetActive(false);
            zoomDragging = false;
            if (game != null && game.Post != null) { game.Post.extraVignette = 0f; game.Post.hurt = 0f; }
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            if (sn == null || rt == null || !Visible) return;
            float dt = Time.unscaledDeltaTime;
            bool kill = KillCam.Active;
            bool cine = Cinematic.Active || rt.State == MissionRuntime.Phase.Intro;
            bool playing = rt.State == MissionRuntime.Phase.Playing;
            float scopeK = Mathf.MoveTowards(scopeGroup.alpha, sn.Scoped && !kill && !cine ? 1f : 0f, dt * 9f);
            scopeGroup.alpha = scopeK;
            UpdateEyeBox(dt);
            hipGroup.alpha = kill || cine ? 0 : 1f - scopeK;
            float uiK = kill || !playing || (Firefight.I != null && Firefight.I.Dead) ? 0f : 1f;
            controlsGroup.alpha = Mathf.MoveTowards(controlsGroup.alpha, uiK, dt * 6f);
            controlsGroup.blocksRaycasts = uiK > 0.5f;
            panelsGroup.alpha = Mathf.MoveTowards(panelsGroup.alpha, kill || cine ? 0f : 1f, dt * 6f);
            panelsGroup.blocksRaycasts = !kill && playing;
            scopedOnlyGroup.alpha = Mathf.MoveTowards(scopedOnlyGroup.alpha, sn.Scoped ? 1f : 0f, dt * 8f);
            scopedOnlyGroup.blocksRaycasts = sn.Scoped && uiK > 0.5f;
            if (game.Post != null)
            {
                game.Post.extraVignette = (kill ? 0.5f : (cine ? 0.35f : (sn.HoldingBreath ? 0.35f : 0f))) + (kill ? 0f : sn.CoverBlend * 0.3f);
                // hurt: the colour drains and the edges darken
                var ff = Firefight.I;
                game.Post.hurt = ff != null && !kill && (playing || ff.Dead) ? Mathf.Max(ff.Hurt * 0.55f, ff.Dead ? 1f : 0f) : 0f;
                if (hurtEdge != null) hurtEdge.alpha = ff != null && !kill && playing && !ff.Dead ? Mathf.Clamp01(ff.Hurt * 0.75f) : 0f;
            }

            UpdateScope(dt);
            UpdateDossier(dt);
            UpdateReloc(dt);
            UpdateMarkers(kill || cine);
            UpdatePanels();
            UpdateControls();
            UpdateToasts(dt);
            UpdateRadio(dt);
            UpdateCinematic(dt);
            UpdateChallenges();
            UpdateTutorial(dt);
            lessons.Tick(dt, kill || cine);
            UpdateTip();
            // the kill cam is the shot's own moment: no hints, radio lines or toasts over it
            if (kill) { coachGroup.alpha = 0f; radioGroup.alpha = 0f; }
            if (toastVeil == null) toastVeil = toastLayer.GetComponent<CanvasGroup>();
            if (toastVeil == null) { toastVeil = toastLayer.gameObject.AddComponent<CanvasGroup>(); toastVeil.blocksRaycasts = false; }
            toastVeil.alpha = Mathf.MoveTowards(toastVeil.alpha, kill ? 0f : 1f, dt * 8f);
        }
        CanvasGroup toastVeil;

        /// <summary>The eye-box shadow: lags behind a turn of the rifle and drifts a little with the breath.</summary>
        void UpdateEyeBox(float dt)
        {
            if (eyeBox == null || sn == null || sn.Cam == null) return;
            var cam = sn.Cam.transform;
            var fwd = cam.forward;
            if (dt > 0f && lastFwd.sqrMagnitude > 0.5f && sn.Scoped)
            {
                var d = fwd - lastFwd;
                var v = new Vector2(Vector3.Dot(d, cam.right), Vector3.Dot(d, cam.up)) / dt;   // radians per second
                float t = Time.unscaledTime;
                var idle = new Vector2(Mathf.Sin(t * 0.7f) * 6f, Mathf.Sin(t * 0.53f + 1.3f) * 5f);
                var want = Vector2.ClampMagnitude(-v * 420f * Mathf.Max(1f, sn.Zoom / 6f), 70f) + idle;
                eyeOff = Vector2.Lerp(eyeOff, want, 1f - Mathf.Exp(-dt * 7f));
            }
            else eyeOff = Vector2.Lerp(eyeOff, Vector2.zero, 1f - Mathf.Exp(-dt * 7f));
            lastFwd = fwd;
            eyeBox.rectTransform.anchoredPosition = eyeOff;
        }

        Vector2 ScreenToCanvas(Vector3 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, null, out var p);
            return p;
        }

        /// <summary>Holdover marks on the reticle for the current zero and zoom, windage dots one mil apart, the turret.</summary>
        void UpdateHoldover()
        {
            if (sn.ZeroRange != lastZero) { lastZero = sn.ZeroRange; zeroText.text = sn.ZeroRange.ToString(); }
            var cam = sn.Cam;
            bool on = sn.Diff.dropMarks && cam != null;
            var cp = cam != null ? cam.transform.position : Vector3.zero;
            var f = cam != null ? cam.transform.forward : Vector3.forward;
            var up = cam != null ? cam.transform.up : Vector3.up;
            var right = cam != null ? cam.transform.right : Vector3.right;
            float vel = sn.CurrentSpec.velocity;
            var c0 = on ? ScreenToCanvas(sn.ToScreen(cp + f * 100f)) : Vector2.zero;
            for (int i = 0; i < dropTick.Length; i++)
            {
                float R = 200f + 100f * i;
                bool show = on;
                if (show)
                {
                    float h = Difficulty.DropAt(R, sn.ZeroRange, vel);
                    var p = ScreenToCanvas(sn.ToScreen(cp + f * R + up * h)) - c0;
                    show = Mathf.Abs(p.y) > 9f && Mathf.Abs(p.y) < 400f;
                    if (show) dropTick[i].rectTransform.anchoredPosition = new Vector2(0f, p.y);
                }
                dropTick[i].enabled = show;
                dropLabel[i].enabled = show;
            }
            // one mil = 0.1 m at 100 m
            float mil = on ? (ScreenToCanvas(sn.ToScreen(cp + f * 100f + right * 0.1f)) - c0).x : 0f;
            for (int i = 0; i < milDot.Length; i++)
            {
                int k = (i / 2 + 1) * (i % 2 == 0 ? 1 : -1);
                float x = mil * k;
                bool show = on && Mathf.Abs(mil) > 7f && Mathf.Abs(x) < 400f;
                milDot[i].enabled = show;
                if (show) milDot[i].rectTransform.anchoredPosition = new Vector2(x, 0f);
            }
        }

        void UpdateScope(float dt)
        {
            if (!sn.Scoped)
            {
                idCardGroup.alpha = Mathf.MoveTowards(idCardGroup.alpha, 0, dt * 8f);
                if (idCardGroup.alpha <= 0.001f) { idShown = null; idLinger = 0f; }
                return;
            }
            int rk = sn.RangeToCenter >= 0 ? Mathf.RoundToInt(sn.RangeToCenter) : -1;
            int zk = Mathf.RoundToInt(sn.Zoom * 10f);
            if (rk != lastRange || zk != lastZoom10)
            {
                lastRange = rk; lastZoom10 = zk;
                rangeText.text = (rk >= 0 ? rk + " m" : "--- m") + "     " + (zk / 10) + "." + (zk % 10) + "×";
            }
            int vk = (int)sn.Vision * 10 + (int)sn.AmmoType;
            if (vk != lastVisionKey)
            {
                lastVisionKey = vk;
                string vm = sn.Vision == VisionMode.Night ? "GECE GÖRÜŞÜ" : (sn.Vision == VisionMode.Thermal ? "TERMAL" : "");
                string am = sn.AmmoType == AmmoKind.Standard ? "" : Gear.AmmoOf(sn.AmmoType).name;
                visionText.text = vm.Length > 0 && am.Length > 0 ? vm + "  ·  " + am : vm + am;
                visionText.color = sn.AmmoType == AmmoKind.Standard ? new Color(0.75f, 1f, 0.8f, 0.95f) : Gear.AmmoOf(sn.AmmoType).tint;
            }
            // predicted impact point (aim assist). On a person it is drawn on him as he stands now: a walker is hit only
            // when the rifle points the right amount ahead of him, and then the dot sits on his body.
            bool showDot = false;
            int zoneKey = -1;
            UpdateHoldover();
            // a lesson about the drop or the wind shows the round's own flight, without the scope's help
            bool rawDot = lessons.ForceDot;
            var shown = rawDot ? sn.RawPrediction : sn.AssistPrediction;
            if ((sn.ShowImpact || rawDot) && sn.HasPrediction && shown.hit)
            {
                var pred = shown;
                var who = pred.Npc;
                var s = sn.ToScreen(pred.bodyPoint);           // where it lands as things stand now (a person, a car, a wall)
                if (s.z > 0)
                {
                    var p = ScreenToCanvas(s);
                    if (p.magnitude < 420f)
                    {
                        impactDot.rectTransform.anchoredPosition = p;
                        // on a body: shown even dead centre (leading a runner puts the crosshair ahead of the dot)
                        showDot = p.magnitude > 3f || who != null;
                        if (who != null && !who.Dead)
                        {
                            var part = pred.Part;
                            zoneKey = (int)MissionRuntime.ZoneOf(part, pred.point);
                        }
                    }
                }
            }
            impactDot.enabled = showDot;
            impactDot.transform.GetChild(0).gameObject.SetActive(showDot);
            if (!showDot) zoneKey = -1;
            if (zoneKey != impactZoneKey)
            {
                impactZoneKey = zoneKey;
                impactZone.text = zoneKey < 0 ? "" : ZoneName((BodyZone)zoneKey);
                var c = zoneKey < 0 ? new Color(1f, 0.25f, 0.2f, 0.7f) : new Color(1f, 0.12f, 0.08f, 1f);
                impactRing.color = c;
                impactRing.rectTransform.sizeDelta = zoneKey < 0 ? new Vector2(30, 30) : new Vector2(38, 38);
            }
            // scan ring
            var h = sn.Hovered;
            bool scanning = h != null && !h.Identified;
            scanRing.enabled = scanning;
            if (scanning) scanRing.fillAmount = sn.ScanProgress;
            // ID card
            if (h != null && h.Identified) { if (idShown != h) SetCard(h); else RefreshCardStatus(h); idLinger = 0.5f; }
            else idLinger -= dt;
            float want = idShown != null && idLinger > 0 ? 1f : 0f;
            idCardGroup.alpha = Mathf.MoveTowards(idCardGroup.alpha, want, dt * 7f);
            if (idCardGroup.alpha <= 0.001f) idShown = null;
        }

        static string ZoneName(BodyZone z)
        {
            switch (z)
            {
                case BodyZone.Head: return "KAFA";
                case BodyZone.Arm: return "KOL";
                case BodyZone.Leg: return "BACAK";
                case BodyZone.Pelvis: return "KARIN";
                default: return "GÖVDE";
            }
        }

        void SetCard(NPC n)
        {
            idShown = n;
            idPortrait.sprite = Res.Portrait(n.CharId);
            idPortrait.color = idPortrait.sprite != null ? Color.white : new Color(1, 1, 1, 0);
            idName.text = n.DisplayName ?? "Bilinmiyor";
            RefreshCardStatus(n);
        }

        /// <summary>V2: a stranger in the scope is a face, not a name, until the player says who he is.</summary>
        bool Unknown(NPC n) => rt != null && rt.PendingId && !n.Protected && !n.HiddenTarget && !n.Confirmed &&
                               n.Role != Role.Guard && n.Role != Role.Bodyguard && !(n.Armed && !n.IsTarget);

        void RefreshCardStatus(NPC n)
        {
            if (Unknown(n))
            {
                idName.text = n.TaggedWrong ? (n.Role == Role.Decoy ? "Dublör" : "Sivil") : "Kimliği belirsiz";
                idTag.color = n.TaggedWrong ? Theme.TextMute : new Color(0.35f, 0.38f, 0.42f);
                idTagText.text = n.TaggedWrong ? "HEDEF DEĞİL" : "TARANDI";
                idTagText.color = Color.white;
                idHint.text = n.TaggedWrong ? "Masum — ateş etme" : "Dosyadaki yüzle karşılaştır — emin olunca İŞARETLE";
                return;
            }
            if (rt != null && rt.IdMode && n.Confirmed) idName.text = n.DisplayName ?? "Hedef";
            bool guard = n.Role == Role.Guard || n.Role == Role.Bodyguard;
            Color c = n.Protected ? Theme.Info : (n.IsTarget ? Theme.Danger : (guard ? Theme.Guard : (n.Armed ? Theme.Guard : Theme.TextMute)));
            string label = n.RoleLabel ?? "";
            if (!n.IsTarget && !guard && n.Armed) label = "SİLAHLI";
            if (n.Sedated) label = "BAYGIN";
            else if (n.Down) label = "YERDE";
            idTag.color = c;
            idTagText.text = label;
            idTagText.color = n.IsTarget || guard || n.Protected || n.Armed ? Color.white : Theme.Text;
            string hint;
            if (n.Protected) hint = !string.IsNullOrEmpty(n.Hint) ? n.Hint : "Koru — ona bir şey olmamalı";
            else if (n.HiddenTarget) hint = "Silahlı — sivil kılığında";
            else if (n.IsTarget) hint = string.IsNullOrEmpty(n.Alias) ? "Sözleşme hedefi" : "“" + n.Alias + "” — sözleşme hedefi";
            else hint = !string.IsNullOrEmpty(n.Hint) ? n.Hint : (guard ? "Silahlı — vurulabilir" : "Masum — ateş etme");
            if (n.Wounded && !n.Dead) hint = "Yaralı — " + hint;
            idHint.text = hint;
        }

        void UpdateMarkers(bool hideAll)
        {
            int used = 0;
            if (!hideAll && rt.State == MissionRuntime.Phase.Playing)
            {
                var cam = sn.Cam;
                var size = ((RectTransform)transform).rect.size;
                bool drone = Time.time < rt.DroneUntil;
                foreach (var n in NPC.All)
                {
                    if (n == null || n.Dead || n.Escaped || (n.IsHidden && !n.MarkWhenHidden)) continue;
                    bool target = n.IsTarget && (!n.HiddenTarget || n.Identified || n.Aiming) && (n.MarkedTarget || n.Aiming);
                    bool guard = n.Identified && (n.Role == Role.Guard || n.Role == Role.Bodyguard);
                    bool prot = n.Protected;
                    bool armed = n.Identified && n.Armed && !target && !guard;
                    bool other = drone && n.Identified && !target && !guard && !prot && !armed;
                    // shooting at the nest (or about to): always marked, the player has to find him
                    bool threat = n.ThreatToPlayer > 0.01f;
                    float watch = n.Lookout != null ? n.Lookout.Meter : 0f;
                    bool lookout = n.Lookout != null && !n.Incapacitated && (n.Identified || watch > 0.05f || n.Lookout.Reporting);
                    if (!target && !guard && !prot && !armed && !other && !threat && !lookout) continue;
                    if (used >= markers.Count) break;
                    var m = markers[used++];
                    m.rt.gameObject.SetActive(true);
                    var head = (n.HeadBone != null && !n.IsHidden ? n.HeadBone.position : n.transform.position + Vector3.up * n.Height) + Vector3.up * (n.Down || n.Sedated ? 0.35f : 0.55f);
                    var s = cam.WorldToScreenPoint(head);
                    bool behind = s.z < 0;
                    if (behind) { s.x = Screen.width - s.x; s.y = Screen.height - s.y; s.z = -s.z; }
                    var p = ScreenToCanvas(s);
                    float dist = Vector3.Distance(cam.transform.position, n.transform.position);
                    bool important = target || prot || threat || (lookout && (watch > 0.05f || n.Lookout.Reporting));
                    bool danger = n.Aiming || threat || (lookout && (watch > 0.05f || n.Lookout.Reporting));
                    Color c;
                    if (danger) c = Color.Lerp(Theme.Danger, Color.white, Mathf.PingPong(Time.unscaledTime * 6f, 1f));
                    else if (prot) c = Theme.Info;
                    else if (target) c = n.Incapacitated ? Theme.Success : (n.Panicking ? Color.Lerp(Theme.Danger, Color.white, Mathf.PingPong(Time.unscaledTime * 4f, 1f)) : Theme.Danger);
                    else if (guard || armed) c = Theme.Guard;
                    else c = new Color(1f, 1f, 1f, 0.8f);
                    m.icon.color = c;
                    m.arrow.color = c;
                    m.ring.enabled = n.Aiming || threat || (lookout && watch > 0.05f);
                    if (n.Aiming) { m.ring.fillAmount = n.AimProgress; m.ring.color = Theme.Danger; }
                    else if (threat) { m.ring.fillAmount = n.ThreatToPlayer; m.ring.color = Theme.Danger; }
                    else if (lookout && watch > 0.05f) { m.ring.fillAmount = watch; m.ring.color = Theme.Accent; }
                    // keep markers out of the corners where the controls live
                    float hw = size.x * 0.5f, hh = size.y * 0.5f;
                    float minX = -hw + 150, maxX = hw - 150, minY = -hh + 330, maxY = hh - 150;
                    bool onScreen = !behind && p.x > -hw + 40 && p.x < hw - 40 && p.y > -hh + 40 && p.y < hh - 40;
                    if (sn.Scoped && onScreen && p.magnitude > 530f) onScreen = false;   // outside the scope circle
                    if (onScreen)
                    {
                        m.rt.anchoredPosition = p;
                        m.icon.enabled = true;
                        m.icon.sprite = Res.Sprite(target || prot || threat ? (n.Aiming || threat ? "diamond" : (prot ? "diamond_line" : "diamond")) : "chevron_down");
                        m.icon.rectTransform.sizeDelta = important ? new Vector2(30, 30) : new Vector2(other ? 18 : 24, other ? 18 : 24);
                        m.arrow.enabled = false;
                        m.label.enabled = important || armed || lookout;
                        int key = prot ? -1 : (threat && !target ? -3 : (lookout && !target ? -4 : (n.Aiming ? 100000 : 0) + Mathf.RoundToInt(dist)));
                        SetMarkerLabel(m, n, key, prot);
                        m.label.color = danger ? Theme.Danger : Color.white;
                        m.g.alpha = important ? 1f : (other ? 0.6f : 0.85f);
                    }
                    else
                    {
                        if (!important && !n.Aiming && !threat) { m.rt.gameObject.SetActive(false); continue; }
                        var dir = p.sqrMagnitude > 1f ? p.normalized : (behind ? Vector2.down : Vector2.up);
                        float k = Mathf.Min(Mathf.Abs(dir.x) > 0.001f ? (dir.x > 0 ? maxX : -minX) / Mathf.Abs(dir.x) : float.MaxValue,
                                            Mathf.Abs(dir.y) > 0.001f ? (dir.y > 0 ? maxY : -minY) / Mathf.Abs(dir.y) : float.MaxValue);
                        var ep = dir * k;
                        ep.x = Mathf.Clamp(ep.x, minX, maxX);
                        ep.y = Mathf.Clamp(ep.y, minY, maxY);
                        m.rt.anchoredPosition = ep;
                        m.icon.enabled = false;
                        m.arrow.enabled = true;
                        m.arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                        m.label.enabled = true;
                        SetMarkerLabel(m, n, n.Aiming ? -2 : (threat ? -3 : 200000 + Mathf.RoundToInt(dist)), false);
                        m.label.color = n.Aiming || threat ? Theme.Danger : Color.white;
                        m.g.alpha = 0.95f;
                    }
                }
            }
            for (int i = used; i < markers.Count; i++)
                if (markers[i].rt.gameObject.activeSelf) markers[i].rt.gameObject.SetActive(false);
            // suggested distraction spot
            bool showD = false;
            if (!hideAll && rt.State == MissionRuntime.Phase.Playing && rt.DistractPoint.HasValue)
            {
                var s = sn.Cam.WorldToScreenPoint(rt.DistractPoint.Value + Vector3.up * 0.4f);
                if (s.z > 0)
                {
                    var p = ScreenToCanvas(s);
                    var size = ((RectTransform)transform).rect.size;
                    if (Mathf.Abs(p.x) < size.x * 0.5f - 60 && Mathf.Abs(p.y) < size.y * 0.5f - 60 && (!sn.Scoped || p.magnitude < 530f))
                    {
                        showD = true;
                        distract.anchoredPosition = p;
                        float pulse = 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 5f);
                        distractRing.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
                        distractGroup.alpha = 0.95f;
                    }
                }
            }
            if (distract.gameObject.activeSelf != showD) distract.gameObject.SetActive(showD);
            UpdatePin(hideAll);
        }

        /// <summary>The pin over the target's room: on screen it floats over the window (faint once the scope is on it,
        /// so the person behind the glass stays clear); off screen an arrow at the edge points the way.</summary>
        void UpdatePin(bool hideAll)
        {
            if (pinRt == null) return;
            bool show = !hideAll && rt != null && rt.PinShown && sn != null && sn.Cam != null && !Cinematic.Active;
            if (!show)
            {
                if (pinRt.gameObject.activeSelf) pinRt.gameObject.SetActive(false);
                return;
            }
            if (!pinRt.gameObject.activeSelf)
            {
                pinRt.gameObject.SetActive(true);
                pinPlace.text = rt.Def != null ? rt.Def.location ?? "" : "";
            }
            var cam = sn.Cam;
            var s = cam.WorldToScreenPoint(rt.PinPoint.Value);
            bool behind = s.z < 0;
            if (behind) { s.x = Screen.width - s.x; s.y = Screen.height - s.y; s.z = -s.z; }
            var p = ScreenToCanvas(s);
            var size = ((RectTransform)transform).rect.size;
            float hw = size.x * 0.5f, hh = size.y * 0.5f;
            float minX = -hw + 170, maxX = hw - 170, minY = -hh + 340, maxY = hh - 190;
            bool onScreen = !behind && p.x > -hw + 60 && p.x < hw - 60 && p.y > -hh + 60 && p.y < hh - 60;
            if (sn.Scoped && onScreen && p.magnitude > 520f) onScreen = false;
            float pulse = 1f + 0.16f * Mathf.Sin(Time.unscaledTime * 4.5f);
            pinRing.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            if (onScreen)
            {
                pinRt.anchoredPosition = p;
                pinIcon.enabled = true; pinRing.enabled = true; pinArrow.enabled = false;
                pinTitle.enabled = true; pinPlace.enabled = !sn.Scoped;
                // in the scope, close to the reticle: almost out of the way
                float a = sn.Scoped ? Mathf.Lerp(0.22f, 0.95f, Mathf.InverseLerp(90f, 260f, p.magnitude)) : 0.95f;
                pinGroup.alpha = Mathf.MoveTowards(pinGroup.alpha, a, Time.unscaledDeltaTime * 4f);
            }
            else
            {
                var dir = p.sqrMagnitude > 1f ? p.normalized : (behind ? Vector2.down : Vector2.up);
                float k = Mathf.Min(Mathf.Abs(dir.x) > 0.001f ? (dir.x > 0 ? maxX : -minX) / Mathf.Abs(dir.x) : float.MaxValue,
                                    Mathf.Abs(dir.y) > 0.001f ? (dir.y > 0 ? maxY : -minY) / Mathf.Abs(dir.y) : float.MaxValue);
                var ep = dir * k;
                ep.x = Mathf.Clamp(ep.x, minX, maxX);
                ep.y = Mathf.Clamp(ep.y, minY, maxY);
                pinRt.anchoredPosition = ep;
                pinIcon.enabled = false; pinRing.enabled = false; pinArrow.enabled = true;
                pinArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                pinTitle.enabled = true; pinPlace.enabled = true;
                pinGroup.alpha = Mathf.MoveTowards(pinGroup.alpha, 0.95f, Time.unscaledDeltaTime * 4f);
            }
        }

        /// <summary>Marker label text, rebuilt only when what it shows changes (key: distance / mode).</summary>
        static void SetMarkerLabel(Marker m, NPC n, int key, bool prot)
        {
            if (m.labelKey == key && m.who == n) return;
            m.labelKey = key;
            m.who = n;
            if (prot) m.label.text = n.DisplayName;
            else if (key == -2) m.label.text = "!  NİŞAN ALIYOR";
            else if (key == -3) m.label.text = "!  SANA ATEŞ EDİYOR";
            else if (key == -4) m.label.text = "GÖZCÜ";
            else if (key >= 200000) m.label.text = (key - 200000) + " m";
            else if (key >= 100000) m.label.text = "!  " + (key - 100000) + " m";
            else m.label.text = key + " m";
        }

        // last values shown (texts are rebuilt only when they change: no garbage every frame)
        int lastRange = int.MinValue, lastZoom10 = int.MinValue, lastVisionKey = -1, lastCd = int.MinValue, lastWind10 = int.MinValue,
            lastAlarm10 = int.MinValue, lastAmmoKey = int.MinValue, lastAmmoTypeKey = int.MinValue, lastRows = -1;
        Objective lastFocus;
        string lastFocusText, lastCdLabel;
        readonly Objective[] rowObj = new Objective[ObjRows];
        readonly ObjectiveState[] rowState = new ObjectiveState[ObjRows];
        readonly string[] rowText = new string[ObjRows];

        void ResetTextCache()
        {
            lastRange = lastZoom10 = lastCd = lastWind10 = lastAlarm10 = lastAmmoKey = lastAmmoTypeKey = int.MinValue;
            lastVisionKey = -1;
            lastRows = -1;
            lastFocus = null;
            lastFocusText = null;
            lastCdLabel = null;
            for (int i = 0; i < ObjRows; i++) { rowObj[i] = null; rowText[i] = null; }
            foreach (var m in markers) { m.labelKey = int.MinValue; m.who = null; }
        }

        /// <summary>Live status of the person the current line is about.</summary>
        static string StatusOf(NPC n, Objective o, out Color c)
        {
            c = Theme.TextDim;
            if (n == null) return "—";
            if (o != null && o.kind == ObjectiveKind.Protect)
            {
                if (n.Dead) { c = Theme.Danger; return "Öldürüldü"; }
                c = Theme.Info;
                return n.Panicking ? "Kaçıyor — güvenli yere" : "Güvende — gözün üzerinde olsun";
            }
            if (n.NeedsId && !n.Confirmed && !n.Dead && !n.Escaped && !n.Incapacitated) { c = Theme.Accent; return "Kimliği doğrulanmadı — dosyadaki yüzü bul"; }
            if (n.IsHidden) return string.IsNullOrEmpty(n.HiddenStatus) ? "Görüş dışında — bekle" : n.HiddenStatus;
            if (n.Sedated) { c = Theme.Success; return n.Down ? "Baygın" : "İğne isabet etti — bayılıyor"; }
            if (n.Down) { c = Theme.Accent; return "Yerde — sürünüyor"; }
            if (n.Aiming) { c = Theme.Danger; return "NİŞAN ALIYOR — HEMEN VUR!"; }
            if (n.Panicking) { c = Theme.Danger; return "KAÇIYOR!"; }
            if (n.Investigating) { c = Theme.Accent; return "Sese doğru bakıyor — fırsat!"; }
            if (n.HiddenTime > 3f) { c = Theme.Accent; return "Siperin arkasında — dikkatini dağıt"; }
            if (n.HiddenTarget && !n.Identified) return "Kalabalıkta — herkesi dürbünle tara";
            if (n.Identified) { c = Theme.Success; return n.Wounded ? "Yaralı" : "Kimlik doğrulandı"; }
            return "Kimliği taranmadı — dürbünle tara";
        }

        void UpdatePanels()
        {
            // focus line + live status
            var f = rt.FocusObjective;
            string status; Color sc;
            Color accent = Theme.Danger;
            string icon = "target";
            if (rt.State != MissionRuntime.Phase.Playing && rt.Success)
            {
                if (lastFocusText != "#done") { objTitle.text = def.targets.Count > 0 ? "HEDEF:  " + def.targets[0].name : def.title; lastFocusText = "#done"; lastFocus = null; }
                status = "Sözleşme tamamlandı"; sc = Theme.Success;
            }
            else if (f == null)
            {
                if (lastFocusText != "#none") { objTitle.text = def.title; lastFocusText = "#none"; lastFocus = null; }
                status = "—"; sc = Theme.TextDim;
            }
            else
            {
                string ft = f.liveText != null ? f.Text : f.text;
                if (f != lastFocus || ft != lastFocusText)
                {
                    lastFocus = f;
                    lastFocusText = ft;
                    if (f.kind == ObjectiveKind.Eliminate && f.npc != null && f.text != null && f.text.StartsWith("Etkisiz hale getir: "))
                        objTitle.text = "HEDEF:  " + f.npc.DisplayName;
                    else objTitle.text = f.Text;
                }
                icon = f.Icon;
                accent = f.kind == ObjectiveKind.Protect ? Theme.Info : (f.kind == ObjectiveKind.Disable || f.kind == ObjectiveKind.Custom ? Theme.Accent : Theme.Danger);
                if (f.npc != null) status = StatusOf(f.npc, f, out sc);
                else if (f.kind == ObjectiveKind.Disable) { status = "Ateş et: devre dışı bırak"; sc = Theme.Accent; }
                else { status = "Devam ediyor"; sc = Theme.TextDim; }
            }
            objStatus.text = status;
            objStatus.color = sc;
            objAccent.color = accent;
            objIcon.sprite = Res.Icon(icon);
            objIcon.color = accent;

            // further lines: everything visible except the focus line
            int row = 0;
            foreach (var o in rt.Objectives)
            {
                if (o == f || o.hidden) continue;
                if (row >= ObjRows) break;
                if (!objRow[row].gameObject.activeSelf) objRow[row].gameObject.SetActive(true);
                string txt = o.liveText != null ? o.Text : o.text;
                if (rowObj[row] != o || rowState[row] != o.State || rowText[row] != txt)
                {
                    rowObj[row] = o;
                    rowState[row] = o.State;
                    rowText[row] = txt;
                    bool done = o.State == ObjectiveState.Done, failed = o.State == ObjectiveState.Failed;
                    objRowIcon[row].sprite = Res.Icon(done ? "check" : (failed ? "close" : o.Icon));
                    objRowIcon[row].color = done ? Theme.Success : (failed ? Theme.Danger : (o.kind == ObjectiveKind.Protect ? Theme.Info : Theme.Accent));
                    objRowText[row].text = (o.optional ? "BONUS  ·  " : "") + txt;
                    objRowText[row].color = done ? Theme.Success : (failed ? Theme.Danger : Theme.Text);
                }
                row++;
            }
            // V2 recon: before the first shot, how many of the threats in sight the scope has been over
            if (rt.ReconOpen && rt.ReconTotal > 0 && row < ObjRows)
            {
                if (!objRow[row].gameObject.activeSelf) objRow[row].gameObject.SetActive(true);
                bool all = rt.ReconMarked >= rt.ReconTotal;
                string txt = "KEŞİF  ·  " + rt.ReconMarked + " / " + rt.ReconTotal + " tehdit tarandı";
                if (rowObj[row] != null || rowText[row] != txt)
                {
                    rowObj[row] = null;
                    rowText[row] = txt;
                    objRowIcon[row].sprite = Res.Icon(all ? "check" : "scan");
                    objRowIcon[row].color = all ? Theme.Success : Theme.Info;
                    objRowText[row].text = txt;
                    objRowText[row].color = all ? Theme.Success : Theme.TextDim;
                }
                row++;
            }
            for (int i = row; i < ObjRows; i++) { rowObj[i] = null; rowText[i] = null; }
            for (int i = row; i < ObjRows; i++) if (objRow[i].gameObject.activeSelf) objRow[i].gameObject.SetActive(false);
            float wantH = 88f + row * 40f + (row > 0 ? 8f : 0f);
            objPanelH = Mathf.MoveTowards(objPanelH, wantH, Time.unscaledDeltaTime * 600f);
            objPanel.sizeDelta = new Vector2(560, objPanelH);

            // countdown below the objective panel: the contract's own clock, or else the target's time window (V2): how
            // long the man the player knows of stays in reach, from his orders — amber, red in the last five seconds
            float cd = rt.CountdownLeft;
            bool scripted = cd >= 0 && !string.IsNullOrEmpty(rt.CountdownLabel);
            float win = rt.Window;
            bool window = !scripted && win >= 0f && win <= 75f && rt.State == MissionRuntime.Phase.Playing;
            bool showCd = scripted || window;
            if (countdownChip.gameObject.activeSelf != showCd) countdownChip.gameObject.SetActive(showCd);
            if (showCd)
            {
                string label = scripted ? rt.CountdownLabel : "Fırsat penceresi";
                int cs = Mathf.CeilToInt(scripted ? cd : win);
                if (cs != lastCd || (object)label != lastCdLabel)
                {
                    lastCd = cs;
                    lastCdLabel = label;
                    countdownText.text = label + "  " + UIK.Time(cs);
                    if (countdownBg != null)
                        countdownBg.color = scripted || cs <= 5 ? new Color(0.55f, 0.08f, 0.06f, 0.92f) : new Color(0.52f, 0.33f, 0.04f, 0.92f);
                }
                countdownChip.anchoredPosition = new Vector2(124, -32 - objPanelH);
            }
            objStackBottom = 20f + objPanelH + (showCd ? 12f + 56f : 0f);

            // wind relative to the view
            var w = new Vector2(sn.Wind.x, sn.Wind.z);
            float spd = w.magnitude;
            int info = sn.Diff.windInfo;
            int w10 = Mathf.RoundToInt(spd * 10f);
            if (w10 != lastWind10)
            {
                lastWind10 = w10;
                // the hardest contracts only tell how the wind feels
                windText.text = info <= 0 ? Difficulty.WindWord(spd) : (w10 / 10) + "," + (w10 % 10) + " m/s";
            }
            windArrow.enabled = info > 0 || spd <= 0.05f;
            if (info <= 0) windSide.text = spd > 0.05f ? "Bayraklara bak" : "Rüzgar yok";
            else if (spd > 0.05f)
            {
                float windYaw = Mathf.Atan2(w.x, w.y) * Mathf.Rad2Deg;           // direction the air moves to
                float rel = Mathf.DeltaAngle(sn.transform.eulerAngles.y, windYaw);
                // arrow sprite points left (-x): rotate so it points along the relative wind
                windArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -90f - rel);
                string side = (rel > 25f && rel < 155f ? "Sağa esiyor »" : (rel < -25f && rel > -155f ? "« Sola esiyor" : (Mathf.Abs(rel) <= 25f ? "Arkadan" : "Karşıdan")))
                              + " · " + Difficulty.WindWord(spd);
                // the first contracts also say how far to hold off at the range on the rangefinder
                if (info >= 2 && sn.Scoped && sn.RangeToCenter > 20f)
                {
                    float cross = spd * Mathf.Sin(rel * Mathf.Deg2Rad);
                    float mil = Difficulty.DriftAt(sn.RangeToCenter, cross, sn.CurrentSpec.velocity) / sn.RangeToCenter * 1000f;
                    int m10 = Mathf.RoundToInt(Mathf.Abs(mil) * 10f);
                    if (m10 >= 2) side = (mil > 0 ? "« Sola tut " : "Sağa tut » ") + (m10 / 10) + "," + (m10 % 10) + " mil";
                }
                windSide.text = side;
            }
            else windSide.text = "Rüzgar yok";

            // alarm
            bool alarm = rt.Alarm, radio = rt.AlarmCountdown > 0;
            bool showA = alarm || radio;
            if (alarmChip.gameObject.activeSelf != showA) alarmChip.gameObject.SetActive(showA);
            if (alarm)
            {
                alarmText.text = "ALARM";
                alarmBg.color = Color.Lerp(Theme.Danger, new Color(0.45f, 0.05f, 0.04f), Mathf.PingPong(Time.unscaledTime * 2.5f, 1f));
            }
            else if (radio)
            {
                int a10 = Mathf.Max(0, Mathf.CeilToInt(rt.AlarmCountdown * 10f));
                if (a10 != lastAlarm10) { lastAlarm10 = a10; alarmText.text = "TELSİZ  " + (a10 / 10) + "," + (a10 % 10) + " sn"; }
                alarmBg.color = Theme.Guard;
            }
            // drone button: owned or loaned, not yet used
            if (Time.unscaledTime >= droneCheckAt) { droneCheckAt = Time.unscaledTime + 0.5f; hasDrone = Gear.HasGadget("drone"); }
            bool droneOk = hasDrone && !rt.DroneUsed && rt.State == MissionRuntime.Phase.Playing;
            if (droneRt.gameObject.activeSelf != droneOk) droneRt.gameObject.SetActive(droneOk);
            if (droneOk) droneRt.anchoredPosition = new Vector2(-20, showA ? -188 : -120);
        }

        void UpdateControls()
        {
            // fire: bolt cycle ring
            fireRing.fillAmount = sn.Busy ? sn.BoltProgress : 1f;
            fireRing.color = sn.Reloading ? new Color(1, 1, 1, 0.25f) : new Color(1, 1, 1, 0.85f);
            scopeIcon.sprite = Res.Icon(sn.Scoped ? "close" : "scope");
            int mag = Mathf.Clamp(sn.MagSize, 1, MaxPips);
            float pitch = mag <= 5 ? 22f : (mag <= 7 ? 18f : 13f);
            float pw = mag <= 5 ? 12f : (mag <= 7 ? 10f : 8f);
            for (int i = 0; i < MaxPips; i++)
            {
                bool on = i < mag;
                if (ammoPips[i].gameObject.activeSelf != on) ammoPips[i].gameObject.SetActive(on);
                if (!on) continue;
                ammoPips[i].rectTransform.anchoredPosition = new Vector2((i - (mag - 1) * 0.5f) * pitch, 8);
                ammoPips[i].rectTransform.sizeDelta = new Vector2(pw, 32);
                bool full = i < sn.Ammo;
                ammoPips[i].color = full ? (sn.AmmoType == AmmoKind.Standard || i != sn.Ammo - 1 ? Theme.Accent : Gear.AmmoOf(sn.AmmoType).tint) : new Color(1, 1, 1, 0.15f);
            }
            int ak = sn.Reloading ? -1 : sn.Ammo * 100 + sn.MagSize;
            if (ak != lastAmmoKey) { lastAmmoKey = ak; ammoText.text = sn.Reloading ? "DOLDURULUYOR" : sn.Ammo + " / " + sn.MagSize; }
            reloadBg.color = sn.Ammo == 0 && !sn.Reloading
                ? Color.Lerp(new Color(0.05f, 0.07f, 0.09f, 0.72f), new Color(0.75f, 0.45f, 0.05f, 0.9f), Mathf.PingPong(Time.unscaledTime * 2f, 1f))
                : new Color(0.05f, 0.07f, 0.09f, 0.72f);
            breathRing.fillAmount = sn.Stamina;
            breathRing.color = sn.Stamina < 0.25f ? Theme.Danger : (sn.HoldingBreath ? Color.white : Theme.Info);
            if (!zoomDragging)
            {
                zoom.maxValue = sn.MaxZoom;
                zoom.SetValueWithoutNotify(sn.Zoom);
            }
            // round selector and vision buttons only when the player has something to switch to
            bool showAmmo = sn.HasSpecialAmmo || sn.AmmoType != AmmoKind.Standard;
            if (ammoTypeRt.gameObject.activeSelf != showAmmo) ammoTypeRt.gameObject.SetActive(showAmmo);
            if (showAmmo)
            {
                if (sn.AmmoType != AmmoKind.Standard && Time.unscaledTime >= ammoCountAt)
                {
                    ammoCountAt = Time.unscaledTime + 0.4f;
                    ammoCount = Gear.AmmoAvailable(sn.AmmoType);
                }
                int tk = sn.AmmoType == AmmoKind.Standard ? -1 : (int)sn.AmmoType * 1000 + ammoCount;
                if (tk != lastAmmoTypeKey) { lastAmmoTypeKey = tk; ammoTypeText.text = sn.AmmoType == AmmoKind.Standard ? "STANDART" : "× " + ammoCount; }
            }
            bool showVision = sn.HasVisionGear;
            if (visionRt.gameObject.activeSelf != showVision) visionRt.gameObject.SetActive(showVision);
        }

        float objStackBottom = 108f, ammoCountAt, droneCheckAt;
        bool hasDrone;
        int ammoCount;

        void UpdateToasts(float dt)
        {
            float y = 0;
            for (int i = 0; i < toasts.Count; i++)
            {
                var t = toasts[i];
                if (t.rt == null) continue;
                t.t += dt;
                var target = new Vector2(0, -y);
                t.rt.anchoredPosition = Vector2.Lerp(t.rt.anchoredPosition, target, 1 - Mathf.Exp(-dt * 14f));
                y += 62;
                if (t.t > t.life) t.g.alpha = Mathf.MoveTowards(t.g.alpha, 0, dt * 3f);
            }
            for (int i = toasts.Count - 1; i >= 0; i--)
                if (toasts[i].t > toasts[i].life + 0.4f) { if (toasts[i].rt != null) Destroy(toasts[i].rt.gameObject); toasts.RemoveAt(i); }
            // the toast / tip column starts below the objective stack (objective lines + countdown) and makes room for the
            // coach box
            float top = Mathf.Max(196f, objStackBottom + 16f);
            ColumnTop = top;
            coach.anchoredPosition = new Vector2(0, Mathf.Lerp(coach.anchoredPosition.y, -top, 1 - Mathf.Exp(-dt * 10f)));
            float baseY = coachGroup.alpha > 0.01f ? -(top + coach.sizeDelta.y + 16f) : (lessons != null && lessons.CardVisible ? -(top + lessons.CardHeight + 16f) : -top);
            // never down to the middle of the screen (the reticle): at most a little over a third of the way down
            float safeH = Safe != null ? Safe.rect.height : 1080f;
            baseY = Mathf.Max(baseY, -safeH * 0.36f);
            toastLayer.anchoredPosition = new Vector2(0, Mathf.Lerp(toastLayer.anchoredPosition.y, baseY, 1 - Mathf.Exp(-dt * 10f)));
        }

        /// <summary>Radio lines one after the other, typed out; above the letterbox during the intro.</summary>
        void UpdateRadio(float dt)
        {
            if (radioNow == null && radioQueue.Count > 0)
            {
                radioNow = radioQueue.Dequeue();
                radioT = 0f;
                radioWho.text = UIK.Up(radioNow.who ?? "");
                radioText.text = "";
                Sfx.UI(Sfx.Radio);
            }
            bool cine = Cinematic.Active;
            float safeW = ((RectTransform)Safe).rect.width;
            if (cine)
            {
                // under the top letterbox (the title card owns the bottom left)
                radioBox.anchorMin = radioBox.anchorMax = new Vector2(0.5f, 1f);
                radioBox.pivot = new Vector2(0.5f, 1f);
                radioBox.anchoredPosition = new Vector2(0f, -118f);
                radioBox.sizeDelta = new Vector2(Mathf.Min(820f, safeW - 120f), 112f);
            }
            else
            {
                // between the left thumb (breath / zoom) and the right thumb cluster (reload, fire)
                radioBox.anchorMin = radioBox.anchorMax = new Vector2(0.5f, 0f);
                radioBox.pivot = new Vector2(0.5f, 0f);
                float width = Mathf.Clamp(safeW - 940f, 420f, 820f);
                radioBox.anchoredPosition = new Vector2(-40f, 30f);
                radioBox.sizeDelta = new Vector2(width, 112f);
            }
            if (radioNow == null)
            {
                radioGroup.alpha = Mathf.MoveTowards(radioGroup.alpha, 0f, dt * 4f);
                return;
            }
            radioT += dt;
            int chars = Mathf.Min(radioNow.text.Length, Mathf.FloorToInt(radioT * 46f));
            if (radioText.text.Length != chars) { var s = radioNow.text.Substring(0, chars); if (radioText is LocText lt) lt.SetRaw(s); else radioText.text = s; }
            radioGroup.alpha = Mathf.MoveTowards(radioGroup.alpha, 1f, dt * 6f);
            float total = radioNow.seconds + radioNow.text.Length / 46f;
            if (radioT > total)
            {
                radioGroup.alpha = Mathf.MoveTowards(radioGroup.alpha, 0f, dt * 5f);
                if (radioGroup.alpha <= 0.01f || radioQueue.Count > 0) radioNow = null;
            }
        }

        /// <summary>A star challenge just became impossible: tell the player once.</summary>
        void UpdateChallenges()
        {
            if (rt.State != MissionRuntime.Phase.Playing) return;
            var ch = rt.Challenges;
            if (challengeWarned.Length != ch.Count) challengeWarned = new bool[ch.Count];
            for (int i = 0; i < ch.Count; i++)
            {
                if (challengeWarned[i]) continue;
                bool possible;
                try { possible = ch[i].StillPossible(rt); } catch { possible = true; }
                if (possible) continue;
                challengeWarned[i] = true;
                OnToast("Yıldız kaçtı: " + ch[i].text, Theme.TextDim, "star_outline");
            }
        }

        // ------------------------------------------------------------------ intro overlay
        void PrepareCinematic()
        {
            foreach (var t in intelTags) if (t.rt != null) Destroy(t.rt.gameObject);
            intelTags.Clear();
            var env = Env.Find(def.preset);
            cineKind.text = "SÖZLEŞME " + def.number.ToString("00") + "  ·  " + UIK.Up(def.kind ?? "SUİKAST") + (rt != null && rt.Veteran ? "  ·  VETERAN" : "");
            cineTitle.text = def.title;
            cineSub.text = def.location + (string.IsNullOrEmpty(def.clock) ? "" : "   ·   " + def.clock) + "   ·   " + env.title;
            // intel tags: targets first, protected people, then the armed men around them
            int order = 0;
            foreach (var n in NPC.All)
            {
                if (n == null || n.Dead) continue;
                bool target = n.IsTarget && !n.HiddenTarget && !n.NeedsId;
                bool guard = n.Role == Role.Guard || n.Role == Role.Bodyguard;
                if (!target && !n.Protected && !guard) continue;
                intelTags.Add(MakeTag(n, target ? 0 : (n.Protected ? 1 : 2)));
            }
            intelTags.Sort((a, b) => Prio(a.npc).CompareTo(Prio(b.npc)));
            foreach (var t in intelTags) t.showAt = 0.12f + 0.09f * order++;
            bool hidden = false;
            foreach (var n in NPC.All) if (n != null && n.HiddenTarget) { hidden = true; break; }
            bool unknown = false;
            foreach (var n in NPC.All) if (n != null && n.NeedsId && !n.Confirmed) { unknown = true; break; }
            string note = !string.IsNullOrEmpty(def.introNote) ? def.introNote
                        : (hidden ? "Kalabalıkta silahlı adam(lar) var — kimliği bilinmiyor"
                        : (unknown ? "Hedef bu bölgede — dosyadaki yüzü bul, emin olunca İŞARETLE" : null));
            if (note != null) intelTags.Add(MakeNoteTag(note, 0.12f + 0.09f * order));
        }

        static int Prio(NPC n) => n == null ? 9 : (n.IsTarget ? 0 : (n.Protected ? 1 : 2));

        IntelTag MakeTag(NPC n, int kind)
        {
            var t = new IntelTag { npc = n };
            t.rt = UIK.Rect(cineTags, "Tag");
            t.rt.sizeDelta = new Vector2(10, 10);
            t.g = UIK.Group(t.rt.gameObject);
            t.g.alpha = 0;
            Color c = kind == 0 ? Theme.Danger : (kind == 1 ? Theme.Info : Theme.Guard);
            t.pin = UIK.Img(t.rt, "Pin", kind == 2 ? "chevron_down" : "diamond", c);
            UIK.Place(t.pin.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, kind == 2 ? new Vector2(24, 24) : new Vector2(30, 30));
            UIK.AddShadow(t.pin, 2, 0.6f);
            var line = UIK.Img(t.rt, "Line", null, new Color(c.r, c.g, c.b, 0.8f));
            UIK.Place(line.rectTransform, UIK.MC, new Vector2(0f, 0.5f), new Vector2(18, 26), new Vector2(46, 2));
            line.rectTransform.localRotation = Quaternion.Euler(0, 0, 30);
            if (kind == 2)
            {
                t.role = UIK.Txt(t.rt, "Role", n.RoleLabel ?? "KORUMA", Res.FontBold, 20, c, TextAnchor.LowerLeft, false);
                UIK.Place(t.role.rectTransform, UIK.MC, new Vector2(0, 0), new Vector2(56, 34), new Vector2(240, 26));
                var o = t.role.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.85f);
                return t;
            }
            var box = UIK.Panel(t.rt, "Box", new Color(0.02f, 0.03f, 0.04f, 0.85f), "rr8");
            UIK.Place(box.rectTransform, UIK.MC, new Vector2(0, 0), new Vector2(56, 34), new Vector2(330, 74));
            var edge = UIK.Img(box.transform, "Edge", null, c);
            UIK.Place(edge.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(5, 74));
            t.role = UIK.Txt(box.transform, "Role", kind == 0 ? "HEDEF" : (UIK.Up(n.RoleLabel) ?? "KORU"), Res.FontBold, 20, c, TextAnchor.UpperLeft, false);
            UIK.Place(t.role.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(16, -6), new Vector2(300, 24));
            t.name = UIK.Txt(box.transform, "Name", (n.DisplayName ?? "") + (string.IsNullOrEmpty(n.Alias) ? "" : "  “" + n.Alias + "”"), Res.FontBold, 28, Theme.Text, TextAnchor.UpperLeft, true, 18);
            UIK.Place(t.name.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(16, -32), new Vector2(306, 36));
            return t;
        }

        IntelTag MakeNoteTag(string text, float at)
        {
            var t = new IntelTag { npc = null, showAt = at };
            t.rt = UIK.Rect(cineTags, "Note");
            t.rt.anchorMin = t.rt.anchorMax = new Vector2(0.5f, 1f);
            t.rt.pivot = new Vector2(0.5f, 1f);
            t.rt.anchoredPosition = new Vector2(0, -130);
            t.rt.sizeDelta = new Vector2(900, 64);
            t.g = UIK.Group(t.rt.gameObject);
            t.g.alpha = 0;
            var bg = UIK.Panel(t.rt, "Bg", new Color(0.3f, 0.04f, 0.03f, 0.88f), "pill");
            UIK.Stretch(bg.rectTransform);
            var ic = UIK.Icon(t.rt, "alert", Color.white, 32);
            UIK.Place(ic.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(32, 32));
            t.name = UIK.Txt(t.rt, "T", text, Res.FontSemi, 27, Color.white, TextAnchor.MiddleLeft, true, 18);
            UIK.Stretch(t.name.rectTransform, 72, 0, 24, 0);
            return t;
        }

        void OnCinematic(bool on)
        {
            if (on)
            {
                cineGroup.gameObject.SetActive(true);
                cineGroup.alpha = 0f;
                Tween.Fade(cineGroup, 1f, 0.5f);
                if (!string.IsNullOrEmpty(def.introLine)) OnRadio(def.handler, def.introLine, 3.5f);
            }
            else
            {
                Tween.Fade(cineGroup, 0f, 0.35f, () => { if (cineGroup != null && !Cinematic.Active) cineGroup.gameObject.SetActive(false); });
                if (tutStep >= 0) ShowCoach(true);
            }
        }

        void UpdateCinematic(float dt)
        {
            if (!cineGroup.gameObject.activeSelf) return;
            if (Cinematic.Leaving && cineGroup.alpha > 0f) cineGroup.alpha = Mathf.MoveTowards(cineGroup.alpha, 0f, dt * 3f);
            var cam = sn.Cam;
            foreach (var t in intelTags)
            {
                if (t.rt == null) continue;
                if (!t.shown && Cinematic.Progress >= t.showAt)
                {
                    t.shown = true;
                    Tween.Fade(t.g, 1f, 0.25f);
                    Tween.Pop(t.rt, 0.3f, 0f, 0.7f);
                    Sfx.UI(Sfx.Click);
                }
                if (t.npc == null) continue;
                var head = (t.npc.HeadBone != null ? t.npc.HeadBone.position : t.npc.transform.position + Vector3.up * t.npc.Height) + Vector3.up * 0.4f;
                var s = cam.WorldToScreenPoint(head);
                if (s.z <= 0) { t.rt.gameObject.SetActive(false); continue; }
                if (!t.rt.gameObject.activeSelf) t.rt.gameObject.SetActive(true);
                t.rt.anchoredPosition = ScreenToCanvas(s);
            }
        }

        // ------------------------------------------------------------------ input from the look pad
        public void OnLookDrag(Vector2 delta)
        {
            if (KillCam.Active || Cinematic.Active) return;
            if (sn == null || !sn.InputEnabled) return;
            sn.AddLook(delta);
            dragAccum += delta.magnitude;
        }

        public void OnPinch(float ratio)
        {
            if (sn == null || !sn.Scoped) return;
            sn.SetZoom(sn.Zoom * ratio);
        }

        public void OnLookTap()
        {
            if (Cinematic.Active) Cinematic.SkipRequested = true;
            else if (KillCam.Active) KillCam.SkipRequested = true;
        }

        // ------------------------------------------------------------------ tutorial (mission 1, first time)
        static readonly string[] TutText =
        {
            "Ekranı parmağınla sürükleyerek etrafa bak.",
            "Sağdaki dürbün tuşuna dokunarak dürbünü aç.",
            "Kırmızı işaret hedefi gösterir. Nişangahı hedefin üzerine getir.",
            "Nişangahı kişinin üzerinde tut: kimliği taranıyor…",
            "Soldaki NEFES tuşuna basılı tut: nişan sabitlenir.",
            "Hazır! Büyük kırmızı tuşla ateş et.",
        };

        void ShowCoach(bool on)
        {
            if (on) { coachIcon.enabled = false; coach.sizeDelta = new Vector2(820, 100); }
            Tween.Fade(coachGroup, on ? 1f : 0f, 0.25f);
            coachRing.gameObject.SetActive(on);
        }

        void OnTutorialEvent() { }

        void UpdateTutorial(float dt)
        {
            if (tutStep < 0) return;
            if (rt.State != MissionRuntime.Phase.Playing || KillCam.Active)
            {
                coachRing.gameObject.SetActive(false);
                return;
            }
            var target = rt.Targets.Count > 0 ? rt.Targets[0] : null;
            bool advance = false;
            RectTransform focus = null;
            switch (tutStep)
            {
                case 0: advance = dragAccum > 380f; break;
                case 1: focus = scopeRt; advance = sn.Scoped; break;
                case 2:
                    if (target != null && sn.Scoped)
                        advance = sn.Hovered == target || Vector3.Angle(sn.Cam.transform.forward, target.ChestPoint - sn.Cam.transform.position) < sn.Cam.fieldOfView * 0.12f;
                    if (!sn.Scoped) tutStep = 1;
                    break;
                case 3: advance = target == null || target.Identified; break;
                case 4:
                    focus = breathRt;
                    if (sn.HoldingBreath) tutTimer += dt; else tutTimer = 0;
                    advance = tutTimer > 0.5f;
                    break;
                case 5: focus = fireRt; advance = rt.Shots > 0; break;
            }
            coachStep.text = (tutStep + 1) + "/" + TutText.Length;
            coachText.text = TutText[Mathf.Clamp(tutStep, 0, TutText.Length - 1)];
            if (focus != null)
            {
                coachRing.gameObject.SetActive(true);
                coachRing.rectTransform.position = focus.position;
                float s = focus.rect.width * 1.5f * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f));
                coachRing.rectTransform.sizeDelta = new Vector2(s, s);
                coachRing.transform.SetAsLastSibling();
            }
            else coachRing.gameObject.SetActive(false);
            if (advance)
            {
                tutStep++;
                tutTimer = 0;
                Sfx.UI(Sfx.Confirm);
                if (tutStep >= TutText.Length)
                {
                    tutStep = -1;
                    Save.SeenTutorial = true;
                    Analytics.Event("tutorial_complete");
                    Analytics.MetaEvent("tutorial");
                    ShowCoach(false);
                }
            }
        }
    }

    /// <summary>Full-screen touch surface: one finger looks around, two fingers pinch to zoom, a tap skips the kill cam / intro.</summary>
    public class LookPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
    {
        public HUD hud;
        readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        float pinchDist;
        float downTime;
        Vector2 downPos;

        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }

        public void OnPointerDown(PointerEventData e)
        {
            pointers[e.pointerId] = e.position;
            if (pointers.Count == 2) pinchDist = PinchDistance();
            downTime = Time.unscaledTime;
            downPos = e.position;
        }

        public void OnPointerUp(PointerEventData e)
        {
            pointers.Remove(e.pointerId);
            if (pointers.Count < 2) pinchDist = 0;
            if (Time.unscaledTime - downTime < 0.25f && (e.position - downPos).magnitude < 20f) hud.OnLookTap();
        }

        public void OnDrag(PointerEventData e)
        {
            if (!pointers.ContainsKey(e.pointerId)) return;
            pointers[e.pointerId] = e.position;
            if (pointers.Count >= 2)
            {
                float d = PinchDistance();
                if (pinchDist > 1f && d > 1f) hud.OnPinch(d / pinchDist);
                pinchDist = d;
                return;
            }
            hud.OnLookDrag(e.delta);
        }

        float PinchDistance()
        {
            Vector2 a = Vector2.zero, b = Vector2.zero;
            int i = 0;
            foreach (var p in pointers.Values) { if (i == 0) a = p; else if (i == 1) b = p; i++; }
            return Vector2.Distance(a, b);
        }

        void OnDisable() { pointers.Clear(); pinchDist = 0; }
    }
}

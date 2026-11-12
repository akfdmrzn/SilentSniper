using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>
    /// Contract select on the region map: every contract is a pin where it happens (done: green with its stars, next:
    /// amber and pulsing, locked: grey), joined by the campaign route; the selected one opens as a dossier card on the
    /// right (target, type, stars, grade, distance, wind, pay, the rifle power it asks for) with the way into its briefing.
    /// </summary>
    public class MissionsScreen : UIScreen
    {
        class Pin
        {
            public MissionDef def;
            public RectTransform rt, dot;
            public Image bg, ring, glow, lockIcon;
            public Text num, stars;
            public Vector2 pos;         // relaxed position on the map (map rect units)
            public Vector2 truePos;     // the contract's place
            public Image leader;
        }
        class Tab { public LocationDef loc; public Button btn; public Image bg, icon, lockIcon; public Text name, sub; }

        readonly List<Pin> pins = new List<Pin>();
        readonly List<Tab> tabs = new List<Tab>();
        readonly List<Image> route = new List<Image>();
        MetaBar bar;
        RectTransform mapPanel, mapRt, cardRt, routeRoot, pinRoot, nestMark;
        Image mapImg;
        string shown;
        Pin selected;
        Vector2 laidOutFor;
        // locked region
        RectTransform lockPanel;
        Text lockTitle, lockText, lockBlurb;
        // dossier card
        Image cPortrait, cKindBg, cGradeBg, cVet, cLock;
        Text cNum, cTitle, cName, cKind, cLoc, cGrade, cInfo, cReward, cPower, cLockText;
        readonly Image[] cStars = new Image[3];
        Button cGo;
        RectTransform cLockRt;

        protected override void Build()
        {
            var bg = UIK.Img(transform, "Bg", null, new Color(0.015f, 0.02f, 0.03f, 0.97f), false, true);
            UIK.Stretch(bg.rectTransform);
            Header("GÖREVLER", () => ui.ShowMenu(false));
            bar = MetaBar.Build(ui, Safe, false);

            int n = Locations.All.Count;
            for (int i = 0; i < n; i++)
            {
                var loc = Locations.All[i];
                var t = new Tab { loc = loc };
                t.bg = UIK.Panel(Safe, "Map_" + loc.id, Theme.PanelLight, "cut24");
                t.bg.raycastTarget = true;
                var r = t.bg.rectTransform;
                r.anchorMin = new Vector2(i / (float)n, 1); r.anchorMax = new Vector2((i + 1) / (float)n, 1);
                r.pivot = new Vector2(0.5f, 1);
                r.offsetMin = new Vector2(i == 0 ? 24 : 8, -222); r.offsetMax = new Vector2(i == n - 1 ? -24 : -8, -136);
                t.btn = t.bg.gameObject.AddComponent<Button>();
                t.btn.transition = Selectable.Transition.None;
                t.btn.onClick.AddListener(() => { Sfx.UI(Sfx.Click); Show(loc.id); });
                t.icon = UIK.Icon(t.bg.transform, loc.icon, Theme.Text, 46);
                UIK.Place(t.icon.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(46, 46));
                t.name = UIK.Txt(t.bg.transform, "Name", loc.name, Res.FontBold, 34, Theme.Text, TextAnchor.UpperLeft, false);
                t.name.rectTransform.anchorMin = new Vector2(0, 1); t.name.rectTransform.anchorMax = new Vector2(1, 1);
                t.name.rectTransform.pivot = new Vector2(0, 1);
                t.name.rectTransform.offsetMin = new Vector2(84, -48); t.name.rectTransform.offsetMax = new Vector2(-70, -6);
                t.sub = UIK.Txt(t.bg.transform, "Sub", "", Res.FontSemi, 23, Theme.TextDim, TextAnchor.UpperLeft, false);
                t.sub.rectTransform.anchorMin = new Vector2(0, 1); t.sub.rectTransform.anchorMax = new Vector2(1, 1);
                t.sub.rectTransform.pivot = new Vector2(0, 1);
                t.sub.rectTransform.offsetMin = new Vector2(84, -80); t.sub.rectTransform.offsetMax = new Vector2(-70, -48);
                t.lockIcon = UIK.Icon(t.bg.transform, "lock", Theme.TextMute, 36);
                UIK.Place(t.lockIcon.rectTransform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(36, 36));
                tabs.Add(t);
            }

            // ---- the map
            var mp = UIK.Panel(Safe, "MapPanel", new Color(0.01f, 0.02f, 0.03f, 1f), "ch18");
            mapPanel = mp.rectTransform;
            mapPanel.gameObject.AddComponent<RectMask2D>();
            mapImg = UIK.Img(mapPanel, "Map", null, Color.white);
            mapRt = mapImg.rectTransform;
            var fit = mapImg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1800f / 1100f;
            var frame = UIK.Panel(mapPanel, "Frame", new Color(1, 1, 1, 0.12f), "ch18_line");
            UIK.Stretch(frame.rectTransform);
            routeRoot = UIK.Rect(mapRt, "Route");
            UIK.Stretch(routeRoot);
            nestMark = UIK.Rect(mapRt, "Nest");
            var nm = UIK.Icon(nestMark, "crosshair", Theme.Info, 40);
            UIK.Place(nm.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            var nl = UIK.Txt(nestMark, "L", "ANA YUVA", Res.FontBold, 20, Theme.Info, TextAnchor.UpperCenter, false);
            UIK.Place(nl.rectTransform, UIK.MC, new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(160, 26));
            UIK.AddShadow(nl, 2f, 0.8f);
            nestMark.sizeDelta = new Vector2(10, 10);
            pinRoot = UIK.Rect(mapRt, "Pins");
            UIK.Stretch(pinRoot);
            foreach (var m in MissionDefs.All) pins.Add(BuildPin(m));
            var legend = UIK.Txt(mapPanel, "Legend", "<color=#36D47C>●</color> Tamamlandı    <color=#FFB21E>●</color> Sıradaki    <color=#6B7682>●</color> Kilitli",
                                 Res.FontSemi, 22, Theme.TextDim, TextAnchor.LowerLeft, false);
            legend.supportRichText = true;
            UIK.Place(legend.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(18, 10), new Vector2(700, 30));

            BuildCard();

            // a region that is still closed: what it is and what opens it
            var lp = UIK.Panel(Safe, "MapLocked", new Color(0.03f, 0.04f, 0.055f, 0.98f), "ch28");
            lockPanel = lp.rectTransform;
            UIK.Stretch(lockPanel, 24, 24, 24, 244);
            var ll = UIK.Panel(lp.transform, "line", new Color(1, 1, 1, 0.1f), "ch28_line");
            UIK.Stretch(ll.rectTransform);
            var li = UIK.Icon(lp.transform, "lock", Theme.TextDim, 96);
            UIK.Place(li.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(96, 96));
            lockTitle = UIK.Txt(lp.transform, "Title", "", Res.FontBold, 64, Theme.Text, TextAnchor.MiddleCenter, false);
            UIK.Place(lockTitle.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(1200, 80));
            lockText = UIK.Txt(lp.transform, "Text", "", Res.FontSemi, 34, Theme.Accent, TextAnchor.MiddleCenter, false);
            UIK.Place(lockText.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1200, 50));
            lockBlurb = UIK.Txt(lp.transform, "Blurb", "", Res.FontBody, 28, Theme.TextDim, TextAnchor.UpperCenter, true, 18);
            UIK.Place(lockBlurb.rectTransform, UIK.MC, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1000, 120));
            lockPanel.gameObject.SetActive(false);
        }

        Pin BuildPin(MissionDef def)
        {
            var p = new Pin { def = def };
            p.leader = UIK.Img(pinRoot, "Leader_" + def.id, null, new Color(1, 1, 1, 0.35f));
            p.leader.rectTransform.pivot = new Vector2(0f, 0.5f);
            p.leader.rectTransform.anchorMin = p.leader.rectTransform.anchorMax = Vector2.zero;
            p.dot = UIK.Img(pinRoot, "Spot_" + def.id, "circle", new Color(1, 1, 1, 0.8f)).rectTransform;
            p.dot.anchorMin = p.dot.anchorMax = Vector2.zero;
            p.dot.sizeDelta = new Vector2(12, 12);
            p.rt = UIK.Rect(pinRoot, "Pin_" + def.id);
            p.rt.anchorMin = p.rt.anchorMax = Vector2.zero;
            p.rt.pivot = new Vector2(0.5f, 0.5f);
            p.rt.sizeDelta = new Vector2(74, 74);
            p.glow = UIK.Img(p.rt, "Glow", "glow_ring", new Color(1f, 0.7f, 0.12f, 0.8f));
            UIK.Place(p.glow.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            p.bg = UIK.Img(p.rt, "Bg", "circle", Theme.Accent, false, true);
            UIK.Stretch(p.bg.rectTransform);
            p.ring = UIK.Img(p.rt, "Ring", "ring_thick", Color.white);
            UIK.Stretch(p.ring.rectTransform, -6, -6, -6, -6);
            p.num = UIK.Txt(p.rt, "N", def.number.ToString(), Res.FontBold, 36, Theme.Ink, TextAnchor.MiddleCenter, false);
            UIK.Stretch(p.num.rectTransform, 0, 2, 0, 0);
            p.lockIcon = UIK.Icon(p.rt, "lock", Theme.TextDim, 30);
            UIK.Place(p.lockIcon.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            p.stars = UIK.Txt(p.rt, "S", "", Res.FontBold, 24, Theme.Accent, TextAnchor.UpperCenter, false);
            UIK.Place(p.stars.rectTransform, UIK.BC, new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(120, 28));
            UIK.AddShadow(p.stars, 2f, 0.9f);
            var b = p.bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Sfx.UI(Sfx.Click); Select(p); });
            return p;
        }

        // ---- the dossier card of the selected contract
        void BuildCard()
        {
            var c = UIK.Panel(Safe, "Card", new Color(0.04f, 0.055f, 0.075f, 0.97f), "ch28");
            cardRt = c.rectTransform;
            var ln = UIK.Panel(c.transform, "line", new Color(1, 1, 1, 0.1f), "ch28_line");
            UIK.Stretch(ln.rectTransform);
            var fr = UIK.Panel(c.transform, "Frame", new Color(73f / 255f, 77f / 255f, 81f / 255f, 1f), "ch18");
            UIK.Place(fr.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(22, -22), new Vector2(214, 268));
            cPortrait = UIK.Img(fr.transform, "P", null, Color.white);
            cPortrait.preserveAspect = true;
            UIK.Stretch(cPortrait.rectTransform, 5, 5, 5, 5);
            cKindBg = UIK.Panel(fr.transform, "Kind", Theme.Danger, "pill");
            UIK.Place(cKindBg.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(8, 8), new Vector2(170, 34));
            cKind = UIK.Txt(cKindBg.transform, "T", "", Res.FontBold, 21, Color.white, TextAnchor.MiddleCenter, false);
            UIK.Stretch(cKind.rectTransform, 6, 0, 6, 0);
            cNum = UIK.Txt(c.transform, "Num", "", Res.FontBold, 30, Theme.Accent, TextAnchor.UpperLeft, false);
            UIK.Place(cNum.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(256, -22), new Vector2(200, 36));
            cTitle = UIK.Txt(c.transform, "Title", "", Res.FontBold, 44, Theme.Text, TextAnchor.UpperLeft, true, 24);
            UIK.Place(cTitle.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(254, -56), new Vector2(320, 104));
            cName = UIK.Txt(c.transform, "Name", "", Res.FontSemi, 26, Theme.TextDim, TextAnchor.UpperLeft, true, 16);
            UIK.Place(cName.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(256, -162), new Vector2(320, 64));
            cLoc = UIK.Txt(c.transform, "Loc", "", Res.FontBody, 23, Theme.TextMute, TextAnchor.UpperLeft, true, 14);
            UIK.Place(cLoc.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(256, -230), new Vector2(320, 60));
            for (int s = 0; s < 3; s++)
            {
                cStars[s] = UIK.Icon(c.transform, "star_outline", Theme.TextMute, 48);
                UIK.Place(cStars[s].rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(22 + s * 56, -306), new Vector2(48, 48));
            }
            cGradeBg = UIK.Panel(c.transform, "Grade", new Color(0.03f, 0.04f, 0.05f, 0.9f), "pill");
            UIK.Place(cGradeBg.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(200, -298), new Vector2(64, 64));
            cGrade = UIK.Txt(cGradeBg.transform, "G", "", Res.FontBold, 44, Theme.Accent, TextAnchor.MiddleCenter, false);
            UIK.Stretch(cGrade.rectTransform, 0, 2, 0, 0);
            cVet = UIK.Icon(c.transform, "medal", Theme.Accent, 44);
            UIK.Place(cVet.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(276, -308), new Vector2(44, 44));
            cInfo = UIK.Txt(c.transform, "Info", "", Res.FontSemi, 25, Theme.TextDim, TextAnchor.UpperLeft, true, 16);
            cInfo.supportRichText = true;
            cInfo.rectTransform.anchorMin = new Vector2(0, 1); cInfo.rectTransform.anchorMax = new Vector2(1, 1);
            cInfo.rectTransform.pivot = new Vector2(0, 1);
            cInfo.rectTransform.offsetMin = new Vector2(24, -446); cInfo.rectTransform.offsetMax = new Vector2(-24, -372);
            cPower = UIK.Txt(c.transform, "Power", "", Res.FontSemi, 25, Theme.TextDim, TextAnchor.UpperLeft, true, 16);
            cPower.supportRichText = true;
            cPower.rectTransform.anchorMin = new Vector2(0, 1); cPower.rectTransform.anchorMax = new Vector2(1, 1);
            cPower.rectTransform.pivot = new Vector2(0, 1);
            cPower.rectTransform.offsetMin = new Vector2(24, -500); cPower.rectTransform.offsetMax = new Vector2(-24, -452);
            cReward = UIK.Txt(c.transform, "Reward", "", Res.FontBold, 46, Theme.Accent, TextAnchor.LowerLeft, false);
            cReward.rectTransform.anchorMin = new Vector2(0, 0); cReward.rectTransform.anchorMax = new Vector2(1, 0);
            cReward.rectTransform.pivot = new Vector2(0, 0);
            cReward.rectTransform.offsetMin = new Vector2(26, 132); cReward.rectTransform.offsetMax = new Vector2(-24, 190);
            cGo = UIK.Btn(c.transform, "Go", "BRİFİNGE GİT", BtnStyle.Primary, () =>
            {
                if (selected == null || !MissionDefs.IsUnlocked(selected.def)) return;
                ui.ShowBriefing(selected.def);
            }, "next", 36);
            var gr = (RectTransform)cGo.transform;
            gr.anchorMin = new Vector2(0, 0); gr.anchorMax = new Vector2(1, 0);
            gr.pivot = new Vector2(0.5f, 0);
            gr.offsetMin = new Vector2(20, 20); gr.offsetMax = new Vector2(-20, 120);
            RewardFX.Shine(gr, 3.2f);
            cLock = UIK.Panel(c.transform, "Locked", new Color(0.02f, 0.03f, 0.04f, 0.9f), "ch18");
            cLockRt = cLock.rectTransform;
            cLockRt.anchorMin = new Vector2(0, 0); cLockRt.anchorMax = new Vector2(1, 0);
            cLockRt.pivot = new Vector2(0.5f, 0);
            cLockRt.offsetMin = new Vector2(20, 20); cLockRt.offsetMax = new Vector2(-20, 120);
            var lic = UIK.Icon(cLock.transform, "lock", Theme.TextDim, 40);
            UIK.Place(lic.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(40, 40));
            cLockText = UIK.Txt(cLock.transform, "T", "", Res.FontSemi, 27, Theme.TextDim, TextAnchor.MiddleLeft, true, 16);
            UIK.Stretch(cLockText.rectTransform, 80, 4, 16, 4);
        }

        /// <summary>Map on the left, card on the right, for the real safe-area size; pins placed and spread out.</summary>
        Vector2 safeFor;

        void Layout()
        {
            var size = Safe.rect.size;
            if (size.x < 10f) return;
            if ((size - safeFor).sqrMagnitude > 1f)
            {
                safeFor = size;
                float cardW = Mathf.Clamp(size.x * 0.3f, 560f, 640f);
                cardRt.anchorMin = new Vector2(1, 0); cardRt.anchorMax = new Vector2(1, 1);
                cardRt.pivot = new Vector2(1, 0.5f);
                cardRt.offsetMin = new Vector2(-24 - cardW, 24); cardRt.offsetMax = new Vector2(-24, -240);
                mapPanel.anchorMin = new Vector2(0, 0); mapPanel.anchorMax = new Vector2(1, 1);
                mapPanel.offsetMin = new Vector2(24, 24); mapPanel.offsetMax = new Vector2(-24 - cardW - 16, -240);
                Canvas.ForceUpdateCanvases();
            }
            var ms = mapRt.rect.size;
            if (ms.x < 10f || (ms - laidOutFor).sqrMagnitude < 1f) return;
            laidOutFor = ms;
            PlacePins();
        }

        void PlacePins()
        {
            var ms = mapRt.rect.size;
            var here = new List<Pin>();
            foreach (var p in pins)
            {
                bool on = p.def.region == shown;
                p.rt.gameObject.SetActive(on);
                p.dot.gameObject.SetActive(on);
                p.leader.gameObject.SetActive(on);
                if (!on) continue;
                Vector2 u = RegionPins.Pin.TryGetValue(p.def.id, out var v) ? v : new Vector2(0.5f, 0.5f);
                p.truePos = new Vector2(u.x * ms.x, u.y * ms.y);
                p.pos = p.truePos;
                here.Add(p);
            }
            // spread pins that sit on the same place (a warehouse door used by two contracts)
            float min = 86f;
            for (int it = 0; it < 60; it++)
            {
                bool moved = false;
                for (int i = 0; i < here.Count; i++)
                    for (int j = i + 1; j < here.Count; j++)
                    {
                        var d = here[j].pos - here[i].pos;
                        float l = d.magnitude;
                        if (l >= min) continue;
                        var dir = l > 0.01f ? d / l : new Vector2(Mathf.Cos(i * 2.4f), Mathf.Sin(i * 2.4f));
                        var push = dir * (min - l) * 0.5f;
                        here[i].pos -= push;
                        here[j].pos += push;
                        moved = true;
                    }
                for (int i = 0; i < here.Count; i++)
                    here[i].pos = new Vector2(Mathf.Clamp(here[i].pos.x, 50f, ms.x - 50f), Mathf.Clamp(here[i].pos.y, 60f, ms.y - 46f));
                if (!moved) break;
            }
            foreach (var p in here)
            {
                p.rt.anchoredPosition = p.pos;
                p.dot.anchoredPosition = p.truePos;
                var d = p.pos - p.truePos;
                p.leader.enabled = d.magnitude > 8f;
                p.leader.rectTransform.anchoredPosition = p.truePos;
                p.leader.rectTransform.sizeDelta = new Vector2(d.magnitude, 3f);
                p.leader.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
            if (RegionPins.Nest.TryGetValue(shown, out var nv))
            {
                nestMark.gameObject.SetActive(true);
                nestMark.anchorMin = nestMark.anchorMax = Vector2.zero;
                nestMark.anchoredPosition = new Vector2(nv.x * ms.x, nv.y * ms.y);
            }
            else nestMark.gameObject.SetActive(false);
            BuildRoute(here);
        }

        /// <summary>The campaign route: dots from pin to pin in contract order (bright up to the last finished one).</summary>
        void BuildRoute(List<Pin> here)
        {
            foreach (var r in route) if (r != null) Destroy(r.gameObject);
            route.Clear();
            here.Sort((a, b) => a.def.number.CompareTo(b.def.number));
            for (int i = 0; i + 1 < here.Count; i++)
            {
                var a = here[i].pos;
                var b = here[i + 1].pos;
                bool done = Save.Completed(here[i + 1].def.id);
                float len = (b - a).magnitude;
                int n = Mathf.FloorToInt((len - 80f) / 22f);
                for (int k = 1; k <= n; k++)
                {
                    var p = Vector2.Lerp(a, b, (40f + k * 22f - 11f) / len);
                    var dot = UIK.Img(routeRoot, "R", "circle", done ? new Color(0.21f, 0.83f, 0.49f, 0.75f) : new Color(1f, 1f, 1f, 0.28f));
                    dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = Vector2.zero;
                    dot.rectTransform.sizeDelta = new Vector2(7, 7);
                    dot.rectTransform.anchoredPosition = p;
                    route.Add(dot);
                }
            }
        }

        void RefreshPins()
        {
            foreach (var p in pins)
            {
                if (p.def.region != shown) continue;
                bool open = MissionDefs.IsUnlocked(p.def);
                int st = Save.Stars(p.def.id);
                bool done = Save.Completed(p.def.id);
                bool sel = p == selected;
                Color c = !open ? new Color(0.16f, 0.19f, 0.23f, 1f) : (done ? Theme.Success : Theme.Accent);
                p.bg.color = c;
                p.ring.color = sel ? Color.white : new Color(0, 0, 0, 0.55f);
                p.num.gameObject.SetActive(open);
                p.num.color = done ? Color.white : Theme.Ink;
                p.lockIcon.gameObject.SetActive(!open);
                p.stars.text = done ? new string('★', st) + new string('☆', 3 - st) : "";
                p.stars.color = Theme.Accent;
                p.glow.enabled = sel || (open && !done);
                p.glow.color = sel ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 0.7f, 0.12f, 0.8f);
                p.rt.localScale = Vector3.one * (sel ? 1.22f : 1f);
                if (sel) p.rt.SetAsLastSibling();
            }
        }

        void Select(Pin p)
        {
            selected = p;
            RefreshPins();
            var d = p.def;
            var t = d.targets.Count > 0 ? d.targets[0] : null;
            cPortrait.sprite = t != null ? Res.Portrait(t.charId) : null;
            cPortrait.color = cPortrait.sprite != null ? Color.white : new Color(1, 1, 1, 0);
            cKind.text = string.IsNullOrEmpty(d.kind) ? "SUİKAST" : d.kind;
            cKindBg.color = MenuScreen.KindColorOf(cKind.text);
            cNum.text = "GÖREV " + d.number.ToString("00");
            cTitle.text = d.title;
            cName.text = t != null ? t.name + (string.IsNullOrEmpty(t.alias) ? "" : "\n“" + t.alias + "”") : "";
            if (d.targets.Count > 1) cName.text = t.name + "  &  " + d.targets[1].name;
            cLoc.text = d.location;
            int st = Save.Stars(d.id);
            for (int s = 0; s < 3; s++)
            {
                cStars[s].sprite = Res.Icon(s < st ? "star" : "star_outline");
                cStars[s].color = s < st ? Theme.Accent : Theme.TextMute;
            }
            int gr = Save.BestGrade(d.id);
            cGradeBg.gameObject.SetActive(gr > 0);
            if (gr > 0)
            {
                cGrade.text = gr == 5 ? "S" : (gr == 4 ? "A" : (gr == 3 ? "B" : (gr == 2 ? "C" : "D")));
                cGrade.color = gr == 5 ? Theme.Accent : (gr == 4 ? Theme.Success : (gr == 3 ? Theme.Info : Theme.TextDim));
            }
            cVet.gameObject.SetActive(Save.Veteran(d.id));
            float dist = d.Distance;
            cInfo.text = (dist > 0 ? "~" + Mathf.RoundToInt(dist / 5f) * 5 + " m   ·   " : "") + "Rüzgâr " + d.WindSpeed.ToString("0.0") + " m/s   ·   " +
                         PresetInfo.Title(d.preset) + "\nZORLUK " + Difficulty.Score(d) + "/10" + (Difficulty.HasArmor(d) ? "   ·   <color=#FF8A2B>ZIRHLI DÜŞMAN</color>" : "");
            int pw = Gear.Power(Gear.Equipped), rec = Progress.Recommended(d);
            string pc = pw >= rec ? "#36D47C" : (pw >= rec * 0.8f ? "#FFB21E" : "#FF4545");
            cPower.text = "Önerilen güç <b>" + rec + "</b>   ·   Tüfeğin <color=" + pc + "><b>" + pw + "</b></color>";
            cReward.text = UIK.Money(d.reward);
            bool open = MissionDefs.IsUnlocked(d);
            cGo.gameObject.SetActive(open);
            cLock.gameObject.SetActive(!open);
            cLockText.text = "Önceki görevi tamamla";
            Tween.Pop(cardRt, 0.25f, 0f, 0.96f);
        }

        protected override void OnShow()
        {
            laidOutFor = Vector2.zero;
            Show(shown ?? (Game.I != null ? Game.I.LocationId : "harbor"));
        }

        /// <summary>Shows the contracts of one region (or what opens a closed region).</summary>
        public void Show(string locationId)
        {
            shown = locationId;
            var loc = Locations.Get(locationId);
            bool open = Locations.IsOpen(loc);
            foreach (var t in tabs)
            {
                bool sel = t.loc == loc, tabOpen = Locations.IsOpen(t.loc);
                var c = sel ? Theme.Accent : Theme.PanelLight;
                t.bg.color = c;
                t.name.color = sel ? Theme.Ink : Theme.Text;
                t.icon.color = sel ? Theme.Ink : Theme.Text;
                t.sub.color = sel ? new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.75f) : Theme.TextDim;
                t.lockIcon.enabled = !tabOpen;
                t.lockIcon.color = sel ? Theme.Ink : Theme.TextMute;
                var list = MissionDefs.InLocation(t.loc.id);
                int stars = 0;
                foreach (var m in list) stars += Save.Stars(m.id);
                t.sub.text = tabOpen ? t.loc.place + "   ·   " + stars + " / " + list.Count * 3 + " ★" : (t.loc.lockedText ?? "Kilitli");
            }
            lockPanel.gameObject.SetActive(!open);
            mapPanel.gameObject.SetActive(open);
            cardRt.gameObject.SetActive(open);
            if (!open)
            {
                lockTitle.text = loc.name;
                lockText.text = loc.lockedText ?? "Kilitli";
                lockBlurb.text = loc.blurb;
                return;
            }
            mapImg.sprite = Res.Map("region_" + locationId);
            mapImg.color = mapImg.sprite != null ? Color.white : new Color(0.05f, 0.08f, 0.1f, 1f);
            laidOutFor = Vector2.zero;
            safeFor = Vector2.zero;
            Layout();
            // select the first open contract without stars here, otherwise the last open one
            Pin pick = null, last = null;
            foreach (var p in pins)
            {
                if (p.def.region != locationId) continue;
                if (!MissionDefs.IsUnlocked(p.def)) continue;
                last = p;
                if (pick == null && Save.Stars(p.def.id) == 0) pick = p;
            }
            pick = pick ?? last;
            if (pick == null) foreach (var p in pins) if (p.def.region == locationId) { pick = p; break; }
            if (pick != null) Select(pick);
        }

        void Update()
        {
            if (!Visible) return;
            Layout();
            // the next contracts breathe; the selected one bobs
            float t = Time.unscaledTime;
            foreach (var p in pins)
            {
                if (!p.rt.gameObject.activeSelf || !p.glow.enabled) continue;
                float k = 0.5f + 0.5f * Mathf.Sin(t * 3.2f + p.def.number);
                p.glow.rectTransform.localScale = Vector3.one * (0.8f + 0.25f * k);
                var c = p.glow.color; c.a = (p == selected ? 0.85f : 0.45f) + 0.35f * k; p.glow.color = c;
            }
        }
    }
}

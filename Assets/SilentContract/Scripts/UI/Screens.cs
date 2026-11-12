using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SilentContract
{
    static class PresetInfo
    {
        public static string Icon(string preset)
        {
            switch (preset)
            {
                case "night": case "snow_night": return "moon";
                case "overcast": case "rain_night": return "rain";
                case "snow_day": return "snow";
                case "blizzard": return "blizzard";
                default: return "sun";
            }
        }
        public static string Title(string preset) => Env.Find(preset).title;
    }

    // ====================================================================== briefing / target dossier
    public class BriefingScreen : UIScreen
    {
        MissionDef def;
        public MissionDef Def => def;
        Text title, nameT, alias, desc, brief, tip, kindT, goalsT, rifleName, rifleSub, dossierLabel;
        Image vetBtn, vetLine, vetIcon;
        Text vetText;
        Button mapBtn;
        Image body, kindBg;
        Button change;
        readonly Text[] chipValue = new Text[4];
        readonly Image[] chipIcon = new Image[4];
        readonly Image[] objStar = new Image[3];
        readonly Text[] objText = new Text[3];
        // right panel parts (laid out for the panel's real width / height)
        RectTransform panelRt, briefLabelRt, goalLabelRt, starLabelRt, tipRt, loadRt, startRt;
        readonly RectTransform[] chipRt = new RectTransform[4];
        readonly RectTransform[] starRowRt = new RectTransform[3];
        Vector2 laidOutFor;

        protected override void Build()
        {
            var bg = UIK.Img(transform, "Bg", null, new Color(0.02f, 0.03f, 0.04f, 0.9f), false, true);
            UIK.Stretch(bg.rectTransform);
            var head = Header("", () => ui.ShowMissions(), "skull", () => def != null ? "ZORLUK  " + Difficulty.Score(def) + "/10" : "");
            title = head.Find("Title").GetComponent<Text>();
            // V2: VETERAN — a finished contract again, the hard way (beside the difficulty chip)
            vetBtn = UIK.Panel(head, "Veteran", Theme.PanelSoft, "pill");
            UIK.Place(vetBtn.rectTransform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-240, 0), new Vector2(250, 72));
            vetBtn.raycastTarget = true;
            vetLine = UIK.Panel(vetBtn.transform, "line", new Color(1, 1, 1, 0.14f), "pill_line");
            UIK.Stretch(vetLine.rectTransform);
            vetIcon = UIK.Icon(vetBtn.transform, "medal", Theme.TextMute, 34);
            UIK.Place(vetIcon.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(34, 34));
            vetText = UIK.Txt(vetBtn.transform, "T", "VETERAN", Res.FontBold, 30, Theme.TextDim, TextAnchor.MiddleLeft, false);
            UIK.Stretch(vetText.rectTransform, 64, 0, 14, 0);
            var vb = vetBtn.gameObject.AddComponent<Button>();
            vb.targetGraphic = vetBtn;
            vb.onClick.AddListener(ToggleVeteran);
            // V2: the tactical map
            mapBtn = UIK.Btn(head, "MapBtn", "HARİTA", BtnStyle.Secondary, () => ui.ShowMap(def), "map", 30);
            UIK.Place((RectTransform)mapBtn.transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-506, 0), new Vector2(214, 72));

            // ---- dossier card (left)
            var card = UIK.Panel(Safe, "Dossier", Theme.Panel, "rr22");
            var crt = card.rectTransform;
            crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(0, 1);
            crt.pivot = new Vector2(0, 1);
            crt.offsetMin = new Vector2(24, 24); crt.offsetMax = new Vector2(724, -140);
            var cl = UIK.Panel(card.transform, "line", new Color(1, 1, 1, 0.1f), "rr22_line");
            UIK.Stretch(cl.rectTransform);
            dossierLabel = UIK.Txt(card.transform, "Label", "HEDEF DOSYASI", Res.FontSemi, 24, Theme.Danger, TextAnchor.UpperLeft);
            UIK.Place(dossierLabel.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(28, -22), new Vector2(400, 34));
            // the frame is the studio grey of the full-body renders (73,77,81): the figure no longer floats in a grey
            // box with a black band above and below it
            var frame = UIK.Panel(card.transform, "Frame", new Color(73f / 255f, 77f / 255f, 81f / 255f, 1f), "rr14");
            var frt = frame.rectTransform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(0, 1);
            frt.pivot = new Vector2(0, 1);
            frt.offsetMin = new Vector2(24, 24); frt.offsetMax = new Vector2(330, -70);
            body = UIK.Img(frame.transform, "Body", null, Color.white);
            body.preserveAspect = true;
            UIK.Stretch(body.rectTransform, 6, 6, 6, 6);

            nameT = UIK.Txt(card.transform, "Name", "", Res.FontBold, 46, Theme.Text, TextAnchor.UpperLeft);
            UIK.Place(nameT.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(352, -70), new Vector2(324, 112));
            alias = UIK.Txt(card.transform, "Alias", "", Res.FontSemi, 30, Theme.Accent, TextAnchor.UpperLeft);
            UIK.Place(alias.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(352, -186), new Vector2(324, 42));
            kindBg = UIK.Panel(card.transform, "Tag", Theme.Danger, "pill");
            UIK.Place(kindBg.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(352, -240), new Vector2(220, 44));
            kindT = UIK.Txt(kindBg.transform, "T", "SUİKAST", Res.FontBold, 26, Color.white, TextAnchor.MiddleCenter);
            UIK.Stretch(kindT.rectTransform, 8, 0, 8, 0);
            var tanima = UIK.Txt(card.transform, "IdLabel", "TANIMA", Res.FontSemi, 24, Theme.TextMute, TextAnchor.UpperLeft);
            UIK.Place(tanima.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(352, -310), new Vector2(324, 34));
            desc = UIK.Txt(card.transform, "Desc", "", Res.FontBody, 28, Theme.Text, TextAnchor.UpperLeft, true, 18);
            var drt = desc.rectTransform;
            drt.anchorMin = new Vector2(0, 0); drt.anchorMax = new Vector2(0, 1);
            drt.pivot = new Vector2(0, 1);
            drt.offsetMin = new Vector2(352, 28); drt.offsetMax = new Vector2(676, -350);

            // ---- contract panel (right)
            var panel = UIK.Rect(Safe, "Panel");
            panel.anchorMin = new Vector2(0, 0); panel.anchorMax = new Vector2(1, 1);
            panel.offsetMin = new Vector2(756, 24); panel.offsetMax = new Vector2(-24, -140);
            panelRt = panel;
            var bl = UIK.Txt(panel, "BriefLabel", "BRİFİNG", Res.FontSemi, 24, Theme.Accent, TextAnchor.UpperLeft);
            briefLabelRt = bl.rectTransform;
            brief = UIK.Txt(panel, "Brief", "", Res.FontBody, 28, Theme.TextDim, TextAnchor.UpperLeft, true, 18);

            var gl = UIK.Txt(panel, "GoalLabel", "GÖREV", Res.FontSemi, 24, Theme.Accent, TextAnchor.UpperLeft);
            goalLabelRt = gl.rectTransform;
            goalsT = UIK.Txt(panel, "Goals", "", Res.FontSemi, 27, Theme.Text, TextAnchor.UpperLeft, true, 18);

            for (int i = 0; i < 4; i++)
            {
                var chip = UIK.Panel(panel, "Info" + i, Theme.PanelSoft, "rr14");
                var r = chip.rectTransform;
                chipRt[i] = r;
                chipIcon[i] = UIK.Icon(chip.transform, "sun", Theme.Accent, 32);
                UIK.Place(chipIcon[i].rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(32, 32));
                chipValue[i] = UIK.Txt(chip.transform, "V", "", Res.FontBold, 28, Theme.Text, TextAnchor.MiddleLeft, true, 15);
                UIK.Stretch(chipValue[i].rectTransform, 52, 6, 8, 6);
            }

            var ol = UIK.Txt(panel, "ObjLabel", "YILDIZ HEDEFLERİ", Res.FontSemi, 24, Theme.Accent, TextAnchor.UpperLeft);
            starLabelRt = ol.rectTransform;
            for (int i = 0; i < 3; i++)
            {
                var row = UIK.Rect(panel, "Obj" + i);
                starRowRt[i] = row;
                objStar[i] = UIK.Icon(row, "star_outline", Theme.TextMute, 34);
                UIK.Place(objStar[i].rectTransform, UIK.ML, new Vector2(0, 0.5f), Vector2.zero, new Vector2(34, 34));
                objText[i] = UIK.Txt(row, "T", "", Res.FontBody, 27, Theme.Text, TextAnchor.MiddleLeft, true, 18);
                UIK.Stretch(objText[i].rectTransform, 50, 0, 0, 0);
            }

            var tipBox = UIK.Panel(panel, "Tip", new Color(0.27f, 0.78f, 0.95f, 0.1f), "rr14");
            var trt = tipBox.rectTransform;
            tipRt = trt;
            var ti = UIK.Icon(tipBox.transform, "info", Theme.Info, 32);
            UIK.Place(ti.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(14, -14), new Vector2(32, 32));
            tip = UIK.Txt(tipBox.transform, "T", "", Res.FontBody, 23, Theme.TextDim, TextAnchor.UpperLeft, true, 14);
            UIK.Stretch(tip.rectTransform, 58, 8, 14, 10);

            // loadout: the rifle carried into the contract and what the handler hands out
            var load = UIK.Panel(panel, "Loadout", Theme.PanelSoft, "rr14");
            var lrt = load.rectTransform;
            loadRt = lrt;
            var ri = UIK.Icon(load.transform, "rifle", Theme.Accent, 40);
            UIK.Place(ri.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(40, 40));
            rifleName = UIK.Txt(load.transform, "Rifle", "", Res.FontBold, 30, Theme.Text, TextAnchor.UpperLeft, true, 18);
            rifleName.rectTransform.anchorMin = new Vector2(0, 1); rifleName.rectTransform.anchorMax = new Vector2(1, 1);
            rifleName.rectTransform.pivot = new Vector2(0, 1);
            rifleName.rectTransform.offsetMin = new Vector2(72, -52); rifleName.rectTransform.offsetMax = new Vector2(-226, -12);
            rifleSub = UIK.Txt(load.transform, "Sub", "", Res.FontSemi, 22, Theme.TextDim, TextAnchor.UpperLeft, true, 14);
            rifleSub.rectTransform.anchorMin = new Vector2(0, 0); rifleSub.rectTransform.anchorMax = new Vector2(1, 1);
            rifleSub.rectTransform.offsetMin = new Vector2(72, 8); rifleSub.rectTransform.offsetMax = new Vector2(-226, -54);
            change = UIK.Btn(load.transform, "Change", "DEĞİŞTİR", BtnStyle.Secondary, () =>
            {
                var d = def;
                ui.ShowArmory(() => ui.ShowBriefing(d));
            }, "settings", 26);
            UIK.Place((RectTransform)change.transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(196, 80));

            var start = UIK.Btn(panel, "Start", "BAŞLA", BtnStyle.Primary, OnStart, "scope", 46);
            startRt = (RectTransform)start.transform;
            BuildNestPicker();
            BuildChapterCard();
            laidOutFor = Vector2.zero;
            LayoutPanel();
        }

        // ---- V2: where to shoot from (only when more than one place sees this contract)
        RectTransform nestPick, nestList;

        void OnStart()
        {
            if (def == null) return;
            var miss = def.MissingGear();
            if (miss.Count > 0 && !Save.UnlockAll)
            {
                var d0 = def;
                ui.Confirm("EKİPMAN GEREKLİ", "Bu iş " + string.Join(" ve ", miss.ToArray()) + " olmadan yapılamaz. Cephaneliğe git?", "CEPHANELİK",
                           () => ui.ShowArmory(() => ui.ShowBriefing(d0)));
                return;
            }
            // V2: a rifle well under the contract's recommended power: one honest warning per contract (upgrade, or go anyway)
            var w = Gear.Equipped;
            int pw = Gear.Power(w), rec = Progress.Recommended(def);
            bool loaned = false;
            foreach (var l in def.loan) if (l.StartsWith("w:")) loaned = true;
            if (!loaned && pw < rec * 0.75f && !Save.Flag("weak_" + def.id))
            {
                Save.SetFlag("weak_" + def.id);
                Analytics.Event("offer_shown", "offer", "weak_rifle", "placement", "briefing", "contract_no", def.number, "power", pw, "recommended", rec);
                var d1 = def;
                ui.Confirm("TÜFEĞİN ZAYIF KALABİLİR", "Bu sözleşme için önerilen güç " + rec + ", " + w.name + " şu an " + pw + ".\n" +
                           "Geliştirmeden de deneyebilirsin ama zırhlılar ve uzak hedefler zorlar.", "GELİŞTİR",
                           () => ui.ShowArmory(() => ui.ShowBriefing(d1)), "YİNE DE BAŞLA", () => { MissionRuntime.PreferredNest = null; game.StartMission(d1); });
                return;
            }
            // armoured guards and nothing that stops them: three armour piercing rounds for a video (once per contract)
            if (!loaned && Difficulty.HasArmor(def) && Gear.Effective(w).power < Gear.ArmorPower &&
                Gear.AmmoAvailable(AmmoKind.ArmorPiercing) <= 0 && Ads.RewardedReady && !Save.Flag("ap_offer_" + def.id))
            {
                Save.SetFlag("ap_offer_" + def.id);
                var d2 = def;
                Analytics.Event("offer_shown", "offer", "ap_ad", "placement", "briefing", "contract_no", def.number);
                ui.Confirm("ZIRHLI HEDEFLER", "Bu işte zırhlı silahlılar var; tüfeğinin mermisi gövdeden geçmez.\nKısa bir video izle, 3 zırh delici mermi senin olsun.",
                           "İZLE  +3 MERMİ", () => Ads.ShowRewarded("briefing_ap", ok =>
                           {
                               if (ok)
                               {
                                   string k = AmmoKind.ArmorPiercing.ToString();
                                   Save.SetAmmoCount(k, Save.AmmoCount(k) + 3);
                                   Analytics.Event("earn_item", "item", "ammo_ap", "amount", 3, "source", "ad");
                               }
                               MissionRuntime.PreferredNest = null;
                               game.StartMission(d2);
                           }), "BAŞLA", () => { MissionRuntime.PreferredNest = null; game.StartMission(d2); });
                return;
            }
            // the contract always starts from the main nest (the other places are reached with YER DEĞİŞTİR in the
            // contract); the picker below stays unused
            MissionRuntime.PreferredNest = null;
            game.StartMission(def);
        }

        void ToggleVeteran()
        {
            if (def == null || !Save.Completed(def.id)) return;
            Difficulty.VeteranId = Difficulty.IsVeteran(def) ? null : def.id;
            if (!Difficulty.IsVeteran(def)) Difficulty.DailyId = null;
            Sfx.UI(Sfx.Confirm);
            RefreshVeteran();
            if (Difficulty.IsVeteran(def)) Tween.Pop(vetBtn.transform, 0.3f, 0f, 0.85f);
        }

        /// <summary>The switch (only on a finished contract), its look, and the tip box: the veteran rules on top.</summary>
        void RefreshVeteran()
        {
            var d = def;
            if (d == null) return;
            bool can = Save.Completed(d.id);
            bool on = can && Difficulty.IsVeteran(d);
            vetBtn.gameObject.SetActive(can);
            vetBtn.color = on ? new Color(0.42f, 0.27f, 0.02f, 0.95f) : Theme.PanelSoft;
            vetIcon.color = on ? Theme.Accent : (Save.Veteran(d.id) ? Theme.Accent : Theme.TextMute);
            vetText.color = on ? Theme.Accent : Theme.TextDim;
            bool daily = Difficulty.IsDaily(d) && !Save.DailyDone(Difficulty.TodayKey);
            vetText.text = daily ? "GÜNÜN İŞİ ×2" : (on ? "VETERAN ×1,5" : "VETERAN");
            var nsk = Difficulty.NewSkill(d);
            string baseTip = string.IsNullOrEmpty(nsk) ? d.tip : "<b><color=#E8B04A>YENİ:</color></b> " + nsk + "\n" + d.tip;
            tip.text = on ? "<b><color=#FFB21E>VETERAN:</color></b> isabet noktası yok, rüzgâr daha sert ve dalgalı, tüfek daha çok sallanır, hedef işaretlenmez. Ödül ×1,5.\n" + d.tip
                          : baseTip;
            tip.supportRichText = true;
        }

        void BuildNestPicker()
        {
            var shade = UIK.Rect(transform, "NestPickShade");
            UIK.Stretch(shade);
            var sh = UIK.Img(shade, "Dark", null, new Color(0, 0, 0, 0.7f), false, true);
            UIK.Stretch(sh.rectTransform);
            nestPick = UIK.Rect(shade, "NestPick");
            UIK.Place(nestPick, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(948, 520));
            var bg = UIK.Panel(nestPick, "Bg", Theme.Panel, "rr22");
            UIK.Stretch(bg.rectTransform);
            var t = UIK.Txt(nestPick, "Title", "NEREDEN ATEŞ EDECEKSİN?", Res.FontBold, 36, Theme.Accent, TextAnchor.MiddleLeft, false);
            UIK.Place(t.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(24, -14), new Vector2(800, 52));
            var sub = UIK.Txt(nestPick, "Sub", "Her yerin artısı eksisi var. Görev sırasında iki kez yer değiştirebilirsin.", Res.FontBody, 22, Theme.TextDim, TextAnchor.MiddleLeft, false);
            UIK.Place(sub.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(24, -64), new Vector2(900, 30));
            nestList = UIK.Rect(nestPick, "List");
            UIK.Place(nestList, UIK.TL, new Vector2(0, 1), new Vector2(24, -108), new Vector2(900, 400));
            var close = UIK.Btn(nestPick, "Back", "GERİ", BtnStyle.Ghost, () => shade.gameObject.SetActive(false), null, 26);
            UIK.Place((RectTransform)close.transform, UIK.TR, new Vector2(1, 1), new Vector2(-16, -14), new Vector2(150, 56));
            nestPick = shade;   // what is shown / hidden is the whole overlay
            nestPickPanel = (RectTransform)t.transform.parent;
            shade.gameObject.SetActive(false);
        }

        RectTransform nestPickPanel;

        // ---- V2: the chapter card before the first contract of each map
        RectTransform chapter;
        Text chapterNo, chapterTitle, chapterText;

        void BuildChapterCard()
        {
            chapter = UIK.Rect(transform, "Chapter");
            UIK.Stretch(chapter);
            var dk = UIK.Img(chapter, "Dark", null, new Color(0.005f, 0.008f, 0.012f, 1f), false, true);
            UIK.Stretch(dk.rectTransform);
            chapterNo = UIK.Txt(chapter, "No", "", Res.FontSemi, 34, Theme.Accent, TextAnchor.UpperLeft, false);
            UIK.Place(chapterNo.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(180, 250), new Vector2(1400, 50));
            chapterTitle = UIK.Txt(chapter, "Title", "", Res.FontBold, 88, Theme.Text, TextAnchor.UpperLeft, false);
            UIK.Place(chapterTitle.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(176, 170), new Vector2(1700, 110));
            var bar = UIK.Img(chapter, "Bar", null, Theme.Accent);
            UIK.Place(bar.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(182, 96), new Vector2(140, 6));
            chapterText = UIK.Txt(chapter, "Text", "", Res.FontBody, 34, Theme.TextDim, TextAnchor.UpperLeft, true, 22);
            UIK.Place(chapterText.rectTransform, UIK.ML, new Vector2(0, 1f), new Vector2(180, 60), new Vector2(1300, 380));
            var go = UIK.Btn(chapter, "Go", "DEVAM", BtnStyle.Primary, CloseChapter, "next", 36);
            UIK.Place((RectTransform)go.transform, UIK.BR, new Vector2(1, 0), new Vector2(-80, 60), new Vector2(360, 104));
            chapter.gameObject.SetActive(false);
        }

        // the briefing under the card is hidden while it is up (test 1: it showed through while the screen faded in)
        readonly List<GameObject> hiddenUnderChapter = new List<GameObject>();

        void CloseChapter()
        {
            if (chapter != null) chapter.gameObject.SetActive(false);
            foreach (var g in hiddenUnderChapter) if (g != null) g.SetActive(true);
            hiddenUnderChapter.Clear();
            Sfx.StopVoices();
        }

        void HideUnderChapter()
        {
            foreach (Transform ch in transform)
                if (ch != chapter && ch.gameObject.activeSelf) { ch.gameObject.SetActive(false); hiddenUnderChapter.Add(ch.gameObject); }
        }

        void MaybeChapter(MissionDef d)
        {
            if (chapter == null) return;
            if (hiddenUnderChapter.Count > 0) CloseChapter();
            if (!Story.OpensChapter(d)) { chapter.gameObject.SetActive(false); return; }
            var c = Story.ChapterOf(d);
            string key = "chapter_" + (c != null ? c.number : 0);
            if (c == null || Save.Seen(key)) { chapter.gameObject.SetActive(false); return; }
            Save.SetSeen(key);
            chapterNo.text = "BÖLÜM " + c.number;
            chapterTitle.text = c.title;
            chapterText.text = c.text;
            chapter.gameObject.SetActive(true);
            chapter.SetAsLastSibling();
            HideUnderChapter();
            // no narration voice: the story is told by the text on the card (the old recordings stay unused)
        }

        /// <summary>
        /// Right panel layout for its real size. Wide (phones): loadout and BAŞLA share the bottom row. Narrow (4:3 /
        /// 11" iPads): BAŞLA gets its own full-width row, the loadout spans the panel above it, and the briefing text
        /// box is a little shorter so the star list still fits.
        /// </summary>
        void LayoutPanel()
        {
            if (panelRt == null) return;
            var size = panelRt.rect.size;
            if (size.x < 10f || (size - laidOutFor).sqrMagnitude < 1f) return;
            laidOutFor = size;
            bool narrow = size.x < 1000f;
            float briefH = narrow ? 136f : 172f;
            float y = 0f;
            TopRow(briefLabelRt, y, 34); y -= 38f;
            TopRow(brief.rectTransform, y, briefH); y -= briefH + 12f;
            TopRow(goalLabelRt, y, 30); y -= 32f;
            TopRow(goalsT.rectTransform, y, 100); y -= 110f;
            for (int i = 0; i < 4; i++)
            {
                var r = chipRt[i];
                r.anchorMin = new Vector2(i / 4f, 1); r.anchorMax = new Vector2((i + 1) / 4f, 1);
                r.pivot = new Vector2(0.5f, 1);
                r.offsetMin = new Vector2(i == 0 ? 0 : 8, y - 76f); r.offsetMax = new Vector2(i == 3 ? 0 : -8, y);
            }
            y -= 92f;
            TopRow(starLabelRt, y, 30); y -= 34f;
            for (int i = 0; i < 3; i++) TopRow(starRowRt[i], y - i * 44f, 40);
            // bottom stack
            float b = 0f;
            if (narrow)
            {
                BottomRow(startRt, 0f, 0f, 96f, 0f);
                b = 96f + 12f;
                BottomRow(loadRt, 0f, b, b + 108f, 0f);
                b += 108f + 12f;
            }
            else
            {
                startRt.anchorMin = startRt.anchorMax = new Vector2(1, 0);
                startRt.pivot = new Vector2(1, 0);
                startRt.anchoredPosition = Vector2.zero;
                startRt.sizeDelta = new Vector2(380, 108);
                BottomRow(loadRt, 0f, 0f, 108f, 404f);
                b = 120f;
            }
            // tip box: between the stars and the bottom rows (dropped when there is no room)
            float starsBottom = -y + 2 * 44f + 40f;                  // distance of the last star row's bottom from the top
            float room = size.y - starsBottom - b - 12f;
            bool showTip = room >= 64f;
            tipRt.gameObject.SetActive(showTip);
            if (showTip) BottomRow(tipRt, 0f, b, b + Mathf.Min(92f, room), 0f);
        }

        static void BottomRow(RectTransform r, float x0, float y0, float y1, float rightInset)
        {
            r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0);
            r.pivot = new Vector2(0, 0);
            r.offsetMin = new Vector2(x0, y0); r.offsetMax = new Vector2(-rightInset, y1);
        }

        void Update()
        {
            if (Visible) LayoutPanel();
        }

        static void TopRow(RectTransform r, float y, float h)
        {
            r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0, 1);
            r.offsetMin = new Vector2(0, y - h); r.offsetMax = new Vector2(0, y);
        }

        static Color KindColor(string kind)
        {
            switch (kind)
            {
                case "KORUMA": return Theme.Info;
                case "CANLI YAKALA": return Theme.Success;
                case "SABOTAJ": return Theme.Accent;
                case "YARALA": return Theme.Guard;
                default: return Theme.Danger;
            }
        }

        public void Set(MissionDef d)
        {
            def = d;
            if (hiddenUnderChapter.Count > 0) CloseChapter();
            title.text = "GÖREV " + d.number.ToString("00") + "  ·  " + d.title;
            var t = d.targets.Count > 0 ? d.targets[0] : null;
            nameT.text = t != null ? t.name : "";
            alias.text = t != null && !string.IsNullOrEmpty(t.alias) ? "“" + t.alias + "”" : "";
            desc.text = t != null ? t.desc : "";
            if (d.targets.Count > 1)
            {
                // two targets on one contract: both names, both descriptions
                var names = new System.Text.StringBuilder();
                var aliases = new System.Text.StringBuilder();
                var descs = new System.Text.StringBuilder();
                foreach (var x in d.targets)
                {
                    if (names.Length > 0) { names.Append("  &  "); aliases.Append("  ·  "); descs.Append('\n'); }
                    names.Append(x.name);
                    aliases.Append("“").Append(x.alias).Append("”");
                    descs.Append(x.alias).Append(": ").Append(x.desc);
                }
                nameT.text = names.ToString();
                alias.text = aliases.ToString();
                desc.text = descs.ToString();
            }
            body.sprite = t != null ? Res.FullBody(t.charId) : null;
            body.color = body.sprite != null ? Color.white : new Color(1, 1, 1, 0);
            string kind = string.IsNullOrEmpty(d.kind) ? "SUİKAST" : d.kind;
            kindT.text = kind;
            kindBg.color = KindColor(kind);
            bool protect = kind == "KORUMA";
            dossierLabel.text = protect ? "KORUNACAK KİŞİ" : (kind == "CANLI YAKALA" ? "HEDEF DOSYASI  ·  CANLI" : "HEDEF DOSYASI");
            dossierLabel.color = protect ? Theme.Info : Theme.Danger;
            brief.text = d.brief;
            if (Difficulty.VeteranId != d.id) Difficulty.VeteranId = null;      // the switch is per visit to this briefing
            if (Difficulty.DailyId != d.id) Difficulty.DailyId = null;
            RefreshVeteran();
            mapBtn.gameObject.SetActive(MapScreen.Has(d));
            // without the veteran switch the map button takes its place beside the difficulty chip
            ((RectTransform)mapBtn.transform).anchoredPosition = new Vector2(Save.Completed(d.id) ? -506 : -240, 0);
            // the title stops before the buttons beside the difficulty chip
            bool hasMap = MapScreen.Has(d), hasVet = Save.Completed(d.id);
            title.rectTransform.offsetMax = new Vector2(hasMap ? (hasVet ? -740f : -480f) : (hasVet ? -510f : -240f), 0f);
            var goals = new System.Text.StringBuilder();
            if (d.goals.Count == 0) goals.Append("•  Hedefi etkisiz hale getir");
            for (int i = 0; i < d.goals.Count; i++) { if (i > 0) goals.Append('\n'); goals.Append("•  ").Append(d.goals[i]); }
            goalsT.text = goals.ToString();
            float dist = d.Distance;
            SetChip(0, PresetInfo.Icon(d.preset), Env.Find(d.preset).title);
            SetChip(1, "wind", d.WindSpeed.ToString("0.0") + " m/s");
            SetChip(2, "distance", dist > 0 ? "~" + Mathf.RoundToInt(dist / 5f) * 5 + " m" : "—");
            SetChip(3, "cash", UIK.Money(d.reward));
            var ch = d.Challenges;
            for (int i = 0; i < 3; i++)
            {
                bool has = i < ch.Count;
                objText[i].text = has ? ch[i].text : "";
                bool got = has && Save.Challenge(d.id, i);
                objStar[i].sprite = Res.Icon(got ? "star" : "star_outline");
                objStar[i].color = got ? Theme.Accent : Theme.TextMute;
                objStar[i].enabled = has;
            }
            // loadout
            WeaponDef loanW = null;
            var loans = new List<string>();
            foreach (var l in d.loan)
            {
                var parts = l.Split(':');
                if (parts[0] == "w" && parts.Length > 1) { loanW = Gear.Weapon(parts[1]); continue; }
                var a = Gear.AmmoById(parts[0]);
                if (a != null) { loans.Add((parts.Length > 1 ? parts[1] : "3") + "× " + a.name); continue; }
                var g = Gear.Gadget(parts[0]);
                if (g != null) loans.Add(g.name);
            }
            var w = loanW ?? Gear.Equipped;
            // V2: the rifle's power against what the contract is balanced for
            int pw = Gear.Power(w), rec = Progress.Recommended(d);
            string pc = pw >= rec ? "#36D47C" : (pw >= rec * 0.8f ? "#FFB21E" : "#FF4545");
            rifleName.supportRichText = true;
            rifleName.text = "TÜFEK:  " + w.name + "   <size=26><color=" + pc + ">GÜÇ " + pw + "</color><color=#6B7682>  /  önerilen " + rec + "</color></size>";
            string sub = loanW != null ? "Bu iş için verildi" : w.caliber;
            if (loans.Count > 0) sub += "   ·   Verilen: " + string.Join(", ", loans.ToArray());
            else if (!string.IsNullOrEmpty(d.gearHint)) sub += "   ·   " + d.gearHint;
            if (Difficulty.HasArmor(d) && Gear.Effective(w).power < Gear.ArmorPower && Gear.AmmoAvailable(AmmoKind.ArmorPiercing) <= 0)
                sub += "   ·   <color=#FF8A2B>Zırhlı silahlılar: kafaya nişan al, HASAR geliştir ya da zırh delici al</color>";
            rifleSub.supportRichText = true;
            rifleSub.text = sub;
            change.gameObject.SetActive(loanW == null);
            UIK.SetBtnLabel(change, pw < rec && loanW == null ? "GELİŞTİR" : "DEĞİŞTİR");
            MaybeChapter(d);
        }

        void SetChip(int i, string icon, string value)
        {
            chipIcon[i].sprite = Res.Icon(icon);
            chipValue[i].text = value;
        }
    }

    // ====================================================================== settings
    public class SettingsScreen : UIScreen
    {
        public bool FromPause;
        Slider sens, vol;
        Text sensV, volV;
        PillToggle assist, killcam, vib;
        Button[] quality = new Button[3];

        protected override void Build()
        {
            var bg = UIK.Img(transform, "Bg", null, new Color(0.02f, 0.03f, 0.04f, 0.94f), false, true);
            UIK.Stretch(bg.rectTransform);
            Header("AYARLAR", () => ui.CloseSettings());
            var panel = UIK.Panel(Safe, "Panel", Theme.Panel, "rr22");
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 1); prt.anchorMax = new Vector2(0.5f, 1);
            prt.pivot = new Vector2(0.5f, 1);
            prt.anchoredPosition = new Vector2(0, -140);
            prt.sizeDelta = new Vector2(1180, 9 * RowH + 32);
            float y = -16;
            sens = SliderRow(panel.transform, "Nişan hassasiyeti", ref y, 0.3f, 2.5f, out sensV, v => { Save.Sensitivity = v; if (game.Sniper) game.Sniper.Sensitivity = v; });
            assist = ToggleRow(panel.transform, "Kolay nişan (3. yıldız alınmaz)", ref y, on => { Save.AimAssist = on; if (game.Sniper) { game.Sniper.AimAssist = on; game.Sniper.RefreshDifficulty(); } });
            killcam = ToggleRow(panel.transform, "Kurşun kamerası (son hedefte ağır çekim)", ref y, on => Save.KillCam = on);
            vib = ToggleRow(panel.transform, "Titreşim", ref y, on => Save.Vibration = on);
            vol = SliderRow(panel.transform, "Ses seviyesi", ref y, 0f, 1f, out volV, v => Save.Volume = v);
            var row = Row(panel.transform, "İlerleme", ref y);
            var reset = UIK.Btn(row, "Reset", "İLERLEMEYİ SIFIRLA", BtnStyle.Ghost, () =>
                ui.Confirm("İlerleme sıfırlansın mı?", "Tüm yıldızlar, para ve geliştirmeler silinecek (satın aldıkların ve elmasların kalır). Bu işlem geri alınamaz.", "SIFIRLA",
                           () => { Save.ResetAll(); Analytics.Event("progress_reset"); Load(); }));
            UIK.Place((RectTransform)reset.transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(430, 80));
            // purchases and privacy: restore (App Store rule) and, where the law asks for it, the consent choices
            var row2 = Row(panel.transform, "Satın alımlar ve gizlilik", ref y);
            var restore = UIK.Btn(row2, "Restore", "GERİ YÜKLE", BtnStyle.Ghost, () => Store.Restore(n =>
                ui.Confirm("GERİ YÜKLEME", n > 0 ? n + " satın alım geri yüklendi." : "Geri yüklenecek satın alım bulunamadı.", "TAMAM", null, "KAPAT")), "retry", 24);
            UIK.Place((RectTransform)restore.transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(300, 80));
            privacyBtn = UIK.Btn(row2, "Privacy", "GİZLİLİK", BtnStyle.Ghost, () =>
            {
                if (Privacy.OptionsRequired) Privacy.ShowOptions();
                else if (!string.IsNullOrEmpty(MonetizationConfig.PrivacyUrl)) Application.OpenURL(MonetizationConfig.PrivacyUrl);
            }, "shield", 24);
            UIK.Place((RectTransform)privacyBtn.transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-340, 0), new Vector2(260, 80));
            // language: the label always carries the English word too, so anyone can find it
            var rowR = Row(panel.transform, "Oyunu beğendin mi?", ref y);
            var rate = UIK.Btn(rowR, "Rate", "PUANLA", BtnStyle.Ghost, Review.OpenStorePage, "star", 26);
            UIK.Place((RectTransform)rate.transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(300, 76));
            var row3 = Row(panel.transform, "Dil  ·  Language", ref y);
            var langBtn = UIK.Btn(row3, "Lang", "", BtnStyle.Ghost, OpenLanguages, "flag", 26);
            UIK.Place((RectTransform)langBtn.transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(430, 80));
            langLabel = langBtn.transform.Find("label")?.GetComponent<Text>();
            BuildLanguagePicker();
        }

        Button privacyBtn;
        Text langLabel;
        GameObject langPicker;
        const float RowH = 88f;

        void ShowLangName()
        {
            if (langLabel is LocText lt) lt.SetRaw(Loc.NativeName(Loc.Lang));
            else if (langLabel != null) langLabel.text = Loc.NativeName(Loc.Lang);
        }

        void OpenLanguages()
        {
            if (langPicker == null) return;
            langPicker.SetActive(true);
            langPicker.transform.SetAsLastSibling();
        }

        /// <summary>The 16 languages, each written in itself (4 × 4).</summary>
        void BuildLanguagePicker()
        {
            var dim = UIK.Img(transform, "LangPicker", null, new Color(0f, 0f, 0f, 0.82f), false, true);
            UIK.Stretch(dim.rectTransform);
            langPicker = dim.gameObject;
            var closeBg = dim.gameObject.AddComponent<Button>();
            closeBg.transition = Selectable.Transition.None;
            closeBg.onClick.AddListener(() => langPicker.SetActive(false));
            var box = UIK.Panel(dim.transform, "Box", Theme.Panel, "rr22");
            var brt = box.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(1240, 4 * 100 + 150);
            box.raycastTarget = true;
            var title = UIK.Txt(box.transform, "Title", "Dil  ·  Language", Res.FontBold, 40, Theme.Text, TextAnchor.MiddleCenter);
            UIK.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(1100, 70));
            var langs = Loc.Languages;
            for (int i = 0; i < langs.Length; i++)
            {
                string code = langs[i].code;
                int col = i % 4, rowI = i / 4;
                var b = UIK.Btn(box.transform, "L_" + code, "", code == Loc.Lang ? BtnStyle.Primary : BtnStyle.Secondary, () =>
                {
                    langPicker.SetActive(false);
                    if (code == Loc.Lang) return;
                    Loc.Set(code);
                    RefreshLanguageButtons();
                    ShowLangName();
                }, null, 28);
                UIK.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                          new Vector2(-450 + col * 300, -120 - rowI * 100), new Vector2(284, 84));
                var lbl = b.transform.Find("label")?.GetComponent<Text>();
                if (lbl is LocText lt) lt.SetRaw(langs[i].native);
                else if (lbl != null) lbl.text = langs[i].native;
                langButtons.Add(new KeyValuePair<string, Button>(code, b));
            }
            langPicker.SetActive(false);
        }

        readonly List<KeyValuePair<string, Button>> langButtons = new List<KeyValuePair<string, Button>>();

        void RefreshLanguageButtons()
        {
            foreach (var kv in langButtons)
            {
                bool on = kv.Key == Loc.Lang;
                var img = kv.Value.GetComponent<Image>();
                var c = on ? Theme.Accent : Theme.PanelLight;
                img.color = c;
                var fx = kv.Value.GetComponent<ButtonFX>();
                if (fx != null) fx.Bind(img, c);
                var t = kv.Value.transform.Find("label")?.GetComponent<Text>();
                if (t != null) t.color = on ? Theme.Ink : Theme.Text;
            }
        }

        RectTransform Row(Transform parent, string label, ref float y)
        {
            var r = UIK.Rect(parent, "Row");
            r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.offsetMin = new Vector2(0, y - RowH); r.offsetMax = new Vector2(0, y);
            var t = UIK.Txt(r, "Label", label, Res.FontBody, 31, Theme.Text, TextAnchor.MiddleLeft, true, 20);
            t.rectTransform.anchorMin = new Vector2(0, 0); t.rectTransform.anchorMax = new Vector2(0.48f, 1);
            t.rectTransform.offsetMin = new Vector2(36, 0); t.rectTransform.offsetMax = new Vector2(0, 0);
            if (y < -20)
            {
                var d = UIK.Img(r, "Sep", null, new Color(1, 1, 1, 0.06f));
                d.rectTransform.anchorMin = new Vector2(0, 1); d.rectTransform.anchorMax = new Vector2(1, 1);
                d.rectTransform.offsetMin = new Vector2(30, -1); d.rectTransform.offsetMax = new Vector2(-30, 1);
            }
            y -= RowH;
            return r;
        }

        Slider SliderRow(Transform parent, string label, ref float y, float min, float max, out Text valueText, Action<float> onChange)
        {
            var r = Row(parent, label, ref y);
            var vt = UIK.Txt(r, "Value", "", Res.FontMono, 30, Theme.Accent, TextAnchor.MiddleRight);
            UIK.Place(vt.rectTransform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(90, 60));
            valueText = vt;
            var s = UIKSlider.Create(r, min, max);
            var srt = (RectTransform)s.transform;
            UIK.Place(srt, UIK.MR, new Vector2(1, 0.5f), new Vector2(-130, 0), new Vector2(420, 64));
            var vref = vt;
            s.onValueChanged.AddListener(v => { vref.text = max <= 1.01f ? Mathf.RoundToInt(v * 100) + "%" : v.ToString("0.0"); onChange(v); });
            return s;
        }

        PillToggle ToggleRow(Transform parent, string label, ref float y, Action<bool> onChange)
        {
            var r = Row(parent, label, ref y);
            var t = PillToggle.Create(r, onChange);
            UIK.Place((RectTransform)t.transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(124, 64));
            return t;
        }

        void QualityRow(Transform parent, ref float y)
        {
            var r = Row(parent, "Grafik kalitesi", ref y);
            string[] names = { "PERFORMANS", "DENGELİ", "KALİTE" };
            for (int i = 0; i < 3; i++)
            {
                int q = i;
                var b = UIK.Btn(r, "Q" + i, names[i], BtnStyle.Secondary, () => { Save.Quality = q; game.ApplyQuality(q); RefreshQuality(); }, null, 26);
                UIK.Place((RectTransform)b.transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-24 - (2 - i) * 188, 0), new Vector2(176, 72));
                quality[i] = b;
            }
        }

        void RefreshQuality()
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = Save.Quality == i;
                var img = quality[i].GetComponent<Image>();
                var c = on ? Theme.Accent : Theme.PanelLight;
                img.color = c;
                quality[i].GetComponent<ButtonFX>().Bind(img, c);
                var t = quality[i].transform.Find("label").GetComponent<Text>();
                t.color = on ? Theme.Ink : Theme.Text;
            }
        }

        void Load()
        {
            sens.SetValueWithoutNotify(Save.Sensitivity); sensV.text = Save.Sensitivity.ToString("0.0");
            vol.SetValueWithoutNotify(Save.Volume); volV.text = Mathf.RoundToInt(Save.Volume * 100) + "%";
            assist.Set(Save.AimAssist, false);
            killcam.Set(Save.KillCam, false);
            vib.Set(Save.Vibration, false);
        }

        protected override void OnShow()
        {
            Load();
            ShowLangName();
            if (langPicker != null) langPicker.SetActive(false);
            if (privacyBtn != null) privacyBtn.gameObject.SetActive(Privacy.OptionsRequired || !string.IsNullOrEmpty(MonetizationConfig.PrivacyUrl));
        }
    }

    /// <summary>uGUI Slider assembled from code (track, fill, knob).</summary>
    public static class UIKSlider
    {
        public static Slider Create(Transform parent, float min, float max)
        {
            var root = UIK.Rect(parent, "Slider");
            var bg = UIK.Panel(root, "Track", new Color(1, 1, 1, 0.12f), "pill");
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f); bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0, 12);
            bg.raycastTarget = true;
            var fillArea = UIK.Rect(root, "FillArea");
            fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(-24, 12);
            var fill = UIK.Panel(fillArea, "Fill", Theme.Accent, "pill");
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.sizeDelta = new Vector2(24, 0);
            var handleArea = UIK.Rect(root, "HandleArea");
            UIK.Stretch(handleArea, 22, 0, 22, 0);
            var knob = UIK.Img(handleArea, "Knob", "circle", Color.white, false, true);
            knob.rectTransform.sizeDelta = new Vector2(46, 46);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = knob.rectTransform;
            s.targetGraphic = knob;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = min;
            s.maxValue = max;
            s.transition = Selectable.Transition.None;
            var nav = s.navigation; nav.mode = Navigation.Mode.None; s.navigation = nav;
            return s;
        }
    }

    /// <summary>iOS style on/off switch.</summary>
    public class PillToggle : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        Image track;
        RectTransform knob;
        bool on;
        Action<bool> changed;

        public static PillToggle Create(Transform parent, Action<bool> onChange)
        {
            var bg = UIK.Panel(parent, "Toggle", Theme.Line, "pill");
            bg.raycastTarget = true;
            var k = UIK.Img(bg.transform, "Knob", "circle", Color.white);
            UIK.Place(k.rectTransform, UIK.ML, new Vector2(0.5f, 0.5f), new Vector2(32, 0), new Vector2(52, 52));
            var t = bg.gameObject.AddComponent<PillToggle>();
            t.track = bg;
            t.knob = k.rectTransform;
            t.changed = onChange;
            return t;
        }

        public void Set(bool value, bool notify)
        {
            on = value;
            track.color = on ? Theme.Accent : Theme.Line;
            float x = on ? 92 : 32;
            var start = knob.anchoredPosition;
            Tween.Run(knob, 0.14f, k => { if (knob) knob.anchoredPosition = Vector2.Lerp(start, new Vector2(x, 0), Util.EaseInOut(k)); });
            if (notify) changed?.Invoke(on);
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
        {
            Sfx.UI(Sfx.Click);
            Set(!on, true);
        }
    }

    // ====================================================================== credits / licences (and the V2 case board)
    public class CreditsScreen : UIScreen
    {
        RectTransform content;
        Text body, head;
        /// <summary>V2: the same page shows the case board (what every finished contract uncovered).</summary>
        public bool CaseBoard;

        protected override void Build()
        {
            var bg = UIK.Img(transform, "Bg", null, new Color(0.02f, 0.03f, 0.04f, 0.94f), false, true);
            UIK.Stretch(bg.rectTransform);
            var hd = Header("KAYNAKLAR VE LİSANSLAR", () => ui.ShowMenu(false));
            var ht = hd.Find("Title");
            head = ht != null ? ht.GetComponent<Text>() : null;
            var view = UIK.Img(Safe, "Viewport", null, new Color(0, 0, 0, 0.001f), false, true).rectTransform;
            UIK.Stretch(view, 60, 24, 60, 140);
            view.gameObject.AddComponent<RectMask2D>();
            content = UIK.Rect(view, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(0, -100); content.offsetMax = Vector2.zero;
            body = UIK.Txt(content, "Body", "", Res.FontBody, 27, Theme.TextDim, TextAnchor.UpperLeft, false);
            body.verticalOverflow = VerticalWrapMode.Overflow;
            UIK.Stretch(body.rectTransform);
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 40;
        }

        static string CaseText()
        {
            var sb = new System.Text.StringBuilder();
            Story.Chapter ch = null;
            var list = Story.Board();
            if (list.Count == 0) return "Henüz dosyada bir şey yok. İlk sözleşmeyi tamamla.";
            foreach (var kv in list)
            {
                var m = kv.Key;
                var c = Story.ChapterOf(m);
                if (c != null && c != ch)
                {
                    ch = c;
                    sb.Append("<size=40><color=#E8B04A><b>BÖLÜM ").Append(c.number).Append(" · ").Append(c.title).Append("</b></color></size>\n\n");
                }
                string who = m.targets.Count > 0 ? m.targets[0].name + (m.targets.Count > 1 ? " & " + m.targets[1].name : "") : "";
                sb.Append("<b>").Append(m.number.ToString("00")).Append(" · ").Append(m.title).Append("</b>")
                  .Append(who.Length > 0 ? "   —   " + who : "").Append("\n").Append(kv.Value).Append("\n\n");
            }
            foreach (var k in new[] { "chemist", "safe", "disk" })
            {
                int d = Story.Decision(k);
                if (d == 0) continue;
                if (k == "disk") sb.Append("<b>SON</b>\n").Append(d == 1 ? "Disk KONTROL'de. Kurul'un koltukları yeniden doluyor." : "Disk savcıda. KONTROL'ün kapısına polis dayandı.").Append("\n\n");
            }
            return sb.ToString();
        }

        protected override void OnShow()
        {
            if (head != null) head.text = CaseBoard ? "DAVA DOSYASI" : "KAYNAKLAR VE LİSANSLAR";
            body.supportRichText = true;
            if (CaseBoard) body.text = CaseText();
            else
            {
                // the licence list in Turkish for Turkish, in English for every other language
                var ta = (Loc.IsSource ? null : Res.Text("credits_en")) ?? Res.Text("credits");
                string ct = ta != null ? ta.text : "Silent Sniper";
                if (body is LocText lt) lt.SetRaw(ct); else body.text = ct;
            }
            Canvas.ForceUpdateCanvases();
            float h = body.preferredHeight + 60;
            content.sizeDelta = new Vector2(0, h);
            content.anchoredPosition = Vector2.zero;
        }
    }

    // ====================================================================== pause
    public class PauseScreen : UIScreen
    {
        Text sub;

        protected override void Build()
        {
            var dim = UIK.Img(transform, "Dim", null, new Color(0, 0, 0, 0.72f), false, true);
            UIK.Stretch(dim.rectTransform);
            var card = UIK.Panel(Safe, "Card", Theme.Panel, "rr32");
            UIK.Place(card.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 832));
            pauseCard = card.rectTransform;
            var ln = UIK.Panel(card.transform, "line", new Color(1, 1, 1, 0.1f), "rr32_line");
            UIK.Stretch(ln.rectTransform);
            var t = UIK.Txt(card.transform, "Title", "DURAKLATILDI", Res.FontBold, Theme.H2, Theme.Text, TextAnchor.UpperCenter);
            UIK.Place(t.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(620, 72));
            sub = UIK.Txt(card.transform, "Sub", "", Res.FontSemi, 30, Theme.TextDim, TextAnchor.UpperCenter);
            UIK.Place(sub.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -122), new Vector2(620, 44));
            AddBtn(card.transform, "DEVAM ET", "play", BtnStyle.Primary, -206, () => ui.ClosePause());
            AddBtn(card.transform, "YENİDEN BAŞLAT", "retry", BtnStyle.Secondary, -320, () => { ui.ClosePause(); game.Restart(); });
            AddBtn(card.transform, "HARİTA", "map", BtnStyle.Secondary, -434, () => ui.ShowMap(game.Current));
            AddBtn(card.transform, "AYARLAR", "settings", BtnStyle.Secondary, -548, () => ui.ShowSettings(true));
            AddBtn(card.transform, "GÖREVDEN ÇIK", "home", BtnStyle.Ghost, -662, () =>
                ui.Confirm("Görevden çıkılsın mı?", "Bu görevdeki ilerlemen kaybolacak.", "ÇIK", () => { ui.ClosePause(); game.GoMenu(); }));
        }

        void AddBtn(Transform p, string label, string icon, BtnStyle st, float y, Action a)
        {
            var b = UIK.Btn(p, label, label, st, a, icon, 36);
            UIK.Place((RectTransform)b.transform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(560, 94));
            if (label == "HARİTA") mapBtn = b;
        }
        Button mapBtn;

        RectTransform pauseCard;

        protected override void OnShow()
        {
            var m = game.Current;
            sub.text = m != null ? "GÖREV " + m.number.ToString("00") + " · " + m.title : "";
            // no map for this contract: the button goes and the card closes the gap
            bool has = MapScreen.Has(m);
            if (mapBtn != null) mapBtn.gameObject.SetActive(has);
            if (pauseCard != null) pauseCard.sizeDelta = new Vector2(700, has ? 832 : 720);
            if (pauseCard == null) return;
            foreach (Transform c in pauseCard)
            {
                var b = c.GetComponent<Button>();
                if (b == null || b == mapBtn) continue;
                var r = (RectTransform)c;
                string n = c.name;
                if (n == "AYARLAR") r.anchoredPosition = new Vector2(0, has ? -548 : -434);
                if (n == "GÖREVDEN ÇIK") r.anchoredPosition = new Vector2(0, has ? -662 : -548);
            }
        }
    }

    // ====================================================================== result
    public class ResultScreen : UIScreen
    {
        Text header, sub, cash, total;
        readonly Image[] stars = new Image[3];
        readonly Image[] chIcon = new Image[3];
        readonly Text[] chText = new Text[3];
        readonly Text[] chNew = new Text[3];
        readonly Text[] statValue = new Text[4];
        Button next, retry, menu;
        bool success;
        // V2 career: the top bar counts the pay in, experience fills the rank bar, a video doubles the pay
        MetaBar bar;
        Button doubleBtn;
        Text xpGain;
        int preCash, preGems;
        bool doubled;

        // V2 layout: header and stars on top; left column the star challenges and the numbers, right column the pay line
        // by line (counted up one after the other), the grade in the corner
        RectTransform cardRt, leftCol, rightCol;
        Image gradeBg;
        Text gradeText, gradeLabel, ledgerNote, cashLabel;
        const int LedgerRows = 10;
        readonly Text[] ledgerLabel = new Text[LedgerRows];
        readonly Text[] ledgerValue = new Text[LedgerRows];
        readonly CanvasGroup[] ledgerGroup = new CanvasGroup[LedgerRows];
        readonly RectTransform[] statCell = new RectTransform[4];
        readonly RectTransform[] chRow = new RectTransform[3];

        protected override void Build()
        {
            var dim = UIK.Img(transform, "Dim", null, new Color(0, 0, 0, 0.78f), false, true);
            UIK.Stretch(dim.rectTransform);
            bar = MetaBar.Build(ui, Safe, true, false, false);
            xpGain = UIK.Txt(bar.transform, "XpGain", "", Res.FontBold, 34, Theme.Info, TextAnchor.MiddleLeft, false);
            UIK.Place(xpGain.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(470, -40), new Vector2(260, 44));
            UIK.AddShadow(xpGain, 2f, 0.7f);
            var card = UIK.Panel(Safe, "Card", Theme.Panel, "ch28");
            cardRt = card.rectTransform;
            UIK.Place(cardRt, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -46), new Vector2(1480, 880));
            var ln = UIK.Panel(card.transform, "line", new Color(1, 1, 1, 0.1f), "ch28_line");
            UIK.Stretch(ln.rectTransform);
            header = UIK.Txt(card.transform, "Header", "", Res.FontBold, 70, Theme.Success, TextAnchor.UpperCenter);
            UIK.Place(header.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(1020, 86));
            sub = UIK.Txt(card.transform, "Sub", "", Res.FontSemi, 30, Theme.TextDim, TextAnchor.UpperCenter);
            UIK.Place(sub.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(1100, 44));
            for (int i = 0; i < 3; i++)
            {
                stars[i] = UIK.Icon(card.transform, "star_outline", Theme.TextMute, i == 1 ? 112 : 94);
                UIK.Place(stars[i].rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2((i - 1) * 132, i == 1 ? -168 : -182), i == 1 ? new Vector2(112, 112) : new Vector2(94, 94));
            }
            // the grade, top right
            gradeBg = UIK.Panel(card.transform, "Grade", Theme.PanelSoft, "pill");
            UIK.Place(gradeBg.rectTransform, UIK.TR, new Vector2(1, 1), new Vector2(-44, -36), new Vector2(150, 150));
            var gl = UIK.Panel(gradeBg.transform, "line", new Color(1, 1, 1, 0.14f), "pill_line");
            UIK.Stretch(gl.rectTransform);
            gradeText = UIK.Txt(gradeBg.transform, "G", "", Res.FontBold, 96, Theme.Accent, TextAnchor.MiddleCenter, false);
            UIK.Stretch(gradeText.rectTransform, 0, 10, 0, 0);
            gradeLabel = UIK.Txt(card.transform, "GL", "DERECE", Res.FontSemi, 22, Theme.TextMute, TextAnchor.UpperCenter, false);
            UIK.Place(gradeLabel.rectTransform, UIK.TR, new Vector2(1, 1), new Vector2(-44, -192), new Vector2(150, 30));

            // ---- left column: star challenges, then the numbers
            leftCol = UIK.Rect(card.transform, "Left");
            UIK.Place(leftCol, UIK.TL, new Vector2(0, 1), new Vector2(48, -304), new Vector2(660, 440));
            var lt = UIK.Txt(leftCol, "Title", "YILDIZ HEDEFLERİ", Res.FontSemi, 24, Theme.TextMute, TextAnchor.UpperLeft, false);
            UIK.Place(lt.rectTransform, UIK.TL, new Vector2(0, 1), Vector2.zero, new Vector2(500, 32));
            for (int i = 0; i < 3; i++)
            {
                var row = UIK.Panel(leftCol, "Ch" + i, Theme.PanelSoft, "rr14");
                chRow[i] = row.rectTransform;
                UIK.Place(row.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(0, -40 - i * 58), new Vector2(660, 50));
                chIcon[i] = UIK.Icon(row.transform, "check", Theme.Success, 30);
                UIK.Place(chIcon[i].rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(30, 30));
                chText[i] = UIK.Txt(row.transform, "T", "", Res.FontSemi, 27, Theme.Text, TextAnchor.MiddleLeft, true, 16);
                UIK.Stretch(chText[i].rectTransform, 62, 0, 160, 0);
                chNew[i] = UIK.Txt(row.transform, "New", "YENİ YILDIZ", Res.FontBold, 22, Theme.Accent, TextAnchor.MiddleRight, false);
                UIK.Place(chNew[i].rectTransform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(170, 40));
            }
            var st = UIK.Txt(leftCol, "StatsTitle", "ATIŞ", Res.FontSemi, 24, Theme.TextMute, TextAnchor.UpperLeft, false);
            UIK.Place(st.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(0, -228), new Vector2(500, 32));
            string[] labels = { "En uzun atış", "Kafadan vuruş", "Süre", "İsabet" };
            for (int i = 0; i < 4; i++)
            {
                int col = i % 2, row = i / 2;
                var cell = UIK.Panel(leftCol, "Stat" + i, Theme.PanelSoft, "rr14");
                statCell[i] = cell.rectTransform;
                UIK.Place(cell.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(col * 336, -268 - row * 70), new Vector2(324, 60));
                var l = UIK.Txt(cell.transform, "L", labels[i], Res.FontBody, 25, Theme.TextDim, TextAnchor.MiddleLeft);
                UIK.Stretch(l.rectTransform, 20, 0, 130, 0);
                statValue[i] = UIK.Txt(cell.transform, "V", "", Res.FontBold, 30, Theme.Text, TextAnchor.MiddleRight);
                UIK.Stretch(statValue[i].rectTransform, 120, 0, 20, 0);
                statValue[i].supportRichText = true;
            }

            // ---- right column: the pay, line by line, and the total
            rightCol = UIK.Rect(card.transform, "Right");
            UIK.Place(rightCol, UIK.TR, new Vector2(1, 1), new Vector2(-48, -304), new Vector2(660, 440));
            var rt = UIK.Txt(rightCol, "Title", "KAZANÇ DÖKÜMÜ", Res.FontSemi, 24, Theme.TextMute, TextAnchor.UpperLeft, false);
            UIK.Place(rt.rectTransform, UIK.TL, new Vector2(0, 1), Vector2.zero, new Vector2(500, 32));
            for (int i = 0; i < LedgerRows; i++)
            {
                var r = UIK.Rect(rightCol, "L" + i);
                UIK.Place(r, UIK.TL, new Vector2(0, 1), new Vector2(0, -40 - i * 31), new Vector2(660, 31));
                ledgerGroup[i] = UIK.Group(r.gameObject);
                ledgerLabel[i] = UIK.Txt(r, "T", "", Res.FontBody, 25, Theme.TextDim, TextAnchor.MiddleLeft, true, 16);
                UIK.Stretch(ledgerLabel[i].rectTransform, 0, 0, 200, 0);
                ledgerValue[i] = UIK.Txt(r, "V", "", Res.FontBold, 26, Theme.Text, TextAnchor.MiddleRight, false);
                UIK.Stretch(ledgerValue[i].rectTransform, 300, 0, 0, 0);
                // the rows are 31 px: a 26 pt line is taller than that and uGUI would drop it (test 6: the amounts were gone)
                ledgerValue[i].verticalOverflow = VerticalWrapMode.Overflow;
                ledgerLabel[i].verticalOverflow = VerticalWrapMode.Overflow;
                ledgerGroup[i].alpha = 0f;
            }
            ledgerNote = UIK.Txt(rightCol, "Note", "", Res.FontBody, 26, Theme.TextMute, TextAnchor.UpperLeft, true, 18);
            UIK.Place(ledgerNote.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(0, -44), new Vector2(660, 120));
            var div = UIK.Img(rightCol, "Div", null, new Color(1, 1, 1, 0.14f));
            UIK.Place(div.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(0, 92), new Vector2(660, 2));
            cashLabel = UIK.Txt(rightCol, "CashLabel", "KAZANÇ", Res.FontSemi, 26, Theme.TextMute, TextAnchor.LowerLeft, false);
            UIK.Place(cashLabel.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(0, 40), new Vector2(300, 40));
            cash = UIK.Txt(rightCol, "Cash", "", Res.FontBold, 62, Theme.Accent, TextAnchor.LowerRight, false);
            UIK.Place(cash.rectTransform, UIK.BR, new Vector2(1, 0), new Vector2(0, 14), new Vector2(460, 76));
            doubleBtn = UIK.Btn(rightCol, "Double", "2× KAZANÇ", BtnStyle.Secondary, DoublePay, "ad", 26);
            UIK.Place((RectTransform)doubleBtn.transform, UIK.BL, new Vector2(0, 0), new Vector2(0, 24), new Vector2(250, 62));
            var dbi = doubleBtn.transform.Find("icon");
            if (dbi != null) dbi.GetComponent<Image>().color = Theme.Success;
            doubleBtn.gameObject.SetActive(false);
            total = UIK.Txt(rightCol, "Total", "", Res.FontBody, 24, Theme.TextDim, TextAnchor.UpperRight, false);
            UIK.Place(total.rectTransform, UIK.BR, new Vector2(1, 0), new Vector2(0, -26), new Vector2(460, 34));

            menu = UIK.Btn(card.transform, "Menu", "MENÜ", BtnStyle.Ghost, () => Leave(() => game.GoMenu()), "home", 32);
            UIK.Place((RectTransform)menu.transform, UIK.BL, new Vector2(0, 0), new Vector2(48, 36), new Vector2(250, 96));
            retry = UIK.Btn(card.transform, "Retry", "TEKRAR", BtnStyle.Secondary, () => Leave(() => game.Restart()), "retry", 32);
            UIK.Place((RectTransform)retry.transform, UIK.BR, new Vector2(1, 0), new Vector2(-448, 36), new Vector2(290, 96));
            next = UIK.Btn(card.transform, "Next", "SONRAKİ GÖREV", BtnStyle.Primary, () => game.NextMission(), "next", 32);
            UIK.Place((RectTransform)next.transform, UIK.BR, new Vector2(1, 0), new Vector2(-48, 36), new Vector2(380, 96));
            BuildIntel();
        }

        /// <summary>The card takes the screen's width (phones: two wide columns; 4:3 iPads: narrower ones).</summary>
        void LayoutCard()
        {
            float sw = ((RectTransform)Safe).rect.width;
            if (sw < 10f) sw = 1900f;
            float w = Mathf.Clamp(sw - 80f, 1180f, 1560f);
            cardRt.sizeDelta = new Vector2(w, 880f);
            float colW = (w - 48f * 2f - 56f) * 0.5f;
            leftCol.sizeDelta = new Vector2(colW, 440f);
            rightCol.sizeDelta = new Vector2(colW, 440f);
            for (int i = 0; i < 3; i++) chRow[i].sizeDelta = new Vector2(colW, 50f);
            float cellW = (colW - 12f) * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                statCell[i].sizeDelta = new Vector2(cellW, 60f);
                statCell[i].anchoredPosition = new Vector2((i % 2) * (cellW + 12f), -268 - (i / 2) * 70);
            }
            for (int i = 0; i < LedgerRows; i++) ((RectTransform)ledgerLabel[i].transform.parent).sizeDelta = new Vector2(colW, 31f);
            sub.rectTransform.sizeDelta = new Vector2(w - 2f * (44f + 150f + 16f), 44f);
            ledgerNote.rectTransform.sizeDelta = new Vector2(colW, 120f);
            ((RectTransform)rightCol.Find("Div")).sizeDelta = new Vector2(colW, 2f);
        }

        // ---- V2: what the contract uncovered, and the decisions
        RectTransform intel, intelCard;
        Text intelText, intelQ, intelOutcome;
        Button intelA, intelB, intelGo;

        void BuildIntel()
        {
            intel = UIK.Rect(transform, "Intel");
            UIK.Stretch(intel);
            var dk = UIK.Img(intel, "Dark", null, new Color(0.01f, 0.015f, 0.02f, 0.99f), false, true);
            UIK.Stretch(dk.rectTransform);
            var card = UIK.Panel(intel, "Card", Theme.Panel, "rr32");
            intelCard = card.rectTransform;
            UIK.Place(card.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 700));
            var lbl = UIK.Txt(card.transform, "Label", "İSTİHBARAT", Res.FontSemi, 28, Theme.Accent, TextAnchor.UpperLeft);
            UIK.Place(lbl.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(48, -36), new Vector2(600, 40));
            intelText = UIK.Txt(card.transform, "Text", "", Res.FontBody, 36, Theme.Text, TextAnchor.UpperLeft, true, 22);
            UIK.Place(intelText.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(48, -96), new Vector2(1084, 220));
            intelQ = UIK.Txt(card.transform, "Q", "", Res.FontBold, 34, Theme.Accent, TextAnchor.UpperLeft, true, 22);
            UIK.Place(intelQ.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(48, -330), new Vector2(1084, 110));
            intelOutcome = UIK.Txt(card.transform, "Out", "", Res.FontBody, 30, Theme.TextDim, TextAnchor.UpperLeft, true, 20);
            UIK.Place(intelOutcome.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(48, -330), new Vector2(1084, 200));
            intelA = UIK.Btn(card.transform, "A", "", BtnStyle.Secondary, () => Decide(1), null, 32);
            UIK.Place((RectTransform)intelA.transform, UIK.BL, new Vector2(0, 0), new Vector2(48, 44), new Vector2(520, 100));
            intelB = UIK.Btn(card.transform, "B", "", BtnStyle.Primary, () => Decide(2), null, 32);
            UIK.Place((RectTransform)intelB.transform, UIK.BR, new Vector2(1, 0), new Vector2(-48, 44), new Vector2(520, 100));
            intelGo = UIK.Btn(card.transform, "Go", "DEVAM", BtnStyle.Primary, AfterIntel, "next", 34);
            UIK.Place((RectTransform)intelGo.transform, UIK.BR, new Vector2(1, 0), new Vector2(-48, 44), new Vector2(360, 100));
            intel.gameObject.SetActive(false);
            BuildEpilogue();
        }

        // ---- V2: the end of the story (after the last decision): a full-screen epilogue
        RectTransform epilogue;
        Text epTitle, epText, epLines;

        void BuildEpilogue()
        {
            epilogue = UIK.Rect(transform, "Epilogue");
            UIK.Stretch(epilogue);
            var dk = UIK.Img(epilogue, "Dark", null, new Color(0.004f, 0.006f, 0.01f, 1f), false, true);
            UIK.Stretch(dk.rectTransform);
            var kind = UIK.Txt(epilogue, "Kind", "SON", Res.FontSemi, 34, Theme.Accent, TextAnchor.UpperLeft, false);
            UIK.Place(kind.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(180, 300), new Vector2(1400, 50));
            epTitle = UIK.Txt(epilogue, "Title", "", Res.FontBold, 88, Theme.Text, TextAnchor.UpperLeft, false);
            UIK.Place(epTitle.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(176, 220), new Vector2(1700, 110));
            var bar = UIK.Img(epilogue, "Bar", null, Theme.Accent);
            UIK.Place(bar.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(182, 146), new Vector2(140, 6));
            epText = UIK.Txt(epilogue, "Text", "", Res.FontBody, 34, Theme.Text, TextAnchor.UpperLeft, true, 22);
            UIK.Place(epText.rectTransform, UIK.ML, new Vector2(0, 1f), new Vector2(180, 110), new Vector2(1400, 200));
            epLines = UIK.Txt(epilogue, "Lines", "", Res.FontBody, 28, Theme.TextDim, TextAnchor.UpperLeft, true, 18);
            UIK.Place(epLines.rectTransform, UIK.ML, new Vector2(0, 1f), new Vector2(180, -110), new Vector2(1400, 260));
            var go = UIK.Btn(epilogue, "Go", "DEVAM", BtnStyle.Primary, () => { epilogue.gameObject.SetActive(false); TryTally(); }, "next", 36);
            UIK.Place((RectTransform)go.transform, UIK.BR, new Vector2(1, 0), new Vector2(-80, 60), new Vector2(360, 104));
            epilogue.gameObject.SetActive(false);
        }

        void AfterIntel()
        {
            intel.gameObject.SetActive(false);
            ShowEpilogueIfEnd();
            TryTally();
        }

        void ShowEpilogueIfEnd()
        {
            var c = Story.ChoiceFor(intelDef);
            if (c == null || c.key != "disk" || Story.Decision("disk") == 0 || epilogue == null) return;
            int disk = Story.Decision("disk"), chem = Story.Decision("chemist"), safe = Story.Decision("safe");
            epTitle.text = disk == 1 ? "YİNE KİMSESİZ" : "KENDİ SEÇİMİN";
            epText.text = disk == 1 ? c.aText : c.bText;
            var lines = new List<string>();
            if (chem != 0) lines.Add("Kimyager: " + (chem == 1 ? "KONTROL'ün hücresinde kayboldu." : "savcılığa ifade verdi; Baran davası onunla başladı."));
            if (safe != 0) lines.Add("Baran'ın kasası: " + (safe == 1 ? "para sende, KONTROL bunu biliyor." : "dosyaların kopyası Başsavcı Arslan'da."));
            lines.Add("Kurul: beş koltuk boşaldı.");
            lines.Add(disk == 1 ? "KONTROL: yerinde. Yeni bir isim arıyor." : "KONTROL: kapısında polis.");
            epLines.text = string.Join("\n", lines.ToArray());
            epilogue.gameObject.SetActive(true);
            epilogue.SetAsLastSibling();
        }

        void TryTally()
        {
            if (tallyPending && !IntelShown && !EpilogueShown) PlayTally();
        }

        MissionDef intelDef;

        /// <summary>Is the intelligence card up (automatic test: picture it, then close it).</summary>
        public bool EpilogueShown => epilogue != null && epilogue.gameObject.activeSelf;
        public bool IntelShown => intel != null && intel.gameObject.activeSelf;
        public void CloseIntel(int decide = 0)
        {
            if (intel == null) return;
            if (decide > 0 && Story.ChoiceFor(intelDef) != null) Decide(decide);
            AfterIntel();
        }

        void ShowIntel(MissionDef d)
        {
            intelDef = d;
            string beat = Story.Beat(d);
            var c = Story.ChoiceFor(d);
            if (string.IsNullOrEmpty(beat) && c == null) { intel.gameObject.SetActive(false); return; }
            intelText.text = beat ?? "";
            // a decision already taken (a replay): the outcome, not the question again
            int taken = c != null ? Story.Decision(c.key) : 0;
            bool ask = c != null && taken == 0;
            intelQ.text = ask ? c.question : "";
            intelOutcome.text = c != null && taken != 0 ? (taken == 1 ? c.aText : c.bText) : "";
            intelA.gameObject.SetActive(ask);
            intelB.gameObject.SetActive(ask);
            intelGo.gameObject.SetActive(!ask);
            if (ask) { UIK.SetBtnLabel(intelA, c.a); UIK.SetBtnLabel(intelB, c.b); }
            // a plain line of intelligence sits in a compact card; a decision gets the full height
            if (intelCard != null) intelCard.sizeDelta = new Vector2(1180f, ask ? 700f : 470f);
            intel.gameObject.SetActive(true);
            intel.SetAsLastSibling();
        }

        void Decide(int option)
        {
            var c = Story.ChoiceFor(intelDef);
            if (c == null) { intel.gameObject.SetActive(false); return; }
            Story.Decide(intelDef, option);
            Sfx.UI(Sfx.Confirm);
            intelQ.text = "";
            intelOutcome.text = option == 1 ? c.aText : c.bText;
            if (intelCard != null) intelCard.sizeDelta = new Vector2(1180f, 700f);
            intelA.gameObject.SetActive(false);
            intelB.gameObject.SetActive(false);
            intelGo.gameObject.SetActive(true);
            total.text = "Toplam: " + UIK.Money(Save.Cash);
        }

        public void Set(MissionRuntime r)
        {
            if (epilogue != null) epilogue.gameObject.SetActive(false);
            success = r.Success;
            header.text = success ? "GÖREV TAMAMLANDI" : "GÖREV BAŞARISIZ";
            header.color = success ? Theme.Success : Theme.Danger;
            var d = r.Def;
            sub.text = (d != null ? d.number.ToString("00") + " · " + d.title : "") + (success ? "" : "   —   " + (r.FailReason ?? ""));
            LayoutCard();
            if (r.Veteran) sub.text += "   ·   VETERAN";
            statValue[0].text = r.LongestKill > 0 ? Mathf.RoundToInt(r.LongestKill) + " m" + (r.NewDistanceRecord ? "  <color=#FFB21E>REKOR</color>" : "") : "—";
            statValue[1].text = r.Headshots > 0 ? r.Headshots.ToString() : "Yok";
            statValue[2].text = UIK.Time(r.Elapsed);
            statValue[3].text = r.Shots > 0 ? r.Hits + " / " + r.Shots : "—";
            var ch = r.Challenges;
            for (int i = 0; i < 3; i++)
            {
                bool has = i < ch.Count;
                chText[i].transform.parent.gameObject.SetActive(has);
                if (!has) continue;
                bool met = i < r.ChallengeMet.Length && r.ChallengeMet[i];
                bool before = d != null && Save.Challenge(d.id, i) && !(i < r.ChallengeNew.Length && r.ChallengeNew[i]);
                chText[i].text = ch[i].text;
                chText[i].color = met ? Theme.Text : Theme.TextDim;
                chIcon[i].sprite = Res.Icon(met ? "check" : (before ? "star" : "close"));
                chIcon[i].color = met ? Theme.Success : (before ? Theme.Accent : Theme.TextMute);
                chNew[i].gameObject.SetActive(i < r.ChallengeNew.Length && r.ChallengeNew[i]);
                chNew[i].text = "YENİ YILDIZ";
                if (!met && before) { chNew[i].gameObject.SetActive(true); chNew[i].text = "ÖNCEDEN"; chNew[i].color = Theme.TextMute; }
                else chNew[i].color = Theme.Accent;
                if (!met && i == ch.Count - 1 && r.EasyStarLost)
                {
                    chNew[i].gameObject.SetActive(true);
                    chNew[i].text = "KOLAY NİŞAN";
                    chNew[i].color = Theme.TextMute;
                }
            }
            cash.text = "+" + UIK.Money(0);
            total.text = "Toplam: " + UIK.Money(Save.Cash);
            // the bar starts where the player was before this contract and catches up while the pay is counted
            int lvlCash = 0, lvlGems = 0;
            foreach (var l in r.LevelUps) { lvlCash += l.cash; lvlGems += l.gems; }
            preCash = Save.Cash - r.CashEarned - lvlCash;
            preGems = Save.Gems - r.GemsEarned - lvlGems;
            bar.Hold = true;
            bar.ShowCash(preCash);
            bar.ShowGems(preGems);
            int lv0 = Progress.Split(r.XpBefore, out int in0, out int nd0);
            bar.SetRank(lv0, in0, nd0);
            xpGain.text = "";
            doubled = false;
            doubleBtn.gameObject.SetActive(false);
            cashLabel.gameObject.SetActive(true);
            // the pay lines, hidden until OnShow counts them in
            int n = success ? Mathf.Min(LedgerRows, r.Ledger.Count) : 0;
            for (int i = 0; i < LedgerRows; i++)
            {
                bool on = i < n;
                ledgerGroup[i].gameObject.SetActive(on);
                ledgerGroup[i].alpha = 0f;
                if (!on) continue;
                var l = r.Ledger[i];
                ledgerLabel[i].text = l.Key;
                bool vet = l.Key.StartsWith("VETERAN");
                ledgerLabel[i].color = l.Value < 0 ? Theme.Danger : (vet ? Theme.Accent : Theme.TextDim);
                ledgerValue[i].text = (l.Value < 0 ? "-" : "+") + UIK.Money(Mathf.Abs(l.Value));
                ledgerValue[i].color = l.Value < 0 ? Theme.Danger : (vet ? Theme.Accent : Theme.Text);
            }
            // diamonds of the contract (first S, daily job) as a cyan line under the pay
            if (success && r.GemsEarned > 0 && n < LedgerRows)
            {
                int k = n;
                ledgerGroup[k].gameObject.SetActive(true);
                ledgerGroup[k].alpha = 0f;
                ledgerLabel[k].text = r.Daily ? "Günün sözleşmesi  ·  elmas" : "İlk S derecesi  ·  elmas";
                ledgerLabel[k].color = Theme.Info;
                ledgerValue[k].text = "+" + r.GemsEarned + " ELMAS";
                ledgerValue[k].color = Theme.Info;
            }
            ledgerNote.gameObject.SetActive(!success);
            ledgerNote.text = success ? "" : "Sözleşme tamamlanmadı — ödeme yok.\nAynı işe tekrar girebilirsin; neyin ters gittiğini yukarıda görürsün." +
                              (Ads.RewardedReady && RefundFor(r) > 0 ? "\nKısa bir videoyla ücretin bir kısmını telafi alabilirsin." : "");
            // the grade
            gradeText.text = success ? r.Grade : "—";
            Color gc = r.Grade == "S" ? Theme.Accent : (r.Grade == "A" ? Theme.Success : (r.Grade == "B" ? Theme.Info : Theme.TextDim));
            gradeText.color = success ? gc : Theme.TextMute;
            gradeBg.transform.localScale = Vector3.one;
            bool hasNext = d != null && MissionDefs.Next(d) != null && success;
            next.gameObject.SetActive(hasNext || !success);
            UIK.SetBtnLabel(next, success ? (hasNext ? "SONRAKİ GÖREV" : "MENÜ") : "TEKRAR DENE");
            next.onClick.RemoveAllListeners();
            next.onClick.AddListener(() =>
            {
                Sfx.UI(Sfx.Confirm);
                if (!success) { if (!OfferPower(d, () => game.Restart())) game.Restart(); }
                else if (hasNext) Leave(() => game.NextMission());
                else Leave(() => game.GoMenu());
            });
            retry.gameObject.SetActive(success);
            if (success && d != null) ShowIntel(d); else intel.gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                stars[i].sprite = Res.Icon("star_outline");
                stars[i].color = Theme.TextMute;
                stars[i].transform.localScale = Vector3.one;
            }
        }

        bool tallyPending;

        /// <summary>Leaving before the count is over: no coins and fanfares over the next screen.</summary>
        protected override void OnHide()
        {
            tallyPending = false;
            for (int i = 0; i < 3; i++) Tween.Stop("star" + i);
            for (int i = 0; i < LedgerRows; i++) { Tween.Stop("ledger" + i); Tween.Stop(ledgerGroup[i]); }
            Tween.Stop("grade");
            Tween.Stop(cash);
            Tween.Stop("xpfill");
            Tween.Stop("levelup");
            if (bar != null) Tween.Stop(bar);
            RewardFX.Clear();
            if (gradeBg != null) gradeBg.transform.localScale = Vector3.one;
            if (bar != null) { bar.Hold = false; bar.Refresh(true); }
        }

        /// <summary>Out of the results: every few contracts a full-screen ad first (never after a failed one).</summary>
        void Leave(Action go)
        {
            if (success) Ads.AfterContract(go);
            else go();
        }

        /// <summary>A failed contract: part of the fee back for a video (once per run).</summary>
        static int RefundFor(MissionRuntime r)
        {
            if (r == null || r.Def == null || r.Success || !Ads.RefundOffer(r.Elapsed)) return 0;
            float fee = r.Def.reward * Tuning.Pct("pay_scale_pct") * Tuning.Pct("fail_refund_pct");
            if (Save.Completed(r.Def.id)) fee *= Mathf.Clamp01(Tuning.Pct("replay_pay_pct"));
            return Mathf.RoundToInt(fee / 10f) * 10;
        }

        void Refund()
        {
            var r = game.Runtime;
            int amount = RefundFor(r);
            if (doubled || amount <= 0) return;
            Ads.ShowRewarded("fail_refund", ok =>
            {
                if (!ok || doubled) return;
                doubled = true;
                Ads.UseRefund();
                Progress.AddCash(amount, "ad_fail_refund");
                doubleBtn.gameObject.SetActive(false);
                cashLabel.gameObject.SetActive(true);
                Tween.Run(cash, 0.7f, k => { if (cash) cash.text = "+" + UIK.Money(Mathf.RoundToInt(amount * Util.EaseOutCubic(k))); });
                Tween.Pop(cash.transform, 0.4f, 0f, 1.3f);
                bar.Hold = false;
                RewardFX.Fly(cash.transform, bar.CashIcon, "coin_c", 8, () => Sfx.Play(Sfx.Coin, 0.3f, 1.3f), () => bar.Refresh(false));
                total.text = "Toplam: " + UIK.Money(Save.Cash);
            });
        }

        /// <summary>
        /// Failed the same contract again with a rifle below the recommended power: once per contract, the way to get
        /// stronger (upgrade in the armory, or a rifle pack). True when the offer took over the button.
        /// </summary>
        bool OfferPower(MissionDef d, Action retryNow)
        {
            var r = game.Runtime;
            if (d == null || r == null || r.Attempt < 2 || Save.Flag("power_offer_" + d.id)) return false;
            var rifle = Gear.Equipped;
            int power = Gear.Power(rifle), need = Progress.Recommended(d);
            if (power >= need) return false;
            Save.SetFlag("power_offer_" + d.id);
            bool packs = !Store.ShadowPack.Owned || !Store.TitanPack.Owned;
            Analytics.Event("offer_shown", "offer", "power", "placement", "fail_retry", "contract_no", d.number, "power", power, "recommended", need);
            ui.Confirm("TÜFEĞİN ZORLANIYOR",
                       "Bu iş için önerilen güç " + need + ", senin tüfeğin " + power + ". Geliştir ya da " + (packs ? "bir silah paketiyle" : "elmasla") + " hemen güçlen.",
                       "GÜÇLEN", () => ui.ShowShop(packs ? ShopScreen.Tab.Packs : ShopScreen.Tab.Gems, () => ui.ShowArmory(null)),
                       "TEKRAR DENE", retryNow);
            return true;
        }

        /// <summary>A video doubles the contract's pay (once); after a failed contract the same button pays the refund.</summary>
        void DoublePay()
        {
            var r = game.Runtime;
            if (r != null && !r.Success) { Refund(); return; }
            if (doubled || r == null || !r.Success || r.CashEarned <= 0) return;
            Ads.ShowRewarded("result_double", ok =>
            {
                if (!ok || doubled) return;
                doubled = true;
                int extra = r.CashEarned;
                Progress.AddCash(extra, "ad_double_pay");
                doubleBtn.gameObject.SetActive(false);
                cashLabel.gameObject.SetActive(true);
                int from = r.CashEarned, to = r.CashEarned * 2;
                Tween.Run(cash, 0.7f, k => { if (cash) cash.text = "+" + UIK.Money(Mathf.RoundToInt(Mathf.Lerp(from, to, Util.EaseOutCubic(k)))); });
                Tween.Pop(cash.transform, 0.4f, 0f, 1.3f);
                bar.Hold = false;
                RewardFX.Fly(cash.transform, bar.CashIcon, "coin_c", 10, () => Sfx.Play(Sfx.Coin, 0.3f, 1.3f), () => bar.Refresh(false));
                total.text = "Toplam: " + UIK.Money(Save.Cash);
            });
        }

        /// <summary>After the pay: coins and diamonds fly into the bar, experience fills the rank, then level ups.</summary>
        void LandRewards(MissionRuntime r, float at)
        {
            Tween.Run("xpfill", 0.01f, null, () =>
            {
                if (!Visible) return;
                int cashTo = preCash + r.CashEarned, gemsTo = preGems + r.GemsEarned;
                if (r.CashEarned > 0)
                {
                    int steps = 8, got = 0;
                    RewardFX.Fly(cash.transform, bar.CashIcon, "coin_c", steps, () =>
                    {
                        got++;
                        bar.ShowCash(preCash + Mathf.RoundToInt(r.CashEarned * got / (float)steps));
                        Sfx.Play(Sfx.Coin, 0.28f, 1.2f + got * 0.03f);
                    });
                }
                if (r.GemsEarned > 0)
                    RewardFX.Fly(cash.transform, bar.GemIcon, "gem", 5, () => Sfx.Play(Sfx.Coin, 0.25f, 1.6f), () => bar.ShowGems(gemsTo));
                // experience: the rank bar runs from before to after (over level boundaries)
                int x0 = r.XpBefore, x1 = r.XpBefore + r.XpEarned;
                xpGain.text = r.XpEarned > 0 ? "+" + UIK.Num(r.XpEarned) + " TP" : "";
                if (r.XpEarned > 0) Tween.Pop(xpGain.transform, 0.4f, 0f, 0.5f);
                int lastLvl = Progress.Split(x0, out _, out _);
                Tween.Run(bar, 1.3f, k =>
                {
                    if (bar == null) return;
                    int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, Util.EaseInOut(k)));
                    int lv = Progress.Split(x, out int into, out int need);
                    bar.SetRank(lv, into, need);
                    if (lv != lastLvl) { lastLvl = lv; Tween.Pop(bar.Badge, 0.45f, 0f, 0.4f); Sfx.Play(Sfx.Success, 0.6f, 1.3f); }
                }, () =>
                {
                    if (r.LevelUps.Count > 0 && Visible)
                    {
                        Tween.Run("levelup", 0.01f, null, () =>
                        {
                            if (!Visible) return;
                            ui.LevelUp.Play(new List<Progress.LevelReward>(r.LevelUps), () => { if (bar != null) { bar.Hold = false; bar.Refresh(false); } });
                        }, 0.5f);
                    }
                    else { bar.Hold = false; bar.Refresh(false); }
                });
                if (r.Success && r.CashEarned > 0 && !doubled && Ads.RewardedReady)
                {
                    UIK.SetBtnLabel(doubleBtn, "2× KAZANÇ");
                    doubleBtn.gameObject.SetActive(true);
                    cashLabel.gameObject.SetActive(false);
                    Tween.Pop(doubleBtn.transform, 0.4f, 0.2f, 0.6f);
                }
                else if (!r.Success && !doubled && RefundFor(r) > 0 && Ads.RewardedReady)
                {
                    UIK.SetBtnLabel(doubleBtn, "TELAFİ +" + UIK.Money(RefundFor(r)));
                    doubleBtn.gameObject.SetActive(true);
                    cashLabel.gameObject.SetActive(false);
                    Tween.Pop(doubleBtn.transform, 0.4f, 0.2f, 0.6f);
                }
            }, at);
        }

        /// <summary>The epilogue's DEVAM (also the test bot).</summary>
        public void CloseEpilogue()
        {
            if (epilogue != null) epilogue.gameObject.SetActive(false);
            TryTally();
        }

        /// <summary>The intelligence card (and the epilogue) come first: the stars and the pay are counted when the player
        /// is back on the result card.</summary>
        protected override void OnShow()
        {
            if (IntelShown || EpilogueShown) { tallyPending = true; return; }
            PlayTally();
        }

        void PlayTally()
        {
            tallyPending = false;
            var r = game.Runtime;
            int st = r.Stars;
            for (int i = 0; i < st; i++)
            {
                int k = i;
                Tween.Run("star" + k, 0.01f, null, () =>
                {
                    stars[k].sprite = Res.Icon("star");
                    stars[k].color = Theme.Accent;
                    Tween.Pop(stars[k].transform, 0.4f, 0f, 0.3f);
                    Sfx.Play(Sfx.Coin, 0.7f, 1f + k * 0.12f);
                }, 0.45f + k * 0.32f);
            }
            // the pay lines one after the other, then the total counts up, then the grade lands
            float t0 = 0.5f + st * 0.32f;
            int n = 0;
            for (int i = 0; i < LedgerRows; i++) if (ledgerGroup[i].gameObject.activeSelf) n++;
            for (int i = 0; i < n; i++)
            {
                int k = i;
                Tween.Fade(ledgerGroup[k], 1f, 0.22f, null, t0 + k * 0.16f);
                Tween.Run("ledger" + k, 0.01f, null, () => Sfx.Play(Sfx.Click, 0.35f, 1.2f + k * 0.05f), t0 + k * 0.16f);
            }
            int target = r.CashEarned;
            float tc = t0 + n * 0.16f + 0.1f;
            Tween.Run(cash, 0.9f, k => { if (cash) cash.text = "+" + UIK.Money(Mathf.RoundToInt(target * Util.EaseOutCubic(k))); }, null, tc);
            if (r.Success)
            {
                gradeBg.transform.localScale = Vector3.zero;
                Tween.Run("grade", 0.01f, null, () =>
                {
                    Tween.Pop(gradeBg.transform, 0.45f, 0f, 0.2f);
                    Sfx.Play(r.Grade == "S" ? Sfx.Success : Sfx.Coin, 0.8f, r.Grade == "S" ? 1.1f : 0.9f);
                }, tc + 0.95f);
            }
            LandRewards(r, r.Success ? tc + 1.5f : 0.6f);
            // V3: Apple's tracking question right here once the first contract is done (it used to wait for the menu, and
            // players who went on with SONRAKİ GÖREV never got there — nor would App Review)
            if (r.Success && Privacy.AttDue)
                Tween.Run(this, 0.01f, null, () => { if (Visible && Privacy.AttDue) StartCoroutine(Privacy.AskTracking(null)); }, tc + 2.2f);
            // the happy moment: stars and pay have landed (Apple's rating sheet, only at the milestone contracts; never on
            // top of the tracking question — then the menu asks)
            if (r.Success) Tween.Run(this, 0.01f, null, () => { if (Visible && !Privacy.Asking && !Privacy.AttDue) Review.TryShow(ui); }, tc + 3.2f);
        }
    }
}

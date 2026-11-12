using System;
using UnityEngine;
using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>
    /// Main menu over the live map: the career bar on top (rank, experience, cash, diamonds), the logo, the next contract
    /// as a dossier card with a big play button, four tiles (contracts, armory, shop, case board), the equipped rifle on a
    /// lit turntable with its power and an upgrade shortcut, and the contract of the day.
    /// </summary>
    public class MenuScreen : UIScreen
    {
        MetaBar bar;
        Button play;
        Text playSub, sub, menuTitle;
        RectTransform left, right;
        // rifle showcase
        StageView stage;
        RectTransform showcase, strip;
        Text rifleName, rifleRarity, riflePower;
        Image rarityBg, powerFill;
        Button upgrade;
        // tiles
        RectTransform tileGrid;
        Text tileMissionsInfo, tileArmoryInfo, tileShopInfo, tileCaseInfo;
        Image armoryBadge, shopBadge;
        // next contract dossier
        Image featPortrait, featKindBg;
        Text featKind, featTitle, featName, featInfo, featReward;
        // daily
        RectTransform daily;
        Text dailyTitle, dailySub;
        MissionDef dailyDef;
        // daily jobs
        RectTransform tasksCard;
        readonly Text[] taskText = new Text[3], taskProg = new Text[3];
        readonly Image[] taskIcon = new Image[3], taskRewardIcon = new Image[3];
        readonly Button[] taskBtn = new Button[3];
        System.Collections.Generic.List<DailyTasks.Task> today;

        protected override void Build()
        {
            var shade = UIK.Img(transform, "Shade", "grad_h", new Color(0.015f, 0.02f, 0.03f, 0.92f));
            shade.rectTransform.anchorMin = new Vector2(0, 0);
            shade.rectTransform.anchorMax = new Vector2(0.62f, 1);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            shade.rectTransform.localScale = new Vector3(-1, 1, 1);
            var top = UIK.Img(transform, "TopShade", "grad_v", new Color(0.01f, 0.015f, 0.02f, 0.75f));
            top.rectTransform.anchorMin = new Vector2(0, 0.82f); top.rectTransform.anchorMax = new Vector2(1, 1);
            top.rectTransform.offsetMin = top.rectTransform.offsetMax = Vector2.zero;
            var bottom = UIK.Img(transform, "BottomShade", "grad_v", new Color(0.01f, 0.015f, 0.02f, 0.8f));
            bottom.rectTransform.anchorMin = new Vector2(0, 0); bottom.rectTransform.anchorMax = new Vector2(1, 0.3f);
            bottom.rectTransform.offsetMin = bottom.rectTransform.offsetMax = Vector2.zero;
            bottom.rectTransform.localScale = new Vector3(1, -1, 1);
            var vig = UIK.Img(transform, "Vignette", "vignette", new Color(0, 0, 0, 0.5f));
            UIK.Stretch(vig.rectTransform);

            bar = MetaBar.Build(ui, Safe, true, true);

            // ---- left column: logo, next contract, play, tiles
            left = UIK.Rect(Safe, "Left");
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0, 1);
            left.pivot = new Vector2(0, 1);
            left.offsetMin = new Vector2(52, 0); left.offsetMax = new Vector2(52 + 640, -122);

            menuTitle = UIK.Txt(left, "Title", "SILENT SNIPER", Res.FontBold, 92, Theme.Text, TextAnchor.UpperLeft, true, 56);
            UIK.Place(menuTitle.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(0, -2), new Vector2(640, 100));
            UIK.AddShadow(menuTitle, 3, 0.6f);
            sub = UIK.Txt(left, "Sub", "LİMAN OPERASYONU", Res.FontSemi, 34, Theme.Accent, TextAnchor.UpperLeft, false);
            UIK.Place(sub.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(4, -100), new Vector2(640, 44));
            UIK.AddShadow(sub, 2, 0.6f);

            BuildFeature(left);

            play = UIK.Btn(left, "Play", "OYNA", BtnStyle.Primary, OnPlay, "play", 54);
            var prt = (RectTransform)play.transform;
            UIK.Place(prt, UIK.TL, new Vector2(0, 1), new Vector2(0, -388), new Vector2(640, 128));
            var lbl = play.transform.Find("label").GetComponent<Text>();
            lbl.rectTransform.offsetMin = new Vector2(lbl.rectTransform.offsetMin.x, 34);
            playSub = UIK.Txt(play.transform, "Sub", "", Res.FontSemi, 25, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.72f), TextAnchor.LowerLeft, true, 16);
            playSub.rectTransform.anchorMin = new Vector2(0, 0); playSub.rectTransform.anchorMax = new Vector2(1, 0);
            playSub.rectTransform.pivot = new Vector2(0, 0);
            playSub.rectTransform.offsetMin = new Vector2(104, 16); playSub.rectTransform.offsetMax = new Vector2(-24, 48);
            var sheen = UIK.Img(play.transform, "Sheen", "sheen", new Color(1, 1, 1, 0.9f));
            UIK.Stretch(sheen.rectTransform, 4, 4, 4, 4);
            RewardFX.Shine(prt, 3.4f);

            tileGrid = UIK.Rect(left, "Tiles");
            UIK.Place(tileGrid, UIK.TL, new Vector2(0, 1), new Vector2(0, -534), new Vector2(640, 268));
            tileMissionsInfo = Tile(0, 0, "GÖREVLER", "list", () => ui.ShowMissions(), out _);
            tileArmoryInfo = Tile(1, 0, "CEPHANELİK", "rifle", () => ui.ShowArmory(null), out armoryBadge);
            tileShopInfo = Tile(0, 1, "MAĞAZA", "cart", () => ui.ShowShop(ShopScreen.Tab.Offers), out shopBadge);
            tileCaseInfo = Tile(1, 1, "DAVA DOSYASI", "user_search", () => ui.ShowCaseBoard(), out _);

            // ---- right column: rifle showcase, contract of the day
            right = UIK.Rect(Safe, "Right");
            right.anchorMin = new Vector2(1, 0); right.anchorMax = new Vector2(1, 1);
            right.pivot = new Vector2(1, 1);
            right.offsetMin = new Vector2(-40 - 760, 0); right.offsetMax = new Vector2(-40, -122);
            showcase = UIK.Rect(right, "Showcase");
            UIK.Place(showcase, UIK.TR, new Vector2(1, 1), new Vector2(0, 0), new Vector2(760, 430));
            stage = StageView.Build(showcase, "Stage", false);
            UIK.Stretch((RectTransform)stage.transform);
            BuildStrip(right);
            BuildDaily(right);
            BuildTasks(right);

            var ver = UIK.Txt(Safe, "Version", "", Res.FontBody, 22, Theme.TextMute, TextAnchor.LowerLeft, false);
            UIK.Place(ver.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(56, 16), new Vector2(760, 32));
            ver.text = "v" + Game.Version + (Game.TestBuild && Save.UnlockAll ? "  ·  TEST MODU: tüm bölümler açık" : "  ·  Tek oyunculu keskin nişancı");
            versionT = ver;
            // hidden test switch (test builds only): hold the version line for 3 seconds -> every map and contract open
            ver.raycastTarget = Game.TestBuild;
            var lp = ver.gameObject.AddComponent<LongPress>();
            lp.seconds = 3f;
            lp.onLongPress = () =>
            {
                bool on = Save.UnlockAll;
                ui.Confirm("TEST MODU", on ? "Tüm haritalar ve görevler şu an açık. Normal ilerlemeye dönülsün mü?"
                                          : "Test için bütün haritalar ve görevler açılsın mı? (Ayarlardan ilerleme sıfırlanmaz.)",
                           on ? "KAPAT" : "AÇ", () => { Save.UnlockAll = !on; OnShow(); });
            };
            var credits = UIK.Btn(Safe, "Credits", "KAYNAKLAR", BtnStyle.Ghost, () => ui.ShowCredits(), "credits", 22);
            UIK.Place((RectTransform)credits.transform, UIK.BL, new Vector2(0, 0), new Vector2(700, 10), new Vector2(230, 48));
            creditsRt = (RectTransform)credits.transform;
        }

        Text versionT;
        RectTransform creditsRt;

        Text Tile(int col, int row, string label, string icon, Action a, out Image badge)
        {
            var t = UIK.Panel(tileGrid, "Tile_" + label, new Color(0.06f, 0.08f, 0.11f, 0.93f), "ch18");
            t.raycastTarget = true;
            var rt = t.rectTransform;
            UIK.Place(rt, UIK.TL, new Vector2(0, 1), new Vector2(col * 326, -row * 138), new Vector2(314, 126));
            var ln = UIK.Panel(t.transform, "line", new Color(1, 1, 1, 0.12f), "ch18_line");
            UIK.Stretch(ln.rectTransform);
            var glow = UIK.Img(t.transform, "Glow", "edge_glow", new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.10f));
            UIK.Stretch(glow.rectTransform, 2, 2, 2, 2);
            var ic = UIK.Icon(t.transform, icon, Theme.Accent, 54);
            UIK.Place(ic.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(22, 8), new Vector2(54, 54));
            var name = UIK.Txt(t.transform, "Name", label, Res.FontBold, 32, Theme.Text, TextAnchor.UpperLeft, true, 20);
            UIK.Place(name.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(92, -22), new Vector2(210, 40));
            var info = UIK.Txt(t.transform, "Info", "", Res.FontSemi, 23, Theme.TextDim, TextAnchor.UpperLeft, true, 15);
            UIK.Place(info.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(92, -64), new Vector2(210, 34));
            badge = UIK.Panel(t.transform, "Badge", Theme.Danger, "pill");
            UIK.Place(badge.rectTransform, UIK.TR, new Vector2(1, 1), new Vector2(-10, -10), new Vector2(36, 36));
            var bt = UIK.Txt(badge.transform, "T", "!", Res.FontBold, 26, Color.white, TextAnchor.MiddleCenter, false);
            UIK.Stretch(bt.rectTransform, 0, 1, 0, 0);
            badge.gameObject.SetActive(false);
            var b = t.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = t;
            t.gameObject.AddComponent<ButtonFX>().Bind(t, t.color);
            b.onClick.AddListener(() => { Sfx.UI(Sfx.Confirm); a(); });
            return info;
        }

        // ---- next contract as a dossier card
        void BuildFeature(RectTransform parent)
        {
            var card = UIK.Panel(parent, "Feature", new Color(0.035f, 0.05f, 0.065f, 0.92f), "ch18");
            UIK.Place(card.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(0, -160), new Vector2(640, 214));
            var ln = UIK.Panel(card.transform, "line", new Color(1, 1, 1, 0.12f), "ch18_line");
            UIK.Stretch(ln.rectTransform);
            var frame = UIK.Panel(card.transform, "Frame", new Color(73f / 255f, 77f / 255f, 81f / 255f, 1f), "ch10");
            UIK.Place(frame.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(16, -16), new Vector2(150, 182));
            featPortrait = UIK.Img(frame.transform, "P", null, Color.white);
            featPortrait.preserveAspect = true;
            UIK.Stretch(featPortrait.rectTransform, 4, 4, 4, 4);
            var lbl = UIK.Txt(card.transform, "Label", "SIRADAKİ SÖZLEŞME", Res.FontSemi, 22, Theme.Accent, TextAnchor.UpperLeft, false);
            UIK.Place(lbl.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(186, -16), new Vector2(400, 30));
            featTitle = UIK.Txt(card.transform, "Title", "", Res.FontBold, 40, Theme.Text, TextAnchor.UpperLeft, true, 24);
            UIK.Place(featTitle.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(186, -46), new Vector2(436, 50));
            featName = UIK.Txt(card.transform, "Name", "", Res.FontSemi, 26, Theme.TextDim, TextAnchor.UpperLeft, true, 16);
            UIK.Place(featName.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(186, -96), new Vector2(436, 34));
            featKindBg = UIK.Panel(card.transform, "Kind", Theme.Danger, "pill");
            UIK.Place(featKindBg.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(186, 20), new Vector2(170, 36));
            featKind = UIK.Txt(featKindBg.transform, "T", "", Res.FontBold, 21, Color.white, TextAnchor.MiddleCenter, false);
            UIK.Stretch(featKind.rectTransform, 6, 0, 6, 0);
            featInfo = UIK.Txt(card.transform, "Info", "", Res.FontSemi, 22, Theme.TextDim, TextAnchor.LowerLeft, true, 14);
            UIK.Place(featInfo.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(370, 22), new Vector2(150, 32));
            featReward = UIK.Txt(card.transform, "Reward", "", Res.FontBold, 32, Theme.Accent, TextAnchor.LowerRight, false);
            UIK.Place(featReward.rectTransform, UIK.BR, new Vector2(1, 0), new Vector2(-20, 18), new Vector2(200, 42));
            card.raycastTarget = true;
            var b = card.gameObject.AddComponent<Button>();
            b.targetGraphic = card;
            b.onClick.AddListener(() => { Sfx.UI(Sfx.Confirm); OnPlay(); });
        }

        // ---- rifle strip under the showcase: name, rarity, power, upgrade
        void BuildStrip(RectTransform parent)
        {
            var s = UIK.Panel(parent, "Strip", new Color(0.035f, 0.05f, 0.065f, 0.92f), "ch18");
            strip = s.rectTransform;
            UIK.Place(strip, UIK.TR, new Vector2(1, 1), new Vector2(0, -440), new Vector2(760, 116));
            var ln = UIK.Panel(s.transform, "line", new Color(1, 1, 1, 0.12f), "ch18_line");
            UIK.Stretch(ln.rectTransform);
            rifleName = UIK.Txt(s.transform, "Name", "", Res.FontBold, 40, Theme.Text, TextAnchor.UpperLeft, true, 24);
            UIK.Place(rifleName.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(24, -14), new Vector2(330, 48));
            rarityBg = UIK.Panel(s.transform, "Rarity", Theme.TextMute, "pill");
            UIK.Place(rarityBg.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(24, -68), new Vector2(124, 32));
            rifleRarity = UIK.Txt(rarityBg.transform, "T", "", Res.FontBold, 20, Theme.Ink, TextAnchor.MiddleCenter, false);
            UIK.Stretch(rifleRarity.rectTransform, 4, 0, 4, 0);
            var pl = UIK.Txt(s.transform, "PowerL", "GÜÇ", Res.FontSemi, 22, Theme.TextDim, TextAnchor.UpperLeft, false);
            UIK.Place(pl.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(366, -16), new Vector2(80, 28));
            riflePower = UIK.Txt(s.transform, "Power", "", Res.FontBold, 44, Theme.Accent, TextAnchor.UpperLeft, false);
            UIK.Place(riflePower.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(366, -40), new Vector2(150, 50));
            powerFill = UIK.Bar(s.transform, "PowerBar", new Color(1, 1, 1, 0.12f), Theme.Accent, out var pb);
            UIK.Place(pb, UIK.TL, new Vector2(0, 1), new Vector2(160, -76), new Vector2(190, 14));
            upgrade = UIK.Btn(s.transform, "Upgrade", "GELİŞTİR", BtnStyle.Primary, () => ui.ShowArmory(null), "up", 32);
            UIK.Place((RectTransform)upgrade.transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(230, 84));
        }

        // ---- contract of the day (a finished one again as VETERAN, ×2 pay once a day)
        void BuildDaily(RectTransform parent)
        {
            var card = UIK.Panel(parent, "Daily", new Color(0.30f, 0.19f, 0.02f, 0.92f), "ch18");
            daily = card.rectTransform;
            UIK.Place(daily, UIK.TR, new Vector2(1, 1), new Vector2(0, -572), new Vector2(760, 104));
            var ln = UIK.Panel(card.transform, "line", new Color(1f, 0.7f, 0.12f, 0.45f), "ch18_line");
            UIK.Stretch(ln.rectTransform);
            var ic = UIK.Icon(card.transform, "medal", Theme.Accent, 52);
            UIK.Place(ic.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(52, 52));
            dailyTitle = UIK.Txt(card.transform, "T", "", Res.FontBold, 32, Theme.Accent, TextAnchor.UpperLeft, true, 20);
            UIK.Place(dailyTitle.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(96, -12), new Vector2(640, 42));
            dailySub = UIK.Txt(card.transform, "S", "", Res.FontSemi, 24, Theme.TextDim, TextAnchor.UpperLeft, true, 16);
            UIK.Place(dailySub.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(96, -56), new Vector2(640, 34));
            card.raycastTarget = true;
            var b = card.gameObject.AddComponent<Button>();
            b.targetGraphic = card;
            b.onClick.AddListener(() =>
            {
                if (dailyDef == null || Save.DailyDone(Difficulty.TodayKey)) return;
                Sfx.UI(Sfx.Confirm);
                Difficulty.VeteranId = dailyDef.id;
                Difficulty.DailyId = dailyDef.id;
                ui.ShowBriefing(dailyDef);
            });
        }

        // ---- three small jobs a day (progress from every contract, collected here)
        void BuildTasks(RectTransform parent)
        {
            var c = UIK.Panel(parent, "Tasks", new Color(0.035f, 0.05f, 0.065f, 0.92f), "ch18");
            tasksCard = c.rectTransform;
            UIK.Place(tasksCard, UIK.TR, new Vector2(1, 1), new Vector2(0, -692), new Vector2(760, 206));
            var ln = UIK.Panel(c.transform, "line", new Color(1, 1, 1, 0.12f), "ch18_line");
            UIK.Stretch(ln.rectTransform);
            var t = UIK.Txt(c.transform, "T", "GÜNLÜK GÖREVLER", Res.FontSemi, 22, Theme.Accent, TextAnchor.UpperLeft, false);
            UIK.Place(t.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(20, -10), new Vector2(400, 28));
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var row = UIK.Rect(c.transform, "Row" + i);
                row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
                row.pivot = new Vector2(0.5f, 1);
                row.offsetMin = new Vector2(16, -42 - i * 54 - 48); row.offsetMax = new Vector2(-16, -42 - i * 54);
                taskIcon[i] = UIK.Icon(row, "target", Theme.TextMute, 28);
                UIK.Place(taskIcon[i].rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(28, 28));
                taskText[i] = UIK.Txt(row, "Text", "", Res.FontSemi, 25, Theme.Text, TextAnchor.MiddleLeft, true, 16);
                UIK.Stretch(taskText[i].rectTransform, 44, 0, 250, 0);
                taskProg[i] = UIK.Txt(row, "Prog", "", Res.FontBold, 25, Theme.TextDim, TextAnchor.MiddleRight, false);
                UIK.Place(taskProg[i].rectTransform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-170, 0), new Vector2(70, 40));
                taskBtn[i] = UIK.Btn(row, "Claim", "", BtnStyle.Secondary, () => ClaimTask(k), null, 24);
                UIK.Place((RectTransform)taskBtn[i].transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(156, 44));
                var lbl = taskBtn[i].transform.Find("label").GetComponent<Text>();
                lbl.alignment = TextAnchor.MiddleRight;
                UIK.Stretch(lbl.rectTransform, 44, 2, 14, 2);
                taskRewardIcon[i] = UIK.Img(taskBtn[i].transform, "RI", "gem", Color.white);
                taskRewardIcon[i].preserveAspect = true;
                UIK.Place(taskRewardIcon[i].rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(32, 32));
            }
        }

        void SetTasks()
        {
            today = DailyTasks.Today();
            for (int i = 0; i < 3; i++)
            {
                bool has = i < today.Count;
                taskText[i].transform.parent.gameObject.SetActive(has);
                if (!has) continue;
                var t = today[i];
                int n = DailyTasks.Count(t);
                bool done = DailyTasks.Done(t), claimed = DailyTasks.Claimed(t);
                taskText[i].text = t.text;
                taskText[i].color = claimed ? Theme.TextMute : Theme.Text;
                taskProg[i].text = n + "/" + t.goal;
                taskProg[i].color = done ? Theme.Success : Theme.TextDim;
                taskIcon[i].sprite = Res.Icon(claimed || done ? "check" : "target");
                taskIcon[i].color = claimed ? Theme.TextMute : (done ? Theme.Success : Theme.TextMute);
                UIK.SetBtnLabel(taskBtn[i], claimed ? "ALINDI" : (t.gems > 0 ? "+" + t.gems : "+" + UIK.Num(t.cash)));
                taskRewardIcon[i].sprite = Res.Sprite(t.gems > 0 ? "gem" : "coin_c");
                taskRewardIcon[i].enabled = !claimed;
                var img = taskBtn[i].GetComponent<Image>();
                var c = done && !claimed ? Theme.Accent : Theme.PanelLight;
                img.color = c;
                taskBtn[i].GetComponent<ButtonFX>().Bind(img, c);
                taskBtn[i].transform.Find("label").GetComponent<Text>().color = done && !claimed ? Theme.Ink : Theme.TextDim;
                taskBtn[i].interactable = done && !claimed;
            }
        }

        void ClaimTask(int i)
        {
            if (today == null || i >= today.Count) return;
            var t = today[i];
            if (!DailyTasks.Claim(t)) return;
            Sfx.Play(Sfx.Success, 0.7f, 1.2f);
            if (t.gems > 0) RewardFX.Fly(taskBtn[i].transform, bar.GemIcon, "gem", 5, () => Sfx.Play(Sfx.Coin, 0.3f, 1.6f));
            if (t.cash > 0) RewardFX.Fly(taskBtn[i].transform, bar.CashIcon, "coin_c", 6, () => Sfx.Play(Sfx.Coin, 0.3f, 1.3f));
            SetTasks();
        }

        void SetDaily()
        {
            if (daily == null) return;
            dailyDef = Progress.Level >= 3 || Save.UnlockAll ? Difficulty.DailyPick() : null;
            daily.gameObject.SetActive(dailyDef != null);
            if (dailyDef == null) return;
            bool done = Save.DailyDone(Difficulty.TodayKey);
            dailyTitle.text = done ? "GÜNÜN SÖZLEŞMESİ  ·  TAMAM" : "GÜNÜN SÖZLEŞMESİ  ·  ×2 ÖDÜL  +10 ELMAS";
            var leftT = DateTime.Today.AddDays(1) - DateTime.Now;
            dailySub.text = done ? "Yenisi " + leftT.Hours + " sa " + leftT.Minutes + " dk sonra"
                                 : dailyDef.number.ToString("00") + " · " + dailyDef.title + "  —  VETERAN kurallarıyla";
        }

        void SetFeature(MissionDef m)
        {
            if (m == null) return;
            var t = m.targets.Count > 0 ? m.targets[0] : null;
            featPortrait.sprite = t != null ? Res.Portrait(t.charId) : null;
            featPortrait.color = featPortrait.sprite != null ? Color.white : new Color(1, 1, 1, 0);
            featTitle.text = m.number.ToString("00") + "  ·  " + m.title;
            featName.text = t != null ? t.name + (string.IsNullOrEmpty(t.alias) ? "" : "  “" + t.alias + "”") : m.location;
            featKind.text = string.IsNullOrEmpty(m.kind) ? "SUİKAST" : m.kind;
            featKindBg.color = KindColorOf(featKind.text);
            float d = m.Distance;
            featInfo.text = d > 0 ? "~" + Mathf.RoundToInt(d / 5f) * 5 + " m" : "";
            featReward.text = UIK.Money(m.reward);
        }

        public static Color KindColorOf(string kind)
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

        void SetRifle()
        {
            var w = Gear.Equipped;
            var col = Gear.RarityColor(w);
            rifleName.text = w.name;
            rifleRarity.text = Gear.Rarity(w);
            rarityBg.color = col;
            int p = Gear.Power(w), max = Gear.MaxPower(w);
            riflePower.text = p.ToString();
            powerFill.fillAmount = max > 0 ? p / (float)max : 0f;
            powerFill.color = col;
            stage.Tint(col);
            stage.Bind(this, w.model, false);
            bool canUp = false;
            for (int i = 0; i < 5 && !canUp; i++) canUp = Gear.CanUpgrade(w, i) && Gear.UpLevel(w, i) < Gear.MaxUp - 1;
            armoryBadge.gameObject.SetActive(canUp);
            tileArmoryInfo.text = "Güç " + p + " / " + max;
        }

        /// <summary>Columns for the real safe-area width (phones: both columns wide; iPads: narrower right column).</summary>
        float laidFor;

        void Layout()
        {
            float sw = Safe.rect.width;
            if (sw < 10f || Mathf.Abs(sw - laidFor) < 0.5f) return;
            laidFor = sw;
            float rw = Mathf.Clamp(sw - 52f - 640f - 40f - 36f, 520f, 760f);
            right.offsetMin = new Vector2(-40 - rw, 0); right.offsetMax = new Vector2(-40, -122);
            float sh = rw * 430f / 760f;
            showcase.sizeDelta = new Vector2(rw, sh);
            strip.sizeDelta = new Vector2(rw, 116f);
            strip.anchoredPosition = new Vector2(0, -sh - 10f);
            daily.sizeDelta = new Vector2(rw, 104f);
            daily.anchoredPosition = new Vector2(0, -sh - 10f - 116f - 16f);
            float ty = -sh - 10f - 116f - 16f - (daily.gameObject.activeSelf ? 104f + 14f : 0f);
            tasksCard.sizeDelta = new Vector2(rw, 206f);
            tasksCard.anchoredPosition = new Vector2(0, ty);
            // short screens: no room under the daily card -> the jobs card hides (the shop tile badge still shows)
            float room = Safe.rect.height - 122f + ty;
            tasksCard.gameObject.SetActive(room >= 206f);
            // narrow screens: the upgrade button and power shift to fit
            float sx = rw / 760f;
            ((RectTransform)upgrade.transform).sizeDelta = new Vector2(Mathf.Lerp(190f, 230f, Mathf.InverseLerp(0.68f, 1f, sx)), 84f);
            riflePower.rectTransform.anchoredPosition = new Vector2(366f * Mathf.Lerp(0.82f, 1f, Mathf.InverseLerp(0.68f, 1f, sx)), -40f);
        }

        void Update()
        {
            if (Visible) Layout();
        }

        /// <summary>The contract to continue with: the first open one without stars on the current map, then anywhere,
        /// otherwise the last open one on the current map.</summary>
        static MissionDef NextMission()
        {
            string here = Game.I != null ? Game.I.LocationId : "harbor";
            foreach (var m in MissionDefs.InLocation(here))
                if (MissionDefs.IsUnlocked(m) && Save.Stars(m.id) == 0) return m;
            foreach (var m in MissionDefs.All)
                if (MissionDefs.IsUnlocked(m) && Save.Stars(m.id) == 0) return m;
            MissionDef last = null;
            foreach (var m in MissionDefs.InLocation(here)) if (MissionDefs.IsUnlocked(m)) last = m;
            return last ?? MissionDefs.All[0];
        }

        protected override void OnShow()
        {
            Progress.Migrate();
            if (Game.I != null) sub.text = Game.I.Location.operation;
            versionT.text = "v" + Game.Version + (Game.TestBuild && Save.UnlockAll ? "  ·  TEST MODU: tüm bölümler açık" : "  ·  Tek oyunculu keskin nişancı");
            var m = NextMission();
            bool fresh = Save.Stars(m.id) == 0;
            UIK.SetBtnLabel(play, fresh && m.number == 1 && Save.TotalStars == 0 ? "OYNA" : "DEVAM ET");
            playSub.text = m.number.ToString("00") + " · " + m.title + (m.region != (Game.I != null ? Game.I.LocationId : m.region) ? "  (" + m.Location.name + ")" : "");
            SetFeature(m);
            SetDaily();
            SetTasks();
            SetRifle();
            tileMissionsInfo.text = Save.TotalStars + " / " + MissionDefs.All.Count * 3 + " ★";
            bool starter = !Store.Starter.Owned;
            tileShopInfo.text = starter ? "Başlangıç paketi" : (Ads.FreeGemsLeft > 0 && Ads.RewardedReady ? "Ücretsiz elmas" : "Elmas · Nakit");
            shopBadge.gameObject.SetActive(starter || (Ads.FreeGemsLeft > 0 && Ads.RewardedReady));
            int found = 0;
            foreach (var d in MissionDefs.All) if (Save.Completed(d.id)) found++;
            tileCaseInfo.text = found + " dosya";
            laidFor = 0f;
            Layout();
            bar.Refresh(true);
            Popups(found);
        }

        /// <summary>
        /// One thing at a time over the menu, in this order: Apple's tracking question (asked at launch; the menu waits
        /// for it, and asks itself if it is still due), the daily login reward, the "no ads" offer (once a day after a few full-screen ads), the starter
        /// pack (once, after the first few contracts).
        /// </summary>
        void Popups(int found)
        {
            // a rating sheet that could not be shown on the result screen (level up on top): ask here instead
            if (Review.TryShow(ui)) return;
            if (Privacy.Asking || (Privacy.AttPending && !Privacy.AttDue))
            {
                Tween.Run(this, 0.01f, null, () => { if (Visible) Popups(found); }, 0.6f);
                return;
            }
            if (Privacy.AttDue)
            {
                Tween.Run(this, 0.01f, null, () => { if (Visible) StartCoroutine(Privacy.AskTracking(() => { if (Visible) Popups(found); })); }, 0.7f);
                return;
            }
            if (DailyRewardScreen.Due && Save.TotalStars > 0) { Tween.Run(this, 0.01f, null, () => { if (Visible && DailyRewardScreen.Due) ui.Daily.Show(0.25f); }, 0.6f); return; }
            if (Ads.NoAdsNudgeDue && Store.Ready && !Store.NoAds.Owned)
            {
                Tween.Run(this, 0.01f, null, () =>
                {
                    if (!Visible || !Ads.NoAdsNudgeDue) return;
                    Ads.NoAdsNudged();
                    Analytics.Event("offer_shown", "offer", "no_ads", "placement", "nudge");
                    ui.Confirm("REKLAMLARDAN SIKILDIN MI?",
                               "Görev aralarındaki reklamları " + Store.PriceOf(Store.NoAds) + " karşılığında sonsuza dek kaldır. Hediye: " + Store.NoAds.gems + " elmas.",
                               "REKLAMSIZ OL", () => Store.Buy(Store.NoAds, (ok, err) =>
                               {
                                   if (ok) { Sfx.Play(Sfx.Success, 0.8f, 1.1f); bar.Refresh(false); }
                                   else if (!string.IsNullOrEmpty(err)) ui.Confirm("SATIN ALINAMADI", err, "TAMAM", null, "KAPAT");
                               }, "nudge"), "ŞİMDİ DEĞİL");
                }, 0.8f);
                return;
            }
            if (found >= Tuning.Int("starter_after") && !Store.Starter.Owned && !Save.Flag("starter_offer") && Store.Ready)
            {
                Tween.Run(this, 0.01f, null, () =>
                {
                    if (!Visible || Save.Flag("starter_offer")) return;
                    Save.SetFlag("starter_offer");
                    Analytics.Event("offer_shown", "offer", "starter_pack", "placement", "menu_auto");
                    ui.ShowShop(ShopScreen.Tab.Offers);
                }, 0.8f);
            }
        }

        protected override void OnHide()
        {
            if (stage != null) stage.Unbind(this);
        }

        void OnPlay() => ui.ShowBriefing(NextMission());
    }
}

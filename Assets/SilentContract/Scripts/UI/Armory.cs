using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>
    /// The armory: rifles in a lit showroom (turn them with a finger) with their power rating and five upgrade lines each
    /// (cash, the last level in diamonds), scope gear, special rounds and the shooter's skills. Opened from the menu, the
    /// briefing or the menu's upgrade shortcut.
    /// </summary>
    public class ArmoryScreen : UIScreen
    {
        enum Tab { Rifles, Gear, Ammo, Skills }
        Tab tab = Tab.Rifles;
        readonly Button[] tabBtn = new Button[4];
        readonly RectTransform[] pages = new RectTransform[4];
        public Action BackTo;
        MetaBar bar;

        // rifles
        WeaponDef selected;
        readonly List<RifleRow> rows = new List<RifleRow>();
        RectTransform listRt, stageRt, panelRt;
        StageView stage;
        Text stagePower, stagePowerMax, wName, wInfo, wRarity, wDesc, wNote;
        Image wRarityBg, stagePowerFill;
        Button wButton;
        Text wAction;
        readonly UpRow[] upRows = new UpRow[5];
        Vector2 laidOutFor;

        class RifleRow { public WeaponDef w; public Image bg, strip, glow, stateIcon; public Text name, sub, state; }
        class UpRow { public int stat; public RectTransform rt; public Image icon; public Text name, value; public Image[] pips; public Button buy; public Text price; public Image priceIcon; }
        // gear / ammo / skills
        class ItemCard { public object item; public Text state, count; public Button btn; public Image icon; }
        class SkillCard { public string key; public int[] cost; public Func<int, string> stat; public Image[] pips; public Text statText; public Button buy; }
        readonly List<ItemCard> gearCards = new List<ItemCard>();
        readonly List<ItemCard> ammoCards = new List<ItemCard>();
        readonly List<SkillCard> skillCards = new List<SkillCard>();

        protected override void Build()
        {
            var bg = UIK.Img(transform, "Bg", null, new Color(0.015f, 0.02f, 0.03f, 0.97f), false, true);
            UIK.Stretch(bg.rectTransform);
            var floor = UIK.Img(transform, "Floor", "grad_v", new Color(0.12f, 0.1f, 0.06f, 0.5f));
            floor.rectTransform.anchorMin = new Vector2(0, 0); floor.rectTransform.anchorMax = new Vector2(1, 0.45f);
            floor.rectTransform.offsetMin = floor.rectTransform.offsetMax = Vector2.zero;
            floor.rectTransform.localScale = new Vector3(1, -1, 1);
            Header("CEPHANELİK", () => Close());
            bar = MetaBar.Build(ui, Safe, false);

            string[] names = { "TÜFEKLER", "EKİPMAN", "MÜHİMMAT", "BECERİLER" };
            string[] icons = { "rifle", "nightvision", "ammo", "medal" };
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                var b = UIK.Btn(Safe, "Tab" + i, names[i], BtnStyle.Secondary, () => SetTab((Tab)k), icons[i], 28);
                var r = (RectTransform)b.transform;
                r.anchorMin = new Vector2(i / 4f, 1); r.anchorMax = new Vector2((i + 1) / 4f, 1);
                r.pivot = new Vector2(0.5f, 1);
                r.offsetMin = new Vector2(i == 0 ? 24 : 8, -210); r.offsetMax = new Vector2(i == 3 ? -24 : -8, -136);
                tabBtn[i] = b;
            }
            for (int i = 0; i < 4; i++)
            {
                var p = UIK.Rect(Safe, "Page" + i);
                p.anchorMin = Vector2.zero; p.anchorMax = Vector2.one;
                p.offsetMin = new Vector2(24, 20); p.offsetMax = new Vector2(-24, -226);
                pages[i] = p;
            }
            BuildRifles(pages[0]);
            BuildGear(pages[1]);
            BuildAmmo(pages[2]);
            BuildSkills(pages[3]);
        }

        void Close()
        {
            if (BackTo != null) { var b = BackTo; BackTo = null; b(); }
            else ui.ShowMenu(false);
        }

        void SetTab(Tab t)
        {
            tab = t;
            Refresh();
        }

        // ------------------------------------------------------------------ rifles: list | showroom | upgrades
        void BuildRifles(RectTransform page)
        {
            listRt = UIK.Rect(page, "List");
            for (int i = 0; i < Gear.Weapons.Count; i++)
            {
                var w = Gear.Weapons[i];
                var row = new RifleRow { w = w };
                row.bg = UIK.Panel(listRt, "Row_" + w.id, new Color(0.05f, 0.065f, 0.085f, 0.95f), "ch18");
                row.bg.raycastTarget = true;
                var r = row.bg.rectTransform;
                r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
                r.pivot = new Vector2(0.5f, 1);
                r.offsetMin = new Vector2(0, -(i + 1) * 112 + 10); r.offsetMax = new Vector2(0, -i * 112);
                row.glow = UIK.Img(row.bg.transform, "Glow", "edge_glow", new Color(1, 1, 1, 0.14f));
                UIK.Stretch(row.glow.rectTransform, 2, 2, 2, 2);
                row.strip = UIK.Img(row.bg.transform, "Strip", null, Color.white);
                row.strip.rectTransform.anchorMin = new Vector2(0, 0); row.strip.rectTransform.anchorMax = new Vector2(0, 1);
                row.strip.rectTransform.pivot = new Vector2(0, 0.5f);
                row.strip.rectTransform.offsetMin = new Vector2(0, 14); row.strip.rectTransform.offsetMax = new Vector2(7, -14);
                var btn = row.bg.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => { Sfx.UI(Sfx.Click); Select(w); });
                row.name = UIK.Txt(row.bg.transform, "Name", w.name, Res.FontBold, 30, Theme.Text, TextAnchor.UpperLeft, true, 18);
                row.name.rectTransform.anchorMin = new Vector2(0, 1); row.name.rectTransform.anchorMax = new Vector2(1, 1);
                row.name.rectTransform.pivot = new Vector2(0, 1);
                row.name.rectTransform.offsetMin = new Vector2(24, -50); row.name.rectTransform.offsetMax = new Vector2(-60, -12);
                row.sub = UIK.Txt(row.bg.transform, "Sub", "", Res.FontSemi, 23, Theme.TextDim, TextAnchor.UpperLeft, true, 14);
                row.sub.rectTransform.anchorMin = new Vector2(0, 1); row.sub.rectTransform.anchorMax = new Vector2(1, 1);
                row.sub.rectTransform.pivot = new Vector2(0, 1);
                row.sub.rectTransform.offsetMin = new Vector2(24, -86); row.sub.rectTransform.offsetMax = new Vector2(-60, -54);
                row.sub.supportRichText = true;
                row.stateIcon = UIK.Icon(row.bg.transform, "check", Theme.Success, 32);
                UIK.Place(row.stateIcon.rectTransform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(32, 32));
                rows.Add(row);
            }

            // showroom
            stageRt = UIK.Rect(page, "Stage");
            stage = StageView.Build(stageRt, "View", true);
            UIK.Stretch((RectTransform)stage.transform, 0, 40, 0, 70);
            var pw = UIK.Panel(stageRt, "Power", new Color(0.03f, 0.04f, 0.055f, 0.85f), "cut16");
            UIK.Place(pw.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(10, -4), new Vector2(300, 92));
            var pl = UIK.Txt(pw.transform, "L", "GÜÇ", Res.FontSemi, 24, Theme.TextDim, TextAnchor.UpperLeft, false);
            UIK.Place(pl.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(18, -8), new Vector2(80, 30));
            stagePower = UIK.Txt(pw.transform, "V", "", Res.FontBold, 54, Theme.Accent, TextAnchor.UpperLeft, false);
            UIK.Place(stagePower.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(16, -26), new Vector2(150, 62));
            stagePowerMax = UIK.Txt(pw.transform, "Max", "", Res.FontSemi, 24, Theme.TextMute, TextAnchor.UpperLeft, false);
            UIK.Place(stagePowerMax.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(150, -50), new Vector2(140, 30));
            stagePowerFill = UIK.Bar(pw.transform, "Bar", new Color(1, 1, 1, 0.12f), Theme.Accent, out var pbar);
            UIK.Place(pbar, UIK.TL, new Vector2(0, 1), new Vector2(150, -24), new Vector2(130, 14));
            var hint = UIK.Txt(stageRt, "Hint", "Çevirmek için sürükle", Res.FontSemi, 23, Theme.TextMute, TextAnchor.LowerCenter, false);
            hint.rectTransform.anchorMin = new Vector2(0, 0); hint.rectTransform.anchorMax = new Vector2(1, 0);
            hint.rectTransform.pivot = new Vector2(0.5f, 0);
            hint.rectTransform.offsetMin = new Vector2(0, 4); hint.rectTransform.offsetMax = new Vector2(0, 34);

            // upgrade panel
            var panel = UIK.Panel(page, "Panel", new Color(0.04f, 0.055f, 0.072f, 0.96f), "ch28");
            panelRt = panel.rectTransform;
            var ln = UIK.Panel(panel.transform, "line", new Color(1, 1, 1, 0.1f), "ch28_line");
            UIK.Stretch(ln.rectTransform);
            wName = UIK.Txt(panel.transform, "Name", "", Res.FontBold, 46, Theme.Text, TextAnchor.UpperLeft, true, 28);
            TopLine(wName.rectTransform, 22, -16, 58, 160);
            wRarityBg = UIK.Panel(panel.transform, "Rarity", Theme.TextMute, "pill");
            UIK.Place(wRarityBg.rectTransform, UIK.TR, new Vector2(1, 1), new Vector2(-22, -24), new Vector2(130, 36));
            wRarity = UIK.Txt(wRarityBg.transform, "T", "", Res.FontBold, 21, Theme.Ink, TextAnchor.MiddleCenter, false);
            UIK.Stretch(wRarity.rectTransform, 4, 0, 4, 0);
            wInfo = UIK.Txt(panel.transform, "Info", "", Res.FontSemi, 24, Theme.Accent, TextAnchor.UpperLeft, true, 16);
            TopLine(wInfo.rectTransform, 24, -74, 32, 24);
            wDesc = UIK.Txt(panel.transform, "Desc", "", Res.FontBody, 23, Theme.TextDim, TextAnchor.UpperLeft, true, 15);
            TopLine(wDesc.rectTransform, 24, -108, 60, 24);
            for (int i = 0; i < 5; i++) upRows[i] = UpgradeRow(panel.transform, i);
            wNote = UIK.Txt(panel.transform, "Note", "", Res.FontSemi, 23, Theme.TextDim, TextAnchor.UpperLeft, true, 15);
            wNote.supportRichText = true;
            wButton = UIK.Btn(panel.transform, "Action", "", BtnStyle.Primary, OnRifleAction, "cash", 34);
            wAction = wButton.transform.Find("label").GetComponent<Text>();
            RewardFX.Shine((RectTransform)wButton.transform, 3.6f);
            laidOutFor = Vector2.zero;
        }

        static void TopLine(RectTransform r, float x, float y, float h, float rightInset)
        {
            r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0, 1);
            r.offsetMin = new Vector2(x, y - h); r.offsetMax = new Vector2(-rightInset, y);
        }

        UpRow UpgradeRow(Transform parent, int stat)
        {
            var u = new UpRow { stat = stat };
            var bgi = UIK.Panel(parent, "Up" + stat, new Color(1, 1, 1, 0.045f), "ch10");
            u.rt = bgi.rectTransform;
            u.icon = UIK.Icon(bgi.transform, Gear.UpIcons[stat], Theme.Accent, 40);
            UIK.Place(u.icon.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(40, 40));
            u.name = UIK.Txt(bgi.transform, "N", Gear.UpNames[stat], Res.FontBold, 27, Theme.Text, TextAnchor.UpperLeft, false);
            UIK.Place(u.name.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(66, -8), new Vector2(160, 32));
            u.pips = new Image[Gear.MaxUp];
            for (int k = 0; k < Gear.MaxUp; k++)
            {
                var p = UIK.Panel(bgi.transform, "Pip" + k, Theme.Line, "cut16");
                UIK.Place(p.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(66 + k * 34, 12), new Vector2(30, 14));
                u.pips[k] = p;
            }
            u.value = UIK.Txt(bgi.transform, "V", "", Res.FontSemi, 23, Theme.TextDim, TextAnchor.MiddleLeft, true, 14);
            u.value.supportRichText = true;
            u.value.rectTransform.anchorMin = new Vector2(0, 0); u.value.rectTransform.anchorMax = new Vector2(1, 1);
            u.value.rectTransform.offsetMin = new Vector2(250, 4); u.value.rectTransform.offsetMax = new Vector2(-196, -4);
            u.buy = UIK.Btn(bgi.transform, "Buy", "", BtnStyle.Primary, () => BuyUpgrade(stat), null, 26);
            UIK.Place((RectTransform)u.buy.transform, UIK.MR, new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(178, 62));
            u.price = u.buy.transform.Find("label").GetComponent<Text>();
            u.price.alignment = TextAnchor.MiddleRight;
            UIK.Stretch(u.price.rectTransform, 50, 2, 16, 2);
            u.priceIcon = UIK.Img(u.buy.transform, "PI", "coin_c", Color.white);
            u.priceIcon.preserveAspect = true;
            UIK.Place(u.priceIcon.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(38, 38));
            return u;
        }

        /// <summary>Three columns for the real page size: list (fixed), showroom (what is left), upgrade panel.</summary>
        void LayoutRifles()
        {
            if (panelRt == null) return;
            var size = pages[0].rect.size;
            if (size.x < 10f || (size - laidOutFor).sqrMagnitude < 1f) return;
            laidOutFor = size;
            float listW = size.x < 1500f ? 300f : 360f;
            float panelW = Mathf.Clamp(size.x * 0.38f, 560f, 690f);
            Col(listRt, 0f, listW);
            Col(stageRt, listW + 16f, size.x - listW - 16f - panelW - 16f);
            Col(panelRt, size.x - panelW, panelW);
            // upgrade rows: on a narrow panel (4:3 iPads) the price button gets smaller and the values start earlier
            bool narrowPanel = panelW < 640f;
            float buyW = narrowPanel ? 140f : 178f;
            foreach (var u in upRows)
            {
                ((RectTransform)u.buy.transform).sizeDelta = new Vector2(buyW, 62f);
                u.value.rectTransform.offsetMin = new Vector2(narrowPanel ? 236f : 250f, 4f);
                u.value.rectTransform.offsetMax = new Vector2(-(buyW + 18f), -4f);
            }
            // panel rows from the top: header block 176, five rows, note, button at the bottom
            float h = size.y;
            float rowH = Mathf.Clamp((h - 176f - 120f - 70f) / 5f, 66f, 84f);
            float y = -176f;
            for (int i = 0; i < 5; i++)
            {
                var r = upRows[i].rt;
                r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
                r.pivot = new Vector2(0.5f, 1);
                r.offsetMin = new Vector2(16, y - rowH + 6f); r.offsetMax = new Vector2(-16, y);
                y -= rowH;
            }
            TopLine(wNote.rectTransform, 24, y - 4f, 60f, 24);
            var br = (RectTransform)wButton.transform;
            br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0);
            br.pivot = new Vector2(0.5f, 0);
            br.offsetMin = new Vector2(16, 16); br.offsetMax = new Vector2(-16, 112);
        }

        static void Col(RectTransform r, float x, float w)
        {
            r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 0.5f);
            r.offsetMin = new Vector2(x, 0); r.offsetMax = new Vector2(x + w, 0);
        }

        void Select(WeaponDef w)
        {
            selected = w;
            RefreshRifles();
        }

        void OnRifleAction()
        {
            var w = selected;
            if (w == null) return;
            if (Gear.Owns(w))
            {
                Gear.Equip(w);
                Sfx.Play(Sfx.Equip, 0.8f);
            }
            else if (!Gear.Unlocked(w.unlockStars) || Save.Cash < w.price)
            {
                // locked by stars (or short of cash): diamonds open it right away
                int gems = GemPrice(w);
                if (Save.Gems < gems)
                {
                    ui.Confirm("ELMAS YETMİYOR", w.name + " için " + gems + " elmas gerekiyor, sende " + Save.Gems + " var.", "MAĞAZA",
                               () => ui.ShowShop(ShopScreen.Tab.Gems, () => ui.ShowArmory(BackTo)));
                    return;
                }
                ui.Confirm("HEMEN AL", w.name + " " + gems + " elmasla hemen senin olsun mu?", "AL", () =>
                {
                    if (!Progress.SpendGems(gems, "rifle_" + w.id)) return;
                    Save.SetOwned("w_" + w.id);
                    Analytics.Event("weapon_buy", "weapon", w.id, "currency", "gems", "price", gems);
                    Gear.Equip(w);
                    Sfx.Play(Sfx.Success, 0.8f);
                    Sfx.Play(Sfx.Equip, 0.7f);
                    Tween.Pop(wName.transform, 0.35f, 0f, 0.8f);
                    if (game.Sniper != null) game.Sniper.RefreshWeapon();
                    Refresh();
                }, "VAZGEÇ");
                return;
            }
            else if (Gear.Buy(w))
            {
                Gear.Equip(w);
                Sfx.Play(Sfx.Coin, 0.9f);
                Sfx.Play(Sfx.Equip, 0.7f);
                Tween.Pop(wName.transform, 0.35f, 0f, 0.8f);
            }
            if (game.Sniper != null) game.Sniper.RefreshWeapon();
            Refresh();
        }

        /// <summary>Diamond price of a rifle that is still locked or too dear (the shortcut past the stars).</summary>
        static int GemPrice(WeaponDef w) => Mathf.Max(60, Mathf.RoundToInt(w.price * 1.2f / 100f / 5f) * 5);

        void BuyUpgrade(int stat)
        {
            var w = selected;
            if (w == null || !Gear.Owns(w)) return;
            int lvl = Gear.UpLevel(w, stat);
            if (lvl >= Gear.MaxUp) return;
            int cost = Gear.UpCost(w, stat, lvl + 1, out bool gems);
            if (gems && Progress.Level < Gear.GemTierLevel)
            {
                Sfx.UI(Sfx.Back);
                ui.Confirm("SEVİYE " + Gear.GemTierLevel + " GEREKLİ", "Son kademe (elmasla) " + Gear.GemTierLevel + ". seviyede açılır. Sözleşmelerden tecrübe kazan.", "TAMAM", null, "KAPAT");
                return;
            }
            // a small gap in cash: a video covers it (a few times a day) — the moment a video is worth the most to the player
            if (!gems && Ads.ShortfallOffer(cost, Save.Cash))
            {
                int gap = cost - Save.Cash;
                Analytics.Event("offer_shown", "offer", "shortfall_ad", "placement", "armory", "gap", gap);
                ui.Confirm("AZ KALDI", "Bu geliştirme için " + UIK.Money(gap) + " eksik. Kısa bir video izle, eksik para tamamlansın ve geliştirme hemen yapılsın." +
                           "\nBugün " + Ads.ShortfallLeft + " hakkın var.",
                           "İZLE VE GELİŞTİR", () => Ads.ShowRewarded("armory_shortfall", ok =>
                           {
                               if (!ok || Save.Cash >= cost) { if (ok) BuyUpgrade(stat); return; }
                               Ads.UseShortfall();
                               Progress.AddCash(cost - Save.Cash, "ad_shortfall");
                               BuyUpgrade(stat);
                           }), "MAĞAZA", () => ui.ShowShop(ShopScreen.Tab.Cash, () => ui.ShowArmory(BackTo)));
                return;
            }
            if (gems ? Save.Gems < cost : Save.Cash < cost)
            {
                Sfx.UI(Sfx.Back);
                ui.Confirm(gems ? "ELMAS YETMİYOR" : "PARA YETMİYOR",
                           gems ? "Son kademe " + cost + " elmas. Mağazadan elmas alabilir ya da günlük ödüllerle toplayabilirsin."
                                : "Bu geliştirme " + UIK.Money(cost) + ". Sözleşmelerden para kazan ya da mağazaya bak.",
                           "MAĞAZA", () => ui.ShowShop(gems ? ShopScreen.Tab.Gems : ShopScreen.Tab.Cash, () => ui.ShowArmory(BackTo)), "KAPAT");
                return;
            }
            int before = Gear.Power(w);
            if (!Gear.BuyUpgrade(w, stat)) return;
            Sfx.Play(Sfx.Equip, 0.7f, 1.1f);
            Sfx.Play(Sfx.Coin, 0.6f, 1.25f);
            var u = upRows[stat];
            Tween.Pop(u.pips[Mathf.Clamp(lvl, 0, Gear.MaxUp - 1)].transform, 0.35f, 0f, 0.3f);
            int after = Gear.Power(w);
            Tween.Run(stagePower, 0.6f, k => { if (stagePower) stagePower.text = Mathf.RoundToInt(Mathf.Lerp(before, after, Util.EaseOutCubic(k))).ToString(); });
            Tween.Pop(stagePower.transform, 0.4f, 0f, 1.25f);
            if (game.Sniper != null) game.Sniper.RefreshWeapon();
            RefreshRifles(false);
        }

        void RefreshRifles(bool power = true)
        {
            if (selected == null) selected = Gear.Equipped;
            foreach (var r in rows)
            {
                bool sel = r.w == selected;
                bool owned = Gear.Owns(r.w), eq = Gear.Equipped == r.w, open = Gear.Unlocked(r.w.unlockStars);
                var rc = Gear.RarityColor(r.w);
                r.bg.color = sel ? new Color(0.13f, 0.11f, 0.06f, 0.98f) : new Color(0.05f, 0.065f, 0.085f, 0.95f);
                r.glow.color = new Color(rc.r, rc.g, rc.b, sel ? 0.26f : 0.12f);
                r.strip.color = rc;
                r.stateIcon.enabled = eq || !open || !owned;
                r.stateIcon.sprite = Res.Icon(eq ? "check" : (open ? "cash" : "lock"));
                r.stateIcon.color = eq ? Theme.Success : (open ? Theme.Accent : Theme.TextMute);
                r.sub.text = owned ? "GÜÇ <color=#FFB21E>" + Gear.Power(r.w) + "</color>   ·   " + (eq ? "<color=#36D47C>KUŞANILDI</color>" : "SAHİP")
                                   : (open ? Gear.Rarity(r.w) + "   ·   " + UIK.Money(r.w.price) : Gear.Rarity(r.w) + "   ·   " + r.w.unlockStars + " ★ ile açılır");
                r.name.color = open || owned ? Theme.Text : Theme.TextDim;
            }
            var w = selected;
            var col = Gear.RarityColor(w);
            bool own = Gear.Owns(w), eq2 = Gear.Equipped == w, open2 = Gear.Unlocked(w.unlockStars);
            wName.text = w.name;
            wRarity.text = Gear.Rarity(w);
            wRarityBg.color = col;
            wInfo.text = w.caliber + "   ·   " + (w.semiAuto ? "Yarı otomatik" : "Sürgülü") + (w.silenced ? "   ·   Susturuculu" : "") +
                         (w.pierce > 0 ? (w.pierce >= 2 ? "   ·   Duvar deler" : "   ·   Siper deler") : "");
            wDesc.text = w.desc;
            int pw = Gear.Power(w), max = Gear.MaxPower(w);
            if (power) { Tween.Stop(stagePower); stagePower.text = pw.ToString(); }
            stagePowerMax.text = "/ " + max;
            stagePowerFill.fillAmount = max > 0 ? pw / (float)max : 0f;
            stagePowerFill.color = col;
            stage.Tint(col);
            for (int i = 0; i < 5; i++)
            {
                var u = upRows[i];
                int lvl = Gear.UpLevel(w, i);
                for (int k = 0; k < Gear.MaxUp; k++)
                    u.pips[k].color = k < lvl ? (k == Gear.MaxUp - 1 ? Theme.Info : Theme.Accent) : Theme.Line;
                string now = Gear.UpValue(w, i, lvl);
                u.value.text = lvl < Gear.MaxUp ? now + "  <color=#6B7682>›</color>  <color=#36D47C>" + Gear.UpValue(w, i, lvl + 1) + "</color>" : now + "  <color=#46C8F2>EN ÜST</color>";
                bool max5 = lvl >= Gear.MaxUp;
                u.buy.gameObject.SetActive(own && !max5);
                if (own && !max5)
                {
                    int cost = Gear.UpCost(w, i, lvl + 1, out bool gems);
                    bool lockedTier = gems && Progress.Level < Gear.GemTierLevel;
                    u.price.text = lockedTier ? "SV " + Gear.GemTierLevel : (gems ? cost.ToString() : UIK.Num(cost));
                    u.priceIcon.sprite = lockedTier ? Res.Icon("lock") : Res.Sprite(gems ? "gem" : "coin_c");
                    u.priceIcon.color = lockedTier ? Theme.Ink : Color.white;
                    bool can = !lockedTier && (gems ? Save.Gems >= cost : Save.Cash >= cost);
                    UIK.Group(u.buy.gameObject).alpha = can ? 1f : 0.55f;
                    var img = u.buy.GetComponent<Image>();
                    var c = gems ? new Color(0.2f, 0.55f, 0.95f) : Theme.Accent;
                    img.color = c;
                    u.buy.GetComponent<ButtonFX>().Bind(img, c);
                    u.price.color = gems ? Color.white : Theme.Ink;
                }
                u.name.color = own ? Theme.Text : Theme.TextDim;
                u.icon.color = own ? Theme.Accent : Theme.TextMute;
            }
            if (!own) wNote.text = "Satın aldığında beş hatta geliştirebilirsin. En üst kademe <color=#46C8F2>elmasla</color>.";
            else if (Gear.Effective(w).power < Gear.ArmorPower)
                wNote.text = "Zırhlı adamların gövdesini delmek için <color=#FFB21E>HASAR</color> " + ArmorLevel(w) + ". kademe ya da zırh delici mermi gerekir.";
            else wNote.text = "Bu tüfek zırhlı gövdeyi deler.";
            var ic = wButton.transform.Find("icon");
            var ici = ic != null ? ic.GetComponent<Image>() : null;
            var bimg = wButton.GetComponent<Image>();
            Color bc = Theme.Accent;
            if (eq2) { wAction.text = "KUŞANILDI"; UIK.SetInteractable(wButton, false); if (ici) ici.sprite = Res.Icon("check"); }
            else if (own) { wAction.text = "KUŞAN"; UIK.SetInteractable(wButton, true); if (ici) ici.sprite = Res.Icon("rifle"); }
            else if (open2 && Save.Cash >= w.price) { wAction.text = "SATIN AL   " + UIK.Money(w.price); UIK.SetInteractable(wButton, true); if (ici) ici.sprite = Res.Icon("cash"); }
            else
            {
                wAction.text = (open2 ? "" : w.unlockStars + " ★ YA DA   ") + GemPrice(w) + " ELMASLA AL";
                UIK.SetInteractable(wButton, true);
                if (ici) ici.sprite = Res.Icon("gem_glyph");
                bc = new Color(0.2f, 0.55f, 0.95f);
            }
            bimg.color = bc;
            wButton.GetComponent<ButtonFX>().Bind(bimg, bc);
            if (Visible && tab == Tab.Rifles) stage.Bind(this, w.model, true);
        }

        /// <summary>The HASAR level from which this rifle's rounds get through body armour.</summary>
        static int ArmorLevel(WeaponDef w)
        {
            for (int l = 1; l <= Gear.MaxUp; l++) if (w.power * (1f + 0.12f * l) >= Gear.ArmorPower) return l;
            return Gear.MaxUp;
        }

        // ------------------------------------------------------------------ gear, ammo
        void BuildGear(RectTransform page)
        {
            var items = Gear.Gadgets;
            for (int i = 0; i < items.Count; i++)
            {
                var g = items[i];
                var c = Card(page, i, items.Count, g.icon, g.name, g.desc, out var stateT, out var countT, out var btn);
                c.item = g; c.state = stateT; c.count = countT; c.btn = btn;
                btn.onClick.AddListener(() =>
                {
                    if (Gear.Owns(g)) return;
                    if (Gear.Buy(g)) { Sfx.Play(Sfx.Coin, 0.9f); Tween.Pop(c.icon.transform, 0.35f, 0f, 0.7f); }
                    else Sfx.UI(Sfx.Back);
                    Refresh();
                });
                gearCards.Add(c);
            }
        }

        void BuildAmmo(RectTransform page)
        {
            var items = new List<AmmoDef>();
            foreach (var a in Gear.Ammo) if (a.kind != AmmoKind.Standard) items.Add(a);
            for (int i = 0; i < items.Count; i++)
            {
                var a = items[i];
                var c = Card(page, i, items.Count, a.icon, a.name, a.desc, out var stateT, out var countT, out var btn);
                c.icon.color = a.tint;
                c.item = a; c.state = stateT; c.count = countT; c.btn = btn;
                btn.onClick.AddListener(() =>
                {
                    if (Gear.Buy(a)) { Sfx.Play(Sfx.Coin, 0.9f); Tween.Pop(c.count.transform, 0.35f, 0f, 0.7f); }
                    else Sfx.UI(Sfx.Back);
                    Refresh();
                });
                ammoCards.Add(c);
            }
        }

        ItemCard Card(RectTransform page, int i, int n, string icon, string title, string desc, out Text state, out Text count, out Button btn)
        {
            var c = new ItemCard();
            var card = UIK.Panel(page, "Card" + i, new Color(0.045f, 0.06f, 0.08f, 0.96f), "ch28");
            var r = card.rectTransform;
            r.anchorMin = new Vector2(i / (float)n, 0); r.anchorMax = new Vector2((i + 1) / (float)n, 1);
            r.offsetMin = new Vector2(i == 0 ? 0 : 12, 0); r.offsetMax = new Vector2(i == n - 1 ? 0 : -12, 0);
            var ln = UIK.Panel(card.transform, "line", new Color(1, 1, 1, 0.1f), "ch28_line");
            UIK.Stretch(ln.rectTransform);
            var spot = UIK.Img(card.transform, "Spot", "spot", new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.12f));
            UIK.Place(spot.rectTransform, UIK.TC, new Vector2(0.5f, 0.5f), new Vector2(0, -92), new Vector2(380, 230));
            c.icon = UIK.Icon(card.transform, icon, Theme.Accent, 104);
            UIK.Place(c.icon.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(104, 104));
            var t = UIK.Txt(card.transform, "Title", title, Res.FontBold, 40, Theme.Text, TextAnchor.UpperCenter);
            t.rectTransform.anchorMin = new Vector2(0, 1); t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.pivot = new Vector2(0.5f, 1);
            t.rectTransform.offsetMin = new Vector2(20, -214); t.rectTransform.offsetMax = new Vector2(-20, -160);
            var d = UIK.Txt(card.transform, "Desc", desc, Res.FontBody, 26, Theme.TextDim, TextAnchor.UpperCenter, true, 16);
            d.rectTransform.anchorMin = new Vector2(0, 1); d.rectTransform.anchorMax = new Vector2(1, 1);
            d.rectTransform.pivot = new Vector2(0.5f, 1);
            d.rectTransform.offsetMin = new Vector2(26, -350); d.rectTransform.offsetMax = new Vector2(-26, -222);
            count = UIK.Txt(card.transform, "Count", "", Res.FontBold, 36, Theme.Accent, TextAnchor.LowerCenter);
            count.rectTransform.anchorMin = new Vector2(0, 0); count.rectTransform.anchorMax = new Vector2(1, 0);
            count.rectTransform.pivot = new Vector2(0.5f, 0);
            count.rectTransform.offsetMin = new Vector2(20, 140); count.rectTransform.offsetMax = new Vector2(-20, 190);
            state = UIK.Txt(card.transform, "State", "", Res.FontSemi, 24, Theme.TextDim, TextAnchor.LowerCenter);
            state.rectTransform.anchorMin = new Vector2(0, 0); state.rectTransform.anchorMax = new Vector2(1, 0);
            state.rectTransform.pivot = new Vector2(0.5f, 0);
            state.rectTransform.offsetMin = new Vector2(20, 116); state.rectTransform.offsetMax = new Vector2(-20, 146);
            btn = UIK.Btn(card.transform, "Buy", "", BtnStyle.Primary, null, "cash", 30);
            var br = (RectTransform)btn.transform;
            br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0);
            br.pivot = new Vector2(0.5f, 0);
            br.offsetMin = new Vector2(26, 24); br.offsetMax = new Vector2(-26, 112);
            return c;
        }

        void RefreshGear()
        {
            foreach (var c in gearCards)
            {
                var g = (GadgetDef)c.item;
                bool owned = Gear.Owns(g), open = Gear.Unlocked(g.unlockStars);
                c.count.text = owned ? "SAHİPSİN" : "";
                c.state.text = owned ? "Görevlerde otomatik takılı" : (open ? "" : g.unlockStars + " yıldızla açılır");
                UIK.SetBtnLabel(c.btn, owned ? "SAHİPSİN" : (open ? "SATIN AL  " + UIK.Money(g.price) : "KİLİTLİ"));
                UIK.SetInteractable(c.btn, !owned && open && Save.Cash >= g.price);
            }
            foreach (var c in ammoCards)
            {
                var a = (AmmoDef)c.item;
                c.count.text = "× " + Gear.AmmoCount(a.kind);
                c.state.text = a.pack + " adetlik paket";
                UIK.SetBtnLabel(c.btn, "+" + a.pack + "  " + UIK.Money(a.price));
                UIK.SetInteractable(c.btn, Save.Cash >= a.price);
            }
        }

        // ------------------------------------------------------------------ the shooter's skills
        void BuildSkills(RectTransform page)
        {
            var defs = new[]
            {
                new { key = "stability", title = "SABİT NİŞAN", icon = "crosshair", desc = "Dürbündeki el titremesi her kademede %17 azalır.", cost = new[] { 2000, 4600, 8400 } },
                new { key = "breath", title = "NEFES KONTROLÜ", icon = "breath", desc = "Nefesini daha uzun tutarsın: nişangah daha uzun sabit kalır.", cost = new[] { 1800, 4000, 7800 } },
                new { key = "composure", title = "SOĞUKKANLILIK", icon = "heart", desc = "Ateş altında ve yaralıyken titreme her kademede %25 azalır.", cost = new[] { 2800, 6000, 10400 } },
            };
            Func<int, string>[] stats =
            {
                l => l == 0 ? "%0" : "-%" + (17 * l),
                l => (4.5f + 1.2f * l).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace(".", ",") + " sn",
                l => l == 0 ? "%0" : "-%" + (25 * l),
            };
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                var u = new SkillCard { key = d.key, cost = d.cost, stat = stats[i] };
                var card = UIK.Panel(page, "Skill_" + d.key, new Color(0.045f, 0.06f, 0.08f, 0.96f), "ch28");
                var r = card.rectTransform;
                r.anchorMin = new Vector2(i / 3f, 0); r.anchorMax = new Vector2((i + 1) / 3f, 1);
                r.offsetMin = new Vector2(i == 0 ? 0 : 12, 0); r.offsetMax = new Vector2(i == 2 ? 0 : -12, 0);
                var ln = UIK.Panel(card.transform, "line", new Color(1, 1, 1, 0.1f), "ch28_line");
                UIK.Stretch(ln.rectTransform);
                var spot = UIK.Img(card.transform, "Spot", "spot", new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.12f));
                UIK.Place(spot.rectTransform, UIK.TC, new Vector2(0.5f, 0.5f), new Vector2(0, -84), new Vector2(380, 220));
                var ic = UIK.Icon(card.transform, d.icon, Theme.Accent, 88);
                UIK.Place(ic.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(88, 88));
                var t = UIK.Txt(card.transform, "Title", d.title, Res.FontBold, 40, Theme.Text, TextAnchor.UpperCenter);
                UIK.Place(t.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -146), new Vector2(370, 52));
                var ds = UIK.Txt(card.transform, "Desc", d.desc, Res.FontBody, 26, Theme.TextDim, TextAnchor.UpperCenter, true, 16);
                UIK.Place(ds.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -206), new Vector2(360, 100));
                u.pips = new Image[3];
                for (int k = 0; k < 3; k++)
                {
                    var p = UIK.Panel(card.transform, "Pip" + k, Theme.Line, "cut16");
                    UIK.Place(p.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2((k - 1) * 112, -330), new Vector2(100, 18));
                    u.pips[k] = p;
                }
                u.statText = UIK.Txt(card.transform, "Stat", "", Res.FontBold, 36, Theme.Accent, TextAnchor.UpperCenter);
                UIK.Place(u.statText.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -372), new Vector2(370, 48));
                u.buy = UIK.Btn(card.transform, "Buy", "", BtnStyle.Primary, () => BuySkill(u), "cash", 30);
                var br = (RectTransform)u.buy.transform;
                br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0);
                br.pivot = new Vector2(0.5f, 0);
                br.offsetMin = new Vector2(26, 24); br.offsetMax = new Vector2(-26, 112);
                skillCards.Add(u);
            }
        }

        void BuySkill(SkillCard u)
        {
            int lvl = Save.Upgrade(u.key);
            if (lvl >= 3) return;
            int cost = u.cost[lvl];
            if (Save.Cash < cost) { Sfx.UI(Sfx.Back); return; }
            Progress.SpendCash(cost, "skill_" + u.key);
            Save.SetUpgrade(u.key, lvl + 1);
            Analytics.Event("skill_upgrade", "skill", u.key, "level", lvl + 1, "price", cost);
            Sfx.Play(Sfx.Coin, 0.9f);
            Refresh();
            Tween.Pop(u.statText.transform, 0.3f, 0f, 0.8f);
        }

        void RefreshSkills()
        {
            foreach (var u in skillCards)
            {
                int lvl = Save.Upgrade(u.key);
                for (int i = 0; i < 3; i++) u.pips[i].color = i < lvl ? Theme.Accent : Theme.Line;
                u.statText.text = lvl < 3 ? u.stat(lvl) + "   »   " + u.stat(lvl + 1) : u.stat(lvl);
                if (lvl >= 3)
                {
                    UIK.SetBtnLabel(u.buy, "MAKSİMUM");
                    UIK.SetInteractable(u.buy, false);
                }
                else
                {
                    int cost = u.cost[lvl];
                    UIK.SetBtnLabel(u.buy, "YÜKSELT  " + UIK.Money(cost));
                    UIK.SetInteractable(u.buy, Save.Cash >= cost);
                }
            }
        }

        // ------------------------------------------------------------------ show / refresh
        protected override void OnShow()
        {
            selected = Gear.Equipped;
            laidOutFor = Vector2.zero;
            Refresh();
        }

        protected override void OnHide()
        {
            if (stage != null) stage.Unbind(this);
        }

        void Refresh()
        {
            bool rifles = tab == Tab.Rifles && Visible;
            if (!rifles && stage != null) stage.Unbind(this);
            Canvas.ForceUpdateCanvases();
            LayoutRifles();
            for (int i = 0; i < 4; i++)
            {
                bool on = (int)tab == i;
                pages[i].gameObject.SetActive(on);
                var img = tabBtn[i].GetComponent<Image>();
                var c = on ? Theme.Accent : Theme.PanelLight;
                img.color = c;
                tabBtn[i].GetComponent<ButtonFX>().Bind(img, c);
                var t = tabBtn[i].transform.Find("label");
                if (t != null) t.GetComponent<Text>().color = on ? Theme.Ink : Theme.Text;
                var ic = tabBtn[i].transform.Find("icon");
                if (ic != null) ic.GetComponent<Image>().color = on ? Theme.Ink : Theme.Text;
            }
            RefreshRifles();
            RefreshGear();
            RefreshSkills();
        }

        void Update()
        {
            if (Visible && tab == Tab.Rifles) LayoutRifles();
        }
    }

    /// <summary>Drag on the rifle preview turns the model.</summary>
    public class PreviewDrag : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
    {
        public Action<Vector2> onDrag;
        public Action onUp;
        public void OnPointerDown(PointerEventData e) { }
        public void OnDrag(PointerEventData e) => onDrag?.Invoke(e.delta);
        public void OnPointerUp(PointerEventData e) => onUp?.Invoke();
    }
}

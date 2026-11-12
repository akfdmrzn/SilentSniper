using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>
    /// The shop: offers (starter pack, no ads, free diamonds for a video), diamond packs (real money through
    /// <see cref="Store"/>) and cash packs (bought with diamonds).
    /// </summary>
    public class ShopScreen : UIScreen
    {
        public enum Tab { Offers, Packs, Gems, Cash }
        const int TabCount = 4;
        Tab tab = Tab.Offers;
        readonly Button[] tabBtn = new Button[TabCount];
        readonly RectTransform[] pages = new RectTransform[TabCount];
        MetaBar bar;
        Text testNote;
        public Action BackTo;

        class Card { public Product p; public Button buy; public Text price, state; public RectTransform rt; public Image art; }
        readonly List<Card> cards = new List<Card>();
        Card starter, noAds;
        Button freeBtn;
        Text freeLeft;
        RectTransform freeCard;

        protected override void Build()
        {
            var bg = UIK.Img(transform, "Bg", null, new Color(0.015f, 0.02f, 0.03f, 0.97f), false, true);
            UIK.Stretch(bg.rectTransform);
            var glow = UIK.Img(transform, "Glow", "spot", new Color(0.27f, 0.6f, 0.95f, 0.10f));
            UIK.Stretch(glow.rectTransform, -200, -100, -200, 200);
            Header("MAĞAZA", Close);
            bar = MetaBar.Build(ui, Safe, false);

            string[] names = { "TEKLİFLER", "SİLAH PAKETLERİ", "ELMAS", "NAKİT" };
            string[] icons = { "gift", "crown", "gem_glyph", "cash" };
            for (int i = 0; i < TabCount; i++)
            {
                int k = i;
                var b = UIK.Btn(Safe, "Tab" + i, names[i], BtnStyle.Secondary, () => SetTab((Tab)k), icons[i], 28);
                var r = (RectTransform)b.transform;
                r.anchorMin = new Vector2(i / (float)TabCount, 1); r.anchorMax = new Vector2((i + 1) / (float)TabCount, 1);
                r.pivot = new Vector2(0.5f, 1);
                r.offsetMin = new Vector2(i == 0 ? 24 : 8, -218); r.offsetMax = new Vector2(i == TabCount - 1 ? -24 : -8, -136);
                tabBtn[i] = b;
            }
            for (int i = 0; i < TabCount; i++)
            {
                var p = UIK.Rect(Safe, "Page" + i);
                p.anchorMin = Vector2.zero; p.anchorMax = Vector2.one;
                p.offsetMin = new Vector2(24, 64); p.offsetMax = new Vector2(-24, -236);
                pages[i] = p;
            }
            BuildOffers(pages[0]);
            BuildBundles(pages[1]);
            BuildPacks(pages[2], Store.Gems);
            BuildPacks(pages[3], Store.Cash);
            Store.Changed += () => { if (this != null && Visible) Refresh(); };
            testNote = UIK.Txt(Safe, "TestNote", "", Res.FontSemi, 24, Theme.TextMute, TextAnchor.LowerLeft, false);
            UIK.Place(testNote.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(28, 60), new Vector2(1400, 36));
            var restore = UIK.Btn(Safe, "Restore", "SATIN ALIMLARI GERİ YÜKLE", BtnStyle.Ghost, Restore, null, 22);
            UIK.Place((RectTransform)restore.transform, UIK.BR, new Vector2(1, 0), new Vector2(-24, 8), new Vector2(420, 50));
            // App Store rule: the purchase screen links the terms of use and the privacy policy
            LegalLink("Terms", "Kullanım Koşulları (EULA)", MonetizationConfig.TermsUrl, 24);
            LegalLink("Privacy", "Gizlilik Politikası", MonetizationConfig.PrivacyUrl, 444);
        }

        void LegalLink(string name, string label, string url, float x)
        {
            if (string.IsNullOrEmpty(url)) return;
            var t = UIK.Txt(Safe, name, label, Res.FontSemi, 24, Theme.Info, TextAnchor.MiddleCenter, true, 16);
            t.raycastTarget = true;
            UIK.Place(t.rectTransform, UIK.BL, new Vector2(0, 0), new Vector2(x, 10), new Vector2(400, 46));
            var line = UIK.Img(t.transform, "Underline", null, new Color(Theme.Info.r, Theme.Info.g, Theme.Info.b, 0.6f));
            line.rectTransform.anchorMin = new Vector2(0.18f, 0); line.rectTransform.anchorMax = new Vector2(0.82f, 0);
            line.rectTransform.offsetMin = new Vector2(0, 4); line.rectTransform.offsetMax = new Vector2(0, 6);
            var b = t.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = t;
            b.onClick.AddListener(() =>
            {
                Sfx.UI(Sfx.Confirm);
                Analytics.Event("legal_link", "link", name.ToLowerInvariant());
                Application.OpenURL(url);
            });
        }

        void Close()
        {
            if (BackTo != null) { var b = BackTo; BackTo = null; b(); }
            else ui.ShowMenu(false);
        }

        public void Open(Tab t)
        {
            tab = t;
            if (Visible) Refresh();
        }

        void SetTab(Tab t)
        {
            tab = t;
            Refresh();
        }

        // ------------------------------------------------------------------ offers
        void BuildOffers(RectTransform page)
        {
            starter = OfferCard(page, Store.Starter, 0f, 0.42f, new Color(0.36f, 0.22f, 0.04f, 0.96f), new Color(1f, 0.72f, 0.2f, 0.7f));
            noAds = OfferCard(page, Store.NoAds, 0.42f, 0.74f, new Color(0.05f, 0.12f, 0.2f, 0.96f), new Color(0.3f, 0.75f, 1f, 0.55f));
            // free diamonds for a video
            var c = UIK.Panel(page, "Free", new Color(0.04f, 0.15f, 0.1f, 0.96f), "ch28");
            freeCard = c.rectTransform;
            freeCard.anchorMin = new Vector2(0.74f, 0); freeCard.anchorMax = new Vector2(1f, 1);
            freeCard.offsetMin = new Vector2(12, 0); freeCard.offsetMax = Vector2.zero;
            var ln = UIK.Panel(c.transform, "line", new Color(0.3f, 1f, 0.6f, 0.45f), "ch28_line");
            UIK.Stretch(ln.rectTransform);
            var burst = UIK.Img(c.transform, "Burst", "burst", new Color(0.4f, 1f, 0.7f, 0.18f));
            UIK.Place(burst.rectTransform, UIK.TC, new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(380, 380));
            burst.gameObject.AddComponent<Spin>().speed = 10f;
            var art = UIK.Img(c.transform, "Art", "shop_gems_1", Color.white);
            art.preserveAspect = true;
            UIK.Place(art.rectTransform, UIK.TC, new Vector2(0.5f, 0.5f), new Vector2(0, -186), new Vector2(230, 230));
            var t = UIK.Txt(c.transform, "Title", "ÜCRETSİZ ELMAS", Res.FontBold, 42, Theme.Text, TextAnchor.UpperCenter, true, 26);
            UIK.Place(t.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(420, 52));
            var a = UIK.Txt(c.transform, "Amount", "+" + Ads.FreeGems, Res.FontBold, 64, Theme.Success, TextAnchor.UpperCenter, false);
            UIK.Place(a.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -384), new Vector2(420, 76));
            freeLeft = UIK.Txt(c.transform, "Left", "", Res.FontSemi, 26, Theme.TextDim, TextAnchor.UpperCenter, false);
            UIK.Place(freeLeft.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -462), new Vector2(420, 34));
            freeBtn = UIK.Btn(c.transform, "Watch", "İZLE", BtnStyle.Primary, WatchForGems, "ad", 32);
            var br = (RectTransform)freeBtn.transform;
            br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0);
            br.pivot = new Vector2(0.5f, 0);
            br.offsetMin = new Vector2(26, 26); br.offsetMax = new Vector2(-26, 122);
        }

        Card OfferCard(RectTransform page, Product p, float x0, float x1, Color fill, Color line)
        {
            var card = new Card { p = p };
            var c = UIK.Panel(page, "Offer_" + p.id, fill, "ch28");
            card.rt = c.rectTransform;
            card.rt.anchorMin = new Vector2(x0, 0); card.rt.anchorMax = new Vector2(x1, 1);
            card.rt.offsetMin = new Vector2(x0 > 0 ? 12 : 0, 0); card.rt.offsetMax = new Vector2(-12, 0);
            var ln = UIK.Panel(c.transform, "line", line, "ch28_line");
            UIK.Stretch(ln.rectTransform);
            var rays = UIK.Img(c.transform, "Rays", "rays", new Color(line.r, line.g, line.b, 0.22f));
            rays.rectTransform.anchorMin = new Vector2(0, 0.35f); rays.rectTransform.anchorMax = new Vector2(1, 1);
            rays.rectTransform.offsetMin = rays.rectTransform.offsetMax = Vector2.zero;
            if (!string.IsNullOrEmpty(p.badge)) Ribbon(c.transform, p.badge, Theme.Danger);
            card.art = UIK.Img(c.transform, "Art", p.art, Color.white);
            card.art.preserveAspect = true;
            UIK.Place(card.art.rectTransform, UIK.TC, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(270, 270));
            var t = UIK.Txt(c.transform, "Title", p.title, Res.FontBold, 50, Theme.Text, TextAnchor.UpperCenter, true, 30);
            UIK.Place(t.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -312), new Vector2(640, 60));
            var d = UIK.Txt(c.transform, "Desc", p.desc, Res.FontBody, 27, Theme.TextDim, TextAnchor.UpperCenter, true, 18);
            d.rectTransform.anchorMin = new Vector2(0, 1); d.rectTransform.anchorMax = new Vector2(1, 1);
            d.rectTransform.pivot = new Vector2(0.5f, 1);
            d.rectTransform.offsetMin = new Vector2(36, -460); d.rectTransform.offsetMax = new Vector2(-36, -378);
            // contents as icon chips
            var items = new List<KeyValuePair<string, string>>();
            if (p.gems > 0) items.Add(new KeyValuePair<string, string>("gem", UIK.Num(p.gems)));
            if (p.cash > 0) items.Add(new KeyValuePair<string, string>("coin_c", UIK.Money(p.cash)));
            if (p.apRounds > 0) items.Add(new KeyValuePair<string, string>("ic:pierce", p.apRounds + "×"));
            if (!string.IsNullOrEmpty(p.weapon)) items.Insert(0, new KeyValuePair<string, string>("ic:rifle", ShortName(p.weapon)));
            if (!string.IsNullOrEmpty(p.gadget)) items.Insert(1, new KeyValuePair<string, string>("ic:" + GadgetIcon(p.gadget), GadgetLabel(p.gadget)));
            for (int i = 0; i < items.Count; i++)
            {
                var chip = UIK.Panel(c.transform, "Item" + i, new Color(0, 0, 0, 0.35f), "pill");
                float w = items.Count >= 4 ? 150f : (items.Count >= 3 ? 168f : 200f);
                UIK.Place(chip.rectTransform, UIK.BC, new Vector2(0.5f, 0), new Vector2((i - (items.Count - 1) * 0.5f) * (w + 12f), 142), new Vector2(w, 60));
                Image ic = items[i].Key.StartsWith("ic:") ? UIK.Icon(chip.transform, items[i].Key.Substring(3), Theme.Info, 40) : UIK.Img(chip.transform, "I", items[i].Key, Color.white);
                ic.preserveAspect = true;
                UIK.Place(ic.rectTransform, UIK.ML, new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(46, 46));
                var v = UIK.Txt(chip.transform, "V", items[i].Value, Res.FontBold, 30, Theme.Text, TextAnchor.MiddleRight, true, 18);
                UIK.Stretch(v.rectTransform, 60, 2, 18, 2);
            }
            card.buy = UIK.Btn(c.transform, "Buy", "", BtnStyle.Primary, () => Buy(card), null, 36);
            var br = (RectTransform)card.buy.transform;
            br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0);
            br.pivot = new Vector2(0.5f, 0);
            br.offsetMin = new Vector2(26, 26); br.offsetMax = new Vector2(-26, 122);
            RewardFX.Shine(br, 3.5f);
            cards.Add(card);
            return card;
        }

        // ------------------------------------------------------------------ rifle packs
        void BuildBundles(RectTransform page)
        {
            OfferCard(page, Store.ShadowPack, 0f, 0.5f, new Color(0.04f, 0.1f, 0.08f, 0.96f), new Color(0.35f, 1f, 0.55f, 0.55f));
            OfferCard(page, Store.TitanPack, 0.5f, 1f, new Color(0.2f, 0.14f, 0.03f, 0.96f), new Color(1f, 0.78f, 0.25f, 0.7f));
        }

        static string ShortName(string weaponId)
        {
            var w = Gear.Weapon(weaponId);
            if (w == null) return weaponId;
            string n = w.name;
            int sp = n.IndexOf(' ');
            return sp > 0 ? n.Substring(sp + 1) : n;          // "GÖLGE SR-7" → "SR-7"
        }

        static string GadgetIcon(string id) { var g = Gear.Gadget(id); return g != null ? g.icon : "eye"; }
        static string GadgetLabel(string id) => id == "nv" ? "GECE" : (id == "thermal" ? "TERMAL" : id.ToUpperInvariant());

        static void Ribbon(Transform parent, string text, Color c)
        {
            var r = UIK.Panel(parent, "Ribbon", c, "cut16");
            UIK.Place(r.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(-6, -18), new Vector2(250, 50));
            var t = UIK.Txt(r.transform, "T", text, Res.FontBold, 26, Color.white, TextAnchor.MiddleCenter, true, 16);
            UIK.Stretch(t.rectTransform, 10, 0, 10, 0);
            UIK.AddShadow(r, 3f, 0.5f);
        }

        // ------------------------------------------------------------------ diamond / cash packs
        void BuildPacks(RectTransform page, List<Product> list)
        {
            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                var p = list[i];
                var card = new Card { p = p };
                bool gem = p.RealMoney;
                var c = UIK.Panel(page, "Pack_" + p.id, new Color(0.045f, 0.06f, 0.08f, 0.97f), "ch28");
                card.rt = c.rectTransform;
                card.rt.anchorMin = new Vector2(i / (float)n, 0); card.rt.anchorMax = new Vector2((i + 1) / (float)n, 1);
                card.rt.offsetMin = new Vector2(i == 0 ? 0 : 10, 0); card.rt.offsetMax = new Vector2(i == n - 1 ? 0 : -10, 0);
                var hl = new Color(gem ? 0.3f : 1f, gem ? 0.75f : 0.72f, gem ? 1f : 0.2f, 0.5f);
                var ln = UIK.Panel(c.transform, "line", hl, "ch28_line");
                UIK.Stretch(ln.rectTransform);
                var spot = UIK.Img(c.transform, "Spot", "spot", new Color(hl.r, hl.g, hl.b, 0.22f));
                UIK.Place(spot.rectTransform, UIK.TC, new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(420, 300));
                if (!string.IsNullOrEmpty(p.badge)) Ribbon(c.transform, p.badge, gem ? Theme.Danger : new Color(0.15f, 0.62f, 0.32f));
                // the pack's own render (a handful of diamonds ... a vault; an envelope of cash ... a briefcase)
                var art = UIK.Img(c.transform, "Art", p.art, Color.white);
                art.preserveAspect = true;
                UIK.Place(art.rectTransform, UIK.TC, new Vector2(0.5f, 0.5f), new Vector2(0, -186), new Vector2(240, 240));
                card.art = art;
                var amount = UIK.Txt(c.transform, "Amount", gem ? UIK.Num(p.gems) : UIK.Money(p.cash), Res.FontBold, 60, Theme.Text, TextAnchor.UpperCenter, true, 34);
                amount.rectTransform.anchorMin = new Vector2(0, 1); amount.rectTransform.anchorMax = new Vector2(1, 1);
                amount.rectTransform.pivot = new Vector2(0.5f, 1);
                amount.rectTransform.offsetMin = new Vector2(16, -384); amount.rectTransform.offsetMax = new Vector2(-16, -310);
                UIK.AddShadow(amount, 3f, 0.6f);
                var title = UIK.Txt(c.transform, "Title", p.title, Res.FontSemi, 28, Theme.TextDim, TextAnchor.UpperCenter, true, 18);
                title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = new Vector2(1, 1);
                title.rectTransform.pivot = new Vector2(0.5f, 1);
                title.rectTransform.offsetMin = new Vector2(16, -424); title.rectTransform.offsetMax = new Vector2(-16, -388);
                if (p.bonusPct > 0)
                {
                    var bonus = UIK.Txt(c.transform, "Bonus", "+%" + p.bonusPct + " BONUS", Res.FontBold, 30, Theme.Success, TextAnchor.UpperCenter, false);
                    bonus.rectTransform.anchorMin = new Vector2(0, 1); bonus.rectTransform.anchorMax = new Vector2(1, 1);
                    bonus.rectTransform.pivot = new Vector2(0.5f, 1);
                    bonus.rectTransform.offsetMin = new Vector2(16, -466); bonus.rectTransform.offsetMax = new Vector2(-16, -428);
                }
                card.buy = UIK.Btn(c.transform, "Buy", "", gem ? BtnStyle.Primary : BtnStyle.Secondary, () => Buy(card), gem ? null : "gem_glyph", 34);
                var br = (RectTransform)card.buy.transform;
                br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0);
                br.pivot = new Vector2(0.5f, 0);
                br.offsetMin = new Vector2(20, 22); br.offsetMax = new Vector2(-20, 118);
                if (!gem)
                {
                    var ic = card.buy.transform.Find("icon");
                    if (ic != null) ic.GetComponent<Image>().color = Theme.Info;
                }
                cards.Add(card);
            }
        }

        // ------------------------------------------------------------------ buying
        void Buy(Card c)
        {
            var p = c.p;
            if (p.Owned || Store.Busy) return;
            if (!p.RealMoney && Save.Gems < p.gemPrice)
            {
                ui.Confirm("ELMAS YETMİYOR", "Bu paket için " + p.gemPrice + " elmas gerekiyor, sende " + Save.Gems + " var.", "ELMAS AL", () => SetTab(Tab.Gems));
                return;
            }
            Store.Buy(p, (ok, err) =>
            {
                if (ok && !string.IsNullOrEmpty(p.weapon))
                {
                    var w = Gear.Weapon(p.weapon);
                    if (w != null) { Gear.Equip(w); if (game.Sniper != null) game.Sniper.RefreshWeapon(); }
                    ui.Confirm("PAKET HAZIR", (w != null ? w.name : "Tüfek") + " kuşanıldı. Cephanelikte seni bekliyor.", "CEPHANELİK",
                               () => ui.ShowArmory(null), "TAMAM");
                }
                if (!ok)
                {
                    if (!string.IsNullOrEmpty(err)) ui.Confirm("SATIN ALINAMADI", err, "TAMAM", null, "KAPAT");
                    return;
                }
                Sfx.Play(Sfx.Success, 0.8f, 1.1f);
                if (p.gems > 0) RewardFX.Fly(c.art != null ? c.art.transform : c.rt, bar.GemIcon, "gem", Mathf.Clamp(p.gems / 60, 4, 12), () => Sfx.Play(Sfx.Coin, 0.3f, 1.6f));
                if (p.cash > 0) RewardFX.Fly(c.art != null ? c.art.transform : c.rt, bar.CashIcon, "coin_c", Mathf.Clamp(p.cash / 4000, 4, 12), () => Sfx.Play(Sfx.Coin, 0.3f, 1.3f));
                Refresh();
            });
        }

        void WatchForGems()
        {
            if (Ads.FreeGemsLeft <= 0 || !Ads.RewardedReady) return;
            Ads.ShowRewarded("shop_free_gems", ok =>
            {
                if (!ok) return;
                Ads.UseFreeGems();
                Progress.AddGems(Ads.FreeGems, "ad_free_gems");
                RewardFX.Fly(freeCard, bar.GemIcon, "gem", 5, () => Sfx.Play(Sfx.Coin, 0.3f, 1.6f));
                Refresh();
            });
        }

        void Restore()
        {
            Store.Restore(n =>
            {
                ui.Confirm("GERİ YÜKLEME", n > 0 ? n + " satın alım geri yüklendi." : "Geri yüklenecek satın alım bulunamadı.", "TAMAM", null, "KAPAT");
                Refresh();
            });
        }

        // ------------------------------------------------------------------ show / refresh
        protected override void OnShow() => Refresh();

        void Refresh()
        {
            for (int i = 0; i < TabCount; i++)
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
            foreach (var c in cards)
            {
                var p = c.p;
                if (p.Owned)
                {
                    UIK.SetBtnLabel(c.buy, "ALINDI");
                    UIK.SetInteractable(c.buy, false);
                    continue;
                }
                UIK.SetBtnLabel(c.buy, Store.PriceOf(p));
                UIK.SetInteractable(c.buy, true);          // a tap while the App Store is not ready explains why (and asks again)
            }
            int left = Ads.FreeGemsLeft;
            freeLeft.text = Ads.RewardedReady ? "Bugün " + left + " / " + Ads.FreeGemsPerDay + " hakkın kaldı" : "Şu an video yok";
            UIK.SetInteractable(freeBtn, left > 0 && Ads.RewardedReady);
            testNote.text = Store.IsTest ? "TEST SÜRÜMÜ: satın almalar ücretsiz tamamlanır, reklamlar yer tutucudur. Gerçek fiyatlar App Store'dan gelir." : "";
        }
    }

    /// <summary>
    /// Daily login reward: a week of rewards, one a day (the streak does not reset — a missed day just waits). Opens once
    /// a day on the menu; a video doubles the day's reward.
    /// </summary>
    public class DailyRewardScreen : UIScreen
    {
        struct DayReward { public int cash, gems, ap; }
        static readonly DayReward[] Week =
        {
            new DayReward { cash = 1000 }, new DayReward { gems = 10 }, new DayReward { cash = 2500 }, new DayReward { ap = 3 },
            new DayReward { gems = 20 }, new DayReward { cash = 6000 }, new DayReward { gems = 50, cash = 5000 },
        };
        readonly RectTransform[] tiles = new RectTransform[7];
        RectTransform dailyCard;
        readonly Image[] tileBg = new Image[7], tileCheck = new Image[7];
        Button take, take2;
        MetaBar bar;

        static string Today => DateTime.Now.ToString("yyyyMMdd");
        static int Claimed => PlayerPrefs.GetInt("sc2_login_count", 0);
        public static bool Due => PlayerPrefs.GetString("sc2_login_last", "") != Today;

        protected override void Build()
        {
            var dim = UIK.Img(transform, "Dim", null, new Color(0, 0, 0, 0.85f), false, true);
            UIK.Stretch(dim.rectTransform);
            bar = MetaBar.Build(ui, Safe, false);
            var card = UIK.Panel(Safe, "Card", new Color(0.04f, 0.055f, 0.075f, 0.98f), "ch28");
            UIK.Place(card.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1640, 760));
            dailyCard = card.rectTransform;
            var ln = UIK.Panel(card.transform, "line", new Color(1f, 0.72f, 0.2f, 0.5f), "ch28_line");
            UIK.Stretch(ln.rectTransform);
            var ic = UIK.Icon(card.transform, "calendar", Theme.Accent, 64);
            UIK.Place(ic.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(48, -40), new Vector2(64, 64));
            var t = UIK.Txt(card.transform, "Title", "GÜNLÜK ÖDÜL", Res.FontBold, 64, Theme.Text, TextAnchor.MiddleLeft, false);
            UIK.Place(t.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(128, -34), new Vector2(900, 76));
            var s = UIK.Txt(card.transform, "Sub", "Her gün gel, ödülün büyüsün. 7. gün büyük ödül.", Res.FontSemi, 28, Theme.TextDim, TextAnchor.MiddleLeft, false);
            UIK.Place(s.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(130, -104), new Vector2(1200, 40));
            for (int i = 0; i < 7; i++)
            {
                bool big = i == 6;
                var tile = UIK.Panel(card.transform, "Day" + i, Theme.PanelLight, "ch18");
                tiles[i] = tile.rectTransform;
                tileBg[i] = tile;
                float w = 196f, gap = 18f;
                float x = 48f + i * (w + gap);
                UIK.Place(tile.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(x, -176), new Vector2(big ? w + 18 : w, 360));
                var dt = UIK.Txt(tile.transform, "D", (i + 1) + ". GÜN", Res.FontBold, 30, Theme.TextDim, TextAnchor.UpperCenter, false);
                UIK.Place(dt.rectTransform, UIK.TC, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(190, 40));
                var r = Week[i];
                string art = big ? "shop_chest" : (r.gems > 0 ? "gem" : (r.cash > 0 ? "coin_c" : null));
                Image a = art != null ? UIK.Img(tile.transform, "A", art, Color.white) : UIK.Icon(tile.transform, "pierce", Theme.Info, 96);
                a.preserveAspect = true;
                UIK.Place(a.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 26), big ? new Vector2(170, 170) : new Vector2(104, 104));
                string label = r.gems > 0 && r.cash > 0 ? r.gems + " + " + UIK.Money(r.cash) : (r.gems > 0 ? r.gems + " ELMAS" : (r.cash > 0 ? UIK.Money(r.cash) : r.ap + " Z. DELİCİ"));
                var lv = UIK.Txt(tile.transform, "V", label, Res.FontBold, 30, Theme.Text, TextAnchor.LowerCenter, true, 18);
                UIK.Place(lv.rectTransform, UIK.BC, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(186, 44));
                tileCheck[i] = UIK.Icon(tile.transform, "check", Theme.Success, 80);
                UIK.Place(tileCheck[i].rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(80, 80));
            }
            take2 = UIK.Btn(card.transform, "Take2", "2× AL", BtnStyle.Secondary, () => Claim(true), "ad", 34);
            UIK.Place((RectTransform)take2.transform, UIK.BR, new Vector2(1, 0), new Vector2(-500, 40), new Vector2(380, 104));
            take = UIK.Btn(card.transform, "Take", "TOPLA", BtnStyle.Primary, () => Claim(false), "gift", 38);
            UIK.Place((RectTransform)take.transform, UIK.BR, new Vector2(1, 0), new Vector2(-48, 40), new Vector2(420, 104));
            RewardFX.Shine((RectTransform)take.transform, 2.6f);
        }

        protected override void OnShow()
        {
            // the week is 1640 wide: narrower screens (4:3 iPads) get the whole card scaled down
            float sw = Safe.rect.width;
            float k = sw > 10f ? Mathf.Min(1f, (sw - 40f) / 1640f) : 1f;
            if (dailyCard != null) dailyCard.localScale = new Vector3(k, k, 1f);
            int today = Claimed % 7;
            for (int i = 0; i < 7; i++)
            {
                bool done = i < today;
                bool now = i == today;
                tileBg[i].color = now ? new Color(0.36f, 0.24f, 0.04f, 1f) : (done ? new Color(0.06f, 0.16f, 0.1f, 1f) : Theme.PanelLight);
                tileCheck[i].enabled = done;
                if (now) Tween.Pop(tiles[i], 0.5f, 0.2f, 0.8f);
            }
            take2.gameObject.SetActive(Ads.RewardedReady);
        }

        void Claim(bool doubled)
        {
            if (!Due) { Hide(); return; }
            if (doubled)
            {
                Ads.ShowRewarded("daily_double", ok => Give(ok ? 2 : 1));
                return;
            }
            Give(1);
        }

        void Give(int mul)
        {
            if (!Due) { Hide(); return; }
            int i = Claimed % 7;
            var r = Week[i];
            PlayerPrefs.SetString("sc2_login_last", Today);
            PlayerPrefs.SetInt("sc2_login_count", Claimed + 1);
            PlayerPrefs.Save();
            if (r.cash > 0) { Progress.AddCash(r.cash * mul, mul > 1 ? "daily_reward_ad" : "daily_reward"); RewardFX.Fly(tiles[i], bar.CashIcon, "coin_c", 8, () => Sfx.Play(Sfx.Coin, 0.3f, 1.3f)); }
            if (r.gems > 0) { Progress.AddGems(r.gems * mul, mul > 1 ? "daily_reward_ad" : "daily_reward"); RewardFX.Fly(tiles[i], bar.GemIcon, "gem", 6, () => Sfx.Play(Sfx.Coin, 0.3f, 1.6f)); }
            if (r.ap > 0) Save.SetAmmoCount(AmmoKind.ArmorPiercing.ToString(), Save.AmmoCount(AmmoKind.ArmorPiercing.ToString()) + r.ap * mul);
            Analytics.Event("daily_reward_claim", "day", i + 1, "doubled", mul > 1 ? 1 : 0);
            tileCheck[i].enabled = true;
            tileBg[i].color = new Color(0.06f, 0.16f, 0.1f, 1f);
            Tween.Pop(tileCheck[i].transform, 0.4f, 0f, 0.3f);
            Sfx.Play(Sfx.Success, 0.8f, 1.2f);
            UIK.SetInteractable(take, false);
            UIK.SetInteractable(take2, false);
            Tween.Run(this, 0.01f, null, () => { Hide(0.25f); UIK.SetInteractable(take, true); UIK.SetInteractable(take2, true); }, 1.3f);
        }
    }
}

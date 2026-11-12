using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>What the shop sells for real money (diamond packs, starter pack, no ads, rifle packs) or for diamonds (cash).</summary>
    public class Product
    {
        public string id;                 // App Store product id (com.akif.silentcontract.gems_500 …)
        public string title, desc, art;   // art: sprite in SC/UI (gem, coin_c …)
        public int gems, cash, apRounds;
        public bool noAds, oneTime;
        /// <summary>Non-consumable (restorable on a new device): no ads, the rifle packs.</summary>
        public bool permanent;
        public string weapon, gadget;     // rifle / gadget the pack unlocks
        public int upgradeTo;             // every upgrade line of that rifle is raised to at least this level
        public int gemPrice;              // > 0: bought with diamonds (cash packs), no store involved
        public string tierPrice;          // fallback price label until the store answers (App Store tier, USD)
        public double usd;                // the base price (analytics fallback)
        public string badge;              // "EN POPÜLER", "%50 BONUS"
        public int bonusPct;
        public Color tint = Color.white;

        public bool RealMoney => gemPrice <= 0;
        public bool Owned => (oneTime || permanent) && Save.Flag("iap_" + id);
    }

    /// <summary>
    /// In-app purchases behind one small interface. RevenueCat (StoreKit underneath) is the store backend on devices;
    /// development builds without a RevenueCat key use a test backend that completes every purchase at once (marked as
    /// TEST in the shop), release builds without a backend say the shop is not open yet.
    /// Consumables (diamonds, starter pack) are granted once per transaction; the permanent items (no ads, rifle packs)
    /// follow the store's entitlements, so they come back on a new device with "restore".
    /// </summary>
    public static class Store
    {
        public interface IBackend
        {
            bool Ready { get; }
            string Price(Product p);                                    // localized price string, null when unknown
            bool PriceValue(Product p, out double value, out string currency);
            void Purchase(Product p, Action<bool, string, string> done); // ok, error (null: cancelled), transaction id
            void Restore(Action<int> done);                              // restored permanent items
        }

        /// <summary>Test purchases complete at once (development builds only).</summary>
        class TestBackend : IBackend
        {
            public bool Ready => true;
            public string Price(Product p) => null;
            public bool PriceValue(Product p, out double value, out string currency) { value = p.usd; currency = "USD"; return true; }
            public void Purchase(Product p, Action<bool, string, string> done) => done(true, null, "test_" + DateTime.UtcNow.Ticks);
            public void Restore(Action<int> done)
            {
                int n = 0;
                foreach (var p in Permanent) if (Save.Flag("iap_" + p.id)) { ApplyPermanent(p, false); n++; }
                done(n);
            }
        }

        public const string Prefix = "com.akif.silentcontract.";

        static IBackend backend;
        public static IBackend Backend
        {
            get
            {
                if (backend == null && (Game.TestBuild || Application.isEditor)) backend = new TestBackend();
                return backend;
            }
            set => backend = value;
        }

        public static bool IsTest => Backend is TestBackend;
        public static bool Ready => Backend != null && Backend.Ready;
        public static event Action Changed;
        internal static void NotifyChanged() => Changed?.Invoke();

        /// <summary>Picks and starts the store backend (boot).</summary>
        public static void Init()
        {
#if SC_RC
            try { if (RevenueCatBackend.Init()) return; } catch (Exception e) { Analytics.Exception(e); }
#endif
        }

        public static readonly List<Product> Gems = new List<Product>
        {
            new Product { id = Prefix + "gems_80", title = "AVUÇ DOLUSU", gems = 80, art = "shop_gems_1", tierPrice = "$0.99", usd = 0.99 },
            new Product { id = Prefix + "gems_500", title = "KESE", gems = 500, bonusPct = 25, art = "shop_gems_2", tierPrice = "$4.99", usd = 4.99, badge = "EN POPÜLER" },
            new Product { id = Prefix + "gems_1200", title = "SANDIK", gems = 1200, bonusPct = 50, art = "shop_gems_3", tierPrice = "$9.99", usd = 9.99 },
            new Product { id = Prefix + "gems_2800", title = "KASA", gems = 2800, bonusPct = 75, art = "shop_gems_4", tierPrice = "$19.99", usd = 19.99, badge = "EN İYİ DEĞER" },
            new Product { id = Prefix + "gems_7500", title = "HAZİNE", gems = 7500, bonusPct = 100, art = "shop_gems_5", tierPrice = "$49.99", usd = 49.99 },
        };

        public static readonly List<Product> Cash = new List<Product>
        {
            new Product { id = "cash_5k", title = "ZARF", cash = 5000, gemPrice = 50, art = "shop_cash_1" },
            new Product { id = "cash_15k", title = "ÇANTA", cash = 15000, gemPrice = 130, art = "shop_cash_2", badge = "%15 FAZLA" },
            new Product { id = "cash_40k", title = "VALİZ", cash = 40000, gemPrice = 300, art = "shop_cash_3", badge = "%33 FAZLA" },
            new Product { id = "cash_100k", title = "KASA", cash = 100000, gemPrice = 650, art = "shop_cash_4", badge = "%54 FAZLA" },
        };

        public static readonly Product Starter = new Product
        {
            id = Prefix + "starter_pack", title = "BAŞLANGIÇ PAKETİ", oneTime = true, gems = 500, cash = 10000, apRounds = 10,
            desc = "500 elmas, $10.000 ve 10 zırh delici mermi. Sadece bir kez.", art = "shop_starter", tierPrice = "$2.99", usd = 2.99, badge = "%80 İNDİRİM",
        };

        public static readonly Product NoAds = new Product
        {
            id = Prefix + "no_ads", title = "REKLAMSIZ", permanent = true, noAds = true, gems = 100,
            desc = "Görev aralarındaki reklamlar sonsuza dek kalkar. Ödüllü videolar istersen açık kalır. Hediye: 100 elmas.",
            art = "shop_noads", tierPrice = "$4.99", usd = 4.99,
        };

        public static readonly Product ShadowPack = new Product
        {
            id = Prefix + "pack_shadow", title = "GÖLGE OPERASYON", permanent = true, weapon = "sr7", gadget = "nv", upgradeTo = 3, gems = 150,
            desc = "Susturuculu GÖLGE SR-7 hemen senin: bütün hatları 3. kademede. Gece görüşü ve 150 elmas.",
            art = "shop_pack_shadow", tierPrice = "$4.99", usd = 4.99, badge = "EN ÇOK SATAN",
        };

        public static readonly Product TitanPack = new Product
        {
            id = Prefix + "pack_titan", title = "TİTAN PAKETİ", permanent = true, weapon = "t50", gadget = "thermal", upgradeTo = 2, gems = 400, apRounds = 20,
            desc = "Efsane TİTAN .50: zırhı ve camı deler. Termal dürbün, 400 elmas ve 20 zırh delici mermi.",
            art = "shop_pack_titan", tierPrice = "$14.99", usd = 14.99, badge = "EFSANE",
        };

        public static readonly List<Product> Bundles = new List<Product> { ShadowPack, TitanPack };
        public static readonly List<Product> Permanent = new List<Product> { NoAds, ShadowPack, TitanPack };

        public static IEnumerable<Product> AllRealMoney()
        {
            foreach (var p in Gems) yield return p;
            yield return Starter;
            foreach (var p in Permanent) yield return p;
        }

        public static Product Find(string id)
        {
            foreach (var p in AllRealMoney()) if (p.id == id) return p;
            foreach (var p in Cash) if (p.id == id) return p;
            return null;
        }

        public static string PriceOf(Product p)
        {
            if (p == null) return "";
            if (!p.RealMoney) return p.gemPrice.ToString();
            string s = Backend != null ? Backend.Price(p) : null;
            return string.IsNullOrEmpty(s) ? p.tierPrice : s;
        }

        // ------------------------------------------------------------------ granting
        /// <summary>How many of this consumable this install has paid out (the store backend compares it with the account's
        /// history to finish purchases that were interrupted).</summary>
        public static int PaidCount(string productId) => PlayerPrefs.GetInt("sc2_paid_" + productId, 0);
        public static void SetPaidCount(string productId, int n) { PlayerPrefs.SetInt("sc2_paid_" + productId, n); PlayerPrefs.Save(); }

        /// <summary>A purchase the store finished without a button press behind it (interrupted purchase, Ask to Buy approved
        /// later): consumables are paid out, a permanent item gets its one-time gift (the unlock comes with the entitlement).</summary>
        public static void GrantRecovered(Product p)
        {
            if (p == null) return;
            if (p.permanent)
            {
                if (Save.Flag("gift_" + p.id)) return;
                ApplyPermanent(p, true);
            }
            else
            {
                SetPaidCount(p.id, PaidCount(p.id) + 1);
                Grant(p);
            }
            Analytics.Event("iap_recovered", "product_id", Short(p));
            Analytics.Purchase(Short(p), p.usd, "USD", null);
            NotifyChanged();
        }

        /// <summary>The store says this one-time consumable (the starter pack) was bought on this account: never offered again.</summary>
        public static void MarkBought(Product p)
        {
            if (p != null && p.oneTime && !Save.Flag("iap_" + p.id)) { Save.SetFlag("iap_" + p.id); NotifyChanged(); }
        }

        /// <summary>The store's list of permanent items the player owns (every launch, after a purchase, after a restore):
        /// they are unlocked; their one-time gifts (diamonds, rounds) are NOT given here — a reinstall or a new device must not
        /// pay them again.</summary>
        public static int ApplyEntitlements(ICollection<string> ownedProductIds)
        {
            int n = 0;
            foreach (var p in Permanent)
            {
                if (!ownedProductIds.Contains(p.id)) continue;
                ApplyPermanent(p, false);
                n++;
            }
            if (n > 0) NotifyChanged();
            return n;
        }

        /// <summary>Unlocks what a permanent item gives; <paramref name="gift"/>: also its one-time gift (once ever).</summary>
        static void ApplyPermanent(Product p, bool gift)
        {
            if (p.noAds) Save.NoAds = true;
            if (!string.IsNullOrEmpty(p.weapon))
            {
                var w = Gear.Weapon(p.weapon);
                if (w != null)
                {
                    Save.SetOwned("w_" + w.id);
                    for (int s = 0; s < 5; s++) if (Save.WeaponUp(w.id, s) < p.upgradeTo) Save.SetWeaponUp(w.id, s, p.upgradeTo);
                }
            }
            if (!string.IsNullOrEmpty(p.gadget)) Save.SetOwned("g_" + p.gadget);
            Save.SetFlag("iap_" + p.id);
            if (gift && !Save.Flag("gift_" + p.id))
            {
                Save.SetFlag("gift_" + p.id);
                if (p.gems > 0) Progress.AddGems(p.gems, "iap_" + Short(p));
                if (p.cash > 0) Progress.AddCash(p.cash, "iap_" + Short(p));
                if (p.apRounds > 0) AddAp(p.apRounds);
            }
            Gear.Invalidate();
        }

        static void AddAp(int n) => Save.SetAmmoCount(AmmoKind.ArmorPiercing.ToString(), Save.AmmoCount(AmmoKind.ArmorPiercing.ToString()) + n);

        public static string Short(Product p) => p.id.StartsWith(Prefix) ? p.id.Substring(Prefix.Length) : p.id;

        /// <summary>Grants what a pack holds (after the store confirmed it, or when it was bought with diamonds).</summary>
        static void Grant(Product p)
        {
            if (p.permanent) { ApplyPermanent(p, true); return; }
            string src = p.RealMoney ? "iap_" + Short(p) : "gem_pack";
            if (p.gems > 0) Progress.AddGems(p.gems, src);
            if (p.cash > 0) Progress.AddCash(p.cash, src);
            if (p.apRounds > 0) AddAp(p.apRounds);
            if (p.noAds) Save.NoAds = true;
            if (p.oneTime) Save.SetFlag("iap_" + p.id);
        }

        /// <summary>Buys a pack. <paramref name="done"/>: success and, when it failed, a message for the player (null: the
        /// player cancelled).</summary>
        /// <summary>A store purchase or restore is in flight (a second tap must not start another one).</summary>
        public static bool Busy { get; private set; }

        public static void Buy(Product p, Action<bool, string> done, string placement = "shop")
        {
            if (p == null || p.Owned) { done?.Invoke(false, "Bu paket zaten alındı."); return; }
            if (Busy) return;
            if (!p.RealMoney)
            {
                if (!Progress.SpendGems(p.gemPrice, p.id)) { done?.Invoke(false, "Elmasın yetmiyor."); return; }
                Grant(p);
                Analytics.Event("cash_pack_buy", "pack", p.id, "gems", p.gemPrice);
                done?.Invoke(true, null);
                NotifyChanged();
                return;
            }
            var b = Backend;
            if (b == null || !b.Ready)
            {
                string why = b == null ? "no store backend" : null;
#if SC_RC
                if (b != null) { why = RevenueCatBackend.LastError ?? "loading"; (b as RevenueCatBackend)?.Reload(); }
#endif
                Debug.LogWarning("[SC] Satın alma açılamadı: " + why);
                Analytics.Event("iap_unavailable", "product_id", Short(p), "reason", why != null && why.Length > 90 ? why.Substring(0, 90) : why);
                done?.Invoke(false, "Mağaza şu an açık değil. İnternet bağlantını kontrol edip biraz sonra tekrar dene." + (string.IsNullOrEmpty(why) ? "" : "\n<size=22><color=#6B7682>(" + why + ")</color></size>"));
                return;
            }
            Debug.Log("[SC] Satın alma başlıyor: " + p.id);
            double price; string currency;
            if (!b.PriceValue(p, out price, out currency)) { price = p.usd; currency = "USD"; }
            Analytics.Event("iap_start", "product_id", Short(p), "placement", placement, "value", price, "currency", currency);
            Busy = true;
            b.Purchase(p, (ok, err, tx) =>
            {
                Busy = false;
                if (ok)
                {
                    if (!p.permanent) SetPaidCount(p.id, PaidCount(p.id) + 1);
                    Grant(p);
                    Analytics.Purchase(Short(p), price, currency, tx);
                    Analytics.RefreshUser();
                    NotifyChanged();
                }
                else if (err == null) Analytics.Event("iap_cancel", "product_id", Short(p), "placement", placement);
                else Analytics.Event("iap_fail", "product_id", Short(p), "placement", placement, "error", err);
                done?.Invoke(ok, ok ? null : err);
            });
        }

        public static void Restore(Action<int> done)
        {
            var b = Backend;
            if (b == null || Busy) { done?.Invoke(0); return; }
            Busy = true;
            b.Restore(n =>
            {
                Busy = false;
                Analytics.Event("iap_restore", "count", n);
                done?.Invoke(n);
            });
        }
    }
}

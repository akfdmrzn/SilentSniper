#if SC_RC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// App Store purchases through RevenueCat (StoreKit 2 underneath, receipts validated on RevenueCat's servers).
    /// Consumables: diamond packs and the starter pack. Non-consumables (no ads, rifle packs) are entitlements, so a new
    /// device gets them back with "restore" and they show up in every RevenueCat chart.
    /// The SDK component lives on its own object and is configured at runtime from <see cref="MonetizationConfig"/>.
    /// </summary>
    public class RevenueCatBackend : Purchases.UpdatedCustomerInfoListener, Store.IBackend
    {
        static RevenueCatBackend inst;
        Purchases purchases;
        readonly Dictionary<string, Purchases.StoreProduct> products = new Dictionary<string, Purchases.StoreProduct>();
        bool configured;
        float nextProductTry;
        int productTries;
        string pendingId;                                       // a purchase in flight: its history is not recovered
        /// <summary>Why the products are not there (shown with the "store is not open" message, sent to analytics).</summary>
        public static string LastError;

        /// <summary>Starts RevenueCat when a key is set (false: no key, the caller keeps its fallback).</summary>
        public static bool Init()
        {
            string key = Application.isEditor || MonetizationConfig.UseRevenueCatTestStore
                ? MonetizationConfig.RevenueCatTestKey : MonetizationConfig.RevenueCatAppleKey;
            if (string.IsNullOrEmpty(key)) return false;
            if (inst != null) return true;
            var go = new GameObject("RevenueCat");
            DontDestroyOnLoad(go);
            go.SetActive(false);                                 // set the fields before Purchases.Start runs
            var p = go.AddComponent<Purchases>();
            p.useRuntimeSetup = true;
            p.productIdentifiers = new string[0];
            inst = go.AddComponent<RevenueCatBackend>();
            inst.purchases = p;
            p.listener = inst;
            go.SetActive(true);
            inst.StartCoroutine(inst.Configure(key));
            Store.Backend = inst;
            return true;
        }

        System.Collections.IEnumerator Configure(string key)
        {
            yield return null;                                   // Purchases.Start has created its native wrapper
            try
            {
                var cfg = Purchases.PurchasesConfiguration.Builder.Init(key).Build();
                purchases.Configure(cfg);
                purchases.SetLogLevel(Purchases.LogLevel.Debug);
                // Apple Search Ads attribution and the ids the RevenueCat → Firebase / Meta integrations need
                purchases.EnableAdServicesAttributionTokenCollection();
                purchases.CollectDeviceIdentifiers();
                configured = true;
                LinkAnalytics();
                Analytics.Ready += LinkAnalytics;
                LoadProducts();
                purchases.GetCustomerInfo((info, err) => { if (err == null) CustomerInfoReceived(info); });
            }
            catch (Exception e) { Analytics.Exception(e); }
        }

        void LinkAnalytics()
        {
            if (!configured) return;
            try
            {
                if (!string.IsNullOrEmpty(Analytics.FirebaseInstanceId)) purchases.SetFirebaseAppInstanceID(Analytics.FirebaseInstanceId);
                purchases.SetAttributes(new Dictionary<string, string>
                {
                    { "player_level", Progress.Level.ToString() },
                    { "contracts_done", Progress.CompletedCount.ToString() },
                });
            }
            catch (Exception e) { Analytics.Exception(e); }
        }

        void LoadProducts()
        {
            var ids = new List<string>();
            foreach (var p in Store.AllRealMoney()) ids.Add(p.id);
            productTries++;
            purchases.GetProducts(ids.ToArray(), (list, err) =>
            {
                if (err != null || list == null || list.Count == 0)
                {
                    LastError = err != null ? (err.ReadableErrorCode + ": " + err.Message + (string.IsNullOrEmpty(err.UnderlyingErrorMessage) ? "" : " / " + err.UnderlyingErrorMessage))
                                            : "App Store returned no products";
                    Debug.LogWarning("[SC] RevenueCat ürünleri alınamadı (" + productTries + ". deneme): " + LastError);
                    Analytics.Event("iap_products_fail", "error", LastError.Length > 90 ? LastError.Substring(0, 90) : LastError, "try", productTries);
                    nextProductTry = Time.unscaledTime + Mathf.Min(120f, 10f * productTries);
                    return;
                }
                foreach (var sp in list) products[sp.Identifier] = sp;
                LastError = null;
                Debug.Log("[SC] RevenueCat: " + products.Count + " ürün (" + string.Join(", ", products.Keys) + ")");
                Store.NotifyChanged();
            }, "inapp");
        }

        void Update()
        {
            if (configured && products.Count == 0 && nextProductTry > 0f && Time.unscaledTime > nextProductTry)
            {
                nextProductTry = 0f;
                LoadProducts();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused && configured && products.Count == 0) LoadProducts();
        }

        // ------------------------------------------------------------------ IBackend
        public bool Ready => configured && products.Count > 0;

        /// <summary>Asks the App Store again (a tap on a price while the products are missing).</summary>
        public void Reload() { if (configured && products.Count == 0) LoadProducts(); }

        public string Price(Product p) => products.TryGetValue(p.id, out var sp) ? sp.PriceString : null;

        public bool PriceValue(Product p, out double value, out string currency)
        {
            if (products.TryGetValue(p.id, out var sp)) { value = sp.Price; currency = sp.CurrencyCode; return true; }
            value = p.usd; currency = "USD";
            return false;
        }

        public void Purchase(Product p, Action<bool, string, string> done)
        {
            if (!configured) { done(false, "Mağaza açılamadı. İnternet bağlantını kontrol et.", null); return; }
            pendingId = p.id;
            long now = DateTime.UtcNow.Ticks;
            if (firstStartTicks == 0) firstStartTicks = now;
            Debug.Log("[SC] RevenueCat satın alma: " + p.id);
            bool answered = false, timedOut = false;
            StartCoroutine(Watchdog(90f, () => answered, () =>
            {
                timedOut = true;
                Debug.LogWarning("[SC] RevenueCat: " + p.id + " için 90 sn içinde yanıt gelmedi");
                Analytics.Event("iap_timeout", "product_id", p.id);
                done(false, "App Store ödeme penceresi açılamadı. Ayarlar → Geliştirici → Sandbox hesabını kontrol edip tekrar dene.", null);
            }));
            purchases.PurchaseProduct(p.id, r =>
            {
                answered = true;
                Debug.Log("[SC] RevenueCat yanıtı: " + p.id + " iptal=" + r.UserCancelled + " hata=" + (r.Error != null ? r.Error.ToString() : "yok"));
                if (timedOut && (r.UserCancelled || r.Error != null)) return;   // already told the player
                pendingId = null;
                if (r.UserCancelled) { done(false, null, null); return; }
                if (r.Error != null)
                {
                    // "already owned" on a permanent item: give it back instead of an error
                    if (p.permanent && r.Error.ReadableErrorCode == "PRODUCT_ALREADY_PURCHASED")
                    {
                        purchases.RestorePurchases((info, e2) => { if (e2 == null && info != null) Apply(info, true); done(false, "Bu paket zaten senin: geri yüklendi.", null); });
                        return;
                    }
                    if (r.Error.ReadableErrorCode == "PAYMENT_PENDING_ERROR") { done(false, "Satın alma onay bekliyor (Satın Alma İzni). Onaylanınca paket kendiliğinden gelir.", null); return; }
                    done(false, "Satın alma tamamlanmadı. (" + r.Error.Message + ")", null);
                    return;
                }
                string tx = r.StoreTransaction != null ? r.StoreTransaction.TransactionIdentifier : null;
                done(true, null, tx);
                if (r.CustomerInfo != null) CustomerInfoReceived(r.CustomerInfo);
            }, "inapp");
        }

        System.Collections.IEnumerator Watchdog(float secs, Func<bool> answered, Action onTimeout)
        {
            float end = Time.realtimeSinceStartup + secs;
            while (Time.realtimeSinceStartup < end) { if (answered()) yield break; yield return null; }
            if (!answered()) onTimeout();
        }

        public void Restore(Action<int> done)
        {
            if (!configured) { done(0); return; }
            purchases.RestorePurchases((info, err) =>
            {
                if (err != null || info == null) { done(0); return; }
                int n = 0;
                try { n = Apply(info, true); } catch (Exception e) { Analytics.Exception(e); }
                done(n);
            });
        }

        // ------------------------------------------------------------------ customer info (every launch, purchase, restore)
        public override void CustomerInfoReceived(Purchases.CustomerInfo info)
        {
            if (info == null) return;
            try { Apply(info, false); }
            catch (Exception e) { Analytics.Exception(e); }
        }

        long firstStartTicks;                                   // the first purchase started in this session (UTC ticks)
        const string BaseKey = "sc2_rc_base";                   // survives a progress reset, not a reinstall

        static long Utc(DateTime d) => (d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : d).Ticks;

        /// <summary>
        /// Applies the account's purchases. Permanent items are unlocked (never their gifts). Purchases are only paid out
        /// when they are newer than the moment this install first saw the account (the "baseline"): old purchases that a
        /// restore or a reinstall brings back are history, not new packs. Of the newer ones, whatever the game has not paid
        /// yet (the app closed mid-purchase, Ask to Buy approved later) is paid now. A restore never pays consumables.
        /// </summary>
        int Apply(Purchases.CustomerInfo info, bool restore)
        {
            var owned = new HashSet<string>();
            if (info.AllPurchasedProductIdentifiers != null) foreach (var id in info.AllPurchasedProductIdentifiers) owned.Add(id);
            if (info.Entitlements != null && info.Entitlements.Active != null)
                foreach (var e in info.Entitlements.Active.Values) if (e != null && e.IsActive) owned.Add(e.ProductIdentifier);
            int n = Store.ApplyEntitlements(owned);
            if (owned.Contains(Store.Starter.id)) Store.MarkBought(Store.Starter);
            if (info.NonSubscriptionTransactions == null) return n;

            long baseTicks = 0;
            long.TryParse(PlayerPrefs.GetString(BaseKey, "0"), out baseTicks);
            if (baseTicks == 0)
            {
                long now = DateTime.UtcNow.Ticks;
                baseTicks = firstStartTicks > 0 && firstStartTicks < now ? firstStartTicks - TimeSpan.TicksPerSecond * 5 : now;
                PlayerPrefs.SetString(BaseKey, baseTicks.ToString());
                PlayerPrefs.Save();
            }
            var count = new Dictionary<string, int>();
            foreach (var t in info.NonSubscriptionTransactions)
            {
                if (t == null || string.IsNullOrEmpty(t.ProductIdentifier)) continue;
                if (t.ProductIdentifier == Store.Starter.id) Store.MarkBought(Store.Starter);
                if (Utc(t.PurchaseDate) <= baseTicks) continue;
                count[t.ProductIdentifier] = (count.TryGetValue(t.ProductIdentifier, out int c) ? c : 0) + 1;
            }
            if (restore) return n;
            foreach (var kv in count)
            {
                if (kv.Key == pendingId) continue;              // its own callback pays it
                var prod = Store.Find(kv.Key);
                if (prod == null) continue;
                if (prod.permanent) { Store.GrantRecovered(prod); continue; }   // only its gift, once
                for (int i = Store.PaidCount(kv.Key); i < kv.Value; i++) Store.GrantRecovered(prod);
            }
            return n;
        }
    }
}
#endif

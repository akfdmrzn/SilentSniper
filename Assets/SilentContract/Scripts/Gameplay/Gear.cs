using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    public enum AmmoKind { Standard, ArmorPiercing, Tranquilizer, Explosive }
    public enum VisionMode { Normal, Night, Thermal }

    /// <summary>A rifle: ballistics, handling, magazine, scope range, how loud it is, what it punches through.</summary>
    public class WeaponDef
    {
        public string id, name, caliber, desc, model;
        public int price;                 // 0 = owned from the start
        public int unlockStars;           // contract stars needed before it can be bought
        public int magazine = 5;
        public float cycle = 1.15f;       // seconds between shots (bolt throw / semi-auto recovery)
        public float reload = 2.0f;
        public float velocity = 830f;     // muzzle velocity m/s
        public float zoomMin = 3f, zoomMax = 12f;
        public float sway = 1f;           // scope sway multiplier
        public float recoil = 1f;
        public bool silenced;
        public bool semiAuto;
        public int pierce;                // 0: glass only, 1: thin cover (wood, sheet metal, car doors), 2: walls, engine blocks
        public float power = 1f;          // >= 2: any hit is lethal, engines die in one shot
        // first person view model
        public float viewYaw = 90f, viewScale = 0.95f;
        public Vector3 viewOffset;
        public float shotPitch = 1f, shotVolume = 1f;
        public float report = 9f;         // metres around the impact where people hear the shot (0 = suppressed)
        public string reticle = "scope_reticle";
        // 0..1 bars in the armory
        public float barPower, barRange, barStability, barHandling, barStealth;

        public WeaponDef Clone() => (WeaponDef)MemberwiseClone();
    }

    public class GadgetDef { public string id, name, desc, icon; public int price, unlockStars; }

    public class AmmoDef
    {
        public AmmoKind kind;
        public string id, name, desc, icon;
        public int price, pack;           // price per pack of rounds
        public Color tint;
    }

    /// <summary>The armory: everything the player can own, equip and carry into a contract.</summary>
    public static class Gear
    {
        public static readonly List<WeaponDef> Weapons = new List<WeaponDef>
        {
            new WeaponDef
            {
                id = "k98", name = "KARTAL K-98", caliber = "7.62×51", model = "ai_rifle_k98",
                desc = "Güvenilir sürgülü tüfek. Her suikastçının ilk ortağı.",
                magazine = 5, cycle = 1.15f, reload = 2.0f, velocity = 830f, zoomMin = 3f, zoomMax = 12f, sway = 1f, recoil = 1f,
                viewYaw = 90f, viewScale = 0.95f, report = 9f,
                barPower = 0.45f, barRange = 0.45f, barStability = 0.45f, barHandling = 0.6f, barStealth = 0.2f,
            },
            new WeaponDef
            {
                id = "sr7", name = "GÖLGE SR-7", caliber = "7.62×51 susturuculu", model = "ai_rifle_tactical", price = 13000, unlockStars = 3,
                desc = "Entegre susturucu: atış sesi duyulmaz, mermi daha yavaş gider. Gizli işler için.",
                magazine = 7, cycle = 1.0f, reload = 1.8f, velocity = 700f, zoomMin = 3f, zoomMax = 14f, sway = 0.85f, recoil = 0.7f,
                silenced = true, shotPitch = 1.4f, shotVolume = 0.45f, report = 0f, reticle = "scope_reticle_tactical",
                viewYaw = 90f, viewScale = 0.95f,
                barPower = 0.4f, barRange = 0.4f, barStability = 0.6f, barHandling = 0.75f, barStealth = 1f,
            },
            new WeaponDef
            {
                id = "dmr", name = "FIRTINA DMR", caliber = "7.62×51 yarı otomatik", model = "ai_rifle_dmr", price = 19000, unlockStars = 8,
                desc = "Yarı otomatik: sürgü çekmeden peş peşe ateş. Kaçan ve kalabalık hedefler için.",
                magazine = 10, cycle = 0.32f, reload = 2.3f, velocity = 800f, zoomMin = 2.5f, zoomMax = 10f, sway = 1.05f, recoil = 0.85f,
                semiAuto = true, shotPitch = 1.08f, report = 11f, reticle = "scope_reticle_tactical",
                viewYaw = 90f, viewScale = 0.95f,
                barPower = 0.4f, barRange = 0.35f, barStability = 0.4f, barHandling = 1f, barStealth = 0.15f,
            },
            new WeaponDef
            {
                id = "lr338", name = "ATMACA .338 LR", caliber = ".338 Lapua Magnum", model = "ai_rifle_lr", price = 32000, unlockStars = 14,
                desc = "Uzun menzil: yüksek namlu çıkış hızı, az düşüş, 20× dürbün. İnce siperleri deler.",
                magazine = 5, cycle = 1.3f, reload = 2.4f, velocity = 940f, zoomMin = 4f, zoomMax = 20f, sway = 0.7f, recoil = 1.2f,
                pierce = 1, power = 1.4f, shotPitch = 0.9f, shotVolume = 1.1f, report = 13f, reticle = "scope_reticle_bdc",
                viewYaw = 90f, viewScale = 0.95f,
                barPower = 0.7f, barRange = 1f, barStability = 0.85f, barHandling = 0.4f, barStealth = 0.1f,
            },
            new WeaponDef
            {
                id = "t50", name = "TİTAN .50", caliber = ".50 BMG", model = "ai_rifle_50", price = 52000, unlockStars = 20,
                desc = "Anti-materyel tüfek: duvar, araç kapısı ve motor bloğu deler. Her isabet öldürür.",
                magazine = 5, cycle = 0.9f, reload = 2.9f, velocity = 880f, zoomMin = 4f, zoomMax = 16f, sway = 1.15f, recoil = 2.3f,
                semiAuto = true, pierce = 2, power = 2.5f, shotPitch = 0.72f, shotVolume = 1.25f, report = 26f, reticle = "scope_reticle_bdc",
                viewYaw = 90f, viewScale = 0.95f,
                barPower = 1f, barRange = 0.85f, barStability = 0.35f, barHandling = 0.25f, barStealth = 0f,
            },
        };

        public static readonly List<GadgetDef> Gadgets = new List<GadgetDef>
        {
            new GadgetDef { id = "nv", name = "GECE GÖRÜŞÜ", icon = "nightvision", price = 6000, unlockStars = 4,
                            desc = "Dürbüne takılır: karanlıkta her şeyi yeşil ışıkta görürsün." },
            new GadgetDef { id = "thermal", name = "TERMAL DÜRBÜN", icon = "thermal", price = 16500, unlockStars = 12,
                            desc = "İnsan vücudu beyaz parlar: sis, karanlık ve kalabalıkta hedefi bul." },
            new GadgetDef { id = "drone", name = "KEŞİF DRONU", icon = "drone", price = 11000, unlockStars = 9,
                            desc = "Görev başına bir kez: bölgedeki herkesi 15 saniye işaretler ve kimliklerini tarar." },
        };

        public static readonly List<AmmoDef> Ammo = new List<AmmoDef>
        {
            new AmmoDef { kind = AmmoKind.Standard, id = "std", name = "STANDART", icon = "bullet", desc = "Sınırsız.", tint = Color.white },
            new AmmoDef { kind = AmmoKind.ArmorPiercing, id = "ap", name = "ZIRH DELİCİ", icon = "pierce", price = 1000, pack = 5,
                          desc = "İnce siperleri, araç kapılarını ve camları delip geçer.", tint = new Color(0.4f, 0.8f, 1f) },
            new AmmoDef { kind = AmmoKind.Tranquilizer, id = "tranq", name = "UYUŞTURUCU İĞNE", icon = "dart", price = 900, pack = 5,
                          desc = "Öldürmez, bayıltır. Sessizdir ama yavaştır: çok düşer.", tint = new Color(0.55f, 1f, 0.55f) },
            new AmmoDef { kind = AmmoKind.Explosive, id = "exp", name = "PATLAYICI", icon = "explosive", price = 1800, pack = 3,
                          desc = "Çarptığı yerde patlar: araçları ve jeneratörleri tek atışta durdurur.", tint = new Color(1f, 0.5f, 0.25f) },
        };

        public static WeaponDef Weapon(string id)
        {
            foreach (var w in Weapons) if (w.id == id) return w;
            return null;
        }

        public static GadgetDef Gadget(string id)
        {
            foreach (var g in Gadgets) if (g.id == id) return g;
            return null;
        }

        public static AmmoDef AmmoOf(AmmoKind k)
        {
            foreach (var a in Ammo) if (a.kind == k) return a;
            return Ammo[0];
        }

        public static WeaponDef Equipped
        {
            get
            {
                var w = Weapon(Save.EquippedWeapon);
                return w != null && Owns(w) ? w : Weapons[0];
            }
        }

        // ------------------------------------------------------------------ contract loans (the handler provides gear)
        /// <summary>Rifle the current contract hands out (null = the player's own).</summary>
        public static WeaponDef LoanWeapon;
        static readonly HashSet<string> loanGadgets = new HashSet<string>();
        static readonly Dictionary<AmmoKind, int> loanAmmo = new Dictionary<AmmoKind, int>();

        /// <summary>The rifle carried into the contract: a loaned one, otherwise the equipped one.</summary>
        public static WeaponDef Carried => LoanWeapon ?? Equipped;

        /// <summary>Sets the contract's loans: "nv", "thermal", "drone", "w:t50", "tranq:4", "ap:5", "exp:2".</summary>
        public static void SetLoans(IEnumerable<string> items)
        {
            LoanWeapon = null;
            loanGadgets.Clear();
            loanAmmo.Clear();
            if (items == null) return;
            foreach (var raw in items)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                var parts = raw.Split(':');
                if (parts[0] == "w" && parts.Length > 1) { LoanWeapon = Weapon(parts[1]); continue; }
                var a = AmmoById(parts[0]);
                if (a != null && a.kind != AmmoKind.Standard)
                {
                    int n = 3;
                    if (parts.Length > 1) int.TryParse(parts[1], out n);
                    loanAmmo[a.kind] = (loanAmmo.TryGetValue(a.kind, out int have) ? have : 0) + Mathf.Max(0, n);
                    continue;
                }
                if (Gadget(parts[0]) != null) loanGadgets.Add(parts[0]);
            }
        }

        public static bool IsLoaned(string gadgetId) => loanGadgets.Contains(gadgetId);
        public static int LoanedAmmo(AmmoKind k) => loanAmmo.TryGetValue(k, out int n) ? n : 0;

        public static AmmoDef AmmoById(string id)
        {
            foreach (var a in Ammo) if (a.id == id) return a;
            return null;
        }

        public static bool Owns(WeaponDef w) => w != null && (w.price <= 0 || Save.Owns("w_" + w.id));
        public static bool Owns(GadgetDef g) => g != null && Save.Owns("g_" + g.id);
        /// <summary>Owned or handed out for this contract.</summary>
        public static bool HasGadget(string id) => Owns(Gadget(id)) || loanGadgets.Contains(id);
        public static bool Unlocked(int stars) => Save.TotalStars >= stars;

        /// <summary>Rounds the player owns (standard rounds are unlimited).</summary>
        public static int AmmoCount(AmmoKind k) => k == AmmoKind.Standard ? int.MaxValue : Save.AmmoCount(k.ToString());

        /// <summary>Rounds available in this contract: loaned + owned.</summary>
        public static int AmmoAvailable(AmmoKind k) => k == AmmoKind.Standard ? int.MaxValue : LoanedAmmo(k) + Save.AmmoCount(k.ToString());

        /// <summary>Uses one special round: loaned rounds first, then the player's own.</summary>
        public static void ConsumeAmmo(AmmoKind k)
        {
            if (k == AmmoKind.Standard) return;
            if (loanAmmo.TryGetValue(k, out int n) && n > 0) { loanAmmo[k] = n - 1; return; }
            Save.SetAmmoCount(k.ToString(), Save.AmmoCount(k.ToString()) - 1);
        }

        public static bool Buy(WeaponDef w)
        {
            if (w == null || Owns(w) || !Unlocked(w.unlockStars) || Save.Cash < w.price) return false;
            Progress.SpendCash(w.price, "rifle_" + w.id);
            Save.SetOwned("w_" + w.id);
            Analytics.Event("weapon_buy", "weapon", w.id, "currency", "cash", "price", w.price);
            return true;
        }

        public static bool Buy(GadgetDef g)
        {
            if (g == null || Owns(g) || !Unlocked(g.unlockStars) || Save.Cash < g.price) return false;
            Progress.SpendCash(g.price, "gadget_" + g.id);
            Save.SetOwned("g_" + g.id);
            Analytics.Event("item_buy", "item", g.id, "currency", "cash", "price", g.price);
            return true;
        }

        public static bool Buy(AmmoDef a)
        {
            if (a == null || a.kind == AmmoKind.Standard || Save.Cash < a.price) return false;
            Progress.SpendCash(a.price, "ammo_" + a.id);
            Analytics.Event("item_buy", "item", "ammo_" + a.id, "currency", "cash", "price", a.price);
            Save.SetAmmoCount(a.kind.ToString(), Save.AmmoCount(a.kind.ToString()) + a.pack);
            return true;
        }

        public static void Equip(WeaponDef w)
        {
            if (Owns(w)) Save.EquippedWeapon = w.id;
        }

        // ------------------------------------------------------------------ rifle upgrades (5 lines × 5 levels)
        public const int MaxUp = 5;
        static readonly string[] StatKeys = { "damage", "scope", "stability", "magazine", "speed" };
        public static readonly string[] UpNames = { "HASAR", "DÜRBÜN", "DENGE", "ŞARJÖR", "HIZ" };
        public static readonly string[] UpIcons = { "bullet", "zoom_in", "crosshair", "ammo", "reload" };
        public static readonly string[] UpHelp =
        {
            "Daha ağır mermi: zırhlı gövdeyi deler, kurşun daha hızlı ve düz gider.",
            "Dürbünün en büyük yakınlaştırması artar.",
            "Dürbündeki sallantı ve geri tepme azalır.",
            "Şarjöre daha çok fişek sığar.",
            "Sürgü ve şarjör değişimi hızlanır.",
        };

        /// <summary>Rifle tier 0..4 (order of the armory list): sets prices and the power rating.</summary>
        public static int Tier(WeaponDef w) => Mathf.Max(0, Weapons.IndexOf(w));

        static readonly string[] RarityNames = { "YAYGIN", "NADİR", "NADİR", "EPİK", "EFSANE" };
        public static string Rarity(WeaponDef w) => RarityNames[Mathf.Clamp(Tier(w), 0, 4)];

        public static Color RarityColor(WeaponDef w)
        {
            switch (Tier(w))
            {
                case 0: return Util.Hex("#9AA6B2");
                case 1: case 2: return Util.Hex("#46C8F2");
                case 3: return Util.Hex("#B07CFF");
                default: return Util.Hex("#FFB21E");
            }
        }

        public static int UpLevel(WeaponDef w, int stat) => w == null ? 0 : Save.WeaponUp(w.id, stat);

        public static int UpTotal(WeaponDef w)
        {
            int s = 0;
            for (int i = 0; i < 5; i++) s += UpLevel(w, i);
            return s;
        }

        /// <summary>Price of taking <paramref name="stat"/> to <paramref name="toLevel"/>: cash for levels 1-4, diamonds for 5.</summary>
        public static int UpCost(WeaponDef w, int stat, int toLevel, out bool gems)
        {
            int tier = Tier(w);
            gems = toLevel >= MaxUp;
            if (gems) return 20 + 10 * tier;
            float[] k = { 1f, 1.9f, 3.2f, 5f };
            // economy (tools/econ/sim2.py): upgrades are the main cash sink. A player who only plays each contract once and
            // watches no videos ends the campaign at ~80% of the recommended power; videos, the daily rewards and a few
            // replays keep him at ~100%; diamonds (the last tier, cash packs, rifle packs) put him ahead.
            float c = (1090f + 990f * tier) * k[Mathf.Clamp(toLevel - 1, 0, 3)] * (stat == 0 ? 1.15f : 1f);
            return Mathf.RoundToInt(c / 50f) * 50;
        }

        /// <summary>The diamond tier (level 5 of a line) opens at player level 5.</summary>
        public const int GemTierLevel = 5;

        public static bool GemTierLocked(WeaponDef w, int stat) => UpLevel(w, stat) == MaxUp - 1 && Progress.Level < GemTierLevel;

        public static bool CanUpgrade(WeaponDef w, int stat)
        {
            int lvl = UpLevel(w, stat);
            if (w == null || !Owns(w) || lvl >= MaxUp) return false;
            int cost = UpCost(w, stat, lvl + 1, out bool gems);
            if (gems && Progress.Level < GemTierLevel) return false;
            return gems ? Save.Gems >= cost : Save.Cash >= cost;
        }

        public static bool BuyUpgrade(WeaponDef w, int stat)
        {
            if (!CanUpgrade(w, stat)) return false;
            int lvl = UpLevel(w, stat);
            int cost = UpCost(w, stat, lvl + 1, out bool gems);
            string item = "upgrade_" + w.id + "_" + stat;
            if (gems ? !Progress.SpendGems(cost, item) : !Progress.SpendCash(cost, item)) return false;
            Save.SetWeaponUp(w.id, stat, lvl + 1);
            Analytics.Event("weapon_upgrade", "weapon", w.id, "stat", StatKeys[stat], "level", lvl + 1, "currency", gems ? "gems" : "cash", "price", cost, "power", Power(w));
            effective.Remove(w.id);
            return true;
        }

        static readonly Dictionary<string, KeyValuePair<int, WeaponDef>> effective = new Dictionary<string, KeyValuePair<int, WeaponDef>>();

        /// <summary>Forget the cached upgraded rifles (upgrade levels changed outside the armory: a store pack).</summary>
        public static void Invalidate() => effective.Clear();

        /// <summary>The rifle with its upgrades applied (a cached copy; the catalogue entry is never changed).</summary>
        public static WeaponDef Effective(WeaponDef w)
        {
            if (w == null) return null;
            int sig = 0;
            for (int i = 0; i < 5; i++) sig = sig * 8 + UpLevel(w, i);
            if (effective.TryGetValue(w.id, out var kv) && kv.Key == sig) return kv.Value;
            var e = w.Clone();
            int dmg = UpLevel(w, 0), scope = UpLevel(w, 1), stab = UpLevel(w, 2), mag = UpLevel(w, 3), spd = UpLevel(w, 4);
            // anti-materiel rifles keep their own power; the others never reach the "every hit kills" class
            e.power = w.power >= 2f ? w.power : Mathf.Min(1.95f, w.power * (1f + 0.12f * dmg));
            e.velocity = w.velocity * (1f + 0.03f * dmg);
            e.zoomMax = w.zoomMax + 1.6f * scope;
            e.sway = w.sway * (1f - 0.07f * stab);
            e.recoil = w.recoil * (1f - 0.06f * stab);
            e.magazine = w.magazine + mag * Mathf.Max(1, Mathf.RoundToInt(w.magazine * 0.2f));
            e.reload = w.reload * (1f - 0.07f * spd);
            e.cycle = w.cycle * (1f - 0.06f * spd);
            effective[w.id] = new KeyValuePair<int, WeaponDef>(sig, e);
            return e;
        }

        static readonly int[] BaseRating = { 120, 165, 205, 270, 340 };
        static readonly int[] RatingStep = { 12, 14, 16, 18, 20 };

        /// <summary>The rifle's power rating (GÜÇ): what the briefing compares with a contract's recommendation.</summary>
        public static int Power(WeaponDef w)
        {
            if (w == null) return 0;
            int t = Mathf.Clamp(Tier(w), 0, 4);
            return BaseRating[t] + RatingStep[t] * UpTotal(w);
        }

        public static int MaxPower(WeaponDef w)
        {
            int t = Mathf.Clamp(Tier(w), 0, 4);
            return BaseRating[t] + RatingStep[t] * 5 * MaxUp;
        }

        /// <summary>Torso shots kill an armoured man with this power (or armour piercing rounds, or an anti-materiel rifle).</summary>
        public const float ArmorPower = 1.45f;

        static string F1(float v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

        /// <summary>What one upgrade line gives at <paramref name="level"/> (armory text).</summary>
        public static string UpValue(WeaponDef w, int stat, int level)
        {
            switch (stat)
            {
                case 0:
                {
                    float p = w.power >= 2f ? w.power : Mathf.Min(1.95f, w.power * (1f + 0.12f * level));
                    return Mathf.RoundToInt(p * 100f).ToString();
                }
                case 1: return F1(w.zoomMax + 1.6f * level) + "×";
                case 2: return level == 0 ? "%0" : "-%" + (7 * level);
                case 3: return (w.magazine + level * Mathf.Max(1, Mathf.RoundToInt(w.magazine * 0.2f))) + " fişek";
                default: return F1(w.reload * (1f - 0.07f * level)) + " sn";
            }
        }
    }
}

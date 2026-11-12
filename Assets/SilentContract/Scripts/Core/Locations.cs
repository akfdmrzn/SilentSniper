using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>A map of the campaign: its own level layout, nest, menu look and contracts.</summary>
    public class LocationDef
    {
        public string id;
        public string name;             // "LİMAN"
        public string operation;        // "LİMAN OPERASYONU" (menu / loading subtitle)
        public string place;            // "Konteyner limanı · Gün batımı"
        public string blurb;            // one line on the location card
        public string layout;           // Resources/SC/Level/<layout>.json
        public string icon;
        public string menuPreset = "sunset";
        public string loadingText = "Kuruluyor";
        /// <summary>Contract that opens this map (null = open from the start).</summary>
        public string unlockAfter;
        public string lockedText;
        // slow pan of the menu camera over the map
        public float menuYaw = -6f, menuSweep = 32f, menuPitch = -7.5f, menuPitchSweep = 1.5f;
    }

    public static class Locations
    {
        public static readonly List<LocationDef> All = new List<LocationDef>
        {
            new LocationDef
            {
                id = "harbor", name = "LİMAN", operation = "LİMAN OPERASYONU", place = "Konteyner limanı", icon = "anchor",
                blurb = "Kaçakçılar, silah tüccarları ve rüşvet ağı. Her şey limanda başladı.",
                layout = "harbor", menuPreset = "sunset", loadingText = "Liman kuruluyor",
                menuYaw = -6f, menuSweep = 32f, menuPitch = -7.5f,
            },
            new LocationDef
            {
                id = "city", name = "ŞEHİR MERKEZİ", operation = "ŞEHİR OPERASYONU", place = "Saat Kulesi Meydanı", icon = "city",
                blurb = "Örgütün parası ve koruyucuları şehirde. Opera, gökdelen, otel — ve deniz kıyısında bir meydan.",
                layout = "city", menuPreset = "night", loadingText = "Şehir kuruluyor",
                unlockAfter = "m6", lockedText = "Liman'da 6. görevi bitir",
                menuYaw = 4f, menuSweep = 44f, menuPitch = -9f, menuPitchSweep = 2f,
            },
            new LocationDef
            {
                id = "mountain", name = "KARTAL TEPE", operation = "DAĞ OPERASYONU", place = "Kartal Tepe Kayak Merkezi", icon = "mountain",
                blurb = "Örgütün konseyi karlı bir dağ otelinde toplanıyor. Teleferik, dağ evleri, donmuş göl — ve tipi.",
                layout = "mountain", menuPreset = "snow_sunset", loadingText = "Dağ kuruluyor",
                unlockAfter = "m16", lockedText = "Şehirde 16. görevi bitir",
                menuYaw = 2f, menuSweep = 46f, menuPitch = -11f, menuPitchSweep = 2.5f,
            },
        };

        public static LocationDef Get(string id)
        {
            foreach (var l in All) if (l.id == id) return l;
            return All[0];
        }

        public static bool IsOpen(LocationDef l) =>
            l != null && (Save.UnlockAll || string.IsNullOrEmpty(l.unlockAfter) || Save.Completed(l.unlockAfter));

        // ---- layout data without building the level (distances in the contract list, intro framing)
        class Info
        {
            public Vector3 player; public Dictionary<string, Vector3> markers = new Dictionary<string, Vector3>();
            public LayoutPlayer playerData; public string playerName; public LayoutNest[] nests;
        }
        static readonly Dictionary<string, Info> infos = new Dictionary<string, Info>();

        static Info Load(string locationId)
        {
            if (infos.TryGetValue(locationId, out var i)) return i;
            i = new Info();
            var ta = Res.Text("Level/" + Get(locationId).layout);
            if (ta != null)
            {
                try
                {
                    var f = JsonUtility.FromJson<LayoutFile>(ta.text);
                    if (f.player != null) i.player = new Vector3(f.player.x, f.player.y, f.player.z);
                    i.playerData = f.player; i.playerName = f.playerName; i.nests = f.nests;
                    if (f.markers != null) foreach (var m in f.markers) i.markers[m.n] = new Vector3(m.x, m.y, m.z);
                }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            infos[locationId] = i;
            return i;
        }

        /// <summary>V2: the places a contract can be shot from (the main nest first), without building its map.</summary>
        public static List<LayoutNest> NestsFor(MissionDef def)
        {
            if (def == null) return new List<LayoutNest>();
            if (Game.I != null && Game.I.LocationId == def.region && Level.I != null && Level.I.Data != null) return Level.I.NestsFor(def);
            var i = Load(def.region);
            Vector3? focus = !string.IsNullOrEmpty(def.focus) && i.markers.TryGetValue(def.focus, out var fp) ? fp : (Vector3?)null;
            return Level.BuildNests(i.playerData, i.playerName, i.nests, def, focus);
        }

        /// <summary>Distance from the nest of a location to one of its markers (0 when unknown).</summary>
        public static float Distance(string locationId, string marker)
        {
            if (string.IsNullOrEmpty(marker)) return 0f;
            // the loaded level is authoritative (and already parsed)
            if (Game.I != null && Game.I.LocationId == locationId && Level.I != null && Level.I.HasMarker(marker))
                return Vector3.Distance(Level.I.PlayerPosition, Level.I.Marker(marker));
            var i = Load(locationId);
            return i.markers.TryGetValue(marker, out var p) ? Vector3.Distance(i.player, p) : 0f;
        }
    }
}

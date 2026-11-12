using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// V3 (user: "every contract I look down from very high up — boring"): most contracts now start somewhere else than
    /// the map's high roof — down on the ground behind a car or a crate at the targets' own level, on a low roof or a
    /// container stack close in, or halfway up — found in the loaded level itself (its real colliders), so the spot
    /// stands on something, is not under a roof or inside a wall, and sees the contract's area. The pick is fixed per
    /// contract (the same place every time it is played); the high roof stays in the list as a place to move to. A
    /// contract whose script is built round the roof (lookouts facing it, a long shot, a window) keeps it.
    /// </summary>
    public static class NestScout
    {
        public enum Style { Keep, Ground, Low, Mid }

        /// <summary>Where each contract starts (absent: the roof).</summary>
        static readonly Dictionary<string, Style> Plan = new Dictionary<string, Style>
        {
            // harbour: 1 tutorial and 7 long shot on the roof, 9 lookout tower built round it
            { "m2", Style.Ground }, { "m3", Style.Low }, { "m4", Style.Mid }, { "m5", Style.Ground }, { "m6", Style.Low },
            { "m8", Style.Mid }, { "m10", Style.Ground }, { "m11", Style.Low }, { "m12", Style.Ground },
            // city: 13 office window, 16 counter-sniper, 19 helipad, 20 penthouse keep theirs
            { "m14", Style.Ground }, { "m15", Style.Low }, { "m17", Style.Ground }, { "m18", Style.Low },
            // mountain: 22 villa lookout and 25 avalanche keep theirs
            { "m21", Style.Mid }, { "m23", Style.Ground }, { "m24", Style.Low }, { "m26", Style.Ground }, { "m27", Style.Low },
            { "m28", Style.Mid },
        };

        public static Style StyleOf(MissionDef def) => def != null && Plan.TryGetValue(def.id, out var s) ? s : Style.Keep;

        static readonly Dictionary<string, LayoutNest> cache = new Dictionary<string, LayoutNest>();
        static readonly HashSet<string> failed = new HashSet<string>();
        static string cacheLevel;

        /// <summary>The contract's own starting place (null: keep the roof, or nothing good was found).</summary>
        public static LayoutNest For(Level level, MissionDef def, Vector3 focus, LayoutPlayer roof)
        {
            var style = StyleOf(def);
            if (style == Style.Keep || level == null || roof == null) return null;
            string lk = level.Data != null ? level.Data.name : "";
            if (cacheLevel != lk) { cache.Clear(); failed.Clear(); cacheLevel = lk; }
            if (cache.TryGetValue(def.id, out var hit)) return Copy(hit);
            if (failed.Contains(def.id)) return null;
            var n = Search(level, def, style, focus, roof);
            if (n == null) { failed.Add(def.id); Debug.Log("[SC] nest scout: nothing for " + def.id + " (" + style + "), the roof it is"); return null; }
            cache[def.id] = n;
            Debug.Log("[SC] nest scout " + def.id + " " + style + ": " + n.x.ToString("0.0") + "," + n.y.ToString("0.0") + "," + n.z.ToString("0.0") +
                      "  " + Mathf.RoundToInt(n.dist) + " m");
            return Copy(n);
        }

        static LayoutNest Copy(LayoutNest n) => new LayoutNest
        {
            id = n.id, name = n.name, desc = n.desc, kind = n.kind, x = n.x, y = n.y, z = n.z, yaw = n.yaw, pitch = n.pitch,
            yawMin = n.yawMin, yawMax = n.yawMax, pitchMin = n.pitchMin, pitchMax = n.pitchMax, dist = n.dist
        };

        struct Band { public float dMin, dMax, hMin, hMax, eye; }

        static Band BandOf(Style s)
        {
            switch (s)
            {
                case Style.Ground: return new Band { dMin = 45f, dMax = 115f, hMin = -1.6f, hMax = 1.8f, eye = 1.2f };
                case Style.Low: return new Band { dMin = 60f, dMax = 165f, hMin = 3f, hMax = 13f, eye = 1.45f };
                default: return new Band { dMin = 80f, dMax = 220f, hMin = 12f, hMax = 32f, eye = 1.5f };
            }
        }

        static LayoutNest Search(Level level, MissionDef def, Style style, Vector3 focus, LayoutPlayer roof)
        {
            var b = BandOf(style);
            int world = Layers.WorldMask;
            // towards the roof (the side the contract was staged for), widening to all round if need be
            var toRoof = new Vector3(roof.x - focus.x, 0f, roof.z - focus.z);
            float baseAng = toRoof.sqrMagnitude > 1f ? Mathf.Atan2(toRoof.x, toRoof.z) * Mathf.Rad2Deg : 0f;
            var rnd = new System.Random(def.id.GetHashCode() & 0x7fffffff);
            LayoutNest best = null;
            float bestScore = float.MinValue;
            var aims = new[] { focus + Vector3.up * 1.2f, focus + Vector3.up * 0.6f, focus + new Vector3(2.5f, 1.2f, 0f), focus + new Vector3(-2.5f, 1.2f, 0f),
                               focus + new Vector3(0f, 1.2f, 2.5f), focus + new Vector3(0f, 1.2f, -2.5f) };
            for (int i = 0; i < 900; i++)
            {
                float spread = i < 450 ? 70f : 150f;
                float ang = baseAng + ((float)rnd.NextDouble() * 2f - 1f) * spread;
                float d = Mathf.Lerp(b.dMin, b.dMax, (float)rnd.NextDouble());
                var p = focus + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * d;
                if (!Physics.Raycast(new Vector3(p.x, focus.y + 90f, p.z), Vector3.down, out var top, 200f, world, QueryTriggerInteraction.Ignore)) continue;
                if (top.normal.y < 0.85f) continue;
                float h = top.point.y - focus.y;
                if (h < b.hMin || h > b.hMax) continue;
                if (top.point.y < Level.WaterY + 0.5f) continue;
                if (top.collider.GetComponentInParent<PathMover>() != null || top.collider.GetComponentInParent<NPC>() != null) continue;
                var floor = top.point;
                // not where the contract's people stand or walk (a ground spot on a route would be walked into)
                if (style == Style.Ground && level.NearAuthored(floor, 7f)) continue;
                // flat for a man's width: not the top of a lamp, a parasol, a bollard
                bool flat = true;
                foreach (var o in new[] { new Vector3(0.7f, 0, 0), new Vector3(-0.7f, 0, 0), new Vector3(0, 0, 0.7f), new Vector3(0, 0, -0.7f) })
                {
                    if (!Physics.Raycast(floor + o + Vector3.up * 1.5f, Vector3.down, out var g, 3f, world, QueryTriggerInteraction.Ignore) || Mathf.Abs(g.point.y - floor.y) > 0.35f)
                    { flat = false; break; }
                }
                if (!flat) continue;
                // room to kneel or stand, nothing overhead
                if (Physics.CheckCapsule(floor + Vector3.up * 0.45f, floor + Vector3.up * 1.75f, 0.32f, world, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Raycast(floor + Vector3.up * 0.5f, Vector3.up, 6f, world, QueryTriggerInteraction.Ignore)) continue;
                var eye = floor + Vector3.up * b.eye;
                // the contract's area must be in sight (the spot itself and around it)
                int seen = 0;
                foreach (var a in aims) if (!Physics.Linecast(eye, a, world, QueryTriggerInteraction.Ignore)) seen++;
                if (seen < 4) continue;
                // cover: something between him and the targets that hides a crouched man (ground and low spots)
                var dir = (focus - floor); dir.y = 0f; dir.Normalize();
                bool cover = Physics.Linecast(focus + Vector3.up * 1.2f, floor + Vector3.up * 0.55f, out var ch, world, QueryTriggerInteraction.Ignore) &&
                             Vector3.Distance(ch.point, floor) < 3.5f;
                float score = seen * 2f + (cover ? 6f : 0f) - Mathf.Abs(d - (b.dMin + b.dMax) * 0.5f) * 0.03f - Mathf.Abs(Mathf.DeltaAngle(ang, baseAng)) * 0.02f;
                if (score <= bestScore) continue;
                bestScore = score;
                var look = focus + Vector3.up * 1.2f - eye;
                float yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
                best = new LayoutNest
                {
                    id = "scout", kind = style == Style.Ground ? (cover ? "ground_cover" : "ground") : "roof",
                    name = style == Style.Ground ? "Yer seviyesi — siper arkası" : (style == Style.Low ? "Alçak mevzi — yakın çatı" : "Orta yükseklik"),
                    desc = style == Style.Ground ? "Hedeflerle aynı seviyede ve yakın. Net atış, ama seni de çabuk görürler."
                         : (style == Style.Low ? "Birkaç kat yukarıda: yakın ve geniş açı." : "Sahaya yukarıdan ama yakından bakar."),
                    x = eye.x, y = eye.y, z = eye.z, yaw = yaw, pitch = pitch,
                    yawMin = -80f, yawMax = 80f, pitchMin = -45f, pitchMax = 15f, dist = Vector3.Distance(eye, focus)
                };
            }
            return best;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    public struct ShotResult
    {
        public bool hit;
        public Vector3 point;
        public Vector3 normal;
        public Collider collider;
        public float time;
        public float distance;
        public bool water;
        public int walls;           // thin surfaces the bullet went through before it stopped here
        public int glass;           // window panes it went through
        /// <summary>A hit on a person (prediction): the spot on his body as he stands now. He walks on while the bullet
        /// flies, so <see cref="point"/> is where that spot will be when it lands and this is where it is now (the
        /// scope's impact dot sits here: on the body, not in the empty air ahead of him).</summary>
        public Vector3 bodyPoint;
        /// <summary>How far the person moves before the bullet gets there (zero for anything else).</summary>
        public Vector3 lead;
        /// <summary>The scope's help pulled this shot onto the man it was about to graze (a hand's breadth at most): the
        /// round leaves the barrel along the corrected line, so it lands where the dot showed.</summary>
        public bool assisted;
        /// <summary>A miss close by someone the shot was meant for: him, and where the round passed the nearest part of
        /// his body as the shooter sees it (metres, x right, y up). The spotter's call after the shot.</summary>
        public NPC nearNpc;
        public Vector2 missBy;
        /// <summary>Range to <see cref="nearNpc"/> (metres).</summary>
        public float nearRange;
        /// <summary>The round would have met <see cref="nearNpc"/> but something in front of him stopped it (his cover).</summary>
        public bool nearBlocked;

        public BodyPart Part => collider != null ? collider.GetComponent<BodyPart>() : null;
        public NPC Npc { get { var p = Part; return p != null ? p.owner : null; } }
    }

    /// <summary>How the scope's help may treat a shot meant for someone: not at all, any part, any limb, or one limb.</summary>
    public enum AssistZone { None, Any, Limb, Arm, Leg }

    /// <summary>What a bullet carries: muzzle velocity, penetration budget, ammunition, stopping power.</summary>
    public struct ShotSpec
    {
        public float velocity;
        public int pierce;          // penetration budget (a thin wall costs 1, a heavy one 2; hollow volumes count twice)
        public float power;         // >= 2: any hit is lethal
        public AmmoKind ammo;

        public static ShotSpec Default => new ShotSpec { velocity = Ballistics.MuzzleVelocity, pierce = 0, power = 1f, ammo = AmmoKind.Standard };
    }

    public static class Ballistics
    {
        public const float MuzzleVelocity = 830f;
        public const float Gravity = 9.81f;
        public const float WindFactor = 1.2f;   // lateral acceleration per m/s of crosswind (gamey but readable)
        public const float ZeroRange = 100f;
        public const float MaxTime = 2.6f;
        /// <summary>Speed kept after a window pane.</summary>
        public const float GlassSlow = 0.93f;
        const float Step = 1f / 240f;

        public static Vector3 InitialVelocity(Vector3 aimDir, Vector3 right) => InitialVelocity(aimDir, right, MuzzleVelocity);

        /// <summary>The rifle is zeroed at 100 m: the bore is tilted up so the bullet crosses the line of sight there.</summary>
        public static Vector3 InitialVelocity(Vector3 aimDir, Vector3 right, float muzzleVelocity) => InitialVelocity(aimDir, right, muzzleVelocity, ZeroRange);

        /// <summary>Zeroed at <paramref name="zero"/> metres (the scope's elevation turret).</summary>
        public static Vector3 InitialVelocity(Vector3 aimDir, Vector3 right, float muzzleVelocity, float zero)
        {
            float v = Mathf.Max(50f, muzzleVelocity);
            zero = Mathf.Max(25f, zero);
            float t = zero / v;
            float drop = 0.5f * Gravity * t * t;
            float angle = Mathf.Atan2(drop, zero) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(-angle, right) * aimDir * v;
        }

        public static Vector3 Accel(Vector3 wind) => Vector3.down * Gravity + wind * WindFactor;

        // ------------------------------------------------------------------ penetration
        static readonly Dictionary<Collider, int> levelCache = new Dictionary<Collider, int>();

        /// <summary>
        /// How hard a surface is to shoot through: 1 thin (sheet metal, car bodies, wood, plastic), 2 heavy (machinery,
        /// building walls), 3 never (ground, quay, ship hulls, cranes, rock). Characters are handled separately.
        /// </summary>
        public static int SurfaceLevel(Collider c)
        {
            if (c == null) return 3;
            if (levelCache.TryGetValue(c, out int lv)) return lv;
            lv = 3;
            var pi = c.GetComponentInParent<PropInfo>();
            var id = pi != null ? pi.id : null;
            if (!string.IsNullOrEmpty(id))
            {
                if (id.StartsWith("sc_container") || id.StartsWith("ai_suv") || id.StartsWith("ai_car") || id.StartsWith("ph_covered_car") ||
                    id.Contains("crate") || id.Contains("pallet") || id.Contains("wooden_barrels") || id.StartsWith("sr_ibc") ||
                    id.StartsWith("sr_barrier") || id.StartsWith("ph_road_barrier") || id.StartsWith("ph_trash") || id.StartsWith("sc_guard_booth") ||
                    id.StartsWith("ph_aircon") || id.StartsWith("ph_utility_box") || id.StartsWith("sr_drum") || id.StartsWith("ph_barrel") ||
                    id.StartsWith("ph_toolbox") || id.StartsWith("ph_tyre") || id.StartsWith("ph_cement") || id.StartsWith("sc_watchtower") ||
                    id.StartsWith("ai_speedboat") || id.StartsWith("ph_bench") || id.StartsWith("ph_welding") || id.StartsWith("sc_city_prop") ||
                    // city centre: cars, tram, street furniture
                    id.StartsWith("ai_limo") || id.StartsWith("ai_lux_sedan") || id.StartsWith("ai_police_car") || id.StartsWith("ai_taxi") ||
                    id.StartsWith("sc_c_tram") || id.StartsWith("ai_kiosk") || id.StartsWith("ai_simit_cart") || id.StartsWith("ai_cafe_set") ||
                    id.StartsWith("ai_parasol") || id.StartsWith("ai_podium") || id.StartsWith("ai_speakers") || id.StartsWith("ai_press_cam") ||
                    id.StartsWith("ai_ad_column") || id.StartsWith("ai_office_desk") || id.StartsWith("ai_sofa") || id.StartsWith("ai_scooter") ||
                    id.StartsWith("sc_c_cafe") || id.StartsWith("sc_c_tramstop") || id.StartsWith("sc_c_stage"))
                    lv = 1;
                else if (id.StartsWith("sc_warehouse") || id.StartsWith("sc_office") || id.StartsWith("sr_forklift") || id.StartsWith("ph_generator") ||
                         id.StartsWith("ph_compressor") || id.StartsWith("ai_yacht") || id.StartsWith("ai_tugboat") || id.StartsWith("sc_bld") ||
                         id.StartsWith("ai_heli"))
                    lv = 2;
            }
            levelCache[c] = lv;
            return lv;
        }

        static readonly Dictionary<Collider, bool> hullCache = new Dictionary<Collider, bool>();

        /// <summary>V8: a closed body behind a concave collider (a car, a cart, a kiosk): two walls, like the box it had —
        /// the round pays for both and leaves through the far side (a back-facing wall would otherwise let it out free).</summary>
        static bool Hull(Collider c)
        {
            if (hullCache.TryGetValue(c, out bool v)) return v;
            if (hullCache.Count > 512) hullCache.Clear();
            var pi = c.GetComponentInParent<PropInfo>();
            var id = pi != null ? pi.id : null;
            v = !string.IsNullOrEmpty(id) &&
                (id.StartsWith("ai_suv") || id.StartsWith("ai_limo") || id.StartsWith("ai_lux_sedan") || id.StartsWith("ai_police_car") ||
                 id.StartsWith("ai_taxi") || id.StartsWith("ph_covered_car") || id.StartsWith("ai_heli") || id.StartsWith("ai_snowmobile") ||
                 id.StartsWith("sr_forklift") || id.StartsWith("ai_simit_cart") || id.StartsWith("ph_coffee_cart") || id.StartsWith("ai_kiosk") ||
                 id.StartsWith("ai_ad_column") || id.StartsWith("ai_statue") || id.StartsWith("ph_fire_hydrant") || id.StartsWith("ai_press_cam") ||
                 id.StartsWith("ai_scooter"));
            hullCache[c] = v;
            return v;
        }

        /// <summary>
        /// Called when a bullet meets world geometry. True when it goes through: the budget pays for the surface
        /// (a box volume such as a container or a car has a wall on both sides, so it costs twice), the bullet is moved
        /// to the far side and slowed down.
        /// </summary>
        public static bool TryPenetrate(RaycastHit h, Vector3 dir, ref int budget, ref Vector3 vel, out Vector3 exit)
        {
            exit = h.point;
            if (budget <= 0 || h.collider == null) return false;
            int lv = SurfaceLevel(h.collider);
            if (lv >= 3) return false;
            bool volume = h.collider is BoxCollider || h.collider is CapsuleCollider || h.collider is SphereCollider ||
                          (h.collider is MeshCollider mc && (mc.convex || Hull(h.collider)));
            int cost = volume ? lv * 2 : lv;
            if (cost > budget) return false;
            if (volume)
            {
                // find the far face: cast back towards the entry point from beyond the volume
                float probe = h.collider.bounds.size.magnitude + 0.5f;
                var back = new Ray(h.point + dir * probe, -dir);
                if (h.collider.Raycast(back, out var bh, probe)) exit = bh.point;
                else exit = h.point + dir * 0.05f;
            }
            budget -= cost;
            vel *= Mathf.Pow(0.82f, cost);
            exit += dir * 0.02f;
            return true;
        }

        /// <summary>Simulates the whole flight against the current world (aim assist / kill cam decision).</summary>
        public static ShotResult Predict(Vector3 origin, Vector3 vel, Vector3 wind, int mask) => Predict(origin, vel, wind, mask, 0);

        public static ShotResult Predict(Vector3 origin, Vector3 vel, Vector3 wind, int mask, int pierce)
        {
            var pos = origin;
            var acc = Accel(wind);
            float t = 0;
            int walls = 0, glass = 0;
            int guard = 0;
            while (t < MaxTime)
            {
                var next = pos + vel * Step + 0.5f * acc * Step * Step;
                var seg = next - pos;
                if (Physics.Raycast(pos, seg.normalized, out var h, seg.magnitude, mask, QueryTriggerInteraction.Ignore))
                {
                    var dir = seg.normalized;
                    var pane = GlassPane.Of(h.collider);
                    if (pane != null && !pane.Broken && glass < 4)
                    {
                        // a window: the bullet goes through (a little slower)
                        glass++;
                        vel *= GlassSlow;
                        pos = h.point + dir * 0.05f;
                        continue;
                    }
                    if (h.collider.GetComponent<BodyPart>() == null && guard++ < 6 && TryPenetrate(h, dir, ref pierce, ref vel, out var exit))
                    {
                        walls++;
                        pos = exit;
                        continue;
                    }
                    return new ShotResult { hit = true, point = h.point, normal = h.normal, collider = h.collider, time = t, distance = Vector3.Distance(origin, h.point), walls = walls, glass = glass };
                }
                if (next.y < Level.WaterY)
                {
                    var wp = Vector3.Lerp(pos, next, Mathf.InverseLerp(pos.y, next.y, Level.WaterY));
                    return new ShotResult { hit = true, water = true, point = wp, normal = Vector3.up, time = t, distance = Vector3.Distance(origin, wp), walls = walls, glass = glass };
                }
                vel += acc * Step;
                pos = next;
                t += Step;
            }
            return new ShotResult { hit = false, point = pos, time = t, walls = walls, glass = glass };
        }

        // ------------------------------------------------------------------ moving people
        static readonly List<NPC> near = new List<NPC>();
        static readonly List<float> nearReach = new List<float>();
        // vehicles carrying someone near the line of fire (a car, a gondola cabin, a sled): tested where they will be,
        // like the people in them
        static readonly List<Transform> vehicles = new List<Transform>();
        static readonly List<Vector3> vehicleVel = new List<Vector3>();
        static readonly HashSet<Collider> vehicleCols = new HashSet<Collider>();
        static readonly Dictionary<Transform, Collider[]> vehicleColCache = new Dictionary<Transform, Collider[]>();
        static readonly RaycastHit[] hitBuf = new RaycastHit[32];

        static Collider[] VehicleColliders(Transform v)
        {
            if (vehicleColCache.TryGetValue(v, out var c) && c != null) return c;
            if (vehicleColCache.Count > 64) vehicleColCache.Clear();
            var all = v.GetComponentsInChildren<Collider>(true);
            var list = new List<Collider>();
            foreach (var x in all) if (x != null && x.gameObject.layer != Layers.Body && !x.isTrigger) list.Add(x);
            c = list.ToArray();
            vehicleColCache[v] = c;
            return c;
        }

        /// <summary>Distance from <paramref name="q"/> to the segment a-b.</summary>
        static float SegmentDistance(Vector3 q, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / l2) : 0f;
            return (a + ab * t - q).magnitude;
        }

        /// <summary>
        /// The whole flight against the world as it will be: people keep walking (along their routes, round corners)
        /// or riding while the bullet is in the air, so a body — and the car or cabin carrying it — is tested where it
        /// will be at that moment of the flight. Aimed straight at a runner this says "miss, it lands behind him"; aimed
        /// the right amount ahead it says "hit" (with <see cref="ShotResult.bodyPoint"/> on him as he stands now). Used
        /// for the scope's impact dot and for the kill cam decision.
        /// </summary>
        public static ShotResult PredictMoving(Vector3 origin, Vector3 vel, Vector3 wind, int mask, int pierce)
        {
            // the hit zones follow the animation: bring them up to date with this frame's poses
            Physics.SyncTransforms();
            int world = mask & ~(1 << Layers.Body);
            var acc = Accel(wind);
            near.Clear();
            nearReach.Clear();
            vehicles.Clear();
            vehicleVel.Clear();
            vehicleCols.Clear();
            if ((mask & (1 << Layers.Body)) != 0)
            {
                // who can be anywhere near the line of fire by the time the bullet passes (cheap: once per prediction)
                float v = Mathf.Max(1f, vel.magnitude);
                var dir = vel / v;
                foreach (var n in NPC.All)
                {
                    if (n == null || n.IsHidden || n.Escaped) continue;
                    var c = n.transform.position + Vector3.up;
                    float s = Vector3.Dot(c - origin, dir);
                    if (s < -3f) continue;
                    float te = Mathf.Max(0f, s / v);
                    if (te > MaxTime) continue;
                    var on = origin + vel * te + 0.5f * acc * te * te;
                    float speed = n.Dead && !n.Riding ? 0f : Mathf.Max(n.Velocity.magnitude, n.WorldVelocity.magnitude);
                    float reach = speed * (te + 0.15f) * 1.25f + 2.6f;
                    if ((on - c).sqrMagnitude > reach * reach) continue;
                    near.Add(n);
                    nearReach.Add(reach);
                    // his vehicle moves with him
                    var veh = n.Riding ? n.transform.parent : null;
                    if (veh != null && !vehicles.Contains(veh) && n.WorldVelocity.sqrMagnitude > 0.04f)
                    {
                        vehicles.Add(veh);
                        vehicleVel.Add(n.WorldVelocity);
                        foreach (var vc in VehicleColliders(veh)) vehicleCols.Add(vc);
                    }
                }
            }
            var pos = origin;
            float t = 0;
            int walls = 0, glass = 0, guard = 0;
            while (t < MaxTime)
            {
                var next = pos + vel * Step + 0.5f * acc * Step * Step;
                var seg = next - pos;
                float segLen = seg.magnitude;
                if (segLen < 1e-6f) break;
                var sdir = seg / segLen;
                // the nearest body this step, each one where it will be at this moment of the flight
                float best = float.MaxValue;
                RaycastHit bh = default;
                var boff = Vector3.zero;
                for (int i = 0; i < near.Count; i++)
                {
                    var n = near[i];
                    if (n == null) continue;
                    var c = n.transform.position + Vector3.up;
                    float r = nearReach[i] + 1.5f + segLen;
                    if ((pos - c).sqrMagnitude > r * r) continue;
                    var off = n.PredictOffset(t);
                    // does this step's stretch of the flight pass close to where he will be?
                    if (SegmentDistance(c + off, pos, next) > 2.4f) continue;
                    var ray = new Ray(pos - off, sdir);
                    foreach (var col in n.HitColliders)
                    {
                        if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                        if (col.Raycast(ray, out var h, segLen) && h.distance < best)
                        {
                            best = h.distance;
                            bh = h;
                            boff = off;
                        }
                    }
                }
                // the world, without the moving vehicles (they are tested where they will be, below)
                bool wallHit = false;
                RaycastHit wh = default;
                float wd = float.MaxValue;
                if (vehicleCols.Count == 0)
                {
                    if (Physics.Raycast(pos, sdir, out var h0, segLen, world, QueryTriggerInteraction.Ignore)) { wd = h0.distance; wh = h0; wallHit = true; }
                }
                else
                {
                    int nh = Physics.RaycastNonAlloc(pos, sdir, hitBuf, segLen, world, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < nh; k++)
                    {
                        var hk = hitBuf[k];
                        if (hk.distance >= wd) continue;
                        if (vehicleCols.Contains(hk.collider)) continue;
                        wd = hk.distance;
                        wh = hk;
                        wallHit = true;
                    }
                }
                var woff = Vector3.zero;
                for (int i = 0; i < vehicles.Count; i++)
                {
                    var veh = vehicles[i];
                    if (veh == null) continue;
                    var off = vehicleVel[i] * t;
                    var ray = new Ray(pos - off, sdir);
                    foreach (var col in VehicleColliders(veh))
                    {
                        if (col == null || !col.enabled || !col.gameObject.activeInHierarchy || ((1 << col.gameObject.layer) & world) == 0) continue;
                        if (col.Raycast(ray, out var h, segLen) && h.distance < wd)
                        {
                            wd = h.distance;
                            wh = h;
                            woff = off;
                            wallHit = true;
                        }
                    }
                }
                if (best < float.MaxValue && (!wallHit || best <= wd))
                {
                    float tt = t + Step * (best / segLen);
                    var at = bh.point + boff;
                    return new ShotResult { hit = true, point = at, bodyPoint = bh.point, lead = boff, normal = bh.normal, collider = bh.collider,
                                            time = tt, distance = Vector3.Distance(origin, at), walls = walls, glass = glass };
                }
                if (wallHit)
                {
                    var wp = wh.point + woff;
                    var pane = GlassPane.Of(wh.collider);
                    if (pane != null && !pane.Broken && glass < 4)
                    {
                        glass++;
                        vel *= GlassSlow;
                        pos = wp + sdir * 0.05f;
                        continue;
                    }
                    if (guard++ < 6 && TryPenetrate(wh, sdir, ref pierce, ref vel, out var exit))
                    {
                        walls++;
                        pos = exit + woff;
                        continue;
                    }
                    return new ShotResult { hit = true, point = wp, bodyPoint = wh.point, normal = wh.normal, collider = wh.collider, time = t + Step * (wd / segLen),
                                            distance = Vector3.Distance(origin, wp), walls = walls, glass = glass };
                }
                if (next.y < Level.WaterY)
                {
                    var wp = Vector3.Lerp(pos, next, Mathf.InverseLerp(pos.y, next.y, Level.WaterY));
                    return new ShotResult { hit = true, water = true, point = wp, bodyPoint = wp, normal = Vector3.up, time = t, distance = Vector3.Distance(origin, wp), walls = walls, glass = glass };
                }
                vel += acc * Step;
                pos = next;
                t += Step;
            }
            return new ShotResult { hit = false, point = pos, bodyPoint = pos, time = t, walls = walls, glass = glass };
        }

        // ------------------------------------------------------------------ the scope's help
        /// <summary>Who the help may pull a shot onto: a target the player knows (marked, and found if he hides in a
        /// crowd), lookouts and armed men who are hostile now (aiming, alerted, in the fight) — never a bystander, a
        /// protected person, a body double or a target nobody has picked out yet.</summary>
        public static bool FairGame(NPC n)
        {
            if (n == null || n.Dead || n.Escaped || n.IsHidden || n.Protected || n.Incapacitated) return false;
            if (n.IsTarget) return n.MarkedTarget && (!n.HiddenTarget || n.Identified || n.Aiming);
            if (n.Lookout != null) return true;
            if (!n.Armed) return false;
            // a gun turned on someone, a man in the fight with the nest, or the enemy's own security on alert (a VIP's
            // escort only once he fights)
            return n.Aiming || (n.Combat != null && n.Combat.Engaged) || (n.Role == Role.Guard && n.Alerted);
        }

        static bool ZoneOk(AssistZone rule, Collider col)
        {
            if (rule == AssistZone.Any) return true;
            if (rule == AssistZone.None) return false;
            var bp = col != null ? col.GetComponent<BodyPart>() : null;
            if (bp == null) return false;
            if (rule == AssistZone.Limb) return bp.zone == BodyZone.Arm || bp.zone == BodyZone.Leg;
            return bp.zone == (rule == AssistZone.Arm ? BodyZone.Arm : BodyZone.Leg);
        }

        static readonly float[] sampleT = new float[5];
        static readonly Vector3[] sampleOff = new Vector3[5];

        /// <summary>
        /// <see cref="PredictMoving"/> with the scope's help. A round that would graze a fair target — passing within
        /// <paramref name="magnet"/> metres of his body at his range (plus <paramref name="perMetre"/> per metre) — is
        /// launched along a line corrected by that much so it meets him, provided the corrected line is clear (no help
        /// through cover, never onto someone else, on a take-alive job only onto the limb asked for and only with a round
        /// that keeps him alive). Ballistics are untouched: drop, wind and lead still decide where the round goes, the help
        /// only forgives the last few centimetres. <paramref name="vel"/> comes back corrected. A miss reports how far it
        /// passed the nearest fair body (the spotter's call), seen along <paramref name="right"/> / <paramref name="up"/>.
        /// <paramref name="raw"/>: the flight without any help (the lessons show it: where drop and wind really take it).
        /// </summary>
        public static ShotResult PredictAssisted(Vector3 origin, ref Vector3 vel, Vector3 wind, int mask, int pierce, AmmoKind ammo, float magnet,
                                                 float perMetre, Vector3 right, Vector3 up, out ShotResult raw)
        {
            var r = PredictMoving(origin, vel, wind, mask, pierce);
            raw = r;
            if (r.hit && r.Npc != null) return r;
            float v = vel.magnitude;
            if (v < 1f) return r;
            var dir = vel / v;
            var acc = Accel(wind);
            var rt = MissionRuntime.I;
            NPC best = null, near = null;
            float bestD = float.MaxValue, bestT = 0f, nearD = 3f, nearS = 0f;
            Vector3 bestAim = Vector3.zero, nearDelta = Vector3.zero;
            bool nearBlocked = false;
            AssistZone bestRule = AssistZone.Any;
            foreach (var n in NPC.All)
            {
                if (!FairGame(n)) continue;
                var c = n.transform.position + Vector3.up;
                float s = Vector3.Dot(c - origin, dir);
                if (s < 3f) continue;
                float te = s / v;
                if (te > MaxTime) continue;
                var on = origin + vel * te + 0.5f * acc * te * te;
                // cheap reject: nowhere near his body
                if ((on - (c + n.PredictOffset(te))).sqrMagnitude > 16f) continue;
                var cols = n.HitColliders;
                // the front of his body as the round comes at him
                float sFront = s;
                foreach (var col in cols)
                {
                    if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                    sFront = Mathf.Min(sFront, Vector3.Dot(col.ClosestPoint(origin) - origin, dir));
                }
                // the round stopped just in front of him above the floor he stands on: his cover took it (no help over
                // or through it); stopped short at his feet's level it simply fell short into the ground
                bool blocked = r.hit && !r.water && r.distance < sFront - 0.05f && r.distance > sFront - 4f &&
                               r.point.y > n.transform.position.y + 0.25f;
                var rule = rt != null ? rt.AssistRule(n, ammo) : AssistZone.Any;
                float limit = blocked || rule == AssistZone.None || magnet <= 0f ? 0f : magnet + perMetre * s;
                // where he will be at five moments across his depth (the same for every zone)
                float step = 0.35f / v;
                for (int k = 0; k < 5; k++)
                {
                    sampleT[k] = te + (k - 2) * step;
                    sampleOff[k] = sampleT[k] > 0f ? n.PredictOffset(sampleT[k]) : Vector3.zero;
                }
                foreach (var col in cols)
                {
                    if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                    bool zoneOk = ZoneOk(rule, col);
                    for (int k = 0; k < 5; k++)
                    {
                        float tk = sampleT[k];
                        if (tk <= 0f) continue;
                        // the flight at that moment, in his frame of now (he walks on meanwhile)
                        var pk = origin + vel * tk + 0.5f * acc * tk * tk - sampleOff[k];
                        var cp = col.ClosestPoint(pk);
                        var delta = pk - cp;
                        float d = delta.magnitude;
                        if (d < nearD) { nearD = d; near = n; nearDelta = delta; nearS = s; nearBlocked = blocked; }
                        if (zoneOk && d <= limit && d < bestD)
                        {
                            bestD = d;
                            best = n;
                            bestT = tk;
                            bestRule = rule;
                            // a few centimetres inside the zone, not on its skin (arms swing, the nudge has room)
                            var into = col.bounds.center - cp;
                            float il = into.magnitude;
                            bestAim = il > 1e-4f ? cp + into / il * Mathf.Min(0.06f, il) : cp;
                        }
                    }
                }
            }
            if (best != null && bestT > 0f)
            {
                var aimWorld = bestAim + best.PredictOffset(bestT);
                var at = origin + vel * bestT + 0.5f * acc * bestT * bestT;
                var v2 = (vel + (aimWorld - at) / bestT).normalized * v;
                var r2 = PredictMoving(origin, v2, wind, mask, pierce);
                if (r2.hit && r2.Npc == best && ZoneOk(bestRule, r2.collider))
                {
                    vel = v2;
                    r2.assisted = true;
                    return r2;
                }
            }
            if (near != null)
            {
                r.nearNpc = near;
                r.nearRange = nearS;
                r.nearBlocked = nearBlocked;
                r.missBy = new Vector2(Vector3.Dot(nearDelta, right), Vector3.Dot(nearDelta, up));
            }
            return r;
        }
    }

    /// <summary>A bullet simulated in (scaled) real time, so moving targets need a lead and the kill cam can follow it.</summary>
    public class Projectile : MonoBehaviour
    {
        public Vector3 velocity;
        public Vector3 wind;
        public Vector3 origin;
        public int mask;
        public System.Action<Projectile, ShotResult> OnImpact;
        public bool done;
        public float flightTime;
        public ShotSpec spec = ShotSpec.Default;
        public WeaponDef weapon;
        /// <summary>What the scope predicted when the trigger broke (the spotter calls a miss from it).</summary>
        public ShotResult predicted;
        /// <summary>At the trigger: the scope's right, a held breath, the pace of the man the round passed.</summary>
        public Vector3 shotRight = Vector3.right, nearVel;
        public bool shotBreath;
        /// <summary>Fired while a louder sound covered it (MissionRuntime.NoiseMask): its crack and impact carry less far.</summary>
        public bool masked;
        /// <summary>Kill cam: the spot it was predicted to hit, in the hit bone's own frame. A walker's arms and legs swing a
        /// little differently from the straight-line prediction, so over the last metres the bullet is nudged onto that
        /// spot (a fraction of a degree): the shot the camera follows always lands where it was going.</summary>
        public Transform guide;
        public Vector3 guideLocal;
        /// <summary>The person this bullet struck (null: a miss, a wall, water).</summary>
        public NPC HitNpc { get; private set; }
        int budget = -1, walls, guard, glass;
        /// <summary>Degrees the nudge may still turn the bullet in all (a few tenths: arm swing, not a homing missile).</summary>
        float steerLeft = 0.8f;

        /// <summary>Bullets still in the air (a restart removes them so nothing from the last run lands in the next).</summary>
        public static readonly System.Collections.Generic.List<Projectile> Live = new System.Collections.Generic.List<Projectile>();

        void OnEnable() { Live.Add(this); }
        void OnDisable() { Live.Remove(this); }

        public static void ClearAll()
        {
            foreach (var p in Live.ToArray())
            {
                if (p == null) continue;
                p.done = true;
                p.OnImpact = null;
                Destroy(p.gameObject);
            }
            Live.Clear();
        }

        void Update()
        {
            if (done) return;
            if (budget < 0) budget = spec.pierce;
            Physics.SyncTransforms();
            float dt = Time.deltaTime;
            var acc = Ballistics.Accel(wind);
            const float maxStep = 1f / 500f;
            while (dt > 0 && !done)
            {
                float s = Mathf.Min(dt, maxStep);
                dt -= s;
                var pos = transform.position;
                if (guide != null) Steer(pos, velocity.magnitude * s);
                var next = pos + velocity * s + 0.5f * acc * s * s;
                var seg = next - pos;
                if (seg.sqrMagnitude > 0 && Physics.Raycast(pos, seg.normalized, out var h, seg.magnitude, mask, QueryTriggerInteraction.Ignore))
                {
                    var dir = seg.normalized;
                    var pane = GlassPane.Of(h.collider);
                    if (pane != null && !pane.Broken && glass < 4)
                    {
                        glass++;
                        pane.Break(h.point, dir, masked);
                        velocity *= Ballistics.GlassSlow;
                        transform.position = h.point + dir * 0.05f;
                        continue;
                    }
                    if (h.collider.GetComponent<BodyPart>() == null && guard++ < 6 && Ballistics.TryPenetrate(h, dir, ref budget, ref velocity, out var exit))
                    {
                        // through: dust and sparks on both sides, the bullet flies on
                        walls++;
                        FX.Dust(h.point, h.normal, true);
                        FX.Dust(exit, dir, true);
                        Sfx.PlayAt(Sfx.ImpactMetal, h.point, 0.6f);
                        transform.position = exit;
                        continue;
                    }
                    transform.position = h.point;
                    Finish(new ShotResult { hit = true, point = h.point, normal = h.normal, collider = h.collider, time = flightTime, distance = Vector3.Distance(origin, h.point), walls = walls, glass = glass });
                    return;
                }
                if (next.y < Level.WaterY)
                {
                    var wp = Vector3.Lerp(pos, next, Mathf.InverseLerp(pos.y, next.y, Level.WaterY));
                    transform.position = wp;
                    Finish(new ShotResult { hit = true, water = true, point = wp, normal = Vector3.up, time = flightTime, distance = Vector3.Distance(origin, wp), walls = walls });
                    return;
                }
                velocity += acc * s;
                transform.position = next;
                if (velocity.sqrMagnitude > 0) transform.rotation = Quaternion.LookRotation(velocity);
                flightTime += s;
                if (flightTime > Ballistics.MaxTime)
                {
                    Finish(new ShotResult { hit = false, point = next, time = flightTime, walls = walls });
                    return;
                }
            }
        }

        /// <summary>Over the last 30-odd metres: a gentle nudge (at most 0.06° per metre, under half a degree in all —
        /// about a hand's breadth) onto the spot the prediction promised. More than that and the man did something the
        /// prediction could not know (stopped, turned, ducked): the bullet flies on and misses.</summary>
        void Steer(Vector3 pos, float travel)
        {
            if (!guide.gameObject.activeInHierarchy || steerLeft <= 0f) { guide = null; return; }
            var to = guide.TransformPoint(guideLocal) - pos;
            float d = to.magnitude;
            if (d > 60f || d < 0.02f) return;            // test 5: a 32 m nudge (~12 cm) let walkers slip out: the dot showed a hit, the round hit the ground
            float ang = Vector3.Angle(velocity, to);
            if (ang > 5f) { guide = null; return; }
            float turn = Mathf.Min(ang, travel * 0.08f, steerLeft);
            steerLeft -= turn;
            velocity = Vector3.RotateTowards(velocity, to / d * velocity.magnitude, turn * Mathf.Deg2Rad, 0f);
        }

        void Finish(ShotResult r)
        {
            done = true;
            HitNpc = r.hit ? r.Npc : null;
            OnImpact?.Invoke(this, r);
            var tr = GetComponentInChildren<TrailRenderer>();
            if (tr != null) tr.emitting = false;
            foreach (var mr in GetComponentsInChildren<MeshRenderer>()) mr.enabled = false;
            Destroy(gameObject, 2f);
        }
    }
}

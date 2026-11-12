using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// The nest under fire. Every shot the player fires can give his position away (a loud rifle quickly, a silenced
    /// one slowly; at night the muzzle flash shows), and lookouts with binoculars sweep the rooftops. Once the enemy has
    /// located the nest, armed men take cover and fire back: rounds take their time to arrive (and their report even
    /// longer), the ones that miss crack past or smack into the parapet, the ones that hit hurt. Ducking behind the
    /// parapet (the cover button) is safe; health comes back after a few quiet seconds. Too many hits and the contract
    /// is over.
    /// </summary>
    public class Firefight : MonoBehaviour
    {
        public static Firefight I;
        /// <summary>Automated test runs: rounds never hurt.</summary>
        public static bool God;

        public const float MaxHealth = 100f;
        public float Health { get; private set; } = MaxHealth;
        public bool Dead { get; private set; }
        /// <summary>0..1: how close the enemy is to knowing where the nest is.</summary>
        public float Heat { get; private set; }
        public bool Located { get; private set; }
        /// <summary>How well the enemy shoots in this contract (0 = nobody fires at the nest).</summary>
        public float Threat { get; private set; } = 1f;
        public float LastHitTime { get; private set; } = -100f;
        public float LastRoundTime { get; private set; } = -100f;
        public int HitsTaken { get; private set; }
        public bool EverLocated { get; private set; }
        public bool EverShotAt { get; private set; }
        /// <summary>0 healthy .. 1 nearly dead.</summary>
        public float Hurt => 1f - Health / MaxHealth;
        /// <summary>A lookout is looking straight at the nest right now (0..1, HUD eye).</summary>
        public float Watched { get; private set; }
        /// <summary>Somebody aiming at the nest or rounds arriving in the last seconds.</summary>
        public bool UnderFire => Time.time - LastRoundTime < 3f || AnyAiming;
        public bool AnyAiming { get; private set; }

        /// <summary>A round arrived (from, hit, damage): HUD direction marker and flash.</summary>
        public event Action<Vector3, bool, float> Round;
        public event Action<bool> LocatedChanged;
        public event Action Died;

        MissionRuntime rt;
        Sniper sn;
        float lastHeatAt = -100f, watchedDecay;
        int roundCount;

        class InRound { public float arrive; public Vector3 from; public GunSpec spec; public bool hit; public float damage; public NPC by; public float side; }
        readonly List<InRound> rounds = new List<InRound>();
        readonly List<InRound> spare = new List<InRound>();

        public void Init(MissionRuntime runtime, Sniper sniper)
        {
            I = this;
            rt = runtime;
            sn = sniper;
        }

        /// <summary>A new contract: full health, nobody knows where the nest is.</summary>
        public void Begin(MissionDef def)
        {
            Clear();
            Threat = def == null ? 0f : (def.threat >= 0f ? def.threat : DefaultThreat(def));
        }

        /// <summary>How dangerous the enemy's return fire is, by the contract's place in the campaign. V3 (user: "the
        /// enemy just stands and watches"): real from the first contract and much sharper on the mountain.</summary>
        static float DefaultThreat(MissionDef def)
        {
            int n = def.number;
            float k = Mathf.Clamp(Tuning.Int("enemy_threat_pct"), 0, 300) / 100f;
            // the very first contract is where the controls are learnt: a little gentler there
            return (n <= 1 ? 0.45f : Mathf.Clamp(0.55f + (n - 1) * 0.032f, 0.55f, 1.4f)) * k;
        }

        /// <summary>How sure the enemy must be of the nest (heat) before gunmen start shooting at where the shots came
        /// from without having seen it (blind return fire: wild, but rounds crack past and the hands shake).</summary>
        public const float BlindHeat = 0.3f;

        public void Clear()
        {
            StopAllCoroutines();
            Health = MaxHealth;
            Dead = false;
            Heat = 0f;
            Located = false;
            LastHitTime = LastRoundTime = lastHeatAt = -100f;
            HitsTaken = 0;
            EverLocated = EverShotAt = false;
            Watched = 0f;
            AnyAiming = false;
            roundCount = 0;
            spare.AddRange(rounds);
            rounds.Clear();
            CombatFX.ClearAll();
        }

        /// <summary>V2: the player moved to another place: the enemy has lost him (rounds already on the way still land
        /// where he was).</summary>
        public void Relocated()
        {
            bool was = Located;
            Heat = 0f;
            Located = false;
            Watched = 0f;
            AnyAiming = false;
            spare.AddRange(rounds);
            rounds.Clear();
            if (was) LocatedChanged?.Invoke(false);
        }

        /// <summary>The nest as the enemy sees it (the player's eye; lower while ducked).</summary>
        public Vector3 Nest => sn != null ? sn.transform.position : Vector3.zero;

        /// <summary>How much of the player shows over the parapet: scoped (head up behind the rifle) 1, looking over
        /// it 0.75, ducked 0.</summary>
        public float Exposure => sn == null || sn.InCover ? 0f : (sn.Scoped ? 1f : 0.75f);

        bool Live => rt != null && rt.IsPlaying && !Dead && Threat > 0f;

        // ------------------------------------------------------------------ being found
        /// <summary>The player fired: the report (and at night the flash) points towards the nest.</summary>
        public void OnPlayerShot(bool silenced, bool masked, bool tranq)
        {
            if (!Live || !AnyEnemy()) return;
            float k = tranq ? 0.04f : (silenced ? 0.12f : 0.34f);
            if (rt.IsNight && !silenced && !tranq) k *= 1.45f;
            if (masked) k *= 0.25f;
            // nobody armed is listening yet: the first crack only gets everybody's attention
            if (!AnyEnemyAlert()) k *= 0.45f;
            AddHeat(k);
        }

        /// <summary>Raises how sure the enemy is of the nest's position (lookouts, searching gunmen, shots).</summary>
        public void AddHeat(float amount)
        {
            if (!Live || amount <= 0f) return;
            Heat = Mathf.Min(1.2f, Heat + amount);
            lastHeatAt = Time.time;
            if (!Located && Heat >= 1f) SetLocated(true);
        }

        /// <summary>A lookout has the nest in his binoculars (0..1).</summary>
        public void Watch(float amount)
        {
            Watched = Mathf.Max(Watched, Mathf.Clamp01(amount));
            watchedDecay = 0.4f;
        }

        /// <summary>The nest's position is known (a lookout radioed it, the alarm went up) or lost again.</summary>
        public void SetLocated(bool on)
        {
            if (Located == on || (on && !Live)) return;
            Located = on;
            if (on)
            {
                Heat = Mathf.Max(Heat, 1f);
                lastHeatAt = Time.time;
                EverLocated = true;
                rt.SetFlag("located");
                // the whole enemy now knows where the shooting comes from
                foreach (var n in NPC.All)
                {
                    if (n == null || n.Dead || n.Escaped || n.Incapacitated) continue;
                    var g = n.Combat;
                    if (g != null) g.OnNestLocated();
                }
            }
            LocatedChanged?.Invoke(on);
        }

        /// <summary>Someone left who could fight back or give the nest away (an armed man able to shoot, a lookout).</summary>
        static bool AnyEnemy()
        {
            foreach (var n in NPC.All)
            {
                if (n == null || n.Dead || n.Incapacitated || n.Escaped) continue;
                if ((n.Combat != null && n.Combat.CanFight) || n.Lookout != null) return true;
            }
            return false;
        }

        static bool AnyEnemyAlert()
        {
            foreach (var n in NPC.All)
                if (n != null && !n.Dead && !n.Incapacitated && n.Combat != null && (n.Alerted || n.Combat.Engaged)) return true;
            return false;
        }

        // ------------------------------------------------------------------ rounds
        /// <summary>A gunman fires one round at the nest from <paramref name="muzzle"/>. <paramref name="aim"/>: his
        /// accuracy this round (settled aim, suppression, distance already folded in by the caller).</summary>
        public void Fire(NPC by, GunSpec spec, Vector3 muzzle, float aim)
        {
            if (!Live || spec == null) return;
            var nest = Nest;
            float d = Vector3.Distance(muzzle, nest);
            float p = spec.acc * Threat * Mathf.Clamp01(aim) * DistanceFactor(d, spec.range);
            var r = spare.Count > 0 ? spare[spare.Count - 1] : new InRound();
            if (spare.Count > 0) spare.RemoveAt(spare.Count - 1);
            r.arrive = Time.time + d / spec.speed;
            r.from = muzzle;
            r.spec = spec;
            r.hit = !God && UnityEngine.Random.value < p;
            r.damage = spec.damage * DamageScale * UnityEngine.Random.Range(0.85f, 1.15f);
            r.by = by;
            r.side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            rounds.Add(r);
            EverShotAt = true;
            roundCount++;
            // what the nest sees and hears: the flash at once, the report after its trip through the air, every third
            // rifle round a tracer
            CombatFX.MuzzleFlash(muzzle, nest - muzzle, spec.twoHanded ? 1f : 0.7f);
            Sfx.PlayAt(spec.Report, muzzle, spec.volume, d / 340f, UnityEngine.Random.Range(0.94f, 1.06f));
            if ((spec.kind == GunKind.Rifle && roundCount % 3 == 0) || (spec.kind == GunKind.Smg && roundCount % 5 == 0))
            {
                var miss = nest + MissOffset(nest - muzzle, r.side, r.hit ? 0.3f : 1.6f);
                CombatFX.Tracer(muzzle, miss, spec.speed);
            }
        }

        /// <summary>1 close in, falling to nothing at the gun's range.</summary>
        static float DistanceFactor(float d, float range)
        {
            if (range <= 1f) return 0f;
            float x = d / range;
            return Mathf.Clamp01(1f - Mathf.Pow(x, 1.6f));
        }

        static Vector3 MissOffset(Vector3 dir, float side, float spread)
        {
            var f = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, f);
            right = right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;
            return right * side * UnityEngine.Random.Range(0.4f, 1f) * spread + Vector3.up * UnityEngine.Random.Range(-0.4f, 0.8f) * spread;
        }

        /// <summary>
        /// One heavy round from an enemy marksman (he has been aiming at the nest): hits hard unless the player is
        /// down behind the parapet. True when it hit.
        /// </summary>
        /// <summary>How hard the guards' rounds hit (balance: the player gets time to answer the fire). V3: 0.7 → 0.85.</summary>
        public static float DamageScale => 0.85f * Mathf.Clamp(Tuning.Int("enemy_damage_pct"), 0, 300) / 100f;

        public bool HeavyRound(NPC by, Vector3 muzzle, float damage, string reason)
        {
            if (rt == null || !rt.IsPlaying || Dead) return false;
            var nest = Nest;
            var dir = nest - muzzle;
            CombatFX.MuzzleFlash(muzzle, dir, 1.2f);
            Sfx.PlayAt(Sfx.Shot, muzzle, 1f, dir.magnitude / 340f, 0.9f);
            LastRoundTime = Time.time;
            EverShotAt = true;
            // down behind the parapet it smacks into the wall; up, a trained marksman seldom misses
            if (sn.InCover || God || UnityEngine.Random.value > (sn.Scoped ? 0.85f : 0.65f))
            {
                Snap(muzzle, true, UnityEngine.Random.value < 0.5f ? -1f : 1f);
                return false;
            }
            Damage(damage, muzzle, by, reason);
            return true;
        }

        void Update()
        {
            if (rt == null) return;
            float dt = Time.deltaTime;
            // lookouts: what they see this frame (decays when nobody is looking)
            if (watchedDecay > 0f) watchedDecay -= dt;
            else Watched = Mathf.MoveTowards(Watched, 0f, dt * 2.5f);
            AnyAiming = false;
            if (!rt.IsPlaying || Dead) return;
            foreach (var n in NPC.All)
                if (n != null && (n.ThreatToPlayer > 0.01f || n.AimingAtPlayer) && !n.Dead && !n.Incapacitated) { AnyAiming = true; break; }
            if (KillCam.Active || Cinematic.Active)
            {
                // the world is in slow motion (or the camera is away): rounds in flight wait for it, they do not all
                // land at once the moment it ends
                for (int i = 0; i < rounds.Count; i++) rounds[i].arrive += Time.deltaTime;
                return;
            }
            // rounds arriving
            for (int i = rounds.Count - 1; i >= 0; i--)
            {
                var r = rounds[i];
                if (Time.time < r.arrive) continue;
                rounds.RemoveAt(i);
                spare.Add(r);
                Arrive(r);
                if (Dead || !rt.IsPlaying) return;
            }
            // health comes back after a few quiet seconds
            if (Health < MaxHealth && Time.time - LastHitTime > 4f) Health = Mathf.Min(MaxHealth, Health + 14f * dt);
            // the enemy forgets: slowly in the open, faster while the player stays down; a located nest is lost only
            // after a long quiet spell behind the parapet
            float quiet = Time.time - lastHeatAt;
            if (quiet > 5f && Heat > 0f)
            {
                float rate = sn.InCover ? 0.07f : 0.025f;
                if (Located) rate *= sn.InCover ? 0.6f : 0.15f;
                Heat = Mathf.Max(0f, Heat - rate * dt);
                if (Located && Heat < 0.45f)
                {
                    SetLocated(false);
                    rt.Say("Seni kaybettiler — ama nişancı yuvasını arıyorlar", Theme.Accent, "eye");
                }
            }
        }

        void Arrive(InRound r)
        {
            LastRoundTime = Time.time;
            // ducked by the time it arrives: it smacks into the parapet
            if (r.hit && !sn.InCover && !God) Damage(r.damage, r.from, r.by, null);
            else Snap(r.from, r.spec.supersonic, r.side);
        }

        /// <summary>A miss: the crack (or whiz) of it going past, now and then a hit on the parapet in front.</summary>
        void Snap(Vector3 from, bool supersonic, float side)
        {
            Round?.Invoke(from, false, 0f);
            // automated runs: the rounds are heard but must not throw the bot's aim off
            if (God) return;
            var cam = sn.Cam;
            float pan = 0f;
            if (cam != null)
            {
                var to = from - cam.transform.position;
                pan = Mathf.Clamp(Vector3.Dot(cam.transform.right, to.normalized) * 1.4f, -0.8f, 0.8f);
            }
            // a burst of rounds is a burst of cracks, not one per round on top of each other (and never the player's
            // own rifle sounds pushed out: incoming fire has its own voices)
            bool sound = Time.unscaledTime - lastSnapSound > 0.07f;
            if (sound) lastSnapSound = Time.unscaledTime;
            if (sn.InCover)
            {
                // pinned down: every round hits the wall in front
                ParapetHit(from, 0.6f, sound);
                sn.Shake(0.06f);
                return;
            }
            if (sound)
            {
                if (supersonic) Sfx.PlayIncoming(Sfx.Snap, 0.75f, UnityEngine.Random.Range(0.9f, 1.15f), -pan * 0.5f + side * 0.3f);
                else Sfx.PlayIncoming(Sfx.Whiz, 0.6f, UnityEngine.Random.Range(0.85f, 1.15f), side * 0.6f);
            }
            if (!sn.Scoped) sn.Shake(0.05f);                // the scope stays still: the crack and the dust tell it
            if (!sn.Scoped && UnityEngine.Random.value < 0.4f) ParapetHit(from, 0.9f, sound);
        }

        float lastSnapSound = -1f;

        /// <summary>Dust and chips from the wall in front of the player, on the side the round came from.</summary>
        void ParapetHit(Vector3 from, float spread, bool sound)
        {
            var cam = sn.Cam;
            if (cam == null) return;
            var eye = cam.transform.position;
            var f = from - eye; f.y = 0f;
            f = f.sqrMagnitude > 1e-4f ? f.normalized : cam.transform.forward;
            var right = Vector3.Cross(Vector3.up, f);
            var want = eye + f * UnityEngine.Random.Range(1.1f, 2.2f) + right * UnityEngine.Random.Range(-spread, spread) + Vector3.down * UnityEngine.Random.Range(0.15f, 0.7f);
            // the real wall if it is there
            var dir = want - eye;
            if (Physics.Raycast(eye, dir.normalized, out var h, dir.magnitude + 1.5f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                CombatFX.ParapetHit(h.point + h.normal * 0.02f, h.normal, UnityEngine.Random.value < 0.3f, sound);
            else
                CombatFX.ParapetHit(want, -f, UnityEngine.Random.value < 0.3f, sound);
        }

        void Damage(float amount, Vector3 from, NPC by, string reason)
        {
            if (Dead) return;
            Health = Mathf.Max(0f, Health - amount);
            LastHitTime = Time.time;
            HitsTaken++;
            rt.SetFlag("hit");
            Sfx.Play(Sfx.Hurt, 0.95f, UnityEngine.Random.Range(0.92f, 1.06f));
            Sfx.Play(Sfx.Flesh, 0.7f, 0.8f);
            if (amount >= 30f || Health < 40f) Sfx.Play(Sfx.EarRing, 0.8f);
            sn.Hit(from, amount);
            Util.Vibrate();
            Round?.Invoke(from, true, amount);
            if (Health <= 0f) Die(by, reason);
        }

        void Die(NPC by, string reason)
        {
            if (Dead) return;
            Dead = true;
            // the contract is lost this instant: nothing still in the air (a kill, the team arriving) can turn it into a
            // success during the fall; the result comes up once the slow moment is over
            string text = !string.IsNullOrEmpty(reason) ? reason : (by != null && !string.IsNullOrEmpty(by.DisplayName) ? "Vuruldun — " + by.DisplayName : "Vuruldun — yerin tespit edildi");
            rt.Fail(text, 2.2f);
            Died?.Invoke();
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            sn.Collapse();
            Sfx.Play(Sfx.Thud, 0.9f, 0.8f);
            // a long, slow moment while the view falls
            Time.timeScale = 0.35f;
            Time.fixedDeltaTime = 0.02f * 0.35f;
            float t = 0f;
            while (t < 1.3f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            // paused meanwhile: the pause menu restores the clock when it closes
            bool paused = Game.I != null && Game.I.Paused;
            if (!KillCam.Active && !paused) { Time.timeScale = 1f; Time.fixedDeltaTime = 0.02f; }
        }
    }
}

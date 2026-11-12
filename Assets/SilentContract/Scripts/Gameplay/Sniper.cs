using System;
using System.Collections;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// The player: a fixed sniper nest. Look around, zoom through the scope, hold breath, identify people with the
    /// rangefinder scanner and fire with bullet drop + wind. The carried rifle (armory or contract loan) sets velocity,
    /// magazine, action, zoom range, sway, recoil, sound and penetration; special rounds (armour piercing,
    /// tranquilizer, explosive) are chosen per shot; night vision / thermal are scope modes.
    /// </summary>
    public class Sniper : MonoBehaviour
    {
        public static Sniper I;

        public Camera Cam { get; private set; }
        public bool Scoped { get; private set; }
        public float Zoom { get; private set; } = 6f;
        public WeaponDef Weapon { get; private set; } = Gear.Weapons[0];
        public float MinZoom => Weapon.zoomMin;
        public float MaxZoom => Weapon.zoomMax;
        public float Stamina { get; private set; } = 1f;
        public bool HoldingBreath { get; private set; }
        public int Ammo { get; private set; } = 5;
        public int MagSize => Weapon.magazine;
        public bool Reloading { get; private set; }
        public bool Busy => Time.time < nextFire || Reloading;
        public float BoltProgress => Mathf.Clamp01(1f - (nextFire - Time.time) / Mathf.Max(0.05f, Weapon.cycle));
        public bool InputEnabled = true;
        /// <summary>The wind right now (the contract's wind with its gusts).</summary>
        public Vector3 Wind;
        Vector3 baseWind;
        /// <summary>This contract's shooting difficulty (help, sway, gusts, wind report).</summary>
        public Difficulty Diff { get; private set; } = new Difficulty { assist = AssistLevel.Full, sway = 1f, windInfo = 2, dropMarks = true };
        /// <summary>Elevation turret: the range the rifle is zeroed at (100-500 m in 50 m clicks).</summary>
        public int ZeroRange { get; private set; } = 100;
        public const int ZeroMin = 100, ZeroMax = 500, ZeroStep = 50;
        public event System.Action ZeroChanged;
        /// <summary>The scope draws the impact dot now (the contract's help, the held breath).</summary>
        public bool ShowImpact => Diff.assist == AssistLevel.Full || (Diff.assist == AssistLevel.Breath && HoldingBreath);
        /// <summary>The easy-aim setting helped in this contract (the third star is not awarded).</summary>
        public bool EasyUsed => easyEver;
        bool easyEver;
        MissionDef diffDef;

        /// <summary>The easy-aim setting changed (pause menu): the help follows at once.</summary>
        public void RefreshDifficulty()
        {
            Diff = Difficulty.For(diffDef);
            easyEver |= Diff.easy;
        }
        public float Sensitivity = 1f;
        public bool AimAssist = true;
        /// <summary>Round chambered for the next shot.</summary>
        public AmmoKind AmmoType { get; private set; } = AmmoKind.Standard;
        public VisionMode Vision { get; private set; } = VisionMode.Normal;
        /// <summary>0 = rifle down, 1 = looking through the scope.</summary>
        public float ScopeBlend => scopeBlend;
        /// <summary>Ducked behind the parapet: out of the enemy's sight and fire, but no scope and no shooting.</summary>
        public bool InCover { get; private set; }
        /// <summary>0 up .. 1 down behind the parapet (eases).</summary>
        public float CoverBlend => coverK;
        float coverK, collapseK;
        bool collapsing;
        const float CoverDrop = 0.85f;

        public ShotResult AssistPrediction { get; private set; }
        /// <summary>The same round without the scope's help (the lessons show where drop and wind really take it).</summary>
        public ShotResult RawPrediction { get; private set; }
        /// <summary>The elevation turret's setting when the last round left (the spotter's call reads it).</summary>
        public int LastShotZero { get; private set; } = ZeroMin;
        public bool HasPrediction { get; private set; }
        public float RangeToCenter { get; private set; } = -1;
        public NPC Hovered { get; private set; }
        public float ScanProgress { get; private set; }
        public int ShotsFired { get; private set; }

        public event Action<Projectile, ShotResult> Fired;
        public event Action<NPC> Scanned;
        public event Action Changed;            // rifle, round or vision mode changed (HUD refresh)

        const float HipFov = 55f;
        float yaw, pitch, baseYaw;
        float minYaw = -85, maxYaw = 85, minPitch = -40, maxPitch = 12;
        float recoil, shake, nextFire;
        bool breathKeyHeld, breathButtonHeld;
        float heartbeatTimer, swaySeed;
        Transform rifleRoot, muzzle;
        // the rifle is drawn shrunk towards the eye (same picture, 1/ViewScale of the depth) so walls never cut it
        const float ViewScale = 0.15f;
        static readonly Vector3 RifleRest = new Vector3(0.2f, -0.24f, 0.55f) * ViewScale;
        Vector3 rifleRest = RifleRest;
        Vector3 rifleScoped = new Vector3(0.0f, -0.12f, 0.25f) * ViewScale;
        public bool ShowRifle = true;
        int mask;
        float scopeBlend;
        string builtModel;

        // saved upgrades, read once per contract (not every frame)
        int upComp, upStab, upBreath;
        float StabilityMul => 1f - 0.17f * upStab;
        float BreathSeconds => 4.5f + 1.2f * upBreath;
        /// <summary>Composure skill: the extra shake under fire and when hurt, 25% less per level.</summary>
        float ComposureMul => 1f - 0.25f * upComp;

        void ReadUpgrades()
        {
            upComp = Save.Upgrade("composure");
            upStab = Save.Upgrade("stability");
            upBreath = Save.Upgrade("breath");
            gearCheckAt = -1f;
        }

        public void Init(Camera cam, LayoutPlayer p)
        {
            I = this;
            Cam = cam;
            mask = Layers.ShotMask;
            transform.position = new Vector3(p.x, p.y, p.z);
            baseYaw = p.yaw;
            minYaw = p.yawMin; maxYaw = p.yawMax; minPitch = p.pitchMin; maxPitch = p.pitchMax;
            yaw = 0; pitch = p.pitch;
            cam.transform.SetParent(transform, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 6000f;
            cam.fieldOfView = HipFov;
            swaySeed = UnityEngine.Random.value * 100f;
            ReadUpgrades();
            Weapon = Gear.Effective(Gear.Carried);
            BuildRifle();
            Apply(true);
        }

        /// <summary>Moves the nest (contracts in other locations).</summary>
        public void Place(LayoutPlayer p)
        {
            if (p == null) return;
            transform.position = new Vector3(p.x, p.y, p.z);
            baseYaw = p.yaw;
            minYaw = p.yawMin; maxYaw = p.yawMax; minPitch = p.pitchMin; maxPitch = p.pitchMax;
            yaw = 0; pitch = p.pitch;
            Apply(true);
        }

        void BuildRifle()
        {
            if (rifleRoot != null) Destroy(rifleRoot.gameObject);
            rifleRoot = CreateRifleView(Cam.transform, Weapon);
            builtModel = Weapon.model;
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(rifleRoot, false);
            float len = 1.23f;
            var prefab = Res.Prop(Weapon.model);
            var info = prefab != null ? prefab.GetComponent<PropInfo>() : null;
            if (info != null && info.size.x > 0.3f) len = info.size.x;
            muzzle.localPosition = new Vector3(0, 0.03f, len * 0.5f * Weapon.viewScale);
        }

        /// <summary>The first person rifle at its resting place under a camera (also used by the preview renderer).</summary>
        public static Transform CreateRifleView(Transform cam, WeaponDef w = null)
        {
            w = w ?? Gear.Weapons[0];
            var root = new GameObject("RifleView").transform;
            root.SetParent(cam, false);
            root.localPosition = RifleRest;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one * ViewScale;
            var prefab = Res.Prop(w.model);
            if (prefab == null && w.model != "ph_rifle") prefab = Res.Prop("ph_rifle");
            if (prefab != null)
            {
                var r = Instantiate(prefab, root);
                r.transform.localPosition = w.viewOffset;
                // the rifles lie along X: turn the muzzle forward and centre the grip near the camera
                r.transform.localRotation = Quaternion.Euler(0, w.viewYaw, 0);
                r.transform.localScale = Vector3.one * w.viewScale;
                foreach (var c in r.GetComponentsInChildren<Collider>())
                {
                    if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
                }
                foreach (var rr in r.GetComponentsInChildren<Renderer>())
                {
                    rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rr.receiveShadows = false;
                }
                Util.SetLayer(r, Layers.IgnoreRaycast);
            }
            return root;
        }

        /// <summary>Yaw that points the rifle model's barrel along +Z (set from the asset preview).</summary>
        public static float RifleYaw = 90f;

        public void ResetForMission(LayoutPlayer p, Vector3 wind) => ResetForMission(p, wind, null);

        public void ResetForMission(LayoutPlayer p, Vector3 wind, MissionDef def)
        {
            diffDef = def;
            Diff = Difficulty.For(def);
            easyEver = Diff.easy;
            if (Diff.veteran) wind *= 1.3f;              // VETERAN: the same wind, a third harder
            baseWind = wind;
            ZeroRange = ZeroMin;
            gustSeed = UnityEngine.Random.value * 50f;
            ReadUpgrades();
            var w = Gear.Effective(Gear.Carried);
            if (w != Weapon || builtModel != w.model || rifleRoot == null) { Weapon = w; BuildRifle(); }
            Ammo = MagSize;
            Reloading = false;
            nextFire = 0;
            Stamina = 1f;
            Scoped = false;
            Zoom = Mathf.Clamp(6f, MinZoom, MaxZoom);
            yaw = 0; pitch = p != null ? p.pitch : -6f;
            recoil = 0; shake = 0;
            Wind = wind;
            ZeroChanged?.Invoke();
            ShotsFired = 0;
            Hovered = null;
            ScanProgress = 0;
            ResetPose();
            AmmoType = AmmoKind.Standard;
            Vision = VisionMode.Normal;
            Sensitivity = Save.Sensitivity;
            AimAssist = Save.AimAssist;
            InputEnabled = true;
            StopAllCoroutines();
            Apply(true);
            Changed?.Invoke();
        }

        /// <summary>Shows another rifle (armory preview on the menu background, or a new purchase).</summary>
        public void RefreshWeapon()
        {
            ReadUpgrades();
            var w = Gear.Effective(Gear.Carried);
            if (w == Weapon && builtModel == w.model && rifleRoot != null) return;
            Weapon = w;
            BuildRifle();
            Ammo = Mathf.Min(Ammo, MagSize);
            Zoom = Mathf.Clamp(Zoom, MinZoom, MaxZoom);
            Changed?.Invoke();
        }

        public void LookAt(Vector3 worldPoint)
        {
            var d = worldPoint - transform.position;
            float y = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pt = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            yaw = Mathf.Clamp(Mathf.DeltaAngle(baseYaw, y), minYaw, maxYaw);
            pitch = Mathf.Clamp(pt, minPitch, maxPitch);
            Apply(true);
        }

        /// <summary>World rotation the camera would have looking at a point (intro fly-back).</summary>
        public Quaternion RotationTowards(Vector3 worldPoint)
        {
            var d = worldPoint - transform.position;
            float y = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pt = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            float ry = Mathf.Clamp(Mathf.DeltaAngle(baseYaw, y), minYaw, maxYaw);
            float rp = Mathf.Clamp(pt, minPitch, maxPitch);
            return Quaternion.Euler(-rp, baseYaw + ry, 0);
        }

        public void AddLook(Vector2 deltaPixels)
        {
            if (!InputEnabled) return;
            // sensitivity scales with the field of view so aiming feels the same at every zoom
            float fovK = Cam.fieldOfView / 60f;
            float k = 0.11f * Sensitivity * fovK * (1080f / Mathf.Max(400f, Screen.height));
            yaw = Mathf.Clamp(yaw + deltaPixels.x * k, minYaw, maxYaw);
            pitch = Mathf.Clamp(pitch + deltaPixels.y * k, minPitch, maxPitch);
        }

        public void SetScoped(bool on)
        {
            if (!InputEnabled && on) return;
            // the scope from cover: pop up and look through it
            if (on && InCover) SetCover(false);
            if (Scoped == on) return;
            Scoped = on;
            if (!on) { HoldingBreath = false; Hovered = null; ScanProgress = 0; }
            Sfx.Play(Sfx.Click, 0.5f, on ? 1.2f : 0.9f);
            if (on && Vision != VisionMode.Normal) Sfx.Play(Vision == VisionMode.Night ? Sfx.VisionOn : Sfx.VisionThermal, 0.5f);
        }

        public void ToggleScope() => SetScoped(!Scoped);

        /// <summary>Down behind the parapet (rounds smack into the wall) or back up.</summary>
        public void SetCover(bool on)
        {
            if (InCover == on) return;
            if (on && (!InputEnabled || collapsing)) return;
            InCover = on;
            if (on)
            {
                if (Scoped) SetScoped(false);
                HoldingBreath = false;
            }
            Sfx.Play(Sfx.Thud, 0.22f, on ? 1.5f : 1.8f);
        }

        public void ToggleCover() => SetCover(!InCover);

        /// <summary>Hit by an enemy round. The sight stays where it is (the player must be able to answer the fire): the
        /// hit is told by the red screen, the sound and the vibration; without the scope the view gives a short jolt.</summary>
        public void Hit(Vector3 from, float damage)
        {
            if (!Scoped) Shake(0.25f + damage * 0.005f);
            Stamina = Mathf.Max(0f, Stamina - 0.08f);
        }

        /// <summary>Back up on his feet, eye level (a new contract, the menu behind the screens).</summary>
        public void ResetPose()
        {
            InCover = false;
            coverK = 0f;
            collapseK = 0f;
            collapsing = false;
            if (Cam != null && Cam.transform.parent == transform) Cam.transform.localPosition = Vector3.zero;
        }

        /// <summary>Shot dead: the view sinks and rolls over.</summary>
        public void Collapse()
        {
            collapsing = true;
            InputEnabled = false;
            if (Scoped) SetScoped(false);
            HoldingBreath = false;
        }

        /// <summary>Sets the view angles directly (menu camera, auto test).</summary>
        public void SetLookAngles(float yawRel, float pitchDeg)
        {
            yaw = Mathf.Clamp(yawRel, minYaw, maxYaw);
            pitch = Mathf.Clamp(pitchDeg, minPitch, maxPitch);
        }

        public float YawRel => yaw;
        public float Pitch => pitch;
        public float BaseYaw => baseYaw;

        // ------------------------------------------------------------------ rounds and vision
        /// <summary>Next round type the player can load (standard, then each special type with rounds left).</summary>
        public void CycleAmmo()
        {
            if (!InputEnabled) return;
            var order = new[] { AmmoKind.Standard, AmmoKind.ArmorPiercing, AmmoKind.Tranquilizer, AmmoKind.Explosive };
            int i = Array.IndexOf(order, AmmoType);
            for (int k = 1; k <= order.Length; k++)
            {
                var a = order[(i + k) % order.Length];
                if (a == AmmoKind.Standard || Gear.AmmoAvailable(a) > 0)
                {
                    if (a == AmmoType) return;
                    AmmoType = a;
                    gearCheckAt = -1f;
                    Sfx.Play(Sfx.Reload, 0.45f, 1.5f);
                    Changed?.Invoke();
                    return;
                }
            }
        }

        // gear flags for the HUD, refreshed a few times a second (they read saved counts)
        float gearCheckAt = -1f;
        bool hasSpecial, hasVision;

        void CheckGear()
        {
            if (Time.unscaledTime < gearCheckAt) return;
            gearCheckAt = Time.unscaledTime + 0.4f;
            hasSpecial = false;
            foreach (var a in Gear.Ammo)
                if (a.kind != AmmoKind.Standard && Gear.AmmoAvailable(a.kind) > 0) { hasSpecial = true; break; }
            hasVision = Gear.HasGadget("nv") || Gear.HasGadget("thermal");
        }

        public bool HasSpecialAmmo { get { CheckGear(); return hasSpecial; } }

        public bool HasVisionGear { get { CheckGear(); return hasVision; } }

        public void CycleVision()
        {
            var next = Vision;
            for (int k = 0; k < 3; k++)
            {
                next = (VisionMode)(((int)next + 1) % 3);
                if (next == VisionMode.Normal) break;
                if (next == VisionMode.Night && Gear.HasGadget("nv")) break;
                if (next == VisionMode.Thermal && Gear.HasGadget("thermal")) break;
            }
            if (next == Vision) return;
            Vision = next;
            if (next == VisionMode.Night) Sfx.Play(Sfx.VisionOn, 0.6f);
            else if (next == VisionMode.Thermal) Sfx.Play(Sfx.VisionThermal, 0.6f);
            else Sfx.Play(Sfx.Click, 0.5f, 0.8f);
            Changed?.Invoke();
        }

        /// <summary>What the next shot carries (round type changes speed, penetration and effect).</summary>
        public ShotSpec CurrentSpec
        {
            get
            {
                var s = new ShotSpec { velocity = Weapon.velocity, pierce = Weapon.pierce, power = Weapon.power, ammo = AmmoType };
                switch (AmmoType)
                {
                    case AmmoKind.ArmorPiercing: s.pierce += 1; s.velocity *= 1.02f; break;
                    case AmmoKind.Tranquilizer: s.pierce = 0; s.power = 0f; s.velocity *= 0.55f; break;
                    case AmmoKind.Explosive: s.pierce = 0; s.velocity *= 0.95f; break;
                }
                return s;
            }
        }

        void LateUpdate()
        {
            if (rifleRoot != null)
            {
                bool show = ShowRifle && !KillCam.Active && !Cinematic.Active && scopeBlend < 0.95f;
                if (rifleRoot.gameObject.activeSelf != show) rifleRoot.gameObject.SetActive(show);
            }
            // scope vision modes (the effect lives in the post pass; thermal adds the body heat pass)
            float k = Scoped && !KillCam.Active && !Cinematic.Active ? Mathf.Clamp01((scopeBlend - 0.5f) * 2f) : 0f;
            VisionFX.Set(Vision, k, MissionRuntime.I != null && MissionRuntime.I.IsNight);
        }

        public void SetZoom(float z) => Zoom = Mathf.Clamp(z, MinZoom, MaxZoom);

        /// <summary>One click of the elevation turret (+1 up, -1 down).</summary>
        public void StepZero(int dir)
        {
            int z = Mathf.Clamp(ZeroRange + dir * ZeroStep, ZeroMin, ZeroMax);
            if (z == ZeroRange) { Sfx.Play(Sfx.Click, 0.4f, 0.7f); return; }
            ZeroRange = z;
            Sfx.Play(Sfx.Click, 0.7f, dir > 0 ? 1.25f : 1.05f);
            ZeroChanged?.Invoke();
        }

        float gustSeed;

        /// <summary>The wind swells and eases and swings a little round its direction; the shot takes the wind of the
        /// moment it is fired.</summary>
        void UpdateWind()
        {
            if (Diff.gust <= 0f || baseWind.sqrMagnitude < 1e-4f) { Wind = baseWind; return; }
            float t = Time.time / Mathf.Max(1f, Diff.gustPeriod) + gustSeed;
            float s = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f;          // -1..1
            float swing = (Mathf.PerlinNoise(0.81f, t * 0.6f) - 0.5f) * 2f;
            float mag = Mathf.Max(0f, 1f + s * Diff.gust);
            Wind = Quaternion.AngleAxis(swing * Diff.gust * 40f, Vector3.up) * baseWind * mag;
        }
        public void SetBreathButton(bool held) => breathButtonHeld = held;
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void Update()
        {
            if (Cam == null || KillCam.Active || Cinematic.Active) return;
            float dt = Time.deltaTime;
            if (InputEnabled)
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                if (Input.GetMouseButtonDown(1)) ToggleScope();
                float wheel = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(wheel) > 0.001f) SetZoom(Zoom + wheel * 12f);
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F)) Fire();
                if (Input.GetKeyDown(KeyCode.R)) Reload();
                if (Input.GetKeyDown(KeyCode.E)) ToggleScope();
                if (Input.GetKeyDown(KeyCode.V)) CycleVision();
                if (Input.GetKeyDown(KeyCode.Q)) CycleAmmo();
                if (Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.T)) StepZero(1);
                if (Input.GetKeyDown(KeyCode.PageDown) || Input.GetKeyDown(KeyCode.G)) StepZero(-1);
                if (Input.GetKeyDown(KeyCode.C)) ToggleCover();
                breathKeyHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                var kk = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                                     (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
                if (kk != Vector2.zero) AddLook(kk * 420f * Time.unscaledDeltaTime);
#endif
            }

            bool wantBreath = Scoped && InputEnabled && (breathKeyHeld || breathButtonHeld);
            if (wantBreath && Stamina > 0)
            {
                HoldingBreath = true;
                Stamina = Mathf.Max(0, Stamina - dt / BreathSeconds);
            }
            else
            {
                HoldingBreath = false;
                Stamina = Mathf.Min(1, Stamina + dt / (wantBreath ? 7f : 3.2f));
            }
            if (HoldingBreath)
            {
                heartbeatTimer -= dt;
                if (heartbeatTimer <= 0) { Sfx.Play(Sfx.Heartbeat, 0.32f); heartbeatTimer = 0.95f; }
            }
            UpdateWind();
            recoil = Mathf.MoveTowards(recoil, 0, dt * 5.5f);
            shake = Mathf.MoveTowards(shake, 0, dt * 2.5f);
            Apply(false);

            if (Scoped && InputEnabled)
            {
                var aim = Cam.transform.forward;
                var spec = CurrentSpec;
                var v = Ballistics.InitialVelocity(aim, Cam.transform.right, spec.velocity, ZeroRange);
                // the same help the trigger gets: the dot shows where this very round would land (worked out only while
                // the dot is shown; the trigger works it out again anyway)
                if (ShowImpact)
                {
                    AssistPrediction = Ballistics.PredictAssisted(Cam.transform.position, ref v, Wind, mask, spec.pierce, spec.ammo, Diff.magnet, Diff.magnetPerM,
                                                                  Cam.transform.right, Cam.transform.up, out var raw);
                    RawPrediction = raw;
                }
                else
                {
                    RawPrediction = Ballistics.PredictMoving(Cam.transform.position, v, Wind, mask, spec.pierce);
                    AssistPrediction = RawPrediction;
                }
                HasPrediction = AssistPrediction.hit;
                RangeToCenter = Physics.Raycast(Cam.transform.position, aim, out var h, 3500, mask, QueryTriggerInteraction.Ignore) ? h.distance : -1;
                UpdateScan(dt);
            }
            else
            {
                HasPrediction = false;
                RangeToCenter = -1;
            }
        }

        void UpdateScan(float dt)
        {
            NPC best = null;
            float tol = Mathf.Max(0.35f, Cam.fieldOfView * 0.075f);
            float bestAng = tol;
            var cp = Cam.transform.position;
            var fwd = Cam.transform.forward;
            foreach (var n in NPC.All)
            {
                if (n == null || n.Dead || n.Escaped || n.IsHidden) continue;
                var c = n.ChestPoint;
                float a1 = Vector3.Angle(fwd, c - cp);
                float a2 = Vector3.Angle(fwd, n.AimPoint - cp);
                float a = Mathf.Min(a1, a2);
                if (a < bestAng && !Physics.Linecast(cp, c, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    bestAng = a;
                    best = n;
                }
            }
            if (best != Hovered) { Hovered = best; ScanProgress = 0; }
            if (Hovered != null && !Hovered.Identified)
            {
                // thermal makes people stand out: the scan locks faster
                ScanProgress += dt / (Vision == VisionMode.Thermal ? 0.25f : 0.4f);
                if (ScanProgress >= 1f)
                {
                    Hovered.Identified = true;
                    Sfx.Play(Sfx.Scan, 0.6f);
                    Scanned?.Invoke(Hovered);
                }
            }
            else if (Hovered == null) ScanProgress = 0;
        }

        void Apply(bool instant)
        {
            if (Cam == null) return;
            float t = Time.time + swaySeed;
            float amp = Scoped ? (HoldingBreath ? 0.025f : (Stamina <= 0.01f ? 0.55f : 0.24f)) * StabilityMul * Weapon.sway * Diff.sway : 0.04f;
            if (Game.I != null && Game.I.InMenu) amp = 0f;              // the menu's view stands perfectly still
            // under fire and hurt the hands shake: rounds cracking past, blood loss (a held breath still steadies it most)
            var ff = Firefight.I;
            if (Scoped && ff != null) amp *= 1f + ((ff.UnderFire ? 0.1f : 0f) + ff.Hurt * 0.15f) * ComposureMul;
            float swayX = (Mathf.PerlinNoise(t * 0.33f, 0.1f) - 0.5f) * 2f * amp + Mathf.Sin(t * 1.2f) * amp * 0.25f;
            float swayY = (Mathf.PerlinNoise(0.7f, t * 0.29f) - 0.5f) * 2f * amp + Mathf.Sin(t * 0.85f) * amp * 0.35f;
            float shX = (UnityEngine.Random.value - 0.5f) * shake * 2f, shY = (UnityEngine.Random.value - 0.5f) * shake * 2f;
            // ducking: the eye sinks behind the parapet; shot dead: it sinks further and rolls over
            coverK = instant ? (InCover ? 1f : 0f) : Mathf.MoveTowards(coverK, InCover ? 1f : 0f, Time.unscaledDeltaTime * 4.5f);
            if (collapsing) collapseK = Mathf.MoveTowards(collapseK, 1f, Time.unscaledDeltaTime * 0.9f);
            float ck = collapseK * collapseK;
            transform.rotation = Quaternion.Euler(-(pitch + swayY + recoil + shY) + ck * 38f, baseYaw + yaw + swayX + shX, ck * 72f);
            // the eye drops straight down (world space), whatever the view's pitch and roll
            if (Cam.transform.parent == transform)
                Cam.transform.localPosition = transform.InverseTransformDirection(Vector3.down * (CoverDrop * Util.EaseInOut(coverK) + ck * 1.1f));
            float fov = Scoped ? 2f * Mathf.Atan(Mathf.Tan(30f * Mathf.Deg2Rad) / Zoom) * Mathf.Rad2Deg : HipFov;
            Cam.fieldOfView = instant ? fov : Mathf.Lerp(Cam.fieldOfView, fov, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 16f));
            scopeBlend = Mathf.MoveTowards(scopeBlend, Scoped ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            if (instant) scopeBlend = Scoped ? 1f : 0f;
            if (rifleRoot != null)
            {
                var p = Vector3.Lerp(rifleRest, rifleScoped, Util.EaseInOut(scopeBlend));
                float bob = Mathf.Sin(Time.time * 1.3f) * 0.0025f;
                rifleRoot.localPosition = p + new Vector3(0, bob - recoil * 0.012f, -recoil * 0.035f) * ViewScale;
                rifleRoot.localRotation = Quaternion.Euler(-recoil * 2.5f, 0, 0);
            }
        }

        public void Fire()
        {
            if (!InputEnabled || Busy || KillCam.Active || Cinematic.Active) return;
            // fire from cover: come up first (no blind shots over the parapet)
            if (InCover) { SetCover(false); return; }
            if (coverK > 0.05f) return;                 // still coming up: the parapet is in the way
            if (Ammo <= 0) { Sfx.Play(Sfx.Click, 0.8f); Reload(); return; }
            var spec = CurrentSpec;
            Ammo--;
            ShotsFired++;
            nextFire = Time.time + Weapon.cycle;
            if (spec.ammo == AmmoKind.Tranquilizer) Sfx.Play(Sfx.DartFire, 0.9f, 1f);
            else if (Weapon.silenced) Sfx.Play(Sfx.ShotSilenced, 0.9f * Weapon.shotVolume / 0.45f, Weapon.shotPitch / 1.4f);
            else
            {
                Sfx.Play(Sfx.Shot, Mathf.Min(1f, Weapon.shotVolume), Weapon.shotPitch);
                Sfx.Play(Sfx.ShotTail, 0.6f * Weapon.shotVolume, Weapon.shotPitch);
            }
            Util.Vibrate();
            if (spec.ammo != AmmoKind.Standard)
            {
                Gear.ConsumeAmmo(spec.ammo);
                gearCheckAt = -1f;
                if (Gear.AmmoAvailable(spec.ammo) <= 0) { AmmoType = AmmoKind.Standard; Changed?.Invoke(); }
            }

            var origin = Cam.transform.position;
            var aim = Cam.transform.forward;
            var vel = Ballistics.InitialVelocity(aim, Cam.transform.right, spec.velocity, ZeroRange);
            // a round grazing a fair target leaves along the line that meets him (a few centimetres of help at most)
            var predicted = Ballistics.PredictAssisted(origin, ref vel, Wind, mask, spec.pierce, spec.ammo, Diff.magnet, Diff.magnetPerM,
                                                       Cam.transform.right, Cam.transform.up, out _);
            LastShotZero = ZeroRange;

            var holder = new GameObject("Bullet");
            holder.layer = Layers.IgnoreRaycast;
            holder.transform.position = origin + aim * 0.6f;
            holder.transform.rotation = Quaternion.LookRotation(vel);
            var body = new GameObject("BulletMesh");
            body.layer = Layers.IgnoreRaycast;
            body.transform.SetParent(holder.transform, false);
            body.transform.localScale = spec.ammo == AmmoKind.Tranquilizer ? new Vector3(0.8f, 0.8f, 1.6f) : (Weapon.power >= 2f ? Vector3.one * 1.6f : Vector3.one);
            body.AddComponent<MeshFilter>().sharedMesh = BulletMesh;
            var bmr = body.AddComponent<MeshRenderer>();
            var bm = Res.Material("bullet");
            if (bm != null) bmr.sharedMaterial = bm;
            bmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var tint = Gear.AmmoOf(spec.ammo).tint;
            var tr = holder.AddComponent<TrailRenderer>();
            tr.time = 0.18f;
            tr.startWidth = Weapon.power >= 2f ? 0.03f : 0.02f;
            tr.endWidth = 0f;
            tr.sharedMaterial = FX.AddMat;
            tr.startColor = new Color(tint.r, tint.g * 0.95f, tint.b * 0.85f, 0.45f);
            tr.endColor = new Color(tint.r, tint.g * 0.95f, tint.b * 0.85f, 0f);
            tr.minVertexDistance = 0.4f;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var p = holder.AddComponent<Projectile>();
            p.velocity = vel;
            p.wind = Wind;
            p.origin = origin;
            p.mask = mask;
            p.spec = spec;
            p.weapon = Weapon;
            p.predicted = predicted;
            // what the spotter will need to call a miss: the shooter's frame and breath, the man's pace, as they were now
            p.shotRight = Cam.transform.right;
            p.shotBreath = HoldingBreath;
            p.nearVel = predicted.nearNpc != null ? predicted.nearNpc.Velocity : Vector3.zero;

            recoil = 2.4f * Weapon.recoil;
            Shake(0.35f * Mathf.Sqrt(Weapon.recoil));
            if (!Scoped && muzzle != null && !Weapon.silenced && spec.ammo != AmmoKind.Tranquilizer) FX.MuzzleFlash(muzzle, Vector3.zero);
            StartCoroutine(Weapon.semiAuto ? SemiCycle() : BoltCycle());
            Fired?.Invoke(p, predicted);
        }

        static Mesh bulletMesh;

        /// <summary>7.62 mm spitzer bullet (lathed profile, +Z forward) — it is the hero of the kill cam.</summary>
        static Mesh BulletMesh
        {
            get
            {
                if (bulletMesh != null) return bulletMesh;
                // profile (z, radius) from the base to the tip
                var prof = new[] { new Vector2(0f, 0f), new Vector2(0f, 0.0036f), new Vector2(0.0015f, 0.0039f), new Vector2(0.017f, 0.0039f),
                                   new Vector2(0.023f, 0.0034f), new Vector2(0.0275f, 0.0024f), new Vector2(0.0308f, 0.0011f), new Vector2(0.032f, 0f) };
                const int seg = 16;
                var verts = new System.Collections.Generic.List<Vector3>();
                var norms = new System.Collections.Generic.List<Vector3>();
                var tris = new System.Collections.Generic.List<int>();
                for (int i = 0; i < prof.Length - 1; i++)
                {
                    var a0 = prof[i]; var a1 = prof[i + 1];
                    var d = (a1 - a0).normalized;
                    var n2 = new Vector2(-d.y, d.x);          // outward normal in (z, r) space
                    int baseIdx = verts.Count;
                    for (int k = 0; k <= seg; k++)
                    {
                        float ang = k * Mathf.PI * 2f / seg;
                        float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                        verts.Add(new Vector3(c * a0.y, sn * a0.y, a0.x - 0.016f));
                        verts.Add(new Vector3(c * a1.y, sn * a1.y, a1.x - 0.016f));
                        var n = new Vector3(c * n2.y, sn * n2.y, n2.x).normalized;
                        norms.Add(n); norms.Add(n);
                    }
                    for (int k = 0; k < seg; k++)
                    {
                        int v = baseIdx + k * 2;
                        tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
                        tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 3);
                    }
                }
                bulletMesh = new Mesh { name = "bullet" };
                bulletMesh.SetVertices(verts);
                bulletMesh.SetNormals(norms);
                bulletMesh.SetTriangles(tris, 0);
                bulletMesh.RecalculateBounds();
                return bulletMesh;
            }
        }

        IEnumerator BoltCycle()
        {
            yield return new WaitForSeconds(0.42f);
            if (Ammo > 0) Sfx.Play(Sfx.Bolt, 0.55f);
            else Reload();
        }

        IEnumerator SemiCycle()
        {
            yield return new WaitForSeconds(0.06f);
            if (Ammo > 0) Sfx.Play(Sfx.BoltSemi, 0.5f, Weapon.power >= 2f ? 0.8f : 1f);
            else { yield return new WaitForSeconds(0.25f); Reload(); }
        }

        public void Reload()
        {
            if (Reloading || Ammo == MagSize || !InputEnabled) return;
            StartCoroutine(ReloadRoutine());
        }

        IEnumerator ReloadRoutine()
        {
            Reloading = true;
            Sfx.Play(Sfx.Reload, 0.8f, 2.0f / Mathf.Max(0.8f, Weapon.reload));
            yield return new WaitForSeconds(Weapon.reload);
            Ammo = MagSize;
            Reloading = false;
        }

        /// <summary>Screen position of a world point (for HUD markers). z &lt; 0 when behind the camera.</summary>
        public Vector3 ToScreen(Vector3 world) => Cam.WorldToScreenPoint(world);
    }
}

using UnityEngine;

namespace SilentContract
{
    /// <summary>Sound effects and ambience: recorded clips (generated with Higgsfield) from Resources/SC/Audio, each with a
    /// synthesised stand-in if its file is missing.</summary>
    public static class Sfx
    {
        static AudioSource ui, ui2;
        static AudioSource[] voices;          // 2D one-shot voices; each keeps its own pitch until its sound has finished
        static float[] voiceEnd;
        static AudioSource[] incoming;        // rounds cracking past / hitting the parapet: their own voices, so a firefight
        static float[] incomingEnd;           // never cuts off the player's rifle, bolt or the UI (silent while paused)
        static AudioSource ambSea, ambRain, alarm;
        static GameObject host;
        public static AudioClip CarAlarm;
        public static AudioClip Shot, ShotTail, Bolt, Reload, Impact, ImpactMetal, Flesh, Splash, Whoosh, Explosion, Rain, Click, Confirm, Back,
            Heartbeat, Sea, Gull, Horn, Siren, Coin, Fail, Success, Scan;
        public static AudioClip ShotSilenced, BoltSemi, DartFire, Dart, Groan, GroanF, Alert, Drone, Pistol, VisionOn, VisionThermal, Radio,
            Briefing, Equip, Thud, PowerDown;
        // city centre
        public static AudioClip Glass, Flash, Applause, Crowd, TramBell, Rotor, CityHum, Cheer, CarHorn;
        static AudioSource ambCity, ambCrowd;
        static bool cityAmb;
        // mountain: wind over the snow (gusts), crows, a snowcat far off, the gondola's hum, fireworks
        public static AudioClip Wind, Gust, Crow, Firework, FireworkCrackle, Gondola, Avalanche, Snowmobile;
        // under fire: the enemy's guns heard from the nest, rounds passing close (supersonic crack, subsonic whiz), rounds
        // striking the parapet, a ricochet, being hit
        public static AudioClip Rifle, Smg, Snap, Whiz, Ricochet, Hurt, EarRing, Debris, Body;
        static AudioSource ambWind;
        static bool mountainAmb;
        static float windLevel = 0.3f;
        const int SR = 32000;
        static float nextGull, nextHorn;

        public static void Init(GameObject h)
        {
            host = h;
            ui = h.AddComponent<AudioSource>();
            ui.playOnAwake = false;
            ui.spatialBlend = 0;
            ui2 = h.AddComponent<AudioSource>();
            ui2.playOnAwake = false;
            ui2.spatialBlend = 0;
            // menu clicks must still be heard while the game (and the listener) is paused
            ui.ignoreListenerPause = true;
            ui2.ignoreListenerPause = true;
            voices = new AudioSource[6];
            voiceEnd = new float[6];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = i == 0 ? ui : (i == 1 ? ui2 : h.AddComponent<AudioSource>());
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0;
                voices[i].ignoreListenerPause = true;
            }
            incoming = new AudioSource[3];
            incomingEnd = new float[3];
            for (int i = 0; i < incoming.Length; i++)
            {
                incoming[i] = h.AddComponent<AudioSource>();
                incoming[i].playOnAwake = false;
                incoming[i].spatialBlend = 0;
            }

            Shot = Make("shot", 0.9f, (t, r) =>
            {
                float crack = (r * 2 - 1) * Mathf.Exp(-t * 60) * 1.0f;
                float body = Mathf.Sin(2 * Mathf.PI * (48 + 70 * Mathf.Exp(-t * 25)) * t) * Mathf.Exp(-t * 8) * 1.1f;
                float tail = (r * 2 - 1) * Mathf.Exp(-t * 5.5f) * 0.18f;
                return (crack + body + tail) * 0.85f;
            }, lowpass: 0.55f);
            ShotTail = Make("shot_tail", 2.6f, (t, r) =>
            {
                // rolling echo from the harbour buildings
                float e = 0;
                e += (r * 2 - 1) * Mathf.Exp(-Mathf.Max(0, t - 0.28f) * 6f) * (t > 0.28f ? 0.32f : 0);
                e += (r * 2 - 1) * Mathf.Exp(-Mathf.Max(0, t - 0.62f) * 5f) * (t > 0.62f ? 0.22f : 0);
                e += (r * 2 - 1) * Mathf.Exp(-Mathf.Max(0, t - 1.1f) * 4f) * (t > 1.1f ? 0.14f : 0);
                return e;
            }, lowpass: 0.07f);
            Bolt = Make("bolt", 0.75f, (t, r) =>
            {
                float c1 = t < 0.025f ? (r * 2 - 1) * (1 - t / 0.025f) : 0;
                float slide = t > 0.1f && t < 0.3f ? (r * 2 - 1) * 0.22f * Mathf.Sin((t - 0.1f) / 0.2f * Mathf.PI) : 0;
                float c2 = t > 0.44f && t < 0.47f ? (r * 2 - 1) * (1 - (t - 0.44f) / 0.03f) : 0;
                float tick = Mathf.Sin(2 * Mathf.PI * 2600 * t) * ((t < 0.015f || (t > 0.44f && t < 0.455f)) ? 0.35f : 0);
                return (c1 + slide + c2) * 0.6f + tick;
            });
            Reload = Make("reload", 1.4f, (t, r) =>
            {
                float a = t > 0.05f && t < 0.09f ? (r * 2 - 1) * 0.7f : 0;
                float b = t > 0.6f && t < 0.64f ? (r * 2 - 1) * 0.9f : 0;
                float c = t > 1.1f && t < 1.13f ? (r * 2 - 1) * 0.8f : 0;
                return a + b + c + Mathf.Sin(2 * Mathf.PI * 1900 * t) * ((t > 0.6f && t < 0.61f) ? 0.4f : 0);
            });
            Impact = Make("impact", 0.3f, (t, r) => (r * 2 - 1) * Mathf.Exp(-t * 30) * 0.7f + Mathf.Sin(2 * Mathf.PI * 160 * t) * Mathf.Exp(-t * 28) * 0.4f, lowpass: 0.5f);
            ImpactMetal = Make("impact_metal", 0.6f, (t, r) => (r * 2 - 1) * Mathf.Exp(-t * 40) * 0.5f + (Mathf.Sin(2 * Mathf.PI * 1250 * t) + Mathf.Sin(2 * Mathf.PI * 1870 * t) * 0.6f) * Mathf.Exp(-t * 9) * 0.25f);
            Flesh = Make("flesh", 0.35f, (t, r) => Mathf.Sin(2 * Mathf.PI * (85 - 40 * t) * t) * Mathf.Exp(-t * 13) * 0.9f + (r * 2 - 1) * Mathf.Exp(-t * 45) * 0.35f, lowpass: 0.4f);
            Splash = Make("splash", 1.0f, (t, r) => (r * 2 - 1) * Mathf.Exp(-t * 4) * (1 - Mathf.Exp(-t * 60)) * 0.7f, lowpass: 0.25f);
            // car alarm: two-tone horn, loops seamlessly (every tone change happens at a zero crossing)
            CarAlarm = Make("car_alarm", 1.0f, (t, r) =>
            {
                float f = Mathf.Repeat(t, 0.5f) < 0.25f ? 640f : 472f;
                float ph = Mathf.Sin(2 * Mathf.PI * f * t);
                return (Mathf.Sign(ph) * 0.55f + ph * 0.45f) * 0.42f;
            }, lowpass: 0.3f);
            Whoosh = Make("whoosh", 1.6f, (t, r) =>
            {
                float env = Mathf.Sin(Mathf.Clamp01(t / 1.6f) * Mathf.PI);
                return (r * 2 - 1) * env * 0.35f + Mathf.Sin(2 * Mathf.PI * (180 + 260 * t) * t) * env * 0.07f;
            }, lowpass: 0.12f);
            Explosion = Make("explosion", 2.6f, (t, r) => (r * 2 - 1) * Mathf.Exp(-t * 2.0f) * 1.0f + Mathf.Sin(2 * Mathf.PI * 38 * t) * Mathf.Exp(-t * 3.5f) * 0.9f, lowpass: 0.07f);
            Rain = Make("rain", 3f, (t, r) => (r * 2 - 1) * 0.2f, lowpass: 0.35f, loopFade: true);
            Sea = Make("sea", 6f, (t, r) => (r * 2 - 1) * (0.16f + 0.08f * Mathf.Sin(2 * Mathf.PI * t / 6f) + 0.05f * Mathf.Sin(2 * Mathf.PI * t / 1.5f)), lowpass: 0.03f, loopFade: true);
            Gull = Make("gull", 0.9f, (t, r) =>
            {
                float f = 1400 + 700 * Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI) - 500 * t;
                float env = t < 0.4f ? Mathf.Sin(t / 0.4f * Mathf.PI) : (t > 0.5f && t < 0.85f ? Mathf.Sin((t - 0.5f) / 0.35f * Mathf.PI) * 0.6f : 0);
                return Mathf.Sin(2 * Mathf.PI * f * t + Mathf.Sin(2 * Mathf.PI * 30 * t) * 2f) * env * 0.25f;
            });
            Horn = Make("horn", 3.2f, (t, r) =>
            {
                float env = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((3.2f - t) / 0.6f);
                float f = 92 + Mathf.Sin(2 * Mathf.PI * 4 * t) * 0.6f;
                float s = Mathf.Sin(2 * Mathf.PI * f * t) + 0.5f * Mathf.Sin(4 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(6 * Mathf.PI * f * t);
                return s * env * 0.18f;
            }, lowpass: 0.3f);
            Siren = Make("siren", 3f, (t, r) =>
            {
                float f = 650 + 350 * Mathf.Sin(2 * Mathf.PI * t / 1.5f);
                return Mathf.Sin(2 * Mathf.PI * f * t) * 0.18f;
            }, loopFade: true);
            Click = Make("click", 0.06f, (t, r) => Mathf.Sin(2 * Mathf.PI * 1700 * t) * Mathf.Exp(-t * 70) * 0.45f);
            Confirm = Make("confirm", 0.22f, (t, r) => (Mathf.Sin(2 * Mathf.PI * 880 * t) * (t < 0.09f ? 1 : 0) + Mathf.Sin(2 * Mathf.PI * 1320 * t) * (t >= 0.09f ? 1 : 0)) * Mathf.Exp(-t * 9) * 0.35f);
            Back = Make("back", 0.18f, (t, r) => Mathf.Sin(2 * Mathf.PI * (900 - 1800 * t) * t) * Mathf.Exp(-t * 14) * 0.35f);
            Heartbeat = Make("heartbeat", 0.9f, (t, r) =>
            {
                float b1 = Mathf.Sin(2 * Mathf.PI * 50 * t) * Mathf.Exp(-t * 25);
                float t2 = t - 0.22f;
                float b2 = t2 > 0 ? Mathf.Sin(2 * Mathf.PI * 45 * t2) * Mathf.Exp(-t2 * 25) * 0.7f : 0;
                return (b1 + b2) * 0.9f;
            });
            Coin = Make("coin", 0.5f, (t, r) => (Mathf.Sin(2 * Mathf.PI * 1568 * t) * (t < 0.08f ? 1 : 0) + Mathf.Sin(2 * Mathf.PI * 2093 * t) * (t >= 0.08f ? 1 : 0)) * Mathf.Exp(-t * 7) * 0.3f);
            Fail = Make("fail", 1.0f, (t, r) => (Mathf.Sin(2 * Mathf.PI * (330 - 120 * t) * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * (220 - 80 * t) * t)) * Mathf.Exp(-t * 2.5f) * 0.28f);
            Success = Make("success", 1.2f, (t, r) =>
            {
                float f = t < 0.15f ? 523 : (t < 0.3f ? 659 : (t < 0.45f ? 784 : 1047));
                return Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-(t % 0.15f) * 6) * 0.28f * (t < 1.0f ? 1 : Mathf.Clamp01((1.2f - t) / 0.2f));
            });
            Scan = Make("scan", 0.35f, (t, r) => Mathf.Sin(2 * Mathf.PI * (1200 + 1600 * t) * t) * Mathf.Exp(-t * 6) * 0.18f);

            // ---- suppressed rifle: no supersonic-crack bark, a dull "thup" and the action
            ShotSilenced = Make("shot_silenced", 0.5f, (t, r) =>
            {
                float puff = (r * 2 - 1) * Mathf.Exp(-t * 38) * 0.75f;
                float thump = Mathf.Sin(2 * Mathf.PI * (95 + 60 * Mathf.Exp(-t * 30)) * t) * Mathf.Exp(-t * 16) * 0.6f;
                float tick = t > 0.035f && t < 0.05f ? (r * 2 - 1) * 0.35f : 0f;
                return puff + thump + tick;
            }, lowpass: 0.3f);
            // semi-automatic action: short spring-and-steel clack
            BoltSemi = Make("bolt_semi", 0.22f, (t, r) =>
                (r * 2 - 1) * Mathf.Exp(-t * 90) * 0.55f + Mathf.Sin(2 * Mathf.PI * 2200 * t) * Mathf.Exp(-t * 60) * 0.25f +
                (t > 0.07f ? (r * 2 - 1) * Mathf.Exp(-(t - 0.07f) * 110) * 0.4f : 0f));
            // tranquilizer dart: compressed gas
            DartFire = Make("dart_fire", 0.4f, (t, r) => (r * 2 - 1) * Mathf.Exp(-t * 22) * (1 - Mathf.Exp(-t * 400)) * 0.6f, lowpass: 0.45f);
            Dart = Make("dart", 0.18f, (t, r) => Mathf.Sin(2 * Mathf.PI * 1900 * t) * Mathf.Exp(-t * 55) * 0.3f + (r * 2 - 1) * Mathf.Exp(-t * 80) * 0.25f);
            // pained groans: glottal pulses through two vowel formants, pitch falling
            Groan = MakeVoice("groan", 1.1f, 125f, 85f, 650f, 1080f);
            GroanF = MakeVoice("groan_f", 1.0f, 240f, 175f, 820f, 1350f);
            // body hitting the ground
            Thud = Make("thud", 0.4f, (t, r) => Mathf.Sin(2 * Mathf.PI * (70 - 30 * t) * t) * Mathf.Exp(-t * 14) * 0.8f + (r * 2 - 1) * Mathf.Exp(-t * 40) * 0.25f, lowpass: 0.25f);
            // HUD warning: two short high beeps
            Alert = Make("alert", 0.42f, (t, r) =>
            {
                bool on = t < 0.12f || (t > 0.2f && t < 0.32f);
                return on ? Mathf.Sin(2 * Mathf.PI * 1560 * t) * 0.32f : 0f;
            });
            // recon drone flying over: rotor buzz with tremolo, rising and fading
            Drone = Make("drone", 3.2f, (t, r) =>
            {
                float env = Mathf.Sin(Mathf.Clamp01(t / 3.2f) * Mathf.PI);
                float f = 175f + 25f * Mathf.Sin(2 * Mathf.PI * 0.6f * t);
                float saw = 2f * Mathf.Repeat(f * t, 1f) - 1f;
                return (saw * 0.22f * (0.75f + 0.25f * Mathf.Sin(2 * Mathf.PI * 31 * t)) + (r * 2 - 1) * 0.06f) * env;
            }, lowpass: 0.35f);
            // handgun report at a distance
            Pistol = Make("pistol", 0.9f, (t, r) =>
            {
                float crack = (r * 2 - 1) * Mathf.Exp(-t * 70) * 0.9f;
                float body = Mathf.Sin(2 * Mathf.PI * (140 + 120 * Mathf.Exp(-t * 30)) * t) * Mathf.Exp(-t * 14) * 0.6f;
                float tail = (r * 2 - 1) * Mathf.Exp(-t * 6f) * 0.12f;
                return crack + body + tail;
            }, lowpass: 0.5f);
            // night vision tube powering up: rising whine
            VisionOn = Make("vision_on", 0.8f, (t, r) =>
            {
                float f = 2400f + 5200f * Mathf.Clamp01(t / 0.6f);
                return Mathf.Sin(2 * Mathf.PI * f * t) * 0.07f * Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((0.8f - t) / 0.25f) +
                       (t < 0.02f ? (r * 2 - 1) * 0.3f : 0f);
            });
            // thermal: relay click and a low sensor hum
            VisionThermal = Make("vision_thermal", 0.7f, (t, r) =>
                (t < 0.015f ? (r * 2 - 1) * 0.5f : 0f) + Mathf.Sin(2 * Mathf.PI * 120 * t) * 0.1f * Mathf.Clamp01((0.7f - t) / 0.4f) +
                Mathf.Sin(2 * Mathf.PI * 3100 * t) * 0.03f * Mathf.Exp(-t * 6));
            // radio squelch: click, static burst
            Radio = Make("radio", 0.32f, (t, r) =>
                (t < 0.01f ? 0.5f : 0f) + (r * 2 - 1) * 0.22f * Mathf.Exp(-t * 9) * (t < 0.25f ? 1f : 0f), lowpass: 0.6f);
            // briefing swell under the intro: a low pad that breathes in
            Briefing = Make("briefing", 6f, (t, r) =>
            {
                float env = Mathf.Clamp01(t / 2.2f) * Mathf.Clamp01((6f - t) / 1.5f);
                float s1 = Mathf.Sin(2 * Mathf.PI * 55f * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 82.4f * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * 110.2f * t);
                return (s1 * 0.12f + (r * 2 - 1) * 0.015f) * env * (0.85f + 0.15f * Mathf.Sin(2 * Mathf.PI * 0.4f * t));
            }, lowpass: 0.2f);
            // equipping a rifle in the armory: bolt rack
            Equip = Make("equip", 0.6f, (t, r) =>
            {
                float a = t < 0.03f ? (r * 2 - 1) * (1 - t / 0.03f) : 0f;
                float b = t > 0.22f && t < 0.26f ? (r * 2 - 1) * 0.9f : 0f;
                float ring = Mathf.Sin(2 * Mathf.PI * 2700 * t) * (t > 0.22f ? Mathf.Exp(-(t - 0.22f) * 30) * 0.2f : 0f);
                return a * 0.7f + b * 0.6f + ring;
            });

            // power cut: a breaker clunk, then the transformer hum winds down
            PowerDown = Make("power_down", 2.4f, (t, r) =>
            {
                float clunk = t < 0.05f ? (r * 2 - 1) * (1 - t / 0.05f) * 0.9f : 0f;
                float f = 100f * Mathf.Exp(-t * 0.9f) + 18f;
                float hum = Mathf.Sin(2 * Mathf.PI * f * t) * 0.35f * Mathf.Exp(-t * 1.2f) + Mathf.Sin(4 * Mathf.PI * f * t) * 0.12f * Mathf.Exp(-t * 1.6f);
                return clunk + hum;
            }, lowpass: 0.4f);

            // a window pane bursting: sharp crack, then the falling shards tinkle
            Glass = Make("glass", 1.6f, (t, r) =>
            {
                float crack = (r * 2 - 1) * Mathf.Exp(-t * 55f) * 0.9f;
                float tink = 0f;
                for (int k = 0; k < 7; k++)
                {
                    float t0 = 0.05f + k * 0.13f + 0.04f * Mathf.Sin(k * 7.3f);
                    if (t < t0) continue;
                    float f = 3200f + 1700f * Mathf.Repeat(k * 0.618f, 1f);
                    tink += Mathf.Sin(2 * Mathf.PI * f * (t - t0)) * Mathf.Exp(-(t - t0) * 38f) * 0.22f;
                }
                float hiss = (r * 2 - 1) * Mathf.Exp(-t * 6f) * 0.12f;
                return crack + tink + hiss;
            });
            // press camera: capacitor whine + shutter click
            Flash = Make("flash", 0.5f, (t, r) =>
            {
                float click = t < 0.012f ? (r * 2 - 1) * 0.8f : 0f;
                float whine = Mathf.Sin(2 * Mathf.PI * (5200f + 3000f * t) * t) * Mathf.Exp(-t * 7f) * 0.05f;
                return click + whine;
            });
            // crowd: many voices (band-limited noise with slow swells), loopable
            Crowd = Make("crowd", 6f, (t, r) =>
            {
                float sw = 0.6f + 0.25f * Mathf.Sin(2 * Mathf.PI * t / 3f) + 0.15f * Mathf.Sin(2 * Mathf.PI * t / 0.7f);
                return (r * 2 - 1) * 0.22f * sw;
            }, lowpass: 0.09f, loopFade: true);
            // applause: dense random claps
            Applause = Make("applause", 4f, (t, r) =>
            {
                float c = Mathf.Repeat(t * 37f + Mathf.Sin(t * 91f) * 0.3f, 1f) < 0.08f ? (r * 2 - 1) : (r * 2 - 1) * 0.35f;
                float env = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((4f - t) / 1.2f);
                return c * env * 0.35f;
            }, lowpass: 0.6f);
            Cheer = Make("cheer", 3f, (t, r) =>
            {
                float env = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((3f - t) / 1.4f);
                float vox = Mathf.Sin(2 * Mathf.PI * (330f + 40f * Mathf.Sin(t * 5f)) * t) * 0.08f + Mathf.Sin(2 * Mathf.PI * 520f * t) * 0.05f;
                return ((r * 2 - 1) * 0.3f + vox) * env;
            }, lowpass: 0.25f);
            // tram bell: two strokes of a bright bell
            TramBell = Make("tram_bell", 1.4f, (t, r) =>
            {
                float b = 0f;
                foreach (float t0 in new[] { 0f, 0.32f })
                {
                    if (t < t0) continue;
                    float u = t - t0;
                    b += (Mathf.Sin(2 * Mathf.PI * 1650f * u) + 0.6f * Mathf.Sin(2 * Mathf.PI * 2420f * u) + 0.35f * Mathf.Sin(2 * Mathf.PI * 4100f * u)) * Mathf.Exp(-u * 5.5f) * 0.25f;
                }
                return b;
            });
            // helicopter rotor: blade slap at ~5 Hz over a turbine whine, loopable
            Rotor = Make("rotor", 2f, (t, r) =>
            {
                float ph = Mathf.Repeat(t * 5f, 1f);
                float slap = Mathf.Exp(-ph * 9f) * (r * 2 - 1) * 0.55f + Mathf.Sin(2 * Mathf.PI * 34f * t) * Mathf.Exp(-ph * 6f) * 0.35f;
                float whine = Mathf.Sin(2 * Mathf.PI * 1180f * t) * 0.03f;
                return slap + whine;
            }, lowpass: 0.35f, loopFade: true);
            // distant city: traffic rumble + far voices
            CityHum = Make("city_hum", 6f, (t, r) => (r * 2 - 1) * (0.14f + 0.04f * Mathf.Sin(2 * Mathf.PI * t / 6f)), lowpass: 0.025f, loopFade: true);
            CarHorn = Make("car_horn", 0.6f, (t, r) =>
            {
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((0.6f - t) / 0.08f);
                float s1 = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 415f * t)) * 0.5f + Mathf.Sin(2 * Mathf.PI * 415f * t) * 0.5f;
                float s2 = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 523f * t)) * 0.5f + Mathf.Sin(2 * Mathf.PI * 523f * t) * 0.5f;
                return (s1 + s2) * env * 0.16f;
            }, lowpass: 0.3f);

            // mountain wind: slow swells of filtered noise (a looping bed) and separate gusts with a hiss on top
            Wind = Make("wind", 9f, (t, r) => (r * 2 - 1) * (0.13f + 0.07f * Mathf.Sin(2 * Mathf.PI * t / 9f) + 0.05f * Mathf.Sin(2 * Mathf.PI * t / 2.25f + 1.3f)), lowpass: 0.02f, loopFade: true);
            Gust = Make("gust", 3.2f, (t, r) =>
            {
                float env = Mathf.Sin(Mathf.Clamp01(t / 3.2f) * Mathf.PI);
                env *= env;
                return (r * 2 - 1) * env * 0.5f;
            }, lowpass: 0.06f);
            Crow = Make("crow", 0.5f, (t, r) =>
            {
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 7f);
                float f = 520f - 160f * t;
                float buzz = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * t)) * 0.5f + (r * 2 - 1) * 0.35f;
                return buzz * env * 0.5f;
            }, lowpass: 0.25f);
            Gondola = Make("gondola", 4f, (t, r) => (Mathf.Sin(2 * Mathf.PI * 50f * t) * 0.18f + Mathf.Sin(2 * Mathf.PI * 100f * t) * 0.08f + (r * 2 - 1) * 0.06f) *
                                                   (0.85f + 0.15f * Mathf.Sin(2 * Mathf.PI * t * 1.5f)), lowpass: 0.2f, loopFade: true);
            Firework = Make("firework", 2.4f, (t, r) =>
            {
                // launch whistle, then the bang and its rolling echo
                if (t < 0.9f) return Mathf.Sin(2 * Mathf.PI * (900f + 1400f * t) * t) * 0.06f * (t / 0.9f) + (r * 2 - 1) * 0.03f;
                float k = t - 0.9f;
                return (r * 2 - 1) * Mathf.Exp(-k * 3.2f) * 0.9f + Mathf.Sin(2 * Mathf.PI * 46f * k) * Mathf.Exp(-k * 5f) * 0.6f;
            }, lowpass: 0.09f);
            FireworkCrackle = Make("firework_crackle", 1.6f, (t, r) =>
            {
                float pop = (r > 0.985f ? 1f : 0f) * (r * 2 - 1);
                return pop * Mathf.Exp(-t * 1.2f) * 0.9f;
            }, lowpass: 0.5f);
            Snowmobile = Make("snowmobile", 3f, (t, r) =>
            {
                // a two-stroke engine pulling away: a rising buzz with a rough edge
                float f = 62f + 34f * Mathf.Clamp01(t / 1.6f);
                float ph = 2 * Mathf.PI * f * t;
                float saw = (ph / (2 * Mathf.PI)) % 1f * 2f - 1f;
                float env = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((3f - t) / 1.2f);
                return (saw * 0.5f + Mathf.Sin(ph * 2f) * 0.2f + (r * 2 - 1) * 0.12f) * env * 0.6f;
            }, lowpass: 0.18f);
            Avalanche = Make("avalanche", 7f, (t, r) =>
            {
                float env = Mathf.Clamp01(t / 1.2f) * Mathf.Clamp01((7f - t) / 3.5f);
                return ((r * 2 - 1) * 0.8f + Mathf.Sin(2 * Mathf.PI * 28f * t) * 0.4f) * env;
            }, lowpass: 0.03f);

            // ---- the enemy's guns, heard from far off: an assault rifle's sharp crack and rolling tail
            Rifle = Make("rifle", 1.7f, (t, r) =>
            {
                float crack = (r * 2 - 1) * Mathf.Exp(-t * 95f) * 1.0f;
                float body = Mathf.Sin(2 * Mathf.PI * (80f + 150f * Mathf.Exp(-t * 45f)) * t) * Mathf.Exp(-t * 17f) * 0.75f;
                float tail = t > 0.03f ? (r * 2 - 1) * Mathf.Exp(-(t - 0.03f) * 3.4f) * 0.17f : 0f;
                float echo = t > 0.42f ? (r * 2 - 1) * Mathf.Exp(-(t - 0.42f) * 4.5f) * 0.13f : 0f;
                return (crack + body + tail + echo) * 0.85f;
            }, lowpass: 0.5f);
            // a submachine gun: a lighter, quicker pop
            Smg = Make("smg", 1.1f, (t, r) =>
            {
                float crack = (r * 2 - 1) * Mathf.Exp(-t * 120f) * 0.9f;
                float body = Mathf.Sin(2 * Mathf.PI * (130f + 160f * Mathf.Exp(-t * 50f)) * t) * Mathf.Exp(-t * 24f) * 0.55f;
                float tail = t > 0.02f ? (r * 2 - 1) * Mathf.Exp(-(t - 0.02f) * 5f) * 0.12f : 0f;
                return (crack + body + tail) * 0.85f;
            }, lowpass: 0.6f);
            // a supersonic round passing within a few metres: the whip crack of its shock wave
            Snap = Make("snap", 0.3f, (t, r) =>
            {
                float n = t < 0.0012f ? 1f : (t < 0.0026f ? -0.85f : 0f);
                float hiss = (r * 2 - 1) * Mathf.Exp(-t * 140f) * 0.55f;
                float zing = Mathf.Sin(2 * Mathf.PI * (4200f - 2600f * t) * t) * Mathf.Exp(-t * 28f) * 0.08f;
                return n + hiss + zing;
            }, lowpass: 0.92f);
            // a slower round going past: a short tearing whiz, falling in pitch
            Whiz = Make("whiz", 0.42f, (t, r) =>
            {
                float env = Mathf.Clamp01(t / 0.13f) * Mathf.Exp(-Mathf.Max(0f, t - 0.13f) * 16f);
                float f = 2300f - 1500f * Mathf.Clamp01(t / 0.3f);
                return (Mathf.Sin(2 * Mathf.PI * f * t + (r * 2 - 1) * 0.7f) * 0.35f + (r * 2 - 1) * 0.45f) * env * 0.6f;
            }, lowpass: 0.45f);
            // a round glancing off concrete or steel
            Ricochet = Make("ricochet", 0.75f, (t, r) =>
            {
                float click = t < 0.008f ? (r * 2 - 1) * 0.9f : 0f;
                float f = 3400f * Mathf.Exp(-t * 2.4f) + 700f;
                float whine = Mathf.Sin(2 * Mathf.PI * f * t + Mathf.Sin(2 * Mathf.PI * 38f * t) * 1.4f) * Mathf.Exp(-t * 4.6f) * 0.32f;
                return click + whine;
            }, lowpass: 0.8f);
            // the nest hit: a heavy thump in the chest and a stifled grunt
            Hurt = Make("hurt", 0.7f, (t, r) =>
            {
                float thump = Mathf.Sin(2 * Mathf.PI * (62f - 30f * t) * t) * Mathf.Exp(-t * 9f) * 1.0f;
                float slap = (r * 2 - 1) * Mathf.Exp(-t * 60f) * 0.5f;
                float grunt = t > 0.05f ? Mathf.Sin(2 * Mathf.PI * (118f - 40f * t) * t) * Mathf.Sin(2 * Mathf.PI * 7f * t) * Mathf.Exp(-(t - 0.05f) * 6f) * 0.35f : 0f;
                return thump + slap + grunt;
            }, lowpass: 0.35f);
            // ringing ears after a close hit
            EarRing = Make("ear_ring", 2.8f, (t, r) => Mathf.Sin(2 * Mathf.PI * 3650f * t) * Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 1.2f) * 0.07f);
            // grit and chips of the parapet raining down
            Debris = Make("debris", 0.8f, (t, r) =>
            {
                float grain = r > 0.93f ? (r * 2 - 1) : 0f;
                float bed = (r * 2 - 1) * 0.12f;
                return (grain * 0.8f + bed) * Mathf.Exp(-t * 4.5f);
            }, lowpass: 0.55f);
            // a round striking a body nearby (heard from the nest when a gunman's shot finds a man)
            Body = Make("body", 0.3f, (t, r) => Mathf.Sin(2 * Mathf.PI * (95f - 50f * t) * t) * Mathf.Exp(-t * 16f) * 0.8f + (r * 2 - 1) * Mathf.Exp(-t * 50f) * 0.3f, lowpass: 0.4f);

            pool3D = new AudioSource[16];
            pool3DStart = new float[16];
            for (int i = 0; i < pool3D.Length; i++)
            {
                var go = new GameObject("sfx3d_" + i);
                go.transform.SetParent(h.transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0.7f;
                s.minDistance = 30;
                s.maxDistance = 900;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.dopplerLevel = 0f;
                pool3D[i] = s;
            }

            ambSea = Loop(h, Sea, 0.35f);
            ambWind = Loop(h, Wind, 0f);
            ambCity = Loop(h, CityHum, 0f);
            ambCrowd = Loop(h, Crowd, 0f);
            ambRain = Loop(h, Rain, 0f);
            alarm = Loop(h, Siren, 0f);
            AudioListener.volume = Save.Volume;
        }

        /// <summary>The recorded version of a sound (Resources/SC/Audio/&lt;name&gt;, generated with Higgsfield), or null: then
        /// the synthesised one below stands in.</summary>
        static AudioClip Recorded(string name)
        {
            var c = Resources.Load<AudioClip>("SC/Audio/" + name);
            if (c != null && c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
            return c;
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float, float> f, float lowpass = 1f, bool loopFade = false)
        {
            var rec = Recorded(name);
            if (rec != null) return rec;
            int n = (int)(SR * seconds);
            var data = new float[n];
            var rnd = new System.Random(name.GetHashCode());
            float prev = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float v = f(t, (float)rnd.NextDouble());
                prev += (v - prev) * lowpass;
                data[i] = Mathf.Clamp(prev, -1, 1);
            }
            if (loopFade)
            {
                int fade = SR / 8;
                for (int i = 0; i < fade; i++)
                {
                    float k = i / (float)fade;
                    data[i] = data[i] * k + data[n - fade + i] * (1 - k);
                }
            }
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A short vocal sound: glottal pulse train (pitch falling from f0a to f0b) through two vowel formants.</summary>
        static AudioClip MakeVoice(string name, float seconds, float f0a, float f0b, float f1, float f2)
        {
            var rec = Recorded(name);
            if (rec != null) return rec;
            int n = (int)(SR * seconds);
            var data = new float[n];
            float phase = 0f;
            // two resonators (biquad band-pass) for the formants
            float[] y1 = new float[2], y2 = new float[2];
            float Bw(float f, float bw, out float a1, out float a2)
            {
                float rr = Mathf.Exp(-Mathf.PI * bw / SR);
                a1 = 2f * rr * Mathf.Cos(2f * Mathf.PI * f / SR);
                a2 = -rr * rr;
                return 1f - rr;
            }
            float g1 = Bw(f1, 110f, out float a11, out float a12);
            float g2 = Bw(f2, 140f, out float a21, out float a22);
            var rnd = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float k = t / seconds;
                float f0 = Mathf.Lerp(f0a, f0b, k) * (1f + 0.02f * Mathf.Sin(2 * Mathf.PI * 5.5f * t));
                phase += f0 / SR;
                float src = 0f;
                if (phase >= 1f) { phase -= 1f; src = 1f; }
                src += ((float)rnd.NextDouble() * 2f - 1f) * 0.08f;               // breathiness
                float o1 = g1 * src + a11 * y1[0] + a12 * y1[1]; y1[1] = y1[0]; y1[0] = o1;
                float o2 = g2 * src + a21 * y2[0] + a22 * y2[1]; y2[1] = y2[0]; y2[0] = o2;
                float env = Mathf.Clamp01(t / 0.06f) * Mathf.Pow(Mathf.Clamp01(1f - k), 1.5f);
                data[i] = Mathf.Clamp((o1 * 1.0f + o2 * 0.55f) * env * 6f, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void Play(AudioClip c, float vol = 1f, float pitch = 1f) => Play(c, vol, pitch, 0f);

        /// <summary>A 2D sound panned left / right (-1..1): a round passing on one side of the nest.</summary>
        public static void Play(AudioClip c, float vol, float pitch, float pan)
        {
            if (voices == null || c == null) return;
            // a free voice (or the one that finishes first): re-pitching a source that is still playing would bend that sound
            float now = Time.unscaledTime;
            int pick = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voiceEnd[i] <= now) { pick = i; break; }
                if (voiceEnd[i] < voiceEnd[pick]) pick = i;
            }
            var s = voices[pick];
            if (voiceEnd[pick] > now) s.Stop();
            s.pitch = pitch;
            s.panStereo = Mathf.Clamp(pan, -1f, 1f);
            s.PlayOneShot(c, vol);
            voiceEnd[pick] = now + c.length / Mathf.Max(0.05f, Mathf.Abs(pitch));
        }

        /// <summary>Incoming fire (a round cracking past, striking the parapet): like <see cref="Play(AudioClip, float, float, float)"/>
        /// on voices of its own.</summary>
        public static void PlayIncoming(AudioClip c, float vol, float pitch, float pan)
        {
            if (c == null) return;
            if (incoming == null) { Play(c, vol, pitch, pan); return; }
            float now = Time.unscaledTime;
            int pick = 0;
            for (int i = 0; i < incoming.Length; i++)
            {
                if (incomingEnd[i] <= now) { pick = i; break; }
                if (incomingEnd[i] < incomingEnd[pick]) pick = i;
            }
            var s = incoming[pick];
            if (incomingEnd[pick] > now) s.Stop();
            s.pitch = pitch;
            s.panStereo = Mathf.Clamp(pan, -1f, 1f);
            s.PlayOneShot(c, vol);
            incomingEnd[pick] = now + c.length / Mathf.Max(0.05f, Mathf.Abs(pitch));
        }

        public static void UI(AudioClip c) => Play(c, 0.8f, 1f);

        /// <summary>Stops every 2D voice (a narration cut short by the player).</summary>
        public static void StopVoices()
        {
            if (voices == null) return;
            for (int i = 2; i < voices.Length; i++) if (voices[i] != null && voices[i].clip != null && voices[i].clip.length > 6f) voices[i].Stop();
        }

        static AudioSource[] pool3D;
        static float[] pool3DStart;

        public static void PlayAt(AudioClip c, Vector3 pos, float vol = 1f) => PlayAt(c, pos, vol, 0f, 1f);

        /// <summary>
        /// A sound in the world (pooled voices: a firefight does not create an object per shot). <paramref name="delay"/>:
        /// seconds before it is heard — a gun far off is seen to fire before its report arrives (sound covers ~340 m/s).
        /// </summary>
        public static void PlayAt(AudioClip c, Vector3 pos, float vol, float delay, float pitch = 1f)
        {
            if (c == null) return;
            if (pool3D == null)
            {
                // before Init (editor tools): a throwaway source
                var go = new GameObject("sfx");
                go.transform.position = pos;
                var s0 = go.AddComponent<AudioSource>();
                s0.clip = c;
                s0.volume = vol;
                s0.spatialBlend = 0.7f;
                s0.minDistance = 30;
                s0.maxDistance = 900;
                s0.rolloffMode = AudioRolloffMode.Linear;
                s0.Play();
                Object.Destroy(go, c.length + 0.2f);
                return;
            }
            // a free voice, else the one that started longest ago
            float now = Time.unscaledTime;
            int pick = 0;
            for (int i = 0; i < pool3D.Length; i++)
            {
                if (pool3D[i] == null) continue;
                if (!pool3D[i].isPlaying && pool3DStart[i] <= now) { pick = i; break; }
                if (pool3DStart[i] < pool3DStart[pick]) pick = i;
            }
            var s = pool3D[pick];
            if (s == null) return;
            s.Stop();
            s.transform.position = pos;
            s.clip = c;
            s.volume = vol;
            s.pitch = pitch;
            if (delay > 0.005f) s.PlayDelayed(delay); else s.Play();
            pool3DStart[pick] = now + Mathf.Max(0f, delay);
        }

        public static AudioSource Loop(GameObject h, AudioClip c, float vol)
        {
            var s = h.AddComponent<AudioSource>();
            s.clip = c;
            s.loop = true;
            s.volume = vol;
            s.spatialBlend = 0;
            s.Play();
            return s;
        }

        public static void SetRain(bool on) { if (ambRain != null) ambRain.volume = on ? 0.32f : 0f; }

        /// <summary>Background bed of the map: the harbour hears the sea, the city its traffic (and the sea further off).</summary>
        public static void SetLocationAmbience(string locationId)
        {
            cityAmb = locationId == "city";
            mountainAmb = locationId == "mountain";
            if (ambSea != null) ambSea.volume = mountainAmb ? 0f : (cityAmb ? 0.16f : 0.35f);
            if (ambCity != null) ambCity.volume = cityAmb ? 0.3f : 0f;
            if (ambWind != null) ambWind.volume = mountainAmb ? windLevel : 0f;
        }

        /// <summary>Strength of the mountain wind bed (0.3 a calm day .. 1 a blizzard).</summary>
        public static void SetWind(float level)
        {
            windLevel = Mathf.Clamp01(level);
            if (ambWind != null && mountainAmb) ambWind.volume = windLevel;
        }

        /// <summary>A crowd murmur under the scene (rally, gala, café terrace): 0 = off.</summary>
        public static void SetCrowd(float volume) { if (ambCrowd != null) ambCrowd.volume = Mathf.Clamp01(volume); }

        /// <summary>A looping positional sound on a moving thing (helicopter rotor, engine).</summary>
        public static AudioSource Loop3D(Transform at, AudioClip c, float vol, float maxDistance = 400f)
        {
            if (at == null || c == null) return null;
            var s = at.gameObject.AddComponent<AudioSource>();
            s.clip = c;
            s.loop = true;
            s.volume = vol;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 20f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            s.Play();
            return s;
        }
        public static void SetAlarm(bool on) { if (alarm != null) alarm.volume = on ? 0.22f : 0f; }

        /// <summary>Silences the game while a full-screen ad plays (the ad brings its own sound).</summary>
        public static void Mute(bool on) { AudioListener.pause = on; }

        /// <summary>Occasional seagulls and ship horns.</summary>
        public static void TickAmbience(bool night)
        {
            if (host == null) return;
            if (mountainAmb)
            {
                // gusts over the ridge, a crow by day
                if (Time.unscaledTime > nextGull)
                {
                    nextGull = Time.unscaledTime + Random.Range(4f, 11f) / Mathf.Max(0.4f, windLevel);
                    Play(Gust, Random.Range(0.12f, 0.3f) * (0.4f + windLevel), Random.Range(0.8f, 1.2f));
                }
                if (Time.unscaledTime > nextHorn)
                {
                    nextHorn = Time.unscaledTime + Random.Range(14f, 34f);
                    if (!night && windLevel < 0.8f) Play(Crow, Random.Range(0.05f, 0.11f), Random.Range(0.9f, 1.1f));
                }
                return;
            }
            if (Time.unscaledTime > nextGull)
            {
                nextGull = Time.unscaledTime + Random.Range(5f, 13f);
                if (!night) Play(Gull, Random.Range(0.12f, 0.3f) * (cityAmb ? 0.6f : 1f), Random.Range(0.85f, 1.2f));
            }
            if (Time.unscaledTime > nextHorn)
            {
                if (cityAmb)
                {
                    // the city: a car horn far away now and then, the tram bell from the promenade
                    nextHorn = Time.unscaledTime + Random.Range(12f, 30f);
                    if (Random.value < 0.6f) Play(CarHorn, Random.Range(0.05f, 0.12f), Random.Range(0.92f, 1.08f));
                    else Play(TramBell, Random.Range(0.06f, 0.12f), Random.Range(0.97f, 1.03f));
                }
                else
                {
                    nextHorn = Time.unscaledTime + Random.Range(35f, 80f);
                    if (Time.unscaledTime > 20f) Play(Horn, 0.35f, Random.Range(0.9f, 1.05f));
                }
            }
        }
    }
}

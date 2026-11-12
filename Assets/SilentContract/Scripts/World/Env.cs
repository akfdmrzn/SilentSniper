using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilentContract
{
    /// <summary>
    /// Time of day / weather presets. The lighting deliberately mirrors the Blender preview renders: the HDRI sky
    /// lights the scene (ambient probe built from the sky texture + light bounced off the ground), the sun uses the
    /// same strength (Blender W/m² / π), there is only a light haze, reflections come from a cubemap of the harbour
    /// itself, the water mirrors the waterfront, and the image goes through AgX like Blender's view transform.
    /// </summary>
    public class Env : MonoBehaviour
    {
        public static Env I;

        [Serializable]
        public class Preset
        {
            public string id;
            public string title;            // shown in briefing ("GÜN BATIMI")
            public string sky;              // material in Resources/SC/Sky
            public float skyRotation;       // degrees
            public float skyExposure = 1f;  // multiplies the sky's own exposure (a daylight overcast sky darkened into a rainy night)
            public Color skyTint;           // replaces the sky tint when alpha > 0
            public Vector3 sunDirTex;       // sun direction in sky texture space (from make_sky.py)
            public float sunElevation;      // light elevation used for shading (deg); <0 -> use texture
            public Color sunColor;
            public float sunIntensity;      // Unity units (= Blender sun strength / pi)
            public float shadowStrength = 0.85f;
            public float ambientIntensity = 1f;
            public float groundAlbedo = 0.24f;   // light bounced from the yard onto down-facing surfaces
            public Color skyAmb, equatorAmb, groundAmb;   // fallback when the sky samples are missing
            public Color fogColor;
            public float fogDensity;
            public Color waterDeep, waterShallow;
            public float waterRefl = 1f;
            public float waves = 1f;        // wave strength (slope) multiplier: calm evening < 1, rain > 1
            public float reflectionIntensity = 1f;
            public bool night;
            public bool rain;
            public float wet;               // ground wetness 0..1
            public float snowfall;          // falling snow 0..1 (1 = blizzard)
            public float wind = 0.3f;       // mountain wind bed 0..1
            // post (AgX)
            public float exposure = 1f, saturation = 1.08f, lookPower = 1.08f, bloom = 0.25f, threshold = 1.6f;
            public Color tint = Color.white;
            public Color lift = Color.black;
            // ---- V3 look (the store-image pass): all off by default, so presets without them look as before
            public float contrast;                                   // display S-curve 0..1
            public Color shadowTint = Color.white, highlightTint = Color.white;   // split toning
            /// <summary>SC/Sky instead of Skybox/Panoramic: a sunset grade of the photo, an HDR sun and a horizon haze band.</summary>
            public bool scSky;
            public float gradeAmount;                                // 0 = the photo's colours, 1 = the ramp's
            public Color gradeSun = Color.white, gradeAway = Color.white, gradeZenith = Color.white;
            public float gradeSunPower = 2f, gradeHeightPower = 0.5f, gradeGain = 1f;
            public Color sunGlow = Color.black;                      // HDR (linear) colour of the sun's glow and disc
            public float glowTight, glowTightPower = 400f, glowWide, glowWidePower = 8f;
            public float discIntensity, discSize = 0.55f;            // disc radius (deg)
            public float horizonHaze, horizonHazeHeight = 0.06f;
            /// <summary>Aerial perspective from the depth buffer (PostFX): density 1/m at sea level (0 = Unity's fog).</summary>
            public float hazeDensity;
            public float hazeFalloff = 0.006f, hazeMax = 0.92f;
            public Color hazeAway = Color.grey, hazeSun = Color.grey;
            public float hazeSunPower = 3f, hazeGlowPower = 10f, hazeGlow;
            public float hazeStart;                                  // m: clear air round the player, haze beyond
            public float ambientGrade = 1f;                          // how much of the sky grade reaches the ambient light
            public Color ambientTint = Color.white;                  // multiplies the sky ambient (cool shade under a warm sky)
            public float waterGlitter = 1f;                          // sun glitter multiplier (a low sun over the sea)
            public Color waterTint = Color.white;                    // multiplies the water body colour
            /// <summary>SC/Sky's sun (glow, disc, the warm side of the grade and of the haze) where the light comes from
            /// (sunElevation) instead of where the photo's sun is: a golden afternoon from a sunset photo.</summary>
            public bool glowAtLight;
            public float windowNight = -1f;                          // _SC_Night override (lit rooms), < 0 = by the time of day
            public float lampPools = 1.7f;                           // night: strength of the street lamps' baked pools on the ground
        }

        public static readonly Preset[] Presets =
        {
            new Preset {
                id = "sunset", title = "GÜN BATIMI", sky = "sky_industrial_sunset_02_puresky", skyRotation = 200.9f,
                sunDirTex = new Vector3(0.8094f, 0.0391f, -0.586f), sunElevation = 13f,
                sunColor = new Color(1f, 0.62f, 0.38f), sunIntensity = 1.02f, shadowStrength = 0.82f,
                skyAmb = new Color(0.44f, 0.56f, 0.70f), equatorAmb = new Color(0.62f, 0.54f, 0.50f), groundAmb = new Color(0.20f, 0.19f, 0.20f),
                fogColor = new Color(0.70f, 0.70f, 0.72f), fogDensity = 0.00018f,
                waterDeep = new Color(0.02f, 0.05f, 0.07f), waterShallow = new Color(0.05f, 0.10f, 0.12f), waves = 0.9f,
                exposure = 1.0f, saturation = 1.1f, lookPower = 1.08f, bloom = 0.3f, threshold = 1.4f,
            },
            new Preset {
                id = "day", title = "GÜNDÜZ", sky = "sky_kloofendal_48d_partly_cloudy_puresky", skyRotation = 269.2f,
                sunDirTex = new Vector3(0.5547f, 0.7415f, -0.3775f), sunElevation = -1f,
                sunColor = new Color(1f, 0.96f, 0.9f), sunIntensity = 1.34f, shadowStrength = 0.85f,
                skyAmb = new Color(0.50f, 0.62f, 0.78f), equatorAmb = new Color(0.62f, 0.64f, 0.66f), groundAmb = new Color(0.25f, 0.24f, 0.22f),
                fogColor = new Color(0.72f, 0.76f, 0.82f), fogDensity = 0.00015f,
                waterDeep = new Color(0.02f, 0.06f, 0.08f), waterShallow = new Color(0.05f, 0.12f, 0.14f),
                exposure = 0.95f, saturation = 1.1f, lookPower = 1.1f, bloom = 0.2f, threshold = 1.8f,
            },
            new Preset {
                id = "overcast", title = "YAĞMURLU", sky = "sky_kloofendal_overcast_puresky", skyRotation = 237.7f,
                sunDirTex = new Vector3(0.8008f, 0.3919f, -0.4529f), sunElevation = -1f,
                sunColor = new Color(0.9f, 0.9f, 0.92f), sunIntensity = 0.32f, shadowStrength = 0.4f,
                skyAmb = new Color(0.62f, 0.62f, 0.66f), equatorAmb = new Color(0.56f, 0.56f, 0.58f), groundAmb = new Color(0.22f, 0.22f, 0.23f),
                fogColor = new Color(0.60f, 0.62f, 0.65f), fogDensity = 0.00032f,
                waterDeep = new Color(0.02f, 0.04f, 0.05f), waterShallow = new Color(0.04f, 0.07f, 0.08f), waterRefl = 1f, waves = 1.3f,
                exposure = 1.0f, saturation = 1.02f, lookPower = 1.14f, bloom = 0.15f, threshold = 2.0f,
                rain = true, wet = 0.75f,
            },
            new Preset {
                id = "night", title = "GECE", sky = "sky_qwantani_night_puresky", skyRotation = 310.9f,
                sunDirTex = new Vector3(0.5966f, 0.7696f, -0.2275f), sunElevation = 38f,
                sunColor = new Color(0.62f, 0.72f, 1f), sunIntensity = 0.12f, shadowStrength = 0.6f, ambientIntensity = 1.3f,
                skyAmb = new Color(0.10f, 0.13f, 0.22f), equatorAmb = new Color(0.12f, 0.12f, 0.16f), groundAmb = new Color(0.05f, 0.05f, 0.06f),
                fogColor = new Color(0.035f, 0.045f, 0.065f), fogDensity = 0.00028f,
                waterDeep = new Color(0.005f, 0.012f, 0.02f), waterShallow = new Color(0.01f, 0.02f, 0.03f), waterRefl = 1f, waves = 0.85f,
                reflectionIntensity = 1f, night = true,
                exposure = 2.4f, saturation = 1.0f, lookPower = 1.05f, bloom = 0.55f, threshold = 1.0f,
                tint = new Color(0.94f, 0.98f, 1.06f),
            },
            // rainy night in the city: low clouds lit orange by the streets, heavy drizzle, glistening stone
            new Preset {
                id = "rain_night", title = "YAĞMURLU GECE", sky = "sky_kloofendal_overcast_puresky", skyRotation = 237.7f,
                skyExposure = 0.045f, skyTint = new Color(0.62f, 0.5f, 0.42f, 0.5f),
                sunDirTex = new Vector3(0.5966f, 0.7696f, -0.2275f), sunElevation = 40f,
                sunColor = new Color(0.55f, 0.62f, 0.8f), sunIntensity = 0.035f, shadowStrength = 0.25f, ambientIntensity = 0.05f,
                skyAmb = new Color(0.08f, 0.08f, 0.1f), equatorAmb = new Color(0.08f, 0.07f, 0.07f), groundAmb = new Color(0.04f, 0.04f, 0.045f),
                fogColor = new Color(0.07f, 0.065f, 0.07f), fogDensity = 0.0019f,
                waterDeep = new Color(0.005f, 0.01f, 0.015f), waterShallow = new Color(0.01f, 0.018f, 0.024f), waterRefl = 1f, waves = 1.35f,
                reflectionIntensity = 1f, night = true, rain = true, wet = 1f,
                exposure = 2.3f, saturation = 0.95f, lookPower = 1.08f, bloom = 0.65f, threshold = 0.9f,
                tint = new Color(0.97f, 0.98f, 1.04f),
            },
            // ---- Kartal Tepe (mountain): snow everywhere bounces a lot of light (high ground albedo)
            new Preset {
                id = "snow_day", title = "KARLI GÜN", sky = "sky_snow_field_puresky", skyRotation = 283.4f,
                sunDirTex = new Vector3(0.6964f, 0.2866f, -0.6579f), sunElevation = 22f,
                sunColor = new Color(1f, 0.93f, 0.85f), sunIntensity = 0.62f, shadowStrength = 0.55f, ambientIntensity = 1.0f, groundAlbedo = 0.62f,
                skyAmb = new Color(0.66f, 0.70f, 0.78f), equatorAmb = new Color(0.70f, 0.72f, 0.76f), groundAmb = new Color(0.55f, 0.56f, 0.6f),
                fogColor = new Color(0.80f, 0.83f, 0.88f), fogDensity = 0.00085f,
                waterDeep = new Color(0.02f, 0.05f, 0.07f), waterShallow = new Color(0.05f, 0.10f, 0.12f),
                exposure = 0.82f, saturation = 1.04f, lookPower = 1.1f, bloom = 0.18f, threshold = 1.9f,
                snowfall = 0.35f, wind = 0.38f,
            },
            new Preset {
                id = "snow_sunset", title = "DAĞDA GÜN BATIMI", sky = "sky_kloppenheim_06_puresky", skyRotation = 242.3f,
                sunDirTex = new Vector3(0.7602f, 0.0804f, -0.6447f), sunElevation = 7f,
                sunColor = new Color(1f, 0.6f, 0.38f), sunIntensity = 1.15f, shadowStrength = 0.85f, ambientIntensity = 0.95f, groundAlbedo = 0.5f,
                skyAmb = new Color(0.46f, 0.56f, 0.74f), equatorAmb = new Color(0.66f, 0.58f, 0.56f), groundAmb = new Color(0.40f, 0.38f, 0.42f),
                fogColor = new Color(0.74f, 0.72f, 0.80f), fogDensity = 0.00042f,
                waterDeep = new Color(0.02f, 0.05f, 0.07f), waterShallow = new Color(0.05f, 0.10f, 0.12f),
                exposure = 0.92f, saturation = 1.12f, lookPower = 1.08f, bloom = 0.32f, threshold = 1.35f,
                tint = new Color(1.02f, 0.99f, 1.0f), wind = 0.25f,
            },
            new Preset {
                id = "snow_night", title = "KARLI GECE", sky = "sky_qwantani_night_puresky", skyRotation = 330.9f,
                sunDirTex = new Vector3(0.5966f, 0.7696f, -0.2275f), sunElevation = 34f,
                sunColor = new Color(0.62f, 0.74f, 1f), sunIntensity = 0.16f, shadowStrength = 0.7f, ambientIntensity = 1.35f, groundAlbedo = 0.6f,
                skyAmb = new Color(0.10f, 0.13f, 0.22f), equatorAmb = new Color(0.12f, 0.13f, 0.18f), groundAmb = new Color(0.08f, 0.09f, 0.12f),
                fogColor = new Color(0.05f, 0.06f, 0.09f), fogDensity = 0.00045f,
                waterDeep = new Color(0.005f, 0.012f, 0.02f), waterShallow = new Color(0.01f, 0.02f, 0.03f),
                reflectionIntensity = 1f, night = true,
                exposure = 2.2f, saturation = 1.0f, lookPower = 1.05f, bloom = 0.55f, threshold = 1.0f,
                tint = new Color(0.93f, 0.98f, 1.08f), snowfall = 0.15f, wind = 0.22f,
            },
            new Preset {
                id = "blizzard", title = "TİPİ", sky = "sky_snow_field_2_puresky", skyRotation = 272.1f, skyExposure = 0.62f,
                sunDirTex = new Vector3(0.7363f, 0.1293f, -0.6642f), sunElevation = 18f,
                sunColor = new Color(0.9f, 0.92f, 0.96f), sunIntensity = 0.18f, shadowStrength = 0.25f, ambientIntensity = 0.85f, groundAlbedo = 0.6f,
                skyAmb = new Color(0.58f, 0.60f, 0.66f), equatorAmb = new Color(0.60f, 0.62f, 0.66f), groundAmb = new Color(0.48f, 0.50f, 0.54f),
                fogColor = new Color(0.66f, 0.69f, 0.74f), fogDensity = 0.0062f,
                waterDeep = new Color(0.02f, 0.05f, 0.07f), waterShallow = new Color(0.05f, 0.10f, 0.12f),
                exposure = 0.95f, saturation = 0.86f, lookPower = 1.12f, bloom = 0.12f, threshold = 2.0f,
                snowfall = 1f, wind = 1f,
            },
        };

        public static Preset Find(string id)
        {
            foreach (var p in Presets) if (p.id == id) return p;
            return Presets[0];
        }

        /// <summary>
        /// V3 (user: "the harbour must look like the store image"): the harbour's own evening — the sun low over the open
        /// sea straight ahead of the main roof (yaw ~26°, above the yacht), backlit yard with long shadows towards the
        /// player, golden air thickening into the distance, a graded sky with a blooming sun, a punchier film look.
        /// Same id as the shared preset, so contracts, briefings and the menu keep calling it "sunset".
        /// </summary>
        public static readonly Preset HarborSunset = new Preset
        {
            id = "sunset", title = "GÜN BATIMI", sky = "sky_kloppenheim_06_puresky", skyRotation = 104f, skyExposure = 1f,
            sunDirTex = new Vector3(0.7602f, 0.0804f, -0.6447f), sunElevation = 8.5f,
            sunColor = new Color(1f, 0.64f, 0.40f), sunIntensity = 2.5f, shadowStrength = 0.9f, ambientIntensity = 0.52f, groundAlbedo = 0.16f,
            skyAmb = new Color(0.36f, 0.40f, 0.52f), equatorAmb = new Color(0.62f, 0.46f, 0.38f), groundAmb = new Color(0.14f, 0.12f, 0.12f),
            fogColor = new Color(0.80f, 0.60f, 0.46f), fogDensity = 0.00034f,
            waterDeep = new Color(0.018f, 0.030f, 0.042f), waterShallow = new Color(0.05f, 0.07f, 0.08f), waves = 1.0f, waterRefl = 1f,
            exposure = 0.95f, saturation = 1.15f, lookPower = 1.12f, bloom = 0.42f, threshold = 1.15f,
            contrast = 0.55f, shadowTint = new Color(0.93f, 0.97f, 1.08f), highlightTint = new Color(1.04f, 1.0f, 0.94f),
            scSky = true, gradeAmount = 0.85f,
            gradeSun = new Color(1.38f, 0.82f, 0.40f), gradeAway = new Color(1.16f, 0.78f, 0.62f), gradeZenith = new Color(0.98f, 0.80f, 0.78f),
            gradeSunPower = 2.2f, gradeHeightPower = 0.55f, gradeGain = 1.0f,
            sunGlow = new Color(1f, 0.55f, 0.24f), glowTight = 2.6f, glowTightPower = 700f, glowWide = 0.55f, glowWidePower = 9f,
            discIntensity = 26f, discSize = 0.62f, horizonHaze = 0.9f, horizonHazeHeight = 0.05f,
            hazeDensity = 1f / 2400f, hazeFalloff = 0.004f, hazeMax = 0.9f, hazeStart = 120f,
            hazeAway = new Color(0.56f, 0.46f, 0.43f), hazeSun = new Color(1.08f, 0.70f, 0.40f), hazeSunPower = 3f, hazeGlowPower = 14f, hazeGlow = 0.55f,
            ambientGrade = 0.35f, ambientTint = new Color(0.85f, 0.93f, 1.12f),
            waterGlitter = 2.4f, waterTint = new Color(1.0f, 0.92f, 0.85f),
        };

        /// <summary>
        /// V3 city (user: "the city centre must look like the store images"): a golden late afternoon over the seafront
        /// square — the sun low in the north-west, front-left of the main roof, so the square is side / back lit with long
        /// shadows, the glass catches it, the bay glitters and the air thickens into a warm haze towards the sun and a pale
        /// blue one away from it. The photo's sun is behind a cloud; the shader draws the sun where the light comes from.
        /// </summary>
        public static readonly Preset CityDay = new Preset
        {
            id = "day", title = "GÜNDÜZ", sky = "sky_industrial_sunset_02_puresky", skyRotation = 171f, skyExposure = 1f,
            sunDirTex = new Vector3(0.8094f, 0.0391f, -0.586f), sunElevation = 21f, glowAtLight = true,
            sunColor = new Color(1f, 0.79f, 0.56f), sunIntensity = 2.0f, shadowStrength = 0.9f, ambientIntensity = 0.62f, groundAlbedo = 0.22f,
            skyAmb = new Color(0.40f, 0.48f, 0.62f), equatorAmb = new Color(0.58f, 0.56f, 0.54f), groundAmb = new Color(0.20f, 0.18f, 0.16f),
            fogColor = new Color(0.78f, 0.74f, 0.70f), fogDensity = 0.0003f,
            waterDeep = new Color(0.010f, 0.060f, 0.090f), waterShallow = new Color(0.030f, 0.130f, 0.170f), waves = 0.95f, waterRefl = 1f,
            exposure = 0.92f, saturation = 1.16f, lookPower = 1.1f, bloom = 0.3f, threshold = 1.3f,
            contrast = 0.55f, shadowTint = new Color(0.92f, 0.98f, 1.1f), highlightTint = new Color(1.02f, 1.0f, 0.96f),
            scSky = true, gradeAmount = 0.8f,
            gradeSun = new Color(1.15f, 1.06f, 0.78f), gradeAway = new Color(0.48f, 0.76f, 1.22f), gradeZenith = new Color(0.30f, 0.60f, 1.25f),
            gradeSunPower = 8.0f, gradeHeightPower = 0.5f, gradeGain = 1.0f,
            sunGlow = new Color(1f, 0.88f, 0.62f), glowTight = 3.2f, glowTightPower = 900f, glowWide = 0.62f, glowWidePower = 7f,
            discIntensity = 34f, discSize = 0.6f, horizonHaze = 0.75f, horizonHazeHeight = 0.045f,
            hazeDensity = 1f / 2100f, hazeFalloff = 0.0045f, hazeMax = 0.88f, hazeStart = 70f,
            hazeAway = new Color(0.50f, 0.60f, 0.74f), hazeSun = new Color(1.05f, 0.92f, 0.66f), hazeSunPower = 6f, hazeGlowPower = 10f, hazeGlow = 0.6f,
            ambientGrade = 0.5f, ambientTint = new Color(0.92f, 0.96f, 1.04f),
            waterGlitter = 2.0f, waterTint = new Color(0.94f, 1.0f, 1.06f),
        };

        /// <summary>V3 city sunset (tram, helipad): the sun just above the bay in the north-west, deep amber light raking
        /// the square from the left, the seafront glowing, the city's windows coming on.</summary>
        public static readonly Preset CitySunset = new Preset
        {
            id = "sunset", title = "GÜN BATIMI", sky = "sky_kloppenheim_06_puresky", skyRotation = 186f, skyExposure = 1f,
            sunDirTex = new Vector3(0.7602f, 0.0804f, -0.6447f), sunElevation = 7.5f, glowAtLight = true,
            sunColor = new Color(1f, 0.62f, 0.38f), sunIntensity = 2.4f, shadowStrength = 0.9f, ambientIntensity = 0.5f, groundAlbedo = 0.17f,
            skyAmb = new Color(0.36f, 0.40f, 0.52f), equatorAmb = new Color(0.62f, 0.46f, 0.38f), groundAmb = new Color(0.14f, 0.12f, 0.12f),
            fogColor = new Color(0.80f, 0.60f, 0.46f), fogDensity = 0.00034f,
            waterDeep = new Color(0.018f, 0.030f, 0.042f), waterShallow = new Color(0.05f, 0.07f, 0.08f), waves = 1.0f, waterRefl = 1f,
            exposure = 0.95f, saturation = 1.15f, lookPower = 1.12f, bloom = 0.42f, threshold = 1.15f,
            contrast = 0.55f, shadowTint = new Color(0.93f, 0.97f, 1.08f), highlightTint = new Color(1.04f, 1.0f, 0.94f),
            scSky = true, gradeAmount = 0.85f,
            gradeSun = new Color(1.38f, 0.82f, 0.40f), gradeAway = new Color(1.10f, 0.80f, 0.68f), gradeZenith = new Color(0.92f, 0.80f, 0.82f),
            gradeSunPower = 2.2f, gradeHeightPower = 0.55f, gradeGain = 1.0f,
            sunGlow = new Color(1f, 0.55f, 0.24f), glowTight = 2.6f, glowTightPower = 700f, glowWide = 0.55f, glowWidePower = 9f,
            discIntensity = 26f, discSize = 0.62f, horizonHaze = 0.9f, horizonHazeHeight = 0.05f,
            hazeDensity = 1f / 1700f, hazeFalloff = 0.0045f, hazeMax = 0.9f, hazeStart = 70f,
            hazeAway = new Color(0.56f, 0.47f, 0.46f), hazeSun = new Color(1.10f, 0.70f, 0.40f), hazeSunPower = 3f, hazeGlowPower = 14f, hazeGlow = 0.55f,
            ambientGrade = 0.35f, ambientTint = new Color(0.85f, 0.93f, 1.12f),
            waterGlitter = 2.4f, waterTint = new Color(1.0f, 0.92f, 0.85f), windowNight = 0.55f,
        };

        /// <summary>V3 city night (the office tower, the gala, the penthouse): no stars over a big city — low cloud lit
        /// from below, blue-grey mist that swallows the far towers, thousands of lit windows, neon and lamps blooming.</summary>
        public static readonly Preset CityNight = new Preset
        {
            id = "night", title = "GECE", sky = "sky_kloofendal_overcast_puresky", skyRotation = 237.7f, skyExposure = 0.07f,
            skyTint = new Color(0.5f, 0.5f, 0.5f, 0.5f),
            sunDirTex = new Vector3(0.5966f, 0.7696f, -0.2275f), sunElevation = 38f,
            sunColor = new Color(0.56f, 0.68f, 1f), sunIntensity = 0.07f, shadowStrength = 0.5f, ambientIntensity = 0.16f, groundAlbedo = 0.3f,
            skyAmb = new Color(0.10f, 0.13f, 0.22f), equatorAmb = new Color(0.12f, 0.12f, 0.16f), groundAmb = new Color(0.05f, 0.05f, 0.06f),
            fogColor = new Color(0.05f, 0.06f, 0.08f), fogDensity = 0.0003f,
            waterDeep = new Color(0.004f, 0.010f, 0.018f), waterShallow = new Color(0.008f, 0.018f, 0.028f), waterRefl = 1f, waves = 0.85f,
            reflectionIntensity = 1f, night = true, wet = 0.25f,
            exposure = 2.3f, saturation = 1.08f, lookPower = 1.06f, bloom = 0.7f, threshold = 0.85f,
            contrast = 0.35f, shadowTint = new Color(0.90f, 0.97f, 1.14f), highlightTint = new Color(1.05f, 1.0f, 0.95f),
            scSky = true, gradeAmount = 1f,
            gradeSun = new Color(1.12f, 1.02f, 1.0f), gradeAway = new Color(1.12f, 1.02f, 1.0f), gradeZenith = new Color(0.36f, 0.46f, 0.80f),
            gradeSunPower = 1f, gradeHeightPower = 0.35f, gradeGain = 1.0f,
            horizonHaze = 0.9f, horizonHazeHeight = 0.12f,
            hazeDensity = 1f / 1100f, hazeFalloff = 0.005f, hazeMax = 0.88f, hazeStart = 45f,
            hazeAway = new Color(0.040f, 0.052f, 0.075f), hazeSun = new Color(0.040f, 0.052f, 0.075f), hazeSunPower = 1f, hazeGlowPower = 10f, hazeGlow = 0f,
            ambientGrade = 1f, ambientTint = new Color(0.9f, 0.96f, 1.12f),
            waterGlitter = 1f, waterTint = Color.white,
        };

        /// <summary>V3 city in the rain at night: the same city drowned in mist, every surface wet, lights smeared.</summary>
        public static readonly Preset CityRainNight = new Preset
        {
            id = "rain_night", title = "YAĞMURLU GECE", sky = "sky_kloofendal_overcast_puresky", skyRotation = 237.7f, skyExposure = 0.06f,
            skyTint = new Color(0.5f, 0.5f, 0.5f, 0.5f),
            sunDirTex = new Vector3(0.5966f, 0.7696f, -0.2275f), sunElevation = 40f,
            sunColor = new Color(0.55f, 0.62f, 0.8f), sunIntensity = 0.04f, shadowStrength = 0.25f, ambientIntensity = 0.1f, groundAlbedo = 0.3f,
            skyAmb = new Color(0.08f, 0.08f, 0.1f), equatorAmb = new Color(0.08f, 0.07f, 0.07f), groundAmb = new Color(0.04f, 0.04f, 0.045f),
            fogColor = new Color(0.05f, 0.06f, 0.075f), fogDensity = 0.0006f,
            waterDeep = new Color(0.005f, 0.01f, 0.015f), waterShallow = new Color(0.01f, 0.018f, 0.024f), waterRefl = 1f, waves = 1.35f,
            reflectionIntensity = 1f, night = true, rain = true, wet = 1f,
            exposure = 2.4f, saturation = 1.05f, lookPower = 1.06f, bloom = 0.8f, threshold = 0.8f,
            contrast = 0.32f, shadowTint = new Color(0.90f, 0.97f, 1.14f), highlightTint = new Color(1.04f, 1.0f, 0.97f),
            scSky = true, gradeAmount = 1f,
            gradeSun = new Color(1.0f, 1.0f, 1.08f), gradeAway = new Color(1.0f, 1.0f, 1.08f), gradeZenith = new Color(0.6f, 0.7f, 0.95f),
            gradeSunPower = 1f, gradeHeightPower = 0.45f, gradeGain = 1.0f,
            horizonHaze = 1f, horizonHazeHeight = 0.2f,
            hazeDensity = 1f / 520f, hazeFalloff = 0.004f, hazeMax = 0.9f, hazeStart = 30f,
            hazeAway = new Color(0.042f, 0.054f, 0.074f), hazeSun = new Color(0.042f, 0.054f, 0.074f), hazeSunPower = 1f, hazeGlowPower = 10f, hazeGlow = 0f,
            ambientGrade = 1f, ambientTint = new Color(0.9f, 0.96f, 1.1f),
            waterGlitter = 1f, waterTint = Color.white,
        };

        /// <summary>The preset as this map shows it (a map can have its own version of a time of day).</summary>
        public static Preset Localize(Preset p)
        {
            string layout = Level.I != null ? Level.I.LayoutName : null;
            if (p == null) return p;
            if (layout == "harbor" && p.id == "sunset") return HarborSunset;
            if (layout == "city")
            {
                switch (p.id)
                {
                    case "day": return CityDay;
                    case "sunset": return CitySunset;
                    case "night": return CityNight;
                    case "rain_night": return CityRainNight;
                }
            }
            return p;
        }

        /// <summary>A copy that can be changed without touching the shared preset (editor preview tuning).</summary>
        public static Preset Clone(Preset p) => JsonUtility.FromJson<Preset>(JsonUtility.ToJson(p));

        public Preset Current { get; private set; }
        public Light Sun { get; private set; }
        public Material WaterMat { get; private set; }
        GameObject water;
        ParticleSystem rain;
        SnowFX snow;
        static readonly int idSnow = Shader.PropertyToID("_SC_Snow");
        Camera envCam;
        RenderTexture envRT;
        Cubemap envCube;
        /// <summary>The captured reflection cube (preview diagnostics only).</summary>
        public RenderTexture EnvCapture => envRT;
        public Vector3 EnvCapturePos => envCam != null ? envCam.transform.position : Vector3.zero;
        static readonly int idWet = Shader.PropertyToID("_SC_Wet");
        static readonly int idNight = Shader.PropertyToID("_SC_Night");
        static readonly int idPool = Shader.PropertyToID("_SC_NightPool");
        static readonly int idWind = Shader.PropertyToID("_SC_Wind");

        // ---------------------------------------------------------------- sky ambient samples (tools/pack/sky_ambient.py)
        [Serializable] class SkyAmbientEntry { public string id; public float[] s; }
        [Serializable] class SkyAmbientFile { public SkyAmbientEntry[] skies; public float[] ground; }
        static SkyAmbientFile ambient;
        static bool ambientLoaded;

        static SkyAmbientFile Ambient
        {
            get
            {
                if (!ambientLoaded)
                {
                    ambientLoaded = true;
                    var ta = Res.Text("Level/sky_ambient");
                    if (ta != null)
                    {
                        try { ambient = JsonUtility.FromJson<SkyAmbientFile>(ta.text); }
                        catch (Exception e) { Debug.LogException(e); }
                    }
                }
                return ambient;
            }
        }

        public void Init(Transform followTarget)
        {
            I = this;
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowBias = 0.05f;
            Sun.shadowNormalBias = 0.5f;
            RenderSettings.sun = Sun;
            BuildWater();
        }

        void BuildWater()
        {
            var sh = Shader.Find("SC/Water");
            WaterMat = new Material(sh != null ? sh : Shader.Find("Standard"));
            var nrm = Resources.Load<Texture2D>("SC/Textures/water_normal");
            if (nrm != null)
            {
                // seen at grazing angles: no mip bands, keep the detail along the view direction
                nrm.filterMode = FilterMode.Trilinear;
                nrm.anisoLevel = 4;
                WaterMat.SetTexture("_NormalMap", nrm);
            }
            PlanarReflection.WaterMaterial = WaterMat;
            PlanarReflection.PlaneY = Level.WaterY;
            water = new GameObject("Water");
            water.transform.SetParent(transform, false);
            water.layer = Layers.Water;
            // near: fine grid for vertex waves; far: big coarse grid out to the horizon
            var near = new GameObject("WaterNear");
            near.layer = Layers.Water;
            near.transform.SetParent(water.transform, false);
            near.transform.position = new Vector3(0, Level.WaterY, 450);
            near.AddComponent<MeshFilter>().sharedMesh = Util.Grid(1400, 1000, 140, 100, 1f);
            var nr = near.AddComponent<MeshRenderer>();
            nr.sharedMaterial = WaterMat;
            nr.shadowCastingMode = ShadowCastingMode.Off;
            nr.receiveShadows = false;
            near.AddComponent<PlanarReflection>();
            var far = new GameObject("WaterFar");
            far.layer = Layers.Water;
            far.transform.SetParent(water.transform, false);
            far.transform.position = new Vector3(0, Level.WaterY - 0.05f, 450);
            far.AddComponent<MeshFilter>().sharedMesh = Util.Grid(16000, 16000, 16, 16, 1f);
            var fr = far.AddComponent<MeshRenderer>();
            fr.sharedMaterial = WaterMat;
            fr.shadowCastingMode = ShadowCastingMode.Off;
            fr.receiveShadows = false;
            far.AddComponent<PlanarReflection>();
        }

        /// <summary>World direction towards the sun for a preset (sky texture direction turned with the skybox).</summary>
        public static Vector3 SunDirection(Preset p)
        {
            Vector3 d = p.sunDirTex.normalized;
            var dr = RotateSky(d, p.skyRotation);
            if (p.sunElevation > 0)
            {
                var flat = new Vector3(dr.x, 0, dr.z).normalized;
                float e = p.sunElevation * Mathf.Deg2Rad;
                dr = flat * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
            }
            return dr.normalized;
        }

        /// <summary>Where SC/Sky and the haze put the sun: the photo's sun, or the light's direction (glowAtLight).</summary>
        public static Vector3 GlowDirection(Preset p) => p.glowAtLight ? SunDirection(p) : RotateSky(p.sunDirTex.normalized, p.skyRotation);

        // Skybox/Panoramic draws texture direction t at world direction R(r) t: x' = cos*x - sin*z, z' = sin*x + cos*z
        static Vector3 RotateSky(Vector3 d, float rotationDeg)
        {
            float r = rotationDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector3(c * d.x - s * d.z, d.y, s * d.x + c * d.z);
        }

        readonly System.Collections.Generic.Dictionary<string, Material> skyCopies = new System.Collections.Generic.Dictionary<string, Material>();

        public void Apply(string presetId, Camera cam, PostFX fx) => ApplyPreset(Localize(Find(presetId)), cam, fx);

        Material scSkyMat;
        static Shader scSkyShader;
        static readonly int idSunSky = Shader.PropertyToID("_SC_SunSkyDir"), idSunGlow = Shader.PropertyToID("_SC_SunGlowColor"),
            idHazeAway = Shader.PropertyToID("_SC_HazeAway"), idHazeSun = Shader.PropertyToID("_SC_HazeSun"),
            idHazeShape = Shader.PropertyToID("_SC_HazeShape"), idHaze = Shader.PropertyToID("_SC_Haze"),
            idHazeStart = Shader.PropertyToID("_SC_HazeStart");

        public void ApplyPreset(Preset p, Camera cam, PostFX fx)
        {
            Current = p;
            var sky = Res.Sky(p.sky);
            if (sky == null) sky = Res.Sky("sky_belfast_sunset_puresky");
            var skySun = GlowDirection(p);
            if (sky != null)
            {
                Material use = null;
                if (p.scSky)
                {
                    if (scSkyShader == null) scSkyShader = Shader.Find("SC/Sky");
                    if (scSkyShader != null && scSkyShader.isSupported)
                    {
                        if (scSkyMat == null) scSkyMat = new Material(scSkyShader) { name = "SC_Sky (runtime)", hideFlags = HideFlags.DontSave };
                        use = scSkyMat;
                        use.SetTexture("_MainTex", sky.GetTexture("_MainTex"));
                        use.SetFloat("_Rotation", p.skyRotation);
                        use.SetFloat("_Exposure", (sky.HasProperty("_Exposure") ? sky.GetFloat("_Exposure") : 1f) * p.skyExposure);
                        use.SetColor("_Tint", p.skyTint.a > 0f ? p.skyTint : (sky.HasProperty("_Tint") ? sky.GetColor("_Tint") : new Color(0.5f, 0.5f, 0.5f, 0.5f)));
                        // linear values: no colour-space conversion
                        use.SetVector("_GradeSun", (Vector4)p.gradeSun);
                        use.SetVector("_GradeAway", (Vector4)p.gradeAway);
                        use.SetVector("_GradeZenith", (Vector4)p.gradeZenith);
                        use.SetVector("_Grade", new Vector4(p.gradeAmount, p.gradeSunPower, p.gradeHeightPower, p.gradeGain));
                        use.SetVector("_Glow", new Vector4(p.glowTight, p.glowTightPower, p.glowWide, p.glowWidePower));
                        float cr = Mathf.Cos(p.discSize * Mathf.Deg2Rad);
                        use.SetVector("_Disc", new Vector4(p.discIntensity, cr, Mathf.Max(1e-6f, (1f - cr) * 0.35f), 0f));
                        use.SetVector("_HorizonHaze", new Vector4(p.horizonHaze, p.horizonHazeHeight, 0f, 0f));
                    }
                }
                if (use == null)
                {
                    // one runtime copy per sky (rotated), reused on every return to the menu / contract start
                    if (!skyCopies.TryGetValue(p.sky ?? "", out var copy) || copy == null)
                    {
                        copy = new Material(sky);
                        skyCopies[p.sky ?? ""] = copy;
                    }
                    copy.SetFloat("_Rotation", p.skyRotation);
                    // presets can share a sky: exposure and tint always start from the sky's own values
                    if (copy.HasProperty("_Exposure") && sky.HasProperty("_Exposure")) copy.SetFloat("_Exposure", sky.GetFloat("_Exposure") * p.skyExposure);
                    if (copy.HasProperty("_Tint") && sky.HasProperty("_Tint")) copy.SetColor("_Tint", p.skyTint.a > 0f ? p.skyTint : sky.GetColor("_Tint"));
                    use = copy;
                }
                RenderSettings.skybox = use;
            }
            // the haze model shared by SC/Sky and the post haze (linear colours)
            Shader.SetGlobalVector(idSunSky, new Vector4(skySun.x, skySun.y, skySun.z, 1f));
            Shader.SetGlobalVector(idSunGlow, (Vector4)p.sunGlow);
            Shader.SetGlobalVector(idHazeAway, (Vector4)p.hazeAway);
            Shader.SetGlobalVector(idHazeSun, (Vector4)p.hazeSun);
            Shader.SetGlobalVector(idHazeShape, new Vector4(p.hazeSunPower, p.hazeGlowPower, p.hazeGlow, 0f));
            Shader.SetGlobalVector(idHaze, new Vector4(p.hazeDensity, p.hazeFalloff, Level.WaterY, p.hazeMax));
            Shader.SetGlobalFloat(idHazeStart, Mathf.Max(0f, p.hazeStart));
            PostFX.HazeActive = p.hazeDensity > 0f;

            var sunDir = SunDirection(p);
            Sun.transform.rotation = Quaternion.LookRotation(-sunDir);
            Sun.color = p.sunColor;
            Sun.intensity = p.sunIntensity;
            Sun.shadowStrength = p.shadowStrength;

            ApplyAmbient(p, sunDir);

            RenderSettings.fog = p.fogDensity > 0f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogDensity = p.fogDensity;

            if (WaterMat != null)
            {
                WaterMat.SetColor("_DeepColor", p.waterDeep * p.waterTint);
                WaterMat.SetColor("_ShallowColor", p.waterShallow * p.waterTint);
                WaterMat.SetFloat("_ReflStrength", p.waterRefl);
                WaterMat.SetFloat("_SunSpec", (p.night ? 4f : (p.rain ? 0.6f : 2.5f)) * p.waterGlitter);
                WaterMat.SetFloat("_LowSun", p.waterGlitter > 1.01f ? 1f : 0f);
                WaterMat.SetFloat("_NormalScale", p.waves);
                WaterMat.SetFloat("_ReflBlur", p.rain ? 0.7f : 0.55f);
                PlanarReflection.BlurWidth = p.rain ? 0.026f : 0.02f;
                WaterMat.SetFloat("_BlurWidth", PlanarReflection.BlurWidth);
            }
            Shader.SetGlobalFloat(idWet, p.wet);
            // street lamps' pools of light baked into the ground mask (SC/Ground), only when the lamps are on
            var pc = new Color(1f, 0.74f, 0.46f) * (p.night ? p.lampPools : 0f);
            Shader.SetGlobalVector(idPool, new Vector4(pc.r, pc.g, pc.b, 0f));
            // snow lying on every up-facing surface of the mountain map (SC/SnowStandard, SC/Terrain)
            Shader.SetGlobalFloat(idSnow, Level.I != null && Level.I.Data != null && Level.I.Data.snow != 0 ? 1f : 0f);
            SyncWater();
            // city windows: rooms light up from dusk (SC/Window)
            Shader.SetGlobalFloat(idNight, p.windowNight >= 0f ? p.windowNight : (p.night ? 1f : (p.id == "sunset" ? 0.45f : (p.rain || p.id == "overcast" ? 0.2f : 0f))));
            Sfx.SetLocationAmbience(Game.I != null ? Game.I.LocationId : "harbor");
            PlanarReflection.PixelLights = p.night ? 1 : 0;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.backgroundColor = p.fogColor;
            }
            if (fx != null)
            {
                fx.exposure = p.exposure; fx.saturation = p.saturation; fx.lookPower = p.lookPower;
                fx.bloomIntensity = p.bloom; fx.threshold = p.threshold; fx.tint = p.tint; fx.lift = p.lift;
                fx.contrast = p.contrast; fx.shadowTint = p.shadowTint; fx.highlightTint = p.highlightTint;
                Grade(p, fx);
            }
            if (Level.I != null) Level.I.SetNight(p.night);
            SetRain(p.rain, cam);
            SetSnow(p.snowfall, cam);
            Sfx.SetWind(p.wind);
            Shader.SetGlobalFloat(idWind, p.wind);          // the far forest sways (SC/ForestCard)
            CaptureEnvironment(p);
        }

        /// <summary>
        /// V2 look: a filmic grade per time of day on top of the preset (test screenshots read flat and grey): warm key
        /// against cool shadows in daylight, a deeper amber at sunset, cold blue nights with a little more glow on the
        /// lamps, a muted wet overcast, snow pulled down so it is not paper white. A touch more haze for depth.
        /// </summary>
        static void Grade(Preset p, PostFX fx)
        {
            fx.vignette = 1.05f;
            if (p.scSky || p.contrast > 0f) { fx.vignette = 1.12f; return; }     // V3 presets carry their own look
            Color tint = Color.white, lift = Color.black;
            float sat = 1f, look = 1f, exp = 1f, bloom = 0f, fog = 1f, vig = 1.12f;
            switch (p.id)
            {
                case "day": tint = new Color(1.03f, 1.0f, 0.95f); lift = new Color(0.006f, 0.010f, 0.018f); sat = 1.06f; look = 1.04f; fog = 1.4f; break;
                case "sunset": tint = new Color(1.06f, 0.98f, 0.90f); lift = new Color(0.012f, 0.008f, 0.020f); sat = 1.04f; bloom = 0.08f; fog = 1.2f; break;
                case "overcast": tint = new Color(0.96f, 0.99f, 1.03f); lift = new Color(0.010f, 0.012f, 0.016f); sat = 0.92f; look = 1.06f; fog = 1.5f; break;
                case "night": tint = new Color(0.92f, 0.97f, 1.08f); lift = new Color(0.004f, 0.008f, 0.018f); bloom = 0.12f; vig = 1.2f; break;
                case "rain_night": tint = new Color(0.92f, 0.97f, 1.08f); lift = new Color(0.004f, 0.008f, 0.018f); bloom = 0.12f; fog = 1.3f; vig = 1.2f; break;
                case "snow_day": tint = new Color(0.98f, 1.0f, 1.03f); exp = 0.94f; look = 1.06f; vig = 1.05f; break;
                case "snow_sunset": tint = new Color(1.05f, 0.97f, 0.92f); bloom = 0.06f; break;
                case "snow_night": tint = new Color(0.92f, 0.97f, 1.08f); bloom = 0.1f; vig = 1.2f; break;
                default: return;
            }
            fx.tint = new Color(fx.tint.r * tint.r, fx.tint.g * tint.g, fx.tint.b * tint.b, fx.tint.a);
            fx.lift = new Color(fx.lift.r + lift.r, fx.lift.g + lift.g, fx.lift.b + lift.b, fx.lift.a);
            fx.saturation *= sat;
            fx.lookPower *= look;
            fx.exposure *= exp;
            fx.bloomIntensity += bloom;
            fx.vignette = vig;
            RenderSettings.fogDensity = p.fogDensity * fog;
        }

        /// <summary>The sea follows the map: its height, and no sea at all in the mountains.</summary>
        void SyncWater()
        {
            if (water == null) return;
            bool sea = Level.I == null || Level.I.Data == null || Level.I.Data.sea != 0;
            if (water.activeSelf != sea) water.SetActive(sea);
            PlanarReflection.PlaneY = Level.WaterY;
            foreach (Transform t in water.transform)
            {
                var p = t.position;
                p.y = Level.WaterY - (t.name == "WaterFar" ? 0.05f : 0f);
                t.position = p;
            }
        }

        void SetSnow(float amount, Camera cam)
        {
            if (amount <= 0.01f)
            {
                if (snow != null) snow.gameObject.SetActive(false);
                return;
            }
            if (snow == null && cam != null) snow = SnowFX.Build(cam);
            if (snow == null) return;
            snow.gameObject.SetActive(true);
            snow.SetAmount(amount);
        }

        /// <summary>Ambient probe from the sky texture (like Cycles lighting with the HDRI) + ground bounce from below.</summary>
        void ApplyAmbient(Preset p, Vector3 sunDir)
        {
            var data = Ambient;
            SkyAmbientEntry entry = null;
            if (data != null && data.skies != null)
            {
                string id = p.sky.StartsWith("sky_") ? p.sky.Substring(4) : p.sky;
                foreach (var e in data.skies) if (e.id == id) { entry = e; break; }
            }
            if (entry == null || entry.s == null || entry.s.Length < 6)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = p.skyAmb * p.ambientIntensity;
                RenderSettings.ambientEquatorColor = p.equatorAmb * p.ambientIntensity;
                RenderSettings.ambientGroundColor = p.groundAmb * p.ambientIntensity;
                return;
            }
            var sh = new SphericalHarmonicsL2();
            Color up = Color.black;
            var skySun = GlowDirection(p);
            for (int i = 0; i + 5 < entry.s.Length; i += 6)
            {
                var d = RotateSky(new Vector3(entry.s[i], entry.s[i + 1], entry.s[i + 2]), p.skyRotation);
                var c = new Color(entry.s[i + 3], entry.s[i + 4], entry.s[i + 5]);
                if (p.scSky && p.gradeAmount > 0f) c = GradeSample(p, d, c, skySun, p.gradeAmount * p.ambientGrade);    // the sky's light as SC/Sky shows it
                c *= p.ambientIntensity;
                c = new Color(c.r * p.ambientTint.r, c.g * p.ambientTint.g, c.b * p.ambientTint.b, 1f);
                sh.AddDirectionalLight(d, c, 1f);
                up += c * Mathf.Max(0f, d.y);
            }
            // the yard bounces sky + sun light back up onto everything that faces down
            var sunLight = p.sunColor * p.sunIntensity * Mathf.Max(0f, sunDir.y);
            var bounce = (up + sunLight) * p.groundAlbedo;
            if (data.ground != null)
                for (int i = 0; i + 3 < data.ground.Length; i += 4)
                    sh.AddDirectionalLight(new Vector3(data.ground[i], data.ground[i + 1], data.ground[i + 2]), bounce * data.ground[i + 3], 1f);
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientProbe = sh;
        }

        /// <summary>The SC/Sky grade for one sky light sample (same ramp as the shader, linear colours).</summary>
        static Color GradeSample(Preset p, Vector3 d, Color c, Vector3 sun, float amount)
        {
            float mu = Vector3.Dot(d.normalized, sun);
            float sunK = Mathf.Pow(Mathf.Clamp01(mu * 0.5f + 0.5f), Mathf.Max(0.01f, p.gradeSunPower));
            var horizon = Color.Lerp(p.gradeAway, p.gradeSun, sunK);
            var ramp = Color.Lerp(horizon, p.gradeZenith, Mathf.Pow(Mathf.Clamp01(d.normalized.y), Mathf.Max(0.01f, p.gradeHeightPower)));
            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            var g = new Color(lum * ramp.r, lum * ramp.g, lum * ramp.b);
            var o = Color.Lerp(c, g, amount) * p.gradeGain;
            o.a = 1f;
            return o;
        }

        /// <summary>
        /// Renders the harbour into a small HDR cubemap from above the quay and uses it as the default reflection,
        /// so glossy things reflect containers / buildings / far shore instead of only the bright sky.
        /// </summary>
        void CaptureEnvironment(Preset p)
        {
            const int size = 128;
            try
            {
                if (envRT == null)
                {
                    envRT = new RenderTexture(size, size, 16, RenderTextureFormat.ARGBHalf)
                    {
                        dimension = TextureDimension.Cube, useMipMap = true, autoGenerateMips = false,
                        hideFlags = HideFlags.DontSave, name = "SC_EnvCapture"
                    };
                    envRT.Create();
                }
                if (envCam == null)
                {
                    var go = new GameObject("SC_EnvCaptureCam") { hideFlags = HideFlags.HideAndDontSave };
                    envCam = go.AddComponent<Camera>();
                    envCam.enabled = false;
                }
                float quay = Level.I != null && Level.I.Data != null ? Level.I.Data.quayZ : 262f;
                float capY = Level.I != null && Level.I.Data != null && Level.I.Data.envY != 0f ? Level.I.Data.envY : 22f;
                envCam.transform.position = new Vector3(0f, capY, quay - 20f);
                envCam.transform.rotation = Quaternion.identity;
                envCam.cullingMask = Layers.EnvMask;
                envCam.clearFlags = CameraClearFlags.Skybox;
                envCam.allowHDR = true;
                envCam.allowMSAA = false;
                envCam.nearClipPlane = 0.5f;
                envCam.farClipPlane = 6000f;
                float oldShadow = QualitySettings.shadowDistance;
                int oldPixel = QualitySettings.pixelLightCount;
                QualitySettings.shadowDistance = 0f;
                QualitySettings.pixelLightCount = p.night ? 2 : 0;
                try { envCam.RenderToCubemap(envRT, 63); }
                finally { QualitySettings.shadowDistance = oldShadow; QualitySettings.pixelLightCount = oldPixel; }
                envRT.GenerateMips();
                if (envCube == null)
                    envCube = new Cubemap(size, TextureFormat.RGBAHalf, true) { hideFlags = HideFlags.DontSave, name = "SC_EnvCube" };
                Graphics.CopyTexture(envRT, envCube);
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = envCube;
                RenderSettings.reflectionIntensity = p.reflectionIntensity;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.reflectionIntensity = p.reflectionIntensity;
            }
        }

        void SetRain(bool on, Camera cam)
        {
            if (!on)
            {
                if (rain != null) rain.gameObject.SetActive(false);
                return;
            }
            if (rain == null && cam != null) rain = FX.BuildRain(cam.transform);
            if (rain != null) rain.gameObject.SetActive(true);
        }

        void OnDestroy()
        {
            if (envRT != null) { envRT.Release(); DestroyImmediate(envRT); }
            if (envCube != null) DestroyImmediate(envCube);
            if (envCam != null) DestroyImmediate(envCam.gameObject);
            PlanarReflection.Release();
        }
    }
}

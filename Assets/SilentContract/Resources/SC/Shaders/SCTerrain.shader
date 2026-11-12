// Silent Contract — mountain terrain: snow, with rock showing through on crags (vertex colour R, sharpened by the rock
// texture and broken up by world-space noise so the coarse mesh never shows), darker snow in the shade of the woods
// (vertex colour G), trodden snow along paths and tracks and the baked ambient occlusion (ground maps: R = AO,
// G = trodden snow, world XZ). Far away the snow's photo detail fades to its mean colour (no tiling) and a large soft
// variation takes over; up close a faint glitter twinkles in the crystals.
Shader "SC/Terrain"
{
    Properties
    {
        _Color ("Snow Color", Color) = (1, 1, 1, 1)
        _MainTex ("Snow Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Snow Normal", 2D) = "bump" {}
        _SnowFar ("Snow colour far away", Color) = (0.90, 0.92, 0.95, 1)
        _RockColor ("Rock Color", Color) = (1, 1, 1, 1)
        _RockTex ("Rock Albedo", 2D) = "grey" {}
        [Normal] _RockBump ("Rock Normal", 2D) = "bump" {}
        _RockTile ("Rock tiling (x main uv)", Float) = 0.6
        _TrodColor ("Trodden snow tint", Color) = (0.78, 0.80, 0.84, 1)
        _ForestFloor ("Forest floor tint", Color) = (0.60, 0.65, 0.72, 1)
        _AOTex ("Ground maps (R AO, G trodden)", 2D) = "white" {}
        _AORect ("Map rect (xMin, zMin, 1/sizeX, 1/sizeZ)", Vector) = (0, 0, 0, 0)
        _AOStrength ("AO Strength", Range(0, 1)) = 1
        _Sparkle ("Glitter", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex, _BumpMap, _RockTex, _RockBump, _AOTex;
        fixed4 _Color, _RockColor, _SnowFar, _TrodColor, _ForestFloor;
        half _RockTile, _AOStrength, _Sparkle;
        float4 _AORect;
        half _SC_Night;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float4 vcol;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcol = v.color;
        }

        float hash3(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float hash2(float2 p)
        {
            p = frac(p * float2(0.1031, 0.1030));
            p += dot(p, p.yx + 33.33);
            return frac((p.x + p.y) * p.x);
        }

        float vnoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = hash2(i), b = hash2(i + float2(1, 0)), c = hash2(i + float2(0, 1)), d = hash2(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex;
            float2 wp = IN.worldPos.xz;
            float dist = distance(IN.worldPos, _WorldSpaceCameraPos);

            // snow: photo detail near, its mean colour far (no visible tiling), a big soft variation everywhere
            half fade = saturate((dist - 25.0) / 150.0) * 0.85;
            half3 snow = lerp(tex2D(_MainTex, uv).rgb * _Color.rgb, _SnowFar.rgb, fade);
            half macro = vnoise(wp * 0.013) * 0.6 + vnoise(wp * 0.051) * 0.4;
            snow *= 0.94 + 0.10 * macro;

            // rock: a near and a large sample (less repetition), weight from the mesh, the texture and world noise
            half3 rock = lerp(tex2D(_RockTex, uv * _RockTile).rgb, tex2D(_RockTex, uv * _RockTile * 0.17).rgb, 0.4) * _RockColor.rgb;
            half rh = dot(rock, half3(0.3, 0.5, 0.2));
            half brk = vnoise(wp * 0.035) * 0.65 + vnoise(wp * 0.11) * 0.35;
            half rw = saturate((IN.vcol.r - 0.5 + (rh - 0.35) * 0.9 + (brk - 0.5) * 0.55) * 3.5 + 0.5);

            half3 ns = UnpackNormal(tex2D(_BumpMap, uv));
            ns.xy *= 1.0 - fade;
            half3 nr = UnpackNormal(tex2D(_RockBump, uv * _RockTile));
            o.Normal = normalize(lerp(ns, nr, rw));

            // ground maps over the village: ambient occlusion, trodden snow (paths, lots, tracks)
            float2 mUV = (wp - _AORect.xy) * _AORect.zw;
            half inside = (_AORect.z > 0.0 && mUV.x > 0.0 && mUV.x < 1.0 && mUV.y > 0.0 && mUV.y < 1.0) ? 1.0 : 0.0;
            half4 maps = tex2D(_AOTex, mUV);
            half ao = lerp(1.0, maps.r, _AOStrength * inside);
            half trod = maps.g * inside * saturate(0.7 + 0.6 * vnoise(wp * 0.9));

            half3 col = snow;
            col = lerp(col, col * _TrodColor.rgb, trod);
            col = lerp(col, col * _ForestFloor.rgb, saturate(IN.vcol.g * 0.85));
            col = lerp(col, rock, rw);
            o.Albedo = col * lerp(1.0, ao, 0.3);
            o.Metallic = 0.0;
            o.Smoothness = lerp(lerp(0.34, 0.46, trod), 0.16, rw);
            o.Occlusion = ao;
            // glitter: a few snow crystals per square metre catch the light now and then (much less by night, none on
            // trodden snow); only near the nest: far away a crystal is smaller than a pixel and would just shimmer
            float3 cell = floor(IN.worldPos * 22.0);
            float g = hash3(cell + floor(_Time.y * 1.7 + hash3(cell) * 7.0));
            half near = saturate(1.0 - dist / 45.0);
            half sparkle = step(0.9965, g) * (1.0 - rw) * (1.0 - trod) * _Sparkle * near * lerp(0.9, 0.12, saturate(_SC_Night));
            o.Emission = sparkle * half3(0.9, 0.95, 1.0);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Standard"
}

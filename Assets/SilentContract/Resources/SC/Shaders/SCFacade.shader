// Silent Contract — city walls (Standard inputs: albedo, normal map, metallic/smoothness map) with weathering, so a
// street of procedural blocks does not read as clean CG boxes: splash dirt at the foot of every wall, rain streaks down
// the facades, a broad tonal drift across the city (no two walls exactly the same shade), and a little extra dirt
// on the ledges and sills that face up. All in world space, no extra textures.
Shader "SC/Facade"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.3
        _GlossMapScale ("Smoothness Scale", Range(0, 1)) = 1
        _Grime ("Dirt at the foot", Range(0, 1)) = 0.3
        _Streaks ("Rain streaks", Range(0, 1)) = 0.22
        _Drift ("Tonal drift", Range(0, 0.5)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma shader_feature_local _NORMALMAP
        #pragma shader_feature_local _METALLICGLOSSMAP
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MetallicGlossMap;
        fixed4 _Color;
        half _BumpScale, _Metallic, _Glossiness, _GlossMapScale, _Grime, _Streaks, _Drift;
        half _SC_Wet;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

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
            fixed4 c = tex2D(_MainTex, uv) * _Color;
        #ifdef _NORMALMAP
            half3 n = UnpackNormal(tex2D(_BumpMap, uv));
            n.xy *= _BumpScale;
            o.Normal = normalize(n);
        #else
            o.Normal = half3(0, 0, 1);
        #endif
            half3 wn = WorldNormalVector(IN, o.Normal);
            float3 wp = IN.worldPos;
            // along the wall (the city is laid out on the axes) and up it
            float along = abs(wn.x) > abs(wn.z) ? wp.z : wp.x;
            half vert = 1.0 - saturate(abs(wn.y) * 2.0);
            // splash dirt at the foot of the wall
            half foot = (1.0 - smoothstep(0.25, 2.6, wp.y)) * _Grime * vert;
            // rain streaks: thin dark runs, longer under the top of the wall, broken up along the facade
            half s1 = vnoise(float2(along * 1.7, wp.y * 0.045));
            half s2 = vnoise(float2(along * 0.31 + 17.0, wp.y * 0.012));
            half streak = saturate((s1 * 0.65 + s2 * 0.5) - 0.55) * 2.2 * _Streaks * vert;
            // broad tonal drift (each wall a slightly different shade and warmth)
            half dr = vnoise(wp.xz * 0.021 + wp.y * 0.013) - 0.5;
            half3 drift = 1.0 + dr * _Drift * half3(1.1, 1.0, 0.8);
            // ledges facing up collect dust
            half ledge = saturate(wn.y - 0.6) * 0.18;
            half dirt = saturate(foot + streak + ledge);
            o.Albedo = c.rgb * drift * (1.0 - dirt * 0.55) * lerp(1.0, 0.7, saturate(_SC_Wet));
        #ifdef _METALLICGLOSSMAP
            half4 ms = tex2D(_MetallicGlossMap, uv);
            o.Metallic = ms.r;
            o.Smoothness = ms.a * _GlossMapScale;
        #else
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        #endif
            o.Smoothness = lerp(o.Smoothness * (1.0 - dirt * 0.5), max(o.Smoothness, 0.42), saturate(_SC_Wet) * 0.6);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Standard"
}

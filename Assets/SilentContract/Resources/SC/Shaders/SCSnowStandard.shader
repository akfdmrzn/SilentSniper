// Silent Contract — the Standard material of a prop with snow lying on it (mountain map). Same inputs as Unity's
// Standard (albedo, normal map, metallic/smoothness map, emission), plus a layer of snow on every surface that faces
// up, broken up by a little noise so the edges are not ruler straight. Global _SC_Snow (Env.cs): 0 none .. 1 full.
// Surfaces under cover (floors and rooms under a roof, balconies under the eaves, the station platform) stay clear: a
// top-down height map of the village (_SC_TopTex, B = height of the highest surface) tells what is under something.
Shader "SC/SnowStandard"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _GlossMapScale ("Smoothness Scale", Range(0, 1)) = 1
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        // materials are made at run time (copies of the props' Standard materials): every variant must be in the build
        #pragma multi_compile_local __ _NORMALMAP
        #pragma multi_compile_local __ _METALLICGLOSSMAP
        #pragma multi_compile_local __ _EMISSION
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MetallicGlossMap;
        sampler2D _EmissionMap;
        fixed4 _Color;
        half4 _EmissionColor;
        half _BumpScale, _Metallic, _Glossiness, _GlossMapScale;
        half _SC_Snow;
        sampler2D _SC_TopTex;
        float4 _SC_TopRect;     // xMin, zMin, 1/sizeX, 1/sizeZ (z = 0: no map)
        float4 _SC_TopRange;    // yMin, yMax - yMin

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
            n = normalize(n);
        #else
            half3 n = half3(0, 0, 1);
        #endif
        #ifdef _METALLICGLOSSMAP
            half4 ms = tex2D(_MetallicGlossMap, uv);
            half metal = ms.r;
            half smooth = ms.a * _GlossMapScale;
        #else
            half metal = _Metallic;
            half smooth = _Glossiness;
        #endif
            // the geometric normal decides where snow can lie (a normal map would sprinkle it on walls)
            float3 wn = WorldNormalVector(IN, float3(0, 0, 1));
            float nz = vnoise(IN.worldPos.xz * 1.3) * 0.6 + vnoise(IN.worldPos.xz * 5.1) * 0.4;
            half w = saturate((wn.y - 0.58 + (nz - 0.5) * 0.3) * 5.5) * saturate(_SC_Snow);
            float2 tuv = (IN.worldPos.xz - _SC_TopRect.xy) * _SC_TopRect.zw;
            if (_SC_TopRect.z > 0.0 && tuv.x > 0.0 && tuv.x < 1.0 && tuv.y > 0.0 && tuv.y < 1.0)
            {
                float topY = _SC_TopRange.x + tex2Dlod(_SC_TopTex, float4(tuv, 0, 0)).b * _SC_TopRange.y;
                w *= 1.0 - saturate((topY - IN.worldPos.y - 0.45) / 0.6);        // something above: under cover
            }
            o.Normal = normalize(lerp(n, half3(0, 0, 1), w * 0.85));
            o.Albedo = lerp(c.rgb, half3(0.90, 0.92, 0.95), w);
            o.Metallic = lerp(metal, 0.0, w);
            o.Smoothness = lerp(smooth, 0.36, w);
        #ifdef _EMISSION
            o.Emission = tex2D(_EmissionMap, uv).rgb * _EmissionColor.rgb;
        #endif
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Standard"
}

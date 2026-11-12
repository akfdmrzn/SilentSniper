// Silent Contract — yard / quay ground: Standard lighting + baked top-down ambient occlusion (world XZ) so containers,
// buildings and vehicles sit on the ground with soft contact shadows, plus a global wetness for rainy weather.
// V3: an optional world-XZ detail mask for the map (globals from Level: asphalt repairs and wear in R, oil stains,
// tyre tracks and panel joints in G) and a second, larger scale of the surface texture that breaks its tiling — on
// up-facing surfaces only.
Shader "SC/Ground"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _UseMS ("Use Metallic/Smoothness Map", Float) = 0
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.2
        _GlossMapScale ("Smoothness Scale", Range(0, 1)) = 1
        _AOTex ("Ground AO (world XZ)", 2D) = "white" {}
        _AORect ("AO rect (xMin, zMin, 1/sizeX, 1/sizeZ)", Vector) = (0, 0, 0, 0)
        _AOStrength ("AO Strength", Range(0, 1)) = 1
        _PoolGain ("Lamp pool gain", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MetallicGlossMap;
        sampler2D _AOTex;
        fixed4 _Color;
        half _BumpScale, _UseMS, _Metallic, _Glossiness, _GlossMapScale, _AOStrength, _PoolGain;
        float4 _AORect;
        half _SC_Wet;      // global (Env.cs): 0 dry .. 1 soaked
        // global (Level.cs): detail mask of the map, rect (xMin, zMin, 1/sizeX, 1/sizeZ), strengths (tint, stains,
        // second scale, second scale strength); all zero = off
        sampler2D _SC_GroundMask;
        float4 _SC_GroundMaskRect;
        float4 _SC_GroundMaskK;
        float4 _SC_NightPool;   // global (Env.cs): colour x strength of the street lamps' baked pools (mask B), 0 by day

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float upN;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.upN = UnityObjectToWorldNormal(v.normal).y;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half4 ms = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            half3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            n.xy *= _BumpScale;
            o.Normal = normalize(n);

            float2 aoUV = (IN.worldPos.xz - _AORect.xy) * _AORect.zw;
            half inside = (_AORect.z > 0.0 && aoUV.x > 0.0 && aoUV.x < 1.0 && aoUV.y > 0.0 && aoUV.y < 1.0) ? 1.0 : 0.0;
            half ao = lerp(1.0, tex2D(_AOTex, aoUV).r, _AOStrength * inside);

            half wet = saturate(_SC_Wet);
            half metal = lerp(_Metallic, ms.r, _UseMS);
            half smooth = lerp(_Glossiness, ms.a * _GlossMapScale, _UseMS);

            // V3 detail mask + second texture scale (up-facing surfaces of the map's ground only)
            float2 muv = (IN.worldPos.xz - _SC_GroundMaskRect.xy) * _SC_GroundMaskRect.zw;
            half km = (_SC_GroundMaskRect.z > 0.0 && muv.x > 0.0 && muv.x < 1.0 && muv.y > 0.0 && muv.y < 1.0) ? saturate(IN.upN * 4.0 - 3.0) : 0.0;
            half pool = 0.0;
            {
                half4 mk = tex2D(_SC_GroundMask, muv);
                pool = mk.b * km;
                c.rgb *= lerp(1.0, mk.r * 2.0, _SC_GroundMaskK.x * km);
                half stain = saturate(mk.g * _SC_GroundMaskK.y * km);
                c.rgb *= 1.0 - stain * 0.72;
                smooth = lerp(smooth, 0.5, stain * 0.55);
                half3 c2 = tex2D(_MainTex, IN.uv_MainTex * _SC_GroundMaskK.z + float2(0.37, 0.61)).rgb;
                half3 cm = tex2Dlod(_MainTex, float4(IN.uv_MainTex, 0.0, 12.0)).rgb;
                half k2 = dot(c2, half3(0.333, 0.333, 0.334)) / max(dot(cm, half3(0.333, 0.333, 0.334)), 0.02);
                c.rgb *= lerp(1.0, clamp(k2, 0.6, 1.5), _SC_GroundMaskK.w * km);
            }
            // occlusion darkens the sky light; a little of it also on the albedo so contact shadows read in sunlight
            o.Albedo = c.rgb * lerp(1.0, 0.62, wet) * lerp(1.0, ao, 0.3);
            o.Metallic = metal;
            o.Smoothness = lerp(smooth, max(smooth, 0.44), wet);     // damp, not mirror-wet: grazing reflections stay soft
            o.Occlusion = ao;
            // the lamps' pools of light at night (baked: one big ground mesh only gets a few pixel lights)
            // (grass and flower beds take less of it: lit green read as a glowing carpet at night)
            o.Emission = o.Albedo * pool * _PoolGain * _SC_NightPool.rgb * lerp(1.0, 1.6, wet);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Standard"
}

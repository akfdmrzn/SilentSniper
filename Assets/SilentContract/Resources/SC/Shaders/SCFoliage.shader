// Silent Contract — tree canopies (leaf cluster cards), Built-in RP forward, mobile friendly.
// Alpha tested and double sided; the cards carry "canopy" normals (pointing out of the crown) so a crown shades as one
// soft volume instead of a pile of flat cards; vertex colour R = how deep in the crown (dark inside), G = tint variety.
// Backlit leaves let the sun through (translucency) — the golden rim of trees against a low sun — and the crown sways
// a little in the wind (vertex colour A = how far out on the branch).
Shader "SC/Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Leaves (RGB) Alpha (A)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.42
        _Glossiness ("Smoothness", Range(0, 1)) = 0.18
        _Translucency ("Translucency", Range(0, 2)) = 0.8
        _TransPower ("Translucency focus", Range(1, 16)) = 4
        _DeepShade ("Shade deep in the crown", Range(0, 1)) = 0.55
        _TintVar ("Tint variety (dry, fresh)", Color) = (0.92, 0.96, 0.62, 1)
        _Sway ("Sway (m)", Range(0, 0.5)) = 0.08
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Foliage fullforwardshadows alphatest:_Cutoff addshadow vertex:vert
        #pragma target 3.0
        #include "UnityPBSLighting.cginc"
        sampler2D _MainTex;
        fixed4 _Color, _TintVar;
        half _Glossiness, _Translucency, _TransPower, _DeepShade, _Sway;
        float _SC_Wind;

        struct Input { float2 uv_MainTex; float4 color : COLOR; };

        void vert(inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float t = _Time.y;
            float w = (0.35 + saturate(_SC_Wind)) * _Sway * v.color.a;
            float ph = dot(wp.xz, float2(0.11, 0.07));
            v.vertex.x += sin(t * 1.3 + ph) * w;
            v.vertex.z += cos(t * 1.1 + ph * 1.3) * w * 0.7;
        }

        struct SurfaceOutputFoliage
        {
            fixed3 Albedo; fixed3 Normal; half3 Emission; half Metallic; half Smoothness; half Occlusion; fixed Alpha;
        };

        inline half4 LightingFoliage(SurfaceOutputFoliage s, half3 viewDir, UnityGI gi)
        {
            SurfaceOutputStandard st;
            st.Albedo = s.Albedo; st.Normal = s.Normal; st.Emission = s.Emission; st.Metallic = 0; st.Smoothness = s.Smoothness;
            st.Occlusion = s.Occlusion; st.Alpha = s.Alpha;
            half4 c = LightingStandard(st, viewDir, gi);
            // light through the leaves: strongest looking into the sun, independent of the card's normal
            half back = pow(saturate(dot(viewDir, -gi.light.dir)), _TransPower);
            half wrap = saturate(dot(-s.Normal, gi.light.dir) * 0.6 + 0.4);
            c.rgb += s.Albedo * gi.light.color * _Translucency * (back * 1.6 + wrap * 0.25) * s.Occlusion;
            return c;
        }

        inline void LightingFoliage_GI(SurfaceOutputFoliage s, UnityGIInput data, inout UnityGI gi)
        {
            Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(s.Smoothness, data.worldViewDir, s.Normal, lerp(unity_ColorSpaceDielectricSpec.rgb, s.Albedo, 0));
            gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal, g);
        }

        void surf(Input IN, inout SurfaceOutputFoliage o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            // tint variety per card (G), darker deep in the crown (R)
            c.rgb *= lerp(_TintVar.rgb, fixed3(1, 1, 1), IN.color.g);
            half deep = IN.color.r;
            o.Albedo = c.rgb * lerp(1.0 - _DeepShade, 1.0, deep);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Occlusion = lerp(0.35, 1.0, deep);
            o.Emission = 0;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}

// Silent Contract — the far forest of Kartal Tepe: impostor cards of the firs (alpha-tested atlas), each turned to face
// the sniper's nest. Lit like the snow around them (the normal leans back towards the nest), a random tint per card
// (vertex colour RGB), the crowns sway in the wind (vertex colour A: 0 at the foot, 1 at the top; global _SC_Wind).
Shader "SC/ForestCard"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Atlas (RGB) Alpha (A)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert alphatest:_Cutoff vertex:vert addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _SC_Wind;
        struct Input
        {
            float2 uv_MainTex;
            fixed4 color : COLOR;
        };

        void vert(inout appdata_full v)
        {
            float h = v.color.a * v.color.a;
            float ph = v.vertex.x * 0.05 + v.vertex.z * 0.07;
            float a = 0.05 + 0.32 * _SC_Wind;
            v.vertex.x += sin(_Time.y * 1.4 + ph) * a * h;
            v.vertex.z += cos(_Time.y * 1.1 + ph * 1.3) * a * h * 0.6;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = c.rgb * IN.color.rgb * _Color.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}

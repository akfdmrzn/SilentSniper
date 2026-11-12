// Silent Contract — blurred copy of the planar water reflection (PlanarReflection.cs)
// pass 0: 2x2 box downsample from mip 0 (HDR highlights limited per tap so bloom cannot flicker on them)
// pass 1: 9-tap gaussian along _BlurDir (uv step between taps; sigma = 2 steps)
Shader "Hidden/SC/ReflBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        float4 _BlurDir;

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f vert(appdata_img v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord;
            return o;
        }

        // each tap limited by luminance before averaging: one hot pixel cannot light up a whole block
        half4 Tap(float2 uv)
        {
            half4 c = tex2Dlod(_MainTex, float4(uv, 0.0, 0.0));
            c.rgb *= min(1.0, 4.0 / max(Luminance(c.rgb), 1e-3));
            return c;
        }

        half4 fragDown(v2f i) : SV_Target
        {
            float2 o = _MainTex_TexelSize.xy * 0.5;
            return (Tap(i.uv + float2(-o.x, -o.y)) + Tap(i.uv + float2(o.x, -o.y))
                  + Tap(i.uv + float2(-o.x, o.y)) + Tap(i.uv + float2(o.x, o.y))) * 0.25;
        }

        half4 fragBlur(v2f i) : SV_Target
        {
            float2 d = _BlurDir.xy;
            half4 c = tex2D(_MainTex, i.uv) * 0.2042;
            c += (tex2D(_MainTex, i.uv + d) + tex2D(_MainTex, i.uv - d)) * 0.1802;
            c += (tex2D(_MainTex, i.uv + d * 2.0) + tex2D(_MainTex, i.uv - d * 2.0)) * 0.1238;
            c += (tex2D(_MainTex, i.uv + d * 3.0) + tex2D(_MainTex, i.uv - d * 3.0)) * 0.0663;
            c += (tex2D(_MainTex, i.uv + d * 4.0) + tex2D(_MainTex, i.uv - d * 4.0)) * 0.0276;
            return c;
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG
        }
    }
    FallBack Off
}

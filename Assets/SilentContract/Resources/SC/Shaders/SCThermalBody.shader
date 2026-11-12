// Silent Contract — thermal scope body heat: drawn additively over the scene for every living character (VisionFX),
// very bright so the post pass maps people to white-hot. Faces towards the viewer read hotter (thin cloth, skin).
Shader "Hidden/SC/ThermalBody"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _SC_Heat;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 n : TEXCOORD0;
                float3 v : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.v = WorldSpaceViewDir(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float ndv = saturate(dot(normalize(i.n), normalize(i.v)));
                float h = _SC_Heat * (0.55 + 0.45 * ndv);
                return half4(h * 26.0, h * 26.0, h * 26.0, 0.0);
            }
            ENDCG
        }
    }
}

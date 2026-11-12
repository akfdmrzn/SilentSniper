// Silent Contract — city windows (Built-in RP, forward, mobile friendly)
// Every window quad shows one cell of a 4x4 interior atlas (rooms, offices, shops). The room picture sits a little
// behind the glass and shifts with the view (parallax), the glass reflects the environment probe with a Fresnel
// term, and at night the rooms marked "lit" in the atlas alpha glow (global _SC_Night: 0 day .. 1 night) while the
// others stay dark. Instancing friendly, no textures beyond the atlas.
Shader "SC/Window"
{
    Properties
    {
        _MainTex ("Interior atlas (A = lit at night)", 2D) = "black" {}
        _Depth ("Room depth (m)", Range(0, 4)) = 1.6
        _CellSize ("Window size in metres (u, v)", Vector) = (1.4, 1.9, 0, 0)
        _DayLight ("Interior brightness by day", Range(0, 1)) = 0.16
        _NightLight ("Lit room brightness at night", Range(0, 4)) = 1.45
        _Reflect ("Glass reflection", Range(0, 2)) = 1.0
        _Tint ("Glass tint", Color) = (0.82, 0.88, 0.92, 1)
        _F0 ("Glass reflectance head-on (coated office glass is high)", Range(0, 1)) = 0.05
        _Glint ("Sun glint", Range(0, 60)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            sampler2D _MainTex;
            float _Depth, _DayLight, _NightLight, _Reflect, _F0, _Glint;
            float4 _CellSize, _Tint;
            float _SC_Night;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                half3 wnormal : TEXCOORD2;
                half3 wtangent : TEXCOORD3;
                half3 wbitangent : TEXCOORD4;
                UNITY_FOG_COORDS(5)
            };

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wnormal = UnityObjectToWorldNormal(v.normal);
                o.wtangent = UnityObjectToWorldDir(v.tangent.xyz);
                o.wbitangent = cross(o.wnormal, o.wtangent) * v.tangent.w * unity_WorldTransformParams.w;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half3 N = normalize(i.wnormal);
                half3 T = normalize(i.wtangent);
                half3 B = normalize(i.wbitangent);
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                half ndv = saturate(dot(N, V));

                // the room picture is _Depth behind the glass: offset along the facade, in atlas units of this cell
                float2 cell = floor(i.uv * 4.0);
                float2 cmin = cell * 0.25 + 0.006;
                float2 cmax = cmin + 0.25 - 0.012;
                float2 tv = float2(dot(V, T), dot(V, B)) / max(ndv, 0.25);
                float2 off = -tv * _Depth * (0.25 / max(_CellSize.xy, 0.2)) * 0.35;
                float2 uv = clamp(i.uv + off, cmin, cmax);
                half4 room = tex2D(_MainTex, uv);

                // lights: by day the rooms are dim behind tinted glass; at night lit rooms glow, the rest are dark
                half night = saturate(_SC_Night);
                half lit = room.a;
                half k = lerp(_DayLight, lerp(0.035, _NightLight, lit), night);
                half3 interior = room.rgb * k;
                // a soft frame shadow at the edges of the opening (reads as depth)
                float2 e = min(i.uv - cell * 0.25, (cell + 1.0) * 0.25 - i.uv) * 4.0;
                half edge = saturate(min(e.x, e.y) * 9.0);
                interior *= lerp(0.45, 1.0, edge);

                // glass: environment reflection, stronger at grazing angles
                half3 R = reflect(-V, N);
                // V3: coated glass (towers) mirrors the sky sharper and stronger than a flat's window
                half4 env = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, R, lerp(2.0, 0.6, saturate(_F0 * 4.0)));
                half3 envc = DecodeHDR(env, unity_SpecCube0_HDR);
                half fres = _F0 + (1.0 - _F0) * pow(1.0 - ndv, 5.0);
                half3 col = interior * _Tint.rgb * (1.0 - fres * 0.75) + envc * fres * _Reflect * lerp(1.0, 0.55, night);
                // the low sun caught in the glass (main directional light)
                half g = pow(saturate(dot(R, _WorldSpaceLightPos0.xyz)), 700.0) * _Glint * (1.0 - night);
                col += _LightColor0.rgb * g * fres;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return half4(col, 1.0);
            }
            ENDCG
        }
        // shadow caster: windows are part of the facade
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct v2s { V2F_SHADOW_CASTER; };
            v2s vs (appdata_base v) { v2s o; TRANSFER_SHADOW_CASTER_NORMALOFFSET(o) return o; }
            float4 fs (v2s i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
    FallBack Off
}

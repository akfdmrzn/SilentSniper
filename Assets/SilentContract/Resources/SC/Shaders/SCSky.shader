// Silent Contract — sky dome: the photographed panorama (lat-long, the Skybox/Panoramic convention: rotation, tint,
// exposure) with an art-directed sunset on top:
// - grade: recolours the photo's sky towards a ramp (horizon towards / away from the sun, zenith) while keeping its
//   clouds and brightness structure (luminance), so a cool blue evening reads as a golden one
// - the sun: a soft HDR disc and a tight + wide glow round it (the post bloom turns them into a halo)
// - a haze band at the horizon in the same colour the post haze gives far objects: the sea, the far shore and the sky
//   meet without a seam
Shader "SC/Sky"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panorama (lat-long)", 2D) = "grey" {}
        _Tint ("Tint", Color) = (0.5, 0.5, 0.5, 0.5)
        _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
        _GradeSun ("Grade: horizon towards the sun", Color) = (1, 1, 1, 1)
        _GradeAway ("Grade: horizon away from the sun", Color) = (1, 1, 1, 1)
        _GradeZenith ("Grade: zenith", Color) = (1, 1, 1, 1)
        _Grade ("Grade (amount, sun power, height power, gain)", Vector) = (0, 2, 0.5, 1)
        _Glow ("Sun glow (tight, tight power, wide, wide power)", Vector) = (0, 400, 0, 8)
        _Disc ("Sun disc (intensity, cos radius, edge)", Vector) = (0, 0.99996, 0.00002, 0)
        _HorizonHaze ("Horizon haze (amount, height)", Vector) = (0, 0.06, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SCAtmos.cginc"

            sampler2D _MainTex;
            half4 _MainTex_HDR;
            half4 _Tint;
            half _Exposure;
            float _Rotation;
            half4 _GradeSun, _GradeAway, _GradeZenith;
            float4 _Grade, _Glow, _Disc, _HorizonHaze;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; float3 tdir : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                // the texture direction: the world direction turned back by the sky rotation (Panoramic: world = R(rot) * tex)
                float s, c;
                sincos(-_Rotation * UNITY_PI / 180.0, s, c);
                o.tdir = float3(c * v.vertex.x - s * v.vertex.z, v.vertex.y, s * v.vertex.x + c * v.vertex.z);
                return o;
            }

            float2 ToRadial(float3 d)
            {
                float3 n = normalize(d);
                float lat = acos(clamp(n.y, -1.0, 1.0));
                float lon = atan2(n.z, n.x);
                float2 sc = float2(lon, lat) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
                return float2(0.5, 1.0) - sc;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                // the panorama is always magnified on screen: level 0, no mip seam where atan2 wraps
                float4 tex = tex2Dlod(_MainTex, float4(ToRadial(i.tdir), 0.0, 0.0));
                float3 c = DecodeHDR(tex, _MainTex_HDR) * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;

                float3 S = _SC_SunSkyDir.xyz;
                float mu = dot(d, S);
                // grade: the photo's brightness, a sunset ramp's colour
                float sunK = pow(saturate(mu * 0.5 + 0.5), max(_Grade.y, 0.01));
                float3 horizon = lerp(_GradeAway.rgb, _GradeSun.rgb, sunK);
                float3 ramp = lerp(horizon, _GradeZenith.rgb, pow(saturate(d.y), max(_Grade.z, 0.01)));
                float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(c, lum * ramp, _Grade.x) * _Grade.w;

                // haze at the horizon (and below it, where only the far edge of the sea could ever show the panorama)
                float band = exp(-max(d.y, 0.0) / max(_HorizonHaze.y, 1e-3)) * _HorizonHaze.x;
                band = max(band, saturate(-d.y * 30.0) * step(0.001, _HorizonHaze.x));
                c = lerp(c, SC_HazeColor(d), saturate(band));

                // the sun
                float m = saturate(mu);
                c += _SC_SunGlowColor.rgb * (_Glow.x * pow(m, max(_Glow.y, 1.0)) + _Glow.z * pow(m, max(_Glow.w, 1.0)));
                c += _SC_SunGlowColor.rgb * _Disc.x * smoothstep(_Disc.y - _Disc.z, _Disc.y, mu);
                return half4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}

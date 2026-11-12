// Silent Contract — harbour water (Built-in RP, forward, mobile friendly)
// Slightly wavy harbour water seen from a high roof:
// - three scrolling normal layers at harbour scale (long swell, wind waves, ripples), rotated against each other so
//   the pattern never visibly repeats
// - planar reflection (PlanarReflection.cs) shifted by the waves the eye can resolve, mostly vertically, the way
//   reflections stretch on real water at grazing angles
// - the waves too small to see from here blur the reflection (a pre-blurred, vertically streaked copy) — sharp where
//   the far shore touches the water, streakier towards the viewer ("contact hardening")
// - Schlick fresnel over a dark water body, sun glitter that widens with the roughness, no HDR fireflies
Shader "SC/Water"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.02, 0.05, 0.06, 1)
        _ShallowColor ("Shallow Color", Color) = (0.04, 0.10, 0.11, 1)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Wave Strength", Range(0, 3)) = 1
        _WaveTile ("Layer size m (swell, wind, ripple)", Vector) = (900, 160, 32, 0)
        _WaveWeight ("Layer weight (swell, wind, ripple)", Vector) = (0.08, 0.055, 0.04, 0)
        _WaveSpeed ("Layer speed m/s (swell, wind, ripple)", Vector) = (2.0, 1.2, 0.6, 0)
        _ReflStrength ("Reflection", Range(0, 1.5)) = 1
        _ReflDistort ("Reflection shift (sideways, vertical, max)", Vector) = (1, 1, 0.08, 0)
        _ReflBlur ("Reflection blur", Range(0, 2)) = 0.55
        _BlurWidth ("Blurred copy width (screen fraction)", Float) = 0.02
        _Shore ("Far shore circle (x, z, radius)", Vector) = (0, 300, 1500, 0)
        _PlanarK ("Planar Reflection", Range(0, 1)) = 0
        _ReflectionTex ("Reflection (set by script)", 2D) = "black" {}
        _ReflectionBlur ("Reflection blurred (set by script)", 2D) = "black" {}
        _SunSpec ("Sun Specular", Range(0, 24)) = 2.5
        _LowSun ("Low sun glitter (sunset over the sea)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        LOD 200
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _NormalMap;
            float4 _NormalMap_TexelSize;
            sampler2D _ReflectionTex;
            sampler2D _ReflectionBlur;
            float4 _DeepColor, _ShallowColor, _WaveTile, _WaveWeight, _WaveSpeed, _ReflDistort, _Shore;
            float _NormalScale, _ReflStrength, _ReflBlur, _BlurWidth, _PlanarK, _SunSpec, _LowSun;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float4 screen : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                half3 sky : TEXCOORD3;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wpos = w;
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1.0));
                o.screen = ComputeScreenPos(o.pos);
                o.sky = max(half3(0, 0, 0), ShadeSH9(half4(0, 1, 0, 1)));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // Slope variance the normal map keeps after mip filtering at a given level (measured on the generated
            // map: 1, 0.80, 0.48, 0.20, 0.05 for levels 0-4). The rest is averaged away = waves too small to see.
            float Kept(float lod)
            {
                return 1.0 / (1.0 + exp(1.34 * (lod - 1.95)));
            }

            // One wave layer: world-space slope (x, z) of a rotated, scrolling copy of the normal map, plus the slope
            // variance the pixel cannot resolve.
            float2 Layer(float2 p, float tile, float2 dir, float speed, float weight, float texPerPixel, inout float lost)
            {
                float2 q = float2(p.x * dir.x + p.y * dir.y, -p.x * dir.y + p.y * dir.x);
                // two copies crossing at ~70 degrees with different speeds: the pattern ripples in place instead of the whole
                // sheet sliding one way (seen from the menu's wide shot that read as the sea "sliding")
                float2 uv = q / tile + float2(_Time.y * speed / tile, 0.0);
                float2 q2 = float2(q.x * 0.342 - q.y * 0.940, q.x * 0.940 + q.y * 0.342) * 1.13;
                float2 uv2 = q2 / tile + float2(-_Time.y * speed * 0.73 / tile, 0.37);
                float2 n1 = UnpackNormal(tex2D(_NormalMap, uv)).xy;
                float2 n2 = UnpackNormal(tex2D(_NormalMap, uv2)).xy;
                // the second copy's slope back into this layer's frame (rotated by -70 degrees)
                n2 = float2(n2.x * 0.342 + n2.y * 0.940, -n2.x * 0.940 + n2.y * 0.342);
                float2 n = (n1 + n2) * 0.72;
                float lod = log2(max(texPerPixel / tile, 1e-4));
                lost += weight * weight * 0.068 * (1.0 - Kept(lod));
                n *= weight;
                return float2(n.x * dir.x - n.y * dir.y, n.x * dir.y + n.y * dir.x);
            }

            // HDR highlights (speculars, lamps) limited by luminance: no flickering bloom blocks, hue kept
            half3 Tame(half3 c, half maxLum)
            {
                return c * min(1.0, maxLum / max(Luminance(c), 1e-3));
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 toCam = _WorldSpaceCameraPos - i.wpos;
                float dist = length(toCam);
                float3 V = toCam / max(dist, 1e-4);
                float2 p = i.wpos.xz;

                // ---- waves
                // footprint of one pixel on the water (along the longer axis) in normal-map texels
                float2 dpx = ddx(p), dpy = ddy(p);
                float texPerPixel = sqrt(max(dot(dpx, dpx), dot(dpy, dpy))) * _NormalMap_TexelSize.z;
                float lost = 0.0;
                float2 slope = Layer(p, _WaveTile.x, float2(0.9397, 0.3420), _WaveSpeed.x, _WaveWeight.x, texPerPixel, lost);   // 20 deg
                slope += Layer(p, _WaveTile.y, float2(0.5736, 0.8192), _WaveSpeed.y, _WaveWeight.y, texPerPixel, lost);        // 55 deg
                slope += Layer(p, _WaveTile.z, float2(0.8660, -0.5000), _WaveSpeed.z, _WaveWeight.z, texPerPixel, lost);       // -30 deg
                slope *= _NormalScale;
                float sigmaLost = sqrt(lost) * _NormalScale;
                float3 N = normalize(float3(-slope.x, 1.0, -slope.y));

                float ndv = saturate(dot(N, V));
                float fres = 0.02 + 0.98 * pow(1.0 - ndv, 5.0);

                // ---- reflection
                half3 refl;
                if (_PlanarK > 0.5)
                {
                    // A facet tilted towards the viewer by s turns the reflected ray up by 2s, which in the mirror
                    // image is further down the screen: s * cot(fov/2), and much less sideways at grazing angles.
                    // A reflected point at distance D behind this water point (distance d) moves by 2s(1 - d/D), so
                    // reflections stay crisp where the far shore meets the water and get streakier towards the viewer.
                    float hd = length(toCam.xz);
                    float2 fwd = -toCam.xz / max(hd, 1e-3);
                    float2 rgt = float2(fwd.y, -fwd.x);
                    float2 c = _WorldSpaceCameraPos.xz - _Shore.xy;
                    float b = -dot(fwd, c);
                    float shoreDist = b + sqrt(max(b * b - (dot(c, c) - _Shore.z * _Shore.z), 0.0));
                    float contact = clamp(1.0 - hd / max(shoreDist, 1.0), 0.03, 1.0);
                    float proj11 = unity_CameraProjection._m11;
                    float proj00 = unity_CameraProjection._m00;
                    float elev = saturate(V.y);
                    float2 shift = -float2(dot(slope, rgt) * elev * proj00 * _ReflDistort.x,
                                           dot(slope, fwd) * proj11 * _ReflDistort.y) * contact;
                    // soft limit (zoomed scope: proj11 is large) instead of a hard clamp that would make ghost copies
                    shift *= rsqrt(1.0 + dot(shift, shift) / (_ReflDistort.z * _ReflDistort.z));
                    float2 suv = i.screen.xy / max(i.screen.w, 1e-4) + shift;
                    half3 sharp = tex2Dlod(_ReflectionTex, float4(suv, 0.0, 0.0)).rgb;
                    half3 soft = tex2Dlod(_ReflectionBlur, float4(suv, 0.0, 0.0)).rgb;
                    // far away (as seen on screen) the mirror is never razor sharp: a little of the soft copy
                    sharp = lerp(sharp, soft, saturate((dist * 1.92 / proj11 - 500.0) / 1500.0) * 0.3);
                    float spread = sigmaLost * proj11 * _ReflBlur * contact;
                    refl = Tame(lerp(sharp, soft, saturate(spread / max(_BlurWidth, 1e-3))), 4.0);
                }
                else
                {
                    float3 R = reflect(-V, N);
                    R.y = abs(R.y);
                    refl = Tame(DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, R, 1.0 + sigmaLost * 40.0), unity_SpecCube0_HDR), 4.0);
                }
                refl *= _ReflStrength;

                // ---- dark water body lit by the sky and a little sun
                float sunUp = saturate(_WorldSpaceLightPos0.y);
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(ndv * 1.4)) * (i.sky + _LightColor0.rgb * sunUp * 0.3);

                // ---- sun glitter: the lobe widens with the unresolved roughness (no sparkling single pixels far away)
                float3 L = _WorldSpaceLightPos0.xyz;                // directional light: already unit length
                float3 H = normalize(L + V);
                float rough2 = sigmaLost * sigmaLost + 0.0006;
                float power = clamp(0.5 / rough2, 40.0, 1500.0);
                // a sun low over the sea lays a long bright path on it (grazing reflection): keep the glitter strong down to
                // a few degrees and let it bloom
                float glitUp = lerp(sunUp, saturate(_WorldSpaceLightPos0.y * 7.0), _LowSun);
                float spec = pow(saturate(dot(N, H)), power) * _SunSpec * glitUp * sqrt(power / 600.0);
                spec = min(spec, lerp(3.0, 14.0, _LowSun));

                half4 col = half4(lerp(body, refl, fres) + spec * _LightColor0.rgb, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}

// Silent Contract — compact post processing: haze (aerial perspective from the depth buffer, sky-coloured: golden
// towards a low sun), bloom (dual filter), exposure, AgX tonemapping (the same view transform Blender uses, so the game
// matches the preview renders), a "look" (contrast curve, saturation, split toning), tint, vignette.
Shader "Hidden/SC/PostFX"
{
    Properties { _MainTex ("", 2D) = "white" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "SCAtmos.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float _Threshold, _Knee, _BloomIntensity, _Exposure, _Saturation, _LookPower, _Vignette, _VignettePower, _OutputSRGB;
    float4 _Tint, _Lift;
    float _Vision, _VisionK, _NVGain, _VisionTime;
    // look
    float _Contrast;
    float4 _ShadowTint, _HighlightTint;
    // haze: x density (1/m) at the base height, y height falloff (1/m), z base height (m), w max amount
    float4 _SC_Haze;
    float _SC_HazeStart;
    float _HazeOn;
    float4x4 _FrustumRays;     // rows: bottom-left, bottom-right, top-left, top-right (world offset per metre of view depth)

    float Hash12(float2 p)
    {
        float3 p3 = frac(float3(p.xyx) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.x + p3.y) * p3.z);
    }

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    // passes that read the depth: the depth uv (flipped like the image on some targets) and the view ray
    struct v2fh { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 uvd : TEXCOORD1; float3 ray : TEXCOORD2; };
    v2fh vertHaze(appdata_img v)
    {
        v2fh o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        o.uvd = v.texcoord;
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0) o.uvd.y = 1.0 - o.uvd.y;
        #endif
        float3 b = lerp(_FrustumRays[0].xyz, _FrustumRays[1].xyz, o.uvd.x);
        float3 t = lerp(_FrustumRays[2].xyz, _FrustumRays[3].xyz, o.uvd.x);
        o.ray = lerp(b, t, o.uvd.y);
        return o;
    }

    // exponential height fog in the colour of the air along the view: thicker down by the water, thinner up high
    float3 ApplyHaze(float3 c, float2 uvd, float3 ray1)
    {
        if (_HazeOn < 0.5) return c;
        float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uvd);
        #if defined(UNITY_REVERSED_Z)
        if (raw <= 1e-6) return c;              // the sky has its own haze band
        #else
        if (raw >= 0.999999) return c;
        #endif
        float eye = LinearEyeDepth(raw);
        float3 ray = ray1 * eye;
        float dist = length(ray);
        float3 d = ray / max(dist, 1e-3);
        float camH = _WorldSpaceCameraPos.y - _SC_Haze.z;
        float k = _SC_Haze.y * ray.y;
        float lineK = abs(k) > 1e-4 ? (1.0 - exp(-k)) / k : 1.0;
        float od = _SC_Haze.x * exp(-_SC_Haze.y * camH) * max(dist - _SC_HazeStart, 0.0) * lineK;
        float f = (1.0 - exp(-max(od, 0.0))) * _SC_Haze.w;
        return lerp(c, SC_HazeColor(d), f);
    }

    half3 Prefilter(half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half rq = clamp(br - _Threshold + _Knee, 0, 2 * _Knee);
        rq = (rq * rq) / (4 * _Knee + 1e-4);
        half w = max(rq, br - _Threshold) / max(br, 1e-4);
        return c * w;
    }

    half3 Down4(float2 uv)
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(-1, -1, 1, 1);
        half3 s = tex2D(_MainTex, uv + d.xy).rgb + tex2D(_MainTex, uv + d.zy).rgb + tex2D(_MainTex, uv + d.xw).rgb + tex2D(_MainTex, uv + d.zw).rgb;
        return s * 0.25;
    }

    half4 fragPrefilter(v2fh i) : SV_Target
    {
        float3 c = min(Down4(i.uv), 64.0);
        c = ApplyHaze(c, i.uvd, i.ray);
        return half4(Prefilter(c), 1);
    }
    half4 fragDown(v2f i) : SV_Target { return half4(Down4(i.uv), 1); }
    half4 fragUp(v2f i) : SV_Target
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(-0.5, -0.5, 0.5, 0.5);
        half3 s = tex2D(_MainTex, i.uv + d.xy).rgb + tex2D(_MainTex, i.uv + d.zy).rgb + tex2D(_MainTex, i.uv + d.xw).rgb + tex2D(_MainTex, i.uv + d.zw).rgb;
        return half4(s * 0.25, 1);
    }

    // ---- AgX (Troy Sobotka), polynomial fit of the default contrast curve
    float3 AgXContrast(float3 x)
    {
        float3 x2 = x * x;
        float3 x4 = x2 * x2;
        return 15.5 * x4 * x2 - 40.14 * x4 * x + 31.96 * x4 - 6.868 * x2 * x + 0.4298 * x2 + 0.1191 * x - 0.00232;
    }

    float3 AgX(float3 c)
    {
        const float3x3 inset = float3x3(
            0.842479062253094, 0.0784335999999992, 0.0792237451477643,
            0.0423282422610123, 0.878468636469772, 0.0791661274605434,
            0.0423756549057051, 0.0784336, 0.879142973793104);
        const float minEv = -12.47393;
        const float maxEv = 4.026069;
        c = mul(inset, max(c, 0.0));
        c = clamp(log2(max(c, 1e-10)), minEv, maxEv);
        c = (c - minEv) / (maxEv - minEv);
        return AgXContrast(c);            // display encoded (~gamma 2.2)
    }

    float3 AgXOutset(float3 c)
    {
        const float3x3 outset = float3x3(
            1.19687900512017, -0.0980208811401368, -0.0990297440797205,
            -0.0528968517574562, 1.15190312990417, -0.0989611768448433,
            -0.0529716355144438, -0.0980434501171241, 1.15107367264116);
        return mul(outset, c);
    }

    half4 fragFinal(v2fh i) : SV_Target
    {
        float3 c = tex2D(_MainTex, i.uv).rgb;
        #if defined(UNITY_COLORSPACE_GAMMA)
        c = GammaToLinearSpace(c);
        #endif
        c = ApplyHaze(c, i.uvd, i.ray);
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
        c *= _Exposure;
        float lumHDR = dot(c, float3(0.2126, 0.7152, 0.0722));
        c = AgX(c);
        // look (display space): contrast and colour on top of plain AgX
        float luma = dot(c, float3(0.2126, 0.7152, 0.0722));
        c = pow(max(c, 0.0), _LookPower);
        c = luma + _Saturation * (c - luma);
        c = saturate(c);
        c = lerp(c, c * c * (3.0 - 2.0 * c), _Contrast);           // S-curve: deeper shade, brighter light, no clipping
        float l2 = dot(c, float3(0.2126, 0.7152, 0.0722));
        c *= lerp(_ShadowTint.rgb, _HighlightTint.rgb, smoothstep(0.0, 1.0, l2));   // split toning
        c = saturate(c * _Tint.rgb + _Lift.rgb);
        // scope vision modes (display space)
        if (_VisionK > 0.001)
        {
            float grain = Hash12(i.uv * _MainTex_TexelSize.zw + frac(_VisionTime * 7.13) * 917.0) - 0.5;
            float3 v;
            if (_Vision < 1.5)
            {
                // night vision: amplified light on a green phosphor screen, grainy
                float g = 1.0 - exp(-lumHDR * _NVGain);
                g = saturate(g * 1.08 + grain * 0.14);
                v = float3(0.30, 1.0, 0.38) * g + float3(0.008, 0.035, 0.012);
            }
            else
            {
                // thermal, white-hot: a cold grey world, bodies (heat pass) glow white
                float cold = 0.10 + 0.28 * (1.0 - exp(-lumHDR * 0.7));
                float hot = smoothstep(5.0, 20.0, lumHDR);
                float h = saturate(lerp(cold, 1.0, hot) + grain * 0.05);
                v = float3(h, h, h);
            }
            c = lerp(c, v, _VisionK);
        }
        // vignette
        float2 v = i.uv - 0.5;
        c *= 1.0 - pow(saturate(dot(v, v) * _Vignette), _VignettePower);
        // back to linear light (the sRGB target encodes it again)
        c = AgXOutset(c);
        c = pow(max(c, 0.0), 2.2);
        if (_OutputSRGB > 0.5)
            c = float3(LinearToGammaSpaceExact(c.r), LinearToGammaSpaceExact(c.g), LinearToGammaSpaceExact(c.b));
        #if defined(UNITY_COLORSPACE_GAMMA)
        else
            c = LinearToGammaSpace(c);
        #endif
        return half4(c, 1);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vertHaze
            #pragma fragment fragPrefilter
            #pragma target 3.0
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vertHaze
            #pragma fragment fragFinal
            #pragma target 3.0
            ENDCG }
    }
}

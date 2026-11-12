using UnityEngine;

namespace SilentContract
{
    /// <summary>Lightweight camera post processing (haze + bloom + AgX tonemap + look + vignette).</summary>
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        public float exposure = 1f, saturation = 1.08f, lookPower = 1.08f;
        public float bloomIntensity = 0.25f, threshold = 1.6f, knee = 0.6f;
        public float vignette = 1.05f, vignettePower = 1.8f;
        public Color tint = Color.white, lift = Color.black;
        /// <summary>S-curve contrast in display space (0 = plain AgX look).</summary>
        public float contrast;
        /// <summary>Split toning: multiplies the shade / the light (white = off).</summary>
        public Color shadowTint = Color.white, highlightTint = Color.white;
        public bool bloom = true;
        public float extraVignette;   // used by the scope / kill cam
        public float hurt;            // 0..1: wounded under fire (colour drains, edges darken and redden)
        public bool manualSRGB;       // editor preview capture: write display values into a linear target

        /// <summary>
        /// Aerial perspective from the depth buffer (Env sets it per preset): the camera renders a depth texture and the
        /// post adds the sky-coloured haze (golden towards a low sun). Unity's own fog is switched off for this camera
        /// while it renders (it stays on for the water mirror and the environment capture, which have no post).
        /// </summary>
        public static bool HazeActive;

        Material mat;
        Camera cam;
        bool fogWas, fogSwitched;
        bool depthAdded;
        static readonly int idThreshold = Shader.PropertyToID("_Threshold"), idKnee = Shader.PropertyToID("_Knee"),
            idBloomTex = Shader.PropertyToID("_BloomTex"), idBloomInt = Shader.PropertyToID("_BloomIntensity"),
            idExposure = Shader.PropertyToID("_Exposure"), idSat = Shader.PropertyToID("_Saturation"), idLook = Shader.PropertyToID("_LookPower"),
            idVig = Shader.PropertyToID("_Vignette"), idVigP = Shader.PropertyToID("_VignettePower"), idTint = Shader.PropertyToID("_Tint"),
            idLift = Shader.PropertyToID("_Lift"), idSRGB = Shader.PropertyToID("_OutputSRGB"),
            idVision = Shader.PropertyToID("_Vision"), idVisionK = Shader.PropertyToID("_VisionK"), idNVGain = Shader.PropertyToID("_NVGain"),
            idVisionTime = Shader.PropertyToID("_VisionTime"), idContrast = Shader.PropertyToID("_Contrast"),
            idShadowTint = Shader.PropertyToID("_ShadowTint"), idHighlightTint = Shader.PropertyToID("_HighlightTint"),
            idHazeOn = Shader.PropertyToID("_HazeOn"), idRays = Shader.PropertyToID("_FrustumRays");
        static readonly RenderTexture[] chain = new RenderTexture[6];
        static readonly Vector3[] corners = new Vector3[4];

        public static Material CreateMaterial()
        {
            var sh = Shader.Find("Hidden/SC/PostFX");
            if (sh == null || !sh.isSupported) return null;
            return new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        /// <summary>Takes over another camera's grade (the editor preview camera copies the game camera's).</summary>
        public void CopyFrom(PostFX o)
        {
            exposure = o.exposure; saturation = o.saturation; lookPower = o.lookPower;
            bloomIntensity = o.bloomIntensity; threshold = o.threshold; knee = o.knee; tint = o.tint; lift = o.lift;
            bloom = o.bloom; vignette = o.vignette; vignettePower = o.vignettePower;
            contrast = o.contrast; shadowTint = o.shadowTint; highlightTint = o.highlightTint;
        }

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            mat = CreateMaterial();
            if (mat == null) enabled = false;
        }

        void OnDisable()
        {
            if (mat != null) DestroyImmediate(mat);
            mat = null;
            if (depthAdded && cam != null) cam.depthTextureMode &= ~DepthTextureMode.Depth;
            depthAdded = false;
        }

        bool Hazed => HazeActive && mat != null && Save.Quality > 0;

        void OnPreCull()
        {
            if (cam == null) return;
            bool want = Hazed;
            if (want && (cam.depthTextureMode & DepthTextureMode.Depth) == 0) { cam.depthTextureMode |= DepthTextureMode.Depth; depthAdded = true; }
            else if (!want && depthAdded) { cam.depthTextureMode &= ~DepthTextureMode.Depth; depthAdded = false; }
        }

        void OnPreRender()
        {
            fogSwitched = false;
            if (!Hazed) return;
            fogWas = RenderSettings.fog;
            if (fogWas) { RenderSettings.fog = false; fogSwitched = true; }
        }

        void OnPostRender()
        {
            if (fogSwitched) RenderSettings.fog = fogWas;
            fogSwitched = false;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null) { Graphics.Blit(src, dst); return; }
            Process(this, mat, src, dst, manualSRGB);
        }

        /// <summary>The whole post chain; also used by the editor preview tool (outputSRGB: write display values to a linear target).</summary>
        public static void Process(PostFX s, Material mat, RenderTexture src, RenderTexture dst, bool outputSRGB)
        {
            mat.SetFloat(idExposure, s.exposure);
            float hurt = Mathf.Clamp01(s.hurt);
            mat.SetFloat(idSat, s.saturation * (1f - 0.65f * hurt));
            mat.SetFloat(idLook, s.lookPower);
            mat.SetFloat(idVig, s.vignette + s.extraVignette + hurt * 0.7f);
            mat.SetFloat(idVigP, s.vignettePower);
            mat.SetColor(idTint, hurt > 0.001f ? Color.Lerp(s.tint, new Color(s.tint.r * 1.18f, s.tint.g * 0.8f, s.tint.b * 0.78f, s.tint.a), hurt) : s.tint);
            mat.SetColor(idLift, s.lift);
            mat.SetFloat(idContrast, Mathf.Clamp01(s.contrast));
            mat.SetColor(idShadowTint, s.shadowTint);
            mat.SetColor(idHighlightTint, s.highlightTint);
            mat.SetFloat(idSRGB, outputSRGB ? 1f : 0f);
            // scope vision modes: night vision amplifies a dark scene a lot more than a lit one (auto gain)
            mat.SetFloat(idVision, (float)VisionFX.Mode);
            mat.SetFloat(idVisionK, VisionFX.Blend);
            mat.SetFloat(idNVGain, VisionFX.Night ? 4.5f : 1.1f);
            mat.SetFloat(idVisionTime, Time.unscaledTime);
            SetHaze(s, mat);

            RenderTexture bloomRt = null;
            if (s.bloom && s.bloomIntensity > 0.001f)
            {
                mat.SetFloat(idThreshold, s.threshold);
                mat.SetFloat(idKnee, Mathf.Max(0.01f, s.knee));
                int w = src.width / 2, h = src.height / 2;
                var fmt = src.format;
                int levels = 0;
                for (int i = 0; i < chain.Length; i++)
                {
                    if (w < 8 || h < 8) break;
                    chain[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                    chain[i].filterMode = FilterMode.Bilinear;
                    Graphics.Blit(i == 0 ? src : chain[i - 1], chain[i], mat, i == 0 ? 0 : 1);
                    levels++;
                    w /= 2; h /= 2;
                }
                for (int i = levels - 1; i > 0; i--)
                    Graphics.Blit(chain[i], chain[i - 1], mat, 2);
                bloomRt = chain[0];
                for (int i = 1; i < levels; i++) { RenderTexture.ReleaseTemporary(chain[i]); chain[i] = null; }
                mat.SetTexture(idBloomTex, bloomRt);
                mat.SetFloat(idBloomInt, s.bloomIntensity);
            }
            else
            {
                mat.SetTexture(idBloomTex, Texture2D.blackTexture);
                mat.SetFloat(idBloomInt, 0f);
            }
            Graphics.Blit(src, dst, mat, 3);
            if (bloomRt != null) { RenderTexture.ReleaseTemporary(bloomRt); chain[0] = null; }
        }

        /// <summary>View rays of the four screen corners (world offset per metre of view depth) for the haze passes.</summary>
        static void SetHaze(PostFX s, Material mat)
        {
            var c = s.cam;
            bool on = s.Hazed && c != null && (c.depthTextureMode & DepthTextureMode.Depth) != 0;
            mat.SetFloat(idHazeOn, on ? 1f : 0f);
            if (!on) return;
            c.CalculateFrustumCorners(new Rect(0, 0, 1, 1), 1f, Camera.MonoOrStereoscopicEye.Mono, corners);
            // corners: bottom-left, top-left, top-right, bottom-right (view space, z = 1)
            var t = c.transform;
            var m = new Matrix4x4();
            m.SetRow(0, (Vector4)t.TransformVector(corners[0]));
            m.SetRow(1, (Vector4)t.TransformVector(corners[3]));
            m.SetRow(2, (Vector4)t.TransformVector(corners[1]));
            m.SetRow(3, (Vector4)t.TransformVector(corners[2]));
            mat.SetMatrix(idRays, m);
        }
    }
}

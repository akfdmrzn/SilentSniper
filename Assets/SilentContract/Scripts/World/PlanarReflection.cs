using UnityEngine;
using UnityEngine.Rendering;

namespace SilentContract
{
    /// <summary>
    /// Mirror camera for the harbour water: renders the Reflect layer (quay edge, boats, cranes, breakwater, far
    /// shore) and the sky mirrored about the water plane into a reduced-resolution HDR texture (multisampled) that the
    /// water shader samples in screen space, plus a blurred, vertically streaked half-size copy for the parts of the
    /// water where small waves smear the reflection. Sits on each water renderer; renders once per camera per frame.
    /// Anti-flicker: the far-shore buildings are tiny in the mirror; with the rifle's constant breathing sway their
    /// windows used to land on different texels every frame and blink. The mirror is multisampled, uses coarser LODs,
    /// and is rendered from a "steady" view that only follows the camera when it turns by more than ~1.5 pixels.
    /// </summary>
    public class PlanarReflection : MonoBehaviour
    {
        public static float ResolutionScale = 0.4f;    // of the screen size; 0 = off (water falls back to the environment cubemap)
        public static int Msaa = 4;                    // multisampling of the mirror (clamped to what the device supports)
        public static int PixelLights = 0;
        public static Material WaterMaterial;
        public static float PlaneY = -2f;
        /// <summary>Vertical blur (sigma) of the soft copy as a fraction of the screen height.</summary>
        public static float BlurWidth = 0.02f;
        /// <summary>Follow the camera exactly every frame (the menu's slow cinematic pan). The "steady view" below is for
        /// the rifle's breathing sway: on a slow continuous pan it held the mirror still until the turn passed its
        /// threshold, then the reflection jumped — the water seemed to slide and snap behind the menu.</summary>
        public static bool FollowExactly;

        static Camera reflCam;
        static RenderTexture rt, softA, softB;
        static Material blurMat;
        static int lastFrame = -1;
        static Camera lastCam;
        static bool rendering;
        static int rtMsaaWanted;
        static bool hasSteady;
        static Quaternion steadyRot;
        static Vector3 steadyPos;
        static float steadyFov;
        static Camera steadyCam;
        static readonly int idTex = Shader.PropertyToID("_ReflectionTex");
        static readonly int idBlur = Shader.PropertyToID("_ReflectionBlur");
        static readonly int idBlurWidth = Shader.PropertyToID("_BlurWidth");
        static readonly int idDir = Shader.PropertyToID("_BlurDir");
        static readonly int idK = Shader.PropertyToID("_PlanarK");

        void OnWillRenderObject() => RenderFor(Camera.current);

        /// <summary>Renders the reflection for a camera (called automatically, or by the editor preview tool).</summary>
        public static void RenderFor(Camera cam, bool force = false)
        {
            if (rendering || cam == null || cam == reflCam || WaterMaterial == null) return;
            if (!cam.CompareTag("MainCamera")) return;                 // skip the cubemap capture camera, scene view, etc.
            if (ResolutionScale <= 0.01f) { WaterMaterial.SetFloat(idK, 0f); return; }
            if (!force && Application.isPlaying && lastFrame == Time.frameCount && lastCam == cam) return;
            rendering = true;
            try { Render(cam); }
            catch (System.Exception e) { Debug.LogException(e); WaterMaterial.SetFloat(idK, 0f); }
            finally { rendering = false; }
            lastFrame = Time.frameCount;
            lastCam = cam;
        }

        static void Render(Camera cam)
        {
            int w = Mathf.Max(64, Mathf.RoundToInt(cam.pixelWidth * ResolutionScale));
            int h = Mathf.Max(64, Mathf.RoundToInt(cam.pixelHeight * ResolutionScale));
            int msaa = Mathf.Max(1, Msaa);
            if (rt == null || rt.width != w || rt.height != h || rtMsaaWanted != msaa)
            {
                FreeTargets();
                rtMsaaWanted = msaa;
                var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.DefaultHDR;
                var desc = new RenderTextureDescriptor(w, h, fmt, 24) { useMipMap = false, autoGenerateMips = false, msaaSamples = msaa };
                desc.msaaSamples = Mathf.Clamp(SystemInfo.GetRenderTextureSupportedMSAASampleCount(desc), 1, msaa);
                // depth and the multisample surfaces never leave the GPU tile memory on iOS (resolved at the end of the pass)
                desc.memoryless = desc.msaaSamples > 1 ? RenderTextureMemoryless.Depth | RenderTextureMemoryless.MSAA : RenderTextureMemoryless.Depth;
                rt = new RenderTexture(desc) { name = "SC_WaterReflection", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                rt.Create();
                softA = NewSoft(Mathf.Max(32, w / 2), Mathf.Max(32, h / 2), fmt, "SC_WaterReflectionSoftA");
                softB = NewSoft(Mathf.Max(32, w / 2), Mathf.Max(32, h / 2), fmt, "SC_WaterReflectionSoftB");
            }
            if (reflCam == null)
            {
                var go = new GameObject("SC_WaterReflectionCam") { hideFlags = HideFlags.HideAndDontSave };
                reflCam = go.AddComponent<Camera>();
                reflCam.enabled = false;
            }
            reflCam.CopyFrom(cam);
            reflCam.cullingMask = 1 << Layers.Reflect;
            reflCam.targetTexture = rt;
            reflCam.clearFlags = CameraClearFlags.Skybox;
            reflCam.allowHDR = true;
            reflCam.allowMSAA = rt.antiAliasing > 1;
            reflCam.useOcclusionCulling = false;
            reflCam.depthTextureMode = DepthTextureMode.None;

            // steady view: ignore sub-pixel changes (breathing sway) so distant detail does not crawl between texels
            var camRot = cam.transform.rotation;
            var camPos = cam.transform.position;
            float pixelAngle = cam.fieldOfView / Mathf.Max(1f, cam.pixelHeight);          // degrees per screen pixel
            if (FollowExactly || !hasSteady || steadyCam != cam || Mathf.Abs(cam.fieldOfView - steadyFov) > 0.001f ||
                (camPos - steadyPos).sqrMagnitude > 1e-4f || Quaternion.Angle(steadyRot, camRot) > pixelAngle * 1.5f)
            {
                steadyRot = camRot;
                steadyPos = camPos;
                steadyFov = cam.fieldOfView;
                steadyCam = cam;
                hasSteady = true;
            }
            var view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(steadyPos, steadyRot, Vector3.one).inverse;

            // mirror the camera about the water plane, clip everything below the water with an oblique near plane
            var normal = Vector3.up;
            var pos = new Vector3(0, PlaneY, 0);
            float d = -Vector3.Dot(normal, pos) - 0.05f;
            var reflection = ReflectionMatrix(new Vector4(normal.x, normal.y, normal.z, d));
            reflCam.worldToCameraMatrix = view * reflection;
            var clipPlane = CameraSpacePlane(reflCam.worldToCameraMatrix, pos, normal, 1f);
            reflCam.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);
            // cull with the regular (non-oblique) mirrored frustum: the oblique matrix only exists to clip at the water
            reflCam.cullingMatrix = cam.projectionMatrix * reflCam.worldToCameraMatrix;
            reflCam.transform.position = new Vector3(steadyPos.x, 2f * PlaneY - steadyPos.y, steadyPos.z);
            var e = steadyRot.eulerAngles;
            reflCam.transform.eulerAngles = new Vector3(-e.x, e.y, e.z);

            int oldPixel = QualitySettings.pixelLightCount;
            float oldShadow = QualitySettings.shadowDistance;
            float oldLodBias = QualitySettings.lodBias;
            QualitySettings.pixelLightCount = PixelLights;
            QualitySettings.shadowDistance = 0f;
            QualitySettings.lodBias = oldLodBias * 0.6f;      // coarser LODs in the mirror: fewer sub-pixel details
            bool oldCull = GL.invertCulling;
            GL.invertCulling = !oldCull;
            try { reflCam.Render(); }
            finally
            {
                GL.invertCulling = oldCull;
                QualitySettings.pixelLightCount = oldPixel;
                QualitySettings.shadowDistance = oldShadow;
                QualitySettings.lodBias = oldLodBias;
            }
            WaterMaterial.SetTexture(idTex, rt);
            WaterMaterial.SetTexture(idBlur, Soften() ? softA : rt);
            WaterMaterial.SetFloat(idBlurWidth, BlurWidth);
            WaterMaterial.SetFloat(idK, 1f);
        }

        static RenderTexture NewSoft(int w, int h, RenderTextureFormat fmt, string name)
        {
            var t = new RenderTexture(w, h, 0, fmt) { name = name, hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.Create();
            return t;
        }

        /// <summary>Half-size copy blurred vertically (sigma = BlurWidth of the screen height; the 2x2 downsample
        /// gives the slight sideways softening): downsample + two vertical gaussian passes. Result in softA.</summary>
        static bool Soften()
        {
            if (blurMat == null)
            {
                var sh = Shader.Find("Hidden/SC/ReflBlur");
                if (sh == null || !sh.isSupported) return false;
                blurMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
            var prev = RenderTexture.active;
            try
            {
                // 9 taps at sigma = 2 steps (cut at 2 sigma -> ~0.93 sigma effective, hence 1.08)
                float s1 = BlurWidth * 0.6f * 1.08f, s2 = BlurWidth * 0.8f * 1.08f;     // combined sigma = BlurWidth
                Graphics.Blit(rt, softA, blurMat, 0);
                blurMat.SetVector(idDir, new Vector4(0f, s1 * 0.5f, 0f, 0f));
                Graphics.Blit(softA, softB, blurMat, 1);
                blurMat.SetVector(idDir, new Vector4(0f, s2 * 0.5f, 0f, 0f));
                Graphics.Blit(softB, softA, blurMat, 1);
            }
            finally { RenderTexture.active = prev; }
            return true;
        }

        static void FreeTargets()
        {
            foreach (var t in new[] { rt, softA, softB })
                if (t != null) { t.Release(); DestroyImmediate(t); }
            rt = softA = softB = null;
        }

        static Vector4 CameraSpacePlane(Matrix4x4 worldToCamera, Vector3 pos, Vector3 normal, float sideSign)
        {
            var offsetPos = pos + normal * 0.05f;
            var cpos = worldToCamera.MultiplyPoint(offsetPos);
            var cnormal = worldToCamera.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        static Matrix4x4 ReflectionMatrix(Vector4 p)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * p[0] * p[0]; m.m01 = -2f * p[0] * p[1]; m.m02 = -2f * p[0] * p[2]; m.m03 = -2f * p[3] * p[0];
            m.m10 = -2f * p[1] * p[0]; m.m11 = 1f - 2f * p[1] * p[1]; m.m12 = -2f * p[1] * p[2]; m.m13 = -2f * p[3] * p[1];
            m.m20 = -2f * p[2] * p[0]; m.m21 = -2f * p[2] * p[1]; m.m22 = 1f - 2f * p[2] * p[2]; m.m23 = -2f * p[3] * p[2];
            m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
            return m;
        }

        /// <summary>Frees the GPU memory (quality change / editor preview cleanup).</summary>
        public static void Release()
        {
            FreeTargets();
            if (reflCam != null) { DestroyImmediate(reflCam.gameObject); reflCam = null; }
            lastFrame = -1;
            lastCam = null;
            hasSteady = false;
        }
    }
}

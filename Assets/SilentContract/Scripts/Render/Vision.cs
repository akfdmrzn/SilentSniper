using UnityEngine;
using UnityEngine.Rendering;

namespace SilentContract
{
    /// <summary>
    /// Scope vision modes. Night vision is a post effect (amplified luminance on a green phosphor, grain). Thermal adds
    /// a body heat pass: every living character is drawn again, additively and very bright, after the scene, so the
    /// post pass maps people to white-hot over a cold grey world (bodies cool down after death). Sits on the main camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class VisionFX : MonoBehaviour
    {
        public static VisionMode Mode { get; private set; }
        public static float Blend { get; private set; }
        public static bool Night { get; private set; }

        public static void Set(VisionMode mode, float blend, bool night)
        {
            Mode = mode;
            Blend = mode == VisionMode.Normal ? 0f : Mathf.Clamp01(blend);
            Night = night;
        }

        Camera cam;
        CommandBuffer cb;
        Material heatMat;
        bool attached;
        static readonly int idHeat = Shader.PropertyToID("_SC_Heat");

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            var sh = Shader.Find("Hidden/SC/ThermalBody");
            if (sh != null && sh.isSupported) heatMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            cb = new CommandBuffer { name = "SC Thermal Bodies" };
        }

        void OnDisable()
        {
            if (cam != null && cb != null && attached) cam.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, cb);
            attached = false;
            cb?.Release();
            cb = null;
            if (heatMat != null) DestroyImmediate(heatMat);
            heatMat = null;
        }

        void OnPreRender()
        {
            if (cb == null || heatMat == null) return;
            bool on = Mode == VisionMode.Thermal && Blend > 0.01f;
            if (!on)
            {
                if (attached) { cam.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, cb); attached = false; }
                return;
            }
            cb.Clear();
            foreach (var n in NPC.All)
            {
                if (n == null || n.Escaped || n.IsHidden) continue;
                // freshly dead bodies are still warm; they cool down over a minute and a half
                float heat = n.Dead ? Mathf.Lerp(0.85f, 0.22f, Mathf.Clamp01((Time.time - n.DeathTime) / 90f)) : 1f;
                cb.SetGlobalFloat(idHeat, heat);
                // hair cards and lashes are left out: cut-out cards would show up as warm rectangles
                foreach (var part in n.HeatParts)
                {
                    var r = part.Key;
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    cb.DrawRenderer(r, heatMat, part.Value, 0);
                }
            }
            if (!attached) { cam.AddCommandBuffer(CameraEvent.AfterForwardAlpha, cb); attached = true; }
        }
    }
}

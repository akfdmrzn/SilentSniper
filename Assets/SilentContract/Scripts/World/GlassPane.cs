using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// A window pane a bullet can go through (office tower, penthouse): it bursts into shards with a crash everyone in
    /// the room hears; the bullet flies on with a little less speed and a slight deflection. Restored for every contract.
    /// </summary>
    public class GlassPane : MonoBehaviour
    {
        public static readonly List<GlassPane> All = new List<GlassPane>();
        Renderer[] rends;
        Collider[] cols;
        public bool Broken { get; private set; }

        void Awake()
        {
            rends = GetComponentsInChildren<Renderer>(true);
            cols = GetComponentsInChildren<Collider>(true);
            All.Add(this);
        }

        void OnDestroy() => All.Remove(this);

        public void Break(Vector3 point, Vector3 dir) => Break(point, dir, null);

        /// <summary><paramref name="masked"/>: the shot was fired under cover of a louder sound (null: judge it now).</summary>
        public void Break(Vector3 point, Vector3 dir, bool? masked)
        {
            if (Broken) return;
            Broken = true;
            foreach (var r in rends) if (r != null) r.enabled = false;
            foreach (var c in cols) if (c != null) c.enabled = false;
            FX.GlassShatter(point, dir, transform.lossyScale);
            Sfx.PlayAt(Sfx.Glass, point, 1f);
            var rt = MissionRuntime.I;
            if (rt != null && rt.IsPlaying)
            {
                if (masked.HasValue) rt.Noise(point, 16f, NoiseKind.Impact, masked.Value);
                else rt.Noise(point, 16f, NoiseKind.Impact);
            }
        }

        public void ResetState()
        {
            Broken = false;
            if (rends != null) foreach (var r in rends) if (r != null) r.enabled = true;
            if (cols != null) foreach (var c in cols) if (c != null) c.enabled = true;
        }

        public static void ResetAll()
        {
            foreach (var g in All.ToArray()) if (g != null) g.ResetState();
        }

        public static GlassPane Of(Collider c) => c != null ? c.GetComponentInParent<GlassPane>() : null;
    }
}

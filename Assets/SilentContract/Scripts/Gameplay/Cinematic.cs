using System;
using System.Collections;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// Contract intro: the world freezes like a surveillance still, the camera glides over the contract area while
    /// the HUD pins intel tags on the target and his people, the handler speaks, then the camera flies back into the
    /// sniper nest and time starts. Tap to skip. The mission clock does not run meanwhile (the world is frozen).
    /// </summary>
    public class Cinematic : MonoBehaviour
    {
        public static Cinematic I;
        public static bool Active { get; private set; }
        public static bool SkipRequested;
        /// <summary>0..1 over the establishing shot (HUD times the tags with it).</summary>
        public static float Progress { get; private set; }
        /// <summary>True while flying back to the nest (HUD fades the intro out).</summary>
        public static bool Leaving { get; private set; }
        public static event Action<bool> StateChanged;

        Camera cam;
        Sniper sn;
        Transform camOldParent;
        Vector3 camOldLocalPos;
        Quaternion camOldLocalRot;
        float oldFov;
        Coroutine running;
        Action onDone;
        bool detached;              // the camera has been taken off the rig (restore only then)

        const float ShotSeconds = 4.4f, BackSeconds = 1.0f;     // V3: a second shorter (players wanted to start sooner)

        public void Init(Camera c, Sniper s)
        {
            I = this;
            cam = c;
            sn = s;
        }

        public void Play(MissionDef def, MissionRuntime rt, Action done)
        {
            if (Active) return;                               // already playing: the first call's callback will start the contract
            if (cam == null || def == null) { done?.Invoke(); return; }
            onDone = done;
            running = StartCoroutine(Routine(def, rt));
        }

        /// <summary>Contract area to frame: the dossier focus marker, else the first target.</summary>
        Vector3 FocusPoint(MissionDef def, MissionRuntime rt)
        {
            if (!string.IsNullOrEmpty(def.focus) && Level.I != null && Level.I.HasMarker(def.focus))
                return Level.I.Marker(def.focus) + Vector3.up * 1.4f;
            foreach (var t in rt.Targets) if (t != null) return t.ChestPoint;
            return sn.transform.position + sn.transform.forward * 80f;
        }

        /// <summary>Lifts a camera spot until it is out of geometry and sees the focus.</summary>
        static Vector3 Clear(Vector3 p, Vector3 focus)
        {
            for (int i = 0; i < 12; i++)
            {
                bool inside = Physics.CheckSphere(p, 0.9f, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                bool blocked = Physics.Linecast(p, focus, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                if (!inside && !blocked) break;
                p += Vector3.up * 3f;
            }
            p.y = Mathf.Max(p.y, Level.WaterY + 3f);
            return p;
        }

        static Vector3 Orbit(Vector3 focus, Vector3 dirToCam, float yawDeg, float radius, float height)
        {
            var d = Quaternion.Euler(0, yawDeg, 0) * dirToCam;
            return focus + d * radius + Vector3.up * height;
        }

        IEnumerator Routine(MissionDef def, MissionRuntime rt)
        {
            Active = true;
            Leaving = false;
            SkipRequested = false;
            Progress = 0f;
            // a few live frames first so every character has its pose, then the world stops
            yield return null;
            yield return null;
            yield return null;
            var F = FocusPoint(def, rt);
            camOldParent = cam.transform.parent;
            camOldLocalPos = cam.transform.localPosition;
            camOldLocalRot = cam.transform.localRotation;
            oldFov = cam.fieldOfView;
            cam.transform.SetParent(null, true);
            detached = true;
            Time.timeScale = 0f;
            StateChanged?.Invoke(true);
            Sfx.Play(Sfx.Briefing, 0.55f);

            var S = sn.transform.position;
            var toNest = S - F; toNest.y = 0f;
            float dist = toNest.magnitude;
            var dir = dist > 1f ? toNest / dist : Vector3.back;
            float r0 = Mathf.Clamp(dist * 0.22f, 24f, 46f);
            var a0 = Clear(Orbit(F, dir, 24f, r0, 15f), F);
            var a1 = Clear(Orbit(F, dir, -14f, r0 * 0.5f, 5.5f), F);

            float t = 0f;
            while (t < ShotSeconds && !SkipRequested)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float k = Util.EaseInOut(t / ShotSeconds);
                Progress = Mathf.Clamp01(t / ShotSeconds);
                var p = Vector3.Lerp(a0, a1, k);
                cam.transform.position = p;
                cam.transform.rotation = Quaternion.LookRotation(F - p, Vector3.up);
                cam.fieldOfView = Mathf.Lerp(42f, 34f, k);
                yield return null;
            }
            Progress = 1f;

            // fly back into the nest, looking where the scope will be
            Leaving = true;
            Sfx.Play(Sfx.Whoosh, 0.5f, 1.25f);
            var fromPos = cam.transform.position;
            var fromRot = cam.transform.rotation;
            float fromFov = cam.fieldOfView;
            var toRot = sn.RotationTowards(F);
            float back = SkipRequested ? 0.55f : BackSeconds;
            t = 0f;
            while (t < back)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float k = Util.EaseInOut(t / back);
                // arc slightly upwards on the way back
                var p = Vector3.Lerp(fromPos, S, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 6f;
                cam.transform.position = p;
                cam.transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
                cam.fieldOfView = Mathf.Lerp(fromFov, 55f, k);
                yield return null;
            }
            Finish();
        }

        void Finish()
        {
            var g = Game.I;
            Time.timeScale = g != null && g.Paused ? 0f : 1f;
            Time.fixedDeltaTime = 0.02f;
            if (cam != null && detached)
            {
                cam.transform.SetParent(camOldParent, false);
                cam.transform.localPosition = camOldLocalPos;
                cam.transform.localRotation = camOldLocalRot;
                cam.fieldOfView = oldFov;
            }
            detached = false;
            running = null;
            Active = false;
            Leaving = false;
            StateChanged?.Invoke(false);
            var d = onDone;
            onDone = null;
            d?.Invoke();
        }

        /// <summary>Leaving the contract mid-intro (menu, restart): restore time and camera without the callback.</summary>
        public static void ForceStop()
        {
            if (I == null || !Active) return;
            if (I.running != null) I.StopCoroutine(I.running);
            I.onDone = null;
            I.Finish();
        }
    }
}

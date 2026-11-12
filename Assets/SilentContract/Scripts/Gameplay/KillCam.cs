using System;
using System.Collections;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// Slow-motion bullet camera, all the way into the target: it rides a hand's breadth behind the spinning bullet
    /// (time slowed so that any distance takes a few seconds), level with it, drifting out beside it for a moment on a
    /// long shot, holds almost still on the impact, then backs off along the line of the shot and watches him fall.
    /// </summary>
    public class KillCam : MonoBehaviour
    {
        public static bool Active { get; private set; }
        /// <summary>The camera is away for a story cutaway, not following a bullet.</summary>
        public static bool IsCutaway { get; private set; }
        public static event Action<bool, string> StateChanged;
        public static bool SkipRequested;

        Camera cam;
        Transform camOldParent;
        Vector3 camOldLocalPos;
        Quaternion camOldLocalRot;
        float oldFov;

        public void Init(Camera c) { cam = c; }

        /// <param name="flightTime">predicted seconds of flight (game time) to the impact</param>
        /// <param name="impact">where the bullet will meet him (predicted, he may be walking)</param>
        public void Run(Projectile p, NPC victim, string label, Action done, float flightTime = -1f, Vector3? impact = null)
        {
            if (Active || p == null || cam == null) { done?.Invoke(); return; }
            StartCoroutine(Routine(p, victim, label, done, flightTime, impact));
        }

        static void SetScale(float s)
        {
            Time.timeScale = s;
            Time.fixedDeltaTime = 0.02f * s;
        }

        /// <summary>A camera position near <paramref name="center"/>, pulled in front of any wall between them.</summary>
        static Vector3 Clear(Vector3 center, Vector3 want)
        {
            if (Physics.Linecast(center, want, out var hit, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                want = hit.point + (center - want).normalized * 0.3f;
            want.y = Mathf.Max(want.y, Level.WaterY + 0.4f);
            return want;
        }

        IEnumerator Routine(Projectile p, NPC victim, string label, Action done, float flightTime, Vector3? impact)
        {
            Active = true;
            SkipRequested = false;
            StateChanged?.Invoke(true, label);
            camOldParent = cam.transform.parent;
            camOldLocalPos = cam.transform.localPosition;
            camOldLocalRot = cam.transform.localRotation;
            oldFov = cam.fieldOfView;
            float oldNear = cam.nearClipPlane;
            cam.transform.SetParent(null, true);
            cam.fieldOfView = 46f;
            cam.nearClipPlane = 0.015f;               // the camera rides a hand's breadth behind the bullet
            Sfx.Play(Sfx.Whoosh, 0.6f);

            // the bullet is the star: drawn a little larger, spinning, no streak (the trail would run through the lens)
            var trail = p.GetComponent<TrailRenderer>();
            if (trail != null) { trail.emitting = false; trail.Clear(); }
            Transform body = p.transform.childCount > 0 ? p.transform.GetChild(0) : null;
            Vector3 bodyScale = body != null ? body.localScale : Vector3.one;
            if (body != null) body.localScale = bodyScale * 2.8f;
            float spin = 0f;

            // 1) the flight, all the way in: the camera stays right behind the bullet, level with it (it never tips up
            //    or down on its own), any distance taking a few real seconds. On a long shot it drifts out beside the
            //    bullet for a moment (a side view of it cutting through the air) and back behind it; over the last metres
            //    it eases back a little so the man is in the frame when the bullet reaches him.
            float T = flightTime > 0.02f ? flightTime : 0.45f;
            float cruiseReal = Mathf.Clamp(1.6f + T * 3.2f, 2.0f, 3.8f);
            const float ApproachGame = 0.042f;      // game seconds of the final approach (~35 m at 830 m/s)
            const float ApproachReal = 1.25f;
            bool swing = T > 0.11f && victim != null;
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float real = 0f;
            bool first = true;
            var target = impact ?? (victim != null ? victim.ChestPoint : p.transform.position + p.velocity.normalized * 50f);
            Vector3 fwdSm = p.velocity.sqrMagnitude > 1e-4f ? p.velocity.normalized : cam.transform.forward;
            while (p != null && !p.done && !SkipRequested)
            {
                // past the meeting point and still flying: it missed (into the open) — no endless slow motion
                if (p.flightTime > T + 0.012f) break;
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                real += dt;
                float left = Mathf.Max(0f, T - p.flightTime);
                float scale;
                if (left > ApproachGame)
                {
                    float realLeft = Mathf.Max(0.3f, cruiseReal - real);
                    scale = Mathf.Clamp((left - ApproachGame) / realLeft, 0.02f, 0.6f);
                }
                else scale = ApproachGame / ApproachReal;
                SetScale(scale);
                var bp = p.transform.position;
                var fwd = p.velocity.sqrMagnitude > 1e-4f ? p.velocity.normalized : fwdSm;
                fwdSm = Vector3.Slerp(fwdSm, fwd, 1f - Mathf.Exp(-dt * 12f));
                var flat = new Vector3(fwdSm.x, 0f, fwdSm.z);
                flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
                var right = Vector3.Cross(Vector3.up, flat).normalized;
                // 0 = behind, 1 = beside: a slow swell in the middle of a long flight
                float prog = Mathf.Clamp01(p.flightTime / Mathf.Max(0.01f, T - ApproachGame));
                float sw = swing ? Mathf.Sin(Mathf.Clamp01((prog - 0.28f) / 0.42f) * Mathf.PI) : 0f;
                sw = sw * sw * (3f - 2f * sw);
                float ang = sw * 62f * side;
                // the approach: ease back a little (and a touch higher) so he is in frame as it arrives
                float k = victim != null ? Util.EaseInOut(Mathf.Clamp01(1f - left / ApproachGame)) : 0f;
                float dist = Mathf.Lerp(0.16f, 0.36f, sw) + k * 0.5f;
                var back = Quaternion.AngleAxis(ang, Vector3.up) * -flat;
                var want = bp + back * dist + Vector3.up * (0.035f + k * 0.08f) + right * side * 0.045f * (1f - sw);
                var look = bp + fwdSm * Mathf.Lerp(1.6f, 0.06f, sw);
                cam.transform.position = want;
                var rot = Quaternion.LookRotation((look - want).sqrMagnitude > 1e-6f ? look - want : fwdSm, Vector3.up);
                cam.transform.rotation = first ? rot : Quaternion.Slerp(cam.transform.rotation, rot, 1f - Mathf.Exp(-dt * 16f));
                first = false;
                if (body != null) { spin += dt * 540f; body.localRotation = Quaternion.AngleAxis(spin, Vector3.forward); }
                if (real > 12f) break;                 // safety only: it always arrives long before
                yield return null;
            }
            // 2) the impact (only when this bullet really found him: a man already hurt by an earlier shot does not count)
            bool landed = victim != null && p != null && p.done && p.HitNpc == victim;
            if (landed && !SkipRequested)
            {
                var hitAt = p != null ? p.transform.position : target;
                if (victim.Sedated && !victim.Dead && !victim.Wounded) Sfx.Play(Sfx.Dart, 0.9f, 0.6f);
                else
                {
                    Sfx.Play(Sfx.Flesh, 0.9f, 0.55f);
                    Sfx.Play(Sfx.Impact, 0.5f, 0.4f);
                }
                SetScale(0.03f);
                // a breath of near stillness on the burst, from where the camera was
                float hold = 0f;
                var p0 = cam.transform.position;
                var r0 = cam.transform.rotation;
                while (hold < 0.45f && !SkipRequested)
                {
                    float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    hold += dt;
                    cam.transform.position = Vector3.Lerp(p0, Vector3.Lerp(p0, hitAt, 0.15f), Util.EaseOutCubic(hold / 0.45f));
                    cam.transform.rotation = r0;
                    yield return null;
                }
                // 3) the fall, seen from the bullet's side: the camera backs off along the line of the shot to a few
                //    metres and stays at that height (no circling, no tipping), watching him go down in slow motion that
                //    ramps up to nearly normal speed — until he is on the ground
                if (SkipRequested || victim == null) goto restore;
                var c0 = victim.Pelvis != null ? victim.Pelvis.position : victim.transform.position + Vector3.up;
                var away = -new Vector3(fwdSm.x, 0f, fwdSm.z);
                away = away.sqrMagnitude > 1e-4f ? away.normalized : -victim.transform.forward;
                var rightF = Vector3.Cross(Vector3.up, away).normalized;
                var restPos = Clear(c0, c0 + away * 3.3f + rightF * side * 0.9f + Vector3.up * 0.45f);
                var startPos = cam.transform.position;
                var lookSm = Vector3.Lerp(hitAt, c0, 0.3f);
                const float Dur = 2.6f;
                float e = 0, settled = victim.DyingOnClip ? -1f : 0f;
                while (victim != null && !SkipRequested && e < 5.2f && (e < Dur || settled < 0f || e < settled + 0.6f))
                {
                    if (settled < 0f && !victim.DyingOnClip) settled = e;
                    float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    e += dt;
                    SetScale(e < 0.8f ? 0.2f : Mathf.Lerp(0.2f, 0.9f, Util.EaseInOut((e - 0.8f) / 1.4f)));
                    var center = victim.Pelvis != null ? victim.Pelvis.position : victim.transform.position + Vector3.up;
                    // a slow sideways drift once there, so the shot is alive but never swings
                    var pos = Vector3.Lerp(startPos, restPos, Util.EaseInOut(e / 0.9f)) + rightF * side * Mathf.Max(0f, e - 0.9f) * 0.12f;
                    cam.transform.position = pos;
                    var lookWant = new Vector3(center.x, Mathf.Max(center.y, c0.y - 0.7f), center.z);
                    lookSm = Vector3.Lerp(lookSm, lookWant, 1f - Mathf.Exp(-dt * 4f));
                    var dir = lookSm - pos;
                    // keep the tilt gentle: never steeper than ~22 degrees down
                    float flatLen = new Vector2(dir.x, dir.z).magnitude;
                    dir.y = Mathf.Clamp(dir.y, -flatLen * 0.4f, flatLen * 0.2f);
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 1 - Mathf.Exp(-dt * 6f));
                    yield return null;
                }
            }
            else if (!SkipRequested && p != null)
            {
                // it missed after all (he turned, stepped behind something): a moment on where it struck, then back
                SetScale(0.3f);
                float e = 0f;
                var at = p.transform.position;
                while (e < 0.6f && !SkipRequested)
                {
                    float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    e += dt;
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(at - cam.transform.position), 1 - Mathf.Exp(-dt * 8f));
                    yield return null;
                }
            }
        restore:
            if (body != null) { body.localScale = bodyScale; body.localRotation = Quaternion.identity; }
            SetScale(1f);
            cam.transform.SetParent(camOldParent, false);
            cam.transform.localPosition = camOldLocalPos;
            cam.transform.localRotation = camOldLocalRot;
            cam.fieldOfView = oldFov;
            cam.nearClipPlane = oldNear;
            Active = false;
            StateChanged?.Invoke(false, null);
            done?.Invoke();
        }

        /// <summary>
        /// A short live shot of something the nest cannot see well (a limousine at the opera steps, a helicopter
        /// setting down): letterboxed, in real time, tap to skip, then back to the scope. The camera glides from
        /// <paramref name="from"/> to <paramref name="to"/> looking at <paramref name="focus"/>. Ignored while the kill
        /// cam or the intro runs.
        /// </summary>
        public void Cutaway(Func<Vector3> focus, Vector3 from, Vector3 to, float seconds, string label, Action done = null)
        {
            if (Active || cam == null || Cinematic.Active || focus == null) { done?.Invoke(); return; }
            StartCoroutine(CutawayRoutine(focus, from, to, Mathf.Max(0.5f, seconds), label, done));
        }

        IEnumerator CutawayRoutine(Func<Vector3> focus, Vector3 from, Vector3 to, float seconds, string label, Action done)
        {
            Active = true;
            IsCutaway = true;
            SkipRequested = false;
            StateChanged?.Invoke(true, label);
            camOldParent = cam.transform.parent;
            camOldLocalPos = cam.transform.localPosition;
            camOldLocalRot = cam.transform.localRotation;
            oldFov = cam.fieldOfView;
            cam.transform.SetParent(null, true);
            Sfx.Play(Sfx.Whoosh, 0.45f, 1.3f);
            float t = 0f;
            bool first = true;
            while (t < seconds && !SkipRequested)
            {
                var rt = MissionRuntime.I;
                if (rt == null || !rt.IsPlaying) break;                    // the contract ended meanwhile
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                t += dt;
                float k = Util.EaseInOut(t / seconds);
                var p = Vector3.Lerp(from, to, k);
                Vector3 f;
                try { f = focus(); } catch (Exception e) { Debug.LogException(e); break; }
                cam.transform.position = p;
                var want = Quaternion.LookRotation((f - p).sqrMagnitude > 1e-4f ? f - p : Vector3.forward, Vector3.up);
                cam.transform.rotation = first ? want : Quaternion.Slerp(cam.transform.rotation, want, 1f - Mathf.Exp(-dt * 7f));
                cam.fieldOfView = Mathf.Lerp(40f, 32f, k);
                first = false;
                yield return null;
            }
            cam.transform.SetParent(camOldParent, false);
            cam.transform.localPosition = camOldLocalPos;
            cam.transform.localRotation = camOldLocalRot;
            cam.fieldOfView = oldFov;
            Active = false;
            IsCutaway = false;
            StateChanged?.Invoke(false, null);
            done?.Invoke();
        }

        public static void ForceStop()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            SkipRequested = true;
        }
    }
}

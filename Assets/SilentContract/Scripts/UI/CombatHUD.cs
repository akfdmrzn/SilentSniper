using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>
    /// The firefight part of the mission interface: the cover button (left thumb, next to the breath button), the
    /// player's health and how close the enemy is to finding the nest (top centre, shown only when it matters), red
    /// markers at the edge of the view pointing at whoever is firing, blood creeping in from the edges while hurt, and
    /// the first-time hints.
    /// </summary>
    public class CombatHUD : MonoBehaviour
    {
        Sniper sn;
        MissionRuntime rt;
        Firefight ff;
        RectTransform canvasRt;
        // cover button
        RectTransform coverRt;
        CanvasGroup coverGroup;
        Image coverBg, coverIcon, coverRing;
        Text coverLabel;
        bool coverShown;
        // vitals
        RectTransform vitals;
        CanvasGroup vitalsGroup, healthGroup, eyeGroup;
        Image healthFill, healthBack, eyeRing, eyeIcon;
        Text eyeText;
        int eyeKey = -1;
        // blood
        Image blood;
        float flash;
        // where the rounds come from
        class Arrow { public RectTransform rt; public Image img; public float t, life; public Vector3 from; public bool hit; }
        readonly List<Arrow> arrows = new List<Arrow>();
        float nextBeat, shownHealth = 1f;
        static bool tipHeat, tipSpotter;

        public static CombatHUD Create(RectTransform canvas, RectTransform safe)
        {
            var go = new GameObject("CombatHUD", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var c = go.AddComponent<CombatHUD>();
            c.canvasRt = canvas;
            c.Build(canvas, safe);
            return c;
        }

        void Build(RectTransform canvas, RectTransform safe)
        {
            var self = (RectTransform)transform;
            UIK.Stretch(self);
            // blood over the whole screen, under everything else of the HUD's own layers
            blood = UIK.Img(self, "Blood", "vignette", new Color(0.45f, 0.0f, 0.0f, 0f));
            UIK.Stretch(blood.rectTransform);
            blood.raycastTarget = false;

            // arrows round the centre of the view
            for (int i = 0; i < 8; i++)
            {
                var img = UIK.Img(self, "Hit" + i, "tri_right", Theme.Danger);
                img.raycastTarget = false;
                img.rectTransform.sizeDelta = new Vector2(56, 56);
                img.gameObject.SetActive(false);
                arrows.Add(new Arrow { rt = img.rectTransform, img = img });
            }

            // top centre: being found (eye) and health
            vitals = UIK.Rect(safe, "Vitals");
            UIK.Place(vitals, UIK.TC, new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(360, 150));
            vitalsGroup = UIK.Group(vitals.gameObject);
            vitalsGroup.blocksRaycasts = false;
            var eye = UIK.Rect(vitals, "Eye");
            UIK.Place(eye, UIK.TC, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(360, 96));
            eyeGroup = UIK.Group(eye.gameObject);
            var eb = UIK.Img(eye, "Bg", "circle", new Color(0.04f, 0.05f, 0.07f, 0.85f));
            UIK.Place(eb.rectTransform, UIK.TC, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(64, 64));
            eyeRing = UIK.Img(eb.transform, "Fill", "ring_thick", Theme.Accent);
            eyeRing.type = Image.Type.Filled;
            eyeRing.fillMethod = Image.FillMethod.Radial360;
            eyeRing.fillOrigin = (int)Image.Origin360.Top;
            UIK.Stretch(eyeRing.rectTransform, -5, -5, -5, -5);
            eyeIcon = UIK.Icon(eb.transform, "eye", Theme.Accent, 34);
            UIK.Place(eyeIcon.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
            eyeText = UIK.Txt(eye, "Text", "", Res.FontBold, 22, Theme.Accent, TextAnchor.UpperCenter, false);
            UIK.Place(eyeText.rectTransform, UIK.TC, new Vector2(0.5f, 1f), new Vector2(0, -68), new Vector2(360, 28));
            var eo = eyeText.gameObject.AddComponent<Outline>();
            eo.effectColor = new Color(0, 0, 0, 0.85f);
            eo.effectDistance = new Vector2(1.5f, -1.5f);

            var health = UIK.Rect(vitals, "Health");
            UIK.Place(health, UIK.TC, new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(300, 34));
            healthGroup = UIK.Group(health.gameObject);
            var hi = UIK.Icon(health, "heart", Theme.Danger, 30);
            UIK.Place(hi.rectTransform, UIK.ML, new Vector2(0, 0.5f), Vector2.zero, new Vector2(30, 30));
            UIK.AddShadow(hi, 1.5f, 0.6f);
            healthFill = UIK.Bar(health, "Bar", new Color(0.04f, 0.05f, 0.07f, 0.85f), Theme.Success, out var barRt);
            UIK.Place(barRt, UIK.ML, new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(258, 18));
            healthBack = barRt.GetComponent<Image>();
            healthFill.fillAmount = 1f;

            // the cover button: left thumb, beside the breath button
            var cb = UIK.Img(safe, "Cover", "circle", new Color(0.05f, 0.07f, 0.09f, 0.78f), false, true);
            coverBg = cb;
            coverRt = cb.rectTransform;
            UIK.Place(coverRt, UIK.BL, new Vector2(0.5f, 0.5f), new Vector2(320, 110), new Vector2(132, 132));
            var edge = UIK.Img(cb.transform, "Edge", "ring", new Color(1, 1, 1, 0.35f));
            UIK.Stretch(edge.rectTransform);
            coverRing = UIK.Img(cb.transform, "Pulse", "glow_ring", Theme.Danger);
            UIK.Stretch(coverRing.rectTransform, -26, -26, -26, -26);
            coverRing.raycastTarget = false;
            coverIcon = UIK.Icon(cb.transform, "shield", Color.white, 48);
            UIK.Place(coverIcon.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(48, 48));
            coverLabel = UIK.Txt(cb.transform, "Label", "SİPER", Res.FontBold, 22, Color.white, TextAnchor.MiddleCenter, false);
            UIK.Place(coverLabel.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), new Vector2(0, -32), new Vector2(130, 26));
            var hb = cb.gameObject.AddComponent<HoldButton>();
            hb.onDown = () => { if (sn != null) sn.ToggleCover(); };
            coverGroup = UIK.Group(cb.gameObject);
            coverGroup.alpha = 0f;
            coverRt.gameObject.SetActive(false);
            vitalsGroup.alpha = 0f;
        }

        /// <summary>The cover button (the lessons point at it) and whether it is on screen.</summary>
        public RectTransform CoverButton => coverRt;
        public bool CoverVisible => coverShown && coverGroup != null && coverGroup.alpha > 0.6f;

        public void Begin(MissionRuntime runtime, Sniper sniper)
        {
            rt = runtime;
            sn = sniper;
            ff = Firefight.I;
            if (ff != null)
            {
                ff.Round -= OnRound; ff.Round += OnRound;
                ff.LocatedChanged -= OnLocated; ff.LocatedChanged += OnLocated;
            }
            coverShown = false;
            coverGroup.alpha = 0f;
            coverRt.gameObject.SetActive(false);
            vitalsGroup.alpha = 0f;
            blood.color = new Color(0.45f, 0f, 0f, 0f);
            flash = 0f;
            shownHealth = 1f;
            eyeKey = -1;
            foreach (var a in arrows) { a.t = a.life = 0f; a.img.gameObject.SetActive(false); }
        }

        float placedFor = -1f;

        /// <summary>Top centre, unless the screen is too narrow for that (iPad): then just right of the objective
        /// panel (which ends 684 units from the left of the safe area), clear of the wind panel on the right.</summary>
        void PlaceVitals()
        {
            var parent = vitals.parent as RectTransform;
            if (parent == null) return;
            float w = parent.rect.width;
            if (Mathf.Abs(w - placedFor) < 0.5f) return;
            placedFor = w;
            float left = w * 0.5f - 180f;
            float x = left < 704f ? 704f - left : 0f;
            // squeezed between the objective panel and the wind panel (290 units from the right) on a very narrow screen
            float room = w - 290f - 704f;
            float scale = x > 0f && room < 360f ? Mathf.Max(0.7f, room / 360f) : 1f;
            if (scale < 1f) x = 704f + 180f * scale - w * 0.5f;
            vitals.anchoredPosition = new Vector2(x, -18f);
            vitals.localScale = new Vector3(scale, scale, 1f);
        }

        void OnRound(Vector3 from, bool hit, float damage)
        {
            Arrow a = null;
            foreach (var x in arrows) if (x.t >= x.life) { a = x; break; }
            if (a == null) a = arrows[0];
            a.from = from;
            a.hit = hit;
            a.t = 0f;
            a.life = hit ? 1.8f : 1.1f;
            a.img.gameObject.SetActive(true);
            if (hit) flash = Mathf.Min(1f, flash + 0.55f + damage * 0.01f);
            // (the first-time "cover" lesson teaches the cover button; a second, nine-second hint box on top of it only
            // covered the screen in the middle of the fight)
        }

        void OnLocated(bool on)
        {
            if (rt == null || !rt.IsPlaying) return;
            // the panel at the top says it ("YERİN TESPİT EDİLDİ", flashing): the alarm sound, no extra line on screen
            if (on) Sfx.Play(Sfx.Alert, 0.75f, 0.9f);
        }

        void Update()
        {
            if (rt == null || sn == null || ff == null) return;
            float dt = Time.unscaledDeltaTime;
            bool playing = rt.IsPlaying;
            bool kill = KillCam.Active || Cinematic.Active;
            PlaceVitals();
            // ---- cover button: there once the danger is real
            bool want = playing && ff.Threat > 0f && !ff.Dead && (ff.Heat > 0.12f || ff.EverShotAt || sn.InCover || ff.Watched > 0.05f || ff.AnyAiming);
            if (want && !coverShown)
            {
                coverShown = true;
                coverRt.gameObject.SetActive(true);
                Tween.Pop(coverRt, 0.35f);
            }
            if (!playing && coverShown && coverGroup.alpha <= 0.01f) { coverShown = false; coverRt.gameObject.SetActive(false); }
            coverGroup.alpha = Mathf.MoveTowards(coverGroup.alpha, coverShown && playing && !kill ? 1f : 0f, dt * 5f);
            coverGroup.blocksRaycasts = coverShown && playing && !kill;
            if (coverShown)
            {
                bool inCover = sn.InCover;
                coverBg.color = inCover ? new Color(0.1f, 0.42f, 0.22f, 0.9f) : new Color(0.05f, 0.07f, 0.09f, 0.78f);
                coverLabel.text = inCover ? "DOĞRUL" : "SİPER";
                // pulsing when someone is aiming at the nest and the player is still up
                bool danger = !inCover && (ff.AnyAiming || ff.UnderFire);
                coverRing.enabled = danger;
                if (danger)
                {
                    float p = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
                    coverRing.color = new Color(Theme.Danger.r, Theme.Danger.g, Theme.Danger.b, 0.35f + 0.5f * p);
                }
            }
            // ---- being found / health (top centre)
            float heat = Mathf.Clamp01(Mathf.Max(ff.Heat, ff.Watched));
            bool showEye = playing && ff.Threat > 0f && (heat > 0.02f || ff.Located);
            bool showHealth = playing && (ff.Health < Firefight.MaxHealth - 0.5f || ff.UnderFire || ff.Located);
            eyeGroup.alpha = Mathf.MoveTowards(eyeGroup.alpha, showEye ? 1f : 0f, dt * 4f);
            healthGroup.alpha = Mathf.MoveTowards(healthGroup.alpha, showHealth ? 1f : 0f, dt * 4f);
            vitalsGroup.alpha = Mathf.MoveTowards(vitalsGroup.alpha, (showEye || showHealth) && !kill ? 1f : 0f, dt * 5f);
            if (showEye)
            {
                eyeRing.fillAmount = ff.Located ? 1f : heat;
                Color c = ff.Located ? Color.Lerp(Theme.Danger, Color.white, Mathf.PingPong(Time.unscaledTime * 2.5f, 1f) * 0.4f)
                                     : Color.Lerp(Theme.Accent, Theme.Danger, heat);
                eyeRing.color = c;
                eyeIcon.color = c;
                eyeText.color = c;
                int key = ff.Located ? 3 : (ff.Watched > 0.05f ? 2 : (heat > 0.5f ? 1 : 0));
                if (key != eyeKey)
                {
                    eyeKey = key;
                    eyeText.text = key == 3 ? "YERİN TESPİT EDİLDİ" : (key == 2 ? "GÖZCÜ SENİ ARIYOR" : (key == 1 ? "YERİNİ ARIYORLAR" : "DİKKAT ÇEKİYORSUN"));
                }
                if (!tipHeat && heat > 0.5f && !ff.Located)
                {
                    tipHeat = true;
                    rt.Hint("Her atış yerini biraz daha belli eder (susturucu daha az). Göz dolarsa yerin tespit edilir ve silahlı adamlar sana ateş eder.", 8f);
                }
                if (!tipSpotter && ff.Watched > 0.3f && !ff.Located)
                {
                    tipSpotter = true;
                    rt.Hint("Bir gözcü dürbünle damları tarıyor: dürbün parlaması sana dönerken eğil ya da önce onu indir.", 8f);
                }
            }
            float h = ff.Health / Firefight.MaxHealth;
            shownHealth = Mathf.MoveTowards(shownHealth, h, dt * 1.5f);
            healthFill.fillAmount = shownHealth;
            healthFill.color = h > 0.6f ? Theme.Success : (h > 0.3f ? Theme.Accent : Color.Lerp(Theme.Danger, Color.white, Mathf.PingPong(Time.unscaledTime * 3f, 1f) * 0.35f));
            // ---- blood at the edges while hurt, a flash on every hit
            flash = Mathf.MoveTowards(flash, 0f, dt * 1.6f);
            float hurt = playing || ff.Dead ? ff.Hurt : 0f;
            float a = Mathf.Clamp01(Mathf.Pow(hurt, 1.2f) * 0.8f + flash * 0.7f + (ff.Dead ? 0.6f : 0f));
            blood.color = new Color(0.45f, 0f, 0f, a);
            if (playing && hurt > 0.55f && Time.unscaledTime >= nextBeat && Time.timeScale > 0f)
            {
                nextBeat = Time.unscaledTime + Mathf.Lerp(0.95f, 0.6f, hurt);
                Sfx.Play(Sfx.Heartbeat, 0.35f + hurt * 0.3f);
            }
            // ---- arrows towards whoever fired
            var cam = sn.Cam;
            foreach (var ar in arrows)
            {
                if (ar.t >= ar.life) { if (ar.img.gameObject.activeSelf) ar.img.gameObject.SetActive(false); continue; }
                ar.t += dt;
                if (cam == null || kill) { ar.img.enabled = false; continue; }
                ar.img.enabled = true;
                var d = ar.from - cam.transform.position;
                var v = new Vector2(Vector3.Dot(d, cam.transform.right), Vector3.Dot(d, cam.transform.up));
                if (v.sqrMagnitude < 1e-4f) v = Vector2.up;
                v.Normalize();
                float rad = ar.hit ? 300f : 340f;
                ar.rt.anchoredPosition = v * rad;
                ar.rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
                float k = 1f - ar.t / ar.life;
                float s = ar.hit ? 78f : 50f;
                ar.rt.sizeDelta = new Vector2(s, s * 0.9f);
                var col = ar.hit ? Theme.Danger : Theme.Guard;
                ar.img.color = new Color(col.r, col.g, col.b, Mathf.Clamp01(k * 1.4f) * (ar.hit ? 1f : 0.8f));
            }
        }
    }
}

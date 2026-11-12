using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>A dim layer over the whole screen with one soft round hole: the lesson's spotlight. It takes no touches,
    /// so the lit control is pressed right through it.</summary>
    public class SpotDim : MaskableGraphic
    {
        Vector2 center;
        float radius;
        bool hole;
        const float Feather = 70f;

        public void Set(bool on, Vector2 c, float r)
        {
            if (on == hole && (!on || ((c - center).sqrMagnitude < 0.5f && Mathf.Abs(r - radius) < 0.75f))) return;
            hole = on;
            center = c;
            radius = r;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var col = color;
            Color32 dark = col;
            Color32 clear = new Color(col.r, col.g, col.b, 0f);
            if (!hole)
            {
                const float E = 6000f;
                vh.AddVert(new Vector3(-E, -E), dark, Vector4.zero);
                vh.AddVert(new Vector3(-E, E), dark, Vector4.zero);
                vh.AddVert(new Vector3(E, E), dark, Vector4.zero);
                vh.AddVert(new Vector3(E, -E), dark, Vector4.zero);
                vh.AddTriangle(0, 1, 2);
                vh.AddTriangle(0, 2, 3);
                return;
            }
            const int seg = 56;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(center + d * radius, clear, Vector4.zero);
                vh.AddVert(center + d * (radius + Feather), dark, Vector4.zero);
                vh.AddVert(center + d * 9000f, dark, Vector4.zero);
            }
            for (int i = 0; i < seg; i++)
            {
                int b = i * 3, n = b + 3;
                vh.AddTriangle(b, b + 1, n + 1);
                vh.AddTriangle(b, n + 1, n);
                vh.AddTriangle(b + 1, b + 2, n + 2);
                vh.AddTriangle(b + 1, n + 2, n + 1);
            }
        }
    }

    /// <summary>
    /// Sniper school, one skill at a time: the first time a contract calls for something (the scope, the range dial,
    /// the wind, a walking target, the dart, armour, return fire, a new nest, the dossier, night vision, the drone) the
    /// screen dims, the control to use is lit with a pulsing ring and an arrow, and a card says what to do and why —
    /// with the round's own landing point drawn when the lesson is about where it goes (how far it drops, how far the
    /// wind takes it, how far a walker gets). Each step waits until the player has actually done it. Taught once; a
    /// lesson cut short (the contract ended, an urgent one took over) comes back the next time.
    /// </summary>
    public class Lessons : MonoBehaviour
    {
        HUD hud;
        RectTransform root, card, ring, arrow, hand, leadMark;
        SpotDim dim;
        CanvasGroup group;
        Text head, body, sub;
        Image icon, ringImg, arrowImg, leadImg, cardEdge;
        CanvasGroup skipGroup;
        float alpha;
        readonly Vector3[] corners = new Vector3[4], corners2 = new Vector3[4];
        int headKey = -1;
        string headLang;

        class Step
        {
            public string text, sub, icon = "info";
            public Func<string> liveText, liveSub;      // formatted lines (numbers): returned already translated
            public Func<Focus> focus;
            public Func<bool> done;
            public float min = 1.2f, max;               // seconds on screen: at least, at most (0 = until done)
            public bool dot;                            // draw the round's landing point whatever the contract's help
            public bool swipe;                          // a finger sweeping across the screen
            public Func<Vector3?> lead;                 // a ring where to aim (world)
            public Action enter;
            public bool needScope;                      // only through the scope: closed, the card says to open it
        }

        class Lesson
        {
            public string id;
            public bool urgent;                         // under fire: may cut in on a calmer lesson
            public bool skill;                          // a sniper skill (range, wind, lead): one of these per contract
            public Func<bool> when;
            public Step[] steps;
            public Action done;
            public int resumeAt;                        // cut short by an urgent lesson: carries on from here
            public Func<bool> abort;                    // what it is about is gone (dead, away, stopped): not learnt, again later
        }

        struct Focus
        {
            public bool on;
            public Vector2 p;
            public float r;
        }

        readonly List<Lesson> all = new List<Lesson>();
        readonly HashSet<string> learnt = new HashSet<string>();
        float nextPick;
        Lesson cur;
        int stepIndex;
        float stepT, idleT, holdT;
        bool skillThisRun;
        string lastText, lastSub;
        MissionDef def;

        Sniper Sn => hud.Sn;
        MissionRuntime Rt => hud.Runtime;

        public bool Active => cur != null;
        Step CurStep => cur != null && stepIndex < cur.steps.Length ? cur.steps[stepIndex] : null;
        /// <summary>Draw the impact dot even where the contract's help would not (range, wind and dart lessons).</summary>
        public bool ForceDot { get { var s = CurStep; return s != null && s.dot && alpha > 0.3f; } }
        public bool CardVisible => cur != null && alpha > 0.05f;
        public float CardHeight => card != null ? card.sizeDelta.y : 0f;

        // ------------------------------------------------------------------ build
        public static Lessons Create(HUD hud, RectTransform parent)
        {
            var r = UIK.Rect(parent, "Lessons");
            UIK.Stretch(r);
            var l = r.gameObject.AddComponent<Lessons>();
            l.hud = hud;
            l.root = r;
            l.Build();
            return l;
        }

        void Build()
        {
            group = UIK.Group(root.gameObject);
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            var dg = UIK.Rect(root, "Dim");
            UIK.Stretch(dg);
            dim = dg.gameObject.AddComponent<SpotDim>();
            dim.color = new Color(0f, 0.01f, 0.02f, 0.62f);
            dim.raycastTarget = false;

            ringImg = UIK.Img(root, "Ring", "glow_ring", Theme.Accent);
            ring = ringImg.rectTransform;
            ring.sizeDelta = new Vector2(200, 200);
            var inner = UIK.Img(ring, "Edge", "ring_thick", new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.95f));
            UIK.Stretch(inner.rectTransform, 26, 26, 26, 26);

            leadImg = UIK.Img(root, "Lead", "ring", Theme.Info);
            leadMark = leadImg.rectTransform;
            leadMark.sizeDelta = new Vector2(70, 70);
            var lc = UIK.Img(leadMark, "Dot", "circle", Theme.Info);
            UIK.Place(lc.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10));
            var ll = UIK.Txt(leadMark, "L", "BURAYA", Res.FontBold, 22, Theme.Info, TextAnchor.LowerCenter, false);
            UIK.Place(ll.rectTransform, UIK.MC, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(220, 28));
            var llo = ll.gameObject.AddComponent<Outline>();
            llo.effectColor = new Color(0, 0, 0, 0.9f);
            llo.effectDistance = new Vector2(1.5f, -1.5f);
            leadMark.gameObject.SetActive(false);

            arrowImg = UIK.Img(root, "Arrow", "tri_right", Theme.Accent);
            arrow = arrowImg.rectTransform;
            arrow.sizeDelta = new Vector2(64, 64);
            UIK.AddShadow(arrowImg, 3f, 0.7f);

            var hi = UIK.Icon(root, "hand", Color.white, 96);
            hand = hi.rectTransform;
            UIK.AddShadow(hi, 3f, 0.7f);
            hand.gameObject.SetActive(false);

            card = UIK.Rect(root, "Card");
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.sizeDelta = new Vector2(980, 150);
            var shade = UIK.Img(card, "Shade", "shadow", new Color(0, 0, 0, 0.55f), true);
            UIK.Stretch(shade.rectTransform, -40, -30, -40, -30);
            var bg = UIK.Panel(card, "Bg", new Color(0.03f, 0.045f, 0.06f, 0.96f), "rr22");
            UIK.Stretch(bg.rectTransform);
            cardEdge = UIK.Panel(card, "Edge", new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.85f), "rr22_line");
            UIK.Stretch(cardEdge.rectTransform);
            var ib = UIK.Img(card, "IconBg", "circle", new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.16f));
            UIK.Place(ib.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(22, -24), new Vector2(84, 84));
            icon = UIK.Icon(ib.transform, "info", Theme.Accent, 50);
            UIK.Place(icon.rectTransform, UIK.MC, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50, 50));
            head = UIK.Txt(card, "Head", "", Res.FontBold, 22, Theme.Accent, TextAnchor.UpperLeft, false);
            UIK.Place(head.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(128, -16), new Vector2(820, 28));
            body = UIK.Txt(card, "Body", "", Res.FontBold, 31, Theme.Text, TextAnchor.UpperLeft, false);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            UIK.Place(body.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(128, -46), new Vector2(820, 80));
            // skip: the one touchable piece (its own group lets touches through the lesson's)
            var sk = UIK.Panel(card, "Skip", new Color(1f, 1f, 1f, 0.1f), "pill");
            sk.raycastTarget = true;
            UIK.Place(sk.rectTransform, UIK.TR, new Vector2(1, 1), new Vector2(-14, -12), new Vector2(150, 40));
            var skg = sk.gameObject.AddComponent<CanvasGroup>();
            skg.ignoreParentGroups = true;
            // ÜBERSPRINGEN, ПРОПУСТИТЬ…: the word shrinks to fit the pill
            var skt = UIK.Txt(sk.transform, "T", "GEÇ", Res.FontBold, 20, Theme.TextDim, TextAnchor.MiddleCenter, true, 12);
            UIK.Stretch(skt.rectTransform, 10, 0, 10, 0);
            var skb = sk.gameObject.AddComponent<HoldButton>();
            skb.onDown = Skip;
            skipGroup = skg;
            sub = UIK.Txt(card, "Sub", "", Res.FontBody, 25, Theme.TextDim, TextAnchor.UpperLeft, false);
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            sub.verticalOverflow = VerticalWrapMode.Overflow;
            UIK.Place(sub.rectTransform, UIK.TL, new Vector2(0, 1), new Vector2(128, -110), new Vector2(820, 60));
        }

        // ------------------------------------------------------------------ run
        public void Begin(MissionDef d)
        {
            def = d;
            End();
            skillThisRun = false;
            BuildLessons();
        }

        /// <summary>The contract is over (or the HUD is gone): a lesson still running is not marked as learnt.</summary>
        public void End()
        {
            cur = null;
            stepIndex = 0;
            alpha = 0f;
            if (group != null) group.alpha = 0f;
            if (leadMark != null) leadMark.gameObject.SetActive(false);
            if (hand != null) hand.gameObject.SetActive(false);
            // the skip pill ignores the lesson group's fade: hide it with everything else
            if (skipGroup != null) { skipGroup.alpha = 0f; skipGroup.blocksRaycasts = false; skipGroup.interactable = false; }
        }

        public void Tick(float dt, bool hidden)
        {
            if (hud == null || Sn == null || Rt == null) return;
            // the pause menu: the clock of a step stands still too
            if (Game.I != null && Game.I.Paused) return;
            bool playing = Rt.State == MissionRuntime.Phase.Playing && !Rt.Relocating && (Firefight.I == null || !Firefight.I.Dead);
            // what to teach is looked at a few times a second, not every frame (ray casts, the people list)
            if (playing && !hidden && Time.unscaledTime >= nextPick)
            {
                nextPick = Time.unscaledTime + 0.2f;
                if (cur == null) Pick();
                else if (!cur.urgent) PreemptIfUrgent();
            }
            if (cur != null && playing && !hidden) Run(dt);
            float want = cur != null && playing && !hidden ? 1f : 0f;
            alpha = Mathf.MoveTowards(alpha, want, dt * (want > alpha ? 5f : 7f));
            group.alpha = alpha;
            if (skipGroup != null)
            {
                skipGroup.alpha = alpha;
                skipGroup.blocksRaycasts = alpha > 0.5f;
                skipGroup.interactable = alpha > 0.5f;
            }
            if (cur == null) { if (leadMark.gameObject.activeSelf) leadMark.gameObject.SetActive(false); return; }
            Draw();
        }

        void Pick()
        {
            foreach (var l in all)
            {
                if (learnt.Contains(l.id)) continue;
                if (l.skill && skillThisRun) continue;
                bool ok;
                try { ok = l.when(); } catch (Exception e) { Debug.LogException(e); ok = false; }
                if (!ok) continue;
                StartLesson(l);
                return;
            }
        }

        void PreemptIfUrgent()
        {
            foreach (var l in all)
            {
                if (!l.urgent || l == cur || learnt.Contains(l.id)) continue;
                bool ok;
                try { ok = l.when(); } catch { ok = false; }
                if (ok) { StartLesson(l); return; }
            }
        }

        void StartLesson(Lesson l)
        {
            // an urgent lesson cuts in: the calmer one was not learnt and may come back in this contract, where it left off
            if (cur != null)
            {
                if (cur.skill) skillThisRun = false;
                cur.resumeAt = cur == all.Find(x => x.id == "basics") ? stepIndex : 0;
            }
            cur = l;
            stepIndex = Mathf.Clamp(l.resumeAt, 0, l.steps.Length - 1);
            l.resumeAt = 0;
            if (l.skill) skillThisRun = true;
            Analytics.Event("lesson_begin", "lesson", l.id);
            EnterStep();
            Sfx.UI(Sfx.Scan);
        }

        void EnterStep()
        {
            stepT = 0f;
            idleT = 0f;
            holdT = 0f;
            lastText = lastSub = null;
            headKey = -1;
            var s = CurStep;
            if (s == null) return;
            s.enter?.Invoke();
            icon.sprite = Res.Icon(s.icon ?? "info");
            Tween.Pop(card, 0.25f, 0f, 0.92f);
        }

        void Run(float dt)
        {
            var s = CurStep;
            if (s == null) { Finish(); return; }
            // what the lesson is about is gone: it ends unlearnt and comes back another time
            bool gone;
            try { gone = cur.abort != null && cur.abort(); } catch { gone = true; }
            if (gone) { Abort(); return; }
            // a scope lesson with the scope down waits (the card asks to open it) — but not past its time limit
            if (s.needScope && !Sn.Scoped)
            {
                stepT += dt;
                if (s.max > 0f && stepT >= s.max) Next();
                return;
            }
            stepT += dt;
            bool ok;
            try { ok = s.done != null && s.done(); } catch (Exception e) { Debug.LogException(e); ok = true; }
            if ((ok && stepT >= s.min) || (s.max > 0f && stepT >= s.max)) Next();
        }

        void Next()
        {
            stepIndex++;
            if (stepIndex >= cur.steps.Length) { Finish(); return; }
            Sfx.UI(Sfx.Confirm);
            EnterStep();
        }

        void Abort()
        {
            if (cur == null) return;
            if (cur.skill) skillThisRun = false;
            cur.resumeAt = 0;
            cur = null;
        }

        void Finish()
        {
            var l = cur;
            cur = null;
            if (l == null) return;
            Save.SetFlag(Key(l));
            learnt.Add(l.id);
            Analytics.Event("lesson_done", "lesson", l.id);
            try { l.done?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            Sfx.UI(Sfx.Confirm);
        }

        static string Key(Lesson l) => "lesson_" + l.id;

        /// <summary>"I know this": the lesson counts as learnt.</summary>
        void Skip()
        {
            if (cur == null || alpha < 0.5f) return;
            Analytics.Event("lesson_skip", "lesson", cur.id, "step", stepIndex);
            Sfx.UI(Sfx.Click);
            Finish();
        }

        // ------------------------------------------------------------------ draw
        void Draw()
        {
            var s = CurStep;
            if (s == null) return;
            // the card: under the objective stack, where the coach box would be
            hud.Safe.GetWorldCorners(corners);
            float safeTop = root.InverseTransformPoint(corners[1]).y;
            float rootTop = root.rect.yMax;
            card.anchoredPosition = new Vector2(0f, -(rootTop - safeTop) - hud.ColumnTop);
            bool waitScope = s.needScope && !Sn.Scoped;
            bool live = !waitScope && s.liveText != null, liveSub = !waitScope && s.liveSub != null;
            string t = waitScope ? "Önce dürbünü aç." : (live ? s.liveText() : s.text);
            string u = waitScope ? null : (liveSub ? s.liveSub() : s.sub);
            if (t != lastText)
            {
                lastText = t;
                if (live) ((LocText)body).SetRaw(t ?? ""); else body.text = t ?? "";
            }
            if (u != lastSub)
            {
                lastSub = u;
                if (liveSub) ((LocText)sub).SetRaw(u ?? ""); else sub.text = u ?? "";
            }
            int n = cur.steps.Length;
            int hk = n * 100 + stepIndex;
            if (hk != headKey || Loc.Lang != headLang)
            {
                headKey = hk;
                headLang = Loc.Lang;
                ((LocText)head).SetRaw(n > 1 ? Loc.Tr("EĞİTİM") + "  ·  " + (stepIndex + 1) + "/" + n : Loc.Tr("EĞİTİM"));
            }
            // narrow screens (iPad): clear of the dossier and the drone button on the right
            float cw = Mathf.Clamp(root.rect.width - 840f, 640f, 980f), tw = cw - 160f;
            if (Mathf.Abs(body.rectTransform.sizeDelta.x - tw) > 0.5f)
            {
                body.rectTransform.sizeDelta = new Vector2(tw, body.rectTransform.sizeDelta.y);
                sub.rectTransform.sizeDelta = new Vector2(tw, sub.rectTransform.sizeDelta.y);
                head.rectTransform.sizeDelta = new Vector2(tw - 150f, 28f);
            }
            float bh = Mathf.Max(56f, body.preferredHeight);
            float sh = string.IsNullOrEmpty(sub.text) ? 0f : sub.preferredHeight;
            body.rectTransform.sizeDelta = new Vector2(tw, bh);
            sub.rectTransform.anchoredPosition = new Vector2(128, -46 - bh - 6);
            sub.rectTransform.sizeDelta = new Vector2(tw, sh);
            card.sizeDelta = new Vector2(cw, Mathf.Max(132f, 46f + bh + (sh > 0f ? sh + 10f : 0f) + 22f));
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            cardEdge.color = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.55f + 0.4f * pulse);

            // the spotlight, the ring and the arrow
            Focus f = default;
            try { if (waitScope) f = On(hud.ScopeRt); else if (s.focus != null) f = s.focus(); } catch (Exception e) { Debug.LogException(e); }
            dim.Set(f.on, f.p, f.r);
            dim.color = new Color(0f, 0.01f, 0.02f, f.on ? 0.6f : (s.swipe ? 0f : 0.25f));
            ring.gameObject.SetActive(f.on);
            arrow.gameObject.SetActive(f.on);
            if (f.on)
            {
                float k = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f);
                ring.anchoredPosition = f.p;
                ring.sizeDelta = Vector2.one * (f.r * 2f + 56f) * k;
                // the arrow comes from the middle of the screen towards the control
                var from = f.p.sqrMagnitude > 1f ? -f.p.normalized : Vector2.up;
                if (f.p.sqrMagnitude < 40000f) from = Vector2.up;            // near the centre: from above
                float bob = 14f * Mathf.Sin(Time.unscaledTime * 7f);
                arrow.anchoredPosition = f.p + from * (f.r + 70f + bob);
                arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(-from.y, -from.x) * Mathf.Rad2Deg);
            }
            // a finger sweeping across the screen
            hand.gameObject.SetActive(s.swipe);
            if (s.swipe)
            {
                float x = Mathf.Sin(Time.unscaledTime * 1.6f) * 260f;
                hand.anchoredPosition = new Vector2(x, -60f);
                hand.localRotation = Quaternion.Euler(0, 0, -15f + Mathf.Sin(Time.unscaledTime * 1.6f) * 8f);
            }
            // where to aim (a walker's lead)
            Vector3? lw = null;
            try { if (s.lead != null && !waitScope) lw = s.lead(); } catch { lw = null; }
            bool showLead = false;
            if (lw.HasValue && Sn.Cam != null)
            {
                var sp = Sn.Cam.WorldToScreenPoint(lw.Value);
                if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var lp))
                {
                    showLead = true;
                    leadMark.anchoredPosition = lp;
                    float lk = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
                    leadMark.localScale = new Vector3(lk, lk, 1f);
                }
            }
            if (leadMark.gameObject.activeSelf != showLead) leadMark.gameObject.SetActive(showLead);
        }

        // ------------------------------------------------------------------ focus helpers
        Focus On(RectTransform r, float pad = 10f)
        {
            if (r == null || !r.gameObject.activeInHierarchy) return default;
            var c = corners2;
            r.GetWorldCorners(c);
            Vector2 a = root.InverseTransformPoint(c[0]), b = root.InverseTransformPoint(c[2]);
            var mid = (a + b) * 0.5f;
            float rad = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) * 0.5f + pad;
            return new Focus { on = true, p = mid - root.rect.center, r = rad };
        }

        Focus OnWorld(Vector3 w, float rad)
        {
            if (Sn == null || Sn.Cam == null) return default;
            var sp = Sn.Cam.WorldToScreenPoint(w);
            if (sp.z <= 0f) return default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var lp)) return default;
            return new Focus { on = true, p = lp - root.rect.center, r = rad };
        }

        /// <summary>The middle of the scope, wide enough to take in the round's landing point too.</summary>
        Focus OnScopeCentre(float minR = 110f)
        {
            float r = minR;
            if (Sn.RawPrediction.hit && Sn.Cam != null)
            {
                var sp = Sn.Cam.WorldToScreenPoint(Sn.RawPrediction.bodyPoint);
                if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var lp))
                    r = Mathf.Clamp((lp - root.rect.center).magnitude + 60f, minR, 360f);
            }
            return new Focus { on = true, p = Vector2.zero, r = r };
        }

        // ------------------------------------------------------------------ the people the lessons are about
        NPC MainTarget()
        {
            foreach (var t in Rt.Targets)
                if (t != null && !t.Dead && !t.Escaped && !t.IsHidden && !t.Incapacitated && t.MarkedTarget) return t;
            return null;
        }

        bool InScope(NPC n, float frac = 0.45f)
        {
            if (n == null || Sn.Cam == null || !Sn.Scoped) return false;
            var to = n.ChestPoint - Sn.Cam.transform.position;
            return Vector3.Angle(Sn.Cam.transform.forward, to) < Sn.Cam.fieldOfView * frac &&
                   !Physics.Linecast(Sn.Cam.transform.position, n.ChestPoint, Layers.WorldMask, QueryTriggerInteraction.Ignore);
        }

        float Range(NPC n) => n != null && Sn.Cam != null ? Vector3.Distance(Sn.Cam.transform.position, n.ChestPoint) : 0f;

        /// <summary>Is the scope's next round going into <paramref name="n"/> — on its own, without the scope's help (the
        /// lesson is about holding for the wind or the lead yourself)?</summary>
        bool OnHim(NPC n) => n != null && Sn.Scoped && Sn.RawPrediction.hit && Sn.RawPrediction.Npc == n;

        static int Round50(float d) => Mathf.Clamp(Mathf.RoundToInt(d / Sniper.ZeroStep) * Sniper.ZeroStep, Sniper.ZeroMin, Sniper.ZeroMax);

        /// <summary>Crosswind at the target (m/s, + blowing to the right as the shooter sees it).</summary>
        float Cross()
        {
            var w = new Vector2(Sn.Wind.x, Sn.Wind.z);
            float spd = w.magnitude;
            if (spd < 0.05f) return 0f;
            float windYaw = Mathf.Atan2(w.x, w.y) * Mathf.Rad2Deg;
            float rel = Mathf.DeltaAngle(Sn.transform.eulerAngles.y, windYaw);
            return spd * Mathf.Sin(rel * Mathf.Deg2Rad);
        }

        static string F(string src, params object[] args)
        {
            try { return string.Format(Loc.Culture, Loc.Tr(src), args); }
            catch { return string.Format(Loc.Culture, src, args); }
        }

        static string Num(float v) => v.ToString("0.0", Loc.Culture);

        // ------------------------------------------------------------------ the curriculum
        void BuildLessons()
        {
            all.Clear();
            learnt.Clear();
            var sn = Sn;
            var rt = Rt;
            if (sn == null || rt == null) return;
            int number = def != null ? def.number : 0;
            NPC focusNpc = null, threatNpc = null;
            float startZoom = 0f;
            int shots0 = 0;
            float range0 = 0f, drop0 = 0f, drift0 = 0f;

            // ---- 1. the basics (first contract): look, scope, the target, the scan, zoom, breath, fire
            all.Add(new Lesson
            {
                id = "basics",
                when = () => number == 1 && !Save.SeenTutorial,
                steps = new[]
                {
                    new Step { text = "Ekranı parmağınla sürükleyerek etrafa bak.", icon = "hand", swipe = true, min = 0.6f,
                               enter = () => Analytics.Event("tutorial_begin"), done = () => hud.DragAccum > 380f },
                    new Step { text = "Sağdaki dürbün tuşuna dokunarak dürbünü aç.", icon = "scope",
                               focus = () => On(hud.ScopeRt), done = () => sn.Scoped, min = 0.3f },
                    new Step { needScope = true, text = "Kırmızı işaret hedefi gösterir. Nişangahı hedefin üzerine getir.", icon = "target",
                               focus = () => { var t = MainTarget(); return t != null ? OnWorld(t.ChestPoint, 70f) : default; },
                               done = () => { var t = MainTarget(); return t == null || sn.Hovered == t || InScope(t, 0.12f); } },
                    new Step { needScope = true, text = "Nişangahı kişinin üzerinde tut: kimliği taranıyor…", icon = "scan",
                               focus = () => OnScopeCentre(90f), done = () => { var t = MainTarget(); return t == null || t.Identified; }, min = 0.4f },
                    new Step { needScope = true, text = "Yakınlaştır: soldaki kaydırıcıyı yukarı it ya da iki parmakla ekranı aç.", icon = "zoom_in",
                               enter = () => startZoom = sn.Zoom, focus = () => On(hud.ZoomRt),
                               done = () => sn.Zoom >= Mathf.Min(sn.MaxZoom - 0.1f, startZoom + 3f), max = 14f },
                    new Step { needScope = true, text = "Soldaki NEFES tuşuna basılı tut: nişan sabitlenir.", sub = "Halka boşalmadan ateş et: nefes biterse nişan titrer.", icon = "breath",
                               focus = () => On(hud.BreathRt), done = () => { holdT = sn.HoldingBreath ? holdT + Time.unscaledDeltaTime : 0f; return holdT > 0.5f; }, max = 20f },
                    new Step { text = "Hazır! Büyük kırmızı tuşla ateş et.", sub = "Kırmızı nokta merminin ineceği yeri gösterir.", icon = "fire",
                               enter = () => shots0 = rt.Shots, focus = () => On(hud.FireRt), done = () => rt.Shots > shots0, min = 0.3f },
                },
                done = () =>
                {
                    Save.SeenTutorial = true;
                    Analytics.Event("tutorial_complete");
                    Analytics.MetaEvent("tutorial");
                },
            });

            // ---- urgent: a man taking aim at the nest
            all.Add(new Lesson
            {
                id = "threat",
                urgent = true,
                when = () =>
                {
                    threatNpc = null;
                    foreach (var n in NPC.All)
                        if (n != null && !n.Dead && !n.Incapacitated && (n.ThreatToPlayer > 0.12f || n.AimingAtPlayer) && sn.Cam != null &&
                            sn.Cam.WorldToScreenPoint(n.ChestPoint).z > 0f) { threatNpc = n; break; }
                    return threatNpc != null;
                },
                steps = new[]
                {
                    new Step { text = "Kırmızı halka: bu adam sana nişan alıyor!", sub = "Halka dolmadan onu vur ya da SİPER'e eğil.", icon = "alert",
                               focus = () => threatNpc != null ? OnWorld(threatNpc.ChestPoint, 80f) : default,
                               done = () => threatNpc == null || threatNpc.Dead || threatNpc.Incapacitated || sn.InCover || (threatNpc.ThreatToPlayer <= 0.01f && !threatNpc.AimingAtPlayer),
                               min = 1.6f, max = 7f },
                },
            });

            // ---- urgent: rounds coming in
            all.Add(new Lesson
            {
                id = "cover",
                urgent = true,
                when = () => hud.Combat != null && hud.Combat.CoverVisible && Firefight.I != null && (Firefight.I.HitsTaken > 0 || Firefight.I.Health < 85f),
                steps = new[]
                {
                    new Step { text = "Vuruluyorsun! SİPER tuşuna bas: duvarın arkasına eğil.", icon = "shield",
                               focus = () => On(hud.Combat.CoverButton), done = () => sn.InCover, min = 0.4f, max = 10f },
                    new Step { text = "Siperde can yenilenir.", sub = "Ateş kesilince dürbünle doğrul ve karşılık ver: önce sana nişan alanı vur.", icon = "heart",
                               done = () => !sn.InCover, min = 2.5f, max = 6f },
                },
            });

            // ---- the range dial: the round falls on its way
            all.Add(new Lesson
            {
                id = "range",
                skill = true,
                abort = () => focusNpc == null || focusNpc.Dead || focusNpc.Escaped || focusNpc.Incapacitated,
                when = () =>
                {
                    var t = MainTarget();
                    if (t == null || (t.NeedsId && !t.Identified) || !InScope(t, 0.3f)) return false;
                    float d = Range(t);
                    if (d < 175f) return false;
                    float drop = Difficulty.DropAt(d, sn.ZeroRange, sn.CurrentSpec.velocity);
                    // the lesson is about the drop: a round that falls short (a dial set past him is the spotter's call)
                    if (drop > -0.1f || Mathf.Abs(Round50(d) - sn.ZeroRange) < 50) return false;
                    focusNpc = t;
                    range0 = d;
                    drop0 = drop;
                    return true;
                },
                steps = new[]
                {
                    new Step { needScope = true, icon = "distance", dot = true, min = 3.5f, max = 6f,
                               liveText = () => F("Hedef {0} m uzakta. Mermi yol boyunca düşer.", Mathf.RoundToInt(range0)),
                               liveSub = () => F("Kırmızı nokta merminin ineceği yer: nişangahın {0} cm altında. Kafaya nişan alırsan ıskalarsın.",
                                                 Mathf.RoundToInt(Mathf.Abs(drop0) * 100f)),
                               focus = () => OnScopeCentre(), done = () => false },
                    new Step { needScope = true, icon = "distance", dot = true, max = 25f,
                               liveText = () => sn.ZeroRange < Round50(range0) ? Loc.Tr("MENZİL'i hedefin mesafesine getir: + tuşuna bas.")
                                                                                 : Loc.Tr("MENZİL'i hedefin mesafesine getir: − tuşuna bas."),
                               liveSub = () => F("MENZİL {0} m  ·  hedef {1} m", sn.ZeroRange, Mathf.RoundToInt(range0)),
                               focus = () => On(sn.ZeroRange < Round50(range0) ? hud.ZeroUpRt : hud.ZeroDownRt, 6f),
                               done = () => Mathf.Abs(sn.ZeroRange - Round50(range0)) <= 25 || !sn.Scoped, min = 0.5f },
                    new Step { text = "Tam ayar! Nokta nişangaha döndü.", sub = "Uzak hedefte önce mesafeyi oku, MENZİL'i ona getir, sonra ateş et.", icon = "fire", dot = true,
                               enter = () => shots0 = rt.Shots, focus = () => On(hud.FireRt), done = () => rt.Shots > shots0, min = 0.6f, max = 12f },
                },
            });

            // ---- a walker: aim where he will be
            all.Add(new Lesson
            {
                id = "lead",
                skill = true,
                abort = () => focusNpc == null || focusNpc.Dead || focusNpc.Escaped || focusNpc.Incapacitated || !focusNpc.Moving,
                when = () =>
                {
                    var t = MainTarget();
                    if (t == null || t.Riding || !InScope(t, 0.35f)) return false;
                    float sp = new Vector3(t.Velocity.x, 0f, t.Velocity.z).magnitude;
                    if (sp < 0.9f || !t.Moving) return false;
                    float d = Range(t);
                    float tof = d / Mathf.Max(100f, sn.CurrentSpec.velocity);
                    if (sp * tof < 0.2f) return false;
                    focusNpc = t;
                    range0 = d;
                    return true;
                },
                steps = new[]
                {
                    new Step { needScope = true, text = "Hedef yürüyor: mermi oraya varana kadar o da ilerler.", sub = "Nişangahı yürüdüğü yöne, mavi halkanın üzerine al.", icon = "run", dot = true,
                               lead = () =>
                               {
                                   if (focusNpc == null || focusNpc.Dead) return null;
                                   float tof = Range(focusNpc) / Mathf.Max(100f, sn.CurrentSpec.velocity);
                                   return focusNpc.ChestPoint + focusNpc.PredictOffset(tof);
                               },
                               done = () => { holdT = OnHim(focusNpc) ? holdT + Time.unscaledDeltaTime : 0f; return holdT > 0.25f; },
                               min = 1.5f, max = 14f },
                    new Step { text = "İşte bu! Önünde kal ve ateş et.", sub = "Koşan adamda pay daha büyük, duran adamda hiç yok.", icon = "fire", dot = true,
                               enter = () => shots0 = rt.Shots, focus = () => On(hud.FireRt), done = () => rt.Shots > shots0, min = 0.3f, max = 8f },
                },
            });

            // ---- the wind: it carries the round sideways
            all.Add(new Lesson
            {
                id = "wind",
                skill = true,
                abort = () => focusNpc == null || focusNpc.Dead || focusNpc.Escaped || focusNpc.Incapacitated,
                when = () =>
                {
                    var t = MainTarget();
                    if (t == null || !InScope(t, 0.3f)) return false;
                    float d = Range(t);
                    if (d < 120f) return false;
                    float drift = Difficulty.DriftAt(d, Cross(), sn.CurrentSpec.velocity);
                    if (Mathf.Abs(drift) < 0.15f) return false;
                    focusNpc = t;
                    range0 = d;
                    drift0 = drift;
                    return true;
                },
                steps = new[]
                {
                    new Step { text = "Rüzgâr mermiyi yana iter. Yönü ve gücü bu panelde.", icon = "wind",
                               focus = () => On(hud.WindRt, 4f), done = () => false, min = 3f, max = 4.5f },
                    new Step { needScope = true, icon = "wind", dot = true, min = 3.5f, max = 6f,
                               liveText = () => drift0 > 0f ? F("Bak: rüzgâr mermiyi {0} cm sağa götürüyor.", Mathf.RoundToInt(Mathf.Abs(drift0) * 100f))
                                                            : F("Bak: rüzgâr mermiyi {0} cm sola götürüyor.", Mathf.RoundToInt(Mathf.Abs(drift0) * 100f)),
                               sub = "Kırmızı nokta merminin ineceği yer.", focus = () => OnScopeCentre(), done = () => false },
                    new Step { needScope = true, icon = "wind", dot = true, max = 16f,
                               liveText = () =>
                               {
                                   float mil = Mathf.Abs(drift0) / Mathf.Max(1f, range0) * 1000f;
                                   return drift0 > 0f ? F("Rüzgâra karşı tut: nişangahı {0} mil sola kaydır.", Num(mil))
                                                      : F("Rüzgâra karşı tut: nişangahı {0} mil sağa kaydır.", Num(mil));
                               },
                               sub = "Sarı noktalar 1 mil aralıklı. Kırmızı nokta hedefe gelince ateş et.",
                               focus = () => OnScopeCentre(),
                               done = () => { holdT = OnHim(focusNpc) ? holdT + Time.unscaledDeltaTime : 0f; return holdT > 0.25f; },
                               min = 1.5f },
                    new Step { text = "Tam üstünde! Şimdi ateş et.", sub = "Rüzgâr dinip artar: ateşten önce paneldeki değere bir daha bak.", icon = "fire", dot = true,
                               enter = () => shots0 = rt.Shots, focus = () => On(hud.FireRt), done = () => rt.Shots > shots0, min = 0.3f, max = 8f },
                },
            });

            // ---- the dart: nobody dies today
            all.Add(new Lesson
            {
                id = "tranq",
                when = () => hud.AmmoTypeShown && Gear.AmmoAvailable(AmmoKind.Tranquilizer) > 0 && sn.AmmoType != AmmoKind.Tranquilizer &&
                             (rt.Objectives.Exists(o => o.State == ObjectiveState.Active && o.kind == ObjectiveKind.Sedate) || (def != null && def.kind == "CANLI YAKALA")),
                steps = new[]
                {
                    new Step { text = "Bu işte kimse ölmeyecek: bu tuşla İĞNE'ye geç.", sub = "Mermi seçici: her basış bir sonraki türü takar.", icon = "dart",
                               focus = () => On(hud.AmmoTypeRt), done = () => sn.AmmoType == AmmoKind.Tranquilizer, min = 0.4f, max = 20f },
                    new Step { needScope = true, text = "İğne yavaş uçar ve çok düşer: biraz yukarı nişan al.", sub = "Kırmızı nokta iğnenin ineceği yeri gösterir.", icon = "dart", dot = true,
                               focus = () => OnScopeCentre(), done = () => false, min = 3.5f, max = 5f },
                },
            });

            // ---- armour: the plate stopped the round
            all.Add(new Lesson
            {
                id = "armor",
                when = () => rt.ArmorStopped,
                steps = new[]
                {
                    new Step { icon = "pierce", min = 1f, max = 9f,
                               liveText = () => Loc.Tr("Zırhlı adam! Gövdesine mermi işlemez."),
                               liveSub = () => Gear.AmmoAvailable(AmmoKind.ArmorPiercing) > 0 ? Loc.Tr("Bu tuşla ZIRH DELİCİ'ye geç ya da kafasına nişan al.")
                                                                                               : Loc.Tr("Kafasına nişan al. Zırh delici mermiyi cephanelikten alabilirsin."),
                               focus = () => Gear.AmmoAvailable(AmmoKind.ArmorPiercing) > 0 && hud.AmmoTypeShown ? On(hud.AmmoTypeRt) : default,
                               done = () => sn.AmmoType == AmmoKind.ArmorPiercing || Gear.AmmoAvailable(AmmoKind.ArmorPiercing) <= 0 },
                },
            });

            // ---- the nest is known: move
            all.Add(new Lesson
            {
                id = "relocate",
                when = () => Firefight.I != null && Firefight.I.Located && rt.CanRelocate && hud.MoveShown,
                steps = new[]
                {
                    new Step { text = "Yerin belli oldu. YER DEĞİŞTİR: yeni yerde seni kimse bilmez.", sub = "Taşınırken dünya durmaz: hedef kaçmadan dön.", icon = "map",
                               focus = () => On(hud.MoveRt), done = () => rt.Relocating || !hud.MoveShown, min = 0.5f, max = 9f },
                },
            });

            // ---- the dossier: is this the face?
            all.Add(new Lesson
            {
                id = "tag",
                when = () => hud.TagShown,
                steps = new[]
                {
                    new Step { text = "Bu yüz dosyadakiyle aynıysa İŞARETLE — HEDEF BU.", sub = "Emin değilsen basma: yanlış kişiyi vurmak işi bitirir.", icon = "tag",
                               focus = () => On(hud.TagRt), done = () => !hud.TagShown, min = 0.6f, max = 10f },
                },
            });

            // ---- night vision / thermal
            all.Add(new Lesson
            {
                id = "vision",
                when = () => hud.VisionShown && sn.HasVisionGear && sn.Vision == VisionMode.Normal && rt.IsNight,
                steps = new[]
                {
                    new Step { text = "Karanlıkta gece görüşü kullan: bu tuşla modu değiştir.", sub = "Termal dürbünde insanlar beyaz parlar: sis ve kar arkasında bile.", icon = "nightvision",
                               focus = () => On(hud.VisionRt), done = () => sn.Vision != VisionMode.Normal, min = 0.4f, max = 10f },
                },
            });

            // ---- the drone
            all.Add(new Lesson
            {
                id = "drone",
                when = () => hud.DroneShown && !sn.Scoped && rt.Elapsed > 6f,
                steps = new[]
                {
                    new Step { text = "DRON'u gönder: bölgeyi tarar, tehditleri işaretler.", sub = "Her sözleşmede bir kez.", icon = "drone",
                               focus = () => On(hud.DroneRt), done = () => rt.DroneUsed || !hud.DroneShown, min = 0.5f, max = 8f },
                },
            });
            foreach (var l in all) if (Save.Flag(Key(l))) learnt.Add(l.id);
        }
    }
}

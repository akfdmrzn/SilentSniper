using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// City centre contracts (13-20): the harbour network is broken, the money and the protection behind it are not.
    /// Every contract climbs one step closer to Kenan Baran: his treasurer, his courier, his partner, the prosecutor he
    /// wants silenced, his bookkeeper, the killer and the policeman on his payroll, his security chief, then the man
    /// himself. Each one has its own set piece: shooting through office glass, a tram that will not wait, a red carpet
    /// with a body double, a counter-sniper in a bell tower, a ferry crowd, a meeting under umbrellas, a helicopter on a
    /// hotel roof, a penthouse under a searchlight.
    /// </summary>
    public static partial class MissionDefs
    {
        const float SW = 0.15f;      // sidewalk / square level in the city layout

        static void AddCity(List<MissionDef> list)
        {
            // ------------------------------------------------------------------ 13. the glass tower (night): through the window
            list.Add(new MissionDef
            {
                id = "m13", number = 13, region = "city", title = "CAM KULE", location = "Kordon Plaza — 11. kat",
                preset = "night", wind = V(1.4f, 0, 0.8f), reward = 8500, focus = "office_window", icon = "office",
                kind = "SUİKAST", clock = "22:40", pin = "office_window", pinUp = 3.4f,
                introLine = "Defne Aksoy hâlâ ofiste. Cam mermiyi durdurmaz — ama kırıldığı an içerideki herkes duyar.",
                brief = "Liman ağı çöktü ama paranın aktığı yer hâlâ ayakta: Baran Holding. Finans direktörü Defne Aksoy, örgütün kirli parasını aklıyor ve bu gece hesapları yurtdışına taşıyacak.\n\nKordon Plaza'nın 11. katında, ışıkları yanan ofiste tek başına çalışıyor. Katta bir güvenlik görevlisi devriye geziyor. Saat 22:42'de asansörle iniyor: ondan önce bitir.",
                tip = "Cam mermiyi yavaşlatır ama durdurmaz. İlk atışta cam kırılır ve içeridekiler sesi duyar: Defne kırık cama bakmaya gelir, ikinci kez ürkerse asansöre koşar.",
                coverHint = "Defne masaların arkasında, görünmüyor. Ofisin camına yakın bir yere ateş et: kırılan camı görmeye pencereye gelir.",
                setup = rt =>
                {
                    // V3 (device test: waiting for her to come to the window took too long): she starts at the window
                    var t = rt.AddTarget("office_f", rt.M("office_window"), 270f);
                    t.HideOnFlee = true;
                    t.FleePath = rt.P("office_to_lift");
                    t.EscapeText = "Defne asansöre bindi";
                    t.EscapeFailDelay = 2.2f;
                    t.SightRange = 16f;
                    // window (on the phone), the meeting corner, the window again, a short stop at the desk, the window:
                    // most of the loop she stands at the glass
                    t.SetPlan(Act.IdleFacing("phone_conv", 270f, 9f), Act.IdleFacing("idle_look", 250f, 3f),
                              Act.Move(rt.P("office_to_meet"), "walk"),
                              Act.IdleFacing("talk", 47f, 5f), Act.IdleFacing("argue", 47f, 3f),
                              Act.Move(MissionRuntime.Rev(rt.P("office_to_meet")), "walk"),
                              Act.IdleFacing("drink", 270f, 7f), Act.IdleFacing("idle_look", 270f, 4f),
                              Act.Move(MissionRuntime.Rev(rt.P("office_to_window")), "walk"),
                              Act.IdleFacing("phone_conv", 270f, 4f),
                              Act.Move(rt.P("office_to_window"), "walk"),
                              Act.Loop(0));
                    var acc = rt.Spawn("guest_m", rt.M("office_coworker"), rt.MY("office_coworker"), Role.Civilian, "Muhasebe Şefi");
                    rt.Idler(acc, rt.MY("office_coworker"), "phone", "idle_wait", "talk");
                    var g = rt.Spawn("guard_b", rt.M("office_guard"), rt.MY("office_guard"), Role.Guard, "Kat Güvenliği");
                    rt.Patrol(g, rt.P("office_patrol"), "walk", 0.8f, "idle_look", 3f);
                    var c = rt.Spawn("worker_c", rt.M("office_cleaner"), rt.MY("office_cleaner"), Role.Worker, "Temizlik Görevlisi");
                    rt.Idler(c, rt.MY("office_cleaner"), "sweep", "idle", "sweep");

                    rt.Countdown("Asansöre inmesine", 92f);
                    rt.At(92f, () =>
                    {
                        if (t.Dead || t.Escaped || t.Incapacitated || t.Panicking) return;
                        t.EndInvestigation();
                        rt.Say("Defne çantasını aldı — asansöre yürüyor!", Theme.Danger, "run");
                        t.SetPlan(Act.Move(MissionRuntime.From(rt.P("office_to_lift"), t.transform.position), "walk_confident"), Act.Escape());
                    });
                    rt.RadioAt(7f, "KONTROL", "Katta bir güvenlik görevlisi var. Cesedi görürse alarm verir, Defne de kaçar.", 4.5f);
                    CityLife(rt, "night", "office_f", "guest_m");
                    Traffic(rt, true, true);
                    Trams(rt, 9f, 31f);
                }
            }.Target("office_f", "Defne Aksoy", "Kasa", "Siyah ceket, mavi etek, kızıl saç, gözlük. Kordon Plaza 11. kat.")
             .Goal("Defne Aksoy'u asansöre binmeden etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.MaxShots, "Tek atış", 1)
             .Star(ChallengeKind.AllHeadshots, "Hedefi kafadan vur"));

            // ------------------------------------------------------------------ 14. the tram (sunset): the courier among the commuters
            list.Add(new MissionDef
            {
                id = "m14", number = 14, region = "city", title = "TRAMVAY", location = "Saat Kulesi tramvay durağı",
                preset = "sunset", wind = V(-2.2f, 0, 1.2f), reward = 9000, focus = "tram_platform_s", icon = "tram",
                kind = "SUİKAST", clock = "19:05",
                introLine = "Postacı defteri tramvayla şehirden çıkaracak. 19:06 tramvayı durağa girdiğinde iş biter.",
                brief = "Mert Tunç, nam-ı diğer Postacı, Baran'ın rüşvet defterini taşıyor: kime ne ödendiyse hepsi orada. Kordon'dan yürüyüp Saat Kulesi durağında tramvay bekleyecek.\n\nDurak kalabalık. Postacı'ya çok benzeyen bir yolcu da orada: kapüşonlu, kasketli. Ateş etmeden önce tara. Tramvaya binerse onu kaybederiz.",
                tip = "Yürüyen hedefe biraz önden nişan al. Durakta bekleyenlerden biri Postacı'ya çok benziyor: nişangahı üzerinde tut, kimlik kartını oku.",
                setup = rt =>
                {
                    rt.WanderIdlers = false;                          // a crowded platform
                    var app = new[] { V(8f, SW, 117f), V(-12f, SW, 117f), V(-31.8f, SW, 116.4f), V(-31.8f, 0.35f, 120.4f), V(-29.4f, 0.35f, 120.9f) };
                    var t = rt.AddTarget("photog", app[0], 270f);
                    t.FleePath = new[] { V(-30.4f, SW, 116.4f), V(30f, SW, 117f), V(70f, SW, 117f), rt.M("exit_6") };
                    t.HideOnFlee = true;
                    t.EscapeText = "Postacı kaçtı";
                    t.SetPlan(Act.Move(app, "walk", 1.05f), Act.IdleFacing("idle_look", 330f, 3f), Act.IdleFacing("phone_conv", 10f, 12f),
                              Act.IdleFacing("idle_wait", 0f, 9f), Act.IdleFacing("idle_look", 60f, 3f), Act.IdleFacing("idle_wait", 0f, 0f));
                    // moving-target star: remember his speed until the moment he falls
                    float lastSpeed = 0f;
                    rt.When(() => { if (!t.Dead) { lastSpeed = t.Velocity.magnitude; return false; } return true; },
                            () => { if (lastSpeed > 0.4f) rt.SetFlag("moving"); });

                    // the look-alike and the other commuters on the platform
                    var d = rt.Spawn("runner", rt.M("commuter_3"), 0f, Role.Decoy, "Yolcu", "SİVİL", "Kapüşonlu, kasketli — Postacı değil");
                    d.ScanNote = "Hedef değil — Postacı'ya benzeyen bir yolcu";
                    rt.Idler(d, 0f, "smoke", "idle_wait", "phone");
                    var riders = new List<NPC> { d };
                    string[] who = { "office_f", "elder_m", "tourist_f", "student_m", "guest_f_c", "civilian_f", "guest_m" };
                    int k = 0;
                    foreach (int i in new[] { 0, 1, 2, 5, 6, 7 })
                    {
                        var c = rt.Spawn(who[k++ % who.Length], rt.M("commuter_" + i), 0f, Role.Civilian, "Yolcu");
                        rt.Idler(c, (i * 37f) % 40f - 20f, "idle_wait", "phone", "idle_look", "idle_shift");
                        riders.Add(c);
                    }

                    // the 19:06 westbound tram: rolls in from the east, nine seconds at the stop, everybody gets on
                    var line = rt.P("tram_west");
                    bool atStop = false;
                    rt.At(13f, () =>
                    {
                        var m = rt.MoveProp("tram_w", new[] { line[0], line[1] }, 9f, false);
                        if (m == null) return;
                        rt.Countdown("Tramvayın gelmesine", () => atStop ? 0f : MissionRuntime.EtaOf(m, 9f));
                        rt.Radio("KONTROL", "Tramvay yola çıktı. Durağa girdiği an Postacı biner.", 4f);
                        m.Arrived = () =>
                        {
                            atStop = true;
                            rt.ClearCountdown();
                            Sfx.PlayAt(Sfx.TramBell, line[1], 0.8f);
                            if (!rt.IsPlaying) return;
                            rt.Say("Tramvay durakta — herkes biniyor!", Theme.Danger, "tram");
                            foreach (var r in riders)
                            {
                                if (r == null || r.Dead || r.Incapacitated || r.Panicking || r.Escaped) continue;
                                var door = NearestBoard(rt, r.transform.position);
                                r.EndInvestigation();
                                r.SetPlan(Act.Idle("idle_look", 0.3f + UnityEngine.Random.value), Act.Move(new[] { door }, "walk"), Act.Hide());
                            }
                            if (!t.Dead && !t.Incapacitated && !t.Panicking && !t.Escaped)
                            {
                                t.EndInvestigation();
                                t.SetPlan(Act.Idle("idle_look", 0.6f), Act.Move(new[] { NearestBoard(rt, t.transform.position) }, "walk"), Act.Escape());
                                t.EscapeText = "Postacı tramvaya bindi";
                                t.EscapeFailDelay = 3.5f;
                            }
                            rt.After(9f, () =>
                            {
                                Sfx.PlayAt(Sfx.TramBell, line[1], 0.6f);
                                rt.MoveProp("tram_w", new[] { line[1], line[2] }, 9f, false);
                            });
                        };
                    });
                    rt.RadioAt(5f, "KONTROL", "Postacı Kordon'dan durağa yürüyor. Durakta ona benzeyen biri var — önce tara.", 4.5f);
                    TramLine(rt, "tram_e", "tram_east", 24f, 10f, 60f);
                    CityLife(rt, "evening", "photog", "runner");
                    Traffic(rt, true, true);
                }
            }.Target("photog", "Mert Tunç", "Postacı", "Koyu kapüşonlu, gri kasket, kot pantolon. Durağa yürüyor.")
             .Goal("Postacı'yı tramvaya binmeden etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.Custom, "Postacı'yı yürürken vur", 0, r => r.Flag("moving"))
             .Star(ChallengeKind.NoCivilians, "Yolculara zarar verme"));

            // ------------------------------------------------------------------ 15. the opera gala (night): red carpet, flashes, a body double
            list.Add(new MissionDef
            {
                id = "m15", number = 15, region = "city", title = "GALA", location = "Devlet Opera ve Balesi", markTargets = 1,
                preset = "night", wind = V(-1.2f, 0, -2.0f), reward = 10000, focus = "opera_carpet1", icon = "opera",
                kind = "SUİKAST", clock = "20:30",
                introLine = "Aydemir'in dublörü önden gelir. Limuzinden inen adama inanma — önce tara.",
                brief = "Armatör Cevdet Aydemir, Baran'ın gemilerinin sahibi. Bu gece operanın sezon açılışında: kırmızı halı, fotoğrafçılar, polis.\n\nAydemir kalabalığa asla önce kendisi çıkmaz: limuzinden önce dublörü iner. Gerçek Aydemir birkaç dakika sonra siyah bir sedanla gelir, eşiyle halıda poz verir ve içeri girer. Perde arasında balkona çıkacağı biliniyor.",
                tip = "Flaşlar patlarken dürbünde parlama olur, panik yapma. Dublörü vurursan herkes kaçar ve gerçek Aydemir'in arabası durmadan geçer.",
                introNote = "Dikkat: limuzinden inen adam Aydemir'in dublörü olabilir",
                setup = rt =>
                {
                    rt.WanderIdlers = false;                          // the forecourt is full of guests and press
                    Level.I.ShowTagged("limo", true);
                    Level.I.ShowTagged("gala_sedan", true);
                    var arrive = rt.P("gala_arrive");
                    var walk = rt.P("carpet_walk");
                    var up = rt.P("carpet_up");
                    var crew = rt.Root.gameObject.AddComponent<FlashCrew>();
                    for (int i = 0; i < 4; i++)
                    {
                        var ph = rt.Spawn(i % 2 == 0 ? "photog" : "student_m", rt.M("press_" + i), rt.MY("press_" + i), Role.Civilian, "Foto Muhabiri", "BASIN");
                        rt.Idler(ph, rt.MY("press_" + i), "phone", "idle_look", "idle_wait");
                        crew.crew.Add(ph);
                    }
                    string[] guests = { "gala_f", "gala_m", "gala_f2", "businessman", "guest_f_b", "guest_f_a" };
                    var forecourt = new List<NPC>(crew.crew);
                    for (int i = 0; i < 6; i++)
                    {
                        var gu = rt.Spawn(guests[i], rt.M("gala_guest_" + i), rt.MY("gala_guest_" + i), Role.Civilian, "Davetli");
                        rt.Idler(gu, rt.MY("gala_guest_" + i), "drink", "talk", "idle", "talk2");
                        forecourt.Add(gu);
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        var dm = rt.Spawn("waiter", rt.M("gala_door_" + i), 90f, Role.Worker, "Kapı Görevlisi", "GÖREVLİ");
                        rt.Idler(dm, 90f, "idle_wait", "idle", "idle_look");
                        var po = rt.Spawn("police_m", rt.M("gala_police_" + i), rt.MY("gala_police_" + i), Role.Guard, "Polis", "POLİS", "Silahlı — cesedi görürse telsize uzanır");
                        rt.Idler(po, rt.MY("gala_police_" + i), "guard_pose", "idle_look");
                        forecourt.Add(dm);
                    }
                    Sfx.SetCrowd(0.22f);

                    // the body double + his bodyguard in the limousine, the real Aydemir + his wife + a bodyguard in the sedan
                    // each rides where his car is parked (the HUD marks a hidden target where he really is)
                    var limoGo = Level.I.Tagged("limo");
                    var sedanGo = Level.I.Tagged("gala_sedan");
                    var limoAt = limoGo != null ? limoGo.transform.position : arrive[0];
                    var sedanAt = sedanGo != null ? sedanGo.transform.position : arrive[0];
                    var dbl = rt.Spawn("gala_m2", limoAt, 270f, Role.Decoy, "Cevdet Aydemir (?)", "SİVİL", "Aydemir'in dublörü — oyuncu");
                    dbl.ScanNote = "DUBLÖR — bu Aydemir değil!";
                    var dg = rt.Spawn("bodyguard_b", limoAt, 270f, Role.Bodyguard, "Koruma");
                    var t = rt.AddTarget("gala_m2", sedanAt, 270f);
                    var wife = rt.Spawn("gala_f", sedanAt, 270f, Role.Civilian, "Leyla Aydemir", "SİVİL", "Aydemir'in eşi — kırmızı elbise");
                    var tg = rt.Spawn("bodyguard_a", sedanAt, 270f, Role.Bodyguard, "Koruma");
                    foreach (var n in new[] { dbl, dg, t, wife, tg }) n.SetHidden(true);
                    rt.AttachToProp(dbl, "limo"); rt.AttachToProp(dg, "limo");
                    rt.AttachToProp(t, "gala_sedan"); rt.AttachToProp(wife, "gala_sedan"); rt.AttachToProp(tg, "gala_sedan");
                    t.MarkWhenHidden = true;
                    t.HiddenStatus = "Sedanda — operaya geliyor";
                    t.HideOnFlee = true;
                    t.FleePath = up;
                    t.EscapeText = "Aydemir operaya kaçtı";
                    t.EscapeFailDelay = 2.4f;
                    bool onCarpet = false, sceneBroken = false;
                    rt.When(() => { if (!t.Dead) return false; return true; }, () => { if (onCarpet) rt.SetFlag("carpet"); });
                    // anyone on the forecourt dead or running: the convoy will not stop
                    rt.When(() => rt.Alarm || forecourt.Exists(n => n != null && (n.Dead || n.Panicking)) || dbl.Dead || dbl.Panicking, () => sceneBroken = true);

                    System.Action<NPC, NPC, NPC, float, bool> stepOut = null;
                    stepOut = (vip, partner, guard, pose, real) =>
                    {
                        var carTag = real ? "gala_sedan" : "limo";
                        foreach (var n in new[] { vip, partner, guard })
                        {
                            if (n == null) continue;
                            rt.DetachFromProp(n);
                        }
                        vip.transform.SetPositionAndRotation(walk[0], Quaternion.Euler(0f, 270f, 0f));
                        vip.SetHidden(false);
                        vip.MarkWhenHidden = false;
                        vip.HiddenStatus = null;
                        var plan = new List<Act>
                        {
                            Act.Do(() => onCarpet = real), Act.Move(walk, "walk_strut", 0.9f),
                            Act.IdleFacing("wave", 80f, 3.2f), Act.IdleFacing("idle", 60f, pose * 0.5f), Act.IdleFacing("bow", 100f, 2.0f),
                            Act.Move(up, "walk_confident", 0.9f), Act.Do(() => onCarpet = false), Act.Hide()
                        };
                        if (real)
                        {
                            // inside for the first act, then the balcony at the interval, then he is gone for the night
                            plan.Add(Act.Idle("idle", 16f));
                            plan.Add(Act.Do(() =>
                            {
                                if (!rt.IsPlaying || vip.Dead) return;
                                vip.transform.SetPositionAndRotation(rt.M("opera_balcony_door"), Quaternion.Euler(0f, 90f, 0f));
                                vip.SetHidden(false);
                                vip.FleePath = new[] { rt.M("opera_balcony_door") };
                                vip.EscapeText = "Aydemir salona döndü — fırsat kaçtı";
                                rt.Say("Aydemir balkona çıktı!", Theme.Danger, "opera");
                            }));
                            plan.Add(Act.Move(new[] { rt.M("opera_balcony") }, "walk"));
                            plan.Add(Act.IdleFacing("smoke", 90f, 9f));
                            plan.Add(Act.IdleFacing("talk", 160f, 7f));
                            plan.Add(Act.IdleFacing("drink", 90f, 6f));
                            plan.Add(Act.Move(new[] { rt.M("opera_balcony_door") }, "walk"));
                            plan.Add(Act.Escape());
                        }
                        vip.SetPlan(plan);
                        if (partner != null)
                        {
                            partner.transform.SetPositionAndRotation(walk[0] + V(0.3f, 0f, -0.9f), Quaternion.Euler(0f, 270f, 0f));
                            partner.SetHidden(false);
                            var pp = new List<Act>
                            {
                                Act.Move(MissionRuntime.Offset(walk, -0.8f), "walk"), Act.IdleFacing("wave", 80f, 3.2f), Act.IdleFacing("idle", 60f, pose * 0.5f),
                                Act.IdleFacing("talk", 100f, 2.0f), Act.Move(MissionRuntime.Offset(up, -0.8f), "walk"), Act.Hide()
                            };
                            if (real)
                            {
                                pp.Add(Act.Idle("idle", 16.4f));
                                pp.Add(Act.Do(() =>
                                {
                                    if (!rt.IsPlaying || partner.Dead) return;
                                    partner.transform.SetPositionAndRotation(rt.M("opera_balcony_door"), Quaternion.Euler(0f, 90f, 0f));
                                    partner.SetHidden(false);
                                }));
                                pp.Add(Act.Move(new[] { rt.M("opera_balcony2") }, "walk"));
                                pp.Add(Act.IdleFacing("talk", 20f, 16f));
                                pp.Add(Act.IdleFacing("drink", 90f, 6f));
                                pp.Add(Act.Move(new[] { rt.M("opera_balcony_door") }, "walk"));
                                pp.Add(Act.Hide());
                            }
                            partner.SetPlan(pp);
                        }
                        if (guard != null)
                        {
                            guard.transform.SetPositionAndRotation(walk[0] + V(0f, 0f, 1.2f), Quaternion.Euler(0f, 270f, 0f));
                            guard.SetHidden(false);
                            guard.SetPlan(Act.Move(MissionRuntime.Offset(walk, 1.3f), "walk"), Act.IdleFacing("guard_pose", 80f, pose + 5.2f),
                                          Act.Move(MissionRuntime.Offset(up, 1.4f), "walk"), Act.Hide());
                        }
                        crew.On = true;
                        crew.rate = real ? 4f : 3f;
                        Sfx.SetCrowd(0.45f);
                        rt.After(pose + 7f, () => { crew.rate = 0.6f; Sfx.SetCrowd(0.22f); });
                        // the car moves on once the doors are shut
                        rt.After(7f, () => rt.MoveProp(carTag, rt.P("limo_out"), 9f, false));
                    };

                    // 1) the limousine with the double (V3 device test: the cars kept the player waiting — the limo leaves at
                    //    once and a little quicker, the sedan follows while the double is still on the carpet)
                    rt.At(0.5f, () =>
                    {
                        var m = rt.MoveProp("limo", arrive, 14f, false);
                        if (m == null) { stepOut(dbl, null, dg, 9f, false); return; }
                        m.Arrived = () =>
                        {
                            if (!rt.IsPlaying) return;
                            stepOut(dbl, null, dg, 9f, false);
                            rt.Cutaway(() => dbl.ChestPoint, V(-76.5f, 3.8f, 50.5f), V(-77.5f, 3.4f, 55.5f), 3.8f, "CANLI  ·  OPERA GİRİŞİ");
                        };
                    });
                    // 2) the sedan with the real Aydemir (does not stop if the scene is already a crime scene)
                    rt.At(8f, () =>
                    {
                        var m = rt.MoveProp("gala_sedan", Join(new[] { sedanAt }, arrive), 15f, false);
                        rt.Radio("KONTROL", "Siyah bir sedan bulvara girdi, operaya geliyor. Kim olduğunu taramadan ateş etme.", 4f);
                        System.Action arrived = () =>
                        {
                            if (!rt.IsPlaying || t.Dead) return;
                            if (sceneBroken)
                            {
                                t.EscapeText = "Aydemir'in arabası durmadan geçti";
                                t.EscapeFailDelay = 3.5f;
                                rt.MoveProp("gala_sedan", rt.P("limo_out"), 12f, false);
                                rt.After(2.5f, () => { if (!t.Dead && !t.Escaped) t.ForceEscape(); });
                                return;
                            }
                            stepOut(t, wife, tg, 6f, true);
                        };
                        if (m != null && m.Running) m.Arrived = arrived;
                        else arrived();
                    });
                    // the double has gone in (seen on the carpet, now inside): the handler says what he was
                    bool dblSeen = false;
                    rt.When(() => { if (!dbl.IsHidden) dblSeen = true; return dblSeen && dbl.IsHidden; }, () =>
                    {
                        if (!dbl.Dead && !t.Dead && !onCarpet) rt.Radio("KONTROL", "İçeri giren dublördü. Asıl adam birazdan geliyor — sabır.", 4f);
                    });
                    CityLife(rt, "night", "gala_m2", "gala_f");
                    Traffic(rt, false, true);
                    Trams(rt, 12f, 44f);
                }
            }.Target("gala_m2", "Cevdet Aydemir", "Armatör", "Kır saç, bıyık, gri smokin ve papyon. Eşi kırmızı elbiseli.")
             .Goal("Cevdet Aydemir'i gala gecesinde etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.Custom, "Kırmızı halıda vur", 0, r => r.Flag("carpet"))
             .Star(ChallengeKind.NoCivilians, "Dublöre ve davetlilere dokunma"));

            // ------------------------------------------------------------------ 16. the rally (day): protect the prosecutor from two killers
            list.Add(new MissionDef
            {
                id = "m16", number = 16, region = "city", title = "MİTİNG", location = "Saat Kulesi Meydanı",
                preset = "day", wind = V(2.4f, 0, -0.6f), reward = 11000, focus = "podium", icon = "podium",
                kind = "KORUMA", clock = "11:30",
                introLine = "Başsavcı bugün Baran hakkında iddianameyi açıklıyor. Onu öldürmek isteyen tek kişi değiliz bu sefer — onu korumak bizim işimiz.",
                brief = "Başsavcı Nermin Arslan, meydandaki halk buluşmasında Baran'a açılan davayı anlatacak. Baran onu konuşturmamak için iki kişi tuttu: meydanı yukarıdan gören bir keskin nişancı ve kalabalığa karışmış silahlı biri.\n\nNişancıyı önce bul. Dikkat: bir atış yaparsan namlu alevini görür ve sana döner. Savcı konuşmasını bitirip sahneden inene kadar hayatta kalmalı.",
                tip = "Keskin nişancının dürbünü güneşi yakaladıkça parlar. Sana nişan alırsa kırmızı halka dolmadan onu indir. Kalabalıktaki adam silah çekince kolundan vurursan silahını düşürür.",
                gearHint = "Susturuculu tüfekle nişancı seni daha geç fark eder",
                introNote = "İstihbarat: meydanı yukarıdan gören bir nişancı ve kalabalıkta silahlı biri var",
                maxCivilians = 1,
                setup = rt =>
                {
                    rt.WanderIdlers = false;                          // a packed square
                    foreach (var tag in new[] { "stage", "stage_podium", "stage_spk", "stage_cam" }) Level.I.ShowTagged(tag, true);
                    var p = rt.AddProtected("prosecutor", rt.M("stage_right"), 200f, "Nermin Arslan", "BAŞSAVCI", "Başsavcı — konuşması bitene kadar koru");
                    p.HideOnFlee = true;
                    p.CanFlee = false;                 // a shot near her: she ducks behind the podium, she does not leave the stage
                    bool speechDone = false;
                    NPC rv = null, gm = null;
                    System.Func<NPC, bool> stopped = h => h == null || h.Dead || h.Sedated || h.Down || (h.Wounded && h.WoundZone == BodyZone.Arm);
                    System.Func<bool> threatsOver = () => rv != null && gm != null && stopped(rv) && (stopped(gm) || gm.Escaped);
                    rt.Protect(p, "Başsavcıyı konuşması bitene kadar koru", () => speechDone || threatsOver());
                    var podium = rt.M("podium");
                    p.SetPlan(Act.IdleFacing("wave_big", 190f, 3.5f), Act.Move(new[] { podium }, "walk"),
                              Act.Do(() => { Sfx.Play(Sfx.Applause, 0.55f); Sfx.SetCrowd(0.5f); }),
                              Act.IdleFacing("talk_passion", 180f, 9f), Act.IdleFacing("talk_open", 180f, 7f), Act.IdleFacing("shout", 180f, 4f),
                              Act.IdleFacing("talk", 180f, 8f), Act.IdleFacing("talk_passion", 170f, 9f), Act.IdleFacing("talk_open", 190f, 8f),
                              Act.IdleFacing("shout", 180f, 4f), Act.IdleFacing("talk", 180f, 9f), Act.IdleFacing("talk_passion", 180f, 8f),
                              Act.Do(() => { Sfx.Play(Sfx.Applause, 0.6f); Sfx.Play(Sfx.Cheer, 0.4f); }),
                              Act.IdleFacing("wave_big", 180f, 2.5f), Act.Do(() => speechDone = true),
                              Act.IdleFacing("wave_big", 180f, 2f), Act.Move(rt.P("rally_exit"), "walk"), Act.Escape());
                    var guard = rt.Spawn("bodyguard_a", rt.M("rally_guard"), rt.MY("rally_guard"), Role.Worker, "Savcının Koruması", "KORUMA", "Savcının koruması — ateş etme");
                    rt.Idler(guard, rt.MY("rally_guard"), "guard_pose", "idle_look");
                    for (int i = 0; i < 2; i++)
                    {
                        var po = rt.Spawn("police_m", rt.M("rally_police_" + i), 180f, Role.Worker, "Polis", "POLİS", "Güvenlik polisi — ateş etme");
                        rt.Idler(po, 180f, "guard_pose", "idle_look", "idle_arms");
                    }
                    var camGo = Level.I.Tagged("stage_cam");
                    var camAt = camGo != null ? camGo.transform.position : podium + V(-3f, -1.6f, -12.5f);
                    var cam = rt.Spawn("photog", camAt + V(0f, 0f, -0.9f), 0f, Role.Civilian, "Kameraman", "BASIN");
                    rt.Idler(cam, 0f, "idle_wait", "phone");

                    // the crowd, facing the stage
                    string[] men = { "civilian_m", "guest_m", "student_m", "elder_m", "worker_a", "worker_b", "businessman", "runner" };
                    string[] women = { "civilian_f", "guest_f_a", "guest_f_b", "guest_f_c", "tourist_f", "office_f" };
                    var rnd = new System.Random(1616);
                    const int gunSpot = 26;                      // east end of the third row
                    for (int i = 0; i < 28; i++)
                    {
                        if (i == gunSpot) continue;
                        string ch = rnd.NextDouble() < 0.55 ? men[rnd.Next(men.Length)] : women[rnd.Next(women.Length)];
                        var c = rt.Spawn(ch, rt.M("crowd_" + i), 0f, Role.Civilian, "Dinleyici");
                        float yaw = (float)(rnd.NextDouble() * 24.0 - 12.0);
                        rt.Idler(c, yaw, "cheer", "idle", "cheer2", "idle_look", "cheer_up", "wave_big", "idle_shift");
                    }

                    // threat 1: the marksman kneeling in the bell tower's north-east arch, rifle over the sill towards the stage
                    rv = rt.AddHitman("pilot", rt.M("rival_spot"), rt.MY("rival_spot"), "Kuledeki Nişancı", "Siyah gömlek, güneş gözlüğü — saat kulesinin çan katında");
                    rv.CanFlee = false;
                    rv.SightRange = 0f;
                    var mk = new Marksman(rt, rv, "rifle_kneel", podium + V(0f, -0.6f, -12f), 26f, false);
                    // threat 2: the gunman in the crowd
                    gm = rt.AddHitman("knifeman", rt.M("crowd_" + gunSpot), 0f, "Kalabalıktaki Adam", "Siyah kapüşonlu, kırmızı bere, sakallı");
                    rt.Idler(gm, 0f, "idle", "idle_look", "cheer");
                    var bonus = rt.Custom("Nişancıyı ve silahlıyı durdur", () => stopped(rv) && stopped(gm), null, null, true);
                    bonus.liveText = () => "Nişancıyı ve silahlıyı durdur  " + ((stopped(rv) ? 1 : 0) + (stopped(gm) ? 1 : 0)) + "/2";

                    System.Action<NPC> rifleShot = n =>
                    {
                        if (!rt.IsPlaying || p.Dead) return;
                        mk.Fire(p.ChestPoint);
                        if (!p.Dead) p.Kill(p.ChestPoint, (p.ChestPoint - mk.Muzzle).normalized * 2f, null, false, false);
                        rt.Noise(p.transform.position, 45f, NoiseKind.Explosion);
                    };
                    bool gunGo = false;
                    System.Action gunman = () =>
                    {
                        if (gunGo || stopped(gm) || gm.Panicking || gm.Escaped || p.Dead || p.Escaped) return;
                        gunGo = true;
                        rt.Radio("KONTROL", "Kalabalıkta biri öne doğru itiliyor — silahı var!", 3.5f);
                        // round the edge of the crowd, then to the front
                        gm.Attack(p, new[] { V(23.2f, SW, 79.2f), V(23.6f, SW, 89.0f), rt.M("rally_gun") }, "walk", 4.5f, rt.AttackerFires);
                    };
                    // the marksman takes aim at the podium a while into the speech (as soon as he is free: not mid-duel with you)
                    rt.When(() => rt.Elapsed >= 43f && mk.Free && !p.Dead && !p.Escaped && !speechDone, () =>
                    {
                        mk.AimAt(p, 9f, rifleShot);
                        rt.Radio("KONTROL", "Bir dürbün parladı! Savcıya nişan alıyorlar — bul onu!", 3.5f);
                    });
                    rt.At(62f, gunman);
                    rt.When(() => stopped(rv), () => rt.After(2.5f, gunman));        // plan B: their sniper is down
                    mk.WatchPlayerShots("Kuledeki nişancı seni vurdu");
                    rt.When(() => stopped(rv) && stopped(gm), () => rt.SetFlag("both"));
                    rt.RadioAt(6f, "KONTROL", "Meydanı tepeden gören her yere bak. Bir nişancı varsa dürbünü güneşte parlar.", 4.5f);
                    Sfx.SetCrowd(0.35f);
                    CityLife(rt, "rally", "prosecutor", "knifeman", "pilot");
                    Traffic(rt, true, true);
                    Trams(rt, 20f, 50f);
                }
            }.Target("prosecutor", "Nermin Arslan", "Başsavcı", "Gri takım, küt kesim saç. Sahnede, kürsüde konuşuyor.")
             .Goal("Başsavcıyı konuşması bitene kadar koru")
             .Goal("Nişancıyı ve kalabalıktaki silahlıyı bul ve durdur (bonus)")
             .Star(ChallengeKind.Complete, "Başsavcıyı koru")
             .Star(ChallengeKind.Custom, "Kimse nişan alamadan ikisini de durdur", 0, r => r.Flag("both") && !r.Flag("aimed") && !r.Flag("spotted"))
             .Star(ChallengeKind.NoCivilians, "Kalabalığa zarar verme"));

            // ------------------------------------------------------------------ 17. the pier (morning): take the bookkeeper alive
            list.Add(new MissionDef
            {
                id = "m17", number = 17, region = "city", title = "İSKELE", location = "Kordon vapur iskelesi",
                preset = "day", wind = V(-3.0f, 0, 1.6f), reward = 11500, focus = "pier_root", icon = "briefcase",
                kind = "CANLI YAKALA", clock = "08:15",
                introLine = "Muhasebeci sabah vapurundan inecek. Müşteri onu canlı istiyor: bacağından.",
                brief = "Ertan Koç, Baran'ın bütün hesaplarını bilen muhasebeci. Sabah vapuruyla şehre geliyor, iskeleden yürüyüp taksiye binecek ve havalimanına gidecek.\n\nMüşteri onu CANLI istiyor: bacağından vur, taksi durağında bekleyen ekibimiz koşup onu alsın. Baran da onu izletiyor: muhasebeci yere düştüğü an susturmak için birini gönderecek.",
                tip = "Vapurdan inen kalabalıkta herkesi tara. Muhasebeci yere düşünce Baran'ın adamı motosikletle promenattan gelir: silah çekmeden önce onu durdur.",
                setup = rt =>
                {
                    // already on its approach when the contract starts (the passengers are off within half a minute)
                    // (V3 device test: half a minute of watching the ferry come in) already well into the bay, moored in ~12 s
                    var ferryIn0 = rt.P("ferry_in");
                    var ferryIn = ferryIn0;
                    if (ferryIn0.Length >= 3)
                    {
                        var rest = new Vector3[ferryIn0.Length - 2];
                        Array.Copy(ferryIn0, 2, rest, 0, rest.Length);
                        ferryIn = Join(new[] { Vector3.Lerp(ferryIn0[1], ferryIn0[2], 0.55f) }, rest);
                    }
                    var ferry = rt.MoveProp("ferry", ferryIn, 10f, false);
                    var gang = rt.M("ferry_gangway");
                    var pierWalk = rt.P("pier_walk");
                    var toTaxi = rt.P("to_taxi");
                    var acct = rt.AddTarget("accountant", gang, 270f);
                    acct.NoCrawl = true;
                    acct.HideOnFlee = true;
                    acct.FleePath = Join(new[] { pierWalk[pierWalk.Length - 1] }, toTaxi);
                    acct.FleeAlong = Join(pierWalk, toTaxi);
                    acct.EscapeText = "Muhasebeci taksiyle kaçtı";
                    acct.EscapeFailDelay = 3.6f;
                    var wound = rt.MustWound(acct, BodyZone.Leg, "Muhasebeciyi BACAĞINDAN vur — ölmemeli");
                    bool teamHere = false;
                    var keep = rt.Protect(acct, "Ekip gelene kadar onu hayatta tut", () => teamHere);
                    keep.hidden = true;

                    // the passengers: hidden aboard until the ferry is moored, then down the pier one after the other
                    string[] pax = { "civilian_m", "office_f", "accountant", "student_m", "elder_m", "guest_f_a", "worker_b", "tourist_f", "guest_m", "civilian_f", "runner" };
                    var rnd = new System.Random(1717);
                    var people = new List<NPC>();
                    for (int i = 0; i < pax.Length; i++)
                    {
                        var n = pax[i] == "accountant" ? acct : rt.Spawn(pax[i], gang, 270f, Role.Civilian, "Yolcu");
                        n.SetHidden(true);
                        people.Add(n);
                    }
                    acct.MarkWhenHidden = true;
                    acct.HiddenStatus = "Vapurda — iskeleye yanaşıyor";
                    var tails = new[] { "east", "west", "acct", "taxi", "tram", "east", "west", "east", "tram", "west", "east" };
                    System.Action disembark = () =>
                    {
                        Sfx.PlayAt(Sfx.Horn, gang, 0.9f);
                        for (int i = 0; i < people.Count; i++)
                        {
                            var n = people[i];
                            float delay = 1.5f + i * 1.35f + (float)rnd.NextDouble() * 0.5f;
                            var acts = new List<Act> { Act.Idle("idle", delay), Act.Do(() => { n.SetHidden(false); n.MarkWhenHidden = false; n.HiddenStatus = null; }) };
                            if (n == acct)
                            {
                                acts.Add(Act.Move(pierWalk, "walk_carry", 0.95f));
                                acts.Add(Act.IdleFacing("idle_look", 60f, 2.5f));
                                acts.Add(Act.IdleFacing("phone_conv", 90f, 5f));
                                acts.Add(Act.Move(toTaxi, "walk_carry", 0.95f));
                                acts.Add(Act.IdleFacing("idle_look", 0f, 1.2f));
                                acts.Add(Act.Escape());
                            }
                            else
                            {
                                acts.Add(Act.Move(pierWalk, "walk", 0.9f + (float)rnd.NextDouble() * 0.25f));
                                switch (tails[i])
                                {
                                    case "east": acts.Add(Act.Move(new[] { V(4f, SW, 136f), V(330f, SW, 136f + (float)rnd.NextDouble() * 6f) }, "walk")); break;
                                    case "west": acts.Add(Act.Move(new[] { V(-4f, SW, 137f), V(-330f, SW, 136f + (float)rnd.NextDouble() * 6f) }, "walk")); break;
                                    case "taxi": acts.Add(Act.Move(Join(toTaxi, new[] { V(80.5f, SW, 120f) }), "walk")); break;
                                    default: acts.Add(Act.Move(new[] { V(-12f, SW, 117f), V(-31.8f, SW, 116.4f), V(-31.8f, 0.35f, 120.6f), V(-38f, 0.35f, 120.8f) }, "walk")); acts.Add(Act.IdleFacing("idle_wait", 0f, 0f)); break;
                                }
                                if (tails[i] != "tram") acts.Add(Act.Hide());
                            }
                            n.SetPlan(acts);
                        }
                    };
                    if (ferry != null && ferry.Running)
                    {
                        ferry.Arrived = disembark;
                        rt.At(9f, () => rt.Cutaway(() => Level.I.Tagged("ferry") != null ? Level.I.Tagged("ferry").transform.position + V(0f, 6f, 0f) : gang,
                                                     V(-11f, 7f, 152f), V(-8f, 6.2f, 160f), 4.2f, "CANLI  ·  VAPUR İSKELESİ"));
                    }
                    else disembark();
                    // the taxi he walks to: off it goes when he gets in
                    acct.OnArrivedEscape += n =>
                    {
                        rt.AttachToProp(n, "taxi");
                        rt.MoveProp("taxi", rt.P("taxi_out"), 10f, false);
                    };

                    // our team at the taxi rank, Baran's cleaner on a scooter
                    var agents = new List<NPC>();
                    for (int i = 0; i < 2; i++)
                    {
                        var a = rt.Spawn("police_m", rt.M("agent_" + i), rt.MY("agent_" + i), Role.Worker, "Ajan", "EKİP", "Bizim ekip — ateş etme");
                        rt.Idler(a, rt.MY("agent_" + i), "idle_wait", "phone", "idle_look");
                        agents.Add(a);
                    }
                    var hit = rt.AddHitman("smuggler", rt.M("hit_scooter_stop"), 90f, "Temizlikçi", "Gri tişört, kırmızı bere — Baran'ın tetikçisi");
                    hit.SetHidden(true);
                    hit.CanFlee = false;
                    // and a second one, waiting on the tram platform like everybody else (a careful scan finds his gun early)
                    var hit2 = rt.AddHitman("guest_m", rt.M("tram_platform_n") + V(4f, 0f, 0f), 180f, "Bekleyen Adam", "Gri ceket — tramvay durağında, silahlı");
                    rt.Idler(hit2, 180f, "phone", "idle_wait", "idle_look");
                    System.Func<NPC, bool> stopped = h => h == null || h.Dead || h.Sedated || h.Down || (h.Wounded && h.WoundZone == BodyZone.Arm);
                    var bonus = rt.Custom("Baran'ın adamlarını durdur", () => stopped(hit) && stopped(hit2), null, null, true);
                    bonus.liveText = () => "Baran'ın adamlarını durdur  " + ((stopped(hit) ? 1 : 0) + (stopped(hit2) ? 1 : 0)) + "/2";
                    bonus.hidden = true;

                    bool started = false;
                    System.Action team = () =>
                    {
                        if (started || acct.Dead || !rt.IsPlaying) return;
                        started = true;
                        rt.Reveal(keep);
                        rt.Reveal(bonus);
                        // the agents run to him
                        Vector3 body = acct.transform.position;
                        float runLeft = 0f;
                        int side = 0;
                        var routes = new List<Vector3[]>();
                        foreach (var a in agents)
                        {
                            var near = body + (side++ == 0 ? V(0.9f, 0f, -0.6f) : V(-0.9f, 0f, -0.6f));
                            // straight to him when nothing is in the way (he fell near the taxi rank), else round by the promenade
                            var route = Level.WalkableLine(a.transform.position, near, out _)
                                ? new[] { a.transform.position, near }
                                : new[] { a.transform.position, V(60f, SW, 109f), V(24f, SW, 114.5f), V(6f, SW, 118f), V(Mathf.Clamp(body.x, -6f, 6f), SW, Mathf.Max(body.z - 1.2f, 118.5f)), near };
                            a.MarkAlerted();
                            a.SetPlan(Act.Move(route, "run", 0.95f), Act.IdleFacing("duck", MissionRuntime.Yaw(near, body), 0f));
                            routes.Add(route);
                            float len = 0f;
                            for (int i = 1; i < route.Length; i++) len += Vector3.Distance(route[i - 1], route[i]);
                            runLeft = Mathf.Max(runLeft, len);
                        }
                        float t0 = rt.Elapsed;
                        float eta = runLeft / 3.0f + 2.5f;
                        rt.Countdown("Ekibin ulaşmasına", () => teamHere ? 0f : Mathf.Max(0.1f, eta - (rt.Elapsed - t0)));
                        rt.Radio("KONTROL", "Muhasebeci yerde! Ekip koşuyor — " + Mathf.RoundToInt(eta) + " saniye. Baran'ın adamı da yolda!", 4.5f);
                        // the man on the platform makes his move (and keeps at it while he can)
                        System.Action goHit2 = () =>
                        {
                            if (!rt.IsPlaying || teamHere || acct.Dead || stopped(hit2) || hit2.Panicking || hit2.Escaped || hit2.Aiming) return;
                            var bp = acct.transform.position;
                            var from = hit2.transform.position;
                            var dd = from - bp; dd.y = 0f;
                            hit2.EndInvestigation();
                            if (dd.magnitude <= 10.5f) { hit2.BeginAim(acct, 4.5f, rt.AttackerFires); return; }
                            var stand = bp + (dd.sqrMagnitude > 1f ? dd.normalized : Vector3.left) * 9f;
                            if (Level.WalkableLine(from, stand, out float sy)) { hit2.Attack(acct, new[] { new Vector3(stand.x, sy, stand.z) }, "walk_confident", 4.5f, rt.AttackerFires); return; }
                            // round by the scooter stop on the promenade
                            var via = rt.M("hit_scooter_stop");
                            var vd = via - bp; vd.y = 0f;
                            var stand2 = bp + (vd.sqrMagnitude > 1f ? vd.normalized : Vector3.left) * 9f;
                            if (Level.WalkableLine(from, via, out _) && Level.WalkableLine(via, stand2, out float s2))
                            {
                                hit2.Attack(acct, new[] { via, new Vector3(stand2.x, s2, stand2.z) }, "walk_confident", 4.5f, rt.AttackerFires);
                                return;
                            }
                            if (dd.magnitude < 30f) hit2.BeginAim(acct, 4.5f, rt.AttackerFires);
                        };
                        rt.After(8f, () =>
                        {
                            if (!rt.IsPlaying || teamHere || acct.Dead || stopped(hit2) || hit2.Panicking || hit2.Escaped) return;
                            rt.Radio("KONTROL", "Tramvay durağındaki gri ceketli de Baran'ın adamı — muhasebeciye gidiyor!", 3.5f);
                            goHit2();
                            System.Action again = null;
                            again = () =>
                            {
                                if (!rt.IsPlaying || teamHere || acct.Dead || stopped(hit2) || hit2.Panicking || hit2.Escaped) return;
                                if (!hit2.Aiming && !hit2.Moving) goHit2();
                                rt.After(5f, again);
                            };
                            rt.After(5f, again);
                        });
                        // both out of it (stopped, or run off): the agents sprint the rest of the way
                        System.Func<NPC, bool> gone = h => stopped(h) || h.Escaped || h.Panicking;
                        rt.When(() => teamHere || acct.Dead || (gone(hit) && gone(hit2)), () =>
                        {
                            if (teamHere || acct.Dead || !rt.IsPlaying) return;
                            float rest = 0f;
                            for (int i = 0; i < agents.Count && i < routes.Count; i++)
                            {
                                var a = agents[i];
                                if (a == null || a.Incapacitated) continue;
                                var r = MissionRuntime.From(routes[i], a.transform.position);
                                float len = 0f;
                                var prev = a.transform.position;
                                foreach (var q in r) { len += Vector3.Distance(prev, q); prev = q; }
                                rest = Mathf.Max(rest, len);
                                a.SetPlan(Act.Move(r, "run", 1.45f), Act.IdleFacing("duck", MissionRuntime.Yaw(r[r.Length - 1], acct.transform.position), 0f));
                            }
                            t0 = rt.Elapsed;
                            eta = rest / Mathf.Max(1f, agents[0].ClipSpeed("run") * 1.45f) + 2f;
                            rt.Radio("KONTROL", "Tehlike geçti — ekip koşuyor!", 2.5f);
                        });
                        // the cleaner: rides in along the promenade, gets off and draws
                        Level.I.ShowTagged("hit_scooter", true);
                        var ride = rt.P("scooter_in");
                        var mv = rt.MoveProp("hit_scooter", ride, 11f, false);
                        var sc = Level.I.Tagged("hit_scooter");
                        if (sc != null)
                        {
                            hit.transform.SetPositionAndRotation(sc.transform.TransformPoint(new Vector3(0f, 0.15f, -0.1f)), sc.transform.rotation);
                            rt.AttachToProp(hit, "hit_scooter");
                        }
                        hit.SetHidden(false);
                        hit.SetPlan(Act.Idle("sit_idle", 0f));
                        System.Action getOff = () =>
                        {
                            if (!rt.IsPlaying || stopped(hit) || hit.Panicking || acct.Dead || teamHere) return;
                            rt.DetachFromProp(hit);
                            hit.transform.SetPositionAndRotation(rt.M("hit_scooter_stop") + V(0.9f, 0f, 0f), Quaternion.Euler(0f, 60f, 0f));
                            hit.Attack(acct, new[] { rt.M("hit_pier") }, "run", 4.5f, rt.AttackerFires);
                        };
                        if (mv != null && mv.Running) mv.Arrived = getOff; else getOff();
                        // they reach him: he is taken away
                        rt.When(() =>
                        {
                            if (teamHere || acct.Dead) return false;
                            foreach (var a in agents)
                                if (a != null && !a.Incapacitated && Vector3.Distance(a.transform.position, acct.transform.position) < 2.2f) return true;
                            return false;
                        }, () => rt.After(1.6f, () =>
                        {
                            if (!rt.IsPlaying || acct.Dead) return;
                            teamHere = true;
                            rt.ClearCountdown();
                            hit.StopAiming();
                            hit2.StopAiming();
                            acct.SetHidden(true);
                            foreach (var a in agents) if (a != null && !a.Dead) a.SetHidden(true);
                            rt.Say("Ekip muhasebeciyi aldı!", Theme.Success, "check");
                        }));
                    };
                    acct.OnWound = (n, z) =>
                    {
                        if (z != BodyZone.Leg || started) return;
                        if (rt.Shots == 1) rt.SetFlag("firstLeg");
                        team();
                    };
                    rt.When(() => acct.Sedated, team);
                    rt.RadioAt(8f, "KONTROL", "Vapur yanaşıyor. Muhasebeci elinde siyah bir evrak çantasıyla inecek.", 4f);
                    CityLife(rt, "day", "accountant", "smuggler");
                    Traffic(rt, true, true);
                    TramLine(rt, "tram_e", "tram_east", 26f, 10f, 70f);
                }
            }.Target("accountant", "Ertan Koç", "Muhasebeci", "Kahverengi deri mont, kot pantolon, gözlük. Elinde siyah evrak çantası.")
             .Goal("Muhasebeciyi BACAĞINDAN vur — ölmemeli")
             .Goal("Ekip gelene kadar onu Baran'ın adamlarından koru")
             .Star(ChallengeKind.Complete, "Muhasebeciyi canlı teslim et")
             .Star(ChallengeKind.Custom, "İlk atışta bacağından vur", 0, r => r.Flag("firstLeg"))
             .Star(ChallengeKind.Custom, "Baran'ın adamları nişan alamasın", 0, r => !r.Flag("aimed")));

            // ------------------------------------------------------------------ 18. the meeting in the rain (night): two at once
            list.Add(new MissionDef
            {
                id = "m18", number = 18, region = "city", title = "YAĞMUR", location = "Saat Kulesi Meydanı — çeşme",
                preset = "rain_night", wind = V(3.6f, 0, -1.4f), reward = 12500, focus = "rain_meet_a", icon = "umbrella",
                kind = "ÇİFT HEDEF", clock = "23:20",
                introLine = "İkisi de şemsiye altında. Biri düştüğü an öteki kaçar — ikinci atışın hazır olsun.",
                brief = "Gazeteci Selin Ay'ı öldüren tetikçi Erol Kaya, nam-ı diğer Çakal, bu gece parasını alacak. Parayı getiren, cinayet dosyasını kapatan Komiser Tuncay Er.\n\nYağmurda, çeşmenin başında buluşacaklar. İkisi de bu işin bedelini ödeyecek. Biri vurulduğu an öteki aracına koşar: Komiser'in arabası Plaza Sokak'ta, Çakal'ın motosikleti meydanın köşesinde. Sana yarı otomatik bir tüfek ve termal dürbün veriyoruz.",
                tip = "Şemsiyeler yukarıdan bakınca başı saklar ama kurşunu durdurmaz. Termal dürbün yağmurda vücut ısısını gösterir. Yarı otomatik tüfekle sürgü çekmeden ikinci atışı yaparsın.",
                gearHint = "Ödünç: FIRTINA DMR (yarı otomatik) ve termal dürbün",
                setup = rt =>
                {
                    var A = rt.M("rain_meet_a");
                    var B = rt.M("rain_meet_b");
                    var kom = rt.AddTarget("police_m", A, rt.MY("rain_meet_a"), "Tuncay Er", "Komiser", "Lacivert polo, kep, bıyıklı. Siyah şemsiye.");
                    var cakStart = V(-12f, SW, 18f);
                    var cak = rt.AddTarget("knifeman", cakStart, 0f, "Erol Kaya", "Çakal", "Siyah kapüşonlu, kırmızı bere, sakallı. Kırmızı şemsiye.");
                    UmbrellaHolder.Give(kom, new Color(0.9f, 0.9f, 0.95f));
                    UmbrellaHolder.Give(cak, new Color(3.4f, 1.12f, 1.0f));
                    var komRun = rt.P("komiser_run");
                    var cakRun = rt.P("cakal_run");
                    kom.FleePath = komRun; kom.HideOnFlee = true; kom.FleeEndPause = 0.8f; kom.FleeEndYaw = 90f;
                    kom.EscapeText = "Komiser arabasıyla kaçtı"; kom.EscapeFailDelay = 4f;
                    cak.FleePath = cakRun; cak.HideOnFlee = true; cak.FleeEndPause = 0.6f; cak.FleeEndYaw = 160f;
                    cak.EscapeText = "Çakal motosikletle kaçtı"; cak.EscapeFailDelay = 4f;
                    kom.OnArrivedEscape += n => rt.MoveProp("komiser_car", rt.P("komiser_drive"), 13f, false);
                    cak.OnArrivedEscape += n =>
                    {
                        // he rides away in plain sight (the contract is already lost)
                        var sc = Level.I.Tagged("scooter_e");
                        if (sc != null)
                        {
                            n.transform.SetPositionAndRotation(sc.transform.TransformPoint(new Vector3(0f, 0.15f, -0.1f)), sc.transform.rotation);
                            n.SetHidden(false);
                            n.Play("sit_idle", 0f);
                            rt.AttachToProp(n, "scooter_e");
                        }
                        rt.MoveProp("scooter_e", rt.P("scooter_ride"), 14f, false);
                    };
                    // the meeting: the commissioner waits, Çakal comes across the square, they talk, money changes hands
                    kom.SetPlan(Act.IdleFacing("idle_wait", 217f, 14f), Act.IdleFacing("phone", 300f, 8f), Act.IdleFacing("idle_look", 217f, 0f));
                    var toB = new[] { cakStart, V(-12f, SW, 30f), B };
                    cak.SetPlan(Act.Move(toB, "walk", 1.1f), Act.Do(() => MeetTalk(rt, kom, cak, komRun, cakRun)));
                    float firstDown = -1f;
                    rt.When(() => kom.Dead || cak.Dead, () => firstDown = rt.Elapsed);
                    rt.When(() => kom.Dead && cak.Dead, () => { float fd = firstDown >= 0f ? firstDown : rt.Elapsed; if (rt.Elapsed - fd <= 4.05f) rt.SetFlag("quick"); });
                    rt.When(() => kom.Incapacitated || cak.Incapacitated || kom.Panicking || cak.Panicking || kom.Escaped || cak.Escaped, rt.ClearCountdown);
                    rt.RadioAt(5f, "KONTROL", "Komiser çeşmenin başında bekliyor. Çakal meydanın güneyinden geliyor — kırmızı şemsiyeli.", 4.5f);
                    rt.RadioAt(16f, "KONTROL", "Biri düştüğü an öteki koşar. İkisini de aynı anda görebileceğin yeri bekle.", 4f);
                    CityLife(rt, "rain", "police_m", "knifeman");
                    Traffic(rt, false, true);
                    Trams(rt, 30f, 8f);
                }
            }.Target("knifeman", "Erol Kaya", "Çakal", "Siyah kapüşonlu, kırmızı bere, sakallı. Kırmızı şemsiye.")
             .Target("police_m", "Tuncay Er", "Komiser", "Lacivert polo, kep, bıyıklı. Siyah şemsiye.")
             .Loan("w:dmr", "thermal")
             .Goal("Çakal'ı etkisiz hale getir")
             .Goal("Komiser Tuncay Er'i etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.Custom, "İkisini 4 saniye içinde vur", 0, r => r.Flag("quick"))
             .Star(ChallengeKind.AllHeadshots, "İkisini de kafadan vur"));

            // ------------------------------------------------------------------ 19. the helipad (sunset): the security chief flies out
            list.Add(new MissionDef
            {
                id = "m19", number = 19, region = "city", title = "HELİPAD", location = "Palas Oteli çatısı",
                preset = "sunset", wind = V(-3.8f, 0, 2.2f), reward = 13000, focus = "helipad", icon = "helicopter",
                kind = "SUİKAST", clock = "19:50",
                introLine = "Demirel'i almaya bir helikopter geliyor. Kapısı kapandığı an iş biter.",
                brief = "Tolga Demirel, eski özel kuvvetler subayı, Baran'ın güvenlik şefi. Sana ve ekibimize karşı yürütülen her şeyi o planlıyor. Bu akşam Palas Oteli'nin çatısından helikopterle şehirden çıkacak.\n\nHelikopter piste indikten sonra Demirel iki korumasıyla çatı kapısından çıkıp yürüyecek. Mesafe uzun ve çatıda rüzgar sert. Helikopterin motorunu önceden vurursan yerde kalır: Demirel binaya geri döner — ama o yürüyüş açıkta geçer.",
                tip = "Uzun atışta rüzgar okuna ve kurşun düşüşüne dikkat. Helikopterin gövdesine iki isabet motoru durdurur; patlayıcı mermi tek atışta yeter.",
                setup = rt =>
                {
                    var heli = Level.I.Tagged("heli");
                    Level.I.ShowTagged("heli", true);
                    var rotor = RotorFX.On(heli);
                    if (rotor != null) { rotor.Stop(); rotor.SetSpin(1f, 0.2f); }
                    var inPath = rt.P("heli_in");
                    inPath[0] = Vector3.Lerp(inPath[0], inPath[1], 0.88f);         // already close in over the sea (V3: ~9 s to land)
                    var mv = rt.MoveProp("heli", inPath, 19f, false);
                    var walk = rt.P("roof_walk");
                    var stairs = rt.M("roof_stairs");
                    // his man on the roof: binoculars on the skyline (Demirel knows how this is done) — at the far corner
                    // of the roof, behind the parapet the hotel's marksman uses on the last night
                    var lookAt = Level.I.HasMarker("roof_sniper") ? rt.M("roof_sniper") : (RoofSpot(stairs, rt.NestPoint) ?? stairs);
                    var lk = rt.Spawn("bodyguard_a", lookAt, MissionRuntime.Yaw(lookAt, rt.NestPoint), Role.Guard, "Çatı Gözcüsü", "GÖZCÜ",
                                      "Dürbünle çevredeki çatıları tarıyor");
                    Spotter.Make(lk, MissionRuntime.Yaw(lookAt, rt.NestPoint), 70f, 11f);
                    rt.At(9f, () =>
                    {
                        if (lk.Incapacitated || lk.Lookout == null || lk.Lookout.Reporting || lk.Lookout.Reported) return;
                        rt.Radio("KONTROL", "Otelin çatısının batı köşesinde dürbünlü biri var. Dürbün sana dönünce eğil — ya da önce onu indir.", 4.5f);
                    });
                    var t = rt.AddTarget("demirel", stairs, 330f);
                    var g1 = rt.Spawn("bodyguard_a", stairs + V(0.9f, 0f, -0.6f), 330f, Role.Bodyguard, "Koruma");
                    var g2 = rt.Spawn("bodyguard_b", stairs + V(-0.9f, 0f, -0.6f), 330f, Role.Bodyguard, "Koruma");

                    foreach (var n in new[] { t, g1, g2 }) n.SetHidden(true);
                    t.MarkWhenHidden = true;
                    t.HiddenStatus = "Binada — çatıya çıkacak";
                    t.HideOnFlee = true;
                    t.FleeAlong = walk;                             // a scare: he hurries on to the helicopter
                    t.EscapeText = "Demirel helikopterle kaçtı";
                    t.EscapeFailDelay = 5f;
                    bool grounded = false, lifting = false;

                    System.Action liftOff = () =>
                    {
                        if (lifting || grounded) return;
                        lifting = true;
                        if (rotor != null) rotor.SetSpin(1f, 1.2f);
                        var up = rt.MoveProp("heli", rt.P("heli_out"), 10f, false);
                        if (up != null) up.Delay(1.6f);
                    };
                    t.OnArrivedEscape += n =>
                    {
                        if (grounded) return;              // back into the building (see below): the contract is lost
                        rt.AttachToProp(n, "heli");
                        foreach (var g in new[] { g1, g2 })
                            if (g != null && !g.Incapacitated) { g.SetHidden(true); rt.AttachToProp(g, "heli"); }
                        liftOff();
                    };
                    System.Action comeOut = () =>
                    {
                        if (!rt.IsPlaying || t.Dead) return;
                        t.MarkWhenHidden = false;
                        t.HiddenStatus = null;
                        foreach (var n in new[] { t, g1, g2 }) n.SetHidden(false);
                        rt.Say("Demirel çatıya çıktı!", Theme.Danger, "target");
                        if (grounded)
                        {
                            t.SetPlan(Act.IdleFacing("shout", 210f, 4f), Act.IdleFacing("phone_conv", 200f, 9f), Act.Escape());
                            t.EscapeText = "Demirel binaya geri döndü";
                        }
                        else t.SetPlan(Act.IdleFacing("idle_look", 300f, 1.5f), Act.Move(walk, "walk_confident"), Act.IdleFacing("idle_look", 90f, 0.8f), Act.Escape());
                        g1.SetPlan(Act.IdleFacing("idle_look", 250f, 1.5f), Act.Move(MissionRuntime.Offset(walk, 1.3f), "walk"), Act.IdleFacing("guard_pose", 230f, 0f));
                        g2.SetPlan(Act.IdleFacing("idle_look", 30f, 1.5f), Act.Move(MissionRuntime.Offset(walk, -1.3f), "walk"), Act.IdleFacing("guard_pose", 210f, 0f));
                    };
                    System.Action land = () =>
                    {
                        if (rotor != null && !grounded) rotor.SetSpin(0.7f, 3f);
                        rt.After(3.5f, comeOut);
                        // on the pad, a bullet in the engine keeps it there
                        rt.DisableProp("heli", "engine", "İstersen helikopterin motorunu vur (2 isabet)", 2, (s, pt) =>
                        {
                            if (lifting) return;
                            grounded = true;
                            rt.SetFlag("grounded");
                            if (rotor != null) rotor.SetSpin(0f, 7f);
                            rt.Say("Motor durdu — helikopter yerde kaldı!", Theme.Success, "helicopter");
                            rt.Radio("KONTROL", "Güzel! Helikopter gitmiyor. Demirel binaya dönecek — yürürken yakala!", 4f);
                            t.FleeAlong = MissionRuntime.Rev(walk);
                            t.EscapeText = "Demirel binaya geri döndü";
                            if (!t.Incapacitated && !t.Escaped && !t.IsHidden && !t.Panicking)
                            {
                                t.EndInvestigation();
                                t.SetPlan(Act.IdleFacing("shout", t.YawTo(rt.M("helipad")), 3f), Act.IdleFacing("phone_conv", 300f, 6f),
                                          Act.Move(MissionRuntime.From(MissionRuntime.Rev(walk), t.transform.position), "walk_confident"), Act.Escape());
                            }
                        }).optional = true;
                    };
                    if (mv != null && mv.Running)
                    {
                        mv.Arrived = land;
                        rt.At(6f, () => rt.Cutaway(() => heli != null ? heli.transform.position + V(0f, 2f, 0f) : rt.M("helipad"),
                                                      V(98f, 34f, 66f), V(102f, 32f, 72f), 5f, "CANLI  ·  PALAS HELİPADI"));
                    }
                    else land();
                    rt.RadioAt(1.5f, "KONTROL", "Helikopter denizden geliyor. Demirel inişten sonra çatı kapısından çıkacak.", 4f);
                    CityLife(rt, "evening", "demirel");
                    Traffic(rt, true, true);
                    Trams(rt, 15f, 40f);
                }
            }.Target("demirel", "Tolga Demirel", "Güvenlik Şefi", "Kel, sakallı, güneş gözlüklü. Siyah kruvaze takım.")
             .Goal("Tolga Demirel'i helikoptere binmeden etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.Custom, "Helikopteri havalanmadan yere çak", 0, r => r.Flag("grounded"))
             .Star(ChallengeKind.NoGuards, "Korumalara dokunma"));

            // ------------------------------------------------------------------ 20. the last act (night): Baran in his penthouse
            list.Add(new MissionDef
            {
                id = "m20", number = 20, region = "city", title = "SON PERDE", location = "Palas Oteli — çatı katı", requires = new[] { "nv" },
                preset = "night", wind = V(2.8f, 0, 2.0f), reward = 16000, focus = "pent_3", icon = "boss",
                kind = "SUİKAST", clock = "01:30", pin = "pent_3", pinUp = 3.0f,
                introLine = "Baran biliyor. Çatıda bir nişancısı, bir projektörü ve bekleyen bir helikopteri var. Bu gece bitiyor.",
                brief = "Kenan Baran. Limandaki kaçakçılık, kirli para, satın alınmış polisler, susturulan tanıklar: hepsinin arkasındaki adam. Bu gece Palas Oteli'nin çatı katında, cam duvarların ardında küçük bir parti veriyor.\n\nBaran bizi bekliyor. Çatıda projektörle damları tarayan bir adam ve bir keskin nişancı var: ışık seni bulursa nişancı sana döner. Ateş edersen Baran merdivenden çatıya, bekleyen helikoptere koşar. Bu son iş — temiz bitir.",
                tip = "Önce çatıdaki nişancıyı bul: dürbünü yeşil parlar. Projektör ışığı üzerine gelince nişancı sana nişan alır. Cam duvarlar mermiyi durdurmaz.",
                introNote = "Çatıda projektör ve bir karşı nişancı var — ışık seni bulmadan onu bul",
                setup = rt =>
                {
                    var heli = Level.I.Tagged("heli");
                    Level.I.ShowTagged("heli", true);
                    var rotor = RotorFX.On(heli);
                    if (rotor != null) { rotor.Stop(); rotor.SetSpin(0.45f, 0.5f); }
                    // the party
                    var b = rt.AddTarget("baran", rt.M("pent_3"), 270f);
                    b.HideOnFlee = true;
                    rt.Eliminate(b, "Kenan Baran'ı etkisiz hale getir");      // the rooftop marksman is not part of the contract
                    var stairsIn = rt.M("pent_stairs");
                    int stage = 0;                                          // 0 penthouse, 1 stairs / roof
                    System.Action fleeRoute = null;
                    fleeRoute = () =>
                    {
                        // the way to the private stair from where he stands (round the inner walls of the glass L)
                        var q = b.transform.position;
                        if (q.x > 99.5f && q.z < 75f)
                            b.FleePath = new[] { V(99.5f, 21.95f, 70.5f), V(98.5f, 21.95f, 78.5f), V(95.4f, 21.95f, 82f), V(95.6f, 21.95f, 101f), stairsIn };
                        else if (q.z < 82f)
                            b.FleePath = new[] { V(95.4f, 21.95f, 82f), V(95.6f, 21.95f, 101f), stairsIn };
                        else
                            b.FleePath = new[] { V(95.6f, 21.95f, Mathf.Min(q.z, 101f)), V(95.6f, 21.95f, 101f), stairsIn };
                        if (stage == 0 && !b.Panicking && !b.Dead && !b.Escaped) rt.After(0.5f, fleeRoute);
                    };
                    fleeRoute();                                    // now, and every half second on the contract clock
                    b.EscapeText = "Baran helikopterle kaçtı";
                    b.EscapeFailDelay = 5f;
                    var p1 = rt.M("pent_1");
                    var toSouth = new[] { p1, V(98.5f, 21.95f, 78.5f), V(99.5f, 21.95f, 70.5f), rt.M("pent_4") };
                    b.SetPlan(Act.IdleFacing("talk", 200f, 8f), Act.IdleFacing("drink", 270f, 6f), Act.Move(new[] { rt.M("pent_2") }, "walk_confident"),
                              Act.IdleFacing("talk_passion", 300f, 9f), Act.IdleFacing("cheer", 270f, 4f), Act.Move(new[] { rt.M("pent_3"), p1 }, "walk"),
                              Act.IdleFacing("phone_conv", 200f, 8f), Act.Move(new[] { rt.M("pent_0") }, "walk"), Act.IdleFacing("idle_look", 250f, 6f),
                              Act.Move(new[] { p1 }, "walk"), Act.Move(toSouth, "walk"), Act.IdleFacing("talk", 180f, 8f), Act.IdleFacing("drink", 200f, 5f),
                              Act.Move(MissionRuntime.Rev(toSouth), "walk"), Act.Move(new[] { rt.M("pent_3") }, "walk"), Act.Loop(0));
                    var guests = new[] { ("gala_f", "pent_2", V(0.9f, 0f, 0.4f)), ("gala_f2", "pent_0", V(0.6f, 0f, 1.0f)), ("gala_m", "pent_4", V(-0.9f, 0f, 0.6f)),
                                         ("businessman", "pent_5", V(0.0f, 0f, 0.0f)), ("guest_f_b", "pent_1", V(-0.8f, 0f, -0.8f)) };
                    foreach (var (ch, mk, off) in guests)
                    {
                        var gu = rt.Spawn(ch, rt.M(mk) + off, rt.MY(mk), Role.Civilian, "Davetli");
                        rt.Idler(gu, rt.MY(mk), "drink", "talk", "idle", "talk2", "cheer");
                    }
                    var bar = rt.Spawn("waiter", rt.M("pent_bar"), 270f, Role.Worker, "Barmen", "GÖREVLİ");
                    rt.Idler(bar, 270f, "idle_wait", "drink_stand", "idle");
                    var bg1 = rt.Spawn("bodyguard_a", V(93.6f, 21.95f, 82.6f), 250f, Role.Bodyguard, "Koruma");
                    rt.Idler(bg1, 250f, "guard_pose", "idle_look");
                    var bg2 = rt.Spawn("bodyguard_b", V(96.2f, 21.95f, 98.2f), 290f, Role.Bodyguard, "Koruma");
                    rt.Idler(bg2, 290f, "idle_look", "guard_pose");

                    // the rooftop: searchlight sweeping the skyline, a marksman behind the parapet, the helicopter waiting
                    var lp = rt.M("roof_light");
                    var lamp = rt.Searchlight(lp, 214f, 84f, -4f);
                    if (lamp != null)
                    {
                        lamp.range = 170f;
                        lamp.spotAngle = 9f;
                        lamp.intensity = 5f;
                        var fl = ScopeGlint.Attach(lamp.transform, Vector3.zero, null, false);
                        fl.aim = lamp.transform;
                        fl.strength = 1.4f;
                        fl.sizeMul = 2.6f;
                        fl.tint = new Color(1f, 0.97f, 0.9f);
                    }
                    var sn = rt.AddHitman("guard_a", rt.M("roof_sniper"), rt.MY("roof_sniper"), "Çatıdaki Nişancı", "Palas'ın çatısında, korkuluğun arkasında");
                    sn.CanFlee = false;
                    sn.SightRange = 0f;
                    var mkm = new Marksman(rt, sn, "rifle_aim", rt.NestPoint, 76f, true);   // sweeps the skyline, the nest in the middle
                    mkm.WatchPlayerShots("Çatıdaki nişancı seni vurdu");
                    // the beam: when it passes over the nest the marksman knows where to look
                    float inBeam = 0f;
                    System.Action scan = null;
                    scan = () =>
                    {
                        if (!rt.IsPlaying) return;
                        if (lamp != null && mkm.Free)
                        {
                            var f = lamp.transform.forward; f.y = 0f;
                            var to = rt.NestPoint - lamp.transform.position; to.y = 0f;
                            float ang = Vector3.Angle(f, to);
                            // down behind the parapet the beam passes over him
                            bool exposed = Firefight.I == null || Firefight.I.Exposure > 0f;
                            inBeam = ang < 2.6f && exposed ? inBeam + 0.1f : 0f;
                            if (inBeam >= 0.2f)
                            {
                                inBeam = 0f;
                                rt.Say("PROJEKTÖR ÜZERİNDE — GÖRÜLDÜN!", Theme.Danger, "eye");
                                mkm.HuntPlayer(5.5f, "Çatıdaki nişancı seni vurdu");
                            }
                        }
                        rt.After(0.1f, scan);
                    };
                    rt.At(22f, scan);
                    rt.RadioAt(19f, "KONTROL", "Projektör damları tarıyor, sana yaklaşıyor! Işık seni bulmadan nişancıyı indir.", 4.5f);

                    // Baran runs: up the private stair, out on the roof, across to the helicopter
                    bool grounded = false;
                    var walk = rt.P("roof_walk");
                    var roofDoor = rt.M("roof_stairs");
                    // on the roof: a scare sends him on to the helicopter, or back to the stair door once it is grounded
                    System.Action roofOrders = () =>
                    {
                        if (b.Dead || b.Escaped || b.IsHidden || b.Sedated) return;
                        if (b.Down)
                        {
                            // shot in the leg: he drags himself on (to the helicopter, or back to the door once it is grounded)
                            var crawl = MissionRuntime.From(grounded ? MissionRuntime.Rev(walk) : walk, b.transform.position);
                            b.EscapeText = grounded ? "Baran binaya geri döndü" : "Baran helikopterle kaçtı";
                            b.FleeAlong = null;
                            b.FleePath = crawl;
                            b.SetPlan(Act.Move(crawl, "crawl"), Act.Escape());
                            return;
                        }
                        b.EndInvestigation();
                        if (grounded)
                        {
                            b.EscapeText = "Baran binaya geri döndü";
                            b.FleeAlong = null;
                            var back = MissionRuntime.From(MissionRuntime.Rev(walk), b.transform.position);     // to the stair door (walk[0])
                            b.FleePath = back;
                            b.SetPlan(Act.IdleFacing("shout", b.YawTo(rt.M("helipad")), 2.5f), Act.Move(back, "run"), Act.Escape());
                        }
                        else
                        {
                            b.EscapeText = "Baran helikopterle kaçtı";
                            b.FleePath = null;
                            b.FleeAlong = walk;
                            b.SetPlan(Act.Move(MissionRuntime.From(walk, b.transform.position), "run"), Act.IdleFacing("idle_look", 90f, 0.6f), Act.Escape());
                        }
                    };
                    b.OnArrivedEscape += n =>
                    {
                        if (stage == 0 && rt.IsPlaying)
                        {
                            stage = 1;
                            n.Reappear(true);              // not gone yet: on the stairs
                            n.SetHidden(true);
                            n.MarkWhenHidden = true;
                            n.HiddenStatus = "Merdivende — çatıya çıkıyor";
                            rt.Say("Baran çatıya kaçıyor — helikoptere!", Theme.Danger, "helicopter");
                            if (rotor != null && !grounded) rotor.SetSpin(1f, 4f);
                            rt.After(6.5f, () =>
                            {
                                if (!rt.IsPlaying || n.Dead || n.Sedated) return;
                                n.MarkWhenHidden = false;
                                n.HiddenStatus = null;
                                n.transform.SetPositionAndRotation(roofDoor, Quaternion.Euler(0f, 330f, 0f));
                                n.SetHidden(false);
                                if (grounded && !n.Down)
                                {
                                    // out on the roof, the helicopter dead on the pad: he shouts for it, then turns back
                                    n.EscapeText = "Baran binaya geri döndü";
                                    n.FleeAlong = null;
                                    n.FleePath = new[] { walk[1], walk[0], roofDoor };
                                    n.SetPlan(Act.Move(new[] { walk[0], walk[1], walk[2] }, "run"), Act.IdleFacing("shout", 230f, 3.5f),
                                              Act.Move(new[] { walk[1], walk[0], roofDoor }, "run"), Act.Escape());
                                }
                                else roofOrders();
                            });
                            return;
                        }
                        if (grounded) return;                       // back through the stair door: the contract is lost
                        rt.AttachToProp(n, "heli");
                        var up = rt.MoveProp("heli", rt.P("heli_out"), 10f, false);   // not on the clock: it still leaves while the contract fails
                        if (up != null) up.Delay(1.5f);
                    };
                    rt.DisableProp("heli", "engine", "İstersen helikopterin motorunu vur (2 isabet)", 2, (s, pt) =>
                    {
                        grounded = true;
                        rt.SetFlag("grounded");
                        if (rotor != null) rotor.SetSpin(0f, 6f);
                        rt.Say("Helikopter yerde kaldı!", Theme.Success, "helicopter");
                        if (stage == 1 && !b.IsHidden && !b.Dead) roofOrders();     // already on the roof: he turns back (or crawls back)
                    }).optional = true;
                    rt.RadioAt(4f, "BARAN", "Biliyorum oradasın, tetikçi. Bu camların ardında bana dokunamazsın.", 4.5f);
                    rt.RadioAt(10f, "KONTROL", "Telsizini dinliyoruz. Çatıda projektör ve bir nişancı var — önce onu bul.", 4.5f);
                    rt.When(() => b.Dead, () => rt.Radio("KONTROL", "Baran düştü. Bitti... Şehir artık nefes alabilir.", 5f));
                    CityLife(rt, "night", "baran");
                    Traffic(rt, true, true);
                    Trams(rt, 18f, 52f);
                }
            }.Target("baran", "Kenan Baran", "Patron", "Gümüş saç ve sakal, koyu takım, kırmızı kravat.")
             .Goal("Kenan Baran'ı etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.Custom, "Çatıdaki nişancı sana nişan alamasın", 0, r => !r.Flag("spotted"))
             .Star(ChallengeKind.NoCivilians, "Davetlilere zarar verme"));
        }

        // ================================================================== contract helpers (city)

        /// <summary>Two people meet at the fountain: talk, the envelope, a word of warning, then each goes his way.</summary>
        static void MeetTalk(MissionRuntime rt, NPC kom, NPC cak, Vector3[] komRun, Vector3[] cakRun)
        {
            if (kom.Incapacitated || cak.Incapacitated || kom.Panicking || cak.Panicking || kom.Wounded || cak.Wounded) return;
            kom.EndInvestigation();
            cak.EndInvestigation();
            float yk = kom.YawTo(cak.transform.position), yc = cak.YawTo(kom.transform.position);
            kom.SetPlan(Act.IdleFacing("talk", yk, 8f), Act.IdleFacing("pay", yk, 6f), Act.IdleFacing("argue", yk, 6f), Act.IdleFacing("talk2", yk, 7f),
                        Act.IdleFacing("idle_look", yk + 40f, 3f), Act.Move(komRun, "walk_confident"), Act.IdleFacing("idle", 90f, 0.6f), Act.Escape());
            cak.SetPlan(Act.IdleFacing("talk2", yc, 8f), Act.IdleFacing("handshake", yc, 6f), Act.IdleFacing("talk", yc, 6f), Act.IdleFacing("smoke", yc, 7f),
                        Act.IdleFacing("idle_look", yc - 40f, 3f), Act.Move(cakRun, "walk"), Act.IdleFacing("idle", 160f, 0.5f), Act.Escape());
            rt.Countdown("Buluşmanın bitmesine", 33f);
            rt.Say("Buluşma başladı", Theme.Accent, "umbrella");
        }

        /// <summary>Boarding point of the tram stop closest to <paramref name="p"/>.</summary>
        static Vector3 NearestBoard(MissionRuntime rt, Vector3 p)
        {
            Vector3 best = rt.M("tram_board_1");
            float bd = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                var b = rt.M("tram_board_" + i);
                float d = (b - p).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        static Vector3[] Join(Vector3[] a, Vector3[] b)
        {
            var r = new Vector3[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        static readonly string[] CityMen = { "civilian_m", "guest_m", "student_m", "elder_m", "runner", "businessman", "worker_a", "photog" };
        static readonly string[] CityWomen = { "civilian_f", "guest_f_a", "guest_f_c", "tourist_f", "office_f", "guest_f_b" };

        static string PickChar(System.Random rnd, string[] pool, string[] avoid)
        {
            for (int k = 0; k < 8; k++)
            {
                var c = pool[rnd.Next(pool.Length)];
                if (avoid == null || Array.IndexOf(avoid, c) < 0) return c;
            }
            foreach (var c in pool) if (avoid == null || Array.IndexOf(avoid, c) < 0) return c;
            return pool[0];
        }

        /// <summary>
        /// Everyday life around the square, scaled to the hour and the weather: people on their way along the promenade
        /// and the boulevard (the ends of those walks are out of sight) or from door to door across the square, the
        /// café terrace, the simit seller, the kiosk, tourists at the statue, a street sweeper, a police patrol at night.
        /// Ordinary bystanders: they glance at noises and run from gunfire. <paramref name="avoid"/>: characters that
        /// would be mistaken for the contract's people.
        /// </summary>
        static void CityLife(MissionRuntime rt, string mood, params string[] avoid)
        {
            var lv = Level.I;
            if (lv == null) return;
            var rnd = new System.Random(rt.Def.number * 31 + 7);
            bool day = mood == "day" || mood == "rally";
            bool evening = mood == "evening";
            bool rain = mood == "rain";
            bool night = mood == "night" || rain;
            int walkers = mood == "rally" ? 6 : (day ? 10 : (evening ? 9 : 6));
            string[] lanes = mood == "rally"
                ? new[] { "walk_d", "walk_e", "walk_f", "walk_g", "walk_e", "walk_d", "walk_g", "walk_f", "walk_e", "walk_d" }
                : new[] { "walk_e", "walk_g", "route_palas_cafe", "walk_d", "route_blvd_palas", "walk_f", "route_opera_cafe", "walk_e", "walk_g", "walk_d" };
            for (int i = 0; i < walkers; i++)
            {
                string lane = lanes[i % lanes.Length];
                if (!lv.HasPath(lane)) continue;
                var route = rt.P(lane);
                if (lane.StartsWith("walk_") ? rnd.NextDouble() < 0.5 : rnd.NextDouble() < 0.35) route = MissionRuntime.Rev(route);
                var n = rt.Spawn(PickChar(rnd, i % 2 == 0 ? CityMen : CityWomen, avoid), route[0], 0f, Role.Civilian, "Yaya");
                float speed = rain ? 1.12f + (float)rnd.NextDouble() * 0.15f : 0.82f + (float)rnd.NextDouble() * 0.3f;
                Stroll(rt, n, route, (float)rnd.NextDouble() * 0.85f, "walk", speed, 6f + (float)rnd.NextDouble() * 16f);
                if (rain) UmbrellaHolder.Give(n, UmbrellaTint(rnd));
            }
            // joggers along the sea in the morning and the evening
            if (day || evening)
            {
                for (int i = 0; i < 2; i++)
                {
                    var route = rt.P(i == 0 ? "walk_f" : "walk_e");
                    if (i == 1) route = MissionRuntime.Rev(route);
                    var j = rt.Spawn(PickChar(rnd, i == 0 ? CityMen : CityWomen, avoid), route[0], 0f, Role.Civilian, "Koşucu");
                    Stroll(rt, j, route, (float)rnd.NextDouble(), "run", 0.62f, 10f);
                }
            }
            // the café terrace (not in the rain or late at night)
            if ((day || evening) && mood != "rally")
            {
                int seated = 0;
                for (int i = 0; i < 8 && seated < 7; i++)
                {
                    if (rnd.NextDouble() < 0.4) continue;
                    foreach (var side in new[] { "", "b" })
                    {
                        if (side == "b" && rnd.NextDouble() < 0.45) continue;
                        string mk = "cafe_seat_" + i + side;
                        if (!lv.HasMarker(mk)) continue;
                        float yaw = lv.MarkerYaw(mk);
                        var c = rt.Spawn(PickChar(rnd, seated % 2 == 0 ? CityWomen : CityMen, avoid), rt.M(mk), yaw, Role.Civilian, "Kafe Müşterisi");
                        rt.Idler(c, yaw, "sit_talk", "sit_drink", "sit_idle");
                        seated++;
                    }
                }
                if (lv.HasMarker("cafe_door"))
                {
                    var w = rt.Spawn("waiter", rt.M("cafe_door"), 90f, Role.Worker, "Garson", "GÖREVLİ");
                    rt.PingPong(w, new[] { rt.M("cafe_door"), V(-32.9f, SW, 26.5f), V(-32.9f, SW, 31.8f) }, "walk_carry", 0.9f, "idle_wait", 4f);
                }
            }
            if (day || evening)
            {
                if (lv.HasMarker("simit_seller"))
                {
                    var s = rt.Spawn("elder_m", rt.M("simit_seller"), 180f, Role.Worker, "Simitçi", "SİVİL");
                    rt.Idler(s, 180f, "idle_wait", "shout", "idle", "idle_look");
                }
                if (lv.HasMarker("statue_view") && mood != "rally")
                {
                    var a = rt.Spawn("tourist_f", rt.M("statue_view"), rt.MY("statue_view"), Role.Civilian, "Turist");
                    rt.Idler(a, rt.MY("statue_view"), "phone", "talk", "idle_look");
                    var b = rt.Spawn("student_m", rt.M("statue_view2"), rt.MY("statue_view2"), Role.Civilian, "Turist");
                    rt.Idler(b, rt.MY("statue_view2"), "phone", "idle_look", "talk");
                }
            }
            if (day && lv.HasMarker("sweeper") && mood != "rally")
            {
                var sw = rt.Spawn("worker_c", rt.M("sweeper"), 90f, Role.Worker, "Temizlik İşçisi", "GÖREVLİ");
                rt.Idler(sw, 90f, "sweep", "idle", "sweep");
            }
            if (!rain && lv.HasMarker("kiosk_front"))
            {
                var k = rt.Spawn("worker_a", rt.M("kiosk_front"), 180f, Role.Worker, "Büfeci", "SİVİL");
                rt.Idler(k, 180f, "idle_wait", "phone", "idle");
            }
            if (night && !rain && Array.IndexOf(avoid ?? new string[0], "police_m") < 0)
            {
                // a pair of police on the boulevard sidewalk
                var route = new[] { V(-70f, SW, -3.6f), V(70f, SW, -3.6f) };
                for (int i = 0; i < 2; i++)
                {
                    var p = rt.Spawn("police_m", route[0] + V(0f, 0f, i * 0.9f), 90f, Role.Worker, "Devriye Polisi", "POLİS", "Devriye polisi — ateş etme");
                    rt.PingPong(p, i == 0 ? route : MissionRuntime.Offset(route, -0.9f), "walk", 0.75f, "idle_look", 5f);
                }
            }
        }

        static Color UmbrellaTint(System.Random rnd)
        {
            // multiplies a dark fabric (gamma values above 1 lift it): navy, grey, green, beige, black
            switch (rnd.Next(6))
            {
                case 0: return new Color(1.1f, 1.25f, 1.9f);
                case 1: return new Color(1.7f, 1.7f, 1.75f);
                case 2: return new Color(1.0f, 1.5f, 1.15f);
                case 3: return new Color(2.3f, 2.0f, 1.55f);
                default: return Color.white;
            }
        }

        /// <summary>
        /// Puts a passer-by part way along his route (people are already on their way when the contract opens), lets him
        /// finish it, then the usual loop: gone through the door / round the corner, a pause, back from the start.
        /// </summary>
        static void Stroll(MissionRuntime rt, NPC n, Vector3[] route, float startFrac, string clip, float speed, float rest)
        {
            if (n == null || route == null || route.Length < 2) return;
            float total = 0f;
            for (int i = 1; i < route.Length; i++) total += Vector3.Distance(route[i - 1], route[i]);
            float want = total * Mathf.Clamp01(startFrac), acc = 0f;
            int seg = route.Length - 2;
            var p = route[route.Length - 2];
            for (int i = 0; i < route.Length - 1; i++)
            {
                float d = Vector3.Distance(route[i], route[i + 1]);
                if (acc + d >= want)
                {
                    seg = i;
                    p = Vector3.Lerp(route[i], route[i + 1], d > 1e-4f ? (want - acc) / d : 0f);
                    break;
                }
                acc += d;
            }
            var tail = new List<Vector3> { p };
            for (int i = seg + 1; i < route.Length; i++) tail.Add(route[i]);
            n.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, MissionRuntime.Yaw(route[seg], route[seg + 1]), 0f));
            var start = route[0];
            float y0 = MissionRuntime.Yaw(route[0], route[1]);
            n.SetPlan(Act.Move(tail.ToArray(), clip, speed), Act.Hide(), Act.Idle("idle", rest),
                      Act.Do(() => { n.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, y0, 0f)); n.SetHidden(false); }),
                      Act.Move(route, clip, speed), Act.Hide(), Act.Idle("idle", rest), Act.Loop(3));
        }

        /// <summary>Cars along the boulevard, round and round (one lane can stay empty for a contract's own convoy).</summary>
        static void Traffic(MissionRuntime rt, bool westbound, bool eastbound)
        {
            var rnd = new System.Random(rt.Def.number * 13 + 5);
            if (westbound)
            {
                Drive(rt, "traffic_0", "bvd_west", 12.5f, 1f + (float)rnd.NextDouble() * 4f, rnd);
                Drive(rt, "traffic_1", "bvd_west", 11f, 14f + (float)rnd.NextDouble() * 8f, rnd);
            }
            if (eastbound)
            {
                Drive(rt, "traffic_2", "bvd_east", 12f, 4f + (float)rnd.NextDouble() * 6f, rnd);
                Drive(rt, "traffic_3", "bvd_east", 13f, 19f + (float)rnd.NextDouble() * 8f, rnd);
            }
        }

        static void Drive(MissionRuntime rt, string tag, string path, float speed, float firstDelay, System.Random rnd)
        {
            var lv = Level.I;
            if (lv == null || lv.Tagged(tag) == null || !lv.HasPath(path)) return;
            var pts = rt.P(path);
            Action go = null;
            go = () =>
            {
                if (!rt.IsPlaying) return;
                var m = rt.MoveProp(tag, pts, speed, false);
                if (m != null) m.Arrived = () => rt.After(4f + (float)rnd.NextDouble() * 16f, go);
            };
            rt.At(firstDelay, go);
        }

        /// <summary>Both tram lines on their timetable (into the stop, a short dwell with the bell, on, back later).</summary>
        static void Trams(MissionRuntime rt, float firstWest, float firstEast)
        {
            TramLine(rt, "tram_w", "tram_west", firstWest, 10f, 55f);
            TramLine(rt, "tram_e", "tram_east", firstEast, 10f, 55f);
        }

        static void TramLine(MissionRuntime rt, string tag, string path, float first, float dwell, float headway)
        {
            var lv = Level.I;
            if (lv == null || lv.Tagged(tag) == null || !lv.HasPath(path)) return;
            var p = rt.P(path);
            if (p.Length < 3) return;
            Action run = null;
            run = () =>
            {
                if (!rt.IsPlaying) return;
                var m = rt.MoveProp(tag, new[] { p[0], p[1] }, 9f, false);
                if (m == null) return;
                m.Arrived = () =>
                {
                    Sfx.PlayAt(Sfx.TramBell, p[1], 0.7f);
                    rt.After(dwell, () =>
                    {
                        if (!rt.IsPlaying) return;
                        Sfx.PlayAt(Sfx.TramBell, p[1], 0.5f);
                        var m2 = rt.MoveProp(tag, new[] { p[1], p[2] }, 9f, false);
                        if (m2 != null) m2.Arrived = () => rt.After(headway, run);
                    });
                };
            };
            rt.At(first, run);
        }

        /// <summary>A place a lookout can stand on a roof: a few metres from <paramref name="from"/> towards
        /// <paramref name="nest"/> (or to either side), on the same level, clear of walls; null when there is none.</summary>
        static Vector3? RoofSpot(Vector3 from, Vector3 nest)
        {
            var d = nest - from; d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return null;
            d.Normalize();
            foreach (float turn in new[] { 0f, 35f, -35f, 70f, -70f })
                foreach (float dist in new[] { 4f, 2.5f })
                {
                    var p = from + Quaternion.Euler(0f, turn, 0f) * d * dist;
                    if (!Level.WalkableLine(from, p, out float y, 0.25f)) continue;
                    // level floor a bit further on too (not standing on the edge)
                    var beyond = from + Quaternion.Euler(0f, turn, 0f) * d * (dist + 1.2f);
                    float gy = Level.GroundY(new Vector3(beyond.x, y + 1f, beyond.z), float.NaN);
                    if (float.IsNaN(gy) || Mathf.Abs(gy - y) > 0.25f) continue;
                    return new Vector3(p.x, y, p.z);
                }
            return null;
        }

        /// <summary>
        /// An enemy marksman with a scoped rifle in his hands (a kneeling or a standing aim clip): he scans his sector and
        /// the scope catches the light whenever it swings past the nest; he takes aim at his mark; he hears the player's
        /// shots (a silenced rifle needs two before he finds the muzzle) and turns his rifle on the nest.
        /// </summary>
        class Marksman
        {
            readonly MissionRuntime rt;
            readonly NPC npc;
            readonly Transform rifle;
            readonly ScopeGlint glint;
            readonly string pose;
            readonly Vector3 scanAt;
            readonly float scanArc, phase;
            float heard;
            int seen;

            public Marksman(MissionRuntime rt, NPC npc, string pose, Vector3 scanAt, float scanArc, bool night)
            {
                this.rt = rt;
                this.npc = npc;
                this.pose = pose;
                this.scanAt = scanAt;
                this.scanArc = scanArc;
                phase = UnityEngine.Random.value * 6.28f;
                GunHold.Remove(npc);                                      // the scoped rifle replaces the hidden pistol
                var prefab = Res.Prop("ai_rifle_lr");
                if (prefab != null)
                {
                    var go = UnityEngine.Object.Instantiate(prefab, rt.Root);
                    go.name = "MarksmanRifle";
                    foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c);
                    Util.SetLayer(go, Layers.IgnoreRaycast);
                    rifle = go.transform;
                    RifleHold.Give(npc, rifle, pose, Look);
                    glint = ScopeGlint.Attach(rifle, new Vector3(0.06f, 0.11f, 0f), npc, night);
                    glint.barrel = rifle;
                }
                else glint = ScopeGlint.Attach(npc.transform, new Vector3(0f, 1.5f, 0.3f), npc, night);
                glint.strength = 1f;
                npc.SetPlan(Act.IdleFacing(pose, npc.YawTo(scanAt), 0));       // at his post (he does not stroll about)
            }

            /// <summary>Where the rifle points now: the mark, the nest, or a slow sweep of his sector.</summary>
            Vector3 Look()
            {
                if (npc.Aiming)
                {
                    if (npc.AimingAtPlayer) { glint.strength = 2f; return rt.NestPoint; }
                    if (npc.AimVictim != null) { glint.strength = 1.6f; return npc.AimVictim.ChestPoint; }
                }
                glint.strength = 1f;
                var from = npc.transform.position;
                float yaw = Mathf.Sin(Time.time * 0.32f + phase) * scanArc * 0.5f;
                return from + Quaternion.Euler(0f, yaw, 0f) * (scanAt - from);
            }

            /// <summary>Alive, awake and with both arms: he can work the rifle.</summary>
            public bool Able => npc != null && !npc.Dead && !npc.Incapacitated && !npc.Escaped && !(npc.Wounded && npc.WoundZone == BodyZone.Arm);

            /// <summary>Able and not already taking aim at something.</summary>
            public bool Free => Able && !npc.Aiming;

            /// <summary>Where his bullet leaves the barrel (the rifle lies along X, muzzle at -X).</summary>
            public Vector3 Muzzle => rifle != null ? rifle.TransformPoint(new Vector3(-0.66f, 0.03f, 0f)) : npc.HandPoint;

            public void Fire(Vector3 at)
            {
                var m = Muzzle;
                FX.MuzzleFlashWorld(m, (at - m).normalized);
                Sfx.PlayAt(Sfx.Shot, m, 1f);
                Sfx.PlayAt(Sfx.ShotTail, m, 0.7f);
            }

            /// <summary>Takes aim at someone below (a clock on the HUD, a brighter glint).</summary>
            public void AimAt(NPC victim, float seconds, Action<NPC> fire)
            {
                if (!Free || victim == null) return;
                vip = victim;
                vipFire = fire;
                vipSeconds = seconds;
                npc.BeginAim(victim, seconds, fire, pose);
            }

            /// <summary>Turns on the sniper nest: the player has <paramref name="seconds"/> to drop him or get down behind
            /// the parapet. He fires, works the bolt and takes aim again (quicker now: he knows where to look) while the
            /// player keeps showing himself. Two rounds into the parapet and he goes back to his own job (the mark he
            /// dropped for this, if there was one).</summary>
            public void HuntPlayer(float seconds, string reason)
            {
                if (!Able || npc.AimingAtPlayer || !rt.IsPlaying) return;
                bool first = !hunting;
                hunting = true;
                if (npc.Aiming && npc.AimVictim != null) resumeVip = true;      // he leaves his mark below for now
                npc.StopAiming();
                npc.BeginAimAtPoint(rt.NestPoint, seconds, n =>
                {
                    if (!rt.IsPlaying) return;
                    bool ducked = Sniper.I != null && Sniper.I.InCover;
                    bool hit = rt.PlayerShot(npc, Muzzle, reason, 70f);
                    if (!rt.IsPlaying) return;                                  // that one killed the player
                    coverShots = hit || !ducked ? 0 : coverShots + 1;
                    if (!hit && Time.time >= nextNote)
                    {
                        nextNote = Time.time + 8f;
                        rt.Say(ducked ? "Kurşun siperine çarptı — nişancı başını kaldırmanı bekliyor" : "Iskaladı — nişancı yeniden nişan alıyor",
                               Theme.Accent, ducked ? "shield" : "alert");
                    }
                    if (coverShots >= 2)
                    {
                        // the shooter stays down: back to his post (and to the mark he left, if any)
                        hunting = false;
                        coverShots = 0;
                        npc.SetPlan(Act.IdleFacing(pose, npc.YawTo(scanAt), 0));
                        if (resumeVip)
                        {
                            resumeVip = false;
                            rt.After(1.5f, () =>
                            {
                                if (rt.IsPlaying && Free && vip != null && !vip.Dead && !vip.Escaped && !vip.IsHidden)
                                {
                                    npc.BeginAim(vip, Mathf.Max(4f, vipSeconds * 0.6f), vipFire, pose);
                                    rt.Radio("KONTROL", "Nişancı yine aşağıya nişan alıyor!", 3f);
                                }
                            });
                        }
                        return;
                    }
                    rt.After(hit ? 2.4f : 1.6f, () => { if (Able && !npc.AimingAtPlayer && rt.IsPlaying) HuntPlayer(UnityEngine.Random.Range(2.6f, 3.4f), reason); });
                }, pose);
                if (first) rt.Radio("KONTROL", "Seni gördü! Ateş etmeden onu indir — ya da siperine eğil!", 3f);
            }

            bool hunting, resumeVip;
            int coverShots;
            float nextNote, vipSeconds;
            NPC vip;
            Action<NPC> vipFire;

            /// <summary>Counts the player's shots: one loud shot (two silenced ones) and he turns on the nest.</summary>
            public void WatchPlayerShots(string reason)
            {
                seen = rt.Shots;
                Action watch = null;
                watch = () =>
                {
                    if (!rt.IsPlaying || !Able) return;
                    if (rt.Shots > seen)
                    {
                        var w = Gear.Carried;
                        heard += (rt.Shots - seen) * (w != null && w.silenced ? 0.5f : 1f);
                        seen = rt.Shots;
                        if (heard >= 0.99f && !npc.AimingAtPlayer) rt.After(1.4f, () => HuntPlayer(6f, reason));
                    }
                    rt.After(0.2f, watch);
                };
                rt.After(0.2f, watch);
            }
        }
    }
}

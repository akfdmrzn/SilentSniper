using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// Kartal Tepe contracts (21-28). Baran's files name the five men who paid for him and kept him safe — the Board —
    /// and every year they spend New Year's week at the Kartal Tepe ski resort. The banker on the gondola, the judge in
    /// his glass villa, the builder in the hot tub, the courier who must be taken alive, the police chief whose car
    /// must meet an "accident", the witness the Board wants dead, the fixer crossing the village in a blizzard and, at
    /// midnight under the fireworks, the man at the top.
    /// </summary>
    public static partial class MissionDefs
    {
        static readonly string[] SnowMen = { "w_ski_m1", "w_ski_m2", "w_lodge_m", "w_ski_m1", "w_worker" };
        static readonly string[] SnowWomen = { "w_ski_f1", "w_ski_f2", "w_lodge_f" };

        static void AddMountain(List<MissionDef> list)
        {
            // ------------------------------------------------------------------ 21. the gondola (day): the banker rides up
            list.Add(new MissionDef
            {
                id = "m21", number = 21, region = "mountain", title = "TELEFERİK", location = "Kartal Tepe teleferiği", markTargets = 1,
                preset = "snow_day", wind = V(-1.6f, 0, 2.2f), reward = 13000, focus = "gondola_board", icon = "gondola",
                kind = "SUİKAST", clock = "10:20",
                introLine = "Sinan Kurt zirvedeki kahvaltıya teleferikle çıkacak. Kabin zirve istasyonuna varırsa onu kaybederiz.",
                brief = "Baran'ın dosyaları bizi Kartal Tepe'ye getirdi. Kurul — Baran'ı yıllarca besleyen ve koruyan beş kişi — yılbaşı haftasını her yıl burada geçiriyor.\n\nİlki Sinan Kurt, Kurul'un bankeri. Kayak barında kahvesini bitirince iki korumasıyla teleferiğe binip zirvedeki restorana çıkacak. Kabin hattın sonuna varırsa onu kaybederiz.",
                tip = "Kabin camı mermiyi durdurmaz; yukarı çıkan kabine biraz önden ve yukarıdan nişan al. İstasyonun yanındaki motor dairesine iki atış hattı durdurur: kabinler sallanır, içindekiler çömelir.",
                gearHint = "Uzun menzil: kabin 250 metreden 1 kilometreye kadar uzaklaşır",
                introNote = "Teleferik motoru: istasyonun batısındaki beton kulübe",
                setup = rt =>
                {
                    var line = CableCarLine.I;
                    var t = rt.AddTarget("w_banker", rt.M("m21_coffee"), rt.MY("m21_coffee"));
                    t.HideOnFlee = true;
                    t.FleePath = new[] { rt.M("gondola_stairs"), rt.M("gondola_hall") };
                    t.EscapeText = "Sinan Kurt istasyona kaçtı";
                    var g0 = rt.Spawn("w_guard", rt.M("m21_guard_0"), rt.MY("m21_guard_0"), Role.Bodyguard, "Koruma");
                    var g1 = rt.Spawn("w_guard2", rt.M("m21_guard_1"), rt.MY("m21_guard_1"), Role.Bodyguard, "Koruma");
                    var walk = rt.P("m21_walk");
                    float boardYaw = rt.MY("gondola_board");
                    t.SetPlan(Act.IdleFacing("coffee", 180f, 9f), Act.IdleFacing("talk", 200f, 7f), Act.IdleFacing("phone_conv", 165f, 8f),
                              Act.Move(walk, "walk", 0.95f), Act.IdleFacing("idle_wait", boardYaw, 0f));
                    g0.SetPlan(Act.IdleFacing("idle_look", 160f, 24.6f), Act.Move(MissionRuntime.Offset(walk, 0.9f), "walk", 0.95f),
                               Act.IdleFacing("guard_pose", boardYaw, 0f));
                    g1.SetPlan(Act.IdleFacing("guard_pose", 200f, 25.4f), Act.Move(MissionRuntime.Offset(walk, -0.9f), "walk", 0.95f),
                               Act.IdleFacing("idle_look", boardYaw, 0f));

                    // boarding: the next cabin crawling past the platform takes all three
                    int cab = -1;
                    bool riding = false;
                    float boardS = line != null ? line.NearestS(rt.M("gondola_board")) : 0f;
                    var boardAt = walk[walk.Length - 1];
                    var riders = new[] { t, g0, g1 };
                    var spots = new[] { new Vector3(0.25f, -4.05f, -0.45f), new Vector3(-0.45f, -4.05f, 0.5f), new Vector3(0.45f, -4.05f, 0.55f) };
                    var spotYaw = new[] { 180f, 120f, 240f };
                    rt.When(() => line != null && !t.Incapacitated && !t.Panicking && !t.Escaped && Vector3.Distance(t.transform.position, walk[walk.Length - 1]) < 1.0f,
                            () =>
                            {
                                cab = line.NextCabinAt(boardS, 1.5f);
                                rt.Radio("KONTROL", "Platformdalar. Sıradaki kabine binecekler.", 3.5f);
                            });
                    rt.When(() => cab >= 0 && line.DistanceTo(cab, boardS) < 0.6f, () =>
                    {
                        // spooked on the platform in the meantime: he runs for the station instead (see below)
                        if (t.Incapacitated || t.Escaped || t.Panicking) { cab = -1; return; }
                        var ct = line.CabinT(cab);
                        line.SetShell(cab, true);
                        for (int i = 0; i < riders.Length; i++)
                        {
                            var r = riders[i];
                            if (r == null || r.Incapacitated || r.Escaped || r.Panicking || (r != t && r.Alerted)) continue;
                            if (Vector3.Distance(r.transform.position, boardAt) > 3.5f) continue;            // off looking at something
                            rt.AttachToProp(r, "gcabin_" + cab);
                            r.transform.localPosition = spots[i];
                            r.transform.localRotation = Quaternion.Euler(0f, spotYaw[i], 0f);
                            r.CanFlee = false;
                            r.SetPlan(Act.Idle(i == 0 ? "idle_look" : "guard_pose", 0f));
                        }
                        t.MarkWhenHidden = true;
                        riding = true;
                        Sfx.PlayAt(Sfx.Gondola, ct.position, 0.5f);
                        rt.Say("Kabine bindiler — yukarı çıkıyorlar", Theme.Accent, "gondola");
                        rt.Countdown("Zirve istasyonuna", () => riding && !t.Dead ? line.EtaTo(cab, line.TopS - 24f) : 0f);
                    });
                    // the cabin reaches the summit: gone
                    rt.When(() => riding && !t.Incapacitated && line.DistanceTo(cab, line.TopS - 24f) < 2f, () =>
                    {
                        if (t.Incapacitated) return;
                        rt.ClearCountdown();
                        t.EscapeText = "Sinan Kurt zirve istasyonuna vardı";
                        t.ForceEscape();
                    });
                    // the motor house: two hits stop the whole line (the cabins swing, the riders crouch); the crew gets it
                    // going again after a while
                    var motor = rt.DisableProp("gondola_motor", "motor", "Teleferik motorunu durdur (isteğe bağlı)", 2, (sh, p) =>
                    {
                        if (line == null) return;
                        line.Stop();
                        Sfx.PlayAt(Sfx.Explosion, p, 0.35f);
                        rt.Say("Teleferik durdu — kabinler sallanıyor!", Theme.Danger, "gondola");
                        foreach (var r in riders)
                        {
                            if (r == null || r.Incapacitated || !riding || !r.Riding) continue;
                            r.EndInvestigation();
                            r.SetPlan(Act.Idle(r == t ? "cower" : "alert", 0f));
                        }
                        rt.After(48f, () =>
                        {
                            if (!rt.IsPlaying || t.Incapacitated) return;
                            line.Restart();
                            rt.Radio("KONTROL", "Teknisyenler hattı yeniden çalıştırdı! Kabin yine hareket ediyor.", 4f);
                            if (riding && !t.Incapacitated && !t.Panicking) { t.EndInvestigation(); t.SetPlan(Act.Idle("idle_look", 0f)); }
                        });
                    });
                    if (motor != null) motor.optional = true;
                    // a kill while the cabin is moving is the hard way
                    rt.When(() => t.Dead, () => { if (riding && line != null && line.Speed > 1.5f) rt.SetFlag("moving_kill"); rt.ClearCountdown(); });
                    // trouble on the ground: they make for the station
                    rt.When(() => !riding && t.Panicking, () =>
                    {
                        foreach (var g in new[] { g0, g1 }) if (g != null && !g.Incapacitated) g.SetPlan(Act.Move(new[] { rt.M("gondola_stairs") }, "run"), Act.IdleFacing("alert", 180f, 0f));
                    });
                    rt.RadioAt(6f, "KONTROL", "Kayak barında, korumalarıyla kahve içiyor. Platforma çıkınca sıradaki kabine binecek.", 4.5f);
                    rt.RadioAt(40f, "KONTROL", "Hat saatte 18 kilometre gidiyor. Kabin uzaklaştıkça mermi düşüşü artar — hesabını yap.", 4.5f);
                    MountainLife(rt, "day", "w_banker", "w_guard", "w_guard2");
                }
            }.Target("w_banker", "Sinan Kurt", "Banker", "Uzun gri palto, gözlük, kır saç. İki korumasıyla.")
             .Goal("Sinan Kurt'u zirve istasyonuna varmadan etkisiz hale getir")
             .Goal("Teleferik motorunu durdurabilirsin (isteğe bağlı)")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.Custom, "Kabin hareket halindeyken vur", 0, r => r.Flag("moving_kill"))
             .Star(ChallengeKind.MaxShots, "En fazla iki atış", 2));

            // ------------------------------------------------------------------ 22. the fireplace (night): the judge's glass villa
            list.Add(new MissionDef
            {
                id = "m22", number = 22, region = "mountain", title = "ŞÖMİNE", location = "Doğu yamacı — cam villa", requires = new[] { "nv" },
                preset = "snow_night", wind = V(0.8f, 0, -0.6f), reward = 13500, focus = "m22_window", icon = "fireplace",
                kind = "SUİKAST", clock = "23:10",
                introLine = "Hakim Ferit Oral şöminenin başında. Villanın bütün ön cephesi cam — ve cam kırıldığı an içeridekiler anlar.",
                brief = "Ferit Oral, Baran'ın her davasını düşüren hakim. Yıllardır Kurul'un yargıdaki adamı. Bu gece doğu yamacındaki cam villasında eşiyle.\n\nDışarıda, terasta bir koruma dolaşıyor; aşağıda araçta bir şoför bekliyor. Hakim 23:25'te yatmaya gidecek. Eşi odadayken ateş edersen tanık kalır.",
                tip = "İlk mermi camı kırar ve sesi içeridekileri ürkütür: ilk atışın öldürücü olsun. Hakim zaman zaman cama gelip vadiye bakıyor — en açık an o.",
                coverHint = "Hakim bölmenin arkasında. Cama ya da şömineye geri gelmesini bekle.",
                setup = rt =>
                {
                    var t = rt.AddTarget("w_judge", rt.M("m22_sofa"), rt.MY("m22_sofa"));
                    t.HideOnFlee = true;
                    t.FleePath = rt.P("m22_flee");
                    t.EscapeText = "Hakim yatak odasına kilitlendi";
                    t.SightRange = 14f;
                    float sofaY = rt.MY("m22_sofa");
                    t.SetPlan(Act.IdleFacing("sit_drink", sofaY, 12f), Act.IdleFacing("sit_talk", sofaY, 9f),
                              Act.Move(rt.P("m22_to_window"), "walk_slow"), Act.IdleFacing("idle_look", rt.MY("m22_window"), 8f),
                              Act.IdleFacing("phone_conv", rt.MY("m22_window") + 20f, 6f),
                              Act.Move(rt.P("m22_to_fire"), "walk_slow"), Act.IdleFacing("idle_arms", rt.MY("m22_fire"), 7f),
                              Act.Move(rt.P("m22_to_bar"), "walk_slow"), Act.IdleFacing("drink", rt.MY("m22_bar"), 7f),
                              Act.Move(rt.P("m22_to_sofa"), "walk_slow"), Act.Loop(0));
                    // his wife: talks to him from the table, goes to the other rooms now and then
                    var w = rt.Spawn("w_lodge_f", rt.M("m22_wife"), rt.MY("m22_wife"), Role.Civilian, "Hakimin Eşi", "SİVİL", "Hakimin eşi — ateş etme");
                    float wy = rt.MY("m22_wife");
                    bool wifeOut = false;
                    w.SetPlan(Act.IdleFacing("talk", wy, 14f), Act.IdleFacing("drink", wy, 9f),
                              Act.Move(rt.P("m22_wife_out"), "walk"), Act.Do(() => { wifeOut = true; w.SetHidden(true); }), Act.Idle("idle", 28f),
                              Act.Do(() => { w.SetHidden(false); wifeOut = false; }), Act.Move(rt.P("m22_wife_in"), "walk"),
                              Act.IdleFacing("phone", wy, 12f), Act.IdleFacing("talk", wy, 10f), Act.Loop(2));
                    // the bodyguard on the deck with a torch, the driver by the car
                    var g = rt.Spawn("w_guard", rt.P("m22_deck")[0], 0f, Role.Guard, "Villa Koruması");
                    rt.PingPong(g, rt.P("m22_deck"), "walk_patrol", 0.75f, "flashlight", 5f);
                    var d = rt.Spawn("w_guard2", rt.M("m22_driver"), rt.MY("m22_driver"), Role.Guard, "Şoför");
                    rt.Idler(d, rt.MY("m22_driver"), "smoke", "idle_look", "phone");
                    // the judge's security man down the drive: night glasses on the slopes across the valley (far enough
                    // from the villa that a shot at him is not heard inside)
                    var dm = rt.M("m22_driver");
                    var away = dm - rt.M("m22_sofa"); away.y = 0f;
                    away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
                    var sideV = new Vector3(away.z, 0f, -away.x);
                    Vector3? lpo = null;
                    foreach (var off in new[] { away * 7f, away * 7f + sideV * 2.5f, away * 7f - sideV * 2.5f, away * 5f, away * 4f + sideV * 2f,
                                                away * 4f - sideV * 2f, V(1.8f, 0f, 1.4f), V(-1.8f, 0f, 1.4f) })
                    {
                        if (!Level.WalkableLine(dm, dm + off, out float ly)) continue;
                        var q = dm + off;
                        q.y = ly;
                        lpo = q;
                        break;
                    }
                    NPC vlk = null;
                    if (lpo.HasValue)
                    {
                        var lp = lpo.Value;
                        vlk = rt.Spawn("w_guard", lp, MissionRuntime.Yaw(lp, rt.NestPoint), Role.Guard, "Villa Gözcüsü", "GÖZCÜ", "Gece dürbünüyle yamaçları tarıyor");
                        Spotter.Make(vlk, MissionRuntime.Yaw(lp, rt.NestPoint), 70f, 12f);
                    }
                    // no witness: she is out of the room (or he is far from her) when he falls
                    rt.When(() => t.Dead, () =>
                    {
                        if (!wifeOut && !w.IsHidden && Vector3.Distance(w.transform.position, t.transform.position) < 9f) rt.SetFlag("witness");
                    });
                    rt.Countdown("Hakimin yatmasına", 150f);
                    rt.At(150f, () =>
                    {
                        if (t.Incapacitated || t.Escaped) return;
                        t.EndInvestigation();
                        rt.Say("Hakim yatak odasına gidiyor!", Theme.Danger, "fireplace");
                        t.SetPlan(Act.Move(MissionRuntime.From(rt.P("m22_flee"), t.transform.position), "walk_slow"), Act.Escape());
                        t.EscapeText = "Hakim yatmaya gitti";
                    });
                    rt.RadioAt(7f, "KONTROL", "Cam ön cephe vadiye bakıyor. Hakim kanepede, eşi masanın başında.", 4.5f);
                    rt.RadioAt(36f, "KONTROL", "Eşi arada bir öbür odalara geçiyor. O sırada vurursan kimse bir şey görmez.", 4.5f);
                    rt.At(11f, () =>
                    {
                        if (vlk == null || vlk.Incapacitated || vlk.Lookout == null || vlk.Lookout.Reporting || vlk.Lookout.Reported) return;
                        rt.Radio("KONTROL", "Yolun aşağısındaki adamın elinde gece dürbünü var. Dürbün sana dönünce eğil.", 4.5f);
                    });
                    MountainLife(rt, "night", "w_judge", "w_lodge_f", "w_guard", "w_guard2");
                }
            }.Target("w_judge", "Ferit Oral", "Hakim", "Beyaz balıkçı yaka kazak, gözlük, beyaz saç. Cam villada.")
             .Goal("Hakim Ferit Oral'ı yatmaya gitmeden etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.Custom, "Tanık bırakma: eşi odadayken ateş etme", 0, r => !r.Flag("witness"))
             .Star(ChallengeKind.MaxShots, "Camın ardından tek atış", 1));

            // ------------------------------------------------------------------ 23. the hot tub (sunset): steam on the terrace
            list.Add(new MissionDef
            {
                id = "m23", number = 23, region = "mountain", title = "JAKUZİ", location = "Kartal Dağ Oteli — spa terası",
                preset = "snow_sunset", wind = V(2.0f, 0, 0.8f), reward = 14000, focus = "m23_tub", icon = "hot_tub",
                kind = "SUİKAST", clock = "17:40",
                introLine = "Selim Tan gün batımında jakuzide. Buhar onu bir saklıyor, bir gösteriyor — pencereni bekle.",
                brief = "Selim Tan, Baran'ın limandaki depolarını, Kurul'un kara parayla yükselen bütün otellerini inşa eden müteahhit.\n\nOtelin spa terasında, jakuzide gün batımını izliyor. Kapıda bir koruması, yanında bir arkadaşı var; garson şampanya getirecek. Sudan çıktığında içeri girer.\n\nKoruma kapıdan dürbünle yamaçları tarıyor. Selim'in dibinde durduğu için onu vurmak Selim'i ürkütür: dürbün sana döndüğünde eğil.",
                tip = "Suyun üstünde yalnızca başı ve omuzları görünüyor. Buhar yoğunlaştığında hedef kaybolur, açıldığında birkaç saniyelik pencere doğar. Sudan çıkıp kapıya yürürken bütün boyuyla açıktadır — ama yalnızca birkaç saniye.",
                setup = rt =>
                {
                    var tub = rt.M("m23_tub");
                    var t = rt.AddTarget("w_tycoon", tub, rt.MY("m23_tub"));
                    t.HideOnFlee = true;
                    t.NoCrawl = true;
                    t.FleePath = rt.P("m23_out");
                    t.EscapeText = "Selim Tan otele girdi";
                    float ty = rt.MY("m23_tub");
                    t.SetPlan(Act.IdleFacing("sit_idle", ty, 10f), Act.IdleFacing("sit_talk", ty, 9f), Act.IdleFacing("sit_drink", ty, 8f), Act.Loop(0));
                    var f = rt.Spawn("w_ski_f1", rt.M("m23_friend"), rt.MY("m23_friend"), Role.Civilian, "Arkadaşı", "SİVİL", "Selim'in arkadaşı — ateş etme");
                    rt.Idler(f, rt.MY("m23_friend"), "talk", "drink", "idle", "phone");
                    var g = rt.Spawn("w_guard", rt.M("m23_guard"), rt.MY("m23_guard"), Role.Bodyguard, "Koruma", "KORUMA", "Kapıda — dürbünle yamaçları tarıyor");
                    // he keeps an eye on the slopes with binoculars: a sweep across them every few seconds
                    Spotter.Make(g, MissionRuntime.Yaw(rt.M("m23_guard"), rt.NestPoint), 64f, 10f);
                    // the waiter with the champagne
                    var wpath = rt.P("m23_waiter");
                    var wt = rt.Spawn("waiter", wpath[0], 90f, Role.Worker, "Garson", "GÖREVLİ");
                    wt.SetHidden(true);
                    rt.At(34f, () =>
                    {
                        if (wt.Incapacitated || wt.Panicking) return;
                        wt.SetHidden(false);
                        wt.SetPlan(Act.Move(wpath, "walk_carry", 0.9f), Act.IdleFacing("pay", MissionRuntime.Yaw(wpath[wpath.Length - 1], tub), 5f),
                                   Act.Move(MissionRuntime.Rev(wpath), "walk", 0.9f), Act.Hide());
                    });
                    // steam over the water: thick and thin by turns
                    ChimneySmoke steam = null;
                    foreach (var cs in Level.I.StaticRoot.GetComponentsInChildren<ChimneySmoke>()) if (cs.name == "Steam") steam = cs;
                    rt.When(() => t.Dead, () => { if (steam != null && steam.Thickness > 0.55f) rt.SetFlag("through_steam"); });
                    // out of the water and inside
                    rt.At(118f, () =>
                    {
                        if (t.Incapacitated || t.Escaped) return;
                        t.EndInvestigation();
                        rt.Say("Selim sudan çıkıyor — kapıya yürüyor!", Theme.Danger, "hot_tub");
                        var outPath = rt.P("m23_out");
                        t.transform.position = outPath[0];
                        t.SetPlan(Act.IdleFacing("idle", MissionRuntime.Yaw(outPath[0], outPath[1]), 1.4f), Act.Move(outPath, "walk_slow"), Act.Escape());
                    });
                    rt.Countdown("Sudan çıkmasına", 118f);
                    rt.RadioAt(6f, "KONTROL", "Buhar bazen tamamen kapatıyor. Sabret: açıldığı an tek bir temiz atış yeter.", 4.5f);
                    rt.At(12f, () =>
                    {
                        if (g.Incapacitated || g.Lookout == null || g.Lookout.Reporting || g.Lookout.Reported) return;
                        rt.Radio("KONTROL", "Kapıdaki koruma dürbünle yamaçları tarıyor. Onu vurursan Selim içeri kaçar: dürbün sana dönünce eğil.", 5f);
                    });
                    MountainLife(rt, "sunset", "w_tycoon", "w_ski_f1", "w_guard", "waiter");
                }
            }.Target("w_tycoon", "Selim Tan", "Müteahhit", "Kalın yapılı, sakallı, koyu saç. Jakuzide.")
             .Goal("Selim Tan'ı içeri girmeden etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.AllHeadshots, "Kafadan vur")
             .Star(ChallengeKind.Custom, "Buharın içinden vur", 0, r => r.Flag("through_steam")));

            // ------------------------------------------------------------------ 24. the snowmobile (day): the courier alive
            list.Add(new MissionDef
            {
                id = "m24", number = 24, region = "mountain", title = "KAR MOTORU", location = "Vadi tabanı — batı yamacı", markTargets = 1,
                preset = "snow_day", wind = V(2.6f, 0, -1.2f), reward = 14500, focus = "m24_door", icon = "snowmobile",
                kind = "CANLI YAKALA", clock = "13:10",
                introLine = "Kurye Kurul'un defterini kar motoruyla dağa kaçıracak. Ölmemeli: defterin şifresi onda.",
                brief = "Doğan Aksu, Kurul'un kuryesi. Bütün ödemelerin kaydı olan şifreli disk onda. Birazdan otelden çıkıp kar motoruyla vadiyi geçecek ve batı yamacından ormana girecek.\n\nOnu CANLI istiyoruz. Önce motoru durdur — motor bloğuna iki atış — sonra kaçmaya çalışırsa bacağından vur. Ekibimiz ratrakla gelip onu alacak.\n\nKurul onu bizim almamıza izin vermez: kurye düşünce spadan adamları çıkıp onu susturmaya gelecek. Ratrak gelene kadar onları durdur.",
                tip = "Kar motoru saatte 25 kilometreyle gidiyor: biraz önden nişan al. Sürücüye isabet edersen ölebilir; motor bloğu sürücünün önünde, hedefin altında. Motor durunca kurye en yakın dağ evine koşar: içeri girmeden bacağından vur.",
                setup = rt =>
                {
                    Level.I.ShowTagged("courier_sled", true);
                    var t = rt.AddTarget("w_courier", rt.M("m24_door"), rt.MY("m24_door"));
                    t.NoCrawl = true;                      // a leg wound drops him where he is: no crawling off into the wood
                    t.HideOnFlee = false;
                    t.EscapeText = "Kurye bir dağ evine saklandı";
                    var wound = rt.MustWound(t, BodyZone.Leg, "Kuryeyi BACAĞINDAN vur — ölmemeli");
                    bool teamHere = false;
                    var keep = rt.Protect(t, "Ekip gelene kadar onu hayatta tut", () => teamHere);
                    keep.hidden = true;
                    var ride = rt.P("m24_ride");
                    bool onSled = false, sledDead = false;
                    PathMover mv = null;
                    var sled = Level.I.Tagged("courier_sled");
                    // our snowcat waits further along his route
                    var cat = Level.I.Tagged("snowcat");
                    if (cat != null && Level.I.HasMarker("m24_cat"))
                        cat.transform.SetPositionAndRotation(rt.M("m24_cat"), Quaternion.Euler(0f, rt.MY("m24_cat"), 0f));
                    // on foot: to the nearest door he can reach (the layout's runs m24_run_k start along the sled's route)
                    Vector3[] runUsed = null;
                    Action runForCover = () =>
                    {
                        var here = t.transform.position;
                        float bd = float.MaxValue;
                        runUsed = null;
                        for (int k = 0; Level.I.HasPath("m24_run_" + k); k++)
                        {
                            var r = rt.P("m24_run_" + k);
                            float d = (r[0] - here).sqrMagnitude;
                            if (d < bd) { bd = d; runUsed = r; }
                        }
                        var run = new List<Vector3> { here };
                        if (runUsed != null) run.AddRange(runUsed);
                        else run.AddRange(MissionRuntime.From(ride, here));
                        t.FleePath = run.ToArray();
                        t.CanFlee = true;
                        t.EndInvestigation();
                        t.SetPlan(Act.Move(t.FleePath, "run"), Act.Escape());
                    };
                    // out of the door, onto the sled, away
                    t.SetPlan(Act.IdleFacing("phone_conv", rt.MY("m24_door"), 7f), Act.Move(rt.P("m24_to_sled"), "run"),
                              Act.Do(() =>
                              {
                                  if (sled == null || t.Incapacitated) return;
                                  if (sledDead)
                                  {
                                      // the engine was wrecked before he got there: he runs for it
                                      runForCover();
                                      rt.Say("Kar motoru çalışmıyor — kurye koşarak kaçıyor!", Theme.Danger, "run");
                                      return;
                                  }
                                  t.transform.SetPositionAndRotation(sled.transform.TransformPoint(new Vector3(0f, 0.32f, -0.25f)), sled.transform.rotation);
                                  rt.AttachToProp(t, "courier_sled");
                                  t.CanFlee = false;
                                  onSled = true;
                                  mv = rt.MoveProp("courier_sled", ride, 7.2f, false);
                                  if (mv != null) mv.Arrived = () => { if (!t.Incapacitated) { t.EscapeText = "Kurye ormana girdi"; t.ForceEscape(); } };
                                  Sfx.PlayAt(Sfx.Snowmobile, sled.transform.position, 0.7f);
                                  rt.Say("Kurye kar motorunda — vadiyi geçiyor!", Theme.Danger, "snowmobile");
                              }),
                              Act.Idle("sit_idle", 0f));
                    // the sled: two hits in the engine stop it; the courier jumps off and runs for the trees
                    var engine = rt.DisableProp("courier_sled", "engine", "Kar motorunu durdur", 2, (sh, p) =>
                    {
                        sledDead = true;
                        if (mv != null) mv.Stop();
                        if (sled != null) FX.Smoke(sled.transform.position + Vector3.up * 0.6f);
                        if (t.Incapacitated || !onSled) return;
                        rt.DetachFromProp(t);
                        onSled = false;
                        t.transform.position = sled.transform.TransformPoint(new Vector3(1.1f, 0f, 0f));
                        runForCover();
                        rt.Say("Motor durdu — kurye koşarak kaçıyor!", Theme.Danger, "run");
                    });
                    if (engine != null) engine.optional = true;
                    // Kurul's men inside the spa: they come out for him when he is down
                    var kurul = new List<NPC>();
                    for (int i = 0; i < 2; i++)
                    {
                        var k = rt.Spawn(i == 0 ? "w_guard" : "w_guard2", rt.M("m24_door"), rt.MY("m24_door"), Role.Guard, "Kurul'un Adamı", "SİLAHLI",
                                         "Kurul'un adamı — kuryeyi susturmaya geliyor");
                        k.NoRadio = true;
                        k.SetHidden(true);
                        kurul.Add(k);
                    }
                    System.Func<NPC, bool> able = h => h != null && !h.Dead && !h.Incapacitated && !h.Escaped && !(h.Wounded && h.WoundZone == BodyZone.Arm);
                    int pending = 0;
                    System.Action<NPC> comeFor = null;
                    comeFor = k =>
                    {
                        if (teamHere || t.Dead || !rt.IsPlaying || !able(k) || k.Aiming) return;
                        var bp = t.transform.position;
                        var from = k.transform.position;
                        var dd = from - bp; dd.y = 0f;
                        k.EndInvestigation();
                        if (dd.magnitude <= 10.5f) { k.BeginAim(t, 4f, rt.AttackerFires); return; }
                        var stand = bp + (dd.sqrMagnitude > 1f ? dd.normalized : Vector3.right) * 9f;
                        if (Level.WalkableLine(from, stand, out float sy)) { k.Attack(t, new[] { new Vector3(stand.x, sy, stand.z) }, "run", 4f, rt.AttackerFires); return; }
                        // the way the courier went: to the sled, along its route (and his run on foot), 9 m short of him
                        var way = new List<Vector3>(rt.P("m24_to_sled"));
                        bool ran = runUsed != null && (runUsed[0] - bp).sqrMagnitude > 36f;
                        way.AddRange(Slice(ride, 0f, AlongPath(ride, ran ? runUsed[0] : bp)));
                        if (ran) way.AddRange(Slice(runUsed, 0f, AlongPath(runUsed, bp)));
                        var wa = way.ToArray();
                        float total = 0f;
                        for (int i = 1; i < wa.Length; i++) total += Vector3.Distance(wa[i - 1], wa[i]);
                        var ap = Slice(wa, AlongPath(wa, from), Mathf.Max(0f, total - 9f));
                        if (ap.Count > 0 && (ap[ap.Count - 1] - from).sqrMagnitude > 4f) k.Attack(t, ap.ToArray(), "run", 4f, rt.AttackerFires);
                        else if (dd.magnitude < 30f) k.BeginAim(t, 4f, rt.AttackerFires);
                    };
                    // down (a bullet in the leg, or a dart): our snowcat comes for him along the sled's route (clear of
                    // buildings) and stops a few metres short of him — or where his run leaves the route (the team walks
                    // the rest)
                    bool sent = false;
                    Action pickup = () =>
                    {
                        if (sent || t.Dead) return;
                        sent = true;
                        if (mv != null && mv.Running) mv.Stop();
                        if (onSled)
                        {
                            rt.DetachFromProp(t);
                            onSled = false;
                            t.transform.position = sled.transform.TransformPoint(new Vector3(1.1f, 0f, 0f));
                            t.SetPlan(t.Sedated ? Act.Once("fall_down", 0f, true) : Act.Idle("wounded_side", 0f));
                        }
                        rt.Reveal(keep);
                        Level.I.ShowTagged("snowcat", true);
                        var him = t.transform.position;
                        var pts = new List<Vector3>();
                        if (cat != null)
                        {
                            float sFrom = AlongPath(ride, cat.transform.position);
                            bool onRoute = runUsed == null || (runUsed[0] - him).sqrMagnitude < 36f;
                            float sTo = AlongPath(ride, onRoute ? him : runUsed[0]);
                            // stop 6 m short of him (on his side of the route)
                            if (onRoute) sTo += sTo < sFrom ? 6f : -6f;
                            pts.Add(cat.transform.position);
                            pts.AddRange(Slice(ride, sFrom, Mathf.Max(0f, sTo)));
                        }
                        float len = 0f;
                        for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
                        var m = pts.Count > 1 ? rt.MoveProp("snowcat", pts.ToArray(), 7.5f, false) : null;
                        float eta = len / 6.8f + 3f;
                        float t0 = rt.Elapsed;
                        if (m != null) rt.Countdown("Ratrakın ulaşmasına", () => teamHere ? 0f : MissionRuntime.EtaOf(m, 7.5f * m.SpeedScale));
                        else rt.Countdown("Ratrakın ulaşmasına", () => teamHere ? 0f : Mathf.Max(0.1f, eta - (rt.Elapsed - t0)));
                        // Kurul's men come out one after the other; when both are stopped the snowcat floors it
                        pending = kurul.Count;
                        for (int i = 0; i < kurul.Count; i++)
                        {
                            var k = kurul[i];
                            rt.After(4f + i * 7f, () =>
                            {
                                pending--;
                                if (teamHere || t.Dead || !rt.IsPlaying || k.Dead) return;
                                k.SetHidden(false);
                                rt.Say("Spadan silahlı biri çıktı — kuryeye gidiyor!", Theme.Danger, "alert");
                                comeFor(k);
                                System.Action again = null;
                                again = () =>
                                {
                                    if (teamHere || t.Dead || !rt.IsPlaying || !able(k)) return;
                                    if (!k.Aiming) comeFor(k);
                                    rt.After(UnityEngine.Random.Range(5f, 7f), again);
                                };
                                rt.After(6f, again);
                            });
                        }
                        rt.When(() => teamHere || t.Dead || (pending <= 0 && !kurul.Exists(x => able(x) && !x.IsHidden)), () =>
                        {
                            if (teamHere || t.Dead || m == null || !m.Running) return;
                            m.SpeedScale = 2.4f;
                            rt.Radio("KONTROL", "Etraf temiz — ratrak tam gaz geliyor!", 3f);
                        });
                        rt.Radio("KONTROL", t.Sedated ? "Kurye uyudu! Ratrak yola çıktı — Kurul'un adamları spadan çıkacak, ona yaklaştırma." : "Bacağından vuruldu! Ratrak yola çıktı — Kurul'un adamları spadan çıkacak, ona yaklaştırma.", 4.5f);
                        if (m != null) m.Arrived = () =>
                        {
                            if (!rt.IsPlaying || t.Dead) return;
                            teamHere = true;
                            rt.ClearCountdown();
                            foreach (var k in kurul) if (k != null) k.StopAiming();
                            rt.Say("Ekip kuryeyi aldı", Theme.Accent, "check");
                        };
                        else rt.After(eta, () => { teamHere = true; rt.ClearCountdown(); rt.Say("Ekip kuryeyi aldı", Theme.Accent, "check"); });
                    };
                    rt.When(() => t.Wounded && t.WoundZone == BodyZone.Leg && !t.Dead, pickup);
                    rt.When(() => t.Sedated && !t.Dead, pickup);
                    rt.RadioAt(5f, "KONTROL", "Otelin spa kapısından çıkacak. Kar motoru otoparkın doğu ucunda.", 4f);
                    MountainLife(rt, "day", "w_courier");
                }
            }.Target("w_courier", "Doğan Aksu", "Kurye", "Koyu yeşil kapüşonlu, gri bere, kargo pantolon. Kar motorunda.")
             .Goal("Kuryeyi BACAĞINDAN vur — ölmemeli")
             .Goal("Önce kar motorunu durdurabilirsin (motor bloğu: 2 atış)")
             .Star(ChallengeKind.Complete, "Kuryeyi canlı yakala")
             .Star(ChallengeKind.MaxShots, "En fazla beş atış", 5)
             .Star(ChallengeKind.Custom, "Kurul'un adamları nişan alamasın", 0, r => !r.Flag("aimed")));

            // ------------------------------------------------------------------ 25. the avalanche (morning): an accident on the road
            list.Add(new MissionDef
            {
                id = "m25", number = 25, region = "mountain", title = "ÇIĞ", location = "Batı yolu — çığ yamacı", markTargets = 1,
                preset = "snow_day", wind = V(-2.4f, 0, 1.0f), reward = 15000, focus = "m25_patrol", icon = "avalanche",
                kind = "KAZA", clock = "08:40",
                introLine = "Bülent Er'in konvoyu batı yolundan gelecek. Kaza gibi görünmeli: yamaçta bir çığ patlayıcısı var.",
                brief = "Bülent Er, emniyet istihbaratının başında ve Kurul'un maaş bordrosunda. Baran'a karşı açılan her soruşturmayı o kapattırdı. Bu sabah iki araçlık konvoyla batı yolundan otele geliyor.\n\nBir emniyet müdürünün kurşunla ölmesi ülkeyi ayağa kaldırır. Kaza olmalı: yolun üstündeki yamaçta çığ kontrol patlayıcısı var. Onu doğru anda vurursan yamaç kopar ve yolu örter. Bülent ikinci araçta.",
                tip = "Çığ yola altı saniyede iner: sayaç altıya indiğinde patlayıcıyı vur. Erken vurursan konvoy kar yığınının önünde durur, geç kalırsan geçip gider; o zaman Bülent'i doğrudan vurmak zorundasın — ama kaza süsü gider.",
                introNote = "Çığ kontrol patlayıcısı: yolun üstündeki yamacın tepesinde, sarı tank",
                setup = rt =>
                {
                    var slide = rt.P("m25_slide");
                    var ava = Avalanche.Build(rt.Root, slide, 44f);
                    var conv = rt.P("m25_convoy");
                    var t = rt.AddTarget("w_fixer", conv[0] + V(0f, -20f, 0f), 80f, "Bülent Er", "Emniyet İstihbaratı", "İkinci araçta. Lacivert palto, şapka, gözlük.");
                    t.SetHidden(true);
                    t.MarkWhenHidden = true;
                    t.HiddenStatus = "İkinci araçta — batı yolundan geliyor";
                    t.HideOnFlee = true;
                    t.EscapeText = "Bülent Er otele girdi";
                    var drivers = new List<NPC>();
                    for (int i = 0; i < 2; i++)
                    {
                        var d = rt.Spawn("w_guard", conv[0] + V(0f, -20f, 0f), 80f, Role.Bodyguard, i == 0 ? "Koruma Aracı" : "Şoför");
                        d.SetHidden(true);
                        drivers.Add(d);
                    }
                    // the patrolman checking the exploder: leaves at half a minute
                    var pm = rt.Spawn("w_worker", rt.M("m25_patrol"), rt.MY("m25_patrol"), Role.Worker, "Çığ Devriyesi", "GÖREVLİ", "Kayak devriyesi — ateş etme");
                    pm.SetPlan(Act.IdleFacing("idle_look", rt.MY("m25_patrol"), 9f), Act.IdleFacing("phone", rt.MY("m25_patrol") + 40f, 7f),
                               Act.Move(rt.P("m25_patrol_off"), "walk"), Act.Hide());
                    // the exploder: one shot and the slope lets go
                    var gaz = rt.MakeShootable("gazex", "exploder", (sh, p) =>
                    {
                        if (ava.Triggered) return;
                        FX.Explosion(p + Vector3.up * 0.8f);
                        rt.Noise(p, 60f, NoiseKind.Explosion);
                        ava.Trigger();
                        rt.Cutaway(() => ava.Front + Vector3.up * 4f, p + V(60f, 30f, -70f), p + V(40f, 18f, -90f), 4.5f, "ÇIĞ");
                        rt.Say("Yamaç koptu — çığ yola iniyor!", Theme.Danger, "avalanche");
                    });
                    // the convoy: two dark SUVs from the west, Bülent in the second
                    var cars = new[] { "convoy_0", "convoy_1" };
                    var movers = new PathMover[2];
                    bool buried = false;
                    // the stretch of road the flow covers (along the convoy's route), worked out once
                    float zone0 = -1f, zone1 = -1f, along = 0f;
                    for (int i = 1; i < conv.Length; i++)
                    {
                        float seg = Vector3.Distance(conv[i - 1], conv[i]);
                        for (float u = 0f; u < seg; u += 1f)
                        {
                            if (ava.InRunout(Vector3.Lerp(conv[i - 1], conv[i], u / seg)))
                            {
                                if (zone0 < 0f) zone0 = along + u;
                                zone1 = along + u;
                            }
                        }
                        along += seg;
                    }
                    if (zone0 < 0f) { zone0 = DistAlong(conv, slide[slide.Length - 5]) - 20f; zone1 = zone0 + 40f; }
                    float zoneMid = (zone0 + zone1) * 0.5f;
                    // Bülent's car keeps its distance: it pulls up 9 m behind the first one
                    float convLen = 0f;
                    for (int i = 1; i < conv.Length; i++) convLen += Vector3.Distance(conv[i - 1], conv[i]);
                    var convBehind = Slice(conv, 0f, convLen - 9f).ToArray();
                    rt.At(5f, () =>         // V3 (device test: long waits at the start): was 18 s
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            int k = i;
                            rt.After(k * 2.6f, () =>
                            {
                                Level.I.ShowTagged(cars[k], true);                  // one after the other: they start from the same spot
                                movers[k] = rt.MoveProp(cars[k], k == 1 ? convBehind : conv, 11.5f, false);
                                if (k == 1 && movers[k] != null)
                                {
                                    rt.AttachToProp(t, cars[k]);
                                    t.transform.localPosition = V(0f, 0.4f, -0.6f);
                                    movers[k].Arrived = () =>
                                    {
                                        if (t.Incapacitated || buried) return;
                                        // at the hotel: out of the car and in through the door
                                        rt.DetachFromProp(t);
                                        t.transform.SetPositionAndRotation(rt.M("m25_exit"), Quaternion.Euler(0f, 0f, 0f));
                                        t.SetHidden(false);
                                        t.MarkWhenHidden = false;
                                        t.HiddenStatus = null;
                                        t.SetPlan(Act.IdleFacing("idle_look", 30f, 2.5f), Act.IdleFacing("phone_conv", 10f, 4f), Act.Move(rt.P("m25_to_door"), "walk_confident"), Act.Escape());
                                        rt.Say("Konvoy otele vardı — Bülent arabadan iniyor!", Theme.Danger, "car");
                                    };
                                    // until Bülent's car is in the middle of the run-out: the flow needs about six seconds
                                    rt.Countdown("Konvoyun yamaca ulaşmasına", () =>
                                    {
                                        var m = movers[1];
                                        if (m == null || !m.Running || buried || ava.Triggered) return 0f;
                                        return Mathf.Max(0f, (zoneMid - m.Travelled) / 11.5f);
                                    });
                                }
                                if (k < drivers.Count) rt.AttachToProp(drivers[k], cars[k]);
                            });
                        }
                        rt.Radio("KONTROL", "Konvoy batı yolunda. Yamacın altındaki virajdan geçecekler.", 4f);
                    });
                    // the flow reaches the road: a car in the run-out is buried; one that has not got there yet stops short
                    // of the debris and Bülent gets out to look (a plain shot then — no accident); one past it got through.
                    // (Deaths are dealt with on the next frame, not inside this check.)
                    var resolved = new bool[2];
                    // the road ahead is blocked: the car stops, Bülent gets out to look (a plain shot now — no accident)
                    Action<int, GameObject, PathMover> StopShort = (i, car, m) =>
                    {
                        resolved[i] = true;
                        m.Stop();
                        if (i == 1 && !t.Incapacitated)
                        {
                            rt.DetachFromProp(t);
                            t.transform.SetPositionAndRotation(car.transform.TransformPoint(new Vector3(1.5f, 0f, 0.3f)), car.transform.rotation);
                            t.SetHidden(false);
                            t.MarkWhenHidden = false;
                            t.HiddenStatus = null;
                            float y = car.transform.eulerAngles.y;
                            t.SetPlan(Act.IdleFacing("idle_look", y, 3f), Act.IdleFacing("phone_conv", y + 20f, 9f), Act.IdleFacing("argue", y - 30f, 5f), Act.Loop(1));
                            rt.Radio("KONTROL", "Çığ yolu kapattı, konvoy durdu. Bülent araçtan indi — kaza olmadı ama fırsat bu.", 5f);
                        }
                        if (i < drivers.Count && !drivers[i].Incapacitated)
                        {
                            var d = drivers[i];
                            rt.DetachFromProp(d);
                            d.transform.SetPositionAndRotation(car.transform.TransformPoint(new Vector3(-1.5f, 0f, 0.6f)), car.transform.rotation);
                            d.SetHidden(false);
                            d.SetPlan(Act.IdleFacing("alert", car.transform.eulerAngles.y, 0f));
                        }
                    };
                    rt.When(() =>
                    {
                        if (!ava.Triggered) return false;
                        for (int i = 0; i < 2; i++)
                        {
                            if (resolved[i]) continue;
                            var car = Level.I.Tagged(cars[i]);
                            var m = movers[i];
                            if (car == null || !car.activeSelf || m == null) continue;
                            if (!ava.Done && ava.Buries(car.transform.position))
                            {
                                resolved[i] = true;
                                m.Stop();
                                car.transform.position += V(0f, -1.1f, 0f);
                                car.transform.rotation *= Quaternion.Euler(6f, 18f, 9f);
                                int k = i;
                                if (k < drivers.Count) rt.After(0f, () => { if (!drivers[k].Dead) drivers[k].Kill(drivers[k].transform.position, Vector3.down, null, false, false); });
                                if (k == 1 && !t.Dead)
                                {
                                    buried = true;
                                    rt.SetFlag("accident");
                                    rt.After(0f, () => { if (!t.Dead) t.Kill(t.transform.position, Vector3.down, null, false, true); });
                                }
                                continue;
                            }
                            if (m.Travelled > zone1 + 3f) { resolved[i] = true; continue; }          // through before it came down
                            bool coming = ava.Done || ava.FrontS > ava.Total * 0.5f;
                            bool blocked = coming && m.Running && m.Travelled < zone0 - 1f && m.Travelled > zone0 - 16f;
                            // never into the back of the first car (stopped short of the slide, or buried in front of him)
                            if (i == 1 && m.Running && movers[0] != null && !movers[0].Running && movers[0].Travelled > m.Travelled &&
                                m.Travelled > movers[0].Travelled - 9f) blocked = true;
                            if (blocked) StopShort(i, car, m);
                        }
                        return resolved[0] && resolved[1];
                    }, () =>
                    {
                        rt.ClearCountdown();
                        if (!buried && !t.Incapacitated && movers[1] != null && movers[1].Running)
                            rt.Radio("KONTROL", "Çığ boşa gitti! Konvoy otele devam ediyor — Bülent arabadan inince fırsatın var.", 5f);
                    });
                    rt.When(() => pm.Dead, () => rt.SetFlag("patrol_hurt"));
                    rt.RadioAt(6f, "KONTROL", "Tepedeki adam kayak devriyesi, patlayıcıyı kontrol ediyor. O oradayken tetikleme.", 4.5f);
                    rt.RadioAt(20f, "KONTROL", "Çığ yamaçtan yola altı saniyede iner. Sayaç altıya indiği an patlayıcıyı vur.", 4.5f);
                    MountainLife(rt, "morning", "w_fixer", "w_worker", "w_guard");
                }
            }.Target("w_fixer", "Bülent Er", "Emniyet İstihbaratı", "İkinci araçta. Lacivert palto, şapka, gözlük.")
             .Goal("Bülent Er'i etkisiz hale getir — kaza gibi görünmeli")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.Custom, "Kaza süsü ver: çığla", 0, r => r.Flag("accident"))
             .Star(ChallengeKind.Custom, "Çığ devriyesine zarar verme", 0, r => !r.Flag("patrol_hurt") && r.CiviliansKilled == 0));

            // ------------------------------------------------------------------ 26. the ski instructor (day): keep the witness alive
            list.Add(new MissionDef
            {
                id = "m26", number = 26, region = "mountain", title = "KAYAK HOCASI", location = "Acemi pisti",
                preset = "snow_day", wind = V(1.4f, 0, 1.8f), reward = 15500, focus = "m26_elif", icon = "ski",
                kind = "KORUMA", clock = "11:30",
                introLine = "Elif Demir yarın mahkemede Kurul'a karşı tanıklık edecek. Bugün onu öldürmeye üç kişi geliyor.",
                brief = "Kayak hocası Elif Demir, geçen kış teleferik makinisti Hasan Usta'nın Kurul'un adamlarınca öldürülüşünü gördü. Yarın mahkemede tanıklık edecek; Kurul onu bugün susturmak istiyor.\n\nElif acemi pistinde ders veriyor. Üç tetikçi var: istasyonun çatısında bir nişancı, otelden yürüyerek gelen biri ve kar motoruyla gelecek biri. Jandarma 11:33'te geliyor. O zamana kadar onu hayatta tut.",
                tip = "Nişancıyı önce bul: teleferik istasyonunun düz çatısında, kar kadar beyaz. Ateş ettiğin an namlu alevini görür ve sana döner. Silah çeken birini kolundan vurursan silahını düşürür.",
                gearHint = "Susturucu nişancının seni görmesini geciktirir",
                introNote = "İstihbarat: üç tetikçi — biri yüksekte",
                maxCivilians = 1,
                setup = rt =>
                {
                    var e = rt.AddProtected("w_instructor", rt.M("m26_elif"), rt.MY("m26_elif"), "Elif Demir", "TANIK", "Kayak hocası — koru");
                    e.CanFlee = false;
                    float ey = rt.MY("m26_elif");
                    e.SetPlan(Act.IdleFacing("talk_open", ey, 9f), Act.IdleFacing("wave_big", ey, 3f), Act.IdleFacing("talk", ey, 8f),
                              Act.IdleFacing("signal", ey + 30f, 4f), Act.IdleFacing("talk_passion", ey, 8f), Act.Loop(0));
                    bool policeHere = false;
                    NPC sn = null, sl = null, wk = null;
                    Func<NPC, bool> stopped = h => h == null || h.Dead || h.Sedated || h.Down || (h.Wounded && h.WoundZone == BodyZone.Arm);
                    Func<bool> allStopped = () => stopped(sn) && stopped(sl) && (stopped(wk) || wk.Escaped);
                    rt.Protect(e, "Elif'i jandarma gelene kadar koru", () => policeHere || allStopped());
                    // her students
                    var studs = new[] { "w_ski_f2", "w_ski_m1", "w_lodge_f", "w_ski_m2", "w_ski_f1" };
                    for (int i = 0; i < 5; i++)
                    {
                        var s = rt.Spawn(studs[i], rt.M("m26_student_" + i), rt.MY("m26_student_" + i), Role.Civilian, "Öğrenci");
                        rt.Idler(s, rt.MY("m26_student_" + i), "idle", "idle_look", "idle_shift", "cheer");
                    }
                    // killer 1: the marksman lying on the station roof
                    sn = rt.AddHitman("w_hit2", rt.M("m26_sniper"), rt.MY("m26_sniper"), "Çatıdaki Nişancı", "Haki yeşil palto, kalpak — teleferik istasyonunun çatısında");
                    sn.CanFlee = false;
                    sn.SightRange = 0f;
                    var mk = new Marksman(rt, sn, "rifle_kneel", rt.M("m26_elif") + V(0f, 0.8f, 0f), 30f, false);
                    Action<NPC> rifleShot = n =>
                    {
                        if (!rt.IsPlaying || e.Dead) return;
                        mk.Fire(e.ChestPoint);
                        if (!e.Dead) e.Kill(e.ChestPoint, (e.ChestPoint - mk.Muzzle).normalized * 2f, null, false, false);
                        rt.Noise(e.transform.position, 45f, NoiseKind.Explosion);
                    };
                    // (V3 device test: long waits) the whole contract runs tighter: the marksman rises at 50 s (was 70)
                    rt.When(() => rt.Elapsed >= 50f && mk.Free && !e.Dead && !policeHere, () =>
                    {
                        mk.AimAt(e, 9f, rifleShot);
                        rt.SetFlag("sniper_aimed");
                        rt.Radio("KONTROL", "İstasyonun çatısında biri doğruldu — Elif'e nişan alıyor!", 3.5f);
                    });
                    mk.WatchPlayerShots("Çatıdaki nişancı seni vurdu");
                    // killer 2: walks up from the hotel, draws at a few metres
                    var wpath = rt.P("m26_walker");
                    wk = rt.AddHitman("w_guard2", wpath[0], 0f, "Yürüyen Adam", "Siyah kapüşonlu, siyah bere, sakallı");
                    wk.SetPlan(Act.IdleFacing("phone", 0f, 14f), Act.Move(wpath, "walk", 0.95f), Act.Do(() => wk.Attack(e, null, "walk", 4f, rt.AttackerFires)));
                    // killer 3: the snowmobile from the east
                    Level.I.ShowTagged("hit_sled", true);
                    var sledRide = rt.P("m26_sled");
                    sl = rt.AddHitman("w_hit1", sledRide[0], 270f, "Kar Motorlu Adam", "Gri kapüşonlu, siyah bere, spor gözlük — kar motorunda");
                    sl.SetHidden(true);
                    rt.At(28f, () =>
                    {
                        if (stopped(sl)) return;
                        var sledGo = Level.I.Tagged("hit_sled");
                        var m = rt.MoveProp("hit_sled", sledRide, 7.5f, false);     // test 4: at 9 m/s over the far slope he was on Elif before a fair shot
                        if (sledGo != null)
                        {
                            sl.transform.SetPositionAndRotation(sledGo.transform.TransformPoint(new Vector3(0f, 0.32f, -0.25f)), sledGo.transform.rotation);
                            rt.AttachToProp(sl, "hit_sled");
                        }
                        sl.SetHidden(false);
                        sl.CanFlee = false;
                        sl.SetPlan(Act.Idle("sit_idle", 0f));
                        Sfx.PlayAt(Sfx.Snowmobile, sledRide[0], 0.8f);
                        rt.Radio("KONTROL", "Doğudan bir kar motoru geliyor, hızlı!", 3.5f);
                        if (m != null) m.Arrived = () =>
                        {
                            if (stopped(sl) || e.Dead) return;
                            rt.DetachFromProp(sl);
                            sl.transform.position = sledGo.transform.TransformPoint(new Vector3(1.0f, 0f, 0f));
                            sl.CanFlee = true;
                            rt.Say("Kar motorlu indi — Elif'e koşuyor!", Theme.Danger, "snowmobile");
                            sl.Attack(e, new[] { rt.M("m26_elif") + (sl.transform.position - rt.M("m26_elif")).normalized * 9f }, "run", 5f, rt.AttackerFires);
                        };
                    });
                    var bonus = rt.Custom("Üç tetikçiyi de durdur", allStopped, null, null, true);
                    bonus.liveText = () => "Üç tetikçiyi de durdur  " + ((stopped(sn) ? 1 : 0) + (stopped(sl) ? 1 : 0) + (stopped(wk) ? 1 : 0)) + "/3";
                    rt.When(() => stopped(sn), () => { if (!rt.Flag("sniper_aimed") && !rt.Flag("spotted")) rt.SetFlag("sniper_first"); });
                    // the gendarmes come up the road
                    const float policeAt = 120f;      // V3: was 155 s
                    rt.Countdown("Jandarmanın gelmesine", policeAt);
                    // the patrol car comes up the road so that it pulls up at the slope when the clock runs out
                    var cop = rt.P("m26_police");
                    float copLen = 0f;
                    for (int i = 1; i < cop.Length; i++) copLen += Vector3.Distance(cop[i - 1], cop[i]);
                    rt.At(Mathf.Max(5f, policeAt - copLen / 14f - 6f), () =>
                    {
                        Level.I.ShowTagged("police_car", true);
                        rt.MoveProp("police_car", cop, 14f, false);
                    });
                    rt.At(policeAt, () => { policeHere = true; rt.ClearCountdown(); rt.Say("Jandarma geldi — Elif güvende", Theme.Accent, "shield"); });
                    rt.RadioAt(6f, "KONTROL", "Üç tetikçi var. Yüksekteki önce: istasyonun çatısını tara.", 4.5f);
                    MountainLife(rt, "day", "w_instructor", "w_hit1", "w_hit2", "w_guard2");
                }
            }.Target("w_instructor", "Elif Demir", "Tanık", "Kırmızı mont, beyaz bere, at kuyruğu. Acemi pistinde ders veriyor.")
             .Goal("Elif Demir'i jandarma gelene kadar koru")
             .Goal("Üç tetikçiyi bul ve durdur (bonus)")
             .Star(ChallengeKind.Complete, "Tanığı koru")
             .Star(ChallengeKind.Custom, "Nişancıyı nişan alamadan durdur", 0, r => r.Flag("sniper_first"))
             .Star(ChallengeKind.NoCivilians, "Öğrencilere zarar verme"));

            // ------------------------------------------------------------------ 27. the blizzard: the fixer in the whiteout
            list.Add(new MissionDef
            {
                id = "m27", number = 27, region = "mountain", title = "TİPİ", location = "Köy — tipi altında", requires = new[] { "thermal" },
                preset = "blizzard", wind = V(8.5f, 0, 2.5f), reward = 16000, focus = "m27_start", icon = "blizzard",
                kind = "SUİKAST", clock = "16:20",
                introLine = "Orhan Sezer tipiyi fırsat bilip ratrakla dağı aşacak. Göz gözü görmüyor — termal dürbün hazır olsun.",
                brief = "Orhan Sezer, Kurul'un temizlikçisi: tanıkları ortadan kaldıran, delilleri yok eden adam. Elif'e gönderilen tetikçileri o tutmuştu. Tipiyi fırsat bilip ratrakla dağı aşacak; zirvenin ötesinde bir helikopter bekliyor.\n\nOtelden çıkıp köyü yürüyerek geçecek ve istasyonun yanındaki ratraka binecek. Kar fırtınasında 200 metrenin ötesi görünmüyor; termal dürbün vücut ısısını sisin içinden gösterir. Rüzgar çok sert.",
                tip = "Termal dürbün herkesi beyaz gösterir: tarayıp kimliği oku. Rüzgar saatte 30 kilometreden fazla esiyor; nişanı rüzgarın geldiği yöne kaydır.",
                gearHint = "Termal dürbün şart",
                setup = rt =>
                {
                    var walk = rt.P("m27_walk");
                    var t = rt.AddTarget("w_fixer", walk[0], 200f);
                    t.HideOnFlee = true;
                    t.FleePath = new[] { rt.M("gondola_stairs"), rt.M("gondola_hall") };
                    t.EscapeText = "Orhan istasyona kaçtı";
                    var g0 = rt.Spawn("w_guard", walk[0] + V(-1.2f, 0f, 1f), 200f, Role.Bodyguard, "Koruma");
                    var g1 = rt.Spawn("w_guard", walk[0] + V(1.2f, 0f, 1f), 200f, Role.Bodyguard, "Koruma");
                    t.SetPlan(Act.IdleFacing("idle_look", 200f, 6f), Act.Move(walk, "walk_careful", 1.0f), Act.Do(() => Board(rt, t, 0.42f)), Act.Idle("sit_idle", 0f));
                    g0.SetPlan(Act.IdleFacing("guard_pose", 200f, 5.5f), Act.Move(MissionRuntime.Offset(walk, 1.1f), "walk_careful", 1.0f), Act.Do(() => Board(rt, g0, -0.42f)), Act.Idle("sit_idle", 0f));
                    g1.SetPlan(Act.IdleFacing("guard_pose", 200f, 6.5f), Act.Move(MissionRuntime.Offset(walk, -1.1f), "walk_careful", 1.0f), Act.Hide());
                    rt.When(() => !t.Incapacitated && t.transform.parent != null && t.transform.parent.name == "snowcat", () =>
                    {
                        t.MarkWhenHidden = true;
                        var m = rt.MoveProp("snowcat", rt.P("m27_climb"), 4.2f, false);
                        rt.Say("Ratrak hareket etti — dağa tırmanıyor!", Theme.Danger, "snowmobile");
                        if (m != null)
                        {
                            rt.Countdown("Ratrakın sırtı aşmasına", () => m.Running ? (m.Length - m.Travelled) / 4.2f : 0f);
                            m.Arrived = () => { if (!t.Incapacitated) { t.EscapeText = "Ratrak sırtı aştı"; t.ForceEscape(); } };
                        }
                    });
                    rt.When(() => t.Dead, () => rt.ClearCountdown());
                    // people hurrying through the storm (all warm on the thermal scope)
                    var who = new[] { "w_worker", "w_lodge_m", "w_ski_m2", "w_ski_f1" };
                    for (int i = 0; i < 4; i++)
                    {
                        var n = rt.Spawn(who[i], rt.M("m27_person_" + i), rt.MY("m27_person_" + i), Role.Civilian, "Yaya");
                        var loop = new[] { rt.M("m27_person_" + i), rt.M("m27_person_" + i + "_b") };
                        rt.PingPong(n, loop, "walk_careful", 1.0f, "idle_look", 3f);
                    }
                    // without thermal: the hard way
                    rt.When(() => VisionFX.Mode == VisionMode.Thermal, () => rt.SetFlag("thermal"));
                    ChimneySmoke.SetAll(0.25f);
                    rt.RadioAt(6f, "KONTROL", "Görüş 200 metrenin altında. Termal dürbünle sıcak noktaları ara, sonra kimliği tara.", 4.5f);
                    rt.RadioAt(30f, "KONTROL", "Rüzgar doğudan sert esiyor. Uzak atışta mermi metrelerce sürüklenir.", 4.5f);
                    MountainLife(rt, "storm", "w_fixer", "w_guard");
                }
            }.Target("w_fixer", "Orhan Sezer", "Temizlikçi", "Lacivert uzun palto, kalpak, gözlük. İki korumasıyla.")
             .Goal("Orhan Sezer'i ratrak sırtı aşmadan etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Sözleşmeyi tamamla")
             .Star(ChallengeKind.MaxShots, "Tek atış", 1)
             .Star(ChallengeKind.Custom, "Termal dürbün kullanmadan", 0, r => !r.Flag("thermal")));

            // ------------------------------------------------------------------ 28. the summit (New Year's Eve): fireworks
            list.Add(new MissionDef
            {
                id = "m28", number = 28, region = "mountain", title = "ZİRVE", location = "Kartal Dağ Oteli — yılbaşı gecesi",
                preset = "snow_night", wind = V(0.6f, 0, 1.2f), reward = 20000, focus = "m28_patron", icon = "firework",
                kind = "SUİKAST", clock = "23:59",
                introLine = "Gece yarısı havai fişekler başlayınca vadi gürültüyle dolacak. Patlamalar senin silahının sesini yutar.",
                brief = "Kurul'dan geriye bir kişi kaldı: Nedim Kurt — Patron. Baran'ı yaratan, yıllarca ayakta tutan ve her şeyin hesabını tutan adam.\n\nYılbaşı gecesi otelin önündeki terasta misafirleriyle. Gece yarısı gölün üstünde havai fişek gösterisi var: Patron izlemek için terasın ucuna yürüyecek. Patlamalar yankılanırken atılan bir kurşunu kimse duymaz. Gösteri bitince içeri girer — bir daha çıkmaz.\n\nPatron tedbirli: güvenlik şefi terasın önünden gece dürbünüyle yamaçları tarıyor. Seni görürse korumalar yerini öğrenir.",
                tip = "Havai fişek patlamasının sesi yuvaya ulaştığı an tetiğe bas: o anki atışı kimse duymaz. Patron'un iki koruması var; kalabalığın içinde ateş etme. Güvenlik şefinin dürbünü sana dönünce eğil (SİPER).",
                introNote = "Gece yarısı: göl üstünde havai fişek",
                maxCivilians = 0,
                setup = rt =>
                {
                    rt.WanderIdlers = false;                          // a packed terrace at midnight
                    var t = rt.AddTarget("w_patron", rt.M("m28_patron"), rt.MY("m28_patron"));
                    t.HideOnFlee = true;
                    t.FleePath = rt.P("m28_inside");
                    t.EscapeText = "Patron içeri girdi";
                    float py = rt.MY("m28_patron");
                    t.SetPlan(Act.IdleFacing("talk", py, 10f), Act.IdleFacing("drink_stand", py - 30f, 8f), Act.IdleFacing("talk_passion", py, 9f),
                              Act.IdleFacing("chat", py + 20f, 0f));
                    var g0 = rt.Spawn("w_guard", rt.M("m28_guard_0"), rt.MY("m28_guard_0"), Role.Bodyguard, "Koruma");
                    var g1 = rt.Spawn("w_guard2", rt.M("m28_guard_1"), rt.MY("m28_guard_1"), Role.Bodyguard, "Koruma");
                    rt.Idler(g0, rt.MY("m28_guard_0"), "guard_pose", "idle_look");
                    rt.Idler(g1, rt.MY("m28_guard_1"), "guard_pose", "idle_look");
                    // the party
                    string[] men = { "gala_m", "gala_m2", "w_lodge_m", "waiter" };
                    string[] women = { "gala_f", "gala_f2", "w_lodge_f", "w_ski_f1" };
                    var rnd = new System.Random(2828);
                    var guests = new List<NPC>();
                    for (int i = 0; i < 17; i++)
                    {
                        string mk = "m28_guest_" + i;
                        if (!Level.I.HasMarker(mk)) break;
                        string ch = i % 2 == 0 ? men[rnd.Next(men.Length - 1)] : women[rnd.Next(women.Length)];
                        var g = rt.Spawn(ch, rt.M(mk), rt.MY(mk), Role.Civilian, "Misafir");
                        rt.Idler(g, rt.MY(mk), "talk", "drink_stand", "chat", "drink", "idle_shift");
                        guests.Add(g);
                    }
                    var waiter = rt.Spawn("waiter", rt.P("m28_waiter")[0], 0f, Role.Worker, "Garson", "GÖREVLİ");
                    rt.Patrol(waiter, rt.P("m28_waiter"), "walk_carry", 0.85f, "pay", 4f);
                    // midnight: the countdown, the fireworks over the frozen lake; the Patron walks out to watch them
                    var fw = Fireworks.Build(rt.Root, rt.M("m28_launch"));
                    rt.NoiseMask = () => fw.Masking;
                    rt.Countdown("Gece yarısına", 46f);
                    rt.At(38f, () => rt.Radio("MİSAFİRLER", "On! Dokuz! Sekiz! Yedi...", 3f));
                    rt.At(46f, () =>
                    {
                        rt.ClearCountdown();
                        fw.Show(62f);
                        Sfx.Play(Sfx.Cheer, 0.55f);
                        rt.Say("Mutlu yıllar! — havai fişekler başladı", Theme.Accent, "firework");
                        foreach (var g in guests)
                        {
                            if (g == null || g.Incapacitated || g.Panicking) continue;
                            float y = MissionRuntime.Yaw(g.transform.position, rt.M("m28_launch"));
                            g.EndInvestigation();
                            g.SetPlan(Act.IdleFacing("cheer", y, 3f + (float)rnd.NextDouble() * 2f), Act.IdleFacing("idle_look", y, 6f), Act.IdleFacing("wave_big", y, 3f),
                                      Act.IdleFacing("cheer_up", y, 5f), Act.IdleFacing("idle_look", y, 8f), Act.Loop(1));
                        }
                        if (!t.Incapacitated && !t.Panicking)
                        {
                            float vy = rt.MY("m28_view");
                            t.EndInvestigation();
                            t.SetPlan(Act.IdleFacing("cheer", py, 2.5f), Act.Move(rt.P("m28_to_view"), "walk_slow"), Act.IdleFacing("idle_look", vy, 9f),
                                      Act.IdleFacing("drink_stand", vy, 8f), Act.IdleFacing("talk", vy - 40f, 7f), Act.IdleFacing("idle_look", vy, 0f));
                            foreach (var g in new[] { g0, g1 })
                                if (g != null && !g.Incapacitated && !g.Alerted)
                                {
                                    g.EndInvestigation();
                                    g.SetPlan(Act.Idle("idle_look", 2f), Act.Move(MissionRuntime.Offset(rt.P("m28_to_view"), g == g0 ? 2.2f : -2.4f), "walk"), Act.IdleFacing("guard_pose", MissionRuntime.Yaw(rt.M("m28_view"), rt.M("m28_patron")), 0f));
                                }
                        }
                        rt.Countdown("Gösterinin bitmesine", 62f);
                    });
                    rt.At(108f, () =>
                    {
                        rt.ClearCountdown();
                        if (t.Incapacitated || t.Escaped) return;
                        t.EndInvestigation();
                        rt.Say("Gösteri bitti — Patron içeri dönüyor!", Theme.Danger, "firework");
                        t.SetPlan(Act.Move(MissionRuntime.From(rt.P("m28_inside"), t.transform.position), "walk"), Act.Escape());
                    });
                    rt.RadioAt(8f, "KONTROL", "Kalabalığın ortasında. Gece yarısı gösteriyi izlemek için terasın ucuna yürüyecek.", 4.5f);
                    rt.RadioAt(52f, "KONTROL", "Patlamanın sesi sana ulaştığı an ateş et — o anki atışı kimse duymaz.", 4.5f);
                    // the Patron's security chief at the front of the terrace: night glasses on the slopes across the valley
                    // (the Kurul has been losing people all week — he knows what to look for)
                    var lookFrom = RoofSpot(rt.M("m28_guard_0"), rt.NestPoint) ?? RoofSpot(rt.M("m28_view"), rt.NestPoint);
                    if (lookFrom.HasValue)
                    {
                        var lk = rt.Spawn("w_guard", lookFrom.Value, MissionRuntime.Yaw(lookFrom.Value, rt.NestPoint), Role.Guard, "Güvenlik Şefi", "GÖZCÜ",
                                          "Gece dürbünüyle yamaçları tarıyor — seni görürse yerini bildirir");
                        Spotter.Make(lk, MissionRuntime.Yaw(lookFrom.Value, rt.NestPoint), 76f, 12f);
                        rt.At(16f, () =>
                        {
                            if (lk.Incapacitated || lk.Lookout == null || lk.Lookout.Reporting || lk.Lookout.Reported) return;
                            rt.Radio("KONTROL", "Terasın önünde gece dürbünlü biri var: Patron'un güvenlik şefi. Dürbün sana dönünce eğil — gece yarısından önce atış sesi duyulur.", 5f);
                        });
                    }
                    MountainLife(rt, "party", "w_patron", "gala_m", "gala_f", "gala_m2", "gala_f2", "waiter", "w_guard", "w_guard2");
                }
            }.Target("w_patron", "Nedim Kurt", "Patron", "Beyaz smokin ceket, papyon, kır sakal. Otel terasında.")
             .Goal("Nedim Kurt'u gösteri bitmeden etkisiz hale getir")
             .Star(ChallengeKind.Complete, "Kurul'u bitir")
             .Star(ChallengeKind.Custom, "Her atışı havai fişeklerin sesine gizle", 0, r => !r.Flag("unmasked"))
             .Star(ChallengeKind.MaxShots, "Tek atış", 1));
        }

        /// <summary>The snowcat waiting by the station (contract 27): climb into the cab (seat <paramref name="side"/>:
        /// metres right of the centre line, the two seats side by side).</summary>
        static void Board(MissionRuntime rt, NPC n, float side)
        {
            if (n == null || n.Incapacitated || n.Escaped || n.Panicking) return;
            var cat = Level.I.Tagged("snowcat");
            if (cat == null) return;
            n.transform.SetPositionAndRotation(cat.transform.TransformPoint(new Vector3(side, 1.6f, 0.6f)), cat.transform.rotation);
            rt.AttachToProp(n, "snowcat");
            n.CanFlee = false;
        }

        /// <summary>Distance along a path to the point of it nearest <paramref name="p"/> (projected onto the segments).</summary>
        static float AlongPath(Vector3[] path, Vector3 p)
        {
            float acc = 0f, best = float.MaxValue, at = 0f;
            for (int i = 1; i < path.Length; i++)
            {
                var a = path[i - 1];
                var b = path[i];
                var ab = b - a; ab.y = 0f;
                float L = ab.magnitude;
                var ap = p - a; ap.y = 0f;
                float u = L > 1e-4f ? Mathf.Clamp(Vector3.Dot(ap, ab) / (L * L), 0f, 1f) : 0f;
                var q = a + (b - a) * u - p; q.y = 0f;
                float d = q.sqrMagnitude;
                if (d < best) { best = d; at = acc + u * Vector3.Distance(a, b); }
                acc += Vector3.Distance(a, b);
            }
            return at;
        }

        /// <summary>The stretch of a path between two distances along it (either way round), end points included.</summary>
        static List<Vector3> Slice(Vector3[] path, float s0, float s1)
        {
            var o = new List<Vector3>();
            if (path == null || path.Length == 0) return o;
            var cum = new float[path.Length];
            for (int i = 1; i < path.Length; i++) cum[i] = cum[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            Func<float, Vector3> at = s =>
            {
                s = Mathf.Clamp(s, 0f, cum[cum.Length - 1]);
                int i = 1;
                while (i < path.Length - 1 && cum[i] < s) i++;
                if (path.Length == 1) return path[0];
                float seg = cum[i] - cum[i - 1];
                return seg > 1e-5f ? Vector3.Lerp(path[i - 1], path[i], (s - cum[i - 1]) / seg) : path[i];
            };
            o.Add(at(s0));
            if (s1 >= s0) { for (int i = 0; i < path.Length; i++) if (cum[i] > s0 && cum[i] < s1) o.Add(path[i]); }
            else { for (int i = path.Length - 1; i >= 0; i--) if (cum[i] < s0 && cum[i] > s1) o.Add(path[i]); }
            o.Add(at(s1));
            return o;
        }

        /// <summary>Distance along a path to the path point nearest <paramref name="p"/>.</summary>
        static float DistAlong(Vector3[] path, Vector3 p)
        {
            float acc = 0f, best = float.MaxValue, at = 0f;
            for (int i = 0; i < path.Length; i++)
            {
                if (i > 0) acc += Vector3.Distance(path[i - 1], path[i]);
                float d = (path[i] - p).sqrMagnitude;
                if (d < best) { best = d; at = acc; }
            }
            return at;
        }

        /// <summary>
        /// Life at the resort, by the time of day: skiers walking between the hotel, the lifts and the lake, the queue at
        /// the gondola, the lift man, ice fishermen on the lake, guests at the après-ski kiosk; at night a few people on
        /// the way home; in a storm only the ones who have to be out. <paramref name="avoid"/>: characters of the contract.
        /// </summary>
        static void MountainLife(MissionRuntime rt, string mood, params string[] avoid)
        {
            var lv = Level.I;
            if (lv == null) return;
            var rnd = new System.Random(rt.Def.number * 37 + 11);
            bool day = mood == "day" || mood == "morning";
            bool night = mood == "night" || mood == "party";
            bool storm = mood == "storm";
            int walkers = storm ? 0 : (night ? 3 : (mood == "sunset" ? 6 : 9));
            string[] lanes = { "walk_street", "walk_station", "walk_lake", "walk_chalets", "walk_street", "walk_chalets", "walk_station", "walk_street", "walk_lake" };
            for (int i = 0; i < walkers; i++)
            {
                string lane = lanes[i % lanes.Length];
                if (!lv.HasPath(lane)) continue;
                var route = rt.P(lane);
                if (rnd.NextDouble() < 0.5) route = MissionRuntime.Rev(route);
                var n = rt.Spawn(PickChar(rnd, i % 2 == 0 ? SnowMen : SnowWomen, avoid), route[0], 0f, Role.Civilian, "Kayakçı");
                Stroll(rt, n, route, (float)rnd.NextDouble() * 0.85f, "walk", 0.8f + (float)rnd.NextDouble() * 0.25f, 5f + (float)rnd.NextDouble() * 14f);
            }
            if (day)
            {
                // the gondola queue (people in a queue keep their place) and the lift man
                bool wander = rt.WanderIdlers;
                rt.WanderIdlers = false;
                for (int i = 0; i < 5; i++)
                {
                    string mk = "gondola_queue_" + i;
                    if (!lv.HasMarker(mk) || rnd.NextDouble() < 0.3 || rt.Def.id == "m21" && i < 2) continue;
                    var q = rt.Spawn(PickChar(rnd, i % 2 == 0 ? SnowWomen : SnowMen, avoid), rt.M(mk), rt.MY(mk), Role.Civilian, "Kayakçı");
                    rt.Idler(q, rt.MY(mk), "idle_wait", "idle_look", "phone", "idle_shift");
                }
                rt.WanderIdlers = wander;
                if (Array.IndexOf(avoid ?? new string[0], "w_worker") < 0 && lv.HasMarker("gondola_hall"))
                {
                    var op = rt.Spawn("w_worker", rt.M("gondola_hall"), lv.MarkerYaw("gondola_hall"), Role.Worker, "Teleferik Görevlisi", "GÖREVLİ");
                    rt.Idler(op, lv.MarkerYaw("gondola_hall"), "idle_wait", "signal", "idle_look", "idle_arms");
                }
            }
            if (day || mood == "sunset")
            {
                // ice fishermen sitting by their holes
                for (int i = 0; i < 3; i++)
                {
                    string mk = "ice_fisher_" + i;
                    if (!lv.HasMarker(mk) || rnd.NextDouble() < 0.25) continue;
                    var f = rt.Spawn(i == 0 ? "w_fisher" : PickChar(rnd, new[] { "w_ski_m2", "w_lodge_m", "w_fisher" }, avoid), rt.M(mk), rt.MY(mk), Role.Civilian, "Balıkçı");
                    rt.Idler(f, rt.MY(mk), "sit_idle", "sit_idle", "sit_drink");
                }
            }
            if (!storm && !night && lv.HasMarker("m21_coffee") && rt.Def.id != "m21")
            {
                var a = rt.Spawn(PickChar(rnd, SnowWomen, avoid), rt.M("m21_coffee"), 180f, Role.Civilian, "Kayakçı");
                rt.Idler(a, 180f, "drink", "talk", "phone");
                var b = rt.Spawn(PickChar(rnd, SnowMen, avoid), rt.M("m21_guard_1"), 200f, Role.Civilian, "Kayakçı");
                rt.Idler(b, 200f, "coffee", "talk", "idle_look");
            }
        }
    }
}

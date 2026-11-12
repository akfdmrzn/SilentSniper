# Silent Contract v2 — Neler değişti

## Mağaza sürümü ve ekonomi dengesi (2 Kasım — sc_59)
- **Test modu kapandı** (`Game.TestBuild = false`): bölümler sırayla açılır, gizli "her şey açık" düğmesi yok, mağaza gerçek
  App Store'dan satar, reklamlar gerçek AdMob'dan gelir.
- **Ekonomi (tools/econ/sim2.py ile bölüm bölüm hesaplandı):** yükseltmeler ×2,6, tüfekler ×2 (SR-7 13.000, DMR 19.000,
  .338 32.000, TİTAN 52.000), gece görüşü 6.000, termal 16.500, dron 11.000, mermiler ve beceriler pahalandı.
  Sonuç: sadece oynayan, video izlemeyen oyuncu son bölümlerde önerilen gücün ~%80'inde (zorlanır); videolar + günlük ödüller
  ~%100'de tutar; elmas / paketler öne geçirir. İlk 6 bölüm cömert kalır.
- **Para açıkları kapandı:** VETERAN tekrarları ilk seferden sonra normal tekrar gibi öder; günün sözleşmesi ×3 yerine ×2.
- Kilitli tüfeği elmasla hemen alma fiyatı tüfeğin nakit fiyatına göre (SR-7 155, TİTAN 625 elmas).
- Bacak vuruşu ve vurulma düzeltmeleri (sc_57, sc_58) de bu sürümde.

## Gerçek kimlikler bağlandı (1 Kasım — sc_54)
- **RevenueCat:** SilentContract projesi. App Store ve Test Store'da 9 ürün, `no_ads` / `pack_shadow` / `pack_titan` yetkileri,
  `default` teklifi (9 paket). Oyun cihazda `appl_` anahtarıyla gerçek App Store (sandbox) satın alımı yapar.
- **AdMob:** gerçek uygulama ve reklam birimleri (geçiş + ödüllü geçiş). Ödül birimi "ödüllü geçiş" biçiminde; AdMob biçim
  uyuşmazlığı bildirirse oyun kendiliğinden "ödüllü" biçime geçer.
- **Firebase:** GoogleService-Info.plist Assets/SilentContract içinde → Analytics, Crashlytics, Remote Config açık.
- **Meta:** Facebook SDK 19 eklendi (iOS), App ID + Client Token; Info.plist'e Meta kayıtları derlemede yazılır.
- Android APK bu sürümde de mağaza/reklam/analitik olmadan test sürümü olarak çıkar.

## Gelir modeli: RevenueCat, AdMob, Firebase, Meta (31 Ekim gece — sc_52)
- **Satın alma RevenueCat ile:** 9 ürün App Store Connect'te açıldı (5 elmas paketi, Başlangıç Paketi, Reklamsız, iki silah
  paketi). Tüketilemezler (Reklamsız, GÖLGE OPERASYON, TİTAN) yeni cihazda "Geri yükle" ile geri gelir. Yarıda kalan satın alma
  (çökme, aile onayı) sonraki açılışta kendiliğinden teslim edilir; yeniden kurulumda eski paketler iki kez verilmez.
- **Mağaza:** yeni **SİLAH PAKETLERİ** sekmesi (GÖLGE OPERASYON 4,99 $: SR-7 + gece görüşü + 150 elmas; TİTAN 14,99 $:
  TİTAN .50 + termal + 400 elmas + 20 zırh delici). Alınca tüfek kuşanılır. Reklamsız 4,99 $ (+100 elmas).
- **Reklam AdMob ile:** ödüllü video yerleri: 2× kazanç, **başarısız görevde telafi** (ücretin %25'i), **cephanelikte eksik
  parayı tamamla** (açık %35'ten azsa, günde 3), **zırhlı görevde 3 zırh delici mermi**, 2× günlük ödül, **2× seviye ödülü**,
  ücretsiz elmas. Geçiş reklamı 3 başarılı görevde bir; ilk 4 görevde ve bir videodan sonraki 90 sn'de yok.
  Günde 2 geçiş reklamı görülünce menüde bir kez "Reklamlardan sıkıldın mı?" teklifi.
- **Ekonomi daha sıkı:** bitirilmiş bir görevi tekrar oynamak ücretin %40'ını öder (VETERAN tam öder), geliştirmeler %20
  pahalı, seviye ödülü nakdi azaldı. Aynı görevde 2. başarısızlıkta tüfek önerilen gücün altındaysa bir kez "GÜÇLEN" teklifi.
  Bütün oranlar Firebase Remote Config'den güncellemesiz değişir.
- **İzinler:** Avrupa'da Google izin formu (UMP), ilk görev bitince Apple'ın takip izni (ATT). Ayarlar'a "Geri yükle" ve
  "Gizlilik" düğmeleri.
- **Analitik (Firebase + Meta):** oyunun bütün önemli anları ölçülüyor: açılış, eğitim, her görevin başlangıç/bitiş/başarısızlık/
  çıkış (deneme sayısı, güç, süre, isabet), seviye, kazanılan/harcanan elmas ve nakit (nereden/nereye), silah alma/geliştirme,
  mağaza açılışı, teklifler, satın alma adımları, reklam gösterimi ve **reklam geliri** (ad_impression), ekranlar.
  Crashlytics çökmelerden önceki adımları da kaydeder.
- **iOS derleme:** In-App Purchase yeteneği, takip metni, SKAdNetwork (Google + Meta), gizlilik bildirimi otomatik. CocoaPods
  kendiliğinden kurulur; Xcode'da `.xcworkspace` açılır.

## Rakip seviyesine: kariyer, cephanelik, mağaza, gerçekçi karakterler (31 Ekim gece — sc_51)
- **Kariyer:** her görev **tecrübe (TP)** kazandırır (başarısız olsa bile biraz). Seviye atlayınca nakit + **elmas** ödülü ve
  "SEVİYE ATLADIN" kartı; rütbe adı ve rengi seviyeyle değişir (ÇAYLAK → TETİKÇİ → NİŞANCI → KESKİN NİŞANCI → GÖLGE → HAYALET →
  EFSANE). Eski kayıtlar bitirdikleri görevlerin TP'sini bir kez sessizce alır.
- **Elmas (premium para):** başlangıçta 25. Seviye ödülleri, bir göreve ilk kez S derecesi, günün sözleşmesi, günlük giriş
  ödülü ve (test sürümünde) video ile kazanılır; mağazadan alınır. Silah geliştirmenin son kademesi, kilitli tüfeği hemen
  almak ve nakit paketleri elmasla.
- **Silah geliştirme:** her tüfeğin 5 hattı × 5 kademe — HASAR (zırh delme, mermi hızı), DÜRBÜN (büyütme), DENGE (sallantı,
  geri tepme), ŞARJÖR (fişek), HIZ (doldurma, sürgü). 1-4. kademe nakit, 5. kademe elmas. Tüfeğin **GÜÇ** puanı geliştirdikçe
  artar; her görevin **önerilen gücü** var (brifing, görev kartı): altındaysan sarı/kırmızı uyarı ve "GELİŞTİR" kısayolu.
- **Zırhlı düşmanlar (zorlaşan ilerleme):** 9. görevden itibaren takviye silahlıların bir kısmı plaka yelekli (22'den sonra hepsi,
  VETERAN'da hep). Gövdeye hafif mermi durur (kıvılcım, sendeler, savaşmaya devam eder) — kafaya nişan al, zırh delici kullan ya
  da HASAR'ı geliştir. Dürbün kartında "ZIRHLI".
- **Yeni ana menü:** üstte rütbe rozeti + TP çubuğu, nakit ve elmas çipleri (+ ile mağaza), ayarlar; solda sıradaki sözleşme
  dosyası, büyük OYNA/DEVAM ET (ışık süzülmesi), 4 kutu (GÖREVLER, CEPHANELİK, MAĞAZA, DAVA DOSYASI); sağda **kuşanılan tüfek
  ışıklı sahnede dönerek** (adı, nadirlik, GÜÇ, GELİŞTİR) ve günün sözleşmesi. Günde bir **günlük ödül takvimi** (7 gün).
- **Yeni cephanelik (showroom):** solda nadirlik renkli tüfek listesi, ortada büyük döndürülebilir 3B tüfek ve GÜÇ göstergesi,
  sağda 5 geliştirme hattı (kademe çubukları, şimdiki → sonraki değer, fiyat). BECERİLER sekmesi: Sabit Nişan, Nefes Kontrolü,
  yeni **Soğukkanlılık** (ateş altında titreme azalır). Başlangıç tüfeği yeni model (ceviz dipçikli modern sürgülü).
- **Görev seçimi harita üzerinde:** bölgenin kuşbakışı haritasında her sözleşme olduğu yerde bir iğne (bitmiş: yeşil + yıldızlar,
  sıradaki: turuncu ve nabız gibi atan, kilitli: gri), kampanya rotası noktalı çizgi, ANA YUVA işareti; seçince sağda dosya
  kartı (portre, tür, yıldız, derece, mesafe, rüzgâr, zorluk, zırhlı uyarısı, önerilen güç, ödül, BRİFİNGE GİT).
- **Sonuç ekranı:** üstte kariyer çubuğu; kazanç sayılınca paralar ve elmaslar çipe uçar, TP çubuğu dolar, seviye atlanınca kart
  açılır. **2× KAZANÇ** (ödüllü video) düğmesi.
- **Mağaza:** TEKLİFLER (Başlangıç Paketi, Reklamsız, ücretsiz elmas videosu), ELMAS paketleri (80 → 7.500, bonuslu), NAKİT
  paketleri (elmasla). Satın alımları geri yükle düğmesi.
  Mağaza görselleri (elmas kesesi → kasa, para destesi → valiz, silah sandığı) Meshy ile üretildi. 3. görevden sonra
  Başlangıç Paketi bir kez teklif edilir.
- **Günlük görevler:** her gün 3 küçük iş (2 sözleşme, 3 kafadan vuruş, 250 m+ isabet, alarmsız iş, S derecesi, zırhlı
  adam...). İlerleme her görevden sayılır, menüdeki kartta AL ile elmas/nakit toplanır.
- **Reklam düzeni:** görev arası tam ekran reklam **her 3 başarılı görevde bir** (ilk 4 görevden önce hiç yok, Reklamsız paketle
  hiç yok; sayı `Ads.InterstitialEvery` ile ayarlanır). Ödüllü videolar hep isteğe bağlı. Şu an **test sağlayıcısı** var (3 sn
  yer tutucu ekran, satın almalar ücretsiz) — gerçek reklam ağı ve App Store ürünleri BENIOKU'daki adımlarla bağlanır.
- **Yükleme ekranları:** her bölgenin kendi sinematik görseli (liman gün batımı, gece şehir, karlı dağ) + yeni ipuçları.
- **Gerçekçi karakterler (Meshy):** hedefler, korumalar ve kalabalık yeni, fotogerçekçi modellerle değişti (aynı kimlik ve
  dosya tarifleri; aynı animasyon bankası). Portreler yeniden çekildi.
- Otomatik test: `bash test_sim.sh ui` sadece menü/mağaza/cephanelik/harita ekranlarını çeker (~3 dk).

## Zorluk ve ölüm düzeltmeleri (31 Ekim akşam)
- **Hata bedelli:** hedefin yakınına ıskalamak (ya da iki boş atış), alarm vermek → çevredeki binalardan **silahlı takviye**
  çıkar (2-4 kişi, kampanya ilerledikçe çok), siper alıp yuvana ateş eder; ikinci hata ikinci dalgayı getirir.
- **Görev, sana ateş eden kalmayınca biter:** hedef düştüğünde seni fark edip ateş eden silahlılar varsa görev bitmez —
  hepsi işaretlenir, görev panelinde "Sana ateş edenleri sustur · 3 kaldı" sayılır, sonradan katılanlar da eklenir. Son
  silahlı susunca "BÖLGE TEMİZ — GÖREV TAMAM". (İki dakika boyunca ulaşılamayanlar geri çekilir; sıkışma olmaz.)
- **Tek kurşunla bitmiyor:** hedef düşünce yakınındaki silahlılar yuvayı arar; 5. görevden itibaren yakında silahlı yoksa
  takviye gelir. Yerin biliniyor, ateş altındasın: siper al, yer değiştir, silahlıları tek tek sustur. Vurulursan görev başarısız.
- Iskalama tepkisi ve çıkış aşaması artık 2. görevden itibaren (eskiden 5.).
- **Ölümler:** vurulan herkes olduğu yere yığılır — dizlerinin üstüne çöker, yavaşça yıkılır ya da oturur; savrulma, takla,
  sıçrama yok (itiş neredeyse sıfır, eklemler yavaş). Ceset bir kaç saniye sonra yerinde sabitlenir.
- **Karlı haritada kaybolan cesetler:** ceset parçaları zeminin altına geçemez, zemine oturtulur.

## V2 büyük paket — oynanış, arayüz, görsel (31 Ekim)
- **Test 6 (sc_46): 25/28 başarılı, VETERAN turu S derece ve madalya, epilog ekranı çıkıyor, 2 zararsız hata.**
- **İsabet adaleti:** isabet noktası kişinin üzerindeyken mermi son 60 m'de hedefe daha çok yönlendirilir (yürüyen hedefte
  nokta "vurur" deyip mermi yere gidiyordu).
- **Denge:** 10 · Kurye — kuryeyi susturmaya gelenlerin nişan süresi 3,6 → 4,6 sn.
- Taktik harita arka planı tamamen opak; test sonuçları her turda AutoTest_arsiv'e saklanır (son 6 tur).
- **Test 5 (sc_44) ilk sonuçlar:** build temiz derlendi; sonuç dökümü, derece, fırsat penceresi (2. görevde 43 sn) çalışıyor.
- **Denge:** 12 · İğne — korumanın baygın kimyageri kaldırma süresi 4,5 → 8 sn (320 m'den iki yavaş iğneye zaman kalsın).
- **İnceleme düzeltmeleri:** tekrar oynanan karar görevinde soru yeniden sorulmaz (para iki kez alınmaz); günün sözleşmesi gün
  içinde değişmez; ekran dışındaki cesette vurulma itişi birikmez; keşif sadece menzildeki ve görünen tehditleri sayar;
  siperdeyken "görünmüyor" uyarısı gelmez; brifing başlığı düğmelerin altına girmez; sonuç sayımı ekrandan çıkınca durur.
- **Günün sözleşmesi:** ana menüde turuncu kart — her gün bitirdiğin görevlerden biri VETERAN kurallarıyla, günde bir kez
  toplam ×3 ödül. Bittiyse yenisine kalan süre yazar.
- **Cesedi suya düşürmek:** suya düşen ceset bulunmaz (alarm vermez); ekranda bildirilir.
- **Otomatik test dinleyicisi:** OTOMATIK_TEST.command bir kez açılınca, Claude her yeni sürümde testi kendisi başlatır.
- **Sonuç ekranı yenilendi:** iki sütun — solda yıldız hedefleri ve atış istatistikleri, sağda **kazanç dökümü** (sözleşme ücreti,
  yıldızlar, sessiz iş, uzak atış, kafadan vuruş, keşif, sivil cezaları, VETERAN ×1,5) satır satır sayılarak gelir; sağ üstte
  **derece** (S / A / B / C / D — S: bütün yıldızlar, alarm yok, kimseye zarar yok, her mermi isabet). En uzun atışta **REKOR**.
  İstihbarat kartı açıkken sayım bekler, kart kapanınca başlar.
- **VETERAN modu:** bitirilmiş görevin brifinginde VETERAN anahtarı — isabet noktası yok, rüzgâr %30 sert ve daha dalgalı,
  tüfek daha çok sallanır, hedef işaretlenmez, kolay nişan geçmez; ödül ×1,5. Görev kartında madalya ve en iyi derece.
- **Keşif:** ilk atıştan önce dürbünle taradığın koruma / silahlı / gözcü sayısı görev panelinde ("KEŞİF · 2 / 5 tehdit
  tarandı"); sonuçta keşif bonusu (hepsi taranırsa ek ödül).
- **Fırsat penceresi:** tanıdığın hedef, kendi planına göre gözden kaybolacaksa (binaya girecek, arabaya binecek, kaçacak)
  sol üstte turuncu "Fırsat penceresi 0:12" sayacı; son 5 saniyede kırmızı.
- **Taktik harita:** her görev için üstten çekilmiş gerçek harita — ana yuva ve diğer atış yerleri, ana yuvadan 100 m'lik
  menzil halkaları, görüş çizgisi, hedef bölgesi ve mesafesi, bilinen güzergâhlar, rüzgâr, ölçek. Brifingde **HARİTA**
  düğmesi, görev içinde duraklat menüsünde **HARİTA**.
- **Dürbün:** yeni mercek kenarı (ışık alan ince kenar, derin gölge), tüfek hareket edince kayan göz hizası gölgesi, gece
  görevlerinde retikülün ortası kırmızı aydınlatmalı.
- **Vurulma tepkisi:** kurşunun itişi iskelete geçer — göğüs/kafa/kol vurulan yöne doğru savrulur, yandan isabette gövde döner;
  ölüm animasyonunda bu itiş ragdoll'a aktarılır. Yakın ıskalamada hedef irkilir.
- **Işık ve atmosfer:** gün/gün batımı/bulutlu/gece/kar için ayrı renk derecelendirme (sıcak ışık–soğuk gölge, gece mavisi ve
  lamba parlaması, ıslak bulutlu hava), biraz daha hava perspektifi.
- **Ana menü:** sağ altta "SIRADAKİ SÖZLEŞME" kartı (hedefin fotoğrafı, tür, mesafe, zorluk, ödül) — dokununca brifing.
- **Denge:** 26 · Kayak Hocası'nda kar motorlu tetikçi daha yavaş gelir ve inince ateş etmeden önce daha uzun nişan alır.
- **Test botu:** VETERAN turu, derece/keşif/döküm kaydı.

## Görsel / yazı / UI cilası (30 Ekim akşam)
- **Brifing başlığı** sadeleşti ("GÖREV 21 · TELEFERİK"); ZORLUK sağ üstte ayrı bir rozet.
- **Hedef dosyası fotoğrafı:** figürün üstündeki/altındaki siyah bantlar kalktı (çerçeve stüdyo grisi).
- **"KILL CAM" → "KURŞUN KAMERASI"** (ekranda ve ayarlarda; oyunun geri kalanı Türkçe).
- **Yanlış yön:** ipucu "soldaki DOSYA" diyordu, panel artık sağda → "sağdaki".
- **Kimlik kartı:** taranmış ama teşhis edilmemiş kişide boş "?" etiketi yerine "TARANDI".
- **İstihbarat kartı:** kısa bir bilgi satırı için yarısı boş kalan kart küçüldü; karar varsa tam boy.
- **Cephanelik:** istatistik yazıları ve notlar büyüdü (telefonda okunmuyordu).
- **Dağ yuvası kayaları** küp yerine yuvarlak taş.
- **Kurgu:** 16. görevdeki mantık hatası (savcı, kim olduğunu bilmediği nişancıya not bırakıyordu) düzeltildi; 19'da tek başına
  çalışan nişancı için "ekibimiz" yerine "senin atış noktaların"; 3. bölüm kartı KONTROL sürprizini baştan açık etmiyor.

## Test 4 sonrası (30 Ekim akşam)
- Test 4: 28/28, **20 başarılı**, 2 hata (su yansıması, zararsız). Yeni dosya paneli ve İŞARETLE yeri ekranda doğrulandı.
- **Hikâyenin sonu:** 28. görevdeki karardan sonra tam ekran **SON** ekranı: seçtiğin sona göre başlık ("YİNE KİMSESİZ" /
  "KENDİ SEÇİMİN"), sonuç ve üç kararın her birinin akıbeti (kimyager, Baran'ın kasası, KONTROL).
- Test botu: kaza görevinde (Çığ) patlayıcıyı sayaç 6'ya inince vurur; bacak nişanı dizden (kalçaya gidiyordu);
  her merminin gerçekte nereye gittiğini log'a yazar (20. görevde kill cam isabet gösterip isabet sayılmaması incelenecek).

## Test 3 sonrası (30 Ekim akşam)
- **Görüş ipucu:** hedef açıktayken bulunduğun yerden 8 sn görülemiyorsa (korkuluk, kule, araba arada) KONTROL
  "Bu açıdan … görünmüyor. YER DEĞİŞTİR ile başka bir yere geç." der (görev başına bir kez).
- Test 3: 28/28 oynandı, **21 başarılı, 0 hata** (kırmızı hata yazıları bitti).
- **DOSYA paneli** büyütüldü (telefonda okunmuyordu). **İŞARETLE** düğmesi kimlik kartının sağına alındı (kartın ipucunu kapatıyordu).
- Test botu: canlı yakalamada hedef yere düşünce ateşi keser, iğne görevinde korumaları da bayıltır, net atış yoksa
  bekler (araba/duvar arkasındaki hedefe boşuna ateş etmez), sonradan ortaya çıkan hedefi bekler.

## Test 1 düzeltmeleri (30 Ekim)
- **Dağ yuvaları:** yamaçtaki kayalar gözün hemen önünde vadiyi kapatıyordu; alçaltılıp öne alındı.
- **Otomatik test botu oyuncu gibi:** nefesini tutup isabet çizgisi hedefin üstüne gelince ateş eder; kurşun duvara/arabaya
  gidecekse başka yuvaya geçer. Log'da her atışın tahmini sonucu yazar.
- **Görev 2:** Doğu konteyner yığınından hedef görünmüyordu (kurşun duvara gidiyordu); bu yer o görevde artık sunulmuyor.
- **Çarpışma hataları:** römorkör, sokak lambaları ve şemsiyelerde "Mesh Collider … Read/Write" hatası; ekranı kırmızı yazı
  kaplıyor, kurşunlar içlerinden geçiyordu. Çarpışma ayarı düzeltildi — ilk açılışta içerik bir kez yeniden derlenir.
- **Dürbün DOSYA paneli** zoom kaydırıcısının üstüne biniyordu: sağ üste, rüzgâr panelinin altına taşındı.
- **İŞARETLE** düğmesi telsiz yazısının üstündeydi: kimlik kartının altına alındı.
- **Bölüm kartı** açıkken arkadaki brifing görünmüyor; **İstihbarat** kartının arkası opak.
- **Kill cam** sırasında eğitim ipucu, telsiz ve bildirimler gizleniyor.
- Ateş altında / yaralıyken dürbün daha çok sallanır; yakına düşen kurşun hedefin çevresindeki korumaları da uyarır;
  brifingde "YENİ:" satırı o görevin getirdiği yeni beceriyi söyler.
- **Otomatik test:** açılış sinematiğini geçiyor, koruma görevlerinin bitişini bekliyor, canlı yakalamada iğne/bacak
  kullanıyor, her atışta oyunun isabet tahminini kaydediyor. test_sim.sh, xcode-select yanlış yeri gösteriyorsa Xcode.app'i
  kendisi kullanıyor.

## Düzeltme — Başlarken yer seçimi kaldırıldı (30 Ekim)
- BAŞLA'ya basınca açılan ve üst üste binen "Nereden ateş edeceksin?" listesi kaldırıldı. Her görev ana yuvadan başlar;
  diğer yerlere görev içinde YER DEĞİŞTİR ile geçilir.

## V2 · Adım 5 — Kurgu (30 Ekim)
- **Hikâye:** adı olmayan, ordunun ölü yazdığı bir nişancısın. KONTROL seni liman ağını çökertmek için tuttu. Her sözleşme
  bir isim ve bir sonrakine giden iz. Liman (Kral'ın ağı) → Şehir (Baran Holding) → Kartal Tepe (Kurul). İlerledikçe
  dosyalarda KONTROL'ün kendi imzası belirmeye başlıyor.
- **Bölüm kartları:** her haritanın ilk görevinden önce bölüm kartı çıkar. KONTROL'ün sesiyle okunur (Higgsfield ile
  seslendirildi).
- **İSTİHBARAT:** her başarılı görevden sonra o işin ortaya çıkardığı bilgi ve sıradaki iz gösterilir.
- **Kararlar:** 12 · İğne (kimyageri KONTROL'e mi savcılığa mı teslim edeceksin), 20 · Son Perde (Baran'ın kasasındaki
  parayı alacak mısın), 28 · Zirve (Kurul'un diskini kime vereceksin). Kararlar sonraki satırları ve sonu değiştirir;
  iki farklı son var.
- **DAVA DOSYASI** (ana menü): bitirdiğin bütün görevlerin istihbaratı bölüm bölüm ve verdiğin kararlar.

## V2 · Adım 4 — Görevlerin matematiği: zorluk, ıskalama, kaçış, ekipman (30 Ekim)
- **ZORLUK x/10** (brifing başlığında): kampanya aşaması, mesafe, rüzgâr, iki hedef, süre sınırı, canlı yakalama / koruma
  ve hedefi kendin bulman toplanarak hesaplanıyor. 28 görev 1'den 10'a ölçülü yükseliyor.
- **Iskalamanın bedeli** (5. görevden itibaren): hedefin 1,5 metre yakınından geçen kurşun onu ürkütür ve kaçmaya başlar.
  İlk atış iyi olmalı.
- **Kaçış aşaması** (5. görevden itibaren): hedef tamamlandığında yerin biliniyorsa ve silahlılar hâlâ ayaktaysa görev
  hemen bitmez: "ÇIKIŞ" sayacı başlar (9 sn). SİPER'de kalarak hayatta kal ya da YER DEĞİŞTİR'e bas (taşınınca hemen
  çıkmış sayılırsın).
- **Ekipman gerçekten gerekli:** 20 · Son Perde ve 22 · Şömine gece görüşü, 27 · Tipi termal dürbün olmadan başlamaz.
  Eksikse brifingde Cephanelik'e yönlendirir. Test modunda bu kontrol kapalı.

## V2 · Adım 3 — Nişancı yerleri ve yer değiştirme (30 Ekim)
Artık her görevde tek bir çatıya mahkûm değilsin. Her haritaya, görev bölgelerini gerçekten gören yeni yerler eklendi
(görüş hattı her yer için bütün görev noktalarına karşı ölçüldü):
- **Şehir:** Batı apartmanı penceresi, Opera çatısı, Doğu apartmanı penceresi.
- **Liman:** Kırmızı vincin operatör kabini, uzak vincin bom üstü, doğu konteyner yığını.
- **Dağ:** Kuzey sırtı, Batı yamacı (kayalık), Doğu tepesi, Köy kenarı (çam altı).
- **Görev öncesi seçim:** BAŞLA'ya basınca, o görevi gören yerler listelenir (ad, hedefe mesafe, tür, artısı ve eksisi).
  Ana yuva her zaman ilk sıradadır.
- **Görev içinde yer değiştirme:** dürbün dışındayken sol alttaki YER DEĞİŞTİR düğmesi (görev başına 2 kez). Taşınma
  mesafeye göre 5–11 saniye sürer, ekran kararır ama dünya durmaz: hedef yürümeye, korumalar aramaya devam eder.
  Yeni yerde düşman seni kaybeder (yerin bulunduysa kaçmanın yolu bu).
- **Yerin hissi:** pencerede önünde duvar, pervaz, kenara çekilmiş perde ve odanın tavanı; vinçte çelik kabin; yamaçta
  tüfeği dayadığın kayalar. SİPER tuşu pencerede pervazın altına, yamaçta kayanın arkasına çömeltir.
- Rüzgâr bayrakları seçilen yerden görünecek şekilde dikilir.

## V2 · Adım 2 — Hedefi bulmak (30 Ekim)
5. görevden itibaren hedef işaretli gelmiyor; oyuncu kimin hedef olduğunu kendisi buluyor.
- **DOSYA:** dürbüne girince solda hedefin fotoğrafı, takma adı ve ipuçları (kıyafet, davranış) görünür. Durum:
  ARANIYOR → DOĞRULANDI → ETKİSİZ.
- **Tarama artık isim vermiyor:** dürbünle bakılan yabancının kartında sadece yüzü ve "Kimliği belirsiz" yazar. Korumalar,
  silahlılar ve korunan kişiler eskisi gibi tanınır.
- **İŞARETLE — HEDEF BU:** taranan kişi dosyadaki yüze benziyorsa ekranın altındaki düğmeyle işaretlenir. Doğruysa
  "KİMLİK DOĞRULANDI" ve işaret çıkar. Yanlışsa Kontrol "O değil" der, düğme 6 saniye kilitlenir. Üçüncü yanlış teşhiste
  sözleşme iptal olur.
- **Yanlış kişiyi vurmak:** hedef henüz bulunmamışken masum birini doğrudan vurmak sözleşmeyi bitirir. Masum birini
  yaralamak ya da bayıltmak yanlış teşhis sayılır. Hedefi yaralamak ya da bayıltmak onu ortaya çıkarır.
- Giriş sahnesi hedefi etiketlemiyor, "Hedef bu bölgede — dosyadaki yüzü bul" yazıyor. Görev paneli, hedef bulunana kadar
  onun nerede ne yaptığını söylemiyor; dikkat dağıtma önerisi de ancak hedef bulunduktan sonra çıkıyor.
- Dron hâlâ bölgedeki herkesi tanır ve hedefi doğrular (bir kerelik ekipman).
- Hedefin araçta ya da kabinde olduğu, ya da dublörlü görevlerde (15 Gala, 21 Teleferik, 24 Kar Motoru, 25 Çığ) hedef
  eskisi gibi işaretli.
- Adım 1 düzeltmeleri: dağ görevlerinde rüzgâr okunu ve yönünü de göstermiyor ("Bayraklara bak").

## V2 · Adım 1 — Atıcılık becerisi (30 Ekim)
Artık isabet, oyuncunun mesafeyi ve rüzgârı okumasına bağlı. Yardım kampanya boyunca azalıyor:
| Görevler | İsabet noktası | Rüzgâr paneli | Dürbün sallantısı | Rüzgâr |
|---|---|---|---|---|
| 1–4 (eğitim) | her zaman | hız, yön ve "kaç mil tut" | az | sabite yakın |
| 5–12 (liman) | sadece NEFES tutarken | hız, yön ve "kaç mil tut" | normal | hafif dalgalı |
| 13–20 (şehir) | yok | hız ve yön | fazla | dalgalı |
| 21–28 (dağ) | yok | sadece his (HAFİF / ORTA / SERT) | en fazla | sert ve değişken |

- **MENZİL ayarı** (sol altta, + / −): dürbün 100–500 m arası 50 m adımlarla ayarlanır. Ayar hedefin mesafesine
  uyarsa mermi tam artının ortasına gider. Mesafe dürbünün altındaki telemetrede yazar.
- **Sarı düşüş çizgileri:** artının altında 2, 3, 4, 5 yazan çizgiler, o anki menzil ayarıyla 200, 300, 400 ve 500 metrede
  merminin nereye düşeceğini gösterir. Ayar yapmadan bu çizgilerle de tutulabilir.
- **Rüzgâr noktaları:** artının iki yanında 1 mil aralıklı sarı noktalar var. İlk görevlerde rüzgâr paneli "Sola tut
  0,8 mil" gibi ne kadar kaydırılacağını söyler.
- **Rüzgâr değişiyor:** esip diniyor, yönü biraz dönüyor. Kurşun ateş edildiği andaki rüzgârı alır.
- **Rüzgâr bayrakları:** her görevde hedef bölgesinin yakınına, dürbünden görünen ve kimsenin yürümediği yerlere iki bayrak
  dikiliyor. Bayrak rüzgârın yönüne doğru uçuşuyor, rüzgâr sertleştikçe dümdüz açılıyor.
- **Kolay nişan** (Ayarlar): açılırsa isabet noktası her zaman görünür ama o görevde 3. yıldız alınmaz (sonuç ekranında
  "KOLAY NİŞAN" yazar). Varsayılan olarak kapalı.
- Her yeni beceri ilk kez gerektiğinde telsizden kısa bir açıklama geliyor.
- Klavye (Mac'te test): T / G ya da PageUp / PageDown menzil ayarı.

## Ceset takla atması — kök neden düzeltmesi (30 Ekim)
- Asıl sebep bulundu: ölünce fiziğe geçen bedenin parçaları birbirine çarpıyordu. Tüfeği iki elle tutan, kolunu
  kavuşturan ya da telefonla konuşan adamın ön kolu göğsünün içinde başlıyordu; fizik motoru parçaları ayırmak için
  bütün bedeni fırlatıyordu. Artık bir bedenin parçaları birbirine çarpmıyor (eklemler yine yerinde tutuyor).
- Yere biraz gömülü başlayan uzuv yavaşça çıkarılıyor, fırlatılmıyor.
- Yürüyen / kaçan insanlar yerdeki cesede çarpıp onu savurmuyor.
- Düşüş boyunca eklemlerde sürtünme var: kollar ve baş savrulmuyor, yere inen beden yuvarlanmıyor.
- Yandan vurulan adam çoğunlukla merminin ittiği yöne, yana doğru yığılıyor.

## Sesler, kurşun kamerası ve vurulma (30 Ekim)
### Bütün sesler yeniden üretildi (Higgsfield)
- Oyundaki 66 sesin hepsi Higgsfield'da üretildi: tüfek atışı ve yankısı, sürgü, şarjör, susturuculu atış, uyutucu
  iğne, mermi isabetleri (beton, metal, beden), cam kırılması, patlama, düşmanların AK / makineli / tabanca sesleri,
  yanından geçen kurşunun çatlaması ve vızıltısı, sekme, kulak çınlaması, inlemeler, kalp atışı, menü ve görev sesleri,
  liman, şehir ve dağ ortam sesleri (deniz, yağmur, trafik, kalabalık, rüzgâr, helikopter, teleferik, siren, alarm,
  havai fişek, çığ, kar motoru...).
- Ortam sesleri kesintisiz döngü olacak şekilde birleştirildi. Ses dosyası bulunamazsa eski sentez ses devreye girer.
### Kurşun kamerası
- Kamera artık kurşunun hemen arkasında, onunla aynı hizada gidiyor ve vurulana kadar ayrılmıyor. Aşağıdan yukarı
  dönen, adamın etrafında dolaşan çekim kaldırıldı.
- Kurşun daha büyük ve dönerek görünüyor, arkasındaki iz çizgisi kamerayı kapatmıyor.
- Uzun atışlarda kurşun yolun ortasında bir an yandan görünüyor, sonra yine arkasına geçiliyor. Son metrelerde
  kamera biraz geri çekiliyor, kurşunun adama girdiği an kadrajda.
- İsabetten sonra kamera atış doğrultusunda birkaç metre geri çekilip sabit kalıyor ve adamın yere yığılmasını
  ağır çekimde izliyor (dönmeden, eğilmeden).
### Vurulma ve ölüm
- Takla attıran animasyonlar çıkarıldı ("geriye savrulma", "yere çarpılma", koşarken dalış). Kalan animasyonlar
  yere bağlı: dizlerin üstüne çökme, geriye düşme, oturur gibi yığılma, öne kapanma, karından vurulunca iki büklüm düşme.
- Ölüm animasyonları biraz hızlandırıldı: vurulan adam sahne yapmadan yere iniyor.
- Fizik bedenine geçişte merminin itişi gerçekçi seviyeye indirildi (eskiden adamı fırlatacak kadar güçlüydü).
  Kafadan vurulan çoğu zaman olduğu yere yığılıyor: dizleri bükülüyor, kalça çöküyor. Kollar ve baş savrulmuyor,
  beden kendi etrafında dönmüyor.

## Test modu (30 Ekim)
- Bu build test sürümü: bütün haritalar ve 28 görev baştan açık. Menünün sol altında "TEST MODU: tüm bölümler açık" yazar.
- Sürüm yazısına 3 sn basılı tutunca normal ilerlemeye dönülebilir. Mağaza sürümü için `Game.cs` içindeki `TestBuild`
  değeri `false` yapılacak.

## Aşama 4 · Yeni modeller — iç mekânlar ve sokak detayları (30 Ekim)
Sniper_3D_Models klasöründeki modellerden oyuna uyanlar seçildi, Blender'da mobile uygun hale getirildi (ölçü, pivot,
yön, poligon, 512–1024 doku) ve haritalara yerleştirildi. Her parçanın NPC yollarını, kapıları ve atış hatlarını
kesmediği tek tek kontrol edildi.
- **Şehir · çatı katı (20 · Son Perde):** koltuk takımı, berjerler, sehpalar, puflar, bar tabureleri, konsol, dolap ve
  avizeler. Cam arkası artık boş bir oda değil, gerçek bir daire.
- **Şehir · cam kule ofisi (13 · Cam Kule):** bekleme köşesi, berjerler, sehpa ve konsol.
- **Şehir · sokaklar:** meydanda ve iskele tarafında iki kahve arabası, lambaların yanında yangın muslukları.
- **Kartal Tepe · otel lobisi (21, 28):** sehpa, koltuklar, sallanan sandalye ve iki avize.
- **Kartal Tepe · cam villa (22 · Şömine):** iki avize.
- **Liman · depoların önü:** variller, karton kutular ve çöp torbası.
- Klasördeki .glb modeller (CesiumMan, Xbot, Michelle, Soldier, oyuncak araba, süt kamyonu, kask, Ferrari) kullanılmadı:
  bunlar three.js/glTF demo modelleri; çizgi film tarzı ya da markalı oldukları için gerçekçi görünümü bozuyorlardı.
  Silahlar, binalar ve araçların çoğu oyunda zaten aynı kaynaktan (Poly Haven) daha iyi sürümleriyle vardı.
- İlk build'de 18 yeni model ve dokuları içe aktarıldığı için build birkaç dakika uzun sürer.

## Aşama 4 · Bölüm 2 ve 3 — Silahlı düşmanlar, siper, gözcüler ve tempo (30 Ekim)
### Düşmanlar artık karşılık veriyor
- **Korumaların ve silahlı adamların elinde gerçek silah var:** kalaşnikof, hafif makineli ya da tabanca. Tüfek sırtta
  asılı durur, tabanca ve makineli kılıfta. Olay çıkınca silahı çekiyor, iki elle tutup nişan alıyorlar.
- **Ateş ettiğinde seni arıyorlar.** Ekranın üstündeki göz halkası, nişancı yuvasının ne kadar belli olduğunu gösterir:
  DİKKAT ÇEKİYORSUN → YERİNİ ARIYORLAR → YERİN TESPİT EDİLDİ. Her atış halkayı doldurur. Susturucu, uyutucu iğne ve
  gürültü (havai fişek vb.) çok daha az doldurur. Gece seni bulmaları zorlaşır. Ortada tepki verecek silahlı kimse yoksa
  halka hiç dolmaz.
- **Yerini bulunca siper alıp ateş ediyorlar:** duvar, araç, konteyner arkasına koşuyorlar. Siperden doğrulup ya da
  diz çöküp nişan alıyor, seri atıyorlar. Şarjör değiştiriyor, ara sıra siper değiştiriyorlar. Siperdeki adamı vurmak
  için başını çıkardığı anı beklemek gerekiyor. Yanına kurşun düşen eğilip bir süre ateş edemiyor.
- **Mermiler gerçekçi geliyor:** önce namlu alevi görünüyor, kurşun mesafeye göre biraz sonra geliyor, silah sesi en
  son. Iskalayan kurşun kulağının yanından vızıldıyor ya da korkuluğa çarpıp toz ve kıymık çıkarıyor. Arada iz
  mermileri görünüyor.
- **Sağlık:** vurulunca ekran kızarıyor, kulak çınlıyor, kurşunun geldiği yönü gösteren ok çıkıyor. Birkaç saniye
  vurulmazsan yaraların yavaş yavaş iyileşiyor. Can biterse yere yığılıyorsun ve görev o an başarısız oluyor.
- **SİPER tuşu** (sol altta, kalkan simgesi): korkuluğun arkasına çömeliyorsun, kurşunlar sana değmiyor. Bir süre
  görünmezsen seni kaybediyorlar. Dürbüne basınca otomatik doğruluyorsun. Tam doğrulmadan ateş edemiyorsun. Tehlike
  varken tuş kırmızı yanıp sönüyor. Sana nişan alan biri olduğu anda (karşı nişancı dahil) tuş ekrana geliyor.
- Dürbünde sana nişan alan adamın üstünde kırmızı halka ve **"SANA ATEŞ EDİYOR"** yazıyor.
- Kill cam ve ara çekimler sırasında kimse sana ateş etmiyor. Gözcüler ve telsiz sayaçları da o sırada bekliyor.
- Silahlılar sana ateş ederken **hedefler ve korunan kişiler kaçmıyor:** bir anlığına eğilip yollarına devam ediyorlar.
  Sivil kalabalık ise dağılıyor.
- Düşmanların isabeti kampanya ilerledikçe artıyor. İlk görevlerde affedici, son görevlerde gerçekten tehlikeli.
- İlk kez dikkat çektiğinde, ateş altına girdiğinde ya da bir gözcü gördüğünde kısa bir ipucu çıkıyor.

### Gözcüler
- Bazı görevlerde dürbünlü gözcüler var. Çatıları tarıyorlar; dürbünün camı sana doğru dönerken parlıyor. Seni yeterince
  uzun görürse dürbünü indirip telsize uzanıyor. Birkaç saniye içinde onu durdurmazsan herkes yerini öğreniyor.
- Gözcünün işaretindeki halka seni ne kadar gördüğünü gösteriyor. Ekranın üstünde **"GÖZCÜ SENİ ARIYOR"** yazıyor.
- Dürbün sana dönerken eğilebilir ya da önce gözcüyü indirebilirsin. Görevin başında telsizden nerede olduğu
  söyleniyor.

### Kimse olduğu yerde beklemiyor
- Boşta duran adamlar bir noktaya çakılı kalmıyor: arada birkaç adım yürüyüp başka bir yere geçiyorlar. Kimsenin yürüyüş
  yolunu kesmiyorlar. Kalabalık sahnelerde (yat, tramvay durağı, gala, miting, yılbaşı terası) ve kuyruklarda
  dolaşmıyorlar.
- Ateş başlayınca sivillerin bir kısmı en yakın siperin arkasına koşup çömeliyor, bir kısmı yere kapanıyor ya da kaçıyor.
- Silahlı adam kolundan vurulunca silahını düşürüp siper alıyor.
- Yerdeki kuryeye ya da muhasebeciye gelen silahlılar artık yolu buluyor. Yüz metre öteden nişan almıyorlar.
- Ekip aracı, etrafı temizlediğinde gerçekten hızlanıyor; geri sayım da ona göre doğru işliyor.
- **Karşı nişancılar** (16, 20, 26): sen siperde kalırsan iki atıştan sonra kendi işine dönüyorlar. Senin yüzünden
  savcıyı ya da kayak hocasını bırakmışsa ona yeniden nişan alıyor. Kill cam sırasında nişan sayaçları duruyor.

### Görevler
- **3 · KONTEYNER TAKASI:** Kuzgun'un arabası daha yakından ve daha hızlı geliyor.
- **9 · TANIK:** Kulede dürbünlü bir gözcü var, kapının iki yanında örgütün iki silahlı adamı bekliyor. Seni bulurlarsa
  sana ateş ediyorlar. Tanığa eşlik eden polisin belinde tabanca var. Tetikçilerden biri silah çekince polis silahını
  ona doğrultup "Arkamda kal!" diye bağırıyor, tanık arkasında çömeliyor. Tehlike geçince yürümeye devam ediyorlar.
  İki tetikçi de durdurulunca görev tamamlanıyor. Tanık yolda yaralanır ya da bayılırsa görev yine başarısız.
- **10 · KURYE:** Ekip yoldayken depodan üç silahlı adam peş peşe çıkıp kuryeye koşuyor. Ortalık temizlenince ekip arabası
  hızlanıyor. Üçüncü yıldız: "Kimse kuryeye nişan alamasın".
- **12 · İĞNE:** Tekne yoldayken gemiden iki tayfa kimyageri almaya iniyor. Gemiye yakınsa temkinli yürüyorlar ve onu
  kaldırmaları biraz sürüyor (iki iğneye vakit var). Onları da durdurunca tekne tam gaz geliyor. Uyuşturucu iğne
  sayısı yediye çıktı.
- **13 · CAM KULE:** Defne her turda pencereye iki kez geliyor.
- **15 · GALA:** Gerçek Aydemir'in arabası dublörden sonra daha çabuk geliyor.
- **17 · İSKELE:** Vapur daha yakından başlıyor, yolcular yarım dakika içinde iniyor. Tramvay durağında ikinci bir tetikçi
  var (gri ceketli). Ekip beklenirken o da muhasebeciye yürüyor. İkisi de durunca ajanlar koşarak geliyor.
- **19 · HELİPAD:** Otelin çatısının uzak köşesinde dürbünlü bir gözcü var. Helikopter daha yakından geliyor. Gözcüyü
  vurmak "Korumalara dokunma" yıldızını götürmüyor (bu, gözcülerin olduğu bütün görevler için geçerli).
- **22 · ŞÖMİNE:** Villanın yolunun aşağısında gece dürbünlü bir gözcü var.
- **23 · JAKUZİ:** Kapıdaki koruma dürbünle yamaçları tarıyor. Selim'in dibinde durduğu için onu vurmak Selim'i
  ürkütüyor: dürbün sana dönünce eğilmek gerekiyor.
- **24 · KAR MOTORU:** Ratrak yoldayken spadan Kurul'un iki adamı çıkıp kuryeye gidiyor. Durdurunca ratrak tam gaz geliyor.
  Yıldızlar: "En fazla beş atış" ve "Kurul'un adamları nişan alamasın".
- **25 · ÇIĞ:** Konvoy daha erken yola çıkıyor.
- **28 · ZİRVE:** Gece yarısı daha erken geliyor (46 sn). O zamana kadar Patron'un güvenlik şefi terasın önünden gece
  dürbünüyle yamaçları tarıyor.
- **20 · SON PERDE:** Çatıdaki nişancı ıskalarsa yeniden nişan alıyor. Projektör ışığı siperdeyken seni görmüyor.

### Teknik
- Düşman namlu alevleri, korkuluğa çarpan kurşunların tozu ve gelen kurşun sesleri havuzdan çalışıyor: çatışmada her
  kurşun için yeni nesne oluşmuyor, gelen kurşun sesleri senin tüfeğinin sesini kesmiyor.
- iPad'de sağlık ve göz göstergesi görev panelinin sağına yerleşiyor (üst üste binmiyor).
- Görevden menüye dönünce kamera eğik ya da çömelmiş kalmıyor.
- Otomatik test (`test_sim.sh`): bot ölümsüz, gelen kurşunlar nişanını bozmuyor.

## Aşama 4 · Bölüm 1 — Hareketli hedef, kill cam, kan ve ölüm animasyonları (29 Ekim)
- **Koşan / yürüyen adam artık vurulabiliyor.** Nişan noktası ve isabet hesabı, kurşun uçarken adamın yürüdüğü yolda
  (köşeleri dönerek) nereye varacağını hesaplıyor. Kırmızı nokta adamın şu anki gövdesinde duruyor: nokta üzerindeyse
  isabet ediyor, değilse önünü almak gerekiyor. Kabin, araba, kızak içindekiler de aracıyla birlikte hesaplanıyor.
  Dürbünde noktanın altında nereye isabet edeceği yazıyor (KAFA / GÖVDE / KOL / BACAK).
- **Kill cam kurşun adama değene kadar sürüyor.** Mesafe ne olursa olsun uçuş birkaç saniyeye yayılıyor, son 30-35 metre
  kurşunun yanından çok yavaş izleniyor, çarpma anında neredeyse duruyor, sonra adam yere yığılana kadar etrafında dönüyor.
  Iska olursa boşlukta takılı kalmıyor. "HAREKETLİ HEDEF" etiketi eklendi.
- **Kan efektleri baştan yapıldı:** giriş deliğinden kısa bir püskürme, kurşunla birlikte savrulan damlalar (yavaş
  çekimde iz bırakarak), havada asılı kalan kan buğusu, arkadaki duvara ve yere damlalar düştükten sonra beliren
  sıçrama izleri. Kafa vuruşu daha sert. Yaralı (bacağından / kolundan vurulmuş) kişi sürünürken ya da topallarken yere
  kan damlatıyor. Gece kan parlamıyor.
- **Ölüm animasyonları:** vurulan adam artık bez bebek gibi devrilmiyor: kurşunun geldiği yöne ve isabet yerine göre
  sendeliyor, dizlerinin üstüne çöküyor, geriye ya da yüzüstü düşüyor, karnını tutarak yığılıyor; koşarken vurulan ileri
  savruluyor. Yere inince fizik devralıyor, altındaki zemine göre yerleşiyor. Arkasında duvar ya da boşluk varsa
  (çatı kenarı, iskele) animasyon yerine doğrudan fizikle düşüyor. Kafadan vurulanların bir kısmı olduğu yere yığılıyor.
- **Kolundan vurulan** üç farklı tepkiden biriyle sarsılıyor (kalabalıkta hepsi aynı hareketi yapmıyor).
- 39 yeni animasyon (ölüm, vurulma, eğilme, siper, silahlı koşu / nişan / şarjör değiştirme) ve düşmanlar için üç gerçek
  silah modeli (AK, hafif makineli, tabanca) eklendi; silahlar Bölüm 2'de kullanılacak.
- Ses: dünyadaki sesler artık havuzdan çalıyor (her ses için yeni nesne oluşturulmuyor).
- **Bu sürüm yeni sanat dosyası içeriyor:** `build_ios.sh` ilk çalıştırmada içeriği yeniden oluşturur (birkaç dakika
  fazladan sürer).

## iPhone'da ilk çalıştırma düzeltmeleri (29 Ekim)
- **Unity 6.6 derleme hatası:** `GetInstanceID()` artık hata veriyor; mermi delme önbelleği doğrudan collider'a göre
  tutuluyor (Ballistics.cs).
- **Açılışta çökme:** Cephanelikteki satın al / kuşan butonları yazı alanı olmadan oluşturuluyordu, yazı yazılırken
  NullReferenceException. Butonlar artık yazılarıyla oluşuyor (UIKit.cs).
- **Güvenlik ağı:** Bir menü sayfası kurulurken hata çıkarsa oyun artık çökmüyor; hata kaydediliyor ve oyun açılıyor (UIRoot.cs).
- **Otomatik test daha sağlam:** `test_sim.sh` artık varsayılan olarak yalnız iPhone simülatöründe çalışıyor (`hepsi` ile
  iPad de). Kayıt (`log.txt`) her kontratta ve her yeni hatada yazılıyor; uygulama çökse ya da takılsa bile o ana kadarki
  kısım kalıyor. Aynı hata her karede tekrarlanıyorsa iki kez yazılıp sayılıyor. Hata veren adım atlanıp test devam ediyor.
  Uygulamanın konsol çıktısı `player.log`, çökme raporu `.ips` olarak aynı klasöre kopyalanıyor. 7 dakika ilerleme
  olmazsa test bekleyip durmuyor.

## Aşama 3 — Kartal Tepe: karlı dağ haritası, 8 yeni görev (29 Ekim)
### Yeni harita: Kartal Tepe Kayak Merkezi
- Nişancı yuvası vadinin güney ucundaki tepede, eski **meteoroloji istasyonunun** çatısında: radar kubbesi, anten direği,
  vadiye bakan alçak korkuluk.
- Vadide **Kartal Dağ Oteli** (taş zemin kat, ahşap balkonlu dört kat, karla kaplı dik çatı; önünde cam duvarlı, şömineli
  salon; yanında çatısında jakuzi olan spa binası), 16 dağ evi, doğu yamacında **cam duvarlı modern villa**, buz balıkçısı
  kulübeleri ve paten pisti olan **donmuş göl**, kayak kiralama, après-ski köşesi, otopark.
- **Teleferik:** vadi istasyonundan dağın 350 metre yukarısındaki zirve istasyonuna 9 direk ve 18 kabin. Kabinler
  istasyonlarda yavaşlayıp halatın etrafından dönüyor, yolda rüzgarda hafifçe sallanıyor; makinenin uğultusu duyuluyor.
- Vadinin iki yanında **çam ormanları** (yakındakiler gerçek üç boyutlu ağaç, uzaktakiler binlerce ağaç), ormanın arasından
  inen kayak pistleri, teleferiğin altında açılmış koridor ve arkada 1300 metreye varan zirveler.
- **Kar her yerde:** çatılarda, korkulukların, pencere pervazlarının ve arabaların üstünde. Çatının altındaki yerlerde
  (salon, balkonlar, istasyon platformu) kar yok. Karda patikalar, kar motoru ve ratrak izleri, kayak izleri; gölde
  rüzgarın süpürdüğü buz, buzdaki delikler. Yakından bakınca kar kristalleri ışıkta parlıyor.
- Yeni hava durumları: **karlı gün, dağda gün batımı, karlı gece, tipi.** Kar yağışı dürbünle birlikte uzağa taşınıyor,
  tipide kar yatay savruluyor ve görüş 200 metreye düşüyor. Bacalardan duman tütüyor, jakuzinin üstünde buhar var.
- Harita, görev listesinin üstündeki sekmeden seçiliyor. Şehirde 16. görevi bitirince açılıyor.

### 8 yeni görev (toplam 28) — Kurul
Baran'ın dosyaları, onu yıllarca besleyen beş kişiyi gösteriyor: Kurul. Her yıl yılbaşı haftasını Kartal Tepe'de geçiriyorlar.
- **21 · TELEFERİK** (karlı gün): Banker korumalarıyla teleferiğe binip zirveye çıkıyor. Hedef hareketli bir kabinin içinde,
  250 metreden 1 kilometreye kadar uzaklaşıyor. Motor dairesine iki atış hattı durduruyor: kabinler sallanıyor,
  içindekiler çömeliyor. Teknisyenler bir süre sonra hattı yeniden çalıştırıyor.
- **22 · ŞÖMİNE** (karlı gece): Hakim, iki kat yüksekliğinde cam cepheli villasının salonunda: kanepe, ortada tavandan
  sarkan şömine, batı camının önünde bar. İlk kurşun camı kırıyor. Eşi arada bir kapıdan yatak odalarına geçiyor: o
  odadayken ateş edersen tanık kalıyor.
- **23 · JAKUZİ** (gün batımı): Müteahhit spa terasında, sıcak suyun içinde. Buhar onu bir saklıyor, bir gösteriyor.
  Sudan çıkıp içeri yürüdüğü birkaç saniye son fırsat.
- **24 · KAR MOTORU** (karlı gün, CANLI YAKALA): Kurye spa kapısından çıkıp kar motoruyla vadiyi geçiyor. Önce motoru
  durdur; kurye en yakın dağ evine koşar, içeri girmeden bacağından vur (uyutucu iğne de olur). Ekip ratrakla gelip onu
  alıyor.
- **25 · ÇIĞ** (sabah, KAZA): Emniyet istihbaratının başındaki adamın konvoyu batı yolundan geliyor. Yamacın tepesindeki çığ
  patlayıcısını tam zamanında vurursan çığ yolu örtüyor. Erken ya da geç kalırsan konvoy duruyor ya da otele varıyor;
  o zaman tek çaren onu doğrudan vurmak.
- **26 · KAYAK HOCASI** (karlı gün, KORUMA): Kurul'a karşı tanıklık edecek kayak hocasını koru. Üç tetikçi var:
  istasyonun çatısında bir nişancı, otelden yürüyen biri ve kar motoruyla gelen biri. Jandarma gelene kadar dayan.
- **27 · TİPİ** (tipi): Kurul'un temizlikçisi kar fırtınasında köyü geçip ratrakla dağı aşıyor. Görüş 200 metrenin altında,
  rüzgar çok sert: termal dürbünle sisin içindeki vücut ısısını bul.
- **28 · ZİRVE** (yılbaşı gecesi): Patron otelin terasında, gece yarısı havai fişekleri izliyor. Patlamanın sesi yuvaya
  ulaştığı an atılan kurşunu kimse duymuyor. Gösteri bitince içeri giriyor.

### Yeni mekanikler
- **Teleferik:** kabinler istasyonlarda yavaşlayıp yolcu alıyor; hat durunca sarkaç gibi sallanıyorlar. Kabinde vurulan
  kişi kabinin içine düşüyor.
- **Çığ:** patlayıcı yamacı koparıyor; toz bulutu önünde yamaçtan iniyor, yolu ağır karla örtüyor ve altında kalan aracı
  gömüyor.
- **Havai fişek ses perdesi:** patlamanın sesi gecikmeli geliyor (ses 340 m/sn); o anda atılan kurşun neredeyse duyulmuyor.
- **Buhar perdesi:** jakuzinin buharı yoğunlaşıp açılıyor.
- **Kar motoru ve ratrak:** kaçış ve kurtarma araçları; motor bloğuna isabetle duruyor.
- 19 yeni karakter, kışlık giysilerle: kayakçılar, dağ evi misafirleri, korumalar, teleferik görevlisi, buz balıkçısı,
  kayak hocası ve Kurul'un beş üyesi.
- 15 yeni yapay zekâ modeli (Meshy): köknarlar, kar motoru, ratrak, jakuzi, kayak rafı, kardan adam, kızak, fener direği,
  odun yığını, buz balıkçısı kulübesi, dürbün, ateş çukuru, tabela. Otel, dağ evleri, villa, teleferik ve istasyonlar,
  çığ patlayıcısı ve arazi prosedürel olarak üretildi.
- Kar yağarken uzak ağaçlar rüzgarda sallanıyor; tipide bacaların dumanı savruluyor.
- Her görevde hedeflerin durduğu ve yürüdüğü yerler yuvadan ölçüldü (ışın testi): cam villanın içi, spa terası, otopark,
  tipideki yayalar dahil hepsi görünüyor.
- Otelin cam salonunun iki yanında giriş kapıları var; insanlar artık kapılardan girip çıkıyor. Bütün yürüyüş ve araç
  yolları binalardan, ağaç gövdelerinden ve eşyalardan geçmeyecek şekilde ölçülerek çizildi.

### Bütün haritalarda düzeltmeler
- **Karakterlerin boyu:** kadınlar ve kısa boylu erkekler bazı animasyonlarda yerden 10-20 cm yukarıda duruyordu, uzun
  boylular biraz gömülüyordu. Artık her animasyon karakterin kendi bacak boyuna oturuyor (oturma pozları koltuk
  yüksekliğinde kalıyor).
- **Araçtakiler:** kabinde, kar motorunda, arabada ya da teknede giden biri ürkünce artık aracın içinde çömeliyor (eskiden
  inip yürümeye çalışabiliyordu); vurulursa cesedi araçla birlikte gidiyor.


## Aşama 2 — Şehir Merkezi: yeni harita, 8 yeni görev (29 Ekim)
### Yeni harita: Saat Kulesi Meydanı
- Deniz kıyısında saat kuleli bir meydan. Nişancı yuvası, bulvarın karşısındaki 13 katlı otelin çatısında.
- Çevrede: sütunlu revakı, balkonu ve kubbesiyle **Devlet Opera ve Balesi**; 11. katında ışıkları yanan bir ofis bulunan
  **Kordon Plaza** cam gökdeleni (camları kurşunla kırılıyor); cam duvarlı çatı katı ve helikopter pisti olan **Palas Oteli**;
  çan katına çıkılabilen **saat kulesi**, kafe pavyonu, çeşme, heykel, büfe ve simit arabası.
- **Kordon:** palmiyeli bulvar, iki yönde sefer yapan kıyı tramvayı (durakta bekler, zil çalar), vapur iskelesi ve vapur,
  bulvarda akan trafik.
- Şehirde hayat var: kaldırımda yürüyenler, kafe terasında oturanlar, koşucular, turistler, simitçi, büfeci, gece devriyesi.
  Yağmurda yayalar şemsiyeyle yürüyor.
- Gece pencereler tek tek yanıyor: her pencerenin ardında derinliği olan bir oda var. Tabelalar ve sokak lambaları parlıyor,
  binaların dibinde yumuşak gölge var.
- Yeni hava durumu: **yağmurlu gece** (alçak bulutlar, ıslak taşlar, pus).
- Harita, görev listesinin üstündeki sekmeden seçiliyor. Liman'da 6. görevi bitirince açılıyor; sürüm yazısına 3 saniye basılı
  tutarsan hepsi açılır.

### 8 yeni görev (toplam 20)
- **13 · CAM KULE** (gece): Baran Holding'in finans direktörü 11. kattaki ofisinde. Atışı camın içinden yapıyorsun; cam kırılınca
  içerideki herkes sesi duyuyor.
- **14 · TRAMVAY** (gün batımı): Kurye durakta tramvay bekliyor. Yanında ona çok benzeyen bir yolcu var. Tramvay geldiğinde
  biniyor, ondan önce bitirmen gerek.
- **15 · GALA** (gece): Operanın açılışı, kırmızı halı, foto muhabirlerinin flaşları. Limuzinden önce hedefin dublörü iniyor,
  gerçek hedef ardından sedanla geliyor. Perde arasında balkona da çıkıyor.
- **16 · MİTİNG** (gündüz, KORUMA): Başsavcıyı konuşması boyunca koruyorsun. Saat kulesinde bir karşı nişancı, kalabalıkta da
  silahlı biri var. Ateş edersen nişancı seni görüyor ve sana dönüyor.
- **17 · İSKELE** (sabah, CANLI YAKALA): Muhasebeci vapurdan inen kalabalığın içinde. Bacağından vurduğun an Baran'ın adamı
  motosikletle geliyor. Ekip onu alana kadar muhasebeciyi korumalısın.
- **18 · YAĞMUR** (yağmurlu gece, ÇİFT HEDEF): Tetikçi Çakal ile rüşvetçi komiser, şemsiyelerin altında buluşuyor. Biri
  düştüğü an öteki aracına koşuyor. Bu görevde sana yarı otomatik tüfek ve termal dürbün veriliyor.
- **19 · HELİPAD** (gün batımı): Helikopter denizden gelip otelin çatısına iniyor. Güvenlik şefi binmeden onu durdurmalısın.
  Motoru vurursan helikopter yerde kalıyor.
- **20 · SON PERDE** (gece): Kenan Baran'ın cam duvarlı çatı katı. Çatıdaki projektör damları tarıyor, ışık seni bulursa
  karşı nişancı sana nişan alıyor. Baran ürkerse helikoptere koşuyor.

### Yeni mekanikler
- **Cam:** Kurşun camdan geçiyor ve camı parçalıyor; içeridekiler sesi duyuyor. Kill cam'de "CAMIN İÇİNDEN" yazıyor.
- **Karşı nişancı:** Tüfeği omzunda: saat kulesinde diz çökmüş, otel çatısında ayakta nişan alıyor; vurulunca tüfeği elinden
  düşüyor. Sektörünü tararken dürbünü sana doğru döndükçe güneşi yakalayıp parlıyor, gece yeşil yanıyor. Sana nişan alınca
  kırmızı halka doluyor: dolmadan onu vurmalısın, yoksa görev bitiyor. Düelloyu kazanırsan "DÜELLO" kill cam'i oynuyor.
  Susturuculu tüfekle seni daha geç fark ediyor.
- **Son anda kurtarış:** Korunan birine nişan almış tetikçiyi vurduğun atış "SON ANDA" kill cam'iyle oynuyor.
- **Canlı ara sahneler:** Limuzinin opera kapısına yanaşması, vapurun iskeleye girişi ve helikopterin piste inişi kısa bir canlı
  çekimle gösteriliyor (dokununca geçilir).
- **Dublör ve benzer yolcu:** Hedefe çok benzeyen masum biri var; tarama yapınca kimliği ortaya çıkıyor.
- Şemsiye yüzü yukarıdan gizliyor ama kurşunu durdurmuyor; vurulan kişinin şemsiyesi yere düşüyor. Ayrıca flaşlar, helikopter
  rotoru ve sesi, motosiklet ve arabayla kaçışlar eklendi.
- 17 yeni karakter: gala davetlileri, garson, foto muhabiri, polis, başsavcı, muhasebeci, pilot, güvenlik şefi, Kenan Baran ve
  diğerleri.
- 26 yeni yapay zekâ modeli (Meshy): limuzin, sedan, taksi, polis aracı, helikopter, vapur, çeşme, heykel, büfe, simit arabası,
  kafe masaları, kürsü, ses sistemi, palmiye ve diğerleri. Binaların hepsi prosedürel olarak üretildi.

### Düzeltmeler ve cila
- Uzak kıyıdaki şehir binaları artık tepelerin üzerinde havada durmuyor (her iki haritada); dik yamaçtaki birkaç bina kaldırıldı.
- Şehirdeki nişancı yuvasında göz hizası korkuluğun hemen arkasına alındı: aşağıya dik atışlarda (kafe, büfe, bulvar kenarı)
  korkuluk artık görüşü kesmiyor.
- Saat kulesi 45° döndürüldü: çan katındaki nişancı yuvadan bir kemerin içinden görünüyor, öbür kemerden sahneye nişan alıyor.
- Taksi ve kaçış araçları bulvarda doğru şeritte gidiyor; foto muhabiri flaşları artık daha hafif (performans).
- Mitingde başsavcıyı ürkütüp sahneden kaçırarak görevi bitirmek artık mümkün değil: korkunca kürsünün arkasına çöküyor.
- Son Perde'de çatıdaki nişancı zorunlu hedef değil; Baran çatıya çıktıktan sonra helikopter vurulursa geri dönüyor.

## Aşama 1 — Tam oyun: cephanelik, yeni görev türleri, sinematik girişler (29 Ekim)
### Cephanelik (menü → CEPHANELİK)
- **5 tüfek**, her biri yapay zekâ ile üretilmiş gerçek 3B model, parmakla çevrilebilen önizleme:
  KARTAL K-98 (başlangıç), GÖLGE SR-7 (susturuculu, sessiz), FIRTINA DMR (yarı otomatik, 10'luk şarjör),
  ATMACA .338 LR (en hızlı mermi, 20× dürbün, ince siperi deler), TİTAN .50 (yarı otomatik, duvar ve araç deler, her isabet öldürür).
  Tüfekler para + yıldızla açılır; namlu hızı, şarjör, nişan salınımı, geri tepme, ses ve dürbün aralığı tüfeğe göre değişir.
- **Ekipman:** gece görüşü dürbünü, termal dürbün (insan ısısı beyaz parlar, cesetler yavaşça soğur), keşif dronu (bölgedeki herkesi tanımlar).
- **Özel mermiler:** zırh delici, uyuşturucu iğne (öldürmez, bayıltır), patlayıcı mermi. Dürbündeki mermi tuşuyla seçilir.
- Mermiler artık ince siperden (konteyner, araç, sandık) geçebiliyor; tüfeğin ve merminin delme gücüne göre.

### Yeni yaralama sistemi
- Bacaktan vurulan yere düşer, sürünür; koldan vurulan silahını düşürür. Kafa/gövde öldürür.
- Görev türleri: SUİKAST, KORUMA, CANLI YAKALA (bacağından vur / iğneyle bayılt), SABOTAJ (pano, motor vb.).
- Her görevin kendi 3 yıldızlık meydan okuması var (ör. "Alarm verdirme", "Tek atış", "İki tetikçiyi de silah çekmeden durdur").

### 4 yeni görev (toplam 12)
- **09 · TANIK** — Mahkemede ifade verecek tanığı kalabalığa karışmış iki tetikçiden koru.
- **10 · KURYE** — Kuryeyi bacağından vur, ekip gelene kadar onu susturmaya çalışan korumalardan koru.
- **11 · KARARTMA** — Gece; elektrik panosunu vurup limanı karartabilirsin. Gece görüşü bu iş için veriliyor.
- **12 · İĞNE** — Kimyageri iğneyle bayılt, korumalar onu gemiye taşımadan durdur. Kimse ölmemeli.
- Eski 8 görev de yeni hedef satırları, saatleri ve yıldızlarla güncellendi.

### Sinematik girişler ve yeni animasyonlar
- Her görev, dünyanın dondurulduğu bir gözetleme girişiyle başlıyor: kamera görev alanının üzerinden süzülür, hedefin ve
  adamlarının üzerine istihbarat etiketleri düşer, telsizden KONTROL konuşur, sonra kamera dürbüne geri uçar (dokununca geçilir).
- 37 yeni animasyon: vurulma tepkileri, yere düşme, sürünme, topallama, yaralı yatma, dans, sohbet, oturma, telefonla konuşma, alkış...
- Brifing ekranı: hedef dosyası, görev satırları, yıldız hedefleri, tüfek ve verilen ekipman (DEĞİŞTİR → cephanelik).
- Sonuç ekranı: hangi yıldızın kazanıldığı, istatistikler, para dökümü.

### Bağımsız kod incelemesinden sonra düzeltilenler
- 11. görev hedefi vurmadan bitebiliyordu; 12. görevde "korumalar onu taşıdı" durumu hiç tetiklenmiyordu — ikisi de düzeltildi.
- Korunan kişiyi kendin yaralar/bayıltırsan görev artık takılı kalmıyor (başarısız sayılıyor); kuryeyi iğneyle bayıltmak da geçerli.
- Yerdeki/baygın birine isabet onu ayağa kaldırmıyor; senaryo gereği hareket eden korumalar artık yanlışlıkla alarm vermiyor.
- Yeniden başlatınca havadaki mermi yeni denemeye düşmüyor; ekip/tekne geri sayımı aracın gerçek varış süresini gösteriyor.
- Sonuç ekranının arkasında dürbün açık kalmıyor; iPad'de brifing, cephanelik ve telsiz kutusu taşmıyor/çakışmıyor.
- HUD her karede çöp üretmiyor (metinler sadece değişince yenileniyor) → daha akıcı.

## Videoda görülen sorunlar → çözüm
| Sorun | Çözüm |
|---|---|
| Konsolda sürekli "MeshCollider doesn't exist" hatası | Kodda hiç `CreatePrimitive` kalmadı, çarpışma şekilleri editörde prefab'lara gömülüyor, motor kodu kırpma kapalı + `link.xml` |
| Development konsolu görünüyordu | Build her zaman "release" (development kapalı) |
| Karanlık, düz görüntü; su kar gibi; sis çok yoğun; karakterler siyah siluet | Linear renk uzayı, HDRI gökyüzü (güneş yönü gökyüzüyle eşleşik), üç renkli ortam ışığı, yansıma probu, yeni su shader'ı, daha hafif sis, PBR materyaller (normal/metal/pürüzsüzlük haritaları), ACES ton eşleme + bloom |
| Dürbün ipucu yazısı nefes çubuğunun üstüne biniyordu, zoom çubuğu NEFES TUT tuşunu kapatıyordu | Arayüz baştan yazıldı: her öğenin ayrılmış bir alanı var (sol alt: nefes + zoom, sağ alt: ateş/dürbün/şarjör, üst: görev/rüzgar/alarm), güvenli alan (çentik) desteği, ipuçları üst ortada |
| Hedef tanınmıyordu (koruma ve işçi vurulup görev bitti) | Dürbünle tarama + kimlik kartı (HEDEF / KORUMA / SİVİL), hedef üstünde kırmızı işaret ve mesafe, ekran dışındaysa kenarda ok, brifingde hedefin boydan fotoğrafı ve kıyafet tarifi; ilk sivil kayıpta uyarı, ikincide görev biter |
| Sade sonuç ekranı | Yıldızlar, istatistikler (mesafe, kafadan vuruş, süre, isabet, sivil, alarm), para sayacı |
| Yengeç gibi kayarak yürüme | Karakterler MakeHuman ile yeniden yapıldı; CMU mocap + yapay zekâ animasyonları iskelete yeniden hedeflendi, yürüme hızı animasyonla senkron (kayma yok) |

## Yeni içerik
- 8 görevlik kampanya (gündüz, gün batımı, yağmurlu, gece): gümrük kapısı, liman ofisi, konteyner takası, gece nöbeti, depodaki köstebek, rıhtım kaçağı, kaptan köşkü, yat partisi.
- Liman: Poly Haven ve NVIDIA SimReady'den 90'dan fazla ücretsiz model; konteyner sahası, 3 vinç, kargo gemisi, depolar, yakıt tankları, dalgakıranlar, deniz feneri, karşı kıyıda tepeler ve şehir.
- 19 gerçekçi karakter, 47 animasyon, ragdoll, kill cam (son hedefte ağır çekim mermi kamerası).
- Alarm sistemi: koruma ceset görürse telsize uzanır, birkaç saniyen var; alarm verilirse hedef kaçar.
- Geliştirmeler (dürbün büyütme, sabit nişan, nefes), ayarlar (hassasiyet, nişan yardımı, kill cam, titreşim, ses, grafik kalitesi), lisans/kaynak ekranı.
- `test_sim.sh`: oyunu simülatörde kendi kendine oynatıp tüm ekranların görüntüsünü `AutoTest/` klasörüne kaydeder.

## Kod incelemesinden sonra düzeltilenler
- Arayüz katman sırası: sayfa arka planları ve dokunma alanı artık düğmelerin üstüne binmiyor (tüm düğmeler basılabilir).
- Ayarlar'daki "İlerlemeyi sıfırla" onay penceresi artık Ayarlar'ın üstünde açılıyor.
- Görev bittikten sonra Duraklat düğmesi kilitli; sonuç ekranından menüye her dönüş görevi tamamen kapatıyor.
- Ekran her zaman tam çözünürlükte: yazılar ve ikonlar keskin (kalite ayarı sadece gölge/kenar yumuşatma/ışıkları değiştirir).
- Dürbünde kimlik kartı artık retikülün altında, bildirimlerle çakışmıyor; iPad'de brifing ipucu kutusu BAŞLA'nın üstüne taşınıyor.
- Nişan alırken küçük parmak hareketleri de görüşü çeviriyor (sürükleme eşiği kaldırıldı); zoom çubuğuna dokununca da zoom yapıyor.
- Kameranın arkasındaki hedefin ok işareti doğru yönü gösteriyor.
- Kaçan siviller ve arama yapan korumalar artık havada / suyun üstünde yürümüyor, duvarların içinden geçmiyor (yol kontrolü).
- Kill cam sadece gerçekten isabet edecek atışlarda açılıyor; yürüyen karakterlerde uzuv titremesi giderildi.
- Ses: perdesi kayan efektler ve çift su sıçrama sesi düzeltildi; bazı malzemelerin simsiyah görünmesine yol açan doku kanalı kullanılmıyor.

## Görüntü: önizleme ile oyun arasındaki fark giderildi
| Sorun (ekran görüntülerinden ölçüldü) | Çözüm |
|---|---|
| Oyun önizlemeye göre ~2 kat parlak, soluk | Blender'ın kullandığı AgX ton eşleme oyuna taşındı (önceki ACES formülünde ön çarpan eksikti + 1.25 pozlama) |
| Sis çok yoğun, karşı kıyı ve kasabalar kayboluyordu | Sis 3-4 kat azaltıldı, rengi gökyüzünün ufkuna eşlendi |
| Su gümüş / kar gibi, sadece gökyüzünü yansıtıyordu | Suya gerçek ayna yansıması (vinçler, gemi, tekneler, dalgakıran, karşı kıyı), yeni dalga dokusu (tekrar etmiyor) |
| Ortam ışığı her yere eşit, her şey düz | Ortam ışığı gökyüzü dokusundan hesaplanıyor (Blender'daki gibi) + yerden yansıyan ışık; zeminde konteyner/bina/araç diplerinde yumuşak gölgelenme (önceden hesaplanmış AO) |
| Parlak yüzeyler hep beyaz gökyüzünü yansıtıyordu | Yansımalar artık limanın kendisinden (küp harita) |
| Kapalı hava görevinde pembe gün batımı gökyüzü | Gerçek kapalı/yağmurlu gökyüzü (Poly Haven), ıslak zemin |
| Ölü ağaç gibi görünen bitkiler | Kaldırıldı (dönüştürmede yaprakları kaybolmuştu) |
| HUD panelleri parlak sahnede griye dönüyordu | Paneller koyulaştırıldı, mermi sayacı ve hedef mesafesi okunur hale getirildi |
| Önizleme (Blender) ile oyun (Unity) farklıydı | `preview_shots.sh`: önizlemeler artık Unity'nin kendisinden, oyunun aynı ayarlarıyla alınıyor |

## Senaryolar: hedef siperde kalınca oyun tıkanmasın
| Sorun (son videodan) | Çözüm |
|---|---|
| 3. görevde hedef konteynerlerin arkasında sabit duruyor, hiç görünmüyor, vurulamıyor | Görev baştan senaryolaştırıldı: Tarık arabasıyla kapıdan girer (işaret arabanın üstünde), ara yolun ağzına park eder, açık alandan yürüyüp görünmeyen takas yerine gider. Yürürken vurulabilir; takas yerindeyken onu açığa çıkarmanın üç yolu var (aşağıda). Takas bitince arabasına döner, binerse araba geri geri çıkıp kapıdan gider |
| Ne yapılacağı belli değil | Oyuncu siperdeki hedefe 6 sn bakarsa ipucu çıkar ve HUD ateş edilecek yeri işaretler ("BURAYA ATEŞ ET"); hedef paneli "Siperin arkasında — dikkatini dağıt" / "Sese doğru bakıyor — fırsat!" yazar |
| — | **Ses tuzağı:** yakınına düşen kurşun → hedef sesin geldiği yere bakmak için açıklığa yürür (fırsat). İkinci kez ürkerse kaçmaya başlar — koşarken vurulabilir |
| — | **Araç alarmı:** hedefin arabası vurulursa alarm çalar, ışıklar yanıp söner; hedef arabayı kontrol etmeye gelir, birkaç saniye arabanın başında durur |
| — | **4. görev:** ateşin yanındaki kırmızı yakıt bidonu vurulursa patlar (yanındakiler ölür, alarm çalar) |
| — | **6. görev:** sürat teknesi önceden vurulursa motor çalışmaz: hedef tekneye varınca iskeleden geri koşmak zorunda kalır |
| — | **2, 5, 7. görev:** hedef binaya kaçarsa 10 sn sonra bakmak için bir kez daha kapıda görünür (ikinci şans) |
| Araçlar/tekneler köşelerde kayarak dönüyordu | Araç sürüşü yeniden yazıldı: virajlarda yavaşlar, yumuşak döner, durmadan önce fren yapar, park yerinden geri geri çıkabilir |

## Deniz
| Sorun | Çözüm |
|---|---|
| Binaların yansıması "flaş patlar gibi" titriyordu (düşük çözünürlüklü keskin ayna, kamera dönerken pikseller kayıyordu) | Yansıma artık dalgaların göremediğimiz küçük kısmı kadar yumuşatılıyor (dikey uzayan, gerçek sudaki gibi), keskinlik sadece karşı kıyının suya değdiği yerde kalıyor; HDR parlamalar sınırlandı (bloom kırpışmıyor) |
| Deniz cam gibi düz, gerçekçi değil | Liman ölçeğinde 3 katman dalga (uzun dalga + rüzgar dalgası + kırışıklık); yansıma dalgalarla kırılıp dalgalanıyor; yağmurlu görevde deniz daha pürüzlü, gün batımında daha sakin; güneş parıltısı dalgalarla genişliyor |
| Yağmurda ıslak zemin uzaktan bakınca fazla parlıyordu (beyazımsı) | Islaklık "nemli" seviyesine indirildi |

## sc_65 — 16 dil
- Oyun içi: EN, TR, DE, FR, ES, PT-BR, IT, RU, JA, KO, ZH-Hant, ZH-Hans, ID, PL, NL, VI. İlk açılış telefonun dili (yoksa İngilizce); Ayarlar → "Dil · Language".
- Tablolar: Assets/SilentContract/Resources/SC/Loc/<kod>.txt (Türkçe kaynak → çeviri). Rusça/Japonca/Korece/Çince harfler iPhone yazı tiplerinden gelir.
- Xcode: 16 .lproj + ATT izin metni her dilde (App Store dil listesi buradan çıkar).
- App Store Connect: alt başlık, açıklama, anahtar kelimeler, tanıtım metni ve 9 ürünün adı/açıklaması 16 dilde (+ en-GB, es-MX) yüklendi.

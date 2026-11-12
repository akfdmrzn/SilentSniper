# Silent Contract v2 — Çalıştırma

## iPhone'a kurmak
1. Terminal:  `bash ~/Desktop/projects/sniperGame/build_ios.sh`
   - İlk seferde Unity tüm modelleri içe aktarır, prefab/materyal/animasyonları oluşturur (15-30 dk).
2. Açılan Xcode projesinde: **Unity-iPhone → Signing & Capabilities → Team** seç.
3. iPhone'u kabloyla bağla, üstte cihazı seç, **▶ Run**.

## Android APK (arkadaşa göndermek için)
1. Unity kapalıyken **ANDROID_APK.command** dosyasına çift tıkla (ya da Terminal'de `bash build_android.sh`).
2. Unity'de Android modülü yoksa Unity Hub ile kendiliğinden kurulur (Android Build Support + OpenJDK + Android SDK/NDK,
   ~5-6 GB). Hub kuramazsa: Unity Hub → Installs → Unity 6 → ⚙ → Add modules → **Android Build Support** (alt seçenekleriyle).
3. İlk derleme uzun sürer (dokular Android için yeniden içe aktarılır, 30-60 dk). Bitince **Builds/Android/SilentContract.apk**
   açılır. Bu dosyayı gönder; telefonda açınca "bilinmeyen kaynaklardan yükleme" izni verip kurulur.
Not: Bu bir test sürümü (her şey açık, satın almalar ücretsiz, reklamlar yer tutucu). Android'den sonra iOS build alınırken
Unity platformu tekrar iOS'a çevirir (ilk seferde yine uzun sürer).

## Simülatörde otomatik test (isteğe bağlı)
`bash ~/Desktop/projects/sniperGame/test_sim.sh`
Oyun iPhone simülatöründe kendi kendini oynar (tüm ekranlar + tüm kontratlar); ekran görüntüleri, hata kaydı (`log.txt`)
ve uygulamanın konsol çıktısı (`player.log`) `AutoTest/` klasörüne kaydedilir. Oyun çökse bile o ana kadarki kayıt kalır.
iPad'i de test etmek için: `bash test_sim.sh hepsi` · Yeniden derlemeden tekrar: `bash test_sim.sh nobuild`
Sadece arayüz ekranları (menü, mağaza, günlük ödül, seviye kartı, cephanelik, harita seçimi; ~3 dk): `bash test_sim.sh ui`

## Unity'den önizleme görüntüleri (isteğe bağlı)
`bash ~/Desktop/projects/sniperGame/preview_shots.sh`
Unity'yi arka planda açıp oyunun gerçek ışık / su / renk ayarlarıyla her hava durumundan ve her görevden görüntü alır,
`Previews/` klasörüne kaydeder (2-4 dk). Unity açıksa aynısı menüde: **Silent Contract → Önizleme Görüntüleri Al**.

## Unity Editör'de
`Silent Contract` menüsü:
- **İçeriği Oluştur** — modellerden prefab, materyal ve animasyon bankasını yeniden üretir.
- **iPhone Build Al** / **iOS Simulator Build Al**
- Sahne: `Assets/SilentContract/Scenes/Main.unity` → ▶ Play (fare: sağ tık dürbün, tekerlek zoom, Space ateş, Shift nefes, R şarjör, C siper).

## Kontroller (telefon)
- Ekranı sürükle: nişan al · İki parmakla sıkıştır: zoom
- Sağ alt: ATEŞ (büyük), üstünde DÜRBÜN, solunda ŞARJÖR
- Sol alt (dürbündeyken): NEFES TUT ve zoom çubuğu
- Nişangahı bir kişinin üzerinde tut → kimliği taranır (HEDEF / KORUMA / SİVİL)
- Sol alt, NEFES TUT'un yanında: SİPER (tehlike başlayınca çıkar) — korkuluğun arkasına eğil / DOĞRUL. Dürbüne basınca
  kendiliğinden doğrulursun. Ekranın üstündeki göz halkası yerinin ne kadar belli olduğunu, kalp çubuğu canını gösterir.

## Gelir modeli: RevenueCat + AdMob + Firebase + Meta (sc_52)
**Ne kurulu:** satın almalar RevenueCat (StoreKit 2), reklamlar Google AdMob (ödüllü + geçiş), analitik + çökme raporu +
uzaktan ayar Firebase (Analytics, Crashlytics, Remote Config), reklam kampanyası ölçümü Meta (Facebook) SDK ve Firebase→Google Ads.
Paketler Unity açılınca kendiliğinden iner (Packages/manifest.json; Firebase dosyaları Packages/firebase/ içinde).
Bir SDK kurulu değilse ya da kimliği girilmemişse oyun yine derlenir: o servis kapalı kalır, test sürümünde yer tutucu çalışır.

**Bütün kimlikler tek dosyada:** `Assets/SilentContract/Scripts/Services/MonetizationConfig.cs` (sadece istemci anahtarları;
gizli sunucu anahtarları projeye ASLA yazılmaz).

### App Store Connect (yapıldı)
9 ürün oluşturuldu, fiyatları ve TR/EN açıklamaları girildi, 175 ülkede açık:
| Ürün kimliği (`com.akif.silentcontract.` + …) | Tür | Fiyat | İçerik |
|---|---|---|---|
| gems_80 / gems_500 / gems_1200 / gems_2800 / gems_7500 | Tüketilebilir | 0,99 / 4,99 / 9,99 / 19,99 / 49,99 $ | Elmas (bonus %25-%100) |
| starter_pack | Tüketilebilir (oyunda bir kez) | 2,99 $ | 500 elmas, $10.000, 10 zırh delici |
| no_ads | Tüketilemez | 4,99 $ | Geçiş reklamları kalkar + 100 elmas |
| pack_shadow | Tüketilemez | 4,99 $ | GÖLGE SR-7 (bütün hatlar 3. kademe), gece görüşü, 150 elmas |
| pack_titan | Tüketilemez | 14,99 $ | TİTAN .50 (2. kademe), termal dürbün, 400 elmas, 20 zırh delici |
Durumları "Missing Metadata": incelemeye göndermeden önce her ürüne **mağaza ekranının bir ekran görüntüsü** eklenmeli
(oyun çalışınca ben yüklerim). **Paid Apps sözleşmesi** (Business → Agreements) imzalı ve banka/vergi bilgisi girilmiş olmalı,
yoksa sandbox'ta da ürünler gelmez.

### RevenueCat
Silent Contract için **ayrı bir proje** gerekli (verilen anahtarlar AirControlTower projesine ait). Proje açılıp
com.akif.silentcontract App Store uygulaması eklenince: v2 gizli anahtar (yazma izinli) ile ürünleri, `no_ads`,
`pack_shadow`, `pack_titan` yetkilerini ve `default` teklifini App Store + Test Store için aynı şekilde ben kurarım.
`appl_…` anahtarı `RevenueCatAppleKey`'e, `test_…` anahtarı `RevenueCatTestKey`'e girer. RevenueCat panelinde
**App Store Connect API key** ve **In-App Purchase Key** (.p8) da girilmeli (fiyatlar ve iadeler için).
RevenueCat → Integrations: **Firebase** açılabilir (gelir olayları `rc_` adıyla ayrı gelir). **Meta entegrasyonunu AÇMA**: oyun satın alımı Meta'ya kendisi gönderiyor, ikisi birden açık olursa her satın alım iki kez sayılır.

### AdMob
`AdMobAppIdIOS`, `AdMobRewardedIOS`, `AdMobInterstitialIOS` şu an Google'ın **test** kimlikleri. Gerçekleri gelince değişir.
AdMob → Privacy & messaging: **GDPR mesajı** ve **IDFA açıklama mesajı** oluştur (Avrupa'daki oyunculara izin formu çıkar).
`app-ads.txt` dosyası geliştirici web sitesinde yayınlanmalı (AdMob verir).

### Firebase
`GoogleService-Info.plist` dosyasını **Assets/** içine koy (Assets/SilentContract/ olur). Dosya yokken Firebase kapalı kalır.
Firebase → Project settings → Integrations: **Google Ads** ve **AdMob** bağla. Google Ads'te dönüşüm olarak
`first_open`, `in_app_purchase`, `tutorial_complete`, `contract_first_clear` içe aktarılabilir.
Remote Config anahtarları (oyun varsayılanlarla çalışır, panelden güncelleme çıkarmadan değiştirilir):
`interstitial_every` (3), `interstitial_grace` (4), `interstitial_cooldown_s` (90), `free_gems_per_day` (5),
`free_gems_amount` (5), `shortfall_ads_per_day` (3), `shortfall_max_pct` (35), `replay_pay_pct` (40),
`fail_refund_pct` (25), `starter_after` (3), `noads_nudge_after` (2), `pay_scale_pct` (100).

### Meta (Facebook)
developers.facebook.com'da uygulama aç → `FacebookAppId` ve `FacebookClientToken`. Kimlik gelince Meta SDK'sını ben eklerim
(kimliksiz eklenirse iOS açılışta kapanır). Events Manager'da iOS 14+ için SKAdNetwork / Aggregated Event Measurement açılmalı.
Meta panelinde "uygulama içi satın almaları otomatik kaydet" KAPALI olsun (oyun satın alımı kendisi gönderiyor, çift sayılmasın).

### iOS derleme
`build_ios.sh` artık CocoaPods'u da kurar (yoksa Homebrew ile). Xcode'da **Unity-iPhone.xcworkspace** açılır
(.xcodeproj değil). In-App Purchase yeteneği, takip izni metni, SKAdNetwork kimlikleri ve gizlilik bildirimi
(PrivacyInfo.xcprivacy) derlemede otomatik eklenir.

### Mağaza sürümü
`Game.TestBuild = false` (Scripts/Core/Game.cs): test mağazası, test reklamları ve "her şey açık" test modu kapanır.
App Store Connect → App Privacy: Identifiers (Device ID — reklam, takip), Usage Data (Product Interaction, Advertising Data),
Purchases, Diagnostics (Crash, Performance). **Gizlilik politikası** adresi zorunlu: docs/privacy.html taslağını bir web
sayfasına koy (ör. GitHub Pages) ve adresi App Store Connect'e ve `PrivacyUrl`'e yaz.

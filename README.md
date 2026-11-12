# Silent Contract v2 — Çalıştırma

## iPhone'a kurmak
1. Terminal:  `bash ~/Desktop/projects/sniperGame/build_ios.sh`
   - İlk seferde Unity tüm modelleri içe aktarır, prefab/materyal/animasyonları oluşturur (15-30 dk).
2. Açılan Xcode projesinde: **Unity-iPhone → Signing & Capabilities → Team** seç.
3. iPhone'u kabloyla bağla, üstte cihazı seç, **▶ Run**.

## Simülatörde otomatik test (isteğe bağlı)
`bash ~/Desktop/projects/sniperGame/test_sim.sh`
Oyun iPhone simülatöründe kendi kendini oynar (tüm ekranlar + tüm kontratlar); ekran görüntüleri, hata kaydı (`log.txt`)
ve uygulamanın konsol çıktısı (`player.log`) `AutoTest/` klasörüne kaydedilir. Oyun çökse bile o ana kadarki kayıt kalır.
iPad'i de test etmek için: `bash test_sim.sh hepsi` · Yeniden derlemeden tekrar: `bash test_sim.sh nobuild`

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

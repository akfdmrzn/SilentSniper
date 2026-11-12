#!/bin/bash
# Silent Contract — Android APK üretir (arkadaşa göndermek için test sürümü).
#   bash build_android.sh            -> Builds/Android/SilentContract.apk
#   bash build_android.sh rebuild    -> içerik (modeller/prefab) de baştan kurulur
# Unity'de Android modülü yoksa önce Unity Hub ile kurar (Android Build Support + OpenJDK + Android SDK/NDK, ~5-6 GB).
# İlk Android derlemesi uzun sürer (bütün dokular Android için yeniden içe aktarılır: 30-60 dk). Sonrakiler kısa.
set -e
PROJECT="$(cd "$(dirname "$0")" && pwd)"
LOG="$PROJECT/Builds/build_android.log"
mkdir -p "$PROJECT/Builds"
VER=$(grep "m_EditorVersion:" "$PROJECT/ProjectSettings/ProjectVersion.txt" | awk '{print $2}')
EDITOR_DIR="/Applications/Unity/Hub/Editor/$VER"
UNITY="$EDITOR_DIR/Unity.app/Contents/MacOS/Unity"
if [ -n "$UNITY_PATH" ]; then UNITY="$UNITY_PATH"; EDITOR_DIR="$(dirname "$(dirname "$(dirname "$(dirname "$UNITY")")")")"; fi
if [ ! -x "$UNITY" ]; then
  UNITY=$(ls -d /Applications/Unity/Hub/Editor/6000*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -1)
  EDITOR_DIR="$(dirname "$(dirname "$(dirname "$(dirname "$UNITY")")")")"
  VER=$(basename "$EDITOR_DIR")
fi
if [ ! -x "$UNITY" ]; then echo "❌ Unity 6 bulunamadı (/Applications/Unity/Hub/Editor)."; exit 1; fi
echo "Unity: $VER"

ANDROID="$EDITOR_DIR/PlaybackEngines/AndroidPlayer"
if [ ! -d "$ANDROID" ] || [ ! -d "$ANDROID/SDK" ] || [ ! -d "$ANDROID/OpenJDK" ]; then
  echo "Android modülü kuruluyor (Unity Hub üzerinden, internet hızına göre 10-30 dk)..."
  HUB="/Applications/Unity Hub.app/Contents/MacOS/Unity Hub"
  if [ ! -x "$HUB" ]; then echo "❌ Unity Hub bulunamadı. Unity Hub → Installs → $VER → ⚙ → Add modules → Android Build Support (alt seçenekleriyle) kur, sonra bunu tekrar çalıştır."; exit 1; fi
  "$HUB" -- --headless install-modules --version "$VER" -m android --childModules 2>&1 | tail -20 || true
  if [ ! -d "$ANDROID/SDK" ]; then
    echo "❌ Android modülü kurulamadı. Unity Hub → Installs → $VER → ⚙ → Add modules → 'Android Build Support' ve altındaki"
    echo "   'OpenJDK' ile 'Android SDK & NDK Tools' işaretle, kur; sonra bu dosyayı tekrar çalıştır."
    exit 1
  fi
  echo "✅ Android modülü kuruldu."
fi

if pgrep -fl "Unity.app/Contents/MacOS/Unity" 2>/dev/null | grep -q -- "$PROJECT"; then
  echo "❌ Unity bu projeyle açık. Unity'yi kapatıp tekrar çalıştır."; exit 1
fi

EXTRA=""; [ "$1" = "rebuild" ] && EXTRA="-rebuildContent"
echo "APK derleniyor... (ilk seferde 30-60 dk sürebilir; log: $LOG)"
"$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" -buildTarget Android \
  -executeMethod SilentContract.EditorTools.AndroidBuild.Build $EXTRA -logFile "$LOG" && CODE=0 || CODE=$?

APK="$PROJECT/Builds/Android/SilentContract.apk"
if [ "$CODE" = "0" ] && [ -f "$APK" ]; then
  echo ""
  echo "✅ APK hazır: $APK  ($(du -h "$APK" | cut -f1))"
  echo "   Arkadaşına bu dosyayı gönder. Telefonda açınca 'bilinmeyen kaynaklardan yükleme' izni istenir, izin verince kurulur."
  [ -z "$NO_OPEN" ] && open -R "$APK"
else
  echo ""
  echo "❌ Android derlemesi başarısız (kod $CODE). Log: $LOG"
  grep -n -E "error CS|Error|EXCEPTION|Exception|FAILURE|What went wrong|\[SC\]" "$LOG" | head -60
  exit 1
fi

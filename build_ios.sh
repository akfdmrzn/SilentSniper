#!/bin/bash
# Silent Contract — iPhone için Xcode projesi üretir.
# Kullanım:
#   bash build_ios.sh               -> gerçek iPhone için (Builds/iOS)
#   bash build_ios.sh simulator     -> Xcode Simulator için (Builds/iOS-Simulator)
#   bash build_ios.sh simulator autotest -> kendi kendini oynayıp ekran görüntüsü alan test sürümü
set -e
PROJECT="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$PROJECT/Builds"
EXTRA=""; OUT="iOS"
for a in "$@"; do
  case "$a" in
    simulator|--simulator) EXTRA="$EXTRA -simulator"; OUT="iOS-Simulator" ;;
    autotest|--autotest) EXTRA="$EXTRA -autotest" ;;
    rebuild|--rebuild) EXTRA="$EXTRA -rebuildContent" ;;
  esac
done

# CocoaPods: the store, ad and analytics SDKs (RevenueCat, AdMob, Firebase) come into the Xcode project through it
export LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8
ensure_pods() {
  command -v pod >/dev/null 2>&1 && return 0
  for d in /opt/homebrew/bin /usr/local/bin "$HOME"/.gem/ruby/*/bin "$HOME"/.local/share/gem/ruby/*/bin; do
    [ -x "$d/pod" ] && export PATH="$d:$PATH" && return 0
  done
  echo "CocoaPods kuruluyor (tek seferlik, birkaç dakika)..."
  if command -v brew >/dev/null 2>&1 || [ -x /opt/homebrew/bin/brew ] || [ -x /usr/local/bin/brew ]; then
    BREW=$(command -v brew || ls /opt/homebrew/bin/brew /usr/local/bin/brew 2>/dev/null | head -1)
    "$BREW" install cocoapods && export PATH="$(dirname "$BREW"):$PATH" && command -v pod >/dev/null 2>&1 && return 0
  fi
  # system Ruby (macOS 2.6): the last CocoaPods line that still runs on it
  gem install --user-install activesupport -v 6.1.7.10 >/dev/null 2>&1
  gem install --user-install cocoapods -v 1.15.2 && for d in "$HOME"/.gem/ruby/*/bin; do export PATH="$d:$PATH"; done
  command -v pod >/dev/null 2>&1
}
if ! ensure_pods; then
  echo "⚠️  CocoaPods kurulamadı. Terminalde bir kez şunu çalıştır:  brew install cocoapods   (Homebrew yoksa: https://brew.sh)"
fi

UNITY=$(ls -d /Applications/Unity/Hub/Editor/6000*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -1)
if [ -n "$UNITY_PATH" ]; then UNITY="$UNITY_PATH"; fi
if [ -z "$UNITY" ]; then
  echo "Unity 6 bulunamadı. Kurulu sürümler:"; ls /Applications/Unity/Hub/Editor/ 2>/dev/null
  echo "Farklı bir yerdeyse:  UNITY_PATH=/yol/Unity.app/Contents/MacOS/Unity bash build_ios.sh"
  exit 1
fi
echo "Unity: $UNITY"
echo "Proje: $PROJECT"
echo "Build başlıyor. İlk seferde modeller içe aktarılıp prefab'lar oluşturulduğu için 15-30 dk sürebilir..."

"$UNITY" -batchmode -quit -nographics \
  -projectPath "$PROJECT" \
  -buildTarget iOS \
  -executeMethod SilentContract.EditorTools.IOSBuild.Build $EXTRA \
  -logFile "$PROJECT/Builds/build_ios.log" && CODE=0 || CODE=$?

if [ "$CODE" = "0" ] && [ -d "$PROJECT/Builds/$OUT/Unity-iPhone.xcodeproj" ]; then
  # the SDK pods (Unity's resolver normally installs them during the build; this catches the case where it could not)
  if [ -f "$PROJECT/Builds/$OUT/Podfile" ] && { [ ! -f "$PROJECT/Builds/$OUT/Podfile.lock" ] || [ ! -d "$PROJECT/Builds/$OUT/Unity-iPhone.xcworkspace" ]; }; then
    if ! command -v pod >/dev/null 2>&1; then
      echo "❌ CocoaPods yok: reklam (AdMob) ve Meta kütüphaneleri kurulamadı. Terminalde:  brew install cocoapods  sonra bu betiği tekrar çalıştır."
      exit 1
    fi
    echo "pod install (AdMob, Meta)..."
    (cd "$PROJECT/Builds/$OUT" && pod install --repo-update) || { echo "❌ pod install başarısız"; exit 1; }
  fi
  XC="$PROJECT/Builds/$OUT/Unity-iPhone.xcodeproj"
  [ -d "$PROJECT/Builds/$OUT/Unity-iPhone.xcworkspace" ] && XC="$PROJECT/Builds/$OUT/Unity-iPhone.xcworkspace"
  echo ""
  echo "✅ Xcode projesi hazır: $XC"
  echo "   (Xcode'da HER ZAMAN .xcworkspace dosyasını aç: reklam/satın alma/analitik kütüphaneleri onun içinde)"
  if [ -z "$NO_OPEN" ]; then open "$XC"; fi
else
  echo ""
  echo "❌ Build başarısız (kod $CODE). Log: $PROJECT/Builds/build_ios.log"
  grep -n -E "error CS|Error|EXCEPTION|Exception|\[SC\]" "$PROJECT/Builds/build_ios.log" | head -60
  exit 1
fi

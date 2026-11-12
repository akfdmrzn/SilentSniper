#!/bin/bash
# Silent Contract — otomatik test: Simulator sürümünü derler, simülatörde oyunu kendi kendine oynatır (tüm ekranlar +
# tüm kontratlar), ekran görüntülerini ve hata kaydını AutoTest/<cihaz>/ klasörüne kaydeder:
#   log.txt     — her kontratın sonucu, FPS ve oyunun verdiği tüm hatalar (oyun çökse bile o ana kadarki kısım kalır)
#   player.log  — uygulamanın kendi konsol çıktısı (Xcode konsolunda görünenin aynısı)
#   *.ips       — uygulama çöktüyse macOS'un çökme raporu
# Kullanım:  bash test_sim.sh             (iPhone simülatörü; derleme dahil ~40-60 dk, başında beklemen gerekmez)
#            bash test_sim.sh hepsi       (iPhone + iPad)
#            bash test_sim.sh nobuild     (son derlenmiş uygulamayı tekrar test eder; "nobuild hepsi" de olur)
#            bash test_sim.sh hepsi kayit en   (İngilizce, iPhone'da ilk 7 dakikanın ekran kaydıyla)
# Not: Test sürerken Unity'de bu projeyi açma ve build_ios.sh'ı aynı anda çalıştırma.
set -e
PROJECT="$(cd "$(dirname "$0")" && pwd)"
BUNDLE="com.akif.silentcontract"
OUTDIR="$PROJECT/AutoTest"
DD="$PROJECT/Builds/sim-derived"
# xcode-select bazen CommandLineTools'u gösterir (xcodebuild/simctl çalışmaz): Xcode.app varsa onu kullan
if ! xcodebuild -version > /dev/null 2>&1 && [ -d /Applications/Xcode.app/Contents/Developer ]; then
  export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
fi
BUILD=1; ALL=0; UIARG=""; RECORD=0; REC_SECS=420; LANGARGS=()
for a in "$@"; do
  case "$a" in
    nobuild) BUILD=0 ;;
    hepsi|all|ipad) ALL=1 ;;
    ui|arayuz) UIARG="-autotest-ui" ;;     # sadece menü ekranları (görevler oynanmaz, ~3 dk)
    kayit|record) RECORD=1 ;;              # iPhone simülatöründe ilk 7 dakikanın ekran kaydı (kayit.mp4)
    en|ingilizce) LANGARGS=(-AppleLanguages "(en)" -AppleLocale en_US) ;;   # oyun İngilizce açılır
    ping) mkdir -p "$PROJECT/_sc2_transfer"; echo "pong $(date '+%Y-%m-%d %H:%M:%S')" > "$PROJECT/_sc2_transfer/PONG"; exit 0 ;;
  esac
done

if [ "$BUILD" = "1" ]; then
  NO_OPEN=1 bash "$PROJECT/build_ios.sh" simulator autotest
  echo "Xcode ile simülatör için derleniyor..."
  XCARG=(-project "$PROJECT/Builds/iOS-Simulator/Unity-iPhone.xcodeproj")
  [ -d "$PROJECT/Builds/iOS-Simulator/Unity-iPhone.xcworkspace" ] && XCARG=(-workspace "$PROJECT/Builds/iOS-Simulator/Unity-iPhone.xcworkspace")
  xcodebuild "${XCARG[@]}" -scheme Unity-iPhone -configuration Release \
    -sdk iphonesimulator -derivedDataPath "$DD" CODE_SIGNING_ALLOWED=NO ONLY_ACTIVE_ARCH=YES build > "$PROJECT/Builds/xcodebuild_sim.log" 2>&1 \
    || { echo "❌ xcodebuild başarısız. Log: $PROJECT/Builds/xcodebuild_sim.log"; grep -n "error:" "$PROJECT/Builds/xcodebuild_sim.log" | head -30; exit 1; }
fi
APP=$(find "$DD/Build/Products" -maxdepth 2 -name "*.app" -type d | head -1)
if [ -z "$APP" ]; then echo "❌ .app bulunamadı ($DD)"; exit 1; fi
EXE=$(/usr/libexec/PlistBuddy -c "Print CFBundleExecutable" "$APP/Info.plist" 2>/dev/null || true)
echo "Uygulama: $APP"

pick() {  # $1 = iPhone | iPad  -> UDID of the newest available device of that family
  xcrun simctl list devices available -j | /usr/bin/python3 -c '
import json, sys
fam = sys.argv[1]
d = json.load(sys.stdin)["devices"]
best = None
for rt, devs in d.items():
    if "iOS" not in rt: continue
    for x in devs:
        if x["name"].startswith(fam) and (fam != "iPhone" or "Pro" in x["name"] or best is None):
            best = (rt, x["udid"], x["name"])
print(best[1] + "|" + best[2] if best else "")' "$1"
}

run_on() {
  local info="$1"; local UDID="${info%%|*}"; local NAME="${info##*|}"
  if [ -z "$UDID" ]; then echo "Simülatör yok: $2"; return 0; fi
  echo "=== $NAME ($UDID)"
  local DEST="$OUTDIR/$(echo "$NAME" | tr ' ' '_')"
  # the previous run is kept (AutoTest_arsiv/<cihaz>_<saat>), the last 6 runs
  if [ -d "$DEST" ] && [ -f "$DEST/log.txt" ]; then
    mkdir -p "$PROJECT/AutoTest_arsiv"
    mv "$DEST" "$PROJECT/AutoTest_arsiv/$(basename "$DEST")_$(date +%m%d_%H%M)"
    ls -dt "$PROJECT/AutoTest_arsiv"/* 2>/dev/null | tail -n +7 | xargs rm -rf
  fi
  rm -rf "$DEST"; mkdir -p "$DEST"
  local TMP; TMP=$(mktemp -d /tmp/sc_autotest.XXXXXX)
  touch "$TMP/stamp"
  xcrun simctl boot "$UDID" 2>/dev/null || true
  xcrun simctl bootstatus "$UDID" -b > /dev/null
  xcrun simctl uninstall "$UDID" "$BUNDLE" 2>/dev/null || true
  xcrun simctl install "$UDID" "$APP"
  local RECPID=""
  if [ "$RECORD" = "1" ] && [ "$2" = "iPhone" ]; then
    xcrun simctl io "$UDID" recordVideo --codec=h264 --force "$DEST/kayit.mp4" > /dev/null 2>&1 &
    RECPID=$!
    sleep 2
    ( sleep "$REC_SECS"; kill -INT "$RECPID" 2>/dev/null ) &
  fi
  xcrun simctl launch --stdout="$TMP/stdout.log" --stderr="$TMP/stderr.log" "$UDID" "$BUNDLE" -autotest $UIARG "${LANGARGS[@]}" > /dev/null 2>&1 \
    || xcrun simctl launch "$UDID" "$BUNDLE" -autotest $UIARG "${LANGARGS[@]}" > /dev/null
  local DATA; DATA=$(xcrun simctl get_app_container "$UDID" "$BUNDLE" data)
  local LOG="$DATA/Documents/autotest/log.txt"
  local T=0 SEEN=0 LAST="" NOTE="bitti" M N
  # wait for DONE; give up when the log has not moved for 7 minutes (crash / hang) or after 90 minutes
  until [ -f "$DATA/Documents/autotest/DONE" ]; do
    sleep 5; T=$((T+5))
    if [ -f "$LOG" ]; then
      M=$(stat -f %m "$LOG" 2>/dev/null || true)
      if [ "$M" != "$LAST" ]; then LAST="$M"; SEEN=$T; fi
    fi
    if [ $((T % 60)) -eq 0 ]; then
      N=$(grep -c '^=== ' "$LOG" 2>/dev/null || true)
      echo "  … $((T/60)) dk, kontrat: ${N:-0}"
    fi
    if [ $((T - SEEN)) -ge 420 ]; then NOTE="⚠️  7 dakikadır ilerleme yok — uygulama çöktü ya da takıldı"; break; fi
    if [ $T -ge 5400 ]; then NOTE="⚠️  90 dakika doldu, test yarıda kesildi"; break; fi
  done
  if [ -n "$RECPID" ]; then kill -INT "$RECPID" 2>/dev/null || true; wait "$RECPID" 2>/dev/null || true; fi
  cp -R "$DATA/Documents/autotest/." "$DEST/" 2>/dev/null || true
  xcrun simctl terminate "$UDID" "$BUNDLE" 2>/dev/null || true
  sleep 2
  cat "$TMP/stdout.log" "$TMP/stderr.log" > "$DEST/player.log" 2>/dev/null || true
  if [ -n "$EXE" ]; then
    find "$HOME/Library/Logs/DiagnosticReports" -maxdepth 1 -name "${EXE}*" -newer "$TMP/stamp" -exec cp {} "$DEST/" \; 2>/dev/null || true
  fi
  rm -rf "$TMP"
  echo "→ $NOTE. $(ls "$DEST" | wc -l | tr -d ' ') dosya: $DEST"
  if [ -f "$DEST/log.txt" ]; then grep -E '^(errors|contracts) ' "$DEST/log.txt" || true; fi
  return 0
}

mkdir -p "$OUTDIR"
run_on "$(pick iPhone)" iPhone
if [ "$ALL" = "1" ]; then run_on "$(pick iPad)" iPad; fi
echo ""
echo "✅ Test bitti. Sonuçlar: $OUTDIR"
open "$OUTDIR"

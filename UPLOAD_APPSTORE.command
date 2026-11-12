#!/bin/bash
# Silent Sniper — derle, arşivle ve App Store Connect'e yükle (Xcode'daki Apple hesabıyla, otomatik imza).
cd "$(dirname "$0")"
PROJECT="$(pwd)"
LOG="$PROJECT/Builds/upload.log"
mkdir -p Builds
exec > >(tee "$LOG") 2>&1
echo "== $(date) Unity derlemesi"
NO_OPEN=1 bash build_ios.sh || { echo "UPLOAD_FAILED unity"; exit 1; }
WS="$PROJECT/Builds/iOS/Unity-iPhone.xcworkspace"
ARCH="$PROJECT/Builds/SilentSniper.xcarchive"
rm -rf "$ARCH" "$PROJECT/Builds/export"
echo "== $(date) Xcode arşivi"
xcodebuild -workspace "$WS" -scheme Unity-iPhone -configuration Release -destination "generic/platform=iOS" \
  -archivePath "$ARCH" -allowProvisioningUpdates DEVELOPMENT_TEAM=G8AN745JCW CODE_SIGN_STYLE=Automatic archive \
  | grep -E "error:|warning: .*signing|ARCHIVE SUCCEEDED|ARCHIVE FAILED|\*\* " || true
[ -d "$ARCH" ] || { echo "UPLOAD_FAILED archive"; exit 1; }
echo "== tracking keys in the archive:"
grep -A1 "NSPrivacyTracking" "$ARCH"/Products/Applications/*.app/Frameworks/UnityFramework.framework/PrivacyInfo.xcprivacy
cat > "$PROJECT/Builds/ExportOptions.plist" <<'P'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>method</key><string>app-store-connect</string>
<key>destination</key><string>upload</string>
<key>teamID</key><string>G8AN745JCW</string>
<key>signingStyle</key><string>automatic</string>
<key>uploadSymbols</key><true/>
<key>manageAppVersionAndBuildNumber</key><false/>
</dict></plist>
P
echo "== $(date) App Store Connect'e yükleniyor"
xcodebuild -exportArchive -archivePath "$ARCH" -exportOptionsPlist "$PROJECT/Builds/ExportOptions.plist" \
  -exportPath "$PROJECT/Builds/export" -allowProvisioningUpdates && echo "UPLOAD_OK" || echo "UPLOAD_FAILED export"

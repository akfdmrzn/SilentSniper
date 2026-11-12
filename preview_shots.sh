#!/bin/bash
# Silent Contract — Unity'nin kendisinden önizleme görüntüleri (oyunun gerçek ışık / su / renk ayarlarıyla).
# Görüntüler Previews/ klasörüne kaydedilir. Unity editörü bu projeyle AÇIK OLMAMALI (açıksa menüden:
# Silent Contract > Önizleme Görüntüleri Al).
# Kullanım:  bash preview_shots.sh        (~2-4 dk)
PROJECT="$(cd "$(dirname "$0")" && pwd)"
UNITY=$(ls -d /Applications/Unity/Hub/Editor/6000*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -1)
if [ -n "$UNITY_PATH" ]; then UNITY="$UNITY_PATH"; fi
if [ -z "$UNITY" ]; then echo "Unity 6 bulunamadı."; exit 1; fi
if [ -f "$PROJECT/Temp/UnityLockfile" ] && lsof "$PROJECT/Temp/UnityLockfile" > /dev/null 2>&1; then
  echo "Unity bu projeyle açık. Editörde: Silent Contract > Önizleme Görüntüleri Al"; exit 1
fi
mkdir -p "$PROJECT/Previews"
echo "Önizleme görüntüleri hazırlanıyor (Unity arka planda açılıyor, 2-4 dk)..."
"$UNITY" -batchmode -projectPath "$PROJECT" -buildTarget iOS \
  -executeMethod SilentContract.EditorTools.PreviewShots.RunBatch \
  -logFile "$PROJECT/Previews/unity.log"
CODE=$?
N=$(ls "$PROJECT/Previews"/*.png 2>/dev/null | wc -l | tr -d ' ')
if [ "$N" -gt 0 ]; then
  echo "✅ $N görüntü: $PROJECT/Previews"
  open "$PROJECT/Previews"
else
  echo "❌ Görüntü oluşmadı (kod $CODE). Log: $PROJECT/Previews/unity.log"
  grep -n -E "error CS|Exception|Shader error|\[SC\]" "$PROJECT/Previews/unity.log" | head -40
  exit 1
fi

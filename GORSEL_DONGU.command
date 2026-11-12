#!/bin/bash
# Silent Sniper — görsel çalışma dinleyicisi. Bir kez çift tıkla, pencere açık kalsın (Unity editörü KAPALI olmalı).
# Claude _sc2_transfer/RUN_SHOTS dosyasını (çekim listesi) bırakınca Unity'yi arka planda açar, oyunun gerçek
# ışığıyla kareleri Previews/ klasörüne çizer ve tekrar beklemeye geçer.
cd "$(dirname "$0")"
PROJECT="$(pwd)"
caffeinate -dimsu -w $$ &
UNITY=$(ls -d /Applications/Unity/Hub/Editor/6000*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -1)
echo "Görsel dinleyici çalışıyor ($UNITY). Bu pencereyi kapatma."
mkdir -p Previews _sc2_transfer
echo "IDLE $(date '+%Y-%m-%d %H:%M:%S')" > _sc2_transfer/SHOTS_STATUS
while true; do
  if [ -f _sc2_transfer/RUN_SHOTS ]; then
    mv -f _sc2_transfer/RUN_SHOTS Previews/request.json
    echo "=== $(date '+%H:%M:%S') çekim başlıyor"
    echo "RUNNING $(date '+%Y-%m-%d %H:%M:%S')" > _sc2_transfer/SHOTS_STATUS
    "$UNITY" -batchmode -projectPath "$PROJECT" -buildTarget iOS \
      -executeMethod SilentContract.EditorTools.PreviewShots.RunBatch \
      -logFile "$PROJECT/Previews/unity.log"
    CODE=$?
    N=$(ls Previews/*.png 2>/dev/null | wc -l | tr -d ' ')
    echo "DONE $CODE $N $(date '+%Y-%m-%d %H:%M:%S')" > _sc2_transfer/SHOTS_STATUS
    echo "=== $(date '+%H:%M:%S') bitti: $N kare (kod $CODE), bekliyor"
  fi
  sleep 5
done

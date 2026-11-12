#!/bin/bash
# Silent Contract — otomatik test dinleyicisi. Bir kez çift tıkla, pencere açık kalsın.
# Claude yeni bir sürüm kurduğunda _sc2_transfer/RUN_TEST dosyasını bırakır; bu pencere onu görünce
# simülatör build'ini alır, oyunu baştan sona oynatır (test_sim.sh) ve tekrar beklemeye geçer.
cd "$(dirname "$0")"
caffeinate -dimsu -w $$ &
echo "Otomatik test dinleyicisi çalışıyor. Bu pencereyi kapatma."
while true; do
  if [ -f _sc2_transfer/RUN_TEST ]; then
    ARGS=$(cat _sc2_transfer/RUN_TEST)
    rm -f _sc2_transfer/RUN_TEST
    echo "=== $(date '+%H:%M:%S') test başlıyor ($ARGS)"
    echo "RUNNING $(date '+%Y-%m-%d %H:%M:%S')" > _sc2_transfer/TEST_STATUS
    bash test_sim.sh $ARGS 2>&1 | tee AutoTest_run.log
    echo "BITTI" >> AutoTest_run.log
    echo "DONE $(date '+%Y-%m-%d %H:%M:%S')" > _sc2_transfer/TEST_STATUS
    echo "=== $(date '+%H:%M:%S') test bitti, bekliyor"
  fi
  if [ -f _sc2_transfer/RUN_ANDROID ]; then
    rm -f _sc2_transfer/RUN_ANDROID
    echo "=== $(date '+%H:%M:%S') Android APK derleniyor"
    echo "ANDROID $(date '+%Y-%m-%d %H:%M:%S')" > _sc2_transfer/TEST_STATUS
    NO_OPEN=1 bash build_android.sh 2>&1 | tee Builds/android_son.txt
    echo "DONE $(date '+%Y-%m-%d %H:%M:%S')" > _sc2_transfer/TEST_STATUS
    echo "=== $(date '+%H:%M:%S') APK işi bitti, bekliyor"
  fi
  sleep 10
done

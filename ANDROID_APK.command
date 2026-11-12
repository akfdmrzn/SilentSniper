#!/bin/bash
# Çift tıkla: Android APK'yı üretir (Unity kapalı olmalı). Bitince APK'nın klasörü açılır.
cd "$(dirname "$0")"
bash build_android.sh "$@" 2>&1 | tee "Builds/android_son.txt"
echo ""
read -n 1 -s -r -p "Kapatmak için bir tuşa bas..."

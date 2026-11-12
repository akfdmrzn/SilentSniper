#!/bin/bash
# Claude'un başlattığı otomatik simülatör testi: derler, simülatörde bütün oyunu oynar, sonuçları AutoTest/ altına yazar.
cd "$(dirname "$0")"
bash test_sim.sh 2>&1 | tee AutoTest_run.log
echo "BITTI" >> AutoTest_run.log

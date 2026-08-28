#!/bin/bash
# Valida as receitas de popular conteineres de variante, com screenshot por run.
# Espera o jogo fechar, instala a build, roda: controle / B1 (Generate) / B2 (surgical).
cd /c/Users/nicolas/Documents/dump/peak-recon || exit 1
PEAK="/d/SteamLibrary/steamapps/common/PEAK"
OUT="testlogs/valid2"
mkdir -p "$OUT"

echo "esperando o jogo fechar..." > "$OUT/status.txt"
for i in $(seq 1 180); do
  tasklist //FI "IMAGENAME eq PEAK.exe" 2>/dev/null | grep -q PEAK.exe || break
  sleep 5
done
if tasklist //FI "IMAGENAME eq PEAK.exe" 2>/dev/null | grep -q PEAK.exe; then
  echo "TIMEOUT: jogo continuou aberto por 15 min" > "$OUT/status.txt"
  exit 1
fi
sleep 3

cp PeakLab.dll "$PEAK/BepInEx/plugins/PeakLab.dll" || { echo "ERRO: cp da DLL falhou" > "$OUT/status.txt"; exit 1; }

run_test() {
  name="$1"; seed="$2"; genafter="$3"; populate="$4"
  echo "rodando $name..." >> "$OUT/status.txt"
  printf '[Geracao]\nRandomizeBiomeVariants = true\nFullRegenerate = false\nGenerateAposVariantes = %s\nPopularVariantesAtivas = %s\n\n[Avancado]\nMontanha = Auto\nPraia = Auto\nSelva = Auto\nNeve = Auto\n' "$genafter" "$populate" > "$PEAK/BepInEx/config/nicolas.peaklab.cfg"
  printf '[AutoTest]\nEnabled = true\nTestSeed = %s\nAscent = 0\nQuitAfterSeconds = 35\nJumpToSegment = \nSceneOverride = Level_1\nTestBoardingUI = false\n' "$seed" > "$PEAK/BepInEx/config/nicolas.peakautotest.cfg"
  cmd //c start "" //D 'D:\SteamLibrary\steamapps\common\PEAK' 'D:\SteamLibrary\steamapps\common\PEAK\PEAK.exe'
  for i in $(seq 1 40); do
    sleep 3
    grep -q "carregada; fechando" "$PEAK/BepInEx/LogOutput.log" 2>/dev/null && break
  done
  sleep 18
  powershell -ExecutionPolicy Bypass -File tools/screenshot.ps1 -Path "$(cygpath -w "$PWD/$OUT/$name.png")" >> "$OUT/status.txt" 2>&1
  for i in $(seq 1 40); do
    tasklist //FI "IMAGENAME eq PEAK.exe" 2>/dev/null | grep -q PEAK.exe || break
    sleep 3
  done
  taskkill //IM PEAK.exe //F >/dev/null 2>&1
  cp "$PEAK/BepInEx/LogOutput.log" "$OUT/$name.log" 2>/dev/null
  sleep 4
}

run_test controle "" false false
run_test b1-generate 454034 true false
run_test b2-populate 454034 false true

printf '[AutoTest]\nEnabled = false\nTestSeed = \nAscent = 0\nQuitAfterSeconds = 25\nJumpToSegment = \nSceneOverride = \nTestBoardingUI = false\n' > "$PEAK/BepInEx/config/nicolas.peakautotest.cfg"
printf '[Geracao]\nRandomizeBiomeVariants = true\nFullRegenerate = false\nGenerateAposVariantes = false\nPopularVariantesAtivas = false\n\n[Avancado]\nMontanha = Auto\nPraia = Auto\nSelva = Auto\nNeve = Auto\n' > "$PEAK/BepInEx/config/nicolas.peaklab.cfg"
echo "DONE" >> "$OUT/status.txt"

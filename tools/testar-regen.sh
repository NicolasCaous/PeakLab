#!/bin/bash
# Bateria de teste da regeneracao por seed (PeakLab 1.5.0).
# uso: tools/testar-regen.sh <nome> <cena> <seed|""> <segmento-do-pulo|""> [Regen=true] [Pool=Padrao]
# Cada run: embarca solo na cena, aplica a seed, pula para o segmento, loga posicao,
# tira foto de dentro do jogo e fecha. Log vai para testlogs/regen/<nome>.log
cd /c/Users/nicolas/Documents/dump/peak-recon || exit 1
PEAK="/d/SteamLibrary/steamapps/common/PEAK"
OUT="testlogs/regen"
mkdir -p "$OUT"
name="$1"; scene="$2"; seed="$3"; jump="$4"; regen="${5:-true}"; pool="${6:-Padrao}"

printf '[Avancado]\nMontanha = Auto\nPraia = Auto\nSelva = Auto\nNeve = Auto\n\n[Geracao]\nRandomizeBiomeVariants = true\nFullRegenerate = false\nGenerateAposVariantes = false\nPopularVariantesAtivas = false\nPoolDeVariantes = %s\nRegenerarBiomas = %s\nDiagnostico = true\n\n[Skins]\nFitSovietico = true\n' \
  "$pool" "$regen" > "$PEAK/BepInEx/config/nicolas.peaklab.cfg"
QUITV=${QUIT:-45}
printf '[AutoTest]\nEnabled = true\nTestSeed = %s\nAscent = 0\nQuitAfterSeconds = %s\nJumpToSegment = %s\nSceneOverride = %s\nTestBoardingUI = false\nAirportOnly = false\nTestPassport = false\nScreenshotName = %s\n' \
  "$seed" "$QUITV" "$jump" "$scene" "$name" > "$PEAK/BepInEx/config/nicolas.peakautotest.cfg"

: > "$PEAK/BepInEx/LogOutput.log"
cmd //c start "" //D 'D:\SteamLibrary\steamapps\common\PEAK' 'D:\SteamLibrary\steamapps\common\PEAK\PEAK.exe'
sleep 10
for i in $(seq 1 60); do
  tasklist //FI "IMAGENAME eq PEAK.exe" 2>/dev/null | grep -q PEAK.exe || break
  sleep 5
done
if tasklist //FI "IMAGENAME eq PEAK.exe" 2>/dev/null | grep -q PEAK.exe; then
  echo "TIMEOUT em $name; matando o jogo"
  taskkill //IM PEAK.exe //F >/dev/null 2>&1
  sleep 3
fi
cp "$PEAK/BepInEx/LogOutput.log" "$OUT/$name.log"
cp "/c/Users/nicolas/AppData/LocalLow/LandCrab/PEAK/Player.log" "$OUT/$name.player.log" 2>/dev/null
[ -f "$PEAK/BepInEx/recon/$name.png" ] && mv -f "$PEAK/BepInEx/recon/$name.png" "$OUT/$name.png"
echo "== $name ($scene seed=$seed jump=$jump regen=$regen)"
grep -E "\[Regen\]|\[Censo\]|ilha regenerada|AutoTest\] pos|variantes ativas|Exception|Error" "$OUT/$name.log" | head -80

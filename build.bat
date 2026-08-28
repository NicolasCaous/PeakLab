@echo off
rem Compila o PeakRecon.dll e instala em BepInEx\plugins
set "PEAK=D:\SteamLibrary\steamapps\common\PEAK"

"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" -nologo -t:library -langversion:5 ^
  -out:PeakRecon.dll ^
  -r:"%PEAK%\PEAK_Data\Managed\UnityEngine.dll" ^
  -r:"%PEAK%\PEAK_Data\Managed\UnityEngine.CoreModule.dll" ^
  -r:"%PEAK%\PEAK_Data\Managed\netstandard.dll" ^
  -r:"%PEAK%\BepInEx\core\BepInEx.dll" ^
  -r:System.dll -r:System.Core.dll ^
  PeakRecon.cs

if exist PeakRecon.dll (
    copy /Y PeakRecon.dll "%PEAK%\BepInEx\plugins\PeakRecon.dll"
    echo OK: PeakRecon.dll compilado e instalado.
) else (
    echo ERRO: compilacao falhou.
)

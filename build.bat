@echo off
rem Compila PeakRecon.dll + PeakLab.dll (instala em BepInEx\plugins) e tools\DumpApi.exe
set "PEAK=D:\SteamLibrary\steamapps\common\PEAK"
set "M=%PEAK%\PEAK_Data\Managed"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

"%CSC%" -nologo -t:library -langversion:5 ^
  -out:PeakRecon.dll ^
  -r:"%M%\UnityEngine.dll" ^
  -r:"%M%\UnityEngine.CoreModule.dll" ^
  -r:"%M%\netstandard.dll" ^
  -r:"%PEAK%\BepInEx\core\BepInEx.dll" ^
  -r:System.dll -r:System.Core.dll ^
  PeakRecon.cs

"%CSC%" -nologo -t:library -langversion:5 ^
  -out:PeakLab.dll ^
  -r:"%M%\UnityEngine.dll" ^
  -r:"%M%\UnityEngine.CoreModule.dll" ^
  -r:"%M%\UnityEngine.UIModule.dll" ^
  -r:"%M%\UnityEngine.UI.dll" ^
  -r:"%M%\Unity.TextMeshPro.dll" ^
  -r:"%M%\Assembly-CSharp.dll" ^
  -r:"%M%\Zorro.ControllerSupport.dll" ^
  -r:"%M%\Zorro.Core.Runtime.dll" ^
  -r:"%M%\Zorro.UI.Runtime.dll" ^
  -r:"%M%\netstandard.dll" ^
  -r:"%PEAK%\BepInEx\core\BepInEx.dll" ^
  -r:"%PEAK%\BepInEx\core\0Harmony.dll" ^
  -r:System.dll -r:System.Core.dll ^
  PeakLab.cs

"%CSC%" -nologo -out:tools\DumpApi.exe tools\DumpApi.cs

if exist PeakRecon.dll copy /Y PeakRecon.dll "%PEAK%\BepInEx\plugins\PeakRecon.dll"
if exist PeakLab.dll copy /Y PeakLab.dll "%PEAK%\BepInEx\plugins\PeakLab.dll"
echo Feito.

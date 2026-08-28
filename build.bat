@echo off
rem Compila PeakRecon.dll + PeakLab.dll (instala em BepInEx\plugins) e tools\DumpApi.exe
set "PEAK=D:\SteamLibrary\steamapps\common\PEAK"
set "M=%PEAK%\PEAK_Data\Managed"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

"%CSC%" -nologo -t:library -langversion:5 ^
  -out:PeakRecon.dll ^
  -r:"%M%\UnityEngine.dll" ^
  -r:"%M%\UnityEngine.CoreModule.dll" ^
  -r:"%M%\UnityEngine.ImageConversionModule.dll" ^
  -r:"%M%\Assembly-CSharp.dll" ^
  -r:"%M%\Zorro.Core.Runtime.dll" ^
  -r:"%M%\netstandard.dll" ^
  -r:"%PEAK%\BepInEx\core\BepInEx.dll" ^
  -r:System.dll -r:System.Core.dll ^
  PeakRecon.cs

"%CSC%" -nologo -t:library -langversion:5 ^
  -out:PeakLab.dll ^
  -resource:assets\FitSoviet_atlas.png,FitSoviet_atlas.png ^
  -resource:assets\FitSoviet_icon.png,FitSoviet_icon.png ^
  -resource:assets\SovietHelmet_tex.png,SovietHelmet_tex.png ^
  -r:"%M%\UnityEngine.dll" ^
  -r:"%M%\UnityEngine.CoreModule.dll" ^
  -r:"%M%\UnityEngine.JSONSerializeModule.dll" ^
  -r:"%M%\UnityEngine.ImageConversionModule.dll" ^
  -r:"%M%\UnityEngine.UIModule.dll" ^
  -r:"%M%\UnityEngine.UI.dll" ^
  -r:"%M%\Unity.TextMeshPro.dll" ^
  -r:"%M%\PhotonUnityNetworking.dll" ^
  -r:"%M%\PhotonRealtime.dll" ^
  -r:"%M%\Assembly-CSharp.dll" ^
  -r:"%M%\Zorro.ControllerSupport.dll" ^
  -r:"%M%\Zorro.Core.Runtime.dll" ^
  -r:"%M%\Zorro.UI.Runtime.dll" ^
  -r:"%M%\netstandard.dll" ^
  -r:"%PEAK%\BepInEx\core\BepInEx.dll" ^
  -r:"%PEAK%\BepInEx\core\0Harmony.dll" ^
  -r:System.dll -r:System.Core.dll ^
  PeakLab.cs PeakLabHistory.cs PeakLabSkins.cs

"%CSC%" -nologo -t:library -langversion:5 ^
  -out:PeakAutoTest.dll ^
  -r:"%M%\UnityEngine.dll" ^
  -r:"%M%\UnityEngine.CoreModule.dll" ^
  -r:"%M%\UnityEngine.ScreenCaptureModule.dll" ^
  -r:"%M%\UnityEngine.ImageConversionModule.dll" ^
  -r:"%M%\UnityEngine.UIModule.dll" ^
  -r:"%M%\UnityEngine.UI.dll" ^
  -r:"%M%\PhotonUnityNetworking.dll" ^
  -r:"%M%\PhotonRealtime.dll" ^
  -r:"%M%\Assembly-CSharp.dll" ^
  -r:"%M%\Zorro.ControllerSupport.dll" ^
  -r:"%M%\Zorro.Core.Runtime.dll" ^
  -r:"%M%\Zorro.UI.Runtime.dll" ^
  -r:"%M%\netstandard.dll" ^
  -r:"%PEAK%\BepInEx\core\BepInEx.dll" ^
  -r:"%PEAK%\BepInEx\core\0Harmony.dll" ^
  -r:System.dll -r:System.Core.dll ^
  PeakAutoTest.cs

"%CSC%" -nologo -out:tools\DumpApi.exe tools\DumpApi.cs

if exist PeakRecon.dll copy /Y PeakRecon.dll "%PEAK%\BepInEx\plugins\PeakRecon.dll"
if exist PeakLab.dll copy /Y PeakLab.dll "%PEAK%\BepInEx\plugins\PeakLab.dll"
if exist PeakAutoTest.dll copy /Y PeakAutoTest.dll "%PEAK%\BepInEx\plugins\PeakAutoTest.dll"
echo Feito.

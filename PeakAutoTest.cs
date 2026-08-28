// PeakAutoTest 0.1.0 - piloto automatico de teste (ferramenta de desenvolvimento)
// Navega sozinho: Pretitle -> Title -> Play Solo -> Airport -> StartGame -> ilha,
// espera alguns segundos e fecha o jogo. Serve para coletar dumps/logs sem humano.
// DESLIGADO por padrao (config [AutoTest] Enabled). Nao usar em partidas normais.
using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[BepInPlugin("nicolas.peakautotest", "PeakAutoTest", "0.1.0")]
public class PeakAutoTestPlugin : BaseUnityPlugin
{
    private static ManualLogSource Log;
    private ConfigEntry<bool> _enabled;
    private ConfigEntry<string> _testSeed;
    private ConfigEntry<int> _ascent;
    private ConfigEntry<int> _quitAfter;
    private ConfigEntry<string> _jumpTo;
    private ConfigEntry<string> _sceneOverride;
    private ConfigEntry<bool> _testUI;
    private ConfigEntry<bool> _airportOnly;
    private ConfigEntry<bool> _testPassport;
    private ConfigEntry<bool> _unlitDummy;
    private bool _soloClicked;
    private bool _boarded;

    private void Awake()
    {
        Log = Logger;
        _enabled = Config.Bind("AutoTest", "Enabled", false, "Liga o piloto automatico de teste");
        _testSeed = Config.Bind("AutoTest", "TestSeed", "", "Seed a aplicar (vazio = vanilla/daily)");
        _ascent = Config.Bind("AutoTest", "Ascent", 0, "Ascent usado no StartGame");
        _quitAfter = Config.Bind("AutoTest", "QuitAfterSeconds", 25, "Segundos na ilha antes de fechar o jogo (0 = nao fecha)");
        _jumpTo = Config.Bind("AutoTest", "JumpToSegment", "", "Se preenchido (ex: Alpine), chama MapHandler.JumpToSegment aos 15s na ilha");
        _sceneOverride = Config.Bind("AutoTest", "SceneOverride", "", "Se preenchido (ex: Level_3), embarca nessa cena em vez da daily");
        _testUI = Config.Bind("AutoTest", "TestBoardingUI", false, "Abre e fecha a boarding pass antes de embarcar (testa a UI injetada)");
        _airportOnly = Config.Bind("AutoTest", "AirportOnly", false, "Para no Airport, espera QuitAfterSeconds e fecha (nao embarca)");
        _testPassport = Config.Bind("AutoTest", "TestPassport", false, "No Airport: veste o Fit_Soviet + bone, abre o passaporte, tira screenshot e fecha");
        _unlitDummy = Config.Bind("AutoTest", "UnlitDummy", false, "No TestPassport: boneco em shader unlit (leitura de UV, cores puras da textura)");
        if (!_enabled.Value)
        {
            Log.LogInfo("[AutoTest] desligado");
            return;
        }
        Log.LogWarning("[AutoTest] ATIVO - o jogo vai navegar e fechar sozinho!");
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        try
        {
            if (scene.name == "Title" && !_soloClicked) StartCoroutine(ClickSolo());
            else if (scene.name == "Airport" && !_boarded) StartCoroutine(Board());
            else if (scene.name.StartsWith("Level_"))
            {
                StartCoroutine(JumpLater());
                StartCoroutine(QuitAfterDelay(scene.name));
            }
        }
        catch (Exception e)
        {
            Log.LogError("[AutoTest] " + e);
        }
    }

    private IEnumerator ClickSolo()
    {
        Log.LogInfo("[AutoTest] Title carregado; esperando 8s para clicar em Play Solo");
        yield return new WaitForSeconds(8f);
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Type t = AccessTools.TypeByName("MainMenuMainPage");
            UnityEngine.Object page = (t != null) ? UnityEngine.Object.FindObjectOfType(t) : null;
            if (page != null)
            {
                FieldInfo bf = AccessTools.Field(t, "m_playSoloButton");
                Button b = (bf != null) ? bf.GetValue(page) as Button : null;
                if (b != null)
                {
                    Log.LogInfo("[AutoTest] clicando Play Solo");
                    _soloClicked = true;
                    b.onClick.Invoke();
                    yield break;
                }
            }
            Log.LogInfo("[AutoTest] menu ainda nao pronto, tentativa " + (attempt + 1));
            yield return new WaitForSeconds(3f);
        }
        Log.LogError("[AutoTest] nao achei o botao Play Solo");
    }

    private IEnumerator Board()
    {
        if (_testPassport.Value)
        {
            _boarded = true;
            yield return StartCoroutine(PassportTest());
            yield break;
        }
        if (_airportOnly.Value)
        {
            int wait = _quitAfter.Value > 0 ? _quitAfter.Value : 15;
            Log.LogInfo("[AutoTest] modo AirportOnly: esperando " + wait + "s no aeroporto e fechando");
            _boarded = true;
            yield return new WaitForSeconds((float)wait);
            Log.LogInfo("[AutoTest] fim do teste (AirportOnly), fechando o jogo");
            Application.Quit();
            yield break;
        }
        Log.LogInfo("[AutoTest] Airport carregado; esperando 10s para embarcar");
        yield return new WaitForSeconds(10f);
        // injeta a seed de teste direto no PeakLab (mesma via da UI)
        string seedTxt = _testSeed.Value.Trim();
        if (seedTxt.Length > 0)
        {
            int s;
            if (int.TryParse(seedTxt, out s))
            {
                Type lab = AccessTools.TypeByName("PeakLabPlugin");
                FieldInfo pf = (lab != null) ? AccessTools.Field(lab, "PendingSeed") : null;
                if (pf != null)
                {
                    pf.SetValue(null, (int?)s);
                    Log.LogInfo("[AutoTest] PendingSeed de teste = " + s);
                }
                else Log.LogWarning("[AutoTest] PeakLab.PendingSeed nao encontrado");
            }
        }
        for (int attempt = 0; attempt < 5; attempt++)
        {
            AirportCheckInKiosk kiosk = UnityEngine.Object.FindObjectOfType<AirportCheckInKiosk>();
            if (kiosk != null)
            {
                string ov = _sceneOverride.Value.Trim();
                _boarded = true;
                if (_testUI.Value)
                {
                    Log.LogInfo("[AutoTest] abrindo BoardingPass para testar a UI");
                    BoardingPass bp = Resources.FindObjectsOfTypeAll<BoardingPass>()[0];
                    bp.Show();
                    yield return new WaitForSeconds(4f);
                    bp.Hide();
                    yield return new WaitForSeconds(1f);
                }
                if (ov.Length > 0)
                {
                    Log.LogInfo("[AutoTest] chamando kiosk.BeginIslandLoadRPC(" + ov + ", " + _ascent.Value + ")");
                    kiosk.BeginIslandLoadRPC(ov, _ascent.Value);
                }
                else
                {
                    Log.LogInfo("[AutoTest] chamando kiosk.StartGame(" + _ascent.Value + ")");
                    kiosk.StartGame(_ascent.Value);
                }
                yield break;
            }
            Log.LogInfo("[AutoTest] kiosk ainda nao encontrado, tentativa " + (attempt + 1));
            yield return new WaitForSeconds(3f);
        }
        Log.LogError("[AutoTest] nao achei o AirportCheckInKiosk");
    }

    private IEnumerator PassportTest()
    {
        Log.LogInfo("[AutoTest] TestPassport: esperando 12s do aeroporto assentar");
        yield return new WaitForSeconds(12f);
        int idx = -1;
        Customization cat = UnityEngine.Object.FindObjectOfType<Customization>();
        if (cat == null)
        {
            UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(typeof(Customization));
            if (all.Length > 0) cat = (Customization)all[0];
        }
        if (cat != null && cat.fits != null)
        {
            for (int i = 0; i < cat.fits.Length; i++)
            {
                if (cat.fits[i] != null && cat.fits[i].name == "Fit_Soviet") { idx = i; break; }
            }
        }
        if (idx >= 0)
        {
            Log.LogInfo("[AutoTest] Fit_Soviet no indice " + idx + "; vestindo fit + bone (override deve trocar por capacete)");
            CharacterCustomization.SetCharacterOutfit(idx);
            CharacterCustomization.SetCharacterHat(0); // o overrideHat do fit deve vencer
        }
        else Log.LogError("[AutoTest] Fit_Soviet NAO esta no catalogo!");
        yield return new WaitForSeconds(1f);
        PassportManager pm = PassportManager.instance;
        if (pm != null)
        {
            Log.LogInfo("[AutoTest] abrindo passaporte via Show()");
            pm.Show();
            yield return new WaitForSeconds(2f);
            if (!pm.isOpen)
            {
                // Show() so ergue o item na mao; o "open" de verdade e a acao do item
                Action_Passport act = UnityEngine.Object.FindObjectOfType<Action_Passport>();
                if (act != null)
                {
                    Log.LogInfo("[AutoTest] chamando Action_Passport.RunAction()");
                    act.RunAction();
                    yield return new WaitForSeconds(2f);
                }
                else Log.LogWarning("[AutoTest] Action_Passport nao encontrado");
            }
            Log.LogInfo("[AutoTest] passaporte isOpen=" + pm.isOpen);
            try
            {
                // o tipo do enum vem da propria assinatura de OpenTab
                MethodInfo mOpen = AccessTools.Method(typeof(PassportManager), "OpenTab");
                Type et = mOpen.GetParameters()[0].ParameterType;
                object fitTab = Enum.Parse(et, "Fit");
                mOpen.Invoke(pm, new object[] { fitTab });
                Log.LogInfo("[AutoTest] aba de fits aberta (enum " + et.FullName + ")");
            }
            catch (Exception e) { Log.LogWarning("[AutoTest] OpenTab(Fit) falhou: " + e.Message); }
        }
        else Log.LogWarning("[AutoTest] PassportManager.instance nulo");
        yield return new WaitForSeconds(3f);
        // afasta a camera do boneco para enquadrar o corpo inteiro no render
        try
        {
            if (pm != null && pm.dummyCamera != null)
            {
                Transform ct = pm.dummyCamera.transform;
                ct.position = ct.position - ct.forward * 1.6f - Vector3.up * 0.45f;
                pm.dummyCamera.fieldOfView = 55f;
            }
            // shader unlit em tudo do boneco: cores puras da textura, sem luz da cena
            if (pm != null && pm.dummy != null && _unlitDummy.Value)
            {
                Shader unlit = Shader.Find("UI/Default");
                if (unlit == null) unlit = Shader.Find("Unlit/Texture");
                if (unlit != null)
                {
                    Renderer[] all = pm.dummy.GetComponentsInChildren<Renderer>(true);
                    for (int ri = 0; ri < all.Length; ri++)
                    {
                        Material[] ms = all[ri].materials;
                        for (int mi = 0; mi < ms.Length; mi++)
                        {
                            if (ms[mi] == null) continue;
                            Texture keep = ms[mi].mainTexture;
                            ms[mi].shader = unlit;
                            ms[mi].mainTexture = keep;
                        }
                    }
                    Log.LogInfo("[AutoTest] boneco em modo unlit para leitura de UV");
                }
                else Log.LogWarning("[AutoTest] nenhum shader unlit disponivel");
            }
        }
        catch (Exception e) { Log.LogWarning("[AutoTest] ajuste de camera/shader falhou: " + e.Message); }
        yield return new WaitForSeconds(1f);
        // prova extra: salva o render do boneco de preview direto da camera dele
        try
        {
            if (pm != null && pm.dummyCamera != null && pm.dummyCamera.targetTexture != null)
            {
                string rtFile = System.IO.Path.Combine(BepInEx.Paths.GameRootPath,
                    System.IO.Path.Combine("BepInEx", System.IO.Path.Combine("recon", "dummy_rt.png")));
                SaveRT(pm.dummyCamera.targetTexture, rtFile);
                Log.LogInfo("[AutoTest] render do boneco salvo em " + rtFile);
            }
            else Log.LogInfo("[AutoTest] dummyCamera sem targetTexture; pulando dump do boneco");
        }
        catch (Exception e) { Log.LogWarning("[AutoTest] dump do boneco falhou: " + e.Message); }
        string shot = System.IO.Path.Combine(BepInEx.Paths.GameRootPath,
            System.IO.Path.Combine("BepInEx", System.IO.Path.Combine("recon", "passport_test.png")));
        Log.LogInfo("[AutoTest] screenshot -> " + shot);
        ScreenCapture.CaptureScreenshot(shot);
        yield return new WaitForSeconds(2f);
        Log.LogInfo("[AutoTest] fim do teste (TestPassport), fechando o jogo");
        Application.Quit();
    }

    private static void SaveRT(RenderTexture rt, string file)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D t2 = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        t2.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        t2.Apply();
        RenderTexture.active = prev;
        System.IO.File.WriteAllBytes(file, ImageConversion.EncodeToPNG(t2));
        UnityEngine.Object.Destroy(t2);
    }

    private IEnumerator JumpLater()
    {
        string tgt = _jumpTo.Value.Trim();
        if (tgt.Length == 0) yield break;
        yield return new WaitForSeconds(15f);
        try
        {
            Type segT = AccessTools.TypeByName("Segment");
            object val = Enum.Parse(segT, tgt);
            Type mh = AccessTools.TypeByName("MapHandler");
            MethodInfo m = AccessTools.Method(mh, "JumpToSegment");
            Log.LogInfo("[AutoTest] chamando MapHandler.JumpToSegment(" + tgt + ")");
            m.Invoke(null, new object[] { val });
        }
        catch (Exception e)
        {
            Log.LogError("[AutoTest] JumpToSegment falhou: " + e);
        }
    }

    private IEnumerator QuitAfterDelay(string sceneName)
    {
        if (_quitAfter.Value <= 0) yield break;
        Log.LogInfo("[AutoTest] ilha " + sceneName + " carregada; fechando em " + _quitAfter.Value + "s");
        yield return new WaitForSeconds((float)_quitAfter.Value);
        Log.LogInfo("[AutoTest] fim do teste, fechando o jogo");
        Application.Quit();
    }
}

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

// PeakAutoTest 0.1.0 - piloto automatico de teste (ferramenta de desenvolvimento)
// Navega sozinho: Pretitle -> Title -> Play Solo -> Airport -> StartGame -> ilha,
// espera alguns segundos e fecha o jogo. Serve para coletar dumps/logs sem humano.
// DESLIGADO por padrao (config [AutoTest] Enabled). Nao usar em partidas normais.
using System;
using System.Collections;
using System.Reflection;
using System.Text;
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
    private ConfigEntry<string> _testFitName;
    private ConfigEntry<bool> _unlitDummy;
    private ConfigEntry<bool> _unlitChar;
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
        _testFitName = Config.Bind("AutoTest", "TestFitName", "Fit_Soviet", "Nome do fit a vestir no TestPassport");
        _unlitDummy = Config.Bind("AutoTest", "UnlitDummy", false, "No TestPassport: boneco em shader unlit (leitura de UV, cores puras da textura)");
        _unlitChar = Config.Bind("AutoTest", "UnlitCharacter", false, "No TestPassport: personagem REAL em shader unlit antes das selfies (leitura de UV)");
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
                if (cat.fits[i] != null && cat.fits[i].name == _testFitName.Value) { idx = i; break; }
            }
        }
        if (idx >= 0)
        {
            Log.LogInfo("[AutoTest] " + _testFitName.Value + " no indice " + idx + "; vestindo fit + bone (override deve trocar por capacete)");
            CharacterCustomization.SetCharacterOutfit(idx);
            CharacterCustomization.SetCharacterHat(0); // o overrideHat do fit deve vencer
        }
        else Log.LogError("[AutoTest] " + _testFitName.Value + " NAO esta no catalogo!");
        yield return new WaitForSeconds(1f);
        DumpCharacterRenderers("apos vestir");
        DumpMeshUvMasks();
        if (_unlitChar.Value) UnlitAllUnder(FindCharacterRoot(), "personagem");
        yield return StartCoroutine(CharacterSelfie("char_selfie_frente.png", false));
        yield return StartCoroutine(CharacterSelfie("char_selfie_costas.png", true));
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
        yield return new WaitForSeconds(1f);
        DumpCharacterRenderers("com passaporte aberto");
        yield return new WaitForSeconds(2f);
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

    private static void DumpCharacterRenderers(string tag)
    {
        try
        {
            CustomizationRefs[] refsAll = UnityEngine.Object.FindObjectsOfType<CustomizationRefs>();
            Log.LogInfo("[AutoTest] === renderers (" + tag + "): " + refsAll.Length + " CustomizationRefs ativos");
            for (int i = 0; i < refsAll.Length; i++)
            {
                Transform root = refsAll[i].transform;
                Log.LogInfo("[AutoTest] --- refs #" + i + " raiz=" + root.root.name + " no=" + root.name);
                Renderer[] rr = root.GetComponentsInChildren<Renderer>(false);
                for (int r = 0; r < rr.Length; r++)
                {
                    if (rr[r] == null || !rr[r].enabled) continue;
                    Material[] mats = rr[r].sharedMaterials;
                    StringBuilder line = new StringBuilder();
                    line.Append("[AutoTest]     " + rr[r].name + " |");
                    for (int mi = 0; mi < mats.Length; mi++)
                    {
                        Material m = mats[mi];
                        string tex = "-";
                        try { if (m != null && m.mainTexture != null) tex = m.mainTexture.name; }
                        catch (Exception) { }
                        line.Append(" [" + mi + "] " + (m != null ? m.name : "null") + " (" + tex + ")");
                    }
                    Log.LogInfo(line.ToString());
                }
            }
        }
        catch (Exception e) { Log.LogWarning("[AutoTest] dump renderers falhou: " + e.Message); }
    }

    // fotografa o personagem REAL com uma camera propria (o que o jogador ve)
    private static Transform FindCharacterRoot()
    {
        CustomizationRefs[] all = UnityEngine.Object.FindObjectsOfType<CustomizationRefs>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].transform.root.name.StartsWith("Character"))
                return all[i].transform.root;
        }
        return null;
    }

    private static void UnlitAllUnder(Transform root, string tag)
    {
        try
        {
            if (root == null) return;
            Shader unlit = Shader.Find("UI/Default");
            if (unlit == null) unlit = Shader.Find("Unlit/Texture");
            if (unlit == null) { Log.LogWarning("[AutoTest] sem shader unlit"); return; }
            Renderer[] rr = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rr.Length; i++)
            {
                Material[] ms = rr[i].materials;
                for (int mi = 0; mi < ms.Length; mi++)
                {
                    if (ms[mi] == null) continue;
                    Texture keep = ms[mi].mainTexture;
                    ms[mi].shader = unlit;
                    ms[mi].mainTexture = keep;
                }
            }
            Log.LogInfo("[AutoTest] " + tag + " em modo unlit (" + rr.Length + " renderers)");
        }
        catch (Exception e) { Log.LogWarning("[AutoTest] unlit falhou: " + e.Message); }
    }

    private IEnumerator CharacterSelfie(string fileName, bool costas)
    {
        GameObject camGo = null;
        CustomizationRefs target = null;
        CustomizationRefs[] all = UnityEngine.Object.FindObjectsOfType<CustomizationRefs>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].transform.root.name.StartsWith("Character"))
            { target = all[i]; break; }
        }
        if (target == null)
        {
            Log.LogWarning("[AutoTest] selfie: personagem nao encontrado");
            yield break;
        }
        try
        {
            Transform root = target.transform.root;
            camGo = new GameObject("PeakAutoTest_SelfieCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.depth = 99f;
            cam.fieldOfView = 45f;
            Vector3 center = root.position + Vector3.up * 0.62f;
            Vector3 dir = costas ? -root.forward : root.forward;
            camGo.transform.position = center + dir * 3.4f + Vector3.up * 0.1f;
            camGo.transform.LookAt(center);
        }
        catch (Exception e)
        {
            Log.LogWarning("[AutoTest] selfie falhou: " + e.Message);
            if (camGo != null) UnityEngine.Object.Destroy(camGo);
            yield break;
        }
        yield return new WaitForSeconds(0.5f);
        string shot = System.IO.Path.Combine(BepInEx.Paths.GameRootPath,
            System.IO.Path.Combine("BepInEx", System.IO.Path.Combine("recon", fileName)));
        ScreenCapture.CaptureScreenshot(shot);
        Log.LogInfo("[AutoTest] selfie do personagem -> " + shot);
        yield return new WaitForSeconds(1f);
        UnityEngine.Object.Destroy(camGo);
    }

    // le direto do mesh: que regiao do atlas cada faixa de altura do corpo usa.
    // Gera recon\uvmask_<quem>_sm<slot>.png (1024, fundo preto):
    //   azul=pes, magenta=canela/meiao, amarelo=quadril, verde=torso, vermelho=gola/ombro
    private static void DumpMeshUvMasks()
    {
        try
        {
            CustomizationRefs[] all = Resources.FindObjectsOfTypeAll<CustomizationRefs>();
            for (int i = 0; i < all.Length; i++)
            {
                CustomizationRefs r = all[i];
                if (r == null || r.mainRenderer == null || r.mainRenderer.sharedMesh == null) continue;
                string who = r.transform.root.name.StartsWith("Character") ? "character"
                           : (r.gameObject.scene.IsValid() ? "cena_" + r.transform.root.name : "prefab");
                Mesh mesh = r.mainRenderer.sharedMesh;
                Log.LogInfo("[AutoTest] uvmask " + who + ": mesh=" + mesh.name +
                            " id=" + mesh.GetInstanceID() + " verts=" + mesh.vertexCount +
                            " submeshes=" + mesh.subMeshCount);
                Vector3[] verts = mesh.vertices;
                Vector2[] uv = mesh.uv;
                float ymin = float.MaxValue, ymax = float.MinValue;
                for (int v = 0; v < verts.Length; v++)
                {
                    if (verts[v].y < ymin) ymin = verts[v].y;
                    if (verts[v].y > ymax) ymax = verts[v].y;
                }
                float span = Mathf.Max(0.0001f, ymax - ymin);
                for (int sm = 0; sm < mesh.subMeshCount; sm++)
                {
                    int[] tris = mesh.GetTriangles(sm);
                    Texture2D mask = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
                    Color32[] px = new Color32[1024 * 1024];
                    for (int p = 0; p < px.Length; p++) px[p] = new Color32(0, 0, 0, 255);
                    for (int t = 0; t < tris.Length; t++)
                    {
                        int vi = tris[t];
                        if (vi >= verts.Length || vi >= uv.Length) continue;
                        float ny = (verts[vi].y - ymin) / span;
                        Color32 c;
                        if (ny < 0.08f) c = new Color32(60, 120, 255, 255);       // pes
                        else if (ny < 0.30f) c = new Color32(255, 0, 255, 255);   // canela/meiao
                        else if (ny < 0.52f) c = new Color32(255, 230, 40, 255);  // quadril
                        else if (ny < 0.80f) c = new Color32(40, 220, 60, 255);   // torso
                        else c = new Color32(255, 40, 40, 255);                   // gola/ombro
                        int ux = Mathf.Clamp((int)(uv[vi].x * 1023f), 0, 1023);
                        int uy = Mathf.Clamp((int)((1f - uv[vi].y) * 1023f), 0, 1023);
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int qx = ux + dx, qy = uy + dy;
                                if (qx < 0 || qy < 0 || qx > 1023 || qy > 1023) continue;
                                px[(1023 - qy) * 1024 + qx] = c;
                            }
                        }
                    }
                    mask.SetPixels32(px);
                    mask.Apply();
                    string f = System.IO.Path.Combine(BepInEx.Paths.GameRootPath,
                        System.IO.Path.Combine("BepInEx", System.IO.Path.Combine("recon",
                        "uvmask_" + who + "_m" + mesh.GetInstanceID() + "_sm" + sm + ".png")));
                    System.IO.File.WriteAllBytes(f, ImageConversion.EncodeToPNG(mask));
                    UnityEngine.Object.Destroy(mask);
                }
            }
        }
        catch (Exception e) { Log.LogWarning("[AutoTest] uvmask falhou: " + e); }
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

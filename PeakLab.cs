// PeakLab 0.1.0 - fase 2 (experimento) do gerador de mapas para PEAK v1.35.a
// - Adiciona campo de SEED + botao de aleatorio na boarding pass do aeroporto
// - Ao carregar a ilha com seed definida: re-sorteia variantes de bioma
//   (e opcionalmente Clear()+Generate(), via config)
// - Espioes Harmony logam quem chama a pipeline de geracao (baked vs runtime)
// AVISO: v0.1 e' para jogo SOLO. Em multiplayer os outros jogadores nao
// recebem a seed ainda (sync vem na v0.2).
using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[BepInPlugin("nicolas.peaklab", "PeakLab", "0.1.0")]
public class PeakLabPlugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static ConfigEntry<bool> CfgVariants;
    internal static ConfigEntry<bool> CfgFullRegen;
    internal static int? PendingSeed;
    private static TMP_InputField _input;

    private void Awake()
    {
        Log = Logger;
        CfgVariants = Config.Bind("Geracao", "RandomizeBiomeVariants", true,
            "Com seed definida, re-sorteia as variantes de bioma da ilha ao carregar");
        CfgFullRegen = Config.Bind("Geracao", "FullRegenerate", false,
            "EXPERIMENTAL: chama LevelGeneration.Clear()+Generate() com a seed (pode quebrar a ilha)");
        try
        {
            Harmony h = new Harmony("nicolas.peaklab");
            SpyPatch(h, "LevelGeneration", "Generate");
            SpyPatch(h, "LevelGeneration", "Clear");
            SpyPatch(h, "LevelGeneration", "RandomizeBiomeVariants");
            SpyPatch(h, "MapGenerator", "GenerateAll");
            SpyPatch(h, "VariantObjectSelector", "SelectVariations");
            SpyPatch(h, "WallPieceSpawner", "Go");
            SpyPatch(h, "MapHandler", "DetectBiomes");
            SpyPatch(h, "BiomeSelector", "Select");
            SpyPatch(h, "TodaysBiomes", "SetBiomes");
            h.Patch(AccessTools.Method(typeof(BoardingPass), "OnOpen"), null,
                new HarmonyMethod(typeof(PeakLabPlugin).GetMethod("BoardingPassOpened",
                    BindingFlags.Static | BindingFlags.NonPublic)));
            h.Patch(AccessTools.Method(typeof(BoardingPass), "StartGame"),
                new HarmonyMethod(typeof(PeakLabPlugin).GetMethod("StartGamePrefix",
                    BindingFlags.Static | BindingFlags.NonPublic)), null);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Log.LogInfo("PeakLab pronto (UI da boarding pass + espioes de geracao)");
        }
        catch (Exception e)
        {
            Log.LogError("PeakLab Awake falhou: " + e);
        }
    }

    // ---------- espioes ----------

    private static void SpyPatch(Harmony h, string typeName, string method)
    {
        try
        {
            Type t = AccessTools.TypeByName(typeName);
            MethodInfo m = (t == null) ? null : AccessTools.Method(t, method);
            if (m == null)
            {
                Log.LogWarning("[SPY] alvo ausente: " + typeName + "." + method);
                return;
            }
            h.Patch(m, new HarmonyMethod(typeof(PeakLabPlugin).GetMethod("SpyPrefix",
                BindingFlags.Static | BindingFlags.NonPublic)), null);
        }
        catch (Exception e)
        {
            Log.LogWarning("[SPY] patch falhou " + typeName + "." + method + ": " + e.Message);
        }
    }

    private static void SpyPrefix(MethodBase __originalMethod)
    {
        Log.LogInfo("[SPY] " + __originalMethod.DeclaringType.Name + "." + __originalMethod.Name +
                    " chamado\n" + ShortStack());
    }

    private static string ShortStack()
    {
        string[] lines = Environment.StackTrace.Split('\n');
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int printed = 0;
        for (int i = 0; i < lines.Length && printed < 8; i++)
        {
            string l = lines[i].Trim();
            if (l.Length == 0) continue;
            if (l.Contains("PeakLabPlugin") || l.Contains("Environment.get_StackTrace") ||
                l.Contains("HarmonyLib.")) continue;
            sb.AppendLine("    " + l);
            printed++;
        }
        return sb.ToString();
    }

    // ---------- UI na boarding pass ----------

    private static void BoardingPassOpened(BoardingPass __instance)
    {
        try
        {
            if (_input == null || !_input) BuildUI(__instance);
        }
        catch (Exception e)
        {
            Log.LogError("[UI] injecao falhou: " + e);
        }
    }

    private static void BuildUI(BoardingPass bp)
    {
        Transform ascentBox = bp.ascentDesc.transform.parent; // o quadro branco "Ascent"
        RectTransform boxR = ascentBox as RectTransform;
        RectTransform descR = bp.ascentDesc.rectTransform;
        Log.LogInfo("[UI] Ascent box rect=" + boxR.rect + " desc pos=" + descR.anchoredPosition +
                    " desc rect=" + descR.rect);

        // rotulo "SEED:" no estilo manuscrito azul do titulo
        GameObject labelGO = UnityEngine.Object.Instantiate(bp.ascentTitle.gameObject, ascentBox);
        labelGO.name = "PeakLab_SeedLabel";
        TMP_Text label = labelGO.GetComponent<TMP_Text>();
        label.text = "SEED:";
        label.enableAutoSizing = false;
        label.fontSize = bp.ascentDesc.fontSize + 4f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform lr = (RectTransform)labelGO.transform;
        SetBottomLeft(lr, new Vector2(24f, 14f), new Vector2(120f, 48f));

        // campo de digitar a seed
        TMP_DefaultControls.Resources res = new TMP_DefaultControls.Resources();
        GameObject inputGO = TMP_DefaultControls.CreateInputField(res);
        inputGO.name = "PeakLab_SeedInput";
        inputGO.transform.SetParent(ascentBox, false);
        _input = inputGO.GetComponent<TMP_InputField>();
        Image bg = inputGO.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.07f);
        RectTransform ir = (RectTransform)inputGO.transform;
        SetBottomLeft(ir, new Vector2(150f, 14f), new Vector2(250f, 48f));
        _input.contentType = TMP_InputField.ContentType.IntegerNumber;
        _input.characterLimit = 9;
        TMP_Text txt = _input.textComponent;
        txt.font = bp.ascentTitle.font;
        txt.color = bp.ascentTitle.color;
        txt.enableAutoSizing = false;
        txt.fontSize = bp.ascentDesc.fontSize + 6f;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        TMP_Text pht = _input.placeholder as TMP_Text;
        if (pht != null)
        {
            pht.text = "daily";
            pht.font = bp.ascentDesc.font;
            pht.enableAutoSizing = false;
            pht.fontSize = bp.ascentDesc.fontSize;
            pht.color = new Color(0f, 0f, 0f, 0.35f);
            pht.alignment = TextAlignmentOptions.MidlineLeft;
        }

        // botao de seed aleatoria (clone da setinha, com "?" em cima)
        Button dice = UnityEngine.Object.Instantiate(bp.incrementAscentButton, ascentBox);
        dice.name = "PeakLab_DiceButton";
        dice.onClick = new Button.ButtonClickedEvent();
        dice.onClick.AddListener(new UnityAction(RandomSeedClicked));
        RectTransform dr = (RectTransform)dice.transform;
        Vector2 keep = dr.sizeDelta;
        SetBottomLeft(dr, new Vector2(414f, 14f), keep == Vector2.zero ? new Vector2(56f, 48f) : keep);
        Transform icon = dice.transform.Find("Image");
        if (icon != null) icon.gameObject.SetActive(false);
        GameObject qGO = UnityEngine.Object.Instantiate(bp.ascentTitle.gameObject, dice.transform);
        qGO.name = "PeakLab_DiceText";
        TMP_Text q = qGO.GetComponent<TMP_Text>();
        q.text = "?";
        q.color = Color.white;
        q.enableAutoSizing = false;
        q.fontSize = bp.ascentDesc.fontSize + 10f;
        q.alignment = TextAlignmentOptions.Center;
        RectTransform qr = (RectTransform)qGO.transform;
        qr.anchorMin = Vector2.zero;
        qr.anchorMax = Vector2.one;
        qr.pivot = new Vector2(0.5f, 0.5f);
        qr.offsetMin = Vector2.zero;
        qr.offsetMax = Vector2.zero;

        Log.LogInfo("[UI] campo de seed injetado na boarding pass");
    }

    private static void SetBottomLeft(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.zero;
        r.pivot = Vector2.zero;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    private static void RandomSeedClicked()
    {
        int s = UnityEngine.Random.Range(1, 1000000);
        if (_input != null && _input) _input.text = s.ToString();
        Log.LogInfo("[PeakLab] seed aleatoria sorteada: " + s);
    }

    private static void StartGamePrefix(BoardingPass __instance)
    {
        PendingSeed = null;
        try
        {
            if (_input != null && _input && !string.IsNullOrEmpty(_input.text))
            {
                int s;
                if (int.TryParse(_input.text, out s)) PendingSeed = s;
            }
        }
        catch (Exception e)
        {
            Log.LogWarning("[PeakLab] leitura da seed falhou: " + e.Message);
        }
        Log.LogInfo("[PeakLab] START apertado; ascent=" + __instance.ascentIndex + " seed=" +
                    (PendingSeed.HasValue ? PendingSeed.Value.ToString() : "(vazia -> daily vanilla)"));
    }

    // ---------- aplicacao da seed na ilha ----------

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.StartsWith("Level_")) return;
        try
        {
            ApplySeed(scene);
        }
        catch (Exception e)
        {
            Log.LogError("[PeakLab] ApplySeed falhou: " + e);
        }
    }

    private void ApplySeed(Scene scene)
    {
        if (!PendingSeed.HasValue)
        {
            Log.LogInfo("[PeakLab] " + scene.name + " carregada em modo daily vanilla (sem seed)");
            LogBiomes();
            return;
        }
        int seed = PendingSeed.Value;
        Log.LogInfo("[PeakLab] aplicando seed " + seed + " em " + scene.name);
        UnityEngine.Random.InitState(seed);

        Type lgType = AccessTools.TypeByName("LevelGeneration");
        if (lgType == null)
        {
            Log.LogWarning("[PeakLab] tipo LevelGeneration nao encontrado");
            return;
        }
        UnityEngine.Object[] gens = Resources.FindObjectsOfTypeAll(lgType);
        Component gen = null;
        foreach (UnityEngine.Object o in gens)
        {
            Component c = o as Component;
            if (c != null && c.gameObject.scene == scene) { gen = c; break; }
        }
        if (gen == null)
        {
            Log.LogWarning("[PeakLab] nenhuma LevelGeneration na cena " + scene.name +
                           " (instancias totais: " + gens.Length + ")");
            return;
        }

        FieldInfo seedField = AccessTools.Field(lgType, "seed");
        if (seedField != null)
        {
            seedField.SetValue(gen, seed);
            Log.LogInfo("[PeakLab] LevelGeneration.seed = " + seed);
        }

        if (CfgVariants.Value) InvokeOn(gen, lgType, "RandomizeBiomeVariants");
        if (CfgFullRegen.Value)
        {
            InvokeOn(gen, lgType, "Clear");
            InvokeOn(gen, lgType, "Generate");
        }
        LogBiomes();
    }

    private static void InvokeOn(object target, Type t, string method)
    {
        try
        {
            MethodInfo m = AccessTools.Method(t, method);
            if (m == null) { Log.LogWarning("[PeakLab] metodo ausente: " + method); return; }
            m.Invoke(target, null);
            Log.LogInfo("[PeakLab] " + t.Name + "." + method + "() OK");
        }
        catch (Exception e)
        {
            Log.LogError("[PeakLab] " + t.Name + "." + method + "() lancou: " + e);
        }
    }

    private static void LogBiomes()
    {
        try
        {
            Type mh = AccessTools.TypeByName("MapHandler");
            if (mh == null) return;
            PropertyInfo inst = mh.GetProperty("Instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            object handler = (inst != null) ? inst.GetValue(null, null) : null;
            if (handler == null) { Log.LogInfo("[PeakLab] MapHandler.Instance ainda nulo"); return; }
            FieldInfo bf = AccessTools.Field(mh, "biomes");
            object biomes = (bf != null) ? bf.GetValue(handler) : null;
            System.Collections.IEnumerable en = biomes as System.Collections.IEnumerable;
            if (en == null) { Log.LogInfo("[PeakLab] lista de biomas indisponivel"); return; }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (object b in en)
            {
                if (sb.Length > 0) sb.Append(" -> ");
                sb.Append(b);
            }
            Log.LogInfo("[PeakLab] lineup de biomas: " + sb);
        }
        catch (Exception e)
        {
            Log.LogWarning("[PeakLab] LogBiomes: " + e.Message);
        }
    }
}

// PeakLab 1.0.0 - gerador/remixador de mapas para PEAK v1.35.a
// UI na boarding pass do aeroporto:
//   SIMPLES:  campo SEED + botao ? (aleatoria). Vazio = daily vanilla.
//   AVANCADO: seletores MONTANHA (Alpine/Mesa), PRAIA, SELVA, NEVE.
// Motor: com seed, re-sorteia variantes de bioma (deterministico);
// pins do avancado forcam escolhas especificas; troca Alpine<->Mesa e
// feita trocando MapHandler.segments[slot] <-> variantSegments[0].
// v1.0 e para jogo SOLO (sync multiplayer planejado).
using System;
using System.Collections.Generic;
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

[BepInPlugin("nicolas.peaklab", "PeakLab", "1.0.1")]
public class PeakLabPlugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static ConfigEntry<bool> CfgVariants;
    internal static ConfigEntry<bool> CfgFullRegen;
    internal static int? PendingSeed;

    // catalogo colhido das 6 cenas (identico em todas)
    private static readonly string[] MountainOpts = { "Auto", "Alpine", "Mesa" };
    private static readonly string[] BeachOpts = { "Auto", "Default", "SnakeBeach", "BlackSand", "BlueBeach", "RedBeach", "JellyHell" };
    private static readonly string[] JungleOpts = { "Auto", "Default", "Thorny", "SkyJungle", "Pillars", "Ivy", "Lava", "Bombs" };
    private static readonly string[] SnowOpts = { "Auto", "Default", "Lava", "Spiky", "GeyserHell" };

    private static ConfigEntry<string> CfgMountain;
    private static ConfigEntry<string> CfgBeach;
    private static ConfigEntry<string> CfgJungle;
    private static ConfigEntry<string> CfgSnow;

    private static TMP_InputField _input;
    private static readonly List<GameObject> _advRows = new List<GameObject>();
    private static bool _advOpen;

    private void Awake()
    {
        Log = Logger;
        CfgVariants = Config.Bind("Geracao", "RandomizeBiomeVariants", true,
            "Com seed definida, re-sorteia as variantes de bioma da ilha");
        CfgFullRegen = Config.Bind("Geracao", "FullRegenerate", false,
            "NAO USAR: Clear() remove conteudo (ex: paredes) que Generate() nao reconstroi - " +
            "a ilha pode nascer vazia no oceano. Mantido so para pesquisa.");
        CfgMountain = Config.Bind("Avancado", "Montanha", "Auto",
            "Auto, Alpine ou Mesa. BETA: a troca manual esta em investigacao - " +
            "se o jogador nascer na agua, volte para Auto");
        CfgBeach = Config.Bind("Avancado", "Praia", "Auto", "Auto ou variante fixa da praia");
        CfgJungle = Config.Bind("Avancado", "Selva", "Auto", "Auto ou variante fixa da selva");
        CfgSnow = Config.Bind("Avancado", "Neve", "Auto", "Auto ou variante fixa da neve (so vale com Alpine)");
        try
        {
            Harmony h = new Harmony("nicolas.peaklab");
            h.Patch(AccessTools.Method(typeof(BoardingPass), "OnOpen"), null,
                new HarmonyMethod(typeof(PeakLabPlugin).GetMethod("BoardingPassOpened",
                    BindingFlags.Static | BindingFlags.NonPublic)));
            h.Patch(AccessTools.Method(typeof(BoardingPass), "UpdateAscent"), null,
                new HarmonyMethod(typeof(PeakLabPlugin).GetMethod("BoardingPassOpened",
                    BindingFlags.Static | BindingFlags.NonPublic)));
            h.Patch(AccessTools.Method(typeof(MenuWindow), "Show"), null,
                new HarmonyMethod(typeof(PeakLabPlugin).GetMethod("MenuShowPostfix",
                    BindingFlags.Static | BindingFlags.NonPublic)));
            h.Patch(AccessTools.Method(typeof(BoardingPass), "StartGame"),
                new HarmonyMethod(typeof(PeakLabPlugin).GetMethod("StartGamePrefix",
                    BindingFlags.Static | BindingFlags.NonPublic)), null);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Log.LogInfo("PeakLab 1.0.1 pronto");
        }
        catch (Exception e)
        {
            Log.LogError("PeakLab Awake falhou: " + e);
        }
    }

    // ==================== UI ====================

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

    private static void MenuShowPostfix(MenuWindow __instance)
    {
        BoardingPass bp = __instance as BoardingPass;
        if (bp != null) BoardingPassOpened(bp);
    }

    private static void BuildUI(BoardingPass bp)
    {
        _advRows.Clear();
        Transform box = bp.ascentDesc.transform.parent;
        RectTransform boxR = box as RectTransform;
        float W = boxR.rect.width;
        float H = boxR.rect.height;
        float fs = bp.ascentDesc.fontSize;
        Log.LogInfo("[UI] Ascent box " + W.ToString("F0") + "x" + H.ToString("F0") + " fontDesc=" + fs);

        // ---- linha da seed (sempre visivel) ----
        TMP_Text label = CloneText(bp, box, "PeakLab_SeedLabel", "SEED:", fs + 4f);
        Place(label.rectTransform, W * 0.03f, H * 0.05f, W * 0.15f, H * 0.17f);

        TMP_DefaultControls.Resources res = new TMP_DefaultControls.Resources();
        GameObject inputGO = TMP_DefaultControls.CreateInputField(res);
        inputGO.name = "PeakLab_SeedInput";
        inputGO.transform.SetParent(box, false);
        _input = inputGO.GetComponent<TMP_InputField>();
        inputGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.07f);
        Place((RectTransform)inputGO.transform, W * 0.19f, H * 0.05f, W * 0.30f, H * 0.17f);
        _input.contentType = TMP_InputField.ContentType.IntegerNumber;
        _input.characterLimit = 9;
        TMP_Text itxt = _input.textComponent;
        itxt.font = bp.ascentTitle.font;
        itxt.color = bp.ascentTitle.color;
        itxt.enableAutoSizing = false;
        itxt.fontSize = fs + 6f;
        itxt.alignment = TextAlignmentOptions.MidlineLeft;
        TMP_Text pht = _input.placeholder as TMP_Text;
        if (pht != null)
        {
            pht.text = "daily";
            pht.font = bp.ascentDesc.font;
            pht.enableAutoSizing = false;
            pht.fontSize = fs;
            pht.color = new Color(0f, 0f, 0f, 0.35f);
            pht.alignment = TextAlignmentOptions.MidlineLeft;
        }

        Button dice = CloneButton(bp, box, "PeakLab_Dice", "?", fs + 8f);
        Place((RectTransform)dice.transform, W * 0.51f, H * 0.05f, H * 0.17f, H * 0.17f);
        dice.onClick.AddListener(new UnityAction(RandomSeedClicked));

        Button adv = CloneButton(bp, box, "PeakLab_AdvToggle", "AVANCADO", fs - 2f);
        Place((RectTransform)adv.transform, W * 0.60f, H * 0.05f, W * 0.37f, H * 0.17f);
        adv.onClick.AddListener(new UnityAction(ToggleAdvanced));

        // ---- linhas do modo avancado ----
        MakeCycler(bp, box, "MONTANHA", MountainOpts, CfgMountain,
            W * 0.03f, H * 0.25f, W * 0.46f, H * 0.17f, fs);
        MakeCycler(bp, box, "PRAIA", BeachOpts, CfgBeach,
            W * 0.51f, H * 0.25f, W * 0.46f, H * 0.17f, fs);
        MakeCycler(bp, box, "SELVA", JungleOpts, CfgJungle,
            W * 0.03f, H * 0.44f, W * 0.46f, H * 0.17f, fs);
        MakeCycler(bp, box, "NEVE", SnowOpts, CfgSnow,
            W * 0.51f, H * 0.44f, W * 0.46f, H * 0.17f, fs);
        SetAdvancedVisible(false);
        Log.LogInfo("[UI] boarding pass pronta (simples + avancado)");
    }

    private static TMP_Text CloneText(BoardingPass bp, Transform parent, string name, string text, float size)
    {
        GameObject go = UnityEngine.Object.Instantiate(bp.ascentTitle.gameObject, parent);
        go.name = name;
        TMP_Text t = go.GetComponent<TMP_Text>();
        t.text = text;
        t.enableAutoSizing = false;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        return t;
    }

    private static Button CloneButton(BoardingPass bp, Transform parent, string name, string labelText, float size)
    {
        Button b = UnityEngine.Object.Instantiate(bp.incrementAscentButton, parent);
        b.name = name;
        b.onClick = new Button.ButtonClickedEvent();
        Transform icon = b.transform.Find("Image");
        if (icon != null) icon.gameObject.SetActive(false);
        GameObject tGO = UnityEngine.Object.Instantiate(bp.ascentTitle.gameObject, b.transform);
        tGO.name = "Label";
        TMP_Text t = tGO.GetComponent<TMP_Text>();
        t.text = labelText;
        t.color = Color.white;
        t.enableAutoSizing = false;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        RectTransform tr = (RectTransform)tGO.transform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        return b;
    }

    private static void MakeCycler(BoardingPass bp, Transform parent, string areaLabel,
        string[] options, ConfigEntry<string> cfg, float x, float y, float w, float h, float fs)
    {
        Button b = CloneButton(bp, parent, "PeakLab_Cyc_" + areaLabel, "", fs - 2f);
        Place((RectTransform)b.transform, x, y, w, h);
        TMP_Text label = b.transform.Find("Label").GetComponent<TMP_Text>();
        UpdateCyclerLabel(label, areaLabel, cfg.Value);
        CyclerState st = new CyclerState();
        st.options = options;
        st.cfg = cfg;
        st.label = label;
        st.area = areaLabel;
        b.onClick.AddListener(new UnityAction(st.Next));
        _advRows.Add(b.gameObject);
    }

    private class CyclerState
    {
        public string[] options;
        public ConfigEntry<string> cfg;
        public TMP_Text label;
        public string area;

        public void Next()
        {
            int idx = Array.IndexOf(options, cfg.Value);
            idx = (idx + 1) % options.Length;
            if (idx < 0) idx = 0;
            cfg.Value = options[idx];
            UpdateCyclerLabel(label, area, cfg.Value);
            Log.LogInfo("[PeakLab] " + area + " = " + cfg.Value);
        }
    }

    private static void UpdateCyclerLabel(TMP_Text label, string area, string val)
    {
        label.text = area + ": " + val.ToUpperInvariant();
    }

    private static void ToggleAdvanced()
    {
        SetAdvancedVisible(!_advOpen);
    }

    private static void SetAdvancedVisible(bool on)
    {
        _advOpen = on;
        for (int i = 0; i < _advRows.Count; i++)
        {
            if (_advRows[i] != null) _advRows[i].SetActive(on);
        }
    }

    private static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.zero;
        r.pivot = Vector2.zero;
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    private static void RandomSeedClicked()
    {
        int s = UnityEngine.Random.Range(1, 1000000);
        if (_input != null && _input) _input.text = s.ToString();
        Log.LogInfo("[PeakLab] seed aleatoria: " + s);
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
            Log.LogWarning("[PeakLab] leitura da seed: " + e.Message);
        }
        Log.LogInfo("[PeakLab] START: ascent=" + __instance.ascentIndex +
                    " seed=" + (PendingSeed.HasValue ? PendingSeed.Value.ToString() : "(daily)") +
                    " montanha=" + CfgMountain.Value + " praia=" + CfgBeach.Value +
                    " selva=" + CfgJungle.Value + " neve=" + CfgSnow.Value);
    }

    // ==================== MOTOR ====================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.StartsWith("Level_")) return;
        try
        {
            ApplyCustomization(scene);
            StartCoroutine(DelayedLog());
        }
        catch (Exception e)
        {
            Log.LogError("[PeakLab] ApplyCustomization falhou: " + e);
        }
    }

    private void ApplyCustomization(Scene scene)
    {
        bool hasSeed = PendingSeed.HasValue;
        string mountain = CfgMountain.Value;
        bool anyPin = mountain != "Auto" || CfgBeach.Value != "Auto" ||
                      CfgJungle.Value != "Auto" || CfgSnow.Value != "Auto";
        if (!hasSeed && !anyPin)
        {
            Log.LogInfo("[PeakLab] " + scene.name + ": daily vanilla puro");
            LogIslandState();
            return;
        }

        // 1) montanha: SOMENTE pin explicito (BETA). A troca automatica pela seed
        // foi removida na 1.0.1: o swap quebrava a intro e o jogador nascia no
        // oceano (NRE engolido pelo DOTween durante a cutscene).
        if (mountain != "Auto")
        {
            Log.LogWarning("[PeakLab] MONTANHA=" + mountain + " e BETA - se nascer na agua, volte para Auto");
            EnsureMountain(mountain);
        }

        // 2) seed nas variantes
        if (hasSeed)
        {
            int seed = PendingSeed.Value;
            UnityEngine.Random.InitState(seed);
            Component gen = FindInScene(scene, "LevelGeneration");
            if (gen != null)
            {
                FieldInfo sf = AccessTools.Field(gen.GetType(), "seed");
                if (sf != null) sf.SetValue(gen, seed);
                if (CfgVariants.Value)
                {
                    InvokeOn(gen, "RandomizeBiomeVariants");
                    RunVariantSelectors(scene);
                }
                if (CfgFullRegen.Value)
                {
                    InvokeOn(gen, "Clear");
                    InvokeOn(gen, "Generate");
                }
            }
            else Log.LogWarning("[PeakLab] LevelGeneration ausente em " + scene.name);
        }

        // 3) pins de area (por cima do sorteio)
        ApplyPin("Beach_Segment", CfgBeach.Value);
        ApplyPin("Jungle_Segment", CfgJungle.Value);
        ApplyPin("Snow_Segment", CfgSnow.Value);

        LogIslandState();
    }

    // troca segments[slotVariante] <-> variantSegments[0] quando o alvo difere do baked
    private static void EnsureMountain(string target)
    {
        try
        {
            Type mh = AccessTools.TypeByName("MapHandler");
            object handler = GetInstance(mh);
            if (handler == null) { Log.LogWarning("[PeakLab] MapHandler nulo"); return; }
            Array segs = AccessTools.Field(mh, "segments").GetValue(handler) as Array;
            Array vars = AccessTools.Field(mh, "variantSegments").GetValue(handler) as Array;
            if (segs == null || vars == null || vars.Length == 0)
            {
                Log.LogInfo("[PeakLab] cena sem segmento variante; montanha fica como esta");
                return;
            }
            int slot = -1;
            for (int i = 0; i < segs.Length; i++)
            {
                object s = segs.GetValue(i);
                if (s != null && true.Equals(ReadField(s, "hasVariant"))) { slot = i; break; }
            }
            if (slot < 0) { Log.LogInfo("[PeakLab] nenhum slot com variante"); return; }
            object cur = segs.GetValue(slot);
            object alt = vars.GetValue(0);
            string curBiome = "" + ReadField(cur, "_biome");
            if (curBiome == target)
            {
                Log.LogInfo("[PeakLab] montanha ja e " + target);
                return;
            }
            string altBiome = "" + ReadField(alt, "_biome");
            if (altBiome != target)
            {
                Log.LogWarning("[PeakLab] variante disponivel e " + altBiome + ", nao " + target);
                return;
            }
            segs.SetValue(alt, slot);
            vars.SetValue(cur, 0);
            SetField(alt, "isVariant", false);
            SetField(alt, "hasVariant", true);
            SetField(cur, "isVariant", true);
            SetField(cur, "hasVariant", false);
            GameObject curParent = ReadField(cur, "_segmentParent") as GameObject;
            GameObject altParent = ReadField(alt, "_segmentParent") as GameObject;
            if (curParent != null) curParent.SetActive(false);
            if (altParent != null)
            {
                altParent.SetActive(true);
                // os segmentos variantes vem com um ancestral desligado
                // (ex: Map/Biome_3/Desert) - sem ligar a cadeia toda o
                // segmento nunca aparece de verdade
                Transform anc = altParent.transform.parent;
                int guard = 0;
                while (anc != null && guard++ < 10)
                {
                    if (!anc.gameObject.activeSelf)
                    {
                        Log.LogInfo("[PeakLab] ativando ancestral: " + anc.name);
                        anc.gameObject.SetActive(true);
                    }
                    anc = anc.parent;
                }
            }
            Log.LogInfo("[PeakLab] montanha trocada: " + curBiome + " -> " + target +
                        " (slot " + slot + ", " +
                        (altParent != null ? altParent.name : "?") + " ativo)");
        }
        catch (Exception e)
        {
            Log.LogError("[PeakLab] EnsureMountain: " + e);
        }
    }

    // forca uma variante especifica de uma area (desliga as irmas)
    private static void ApplyPin(string segmentRoot, string choice)
    {
        if (choice == "Auto") return;
        try
        {
            Type bv = AccessTools.TypeByName("BiomeVariant");
            UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(bv);
            int hits = 0;
            bool found = false;
            foreach (UnityEngine.Object o in all)
            {
                Component c = o as Component;
                if (c == null || !c.gameObject.scene.IsValid()) continue;
                Transform p = c.transform.parent;
                if (p == null || p.name != segmentRoot) continue;
                hits++;
                bool pick = c.gameObject.name == choice;
                c.gameObject.SetActive(pick);
                if (pick) found = true;
            }
            if (hits == 0) Log.LogInfo("[PeakLab] pin " + segmentRoot + ": nenhum marcador (cena sem essa area?)");
            else if (!found) Log.LogWarning("[PeakLab] pin " + segmentRoot + ": variante '" + choice + "' nao existe aqui");
            else Log.LogInfo("[PeakLab] pin " + segmentRoot + " = " + choice);
        }
        catch (Exception e)
        {
            Log.LogError("[PeakLab] ApplyPin " + segmentRoot + ": " + e);
        }
    }

    // roda os seletores de micro-variantes (ex.: deserto) com o Random ja semeado
    private static void RunVariantSelectors(Scene scene)
    {
        try
        {
            Type t = AccessTools.TypeByName("VariantObjectSelector");
            if (t == null) return;
            UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(t);
            int n = 0;
            foreach (UnityEngine.Object o in all)
            {
                Component c = o as Component;
                if (c == null || c.gameObject.scene != scene) continue;
                MethodInfo m = AccessTools.Method(t, "SelectVariations");
                if (m != null) { m.Invoke(c, null); n++; }
            }
            if (n > 0) Log.LogInfo("[PeakLab] " + n + " VariantObjectSelector re-sorteados");
        }
        catch (Exception e)
        {
            Log.LogWarning("[PeakLab] RunVariantSelectors: " + e.Message);
        }
    }

    // ==================== util/log ====================

    private static Component FindInScene(Scene scene, string typeName)
    {
        Type t = AccessTools.TypeByName(typeName);
        if (t == null) return null;
        UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(t);
        foreach (UnityEngine.Object o in all)
        {
            Component c = o as Component;
            if (c != null && c.gameObject.scene == scene) return c;
        }
        return null;
    }

    private static object GetInstance(Type t)
    {
        PropertyInfo p = t.GetProperty("Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        return (p != null) ? p.GetValue(null, null) : null;
    }

    private static void InvokeOn(object target, string method)
    {
        try
        {
            MethodInfo m = AccessTools.Method(target.GetType(), method);
            if (m == null) { Log.LogWarning("[PeakLab] metodo ausente: " + method); return; }
            m.Invoke(target, null);
            Log.LogInfo("[PeakLab] " + method + "() OK");
        }
        catch (Exception e)
        {
            Log.LogError("[PeakLab] " + method + "() lancou: " + e);
        }
    }

    private static object ReadField(object o, string name)
    {
        FieldInfo f = AccessTools.Field(o.GetType(), name);
        return (f != null) ? f.GetValue(o) : null;
    }

    private static void SetField(object o, string name, object val)
    {
        FieldInfo f = AccessTools.Field(o.GetType(), name);
        if (f != null) f.SetValue(o, val);
    }

    private System.Collections.IEnumerator DelayedLog()
    {
        yield return new WaitForSeconds(12f);
        Log.LogInfo("[PeakLab] ===== estado assentado (t+12s) =====");
        LogIslandState();
    }

    private static void LogIslandState()
    {
        try
        {
            Type mh = AccessTools.TypeByName("MapHandler");
            object handler = GetInstance(mh);
            if (handler == null) return;
            Array segs = AccessTools.Field(mh, "segments").GetValue(handler) as Array;
            if (segs != null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("[PeakLab] segmentos:");
                for (int i = 0; i < segs.Length; i++)
                {
                    object s = segs.GetValue(i);
                    if (s == null) continue;
                    GameObject p = ReadField(s, "_segmentParent") as GameObject;
                    sb.AppendLine("  #" + i + " " + ReadField(s, "_biome") +
                                  " parent=" + (p != null ? p.name + (p.activeSelf ? "(on)" : "(OFF)") : "null"));
                }
                Log.LogInfo(sb.ToString());
            }
            Type bv = AccessTools.TypeByName("BiomeVariant");
            UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(bv);
            System.Text.StringBuilder vb = new System.Text.StringBuilder();
            int n = 0;
            foreach (UnityEngine.Object o in all)
            {
                Component c = o as Component;
                if (c == null || !c.gameObject.scene.IsValid()) continue;
                if (!c.gameObject.activeSelf) continue;
                if (n > 0) vb.Append(", ");
                Transform p = c.transform.parent;
                vb.Append((p != null ? p.name + "/" : "") + c.gameObject.name);
                n++;
            }
            Log.LogInfo("[PeakLab] variantes ativas: " + vb);
        }
        catch (Exception e)
        {
            Log.LogWarning("[PeakLab] LogIslandState: " + e.Message);
        }
    }
}

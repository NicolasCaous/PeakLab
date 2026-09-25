// PeakLabRegen - parte do PeakLab.dll
// Regenera o conteudo de cada bioma a partir da seed, com os geradores dos devs.
//
// O que o IL do build mostrou (tools/CallGraph):
//  - LevelGeneration.Generate() esta VAZIO no build (codigo so de editor).
//  - PropGrouper.RunAll() so roda os passos Early; os Late dependiam de um
//    callback de lightmap do editor que nunca e chamado no build.
//  - Toda aleatoriedade vem de UnityEngine.Random (value/Range), entao semear o
//    Random antes de cada bioma torna o resultado deterministico por seed.
// Por isso o motor aqui refaz o RunAll na mao: limpa todos os passos do bioma,
// roda os geradores de geometria (paredes/pedras), depois os passos Early e os
// Late, e registra os PhotonViews novos.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class PeakLabRegen
{
    // geradores que nao herdam de LevelGenStep: tipo, metodo de limpar, metodo de gerar
    private static readonly string[][] GeometryGens = new string[][]
    {
        new string[] { "WallPieceSpawner", "Clear", "Go" },
        new string[] { "RockSpawner", "Clear", "Go" },
        new string[] { "RockSpawnerGD", "clearList", "spawnObjects" },
        new string[] { "BeachSpawner", "Clear", "Spawn" }
    };

    private static ManualLogSource Log { get { return PeakLabPlugin.Log; } }

    // raizes dos biomas da trilha atual (os _segmentParent ativos do MapHandler)
    internal static List<GameObject> SegmentRoots()
    {
        List<GameObject> roots = new List<GameObject>();
        Type mh = AccessTools.TypeByName("MapHandler");
        PropertyInfo ip = mh.GetProperty("Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        object handler = (ip != null) ? ip.GetValue(null, null) : null;
        if (handler == null) return roots;
        // nas ilhas Mesa o deserto ativo mora em variantSegments, e o Snow_Segment
        // de segments[] fica com um ancestral desligado; por isso as duas listas
        // entram e so vale quem esta ativo de verdade na hierarquia
        string[] lists = new string[] { "segments", "variantSegments" };
        for (int l = 0; l < lists.Length; l++)
        {
            Array segs = AccessTools.Field(mh, lists[l]).GetValue(handler) as Array;
            if (segs == null) continue;
            for (int i = 0; i < segs.Length; i++)
            {
                object s = segs.GetValue(i);
                if (s == null) continue;
                FieldInfo pf = AccessTools.Field(s.GetType(), "_segmentParent");
                GameObject p = (pf != null) ? pf.GetValue(s) as GameObject : null;
                if (p != null && p.activeInHierarchy && !roots.Contains(p)) roots.Add(p);
            }
        }
        return roots;
    }

    // mistura seed + nome do bioma: cada bioma tem sua sequencia propria, entao
    // mexer num bioma nao muda o resultado dos outros
    private static int SeedFor(int seed, string area)
    {
        unchecked
        {
            int h = (int)2166136261;
            for (int i = 0; i < area.Length; i++) h = (h ^ area[i]) * 16777619;
            return seed * 486187739 + h;
        }
    }

    internal static int RegenerateAll(int seed)
    {
        List<GameObject> roots = SegmentRoots();
        int total = 0;
        foreach (GameObject root in roots)
        {
            try { total += Regenerate(root, seed); }
            catch (Exception e) { Log.LogError("[Regen] " + root.name + ": " + e); }
        }
        return total;
    }

    // limpa e regenera um bioma inteiro; devolve quantos geradores rodaram
    internal static int Regenerate(GameObject root, int seed)
    {
        float t0 = Time.realtimeSinceStartup;
        UnityEngine.Random.InitState(SeedFor(seed, root.name));

        Type stepT = AccessTools.TypeByName("LevelGenStep");
        MethodInfo stepClear = AccessTools.Method(stepT, "Clear");
        MethodInfo stepGo = AccessTools.Method(stepT, "Go");

        // 1) limpar: passos de tras para frente (igual ao ClearAll dos devs, mas
        // sem depender da ordem de destruicao) e depois a geometria
        Component[] steps = root.GetComponentsInChildren(stepT, false);
        bool diag = PeakLabPlugin.CfgDiag.Value;
        Dictionary<Component, int> bakedCount = new Dictionary<Component, int>();
        string bakedSpread = "";
        if (diag)
        {
            foreach (Component s in steps) bakedCount[s] = s.transform.childCount;
            bakedSpread = Spread(steps);
        }
        int cleared = 0;
        for (int i = steps.Length - 1; i >= 0; i--)
        {
            if (steps[i] == null) continue;
            if (Call(steps[i], stepClear)) cleared++;
        }
        int ran = 0;
        for (int g = 0; g < GeometryGens.Length; g++)
        {
            Type t = AccessTools.TypeByName(GeometryGens[g][0]);
            if (t == null) continue;
            MethodInfo clear = AccessTools.Method(t, GeometryGens[g][1]);
            MethodInfo go = AccessTools.Method(t, GeometryGens[g][2]);
            foreach (Component c in root.GetComponentsInChildren(t, false))
            {
                if (c == null) continue;
                if (clear != null && clear.GetParameters().Length == 0) Call(c, clear);
                // 2) geometria primeiro: os props fazem raycast em cima dela
                if (go != null && go.GetParameters().Length == 0 && Call(c, go)) ran++;
            }
        }
        Physics.SyncTransforms();

        // 3) passos Early e depois Late, na ordem da hierarquia (a mesma do RunAll)
        steps = root.GetComponentsInChildren(stepT, false);
        List<Component> late = new List<Component>();
        Type pgT = AccessTools.TypeByName("PropGrouper");
        FieldInfo timingF = AccessTools.Field(pgT, "timing");
        int early = 0;
        foreach (Component s in steps)
        {
            if (s == null) continue;
            Component pg = s.GetComponentInParent(pgT);
            bool isLate = pg != null && timingF != null && timingF.GetValue(pg).ToString() == "Late";
            if (isLate) { late.Add(s); continue; }
            if (Call(s, stepGo)) { ran++; early++; }
        }
        Physics.SyncTransforms();
        foreach (Component s in late)
        {
            if (s != null && Call(s, stepGo)) ran++;
        }

        // 4) grama por ultimo (le o chao e o que ficou em cima)
        Type grassT = AccessTools.TypeByName("BasicGrassSpawner");
        if (grassT != null)
        {
            MethodInfo gen = AccessTools.Method(grassT, "Generate");
            foreach (Component c in root.GetComponentsInChildren(grassT, false))
            {
                if (c != null && Call(c, gen)) ran++;
            }
        }

        if (diag) CompareWithBake(root, stepT, bakedCount, bakedSpread);

        int views = PeakLabPlugin.FixPhotonViews(root.GetComponent<Transform>());
        Log.LogInfo("[Regen] " + root.name + ": " + cleared + " passos limpos, " + ran +
                    " geradores (" + early + " early, " + late.Count + " late), " + views +
                    " PhotonViews novos, " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("F0") + " ms");
        return ran;
    }

    private static bool Call(Component c, MethodInfo m)
    {
        if (m == null) return false;
        try
        {
            m.Invoke(c, null);
            return true;
        }
        catch (Exception e)
        {
            Exception inner = e.InnerException ?? e;
            Log.LogWarning("[Regen] " + c.GetType().Name + "." + m.Name + " em " + PathOf(c.transform) +
                           ": " + inner.GetType().Name + ": " + inner.Message);
            return false;
        }
    }

    // ---------- diagnostico ----------

    // largura ocupada pelo que os passos geraram: faixa de x e z (percentis 5-95,
    // para uma pedra perdida nao distorcer) e a quantidade de objetos
    private static string Spread(Component[] steps)
    {
        List<float> xs = new List<float>();
        List<float> zs = new List<float>();
        foreach (Component s in steps)
        {
            if (s == null) continue;
            Transform t = s.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                Vector3 p = t.GetChild(i).position;
                xs.Add(p.x);
                zs.Add(p.z);
            }
        }
        if (xs.Count == 0) return "vazio";
        xs.Sort();
        zs.Sort();
        int lo = xs.Count * 5 / 100, hi = xs.Count * 95 / 100;
        if (hi >= xs.Count) hi = xs.Count - 1;
        return xs.Count + " objetos, x " + xs[lo].ToString("F0") + ".." + xs[hi].ToString("F0") +
               " (largura " + (xs[hi] - xs[lo]).ToString("F0") + "), z " + zs[lo].ToString("F0") +
               ".." + zs[hi].ToString("F0");
    }

    // compara passo a passo o que a ilha tinha de fabrica com o que a seed gerou
    private static void CompareWithBake(GameObject root, Type stepT, Dictionary<Component, int> baked, string bakedSpread)
    {
        Component[] now = root.GetComponentsInChildren(stepT, false);
        Log.LogInfo("[Compara] " + root.name + " fabrica: " + bakedSpread);
        Log.LogInfo("[Compara] " + root.name + " seed:    " + Spread(now));
        int sumB = 0, sumN = 0;
        foreach (Component s in now)
        {
            int b;
            if (s == null || !baked.TryGetValue(s, out b)) continue;
            int n = s.transform.childCount;
            sumB += b;
            sumN += n;
            // so os passos que mudaram muito (mais de 30% e pelo menos 5 objetos)
            if (Math.Abs(n - b) >= 5 && Math.Abs(n - b) > 0.3f * Math.Max(b, 1))
                Log.LogInfo("[Compara]   " + PathOf(s.transform).Replace(PathOf(root.transform) + "/", "") +
                            ": fabrica " + b + " -> seed " + n);
        }
        Log.LogInfo("[Compara] " + root.name + " total nos passos que sobreviveram: fabrica " + sumB + " -> seed " + sumN);
    }

    // impressao digital do conteudo de um bioma: nome + posicao arredondada de todo
    // objeto ativo. Mesma seed tem que dar a mesma impressao; seeds diferentes, outra.
    internal static string Fingerprint(GameObject root, out int count)
    {
        count = 0;
        uint h = 2166136261;
        Transform[] all = root.GetComponentsInChildren<Transform>(false);
        foreach (Transform t in all)
        {
            Vector3 p = t.position;
            string key = t.name + "|" + Mathf.RoundToInt(p.x * 10f) + "," +
                         Mathf.RoundToInt(p.y * 10f) + "," + Mathf.RoundToInt(p.z * 10f);
            for (int i = 0; i < key.Length; i++) h = unchecked((h ^ key[i]) * 16777619);
            count++;
        }
        return h.ToString("X8");
    }

    internal static void LogFingerprints(string tag)
    {
        StringBuilder sb = new StringBuilder();
        foreach (GameObject root in SegmentRoots())
        {
            int n;
            string fp = Fingerprint(root, out n);
            sb.Append(" " + root.name + "=" + fp + "(" + n + ")");
        }
        Log.LogInfo("[Regen] impressao " + tag + ":" + sb);
    }

    // censo dos geradores de cada bioma (so com Diagnostico ligado)
    internal static void Census()
    {
        Type stepT = AccessTools.TypeByName("LevelGenStep");
        Type pgT = AccessTools.TypeByName("PropGrouper");
        FieldInfo timingF = AccessTools.Field(pgT, "timing");
        foreach (GameObject root in SegmentRoots())
        {
            Dictionary<string, int> byType = new Dictionary<string, int>();
            int lateN = 0, earlyN = 0, inactive = 0, children = 0;
            foreach (Component s in root.GetComponentsInChildren(stepT, true))
            {
                if (!s.gameObject.activeInHierarchy) { inactive++; continue; }
                string k = s.GetType().Name;
                byType[k] = (byType.ContainsKey(k) ? byType[k] : 0) + 1;
                Component pg = s.GetComponentInParent(pgT);
                if (pg != null && timingF.GetValue(pg).ToString() == "Late") lateN++; else earlyN++;
                children += s.transform.childCount;
            }
            for (int g = 0; g < GeometryGens.Length; g++)
            {
                Type t = AccessTools.TypeByName(GeometryGens[g][0]);
                if (t == null) continue;
                Component[] cs = root.GetComponentsInChildren(t, false);
                if (cs.Length > 0) byType[GeometryGens[g][0]] = cs.Length;
                for (int i = 0; i < cs.Length && i < 5; i++)
                    Log.LogInfo("[Censo]   " + GeometryGens[g][0] + " em " + PathOf(cs[i].transform));
            }
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, int> kv in byType) sb.Append(kv.Key + "=" + kv.Value + " ");
            Log.LogInfo("[Censo] " + root.name + ": " + sb + "| early=" + earlyN + " late=" + lateN +
                        " inativos=" + inactive + " filhos dos passos=" + children);
            // grupos Late: quem sao
            foreach (Component pg in root.GetComponentsInChildren(pgT, false))
            {
                if (timingF.GetValue(pg).ToString() == "Late")
                    Log.LogInfo("[Censo]   grupo Late: " + PathOf(pg.transform));
            }
        }
    }

    internal static string PathOf(Transform t)
    {
        string p = t.name;
        int guard = 0;
        while (t.parent != null && guard++ < 12)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }
}

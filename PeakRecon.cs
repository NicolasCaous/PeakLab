// PeakRecon 0.1.0 - plugin passivo de diagnostico para PEAK v1.35.a
// Nao altera nada do jogo: apenas escreve dumps em BepInEx\recon\
// Compilado com csc.exe (C# 5) contra os DLLs do proprio jogo.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

[BepInPlugin("nicolas.peakrecon", "PeakRecon", "0.1.0")]
public class PeakReconPlugin : BaseUnityPlugin
{
    // tipos cuja API completa vai para types.txt
    private static readonly string[] TypeKeys = new string[]
    {
        "MapHandler", "MapSegment", "Biome", "MapGenerator", "LevelGeneration",
        "LevelGenStep", "GenerationNode", "NextLevel", "Ascent", "Campfire",
        "Kiosk", "RunHandler", "Variant", "Spawner", "Segment"
    };

    // componentes cujas instancias (e valores de campos) vao para scene_*.txt
    private static readonly string[] SceneDumpTypes = new string[]
    {
        "MapHandler", "Segment", "Campfire", "Biome", "Variant",
        "MapGenerator", "LevelGeneration", "Spawner", "Kiosk"
    };

    private string _dir;

    private void Awake()
    {
        try
        {
            _dir = Path.Combine(Paths.GameRootPath, Path.Combine("BepInEx", "recon"));
            Directory.CreateDirectory(_dir);
            DumpTypes();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Logger.LogInfo("PeakRecon pronto; dumps em " + _dir);
        }
        catch (Exception e)
        {
            Logger.LogError("PeakRecon Awake falhou: " + e);
        }
    }

    private void DumpTypes()
    {
        Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(delegate(Assembly a) { return a.GetName().Name == "Assembly-CSharp"; });
        if (asm == null)
        {
            Logger.LogWarning("PeakRecon: Assembly-CSharp nao encontrado");
            return;
        }
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException rtle)
        {
            types = rtle.Types.Where(delegate(Type t) { return t != null; }).ToArray();
        }
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("PeakRecon types.txt - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        foreach (Type t in types.OrderBy(delegate(Type t2) { return t2.FullName; }))
        {
            if (t == null || t.Name.IndexOf('<') >= 0) continue;
            if (!MatchesAny(t.Name, TypeKeys)) continue;
            DumpType(t, sb);
        }
        File.WriteAllText(Path.Combine(_dir, "types.txt"), sb.ToString());
        Logger.LogInfo("PeakRecon: types.txt escrito (" + sb.Length + " chars)");
    }

    private static bool MatchesAny(string name, string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (name.IndexOf(keys[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    private static void DumpType(Type t, StringBuilder sb)
    {
        string baseName = t.BaseType != null ? " : " + t.BaseType.Name : "";
        sb.AppendLine();
        sb.AppendLine("==== " + t.FullName + baseName);
        if (t.IsEnum)
        {
            sb.AppendLine("  ENUM: " + string.Join(", ", Enum.GetNames(t)));
            return;
        }
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                             BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (FieldInfo f in t.GetFields(flags))
        {
            if (f.Name.IndexOf('<') >= 0) continue;
            sb.AppendLine("  F " + (f.IsStatic ? "static " : "") + Nice(f.FieldType) + " " + f.Name);
        }
        foreach (PropertyInfo p in t.GetProperties(flags))
        {
            sb.AppendLine("  P " + Nice(p.PropertyType) + " " + p.Name);
        }
        foreach (MethodInfo m in t.GetMethods(flags))
        {
            if (m.IsSpecialName || m.Name.IndexOf('<') >= 0) continue;
            string ps = string.Join(", ", m.GetParameters()
                .Select(delegate(ParameterInfo pp) { return Nice(pp.ParameterType) + " " + pp.Name; }).ToArray());
            sb.AppendLine("  M " + (m.IsStatic ? "static " : "") + Nice(m.ReturnType) + " " + m.Name + "(" + ps + ")");
        }
    }

    private static string Nice(Type t)
    {
        if (t == null) return "?";
        if (!t.IsGenericType) return t.Name;
        string n = t.Name;
        int tick = n.IndexOf('`');
        if (tick > 0) n = n.Substring(0, tick);
        string args = string.Join(",", t.GetGenericArguments()
            .Select(delegate(Type a) { return Nice(a); }).ToArray());
        return n + "<" + args + ">";
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SCENE " + scene.name + " mode=" + mode + " roots=" + scene.rootCount +
                          " at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("======== COMPONENTES DE INTERESSE (inclui inativos e assets em memoria) ========");
            DumpInterestingObjects(sb);
            sb.AppendLine();
            sb.AppendLine("======== HIERARQUIA (profundidade<=7, max 12000 linhas) ========");
            int budget = 12000;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Walk(root.transform, 0, sb, ref budget);
                if (budget <= 0) { sb.AppendLine("... (limite de linhas atingido)"); break; }
            }
            File.WriteAllText(Path.Combine(_dir, "scene_" + Sanitize(scene.name) + ".txt"), sb.ToString());
            Logger.LogInfo("PeakRecon: dump da cena " + scene.name + " escrito");
        }
        catch (Exception e)
        {
            Logger.LogError("PeakRecon: dump da cena falhou: " + e);
        }
    }

    private void DumpInterestingObjects(StringBuilder sb)
    {
        UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(typeof(MonoBehaviour));
        Dictionary<string, List<MonoBehaviour>> byType = new Dictionary<string, List<MonoBehaviour>>();
        foreach (UnityEngine.Object o in all)
        {
            MonoBehaviour mb = o as MonoBehaviour;
            if (mb == null) continue;
            string tn = mb.GetType().Name;
            if (!MatchesAny(tn, SceneDumpTypes)) continue;
            List<MonoBehaviour> list;
            if (!byType.TryGetValue(tn, out list))
            {
                list = new List<MonoBehaviour>();
                byType[tn] = list;
            }
            list.Add(mb);
        }
        foreach (KeyValuePair<string, List<MonoBehaviour>> kv in byType.OrderBy(delegate(KeyValuePair<string, List<MonoBehaviour>> k) { return k.Key; }))
        {
            sb.AppendLine("### " + kv.Key + " x" + kv.Value.Count);
            int shown = 0;
            foreach (MonoBehaviour mb in kv.Value)
            {
                if (mb == null) continue;
                if (shown++ >= 25)
                {
                    sb.AppendLine("  ... (+" + (kv.Value.Count - 25) + " instancias omitidas)");
                    break;
                }
                string where;
                try
                {
                    Scene sc = mb.gameObject.scene;
                    where = sc.IsValid() ? sc.name : "ASSET/PREFAB";
                }
                catch (Exception) { where = "?"; }
                string act;
                try { act = mb.gameObject.activeInHierarchy ? "on" : "OFF"; }
                catch (Exception) { act = "?"; }
                sb.AppendLine("  @ [" + where + "] " + PathOf(mb.transform) + "  (" + act + ")");
                DumpFields(mb, sb, "      ");
            }
        }
    }

    private static void DumpFields(object obj, StringBuilder sb, string ind)
    {
        Type t = obj.GetType();
        int count = 0;
        while (t != null && t != typeof(MonoBehaviour) && t != typeof(Behaviour) &&
               t != typeof(Component) && t != typeof(UnityEngine.Object) && t != typeof(object))
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (f.Name.IndexOf('<') >= 0) continue;
                if (count++ > 80) { sb.AppendLine(ind + "... (limite de campos)"); return; }
                string vs;
                try { vs = Fmt(f.GetValue(obj)); }
                catch (Exception) { vs = "<erro>"; }
                sb.AppendLine(ind + f.Name + " = " + vs);
            }
            t = t.BaseType;
        }
    }

    private static string Fmt(object v)
    {
        if (v == null) return "null";
        if (v is string) return "\"" + (string)v + "\"";
        Type t = v.GetType();
        if (t.IsPrimitive || t.IsEnum || v is Vector3 || v is Vector2 ||
            v is Quaternion || v is Color || v is Bounds) return v.ToString();
        UnityEngine.Object uo = v as UnityEngine.Object;
        if (uo != null) return (uo ? uo.name : "null(destruido)") + " [" + t.Name + "]";
        ICollection col = v as ICollection;
        if (col != null)
        {
            StringBuilder s2 = new StringBuilder();
            s2.Append("Count=" + col.Count + " [");
            int i = 0;
            foreach (object item in col)
            {
                if (i >= 8) { s2.Append(", ..."); break; }
                if (i > 0) s2.Append(", ");
                if (item == null) s2.Append("null");
                else
                {
                    UnityEngine.Object iuo = item as UnityEngine.Object;
                    if (iuo != null) s2.Append(iuo ? iuo.name : "null(destruido)");
                    else s2.Append(item.ToString());
                }
                i++;
            }
            s2.Append("]");
            return s2.ToString();
        }
        return "{" + t.Name + "}";
    }

    private static void Walk(Transform tr, int depth, StringBuilder sb, ref int budget)
    {
        if (budget <= 0 || depth > 7) return;
        budget--;
        StringBuilder line = new StringBuilder();
        line.Append(new string(' ', depth * 2));
        line.Append(tr.name);
        if (!tr.gameObject.activeSelf) line.Append(" [OFF]");
        Component[] comps = tr.GetComponents(typeof(Component));
        List<string> cn = new List<string>();
        foreach (Component c in comps)
        {
            if (c == null) { cn.Add("Missing"); continue; }
            string nm = c.GetType().Name;
            if (nm != "Transform") cn.Add(nm);
        }
        if (cn.Count > 0) line.Append("  <" + string.Join(",", cn.ToArray()) + ">");
        line.Append("  y=" + tr.position.y.ToString("F0"));
        sb.AppendLine(line.ToString());
        for (int i = 0; i < tr.childCount; i++)
        {
            Walk(tr.GetChild(i), depth + 1, sb, ref budget);
        }
    }

    private static string PathOf(Transform tr)
    {
        string p = tr.name;
        Transform cur = tr.parent;
        int guard = 0;
        while (cur != null && guard++ < 32)
        {
            p = cur.name + "/" + p;
            cur = cur.parent;
        }
        return p;
    }

    private static string Sanitize(string s)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char c in s)
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        }
        return sb.ToString();
    }
}

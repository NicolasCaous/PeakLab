// DumpApi - inspetor offline de assemblies do PEAK (nao precisa abrir o jogo)
// uso: DumpApi.exe <pasta Managed> <trecho-do-nome-1> [trecho-2 ...]
using System;
using System.IO;
using System.Linq;
using System.Reflection;

class DumpApi
{
    static string _dir;

    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("uso: DumpApi.exe <pasta Managed> <trecho-do-nome> [...]");
            return 1;
        }
        _dir = args[0];
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        Assembly asm = Assembly.LoadFrom(Path.Combine(_dir, "Assembly-CSharp.dll"));
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(delegate(Type t) { return t != null; }).ToArray();
        }
        foreach (Type t in types.OrderBy(delegate(Type x) { return x.FullName; }))
        {
            if (t.Name.IndexOf('<') >= 0) continue;
            bool hit = false;
            for (int i = 1; i < args.Length; i++)
            {
                if (t.Name.IndexOf(args[i], StringComparison.OrdinalIgnoreCase) >= 0) { hit = true; break; }
            }
            if (!hit) continue;
            Dump(t);
        }
        return 0;
    }

    static Assembly Resolve(object s, ResolveEventArgs e)
    {
        string n = new AssemblyName(e.Name).Name;
        string p = Path.Combine(_dir, n + ".dll");
        if (File.Exists(p))
        {
            try { return Assembly.LoadFrom(p); } catch (Exception) { }
        }
        return null;
    }

    static void Dump(Type t)
    {
        Console.WriteLine();
        Console.WriteLine("==== " + t.FullName + (t.BaseType != null ? " : " + t.BaseType.Name : "") +
                          (t.IsPublic || t.IsNestedPublic ? "" : "  [nao-publico]"));
        if (t.IsEnum)
        {
            Console.WriteLine("  ENUM: " + string.Join(", ", Enum.GetNames(t)));
            return;
        }
        BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (FieldInfo fi in t.GetFields(f))
        {
            if (fi.Name.IndexOf('<') >= 0) continue;
            Console.WriteLine("  F " + Vis(fi.IsPublic) + (fi.IsStatic ? "static " : "") + Nice(fi.FieldType) + " " + fi.Name);
        }
        foreach (PropertyInfo p in t.GetProperties(f))
        {
            Console.WriteLine("  P " + Nice(p.PropertyType) + " " + p.Name);
        }
        foreach (MethodInfo m in t.GetMethods(f))
        {
            if (m.IsSpecialName || m.Name.IndexOf('<') >= 0) continue;
            string ps = string.Join(", ", m.GetParameters()
                .Select(delegate(ParameterInfo pp) { return Nice(pp.ParameterType) + " " + pp.Name; }).ToArray());
            Console.WriteLine("  M " + Vis(m.IsPublic) + (m.IsStatic ? "static " : "") + Nice(m.ReturnType) + " " + m.Name + "(" + ps + ")");
        }
    }

    static string Vis(bool isPublic) { return isPublic ? "pub " : "prv "; }

    static string Nice(Type t)
    {
        if (t == null) return "?";
        if (!t.IsGenericType) return t.Name;
        string n = t.Name;
        int tick = n.IndexOf('`');
        if (tick > 0) n = n.Substring(0, tick);
        return n + "<" + string.Join(",", t.GetGenericArguments()
            .Select(delegate(Type a) { return Nice(a); }).ToArray()) + ">";
    }
}

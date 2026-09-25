// CallGraph - le o IL de metodos do PEAK por reflexao e lista chamadas, campos e strings
// (nao ha decompilador na maquina; isso substitui o "caixa preta" por evidencia)
// uso: CallGraph.exe <pasta Managed> Tipo.Metodo [Tipo.Metodo ...]
//      Tipo.*  lista todos os metodos declarados do tipo
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

class CallGraph
{
    static string _dir;
    static readonly Dictionary<short, OpCode> Ops = new Dictionary<short, OpCode>();

    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("uso: CallGraph.exe <pasta Managed> Tipo.Metodo [...]");
            return 1;
        }
        _dir = args[0];
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        foreach (FieldInfo fi in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            OpCode oc = (OpCode)fi.GetValue(null);
            Ops[oc.Value] = oc;
        }
        Assembly asm = Assembly.LoadFrom(Path.Combine(_dir, "Assembly-CSharp.dll"));
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(delegate(Type t) { return t != null; }).ToArray(); }

        for (int i = 1; i < args.Length; i++)
        {
            string spec = args[i];
            // sub:Base lista as subclasses (diretas e indiretas) de um tipo
            if (spec.StartsWith("sub:"))
            {
                string bn = spec.Substring(4);
                foreach (Type x in types)
                {
                    for (Type b = x.BaseType; b != null; b = b.BaseType)
                    {
                        if (b.Name == bn) { Console.WriteLine(x.FullName + " : " + x.BaseType.Name); break; }
                    }
                }
                continue;
            }
            int dot = spec.LastIndexOf('.');
            string tn = spec.Substring(0, dot);
            string mn = spec.Substring(dot + 1);
            Type t = types.FirstOrDefault(delegate(Type x) { return x.FullName == tn || x.Name == tn; });
            if (t == null) { Console.WriteLine("tipo nao achado: " + tn); continue; }
            BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                             BindingFlags.Static | BindingFlags.DeclaredOnly;
            List<MethodBase> ms = new List<MethodBase>();
            ms.AddRange(t.GetMethods(f));
            ms.AddRange(t.GetConstructors(f));
            foreach (MethodBase m in ms)
            {
                if (mn != "*" && m.Name != mn) continue;
                try { DumpMethod(t, m); }
                catch (Exception ex) { Console.WriteLine("  [erro: " + ex.Message + "]"); }
            }
            // corpos de lambdas/iteradores ficam em tipos aninhados gerados pelo compilador
            if (mn != "*")
            {
                foreach (Type nt in t.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (nt.Name.IndexOf(mn, StringComparison.Ordinal) < 0 || nt.Name.IndexOf('<') < 0) continue;
                    foreach (MethodInfo m in nt.GetMethods(f))
                    {
                        if (m.Name != "MoveNext") continue;
                        try { DumpMethod(nt, m); }
                        catch (Exception ex) { Console.WriteLine("  [erro: " + ex.Message + "]"); }
                    }
                }
                foreach (MethodInfo m in t.GetMethods(f))
                {
                    if (m.Name.StartsWith("<" + mn + ">"))
                    {
                        try { DumpMethod(t, m); }
                        catch (Exception ex) { Console.WriteLine("  [erro: " + ex.Message + "]"); }
                    }
                }
            }
        }
        return 0;
    }

    static Assembly Resolve(object s, ResolveEventArgs e)
    {
        string n = new AssemblyName(e.Name).Name;
        string p = Path.Combine(_dir, n + ".dll");
        if (File.Exists(p)) { try { return Assembly.LoadFrom(p); } catch (Exception) { } }
        return null;
    }

    static void DumpMethod(Type t, MethodBase m)
    {
        Console.WriteLine();
        Console.WriteLine("==== " + t.FullName + "." + m.Name + "(" +
            string.Join(", ", m.GetParameters().Select(delegate(ParameterInfo p) { return p.ParameterType.Name + " " + p.Name; }).ToArray()) + ")");
        MethodBody body = m.GetMethodBody();
        if (body == null) { Console.WriteLine("  (sem corpo)"); return; }
        byte[] il = body.GetILAsByteArray();
        Module mod = m.Module;
        Type[] targs = t.IsGenericType ? t.GetGenericArguments() : null;
        Type[] margs = m.IsGenericMethod ? m.GetGenericArguments() : null;
        int pos = 0;
        while (pos < il.Length)
        {
            int at = pos;
            short v = il[pos++];
            if (v == 0xFE) v = (short)(0xFE00 | il[pos++]);
            OpCode oc;
            if (!Ops.TryGetValue(v, out oc)) { Console.WriteLine("  ?? opcode " + v); return; }
            string line = null;
            switch (oc.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    if (oc == OpCodes.Ldc_I4_S) line = "ldc " + (sbyte)il[pos];
                    pos += 1; break;
                case OperandType.InlineVar: pos += 2; break;
                case OperandType.InlineBrTarget:
                case OperandType.InlineSig:
                case OperandType.ShortInlineR:
                    if (oc == OpCodes.Ldc_R4) line = "ldc " + BitConverter.ToSingle(il, pos);
                    pos += 4; break;
                case OperandType.InlineI:
                    line = "ldc " + BitConverter.ToInt32(il, pos);
                    pos += 4; break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    pos += 8; break;
                case OperandType.InlineSwitch:
                    int n = BitConverter.ToInt32(il, pos);
                    pos += 4 + 4 * n; break;
                case OperandType.InlineString:
                    line = "str \"" + mod.ResolveString(BitConverter.ToInt32(il, pos)) + "\"";
                    pos += 4; break;
                case OperandType.InlineMethod:
                    {
                        int tok = BitConverter.ToInt32(il, pos); pos += 4;
                        try
                        {
                            MethodBase mb = mod.ResolveMethod(tok, targs, margs);
                            line = oc.Name + " " + (mb.DeclaringType != null ? mb.DeclaringType.Name : "?") + "." + mb.Name;
                        }
                        catch (Exception) { line = oc.Name + " <token " + tok + ">"; }
                        break;
                    }
                case OperandType.InlineField:
                    {
                        int tok = BitConverter.ToInt32(il, pos); pos += 4;
                        try
                        {
                            FieldInfo fi = mod.ResolveField(tok, targs, margs);
                            line = oc.Name + " " + fi.DeclaringType.Name + "." + fi.Name;
                        }
                        catch (Exception) { line = oc.Name + " <field " + tok + ">"; }
                        break;
                    }
                case OperandType.InlineType:
                case OperandType.InlineTok:
                    {
                        int tok = BitConverter.ToInt32(il, pos); pos += 4;
                        try
                        {
                            MemberInfo mi = mod.ResolveMember(tok, targs, margs);
                            line = oc.Name + " " + (mi is Type ? ((Type)mi).Name : mi.Name);
                        }
                        catch (Exception) { line = oc.Name + " <tok " + tok + ">"; }
                        break;
                    }
                default: pos += 4; break;
            }
            if (line != null) Console.WriteLine("  " + at.ToString("X4") + " " + line);
        }
    }
}

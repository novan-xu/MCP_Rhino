using System.Reflection;
using System.Runtime.Loader;
using System.Reflection.Emit;

string root = @"C:\Program Files\Rhino 8";
string[] folders = { root + @"\System", root + @"\System\netcore", root + @"\Plug-ins\Grasshopper", root + @"\Plug-ins\Grasshopper\Components", root + @"\Plug-ins\RhinoCode" };
AssemblyLoadContext.Default.Resolving += (_, name) => {
    foreach (string folder in folders)
        foreach (string extension in new[] { ".dll", ".gha", ".rhp" }) {
            string file = Path.Combine(folder, name.Name + extension);
            if (File.Exists(file)) return Assembly.LoadFrom(file);
        }
    return null;
};
var files = new[] { root + @"\Plug-ins\Grasshopper\Components\RhinoCodePluginGH.gha", root + @"\System\RhinoCodePlatform.GH.Context.dll", root + @"\System\RhinoCodePlatform.GH.dll", root + @"\System\RhinoCodePlatform.GH1.dll", root + @"\System\Rhino.Runtime.Code.dll", root + @"\System\RhinoCodePlatform.Rhino3D.dll", root + @"\Plug-ins\Grasshopper\Grasshopper.dll" };
foreach (string file in files) {
    var assembly = Assembly.LoadFrom(file);
    Console.WriteLine("ASSEMBLY " + assembly.FullName);
    IEnumerable<Type> types;
    try { types = assembly.GetTypes(); }
    catch (ReflectionTypeLoadException error) { types = error.Types.Where(t => t != null); }
    foreach (var type in types.Where(t => args.Any(a => t.FullName.Contains(a)))) {
        Console.WriteLine("TYPE " + type.FullName + " public=" + type.IsVisible);
        if (args.Contains("--names")) continue;
        string[] callMethods = args.Where(a => a.StartsWith("--calls:")).Select(a => a.Substring(8)).ToArray();
        if (callMethods.Length > 0) {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Where(m => callMethods.Contains(m.Name))) PrintCalls(method, args.Contains("--instructions"));
            if (callMethods.Contains(".cctor") && type.TypeInitializer != null) PrintCalls(type.TypeInitializer);
            continue;
        }
        foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).OrderBy(m => m.Name)) {
            try { Console.WriteLine("  " + member.MemberType + " " + member + (member is FieldInfo field && field.IsLiteral ? " = " + field.GetRawConstantValue() : "")); }
            catch (Exception error) { Console.WriteLine("  " + member.Name + ": " + error.Message); }
        }
    }
}

static void PrintCalls(MethodBase method, bool instructions = false) {
    Console.WriteLine("METHOD " + method);
    byte[] il = method.GetMethodBody()?.GetILAsByteArray();
    if (il == null) return;
    var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(c => (ushort)c.Value);
    for (int i = 0; i < il.Length;) {
        int offset = i;
        ushort value = il[i++]; if (value == 0xfe) value = (ushort)(0xfe00 | il[i++]);
        var code = codes[value];
        if (instructions) Console.WriteLine("  IL_" + offset.ToString("X4") + ": " + code.Name +
            (code.OperandType == OperandType.ShortInlineBrTarget ? " -> " + (i + 1 + (sbyte)il[i]).ToString("X4") :
            code.OperandType == OperandType.InlineBrTarget ? " -> " + (i + 4 + BitConverter.ToInt32(il,i)).ToString("X4") : ""));
        if (new[] { OperandType.InlineMethod, OperandType.InlineField, OperandType.InlineType, OperandType.InlineTok }.Contains(code.OperandType)) {
            try { var resolved = method.Module.ResolveMember(BitConverter.ToInt32(il, i), method.DeclaringType?.GetGenericArguments(), method is MethodInfo m ? m.GetGenericArguments() : null); Console.WriteLine("  " + code.Name + " " + resolved?.DeclaringType + " :: " + resolved); }
            catch (Exception error) { Console.WriteLine(error.Message); }
        }
        if (code.OperandType == OperandType.InlineString) Console.WriteLine("  string " + method.Module.ResolveString(BitConverter.ToInt32(il, i)));
        i += code.OperandType switch {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
            _ => 4
        };
    }
}

using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;

const string grasshopperDirectory = @"C:\Program Files\Rhino 8\Plug-ins\Grasshopper";
const string rhinoSystemDirectory = @"C:\Program Files\Rhino 8\System\netcore";
const string rhinoSharedDirectory = @"C:\Program Files\Rhino 8\System";

AssemblyLoadContext.Default.Resolving += (_, name) =>
    LoadIfPresent(Path.Combine(grasshopperDirectory, name.Name + ".dll"))
    ?? LoadIfPresent(Path.Combine(rhinoSystemDirectory, name.Name + ".dll"))
    ?? LoadIfPresent(Path.Combine(rhinoSharedDirectory, name.Name + ".dll"));

Assembly grasshopper = Assembly.LoadFrom(Path.Combine(grasshopperDirectory, "Grasshopper.dll"));
Assembly ghIo = Assembly.LoadFrom(Path.Combine(grasshopperDirectory, "GH_IO.dll"));
Assembly grasshopperPlugin = Assembly.LoadFrom(Path.Combine(grasshopperDirectory, "GrasshopperPlugin.rhp"));
Assembly rhinoCommon = Assembly.LoadFrom(Path.Combine(rhinoSystemDirectory, "RhinoCommon.dll"));

PrintAssembly(grasshopper);
PrintAssembly(ghIo);
PrintAssembly(grasshopperPlugin);
PrintAssembly(rhinoCommon);

Type rhinoPluginType = rhinoCommon.GetType("Rhino.PlugIns.PlugIn")
    ?? throw new InvalidOperationException("Rhino.PlugIns.PlugIn is missing.");
foreach (Type pluginType in grasshopperPlugin.GetTypes().Where(type =>
             rhinoPluginType.IsAssignableFrom(type) && type != rhinoPluginType))
{
    Console.WriteLine($"PLUGIN {pluginType.FullName} guid={pluginType.GetCustomAttribute<GuidAttribute>()?.Value ?? "<none>"}");
}

string[] typeNames =
[
    "Grasshopper.Instances",
    "Grasshopper.Kernel.GH_Document",
    "Grasshopper.Kernel.GH_DocumentServer",
    "Grasshopper.Kernel.GH_ComponentServer",
    "Grasshopper.Kernel.IGH_ObjectProxy",
    "Grasshopper.Kernel.GH_AssemblyInfo",
    "Grasshopper.Kernel.GH_DocumentIO",
    "Grasshopper.Kernel.Undo.GH_UndoServer",
    "Grasshopper.Kernel.Undo.GH_UndoRecord",
    "Grasshopper.Kernel.GH_Document+GH_UndoUtil",
    "Grasshopper.Kernel.Undo.Actions.GH_AddObjectAction",
    "Grasshopper.Kernel.Undo.Actions.GH_RemoveObjectAction",
    "Grasshopper.Kernel.GH_ComponentParamServer",
    "Grasshopper.Kernel.IGH_DocumentObject",
    "Grasshopper.Kernel.IGH_Param",
    "Grasshopper.Kernel.IGH_Component",
    "Grasshopper.Kernel.Special.GH_NumberSlider",
    "Grasshopper.GUI.Base.GH_SliderBase",
    "Grasshopper.Kernel.IGH_Attributes",
    "Grasshopper.Kernel.GH_ActiveObject"
];

foreach (string typeName in typeNames)
{
    PrintType(grasshopper.GetType(typeName));
}

PrintType(rhinoCommon.GetType("Rhino.PlugIns.PlugIn"));
PrintType(rhinoCommon.GetType("Rhino.RhinoApp"));
PrintType(ghIo.GetType("GH_IO.Serialization.GH_Archive"));
PrintType(ghIo.GetType("GH_IO.Serialization.GH_Chunk"));

static Assembly? LoadIfPresent(string path) => File.Exists(path) ? Assembly.LoadFrom(path) : null;

static void PrintAssembly(Assembly assembly)
{
    string guid = assembly.GetCustomAttribute<GuidAttribute>()?.Value ?? "<none>";
    Console.WriteLine($"ASSEMBLY {assembly.GetName().Name} {assembly.GetName().Version} guid={guid} {assembly.Location}");
}

static void PrintType(Type? type)
{
    if (type is null)
    {
        Console.WriteLine("TYPE MISSING");
        return;
    }

    Console.WriteLine($"\nTYPE {type.FullName}");
    foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
             .Where(member => member.MemberType is MemberTypes.Constructor or MemberTypes.Event or MemberTypes.Field or MemberTypes.Method or MemberTypes.Property)
             .OrderBy(member => member.MemberType)
             .ThenBy(member => member.Name))
    {
        try
        {
            string signature = member switch
            {
                MethodInfo method => $"{FormatType(method.ReturnType)} {method.Name}({string.Join(", ", method.GetParameters().Select(FormatParameter))})",
                ConstructorInfo ctor => $"{type.Name}({string.Join(", ", ctor.GetParameters().Select(FormatParameter))})",
                PropertyInfo property => $"{FormatType(property.PropertyType)} {property.Name}",
                FieldInfo field => $"{FormatType(field.FieldType)} {field.Name}",
                EventInfo evt => $"{FormatType(evt.EventHandlerType)} {evt.Name}",
                _ => member.Name
            };
            Console.WriteLine($"  {member.MemberType}: {signature}");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"  {member.MemberType}: {member.Name} [signature unavailable: {exception.GetType().Name}]");
        }
    }
}

static string FormatParameter(ParameterInfo parameter) =>
    $"{FormatType(parameter.ParameterType)} {parameter.Name}";

static string FormatType(Type? type)
{
    if (type is null)
    {
        return "null";
    }

    if (!type.IsGenericType)
    {
        return type.FullName ?? type.Name;
    }

    string name = type.GetGenericTypeDefinition().FullName?.Split('`')[0] ?? type.Name;
    return $"{name}<{string.Join(",", type.GetGenericArguments().Select(FormatType))}>";
}

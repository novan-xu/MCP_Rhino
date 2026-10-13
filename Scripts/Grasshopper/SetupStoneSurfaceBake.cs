// #! csharp
// LEGACY OPTIONAL BOOTSTRAP. Prefer linking StoneSurfaceBake.cs directly to the
// special script input with Input is Path; it now configures itself without pasting.
// ONE-TIME SETUP: paste this entire file into a fresh Rhino 8 C# Script component.
// Keep its default x/y inputs and out/a outputs. No wires or input data are needed.
// Save/close the editor. The component configures itself and links the file below.
#r "C:/Program Files/Rhino 8/Plug-ins/Grasshopper/Components/RhinoCodePluginGH.gha"
#r "C:/Program Files/Rhino 8/System/RhinoCodePlatform.Rhino3D.dll"
using System;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using RhinoCodePluginGH.Components;
using RhinoCodePluginGH.Parameters;
using RhinoCodePluginGH.Types;
using RhinoCodePlatform.Rhino3D.Languages.GH1;

public class Script_Instance : GH_ScriptInstance
{
    // Change only this path if you move StoneSurfaceBake.cs.
    private const string SourceFile = @"C:\01_Projects\MCP_Rhino\Scripts\Grasshopper\StoneSurfaceBake.cs";
    private bool _scheduled;

    private void RunScript(object x, object y, ref object a)
    {
        if (_scheduled) { a = "Stone Surface Bake setup is scheduled."; return; }
        var component = Component as CSharpComponent;
        if (component == null)
            throw new InvalidOperationException("Use the Rhino 8 C# Script component, not the legacy component.");
        var document = component.OnPingDocument();
        if (document == null) throw new InvalidOperationException("Place the component on a Grasshopper canvas first.");
        string path = Path.GetFullPath(SourceFile);
        ValidateFreshComponent(component);
        ValidateFile(path);
        _scheduled = true;
        a = "Configuring Stone Surface Bake. Bake starts false.";

        // Changing ports or script source during SolveInstance is unsafe. Wait until it ends.
        document.ScheduleSolution(10, scheduledDocument =>
        {
            if (component.OnPingDocument() != scheduledDocument) return;
            bool started = false;
            try
            {
                ValidateFreshComponent(component);
                ValidateFile(path);
                // Construct a file-backed script value before touching the component.
                var linkedScript = new Grasshopper1Script(new Uri(path));
                var scriptValue = new ScriptType(linkedScript);
                component.RecordUndoEvent("Set up Stone Surface Bake");
                started = true;
                Configure(component, scriptValue);
                component.ExpireSolution(false);
            }
            catch (Exception error)
            {
                // Stop a partially configured component; never let it reach a bake.
                if (started) component.Locked = true;
                component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Setup failed: " + error.Message +
                    (started ? " The component is disabled. Use Grasshopper Undo or a fresh component." : ""));
            }
        });
    }

    private static void ValidateFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Cannot find StoneSurfaceBake.cs. Edit SourceFile in the setup script.", path);
        string source = File.ReadAllText(path);
        if (!source.Contains("public static class StoneSurfaceBake") || !source.Contains("LiveStoneSurfaceComponent.Run"))
            throw new InvalidOperationException("SourceFile must point to the supplied StoneSurfaceBake.cs delivery file.");
    }

    private static void ValidateFreshComponent(CSharpComponent component)
    {
        string[] inputs = component.Params.Input.Select(p => p.NickName).ToArray();
        string[] outputs = component.Params.Output.Select(p => p.NickName).ToArray();
        if (!inputs.SequenceEqual(new[] { "x", "y" }) ||
            !(outputs.SequenceEqual(new[] { "out", "a" }) || outputs.SequenceEqual(new[] { "a" })))
            throw new InvalidOperationException("Paste setup into a fresh C# component with its default x/y and out/a ports.");
        if (component.Params.Input.Any(p => p.SourceCount > 0) || component.Params.Output.Any(p => p.Recipients.Count > 0) ||
            component.Params.Input.OfType<ScriptVariableParam>().Any(p => p.PersistentDataCount > 0))
            throw new InvalidOperationException("Use an unconnected, empty C# component. Setup preserves existing wired components by refusing to replace their ports.");
    }

    private static void Configure(CSharpComponent component, ScriptType scriptValue)
    {
        component.UsingStandardOutputParam = false;
        component.UsingScriptInputParam = false;
        component.UsingScriptOutputParam = false;
        component.UsingLibraryInputParam = false;
        foreach (var parameter in component.Params.Input.ToArray())
            component.Params.UnregisterInputParameter(parameter, true);
        foreach (var parameter in component.Params.Output.ToArray())
            component.Params.UnregisterOutputParameter(parameter, true);

        AddInput(component, "FinBaseZ", "Fin base offset in world Z, in inches. Positive or negative.", typeof(double), new GH_Number(0));
        AddInput(component, "FinHeight", "Fin surface height in world +Z, in inches. Must be positive.", typeof(double), new GH_Number(5));
        AddInput(component, "FlatBaseZ", "Flat-stone base offset in world Z, in inches. Positive or negative.", typeof(double), new GH_Number(0));
        AddInput(component, "FlatHeight", "Flat-stone surface height in world +Z, in inches. Must be positive.", typeof(double), new GH_Number(5));
        AddInput(component, "Bake", "False previews. True creates or updates the WF blocks in the active Rhino document.", typeof(bool), new GH_Boolean(false));

        AddOutput(component, "Fins", "Fin surface preview geometry.");
        AddOutput(component, "Flats", "Flat-stone surface preview geometry.");
        AddOutput(component, "BakedIds", "Rhino block instance IDs from the bake.");
        AddOutput(component, "Report", "Target document, counts, and any errors.");

        // Shared scripts require Script-Mode, so replace this SDK bootstrap first.
        component.SetSource("// The StoneSurfaceBake.cs file is supplied by the script input.\n");
        component.UsingScriptInputParam = true;
        var scriptInput = component.Params.Input.OfType<BaseScriptParam>().Single();
        // A typed file-backed ScriptType carries its URI; no manual Input is Path switch is needed.
        scriptInput.SetPersistentData(scriptValue);
        component.VariableParameterMaintenance();
        component.Params.OnParametersChanged();
        component.Name = "Stone Surface Bake";
        component.NickName = "Stone Surface Bake";
        component.Description = "Bake fin and flat-stone surfaces from STN-MET-GA- assemblies. All numeric inputs are inches.";
        component.Tooltip = component.Description;
        component.Message = "STN-MET-GA- | inches";
        component.Attributes.ExpireLayout();
    }

    private static void AddInput(CSharpComponent component, string name, string description, Type type, IGH_Goo value)
    {
        var parameter = new ScriptVariableParam(name)
        {
            PrettyName = name,
            ToolTip = description,
            Optional = true,
            Access = GH_ParamAccess.item,
            AllowTreeAccess = false
        };
        if (parameter.TypeHints.Select(type) == null)
            throw new InvalidOperationException("The installed Rhino version cannot select the " + type.Name + " type hint.");
        parameter.SetPersistentData(value);
        parameter.CreateAttributes();
        component.Params.RegisterInputParam(parameter);
    }

    private static void AddOutput(CSharpComponent component, string name, string description)
    {
        var parameter = new ScriptVariableParam(name) { PrettyName = name, ToolTip = description };
        parameter.CreateAttributes();
        component.Params.RegisterOutputParam(parameter);
    }
}

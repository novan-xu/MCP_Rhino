// Bundled after the Script-Mode entry; imports are supplied by that entry.
public static class LiveStoneSurfaceComponent
{
    private static readonly string[] InputNames = { "FinBaseZ", "FinHeight", "FlatBaseZ", "FlatHeight", "Bake" };
    private static readonly string[] OutputNames = { "Fins", "Flats", "BakedIds", "Report" };
    private static readonly HashSet<Guid> PendingSetup = new HashSet<Guid>();

    public static void Run(CSharpComponent component, IGH_DataAccess data)
    {
        if (component == null || data == null)
            throw new InvalidOperationException("Load this file through a Rhino 8 C# Script component's script input.");
        try
        {
            if (!HasConfiguredNames(component))
            {
                ScheduleSetup(component);
                Console.WriteLine("Configuring Stone Surface Bake inputs. Bake starts false.");
                return;
            }
            ValidateAccess(component);
            // A document-wide bake accepts one setting per input, not GH list matching.
            foreach (var parameter in component.Params.Input.OfType<ScriptVariableParam>())
                if (parameter.VolatileDataCount > 1)
                    throw new InvalidOperationException("Supply one value to " + parameter.VariableName + ", not a list or multiple branches.");

            var settings = new StoneSurfaceBake.Settings(
                ReadNumber(component, data, "FinBaseZ", 0), ReadNumber(component, data, "FinHeight", 5),
                ReadNumber(component, data, "FlatBaseZ", 0), ReadNumber(component, data, "FlatHeight", 5));
            bool bake = false;
            data.GetData(InputIndex(component, "Bake"), ref bake);
            var result = StoneSurfaceBake.Run(settings, bake);
            WriteResults(component, data, result);
            Console.WriteLine(result.Report);
        }
        catch (Exception error)
        {
            string message = "ERROR: " + error.Message;
            int reportIndex = component.Params.Output.FindIndex(p => p.NickName == "Report");
            if (reportIndex >= 0) data.SetData(reportIndex, message);
            component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
            Console.WriteLine(message);
        }
    }

    private static bool HasConfiguredNames(CSharpComponent component)
    {
        return component.Params.Input.OfType<ScriptVariableParam>().Select(p => p.VariableName).SequenceEqual(InputNames)
            && component.Params.Output.OfType<ScriptVariableParam>().Select(p => p.VariableName).SequenceEqual(OutputNames);
    }

    private static void ValidateAccess(CSharpComponent component)
    {
        if (component.Params.Input.OfType<ScriptVariableParam>().Any(p => p.Access != GH_ParamAccess.item))
            throw new InvalidOperationException("All five stone inputs must use Item access.");
        foreach (var output in component.Params.Output.OfType<ScriptVariableParam>())
        {
            var hint = output.TypeHints.GetSelected();
            var noHint = output.TypeHints.Find(typeof(object));
            if (hint != null && (noHint == null || hint.HintID != noHint.HintID))
                throw new InvalidOperationException("Keep " + output.VariableName + " at No Type Hint.");
        }
    }

    private static void ValidateFreshComponent(CSharpComponent component)
    {
        var inputs = component.Params.Input.OfType<ScriptVariableParam>().ToArray();
        var outputs = component.Params.Output.OfType<ScriptVariableParam>().ToArray();
        if (!component.UsingScriptInputParam || component.Params.Input.OfType<BaseScriptParam>().Count() != 1)
            throw new InvalidOperationException("Enable the special script input and link StoneSurfaceBake.cs with Input is Path.");
        if (!inputs.Select(p => p.VariableName).SequenceEqual(new[] { "x", "y" }) ||
            !outputs.Select(p => p.VariableName).SequenceEqual(new[] { "a" }) ||
            component.UsingLibraryInputParam || component.UsingScriptOutputParam)
            throw new InvalidOperationException("For automatic setup, link the file to a fresh C# component with its default x/y and out/a ports.");
        if (inputs.Any(p => p.SourceCount > 0 || p.PersistentDataCount > 0) || component.Params.Output.Any(p => p.Recipients.Count > 0))
            throw new InvalidOperationException("Use a fresh component. Setup will not discard existing input data or wires; only the script input should be connected.");
        var scriptInput = component.Params.Input.OfType<BaseScriptParam>().Single();
        if (scriptInput.VolatileDataCount != 1)
            throw new InvalidOperationException("Link exactly one StoneSurfaceBake.cs file to the script input.");
    }

    private static void ScheduleSetup(CSharpComponent component)
    {
        ValidateFreshComponent(component);
        var document = component.OnPingDocument();
        if (document == null) throw new InvalidOperationException("The component is not in a Grasshopper document.");
        if (!PendingSetup.Add(component.InstanceGuid)) return;
        document.ScheduleSolution(10, scheduledDocument =>
        {
            bool started = false;
            try
            {
                if (component.OnPingDocument() != scheduledDocument) return;
                if (HasConfiguredNames(component)) return;
                ValidateFreshComponent(component);
                component.RecordUndoEvent("Configure linked Stone Surface Bake");
                started = true;
                Configure(component);
                component.ExpireSolution(false);
            }
            catch (Exception error)
            {
                if (started) component.Locked = true;
                component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Setup failed: " + error.Message +
                    (started ? " The component is disabled. Use a fresh component to retry." : ""));
            }
            finally { PendingSetup.Remove(component.InstanceGuid); }
        });
    }

    private static void Configure(CSharpComponent component)
    {
        // Preserve the exact special input, its URI/path setting, and all its source wires.
        // Never call SetSource, replace script data, or toggle UsingScriptInputParam here.
        component.UsingStandardOutputParam = false;
        foreach (var parameter in component.Params.Input.OfType<ScriptVariableParam>().ToArray())
            component.Params.UnregisterInputParameter(parameter, true);
        foreach (var parameter in component.Params.Output.OfType<ScriptVariableParam>().ToArray())
            component.Params.UnregisterOutputParameter(parameter, true);

        AddInput(component, "FinBaseZ", "Fin base offset in world Z, in inches. Positive or negative.", typeof(double), new GH_Number(0));
        AddInput(component, "FinHeight", "Fin height in world +Z, in inches. Must be positive.", typeof(double), new GH_Number(5));
        AddInput(component, "FlatBaseZ", "Flat-stone base offset in world Z, in inches. Positive or negative.", typeof(double), new GH_Number(0));
        AddInput(component, "FlatHeight", "Flat-stone height in world +Z, in inches. Must be positive.", typeof(double), new GH_Number(5));
        AddInput(component, "Bake", "False previews. True creates or updates WF blocks in the active Rhino document.", typeof(bool), new GH_Boolean(false));
        AddOutput(component, "Fins", "Fin surface preview geometry.");
        AddOutput(component, "Flats", "Flat-stone surface preview geometry.");
        AddOutput(component, "BakedIds", "Rhino block instance IDs from the bake.");
        AddOutput(component, "Report", "Target document, counts, and errors.");
        component.VariableParameterMaintenance();
        component.Params.OnParametersChanged();
        component.Name = "Stone Surface Bake";
        component.NickName = "Stone Surface Bake";
        component.Description = "Linked stone bake script. STN-MET-GA- assemblies; all numbers are inches.";
        component.Tooltip = component.Description;
        component.Message = "STN-MET-GA- | inches";
        component.Attributes.ExpireLayout();
    }

    private static void AddInput(CSharpComponent component, string name, string description, Type type, IGH_Goo value)
    {
        var parameter = new ScriptVariableParam(name)
        {
            PrettyName = name, ToolTip = description, Optional = true,
            Access = GH_ParamAccess.item, AllowTreeAccess = false
        };
        if (parameter.TypeHints.Select(type) == null)
            throw new InvalidOperationException("Cannot select the " + type.Name + " input hint in this Rhino installation.");
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

    private static int InputIndex(CSharpComponent component, string name)
    {
        int index = component.Params.Input.FindIndex(p => p is ScriptVariableParam variable && variable.VariableName == name);
        if (index < 0) throw new InvalidOperationException("Missing input " + name + ".");
        return index;
    }

    private static double ReadNumber(CSharpComponent component, IGH_DataAccess data, string name, double fallback)
    {
        double value = fallback;
        int index = InputIndex(component, name);
        if (!data.GetData(index, ref value) && component.Params.Input[index].VolatileDataCount > 0)
            throw new InvalidOperationException(name + " must be a number.");
        return value;
    }

    private static void WriteResults(CSharpComponent component, IGH_DataAccess data, StoneSurfaceBake.Result result)
    {
        // Script-Mode's implicit output globals stay null. Rhino 8 skips unset/null
        // globals during its output pass, preserving these direct data-access results.
        data.SetDataList(component.Params.Output.FindIndex(p => p.NickName == "Fins"), result.Fins);
        data.SetDataList(component.Params.Output.FindIndex(p => p.NickName == "Flats"), result.Flats);
        data.SetDataList(component.Params.Output.FindIndex(p => p.NickName == "BakedIds"), result.BakedIds);
        data.SetData(component.Params.Output.FindIndex(p => p.NickName == "Report"), result.Report);
    }
}

// #! csharp
// LINK THIS FILE through Rhino 8 C# Script's special script input (Input is Path).
// A fresh component's x/y ports configure themselves on the first solve.
// The file connection stays in place. No code needs to be pasted into the editor.
// Inputs (Item): FinBaseZ=0, FinHeight=5, FlatBaseZ=0, FlatHeight=5, Bake=false.
// Outputs: Fins, Flats, BakedIds, Report. All numeric values are inches.
#r "C:/Program Files/Rhino 8/Plug-ins/Grasshopper/Components/RhinoCodePluginGH.gha"
#r "C:/Program Files/Rhino 8/System/RhinoCodePlatform.Rhino3D.dll"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using RhinoCodePluginGH.Components;
using RhinoCodePluginGH.Parameters;

// ghenv is supplied by Rhino's Script-Mode, even before our named ports exist.
// DataAccessManager avoids referencing missing FinBaseZ/Fins/etc. globals during setup.
LiveStoneSurfaceComponent.Run(ghenv.Component as CSharpComponent, ghenv.DataAccessManager);

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

// Standalone RhinoCommon implementation. The delivery script includes this file verbatim.
// No MCP, LLM, file access, external packages, or Rhino commands are required.
public static class StoneSurfaceBake
{
    public const string FinLayer = "01_MET-IEF-Curtain Wall::Surfaces - STN detailed::Fin";
    public const string FlatLayer = "01_MET-IEF-Curtain Wall::Surfaces - STN detailed::Back";
    public const string SourceKey = "WF_Bake_Source";
    public const string OwnerKey = "WF_Bake_Tool";
    public const string Owner = "GrasshopperStoneSurfaceBake/v1";

    public sealed class Settings
    {
        public double FinBaseZ, FinHeight, FlatBaseZ, FlatHeight;
        public Settings(double finBaseZ, double finHeight, double flatBaseZ, double flatHeight)
        {
            FinBaseZ = finBaseZ; FinHeight = finHeight; FlatBaseZ = flatBaseZ; FlatHeight = flatHeight;
            if (!Finite(finBaseZ) || !Finite(flatBaseZ)) throw new ArgumentException("Base Z offsets must be finite numbers (inches).");
            if (!Finite(finHeight) || !Finite(flatHeight) || finHeight <= 0 || flatHeight <= 0)
                throw new ArgumentException("FinHeight and FlatHeight must be positive numbers (inches).");
        }
    }

    public sealed class Result
    {
        public readonly List<Brep> Fins = new List<Brep>();
        public readonly List<Brep> Flats = new List<Brep>();
        public readonly List<string> BlockNames = new List<string>();
        public readonly List<string> AssemblyIDs = new List<string>();
        public readonly List<Guid> BakedIds = new List<Guid>();
        public string Report = "";
    }

    private sealed class DefinitionPlan
    {
        public InstanceDefinition Source, Existing;
        public Line Edge;
        public string Name, Layer;
        public bool Fin, Change;
        public double Height;
        public int LayerIndex = -1;
        public ObjectAttributes ExistingAttributes;
        public NurbsSurface Surface;
    }

    private sealed class Placement
    {
        public Guid ParentId;
        public string Assembly, Key;
        public InstanceDefinition Stone;
        public Transform SourceTransform, BaseTransform, Desired;
        public DefinitionPlan Definition;
        public InstanceObject Existing;
        public Guid ExistingId;
        public bool Change;
    }

    // GH normally solves on Rhino's UI thread. This also supports a threaded caller.
    public static Result Run(Settings settings, bool bake)
    {
        Result result = null;
        Action work = () => result = Evaluate(RhinoDoc.ActiveDoc, settings, bake);
        if (RhinoApp.InvokeRequired) RhinoApp.InvokeOnUiThread(work);
        else work();
        return result;
    }

    public static bool IsAssembly(string name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && name.StartsWith("STN-MET-GA-", StringComparison.OrdinalIgnoreCase)
            && !name.EndsWith("_EL", StringComparison.OrdinalIgnoreCase)
            && !name.EndsWith("_V", StringComparison.OrdinalIgnoreCase);
    }

    public static int StoneFamily(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.EndsWith("_EL", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("_V", StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.StartsWith("STN-MET-1", StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.StartsWith("STN-MET-2", StringComparison.OrdinalIgnoreCase)) return 2;
        return 0;
    }

    public static string OccurrenceKey(Guid assemblyId, IEnumerable<Guid> path)
    {
        return assemblyId.ToString("D") + "/" + string.Join("/", path.Select(id => id.ToString("D")));
    }

    public static Transform PlacementTransform(Line edge, Transform source, double baseZ, double tolerance)
    {
        if (!Finite(baseZ) || !source.IsValid || source.M30 != 0 || source.M31 != 0 || source.M32 != 0 || source.M33 != 1
            || !edge.IsValid || edge.Length <= tolerance) throw new InvalidOperationException("Invalid stone edge or affine transform.");
        Point3d start = edge.From, end = edge.To;
        start.Transform(source); end.Transform(source);
        if (!start.IsValid || !end.IsValid) throw new InvalidOperationException("Invalid world edge endpoints.");
        Vector3d delta = end - start;
        double horizontal = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        if (!Finite(horizontal) || horizontal <= tolerance)
            throw new InvalidOperationException("A placed edge is parallel to world Z; a surface would be degenerate.");
        Transform value = Transform.Identity;
        value.M00 = delta.X / edge.Length; value.M10 = delta.Y / edge.Length; value.M20 = delta.Z / edge.Length;
        value.M01 = -delta.Y / horizontal; value.M11 = delta.X / horizontal; value.M21 = 0;
        value.M02 = 0; value.M12 = 0; value.M22 = 1;
        value.M03 = start.X; value.M13 = start.Y; value.M23 = start.Z + baseZ;
        if (!value.IsValid) throw new InvalidOperationException("The base Z offset produced an invalid placement.");
        return value;
    }

    public static bool SameTransform(Transform a, Transform b, double tolerance)
    {
        for (int row = 0; row < 4; row++)
        for (int column = 0; column < 4; column++)
        {
            double epsilon = column == 3 && row < 3 ? tolerance : 1e-9;
            if (!Finite(a[row, column]) || !Finite(b[row, column])
                || Math.Abs(a[row, column] - b[row, column]) > epsilon) return false;
        }
        return true;
    }

    public static bool Finite(double number) { return !double.IsNaN(number) && !double.IsInfinity(number); }

    public static bool LegacyMatches(string name, string assembly, string sourceKey, string owner,
        Transform actual, string expectedName, string expectedAssembly, Transform original, Transform desired, double tolerance)
    {
        return string.IsNullOrEmpty(sourceKey) && string.IsNullOrEmpty(owner)
            && string.Equals(name, expectedName, StringComparison.Ordinal) && assembly == expectedAssembly
            && (SameTransform(actual, original, tolerance) || SameTransform(actual, desired, tolerance));
    }

    public static bool CanChangeSharedDefinition(int nestedCount, IEnumerable<Guid> references, ISet<Guid> matched)
    {
        return nestedCount == 0 && references.All(id => matched.Contains(id));
    }

    private static Result Evaluate(RhinoDoc doc, Settings settings, bool bake)
    {
        if (doc == null) throw new InvalidOperationException("Open the target Rhino document before solving this component.");
        if (settings == null) throw new ArgumentNullException("settings");
        double scale = InchesToModel(doc.ModelUnitSystem);
        double finOffset = settings.FinBaseZ * scale, flatOffset = settings.FlatBaseZ * scale;
        double finHeight = settings.FinHeight * scale, flatHeight = settings.FlatHeight * scale;
        double tolerance = Math.Max(doc.ModelAbsoluteTolerance, 1e-7);
        if (!Finite(finOffset) || !Finite(flatOffset) || !Finite(finHeight) || !Finite(flatHeight)
            || finHeight <= tolerance || flatHeight <= tolerance)
            throw new InvalidOperationException("Converted values must be finite and both surface heights must exceed model tolerance.");

        var allInstances = doc.Objects.GetObjectList(new ObjectEnumeratorSettings {
            NormalObjects = true, HiddenObjects = true, LockedObjects = true,
            ActiveObjects = true, ReferenceObjects = false, IdefObjects = false, DeletedObjects = false,
            ObjectTypeFilter = ObjectType.InstanceReference
        }).OfType<InstanceObject>().Where(o => !o.IsDeleted && !o.Attributes.IsInstanceDefinitionObject
            && o.InstanceDefinition != null).OrderBy(o => o.Id).ToList();
        var assemblies = allInstances.Where(o => IsAssembly(o.InstanceDefinition.Name)).ToList();
        var placements = new List<Placement>();
        foreach (var assembly in assemblies)
            Traverse(doc, assembly.InstanceDefinition, assembly.InstanceXform, assembly.Id, assembly.InstanceDefinition.Name,
                new List<Guid>(), new HashSet<Guid>(), placements, 0);
        var result = new Result();
        if (placements.Count == 0)
        {
            result.Report = "No nested STN-MET-1/2 stones found in eligible STN-MET-GA- assemblies in " + doc.Name + ".";
            return result;
        }
        var definitions = new List<DefinitionPlan>();
        try
        {
            foreach (var group in placements.GroupBy(p => p.Stone.Id))
            {
                var first = group.First();
                bool fin = StoneFamily(first.Stone.Name) == 2;
                var definition = new DefinitionPlan {
                    Source = first.Stone, Name = "WF_" + first.Stone.Name, Edge = SelectEdge(first.Stone, tolerance),
                    Fin = fin, Height = fin ? finHeight : flatHeight, Layer = fin ? FinLayer : FlatLayer
                };
                definition.Existing = doc.InstanceDefinitions.Find(definition.Name);
                definition.LayerIndex = doc.Layers.FindByFullPath(definition.Layer, -1);
                CheckLayerPath(doc, definition.Layer);
                definition.Surface = CreateSurface(definition.Edge.Length, definition.Height);
                definitions.Add(definition);
                if (definition.Existing != null)
                {
                    if (definition.Existing.IsReference || definition.Existing.UpdateType != InstanceDefinitionUpdateType.Static
                        || !string.IsNullOrEmpty(definition.Existing.SourceArchive))
                        throw new InvalidOperationException("Cannot modify a linked/reference WF definition: " + definition.Name);
                    RhinoObject[] members = definition.Existing.GetObjects();
                    double oldHeight;
                    if (members.Length != 1 || !TryCanonicalSurface(members[0].Geometry, definition.Edge.Length, tolerance, out oldHeight))
                        throw new InvalidOperationException("Existing " + definition.Name + " is not this workflow's single rectangular surface. It was left unchanged.");
                    definition.ExistingAttributes = members[0].Attributes.Duplicate();
                    definition.Change = Math.Abs(oldHeight - definition.Height) > tolerance
                        || !MemberAttributesMatch(members[0].Attributes, definition.Name, definition.LayerIndex);
                }
                else definition.Change = true;
                foreach (var placement in group)
                {
                    placement.Definition = definition;
                    placement.BaseTransform = PlacementTransform(definition.Edge, placement.SourceTransform, 0, tolerance);
                    placement.Desired = PlacementTransform(definition.Edge, placement.SourceTransform, fin ? finOffset : flatOffset, tolerance);
                }
            }

            var outputs = allInstances.Where(o => o.InstanceDefinition.Name.StartsWith("WF_", StringComparison.OrdinalIgnoreCase)).ToList();
            var consumed = new HashSet<Guid>();
            foreach (var p in placements)
            {
                var managed = outputs.Where(o => o.Attributes.GetUserString(SourceKey) == p.Key).ToList();
                if (managed.Count > 1) throw new InvalidOperationException("Duplicate WF_Bake_Source identifiers exist for " + p.Definition.Name + ".");
                if (managed.Count == 1)
                {
                    p.Existing = managed[0];
                    if (!string.Equals(p.Existing.InstanceDefinition.Name, p.Definition.Name, StringComparison.Ordinal)
                        || p.Existing.Attributes.GetUserString("Assembly_ID") != p.Assembly
                        || p.Existing.Attributes.GetUserString(OwnerKey) != Owner)
                        throw new InvalidOperationException("Managed output identity conflicts for " + p.Definition.Name + ".");
                }
                else
                {
                    var legacy = outputs.Where(o => !consumed.Contains(o.Id)
                        && LegacyMatches(o.InstanceDefinition.Name, o.Attributes.GetUserString("Assembly_ID"),
                            o.Attributes.GetUserString(SourceKey), o.Attributes.GetUserString(OwnerKey), o.InstanceXform,
                            p.Definition.Name, p.Assembly, p.BaseTransform, p.Desired, tolerance)).ToList();
                    if (legacy.Count > 1) throw new InvalidOperationException("Ambiguous overlapping legacy outputs for " + p.Definition.Name + "; no changes made.");
                    if (legacy.Count == 1) p.Existing = legacy[0];
                }
                if (p.Existing != null && !consumed.Add(p.Existing.Id))
                    throw new InvalidOperationException("One baked block matched more than one source occurrence.");
                p.ExistingId = p.Existing == null ? Guid.Empty : p.Existing.Id;
                p.Change = p.Existing == null || !SameTransform(p.Existing.InstanceXform, p.Desired, tolerance)
                    || !InstanceAttributesMatch(p.Existing.Attributes, p, p.Definition.LayerIndex);
                if (p.Existing != null && (p.Change || p.Definition.Change)) CheckEditable(doc, p.Existing);
            }

            foreach (var definition in definitions.Where(d => d.Existing != null && d.Change))
            {
                definition.Existing.UseCount(out int top, out int nested);
                var references = definition.Existing.GetReferences(0);
                if (!CanChangeSharedDefinition(nested, references.Select(r => r.Id), consumed))
                    throw new InvalidOperationException("Changing " + definition.Name + " would affect an unmatched or nested instance. Resolve those references before baking.");
            }

            foreach (var p in placements)
            {
                Brep preview = p.Definition.Surface.ToBrep();
                if (!preview.Transform(p.Desired)) { preview.Dispose(); throw new InvalidOperationException("Could not transform surface preview."); }
                if (p.Definition.Fin) result.Fins.Add(preview); else result.Flats.Add(preview);
                result.BlockNames.Add(p.Definition.Name);
                result.AssemblyIDs.Add(p.Assembly);
            }
            int creates = placements.Count(p => p.Existing == null);
            int updates = placements.Count(p => p.Existing != null && p.Change);
            int definitionChanges = definitions.Count(d => d.Change);
            int orphans = outputs.Count(o => o.Attributes.GetUserString(OwnerKey) == Owner && !consumed.Contains(o.Id));
            string counts = assemblies.Count + " assemblies; " + result.Fins.Count + " fins; " + result.Flats.Count
                + " flat stones; " + definitions.Count + " WF definitions. ";
            if (bake && (creates > 0 || updates > 0 || definitionChanges > 0))
            {
                Apply(doc, definitions, placements, tolerance);
                result.Report = "Baked in " + doc.Name + ": " + counts + creates + " new instances, "
                    + updates + " updated instances, " + definitionChanges + " created/updated definitions. One Rhino Undo.";
            }
            else result.Report = (bake ? "Already up to date in " : "Preview only in ") + doc.Name + ": " + counts
                + "Pending: " + creates + " new instances, " + updates + " instance updates, " + definitionChanges + " definition changes.";
            foreach (var p in placements) if (p.Existing != null) result.BakedIds.Add(p.Existing.Id);
            result.Report += " Inputs are inches; base values are relative world-Z offsets. Source assemblies preserved.";
            if (orphans > 0) result.Report += " " + orphans + " unmatched managed outputs were retained (no deletion).";
            return result;
        }
        catch
        {
            foreach (var geometry in result.Fins.Concat(result.Flats)) geometry.Dispose();
            throw;
        }
        finally
        {
            foreach (var definition in definitions) if (definition.Surface != null) definition.Surface.Dispose();
        }
    }

    private static double InchesToModel(UnitSystem units)
    {
        if (!Enum.IsDefined(typeof(UnitSystem), units) || units == UnitSystem.None || units == UnitSystem.Unset || units == UnitSystem.CustomUnits)
            throw new InvalidOperationException("Use standard Rhino document units so inch inputs can be converted reliably.");
        double scale = units == UnitSystem.Inches ? 1 : RhinoMath.UnitScale(UnitSystem.Inches, units);
        if (!Finite(scale) || scale <= 0) throw new InvalidOperationException("Could not convert inches to model units.");
        return scale;
    }

    private static void Traverse(RhinoDoc doc, InstanceDefinition definition, Transform xform, Guid parentId,
        string assembly, List<Guid> path, HashSet<Guid> stack, List<Placement> result, int depth)
    {
        if (depth >= 24 || !stack.Add(definition.Id)) throw new InvalidOperationException("Cyclic or excessively deep assembly: " + assembly);
        foreach (var member in definition.GetObjects())
        {
            var reference = member.Geometry as InstanceReferenceGeometry;
            if (reference == null) continue;
            var nested = doc.InstanceDefinitions.Find(reference.ParentIdefId, true);
            if (nested == null || nested.IsDeleted) throw new InvalidOperationException("Missing nested block in " + assembly);
            path.Add(member.Id);
            Transform nestedTransform = xform * reference.Xform;
            if (StoneFamily(nested.Name) != 0)
                result.Add(new Placement { ParentId = parentId, Assembly = assembly, Stone = nested,
                    SourceTransform = nestedTransform, Key = OccurrenceKey(parentId, path) });
            else Traverse(doc, nested, nestedTransform, parentId, assembly, path, stack, result, depth + 1);
            path.RemoveAt(path.Count - 1);
        }
        stack.Remove(definition.Id);
    }

    private static Line SelectEdge(InstanceDefinition stone, double tolerance)
    {
        var candidates = new List<Line>();
        foreach (var member in stone.GetObjects())
        {
            var curve = member.Geometry as Curve;
            if (curve == null) continue;
            if (curve.IsLinear(tolerance)) AddCandidate(curve, tolerance, candidates);
            else
            {
                var segments = curve.DuplicateSegments();
                if (segments == null) continue;
                foreach (var segment in segments)
                    using (segment) if (segment.IsLinear(tolerance)) AddCandidate(segment, tolerance, candidates);
            }
        }
        if (candidates.Count == 0) throw new InvalidOperationException("No horizontal upper stone edge found in " + stone.Name);
        double top = candidates.Max(l => (l.From.Y + l.To.Y) / 2);
        var upper = candidates.Where(l => Math.Abs((l.From.Y + l.To.Y) / 2 - top) <= tolerance).ToList();
        double longest = upper.Max(l => l.Length);
        var tied = upper.Where(l => Math.Abs(l.Length - longest) <= tolerance).OrderBy(l => l.From.X).ThenBy(l => l.From.Z).ToList();
        Line selected = tied[0];
        if (tied.Any(l => l.From.DistanceTo(selected.From) > tolerance || l.To.DistanceTo(selected.To) > tolerance))
            throw new InvalidOperationException("Ambiguous upper horizontal edges in " + stone.Name + "; no changes made.");
        return selected;
    }

    private static void AddCandidate(Curve curve, double tolerance, List<Line> candidates)
    {
        Point3d a = curve.PointAtStart, b = curve.PointAtEnd;
        if (Math.Abs(a.Y - b.Y) > tolerance || Math.Abs(a.Z - b.Z) > tolerance || Math.Abs(a.X - b.X) <= tolerance) return;
        candidates.Add(a.X <= b.X ? new Line(a, b) : new Line(b, a));
    }

    private static NurbsSurface CreateSurface(double length, double height)
    {
        var value = NurbsSurface.CreateFromCorners(Point3d.Origin, new Point3d(length, 0, 0),
            new Point3d(length, 0, height), new Point3d(0, 0, height));
        if (value == null || !value.IsValid) { if (value != null) value.Dispose(); throw new InvalidOperationException("Could not create valid stone surface."); }
        return value;
    }

    private static bool TryCanonicalSurface(GeometryBase geometry, double length, double tolerance, out double height)
    {
        height = 0;
        Surface surface = geometry as Surface;
        var brep = geometry as Brep;
        if (brep != null)
        {
            if (brep.Faces.Count != 1 || !brep.Faces[0].IsSurface) return false;
            surface = brep.Faces[0];
        }
        if (surface == null || !surface.IsValid) return false;
        using (var nurbs = surface.ToNurbsSurface())
        {
            if (nurbs == null || nurbs.OrderU != 2 || nurbs.OrderV != 2 || nurbs.Points.CountU != 2 || nurbs.Points.CountV != 2) return false;
            var points = new List<Point3d>();
            for (int u = 0; u < 2; u++) for (int v = 0; v < 2; v++)
            {
                var point = nurbs.Points.GetControlPoint(u, v);
                if (Math.Abs(point.Weight - 1) > 1e-12) return false;
                points.Add(point.Location);
            }
            height = points.Max(p => p.Z);
            if (height <= tolerance) return false;
            var a = points[0]; var b = points[1]; var c = points[2]; var d = points[3];
            if ((a + (b - a) + (c - a)).DistanceTo(d) > tolerance) return false;
            foreach (var expected in new[] { Point3d.Origin, new Point3d(length, 0, 0), new Point3d(length, 0, height), new Point3d(0, 0, height) })
            {
                int index = points.FindIndex(p => p.DistanceTo(expected) <= tolerance);
                if (index < 0) return false;
                points.RemoveAt(index);
            }
        }
        return true;
    }

    private static bool MemberAttributesMatch(ObjectAttributes attributes, string name, int layer)
    {
        return layer >= 0 && attributes.Name == name && attributes.LayerIndex == layer
            && attributes.ColorSource == ObjectColorSource.ColorFromLayer;
    }

    private static bool InstanceAttributesMatch(ObjectAttributes attributes, Placement p, int layer)
    {
        return MemberAttributesMatch(attributes, p.Definition.Name, layer)
            && attributes.GetUserString("Assembly_ID") == p.Assembly
            && attributes.GetUserString(SourceKey) == p.Key && attributes.GetUserString(OwnerKey) == Owner;
    }

    private static void CheckEditable(RhinoDoc doc, InstanceObject obj)
    {
        if (obj.IsReference || obj.IsLocked) throw new InvalidOperationException("An output block is locked or referenced: " + obj.Id);
        var layer = doc.Layers[obj.Attributes.LayerIndex];
        if (layer != null) CheckLayerPath(doc, layer.FullPath);
    }

    private static void CheckLayerPath(RhinoDoc doc, string path)
    {
        string current = "";
        foreach (var part in path.Split(new[] { "::" }, StringSplitOptions.None))
        {
            current = current.Length == 0 ? part : current + "::" + part;
            int index = doc.Layers.FindByFullPath(current, -1);
            if (index >= 0 && (doc.Layers[index].IsLocked || doc.Layers[index].IsReference))
                throw new InvalidOperationException("Target/output layer is locked or referenced: " + current);
        }
    }

    private static int EnsureLayer(RhinoDoc doc, string path, ref bool mutated)
    {
        Guid parent = Guid.Empty;
        string current = "";
        int index = -1;
        foreach (var part in path.Split(new[] { "::" }, StringSplitOptions.None))
        {
            current = current.Length == 0 ? part : current + "::" + part;
            index = doc.Layers.FindByFullPath(current, -1);
            if (index < 0)
            {
                index = doc.Layers.Add(new Layer { Name = part, ParentLayerId = parent });
                if (index < 0) throw new InvalidOperationException("Could not create layer: " + current);
                mutated = true;
            }
            parent = doc.Layers[index].Id;
        }
        return index;
    }

    private static void Apply(RhinoDoc doc, List<DefinitionPlan> definitions, List<Placement> placements, double tolerance)
    {
        uint undo = doc.BeginUndoRecord("GH: Stone surface blocks");
        if (undo == 0) throw new InvalidOperationException("Bake needs Rhino Undo enabled and no running Rhino command.");
        bool mutated = false;
        Exception failure = null;
        try
        {
            foreach (var d in definitions)
            {
                d.LayerIndex = EnsureLayer(doc, d.Layer, ref mutated);
                if (!d.Change) continue;
                ObjectAttributes attributes = d.ExistingAttributes == null ? new ObjectAttributes() : d.ExistingAttributes.Duplicate();
                attributes.Name = d.Name; attributes.LayerIndex = d.LayerIndex; attributes.ColorSource = ObjectColorSource.ColorFromLayer;
                if (d.Existing == null)
                {
                    int index = doc.InstanceDefinitions.Add(d.Name, "Stone world +Z surface from " + d.Source.Name,
                        Point3d.Origin, new GeometryBase[] { d.Surface }, new[] { attributes });
                    if (index < 0) throw new InvalidOperationException("Could not create " + d.Name);
                    d.Existing = doc.InstanceDefinitions[index];
                }
                else if (!doc.InstanceDefinitions.ModifyGeometry(d.Existing.Index, new GeometryBase[] { d.Surface }, new[] { attributes }))
                    throw new InvalidOperationException("Could not update " + d.Name);
                mutated = true;
            }
            foreach (var p in placements)
            {
                // Definition replacement can invalidate RhinoObject wrappers; resolve fresh objects.
                if (p.ExistingId != Guid.Empty)
                {
                    p.Existing = doc.Objects.FindId(p.ExistingId) as InstanceObject;
                    if (p.Existing == null) throw new InvalidOperationException("An existing output changed during the bake.");
                }
                if (!p.Change) continue;
                ObjectAttributes attributes = p.Existing == null ? new ObjectAttributes() : p.Existing.Attributes.Duplicate();
                attributes.Name = p.Definition.Name; attributes.LayerIndex = p.Definition.LayerIndex;
                attributes.ColorSource = ObjectColorSource.ColorFromLayer;
                attributes.SetUserString("Assembly_ID", p.Assembly);
                attributes.SetUserString(SourceKey, p.Key); attributes.SetUserString(OwnerKey, Owner);
                if (p.Existing != null && SameTransform(p.Existing.InstanceXform, p.Desired, tolerance))
                {
                    if (!doc.Objects.ModifyAttributes(p.Existing.Id, attributes, true)) throw new InvalidOperationException("Could not update block attributes.");
                    mutated = true;
                    p.Existing = doc.Objects.FindId(p.Existing.Id) as InstanceObject;
                }
                else
                {
                    Guid id = doc.Objects.AddInstanceObject(p.Definition.Existing.Index, p.Desired, attributes);
                    if (id == Guid.Empty) throw new InvalidOperationException("Could not place " + p.Definition.Name);
                    mutated = true;
                    if (p.Existing != null && !doc.Objects.Delete(p.Existing.Id, true))
                        throw new InvalidOperationException("Could not replace the previous managed block.");
                    p.Existing = doc.Objects.FindId(id) as InstanceObject;
                }
                if (p.Existing == null) throw new InvalidOperationException("Baked block could not be read back.");
            }
        }
        catch (Exception ex) { failure = ex; }
        finally { doc.EndUndoRecord(undo); }
        if (failure != null)
        {
            // Undo only when this record actually contains our writes; never undo prior user work.
            bool restored = !mutated || doc.Undo();
            doc.Views.Redraw();
            throw new InvalidOperationException(failure.Message + (restored ? " Bake changes rolled back." : " Rollback failed; use Rhino Undo to inspect the failed bake."), failure);
        }
        doc.Views.Redraw();
    }
}

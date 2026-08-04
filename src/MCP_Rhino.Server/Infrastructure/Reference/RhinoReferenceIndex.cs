using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Infrastructure.Reference;

public static class RhinoReferenceIndex
{
    public const string Source = "Curated MCP_Rhino internal reference; no external Rhino documentation text copied.";
    public const string RhinoVersion = "Rhino 8 / RhinoCommon concepts used by MCP_Rhino";

    private static readonly IReadOnlyList<RhinoReferenceModuleEntry> Modules = new List<RhinoReferenceModuleEntry>
    {
        new(
            "document",
            "Live document access, undo records, layers, and view redraw boundaries.",
            new[]
            {
                Function("RhinoDoc.ActiveDoc", "Resolve the current Rhino document in the global debug pipe path.", "RhinoDoc? RhinoDoc.ActiveDoc", new[]
                {
                    "MCP_Rhino wraps all live access through ILiveRhinoDocumentAccessor.",
                    "Do not fall back to disk .3dm snapshots after live resolution fails."
                }, "document", "live", "active"),
                Function("RhinoDoc.BeginUndoRecord", "Start one Rhino undo record around a mutation tool call.", "uint doc.BeginUndoRecord(string description)", new[]
                {
                    "Every apply path that mutates Rhino state should pair this with EndUndoRecord.",
                    "Read-only preview paths must not open undo records."
                }, "undo", "mutation"),
                Function("RhinoDoc.Views.Redraw", "Refresh Rhino views after successful document mutation.", "doc.Views.Redraw()", new[]
                {
                    "Call after object, layer, selection, or viewport-visible state changes."
                }, "view", "redraw")
            }),
        new(
            "objects",
            "Object lookup, creation, replacement, deletion, and attribute assignment.",
            new[]
            {
                Function("RhinoDoc.Objects.FindId", "Resolve a live object by confirmed Guid.", "RhinoObject? doc.Objects.FindId(Guid objectId)", new[]
                {
                    "MCP_Rhino runtime tools should use confirmed object ids rather than loose name matching for mutations."
                }, "objects", "lookup"),
                Function("RhinoDoc.Objects.Add", "Add prepared Rhino geometry with object attributes.", "Guid doc.Objects.Add(GeometryBase geometry, ObjectAttributes attributes)", new[]
                {
                    "Creation-only tools are mutation closed-world and normally non-destructive.",
                    "Return object ids and layer paths to the caller."
                }, "objects", "create"),
                Function("RhinoDoc.Objects.Replace", "Replace object geometry while preserving or replaying metadata as needed.", "bool doc.Objects.Replace(Guid objectId, GeometryBase geometry, bool ignoreModes)", new[]
                {
                    "Replacement tools are destructive because existing geometry is overwritten."
                }, "objects", "replace", "destructive"),
                Function("RhinoDoc.Objects.Delete", "Delete an object from the live document.", "bool doc.Objects.Delete(Guid objectId, bool quiet)", new[]
                {
                    "Delete and destructive replace tools must be marked Destructive = true."
                }, "objects", "delete", "destructive")
            }),
        new(
            "layers",
            "Layer lookup and layer-path behavior used by creation and edit tools.",
            new[]
            {
                Function("LayerTable.FindByFullPath", "Find an existing layer by full path.", "int doc.Layers.FindByFullPath(string fullPath, int notFoundReturnValue)", new[]
                {
                    "Generic geometry creation requires the target layer to exist.",
                    "Some higher-level architectural workflows may explicitly auto-create layers."
                }, "layers", "lookup"),
                Function("LayerTable.AddPath", "Create a nested layer path when a workflow explicitly allows it.", "int doc.Layers.AddPath(string fullPath)", new[]
                {
                    "Use only in tools whose contract includes auto-create layer behavior."
                }, "layers", "create")
            }),
        new(
            "geometry-creation",
            "RhinoCommon geometry constructors and factory methods used by MCP_Rhino creation tools.",
            new[]
            {
                Function("Circle", "Construct circle curves from a plane and radius.", "new Circle(Plane plane, double radius)", new[]
                {
                    "MCP_Rhino exposes this through CreateCircles."
                }, "curve", "circle"),
                Function("Ellipse.ToNurbsCurve", "Create an ellipse and convert it to a curve object.", "new Ellipse(Plane plane, double radiusX, double radiusY).ToNurbsCurve()", new[]
                {
                    "MCP_Rhino exposes this through CreateEllipses."
                }, "curve", "ellipse"),
                Function("NurbsCurve.Create", "Create a control-point NURBS curve.", "NurbsCurve.Create(bool periodic, int degree, IEnumerable<Point3d> points)", new[]
                {
                    "MCP_Rhino validates degree and control point count before creation."
                }, "curve", "nurbs"),
                Function("Brep.CreateFromLoft", "Create loft Breps from ordered curves.", "Brep.CreateFromLoft(IEnumerable<Curve> curves, Point3d start, Point3d end, LoftType type, bool closed)", new[]
                {
                    "MCP_Rhino exposes this through CreateLofts in Geometry/CurveOps."
                }, "brep", "loft"),
                Function("Brep.CreatePipe", "Create pipe Breps along a rail curve.", "Brep.CreatePipe(Curve rail, double radius, bool localBlending, PipeCapMode cap, bool fitRail, double tolerance, double angleToleranceRadians)", new[]
                {
                    "MCP_Rhino exposes this through CreatePipes."
                }, "brep", "pipe")
            }),
        new(
            "curve-operations",
            "Common curve-derived operations exposed as atomic MCP_Rhino tools.",
            new[]
            {
                Function("Curve.Offset", "Create offset curves in a supplied plane.", "curve.Offset(Plane plane, double distance, double tolerance, CurveOffsetCornerStyle cornerStyle)", new[]
                {
                    "MCP_Rhino exposes this through CreateCurveOffsets."
                }, "curve", "offset"),
                Function("Curve.ProjectToBrep", "Project a curve onto Brep targets along a direction.", "Curve.ProjectToBrep(Curve curve, IEnumerable<Brep> breps, Vector3d direction, double tolerance)", new[]
                {
                    "MCP_Rhino exposes this through ProjectCurves."
                }, "curve", "projection"),
                Function("Curve.Split", "Split a curve at resolved parameter values.", "curve.Split(IEnumerable<double> parameters)", new[]
                {
                    "PreviewSplitCurves resolves parameters without mutation.",
                    "ReplaceSplitCurves is destructive because it deletes source curves."
                }, "curve", "split")
            }),
        new(
            "selection-and-viewport",
            "Read and mutate Rhino UI selection state or capture viewports through explicit tools.",
            new[]
            {
                Function("RhinoObject.Select", "Set object selection state in the live document.", "rhinoObject.Select(bool select)", new[]
                {
                    "MCP_Rhino wraps selection mutation in SelectObjectsInLive."
                }, "selection", "ui"),
                Function("ViewCapture", "Capture a viewport image as a bitmap payload.", "ViewCapture.CaptureToBitmap(ViewCaptureSettings settings)", new[]
                {
                    "MCP_Rhino wraps viewport capture in CaptureViewportImage and uses settings-based capture for Rhino 8 runtime compatibility."
                }, "viewport", "capture")
            })
    };

    public static RhinoReferenceModuleListResponse ListModules(string? query = null, int limit = 20)
    {
        IEnumerable<RhinoReferenceModuleEntry> matches = Modules;
        if (!string.IsNullOrWhiteSpace(query))
        {
            string normalized = query.Trim();
            matches = matches.Where(module =>
                module.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || module.Summary.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || module.Functions.Any(function => Matches(function, normalized)));
        }

        List<RhinoReferenceModuleEntry> modules = matches.Take(ClampLimit(limit)).ToList();
        return new RhinoReferenceModuleListResponse
        {
            Source = Source,
            RhinoVersion = RhinoVersion,
            ModuleCount = modules.Count,
            Modules = modules.Select(module => new RhinoReferenceModuleResponse
            {
                Name = module.Name,
                Summary = module.Summary,
                FunctionNames = module.Functions.Select(function => function.FunctionName).ToList()
            }).ToList()
        };
    }

    public static RhinoReferenceSearchResponse Search(string query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new RhinoReferenceSearchResponse
            {
                Query = string.Empty,
                Source = Source,
                ResultCount = 0
            };
        }

        string normalized = query.Trim();
        List<RhinoReferenceFunctionEntry> matches = Modules
            .SelectMany(module => module.Functions.Select(function => function with { ModuleName = module.Name }))
            .Where(function => Matches(function, normalized))
            .Take(ClampLimit(limit))
            .ToList();

        return new RhinoReferenceSearchResponse
        {
            Query = normalized,
            Source = Source,
            ResultCount = matches.Count,
            Results = matches.Select(ToSummary).ToList()
        };
    }

    public static RhinoReferenceFunctionDetailResponse? GetFunction(string functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName))
        {
            return null;
        }

        string normalized = functionName.Trim();
        foreach (RhinoReferenceModuleEntry module in Modules)
        {
            RhinoReferenceFunctionEntry? function = module.Functions.FirstOrDefault(item =>
                string.Equals(item.FunctionName, normalized, StringComparison.OrdinalIgnoreCase));
            if (function is null)
            {
                continue;
            }

            return new RhinoReferenceFunctionDetailResponse
            {
                Source = Source,
                RhinoVersion = RhinoVersion,
                ModuleName = module.Name,
                FunctionName = function.FunctionName,
                Summary = function.Summary,
                Signature = function.Signature,
                Notes = function.Notes,
                Tags = function.Tags
            };
        }

        return null;
    }

    public static string FormatModulesMarkdown(string? query = null, int limit = 20)
    {
        RhinoReferenceModuleListResponse modules = ListModules(query, limit);
        var lines = new List<string>
        {
            "# Rhino Reference Modules",
            string.Empty,
            $"Source: {modules.Source}",
            $"Version scope: {modules.RhinoVersion}",
            string.Empty
        };

        foreach (RhinoReferenceModuleResponse module in modules.Modules)
        {
            lines.Add($"## {module.Name}");
            lines.Add(module.Summary);
            lines.Add(string.Empty);
            lines.Add("Functions:");
            lines.AddRange(module.FunctionNames.Select(name => $"- {name}"));
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatModuleMarkdown(string moduleName)
    {
        RhinoReferenceModuleEntry? module = Modules.FirstOrDefault(item =>
            string.Equals(item.Name, moduleName, StringComparison.OrdinalIgnoreCase));
        if (module is null)
        {
            return $"# Rhino Reference Module Not Found{Environment.NewLine}{Environment.NewLine}No curated module is named `{moduleName}`.";
        }

        var lines = new List<string>
        {
            $"# {module.Name}",
            string.Empty,
            module.Summary,
            string.Empty
        };

        foreach (RhinoReferenceFunctionEntry function in module.Functions)
        {
            lines.Add($"## {function.FunctionName}");
            lines.Add(function.Summary);
            lines.Add(string.Empty);
            lines.Add($"Signature: `{function.Signature}`");
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatFunctionMarkdown(string functionName)
    {
        RhinoReferenceFunctionDetailResponse? function = GetFunction(functionName);
        if (function is null)
        {
            return $"# Rhino Reference Function Not Found{Environment.NewLine}{Environment.NewLine}No curated function is named `{functionName}`.";
        }

        var lines = new List<string>
        {
            $"# {function.FunctionName}",
            string.Empty,
            $"Module: `{function.ModuleName}`",
            string.Empty,
            function.Summary,
            string.Empty,
            $"Signature: `{function.Signature}`",
            string.Empty,
            "Notes:"
        };
        lines.AddRange(function.Notes.Select(note => $"- {note}"));
        lines.Add(string.Empty);
        lines.Add("Tags: " + string.Join(", ", function.Tags));
        return string.Join(Environment.NewLine, lines);
    }

    private static RhinoReferenceFunctionSummaryResponse ToSummary(RhinoReferenceFunctionEntry function)
    {
        return new RhinoReferenceFunctionSummaryResponse
        {
            ModuleName = function.ModuleName,
            FunctionName = function.FunctionName,
            Summary = function.Summary,
            Tags = function.Tags
        };
    }

    private static bool Matches(RhinoReferenceFunctionEntry function, string query)
    {
        return function.FunctionName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || function.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
            || function.Signature.Contains(query, StringComparison.OrdinalIgnoreCase)
            || function.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static RhinoReferenceFunctionEntry Function(
        string name,
        string summary,
        string signature,
        IReadOnlyList<string> notes,
        params string[] tags)
    {
        return new RhinoReferenceFunctionEntry(string.Empty, name, summary, signature, notes, tags);
    }

    private static int ClampLimit(int limit)
    {
        return Math.Clamp(limit <= 0 ? 10 : limit, 1, 50);
    }

    private sealed record RhinoReferenceModuleEntry(
        string Name,
        string Summary,
        IReadOnlyList<RhinoReferenceFunctionEntry> Functions);

    private sealed record RhinoReferenceFunctionEntry(
        string ModuleName,
        string FunctionName,
        string Summary,
        string Signature,
        IReadOnlyList<string> Notes,
        IReadOnlyList<string> Tags);
}

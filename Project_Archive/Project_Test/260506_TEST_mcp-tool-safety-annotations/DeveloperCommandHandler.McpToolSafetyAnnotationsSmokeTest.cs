using System.Reflection;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string McpToolSafetyAnnotationsSlug = "mcp-tool-safety-annotations-smoke-test";

    partial void RegisterMcpToolSafetyAnnotationHandlers()
    {
        _extensionHandlers[McpToolSafetyAnnotationsSlug] = HandleMcpToolSafetyAnnotationsSmokeTest;
    }

    private bool HandleMcpToolSafetyAnnotationsSmokeTest(string[] args)
    {
        try
        {
            RunMcpToolSafetyAnnotationsSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MCP tool safety annotations smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunMcpToolSafetyAnnotationsSmoke()
    {
        IReadOnlyDictionary<string, ToolSafetyExpectation> expectations = BuildToolSafetyExpectations();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var toolMethods = GetLoadableTypes(typeof(DeveloperCommandHandler).Assembly)
            .Where(IsToolType)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetCustomAttribute<McpServerToolAttribute>()
            })
            .Where(item => item.Attribute is not null)
            .OrderBy(item => SafeTypeFullName(item.Method.DeclaringType), StringComparer.Ordinal)
            .ThenBy(item => item.Method.Name, StringComparer.Ordinal)
            .ToList();

        RequireMcpToolSafety(toolMethods.Count == expectations.Count, $"Expected {expectations.Count} MCP tools, found {toolMethods.Count}.");

        foreach (var item in toolMethods)
        {
            string toolName = item.Method.Name;
            RequireMcpToolSafety(expectations.TryGetValue(toolName, out ToolSafetyExpectation expectation), $"MCP tool has no safety expectation: {toolName}");
            seen.Add(toolName);

            McpServerToolAttribute attribute = item.Attribute!;
            RequireMcpToolSafety(attribute.ReadOnly == expectation.ReadOnly, $"{toolName} ReadOnly should be {expectation.ReadOnly}.");
            RequireMcpToolSafety(attribute.Destructive == expectation.Destructive, $"{toolName} Destructive should be {expectation.Destructive}.");
            RequireMcpToolSafety(attribute.OpenWorld == expectation.OpenWorld, $"{toolName} OpenWorld should be {expectation.OpenWorld}.");
        }

        foreach (string expectedName in expectations.Keys)
        {
            RequireMcpToolSafety(seen.Contains(expectedName), $"Expected MCP tool was not found: {expectedName}");
        }

        VerifyToolSourceAttributes();

        Console.WriteLine($"[OK] MCP safety annotations verified for {toolMethods.Count} tools.");
        Console.WriteLine("[OK] No bare method-level [McpServerTool] attributes remain.");
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }

    private static bool IsToolType(Type type)
    {
        string? fullName = SafeTypeFullName(type);
        return fullName is not null && fullName.Contains(".Tools.", StringComparison.Ordinal);
    }

    private static string? SafeTypeFullName(Type? type)
    {
        if (type is null)
        {
            return null;
        }

        try
        {
            return type.FullName;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, ToolSafetyExpectation> BuildToolSafetyExpectations()
    {
        var expectations = new Dictionary<string, ToolSafetyExpectation>(StringComparer.Ordinal);

        Add(expectations, ToolSafetyExpectation.ReadOnlyClosedWorld,
            "CheckContinuityInLive",
            "FilterObjects",
            "GetClosestPointsInLive",
            "GetContourCurvesInLive",
            "GetContourCurvesByFilter",
            "GetCurvatureSamplesInLive",
            "GetCurvatureSamplesByFilter",
            "InspectTakeoffSources",
            "GetCurrentLayerInLive",
            "GetDocumentSummary",
            "GetGeometryFramesInLive",
            "GetGeometryFramesByFilter",
            "GetMassPropertiesInLive",
            "GetMassPropertiesByFilter",
            "GetObjectMetricsInLive",
            "GetObjectMetricsByFilter",
            "GetSelectedObjectsInLive",
            "IntersectObjectsInLive",
            "MeasureAnglesInLive",
            "MeasureDistancesInLive",
            "ResolveObjectIdsInLive",
            "CaptureViewportImage",
            "CaptureReferenceImageModelingQaViews",
            "CaptureDrawingExportState",
            "GetObjectUserStrings",
            "PreviewBulkObjectAttributeRecipe",
            "PreviewObjectEdits",
            "PreviewObjectUserTextWrites",
            "GetDocumentUserStrings",
            "GetBlockDefinitionDetails",
            "GetBlockInstanceDetails",
            "InspectSubDObjects",
            "InspectRenderMaterialTextures",
            "GetMcpRhinoToolHelp",
            "GetReferenceImageBriefSchema",
            "GetRhinoReferenceFunction",
            "ListWorksessionAttachments",
            "ListBlockDefinitions",
            "ListBlockInstances",
            "ListRhinoReferenceModules",
            "GetEditableGeometryDescriptor",
            "PreviewEditCurveGeometry",
            "PreviewEditSurfaceGeometry",
            "PreviewDeleteObjects",
            "PreviewEditControlPoints",
            "PreviewReplaceGeometry",
            "PreviewReferenceImageProductGeometryPlan",
            "PreviewTakeoffSchedule",
            "PreviewSplitCurves",
            "PreviewSubDCage",
            "PreviewTextureMapping",
            "PreviewTransformObjects",
            "PreviewBooleanObjects",
            "PreviewOpenings",
            "PreviewCreateBlockDefinitions",
            "PreviewExplodeBlockInstances",
            "PreviewInsertBlockInstances",
            "PreviewPurgeUnusedBlockDefinitions",
            "PreviewTransformBlockInstances",
            "InspectSurfaceRebuildDescriptor",
            "PreviewRedefineSurfacePointOrder",
            "PreviewTweakSurfaceDirections",
            "FindLayerCandidates",
            "GetLayers",
            "PreviewDeleteLayers",
            "PreviewModifyLayers",
            "PreviewPurgeLayers");
        Add(expectations, ToolSafetyExpectation.ReadOnlyClosedWorld,
            "SearchRhinoReference");

        Add(expectations, ToolSafetyExpectation.MutationClosedWorld,
            "ApplyDrawingExportStyle",
            "RestoreDrawingExportState",
            "SelectObjectsInLive",
            "SetDrawingExportBackground",
            "SetCurrentLayerInLive",
            "SetupDrawingViews",
            "ApplyBulkObjectAttributeRecipe",
            "ApplyObjectMaterials",
            "ApplyObjectEdits",
            "ApplyObjectUserTextWrites",
            "ApplyTextureMapping",
            "SetDocumentUserStrings",
            "ApplyCreateBlockDefinitions",
            "ApplyInsertBlockInstances",
            "ApplyTransformBlockInstances",
            "CreateArcs",
            "CreateBeams",
            "CreateBoxes",
            "CreateCapsules",
            "CreateCircles",
            "CreateColumns",
            "CreateCones",
            "CreateCurveExtrusions",
            "CreateCurveOffsets",
            "CreateCylinders",
            "CreateEllipsoids",
            "CreateEllipses",
            "CreateExtrusions",
            "CreateLines",
            "CreateLofts",
            "CreateLoftsFromProfiles",
            "CreateNurbsCurves",
            "CreatePipes",
            "CreatePipesFromPoints",
            "CreatePlanarBreps",
            "CreatePoints",
            "CreatePolylines",
            "CreateProfileExtrusionsFromPoints",
            "CreateRaisedStrips",
            "CreateRenderMaterials",
            "CreateRoundedBoxes",
            "CreateSlabs",
            "CreateSubDBox",
            "CreateSubDCage",
            "CreateSubDCushions",
            "CreateSplitCurveSegments",
            "CreateSpheres",
            "CreateSurfaces",
            "CreateSweepOneRail",
            "CreateTaperedBoxes",
            "CreateTori",
            "CreateWalls",
            "EditControlPoints",
            "ProjectCurves",
            "TransformObjects",
            "ApplyEditCurveGeometry",
            "ApplyEditSurfaceGeometry",
            "ApplyFlipSurfaceFrontBack",
            "ApplyRedefineSurfacePointOrder",
            "ApplyStandardFourPointSurfaceRebuild",
            "ApplyTweakSurfaceDirections",
            "CreateLayers",
            "ModifyLayers");

        Add(expectations, ToolSafetyExpectation.MutationClosedWorld,
            "RunReferenceImageObjectModelingAgent");

        Add(expectations, ToolSafetyExpectation.DestructiveClosedWorld,
            "DeleteObjectUserText",
            "ApplyExplodeBlockInstances",
            "ApplyPurgeUnusedBlockDefinitions",
            "ApplyBooleanObjects",
            "ApplyOpenings",
            "DeleteDocumentUserStrings",
            "DeleteObjects",
            "ReplaceGeometry",
            "ReplaceSplitCurves",
            "DeleteLayers",
            "PurgeLayers");

        Add(expectations, ToolSafetyExpectation.MutationOpenWorld,
            "AppendActivityLog",
            "CreateTexturedRenderMaterials",
            "UpdateLinkedBlock");

        Add(expectations, ToolSafetyExpectation.DestructiveOpenWorld,
            "ExportDrawingPackage",
            "ExportToDwg",
            "ExportToDxf",
            "ExportToIfc",
            "ExportToImage",
            "ExportToPdf",
            "ExportToStl",
            "GenerateProceduralTextureImage",
            "ExportTakeoffSchedule",
            "RunTakeoffSpreadsheetAgent");

        return expectations;
    }

    private static void Add(
        IDictionary<string, ToolSafetyExpectation> expectations,
        ToolSafetyExpectation expectation,
        params string[] toolNames)
    {
        foreach (string toolName in toolNames)
        {
            if (!expectations.TryAdd(toolName, expectation))
            {
                throw new InvalidOperationException($"Duplicate MCP tool safety expectation: {toolName}");
            }
        }
    }

    private static void VerifyToolSourceAttributes()
    {
        string root = Directory.GetCurrentDirectory();
        string toolsRoot = Path.Combine(root, "src", "MCP_Rhino.Server", "Tools");
        RequireMcpToolSafety(Directory.Exists(toolsRoot), $"Tools source directory was not found: {toolsRoot}");

        var bareAttribute = new Regex(@"^\s*\[McpServerTool\]\s*$", RegexOptions.Compiled);
        var methodAttribute = new Regex(@"^\s*\[McpServerTool\((?<args>.*)\)\]\s*$", RegexOptions.Compiled);
        int methodAttributeCount = 0;

        foreach (string sourcePath in Directory.EnumerateFiles(toolsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(root, sourcePath);
            string[] lines = File.ReadAllLines(sourcePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                RequireMcpToolSafety(!bareAttribute.IsMatch(line), $"Bare [McpServerTool] attribute found: {relativePath}:{i + 1}");

                Match match = methodAttribute.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                string args = match.Groups["args"].Value;
                methodAttributeCount++;
                RequireMcpToolSafety(args.Contains("ReadOnly =", StringComparison.Ordinal), $"Missing ReadOnly metadata: {relativePath}:{i + 1}");
                RequireMcpToolSafety(args.Contains("Destructive =", StringComparison.Ordinal), $"Missing Destructive metadata: {relativePath}:{i + 1}");
                RequireMcpToolSafety(args.Contains("OpenWorld =", StringComparison.Ordinal), $"Missing OpenWorld metadata: {relativePath}:{i + 1}");
            }
        }

        RequireMcpToolSafety(methodAttributeCount == BuildToolSafetyExpectations().Count, $"Expected annotated source count to match tool expectation count. Source={methodAttributeCount}.");
    }

    private static void RequireMcpToolSafety(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private readonly record struct ToolSafetyExpectation(bool ReadOnly, bool Destructive, bool OpenWorld)
    {
        public static ToolSafetyExpectation ReadOnlyClosedWorld => new(true, false, false);
        public static ToolSafetyExpectation MutationClosedWorld => new(false, false, false);
        public static ToolSafetyExpectation DestructiveClosedWorld => new(false, true, false);
        public static ToolSafetyExpectation MutationOpenWorld => new(false, false, true);
        public static ToolSafetyExpectation DestructiveOpenWorld => new(false, true, true);
    }
}

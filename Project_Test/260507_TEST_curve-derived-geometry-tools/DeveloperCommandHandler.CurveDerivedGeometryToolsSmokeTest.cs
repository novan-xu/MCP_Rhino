using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.Plugin;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string CurveDerivedGeometryToolsSlug = "curve-derived-geometry-tools-smoke-test";

    partial void RegisterCurveDerivedGeometryToolsHandlers()
    {
        _extensionHandlers[CurveDerivedGeometryToolsSlug] = HandleCurveDerivedGeometryToolsSmokeTest;
    }

    private bool HandleCurveDerivedGeometryToolsSmokeTest(string[] args)
    {
        try
        {
            if (McpRhinoPlugin.Instance is null)
            {
                RunCurveDerivedGeometryToolsCliFallbackSmoke();
            }
            else
            {
                string filePath = args.Length > 1 ? args[1] : string.Empty;
                RunCurveDerivedGeometryToolsLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Curve-derived geometry tools smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCurveDerivedGeometryToolsCliFallbackSmoke()
    {
        string filePath = "C:/mcp-rhino/curve-derived-smoke.3dm";
        GeometryCreationCommonOptions common = CurveDerivedSmokeCommon("MCP::CURVE_DERIVED_SMOKE");
        Guid curveId = Guid.NewGuid();
        Guid curveId2 = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();

        RequireCurveDerivedLiveRequired(_curveDerivedGeometryService.CreateLofts(new CreateLoftsRequest
        {
            FilePath = filePath,
            Entries = new List<LoftEntryRequest> { new() { CurveObjectIds = new List<Guid> { curveId, curveId2 } } },
            Common = common
        }), "CreateLofts");

        RequireCurveDerivedLiveRequired(_curveDerivedGeometryService.CreateCurveExtrusions(new CreateCurveExtrusionsRequest
        {
            FilePath = filePath,
            Entries = new List<CurveExtrusionEntryRequest> { new() { CurveObjectId = curveId, VectorZ = 2 } },
            Common = common
        }), "CreateCurveExtrusions");

        RequireCurveDerivedLiveRequired(_curveDerivedGeometryService.CreateSweepOneRail(new CreateSweepOneRailRequest
        {
            FilePath = filePath,
            Entries = new List<SweepOneRailEntryRequest> { new() { RailCurveObjectId = curveId, ProfileCurveObjectIds = new List<Guid> { curveId2 } } },
            Common = common
        }), "CreateSweepOneRail");

        RequireCurveDerivedLiveRequired(_curveDerivedGeometryService.CreateCurveOffsets(new CreateCurveOffsetsRequest
        {
            FilePath = filePath,
            Entries = new List<CurveOffsetEntryRequest> { new() { CurveObjectId = curveId, Distance = 0.25 } },
            Common = common
        }), "CreateCurveOffsets");

        RequireCurveDerivedLiveRequired(_curveDerivedGeometryService.CreatePipes(new CreatePipesRequest
        {
            FilePath = filePath,
            Entries = new List<PipeEntryRequest> { new() { CurveObjectId = curveId, Radius = 0.1 } },
            Common = common
        }), "CreatePipes");

        RequireCurveDerivedLiveRequired(_curveDerivedGeometryService.ProjectCurves(new ProjectCurvesRequest
        {
            FilePath = filePath,
            Entries = new List<CurveProjectionEntryRequest> { new() { CurveObjectId = curveId, TargetObjectIds = new List<Guid> { targetId } } },
            Common = common
        }), "ProjectCurves");

        RequireCurveDerivedLiveRequired(_curveDerivedGeometryService.PreviewSplitCurves(new PreviewSplitCurvesRequest
        {
            FilePath = filePath,
            Entries = new List<CurveSplitEntryRequest> { new() { CurveObjectId = curveId, Parameters = new List<double> { 0.5 } } }
        }), "PreviewSplitCurves");

        RequireCurveDerivedLiveRequired(_curveDerivedGeometryService.CreateSplitCurveSegments(new CreateSplitCurveSegmentsRequest
        {
            FilePath = filePath,
            Entries = new List<CurveSplitEntryRequest> { new() { CurveObjectId = curveId, Parameters = new List<double> { 0.5 } } },
            Common = common
        }), "CreateSplitCurveSegments");

        RequireCurveDerivedLiveRequired(_curveDerivedGeometryService.ReplaceSplitCurves(new ReplaceSplitCurvesRequest
        {
            FilePath = filePath,
            Entries = new List<CurveSplitEntryRequest> { new() { CurveObjectId = curveId, Parameters = new List<double> { 0.5 } } },
            Common = common
        }), "ReplaceSplitCurves");

        Console.WriteLine("[OK] curve-derived-geometry-tools CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.");
    }

    private void RunCurveDerivedGeometryToolsLiveSmoke(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Live curve-derived geometry smoke requires a saved active document path.");
        }

        string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string smokeLayer = $"MCP::CURVE_DERIVED_SMOKE_{suffix}";
        GeometryCreationCommonOptions common = CurveDerivedSmokeCommon(smokeLayer);

        RequireCurveDerivedSuccess(_layerManagementService.Create(new CreateLayersRequest
        {
            FilePath = filePath,
            Entries = new List<LayerCreationEntryRequest> { new() { FullPath = smokeLayer } }
        }), "Create smoke layer");

        Guid loftProfile0 = RequireCurveDerivedCreated(_geometryCreationSkill.Create(new CreateCirclesRequest
        {
            FilePath = filePath,
            Items = new List<CircleItemRequest> { new() { CenterX = 0, CenterY = 0, CenterZ = 0, Radius = 0.5, NormalZ = 1 } },
            Common = common
        }), "Create loft profile 0");

        Guid loftProfile1 = RequireCurveDerivedCreated(_geometryCreationSkill.Create(new CreateCirclesRequest
        {
            FilePath = filePath,
            Items = new List<CircleItemRequest> { new() { CenterX = 0, CenterY = 0, CenterZ = 1, Radius = 0.35, NormalZ = 1 } },
            Common = common
        }), "Create loft profile 1");

        Guid extrusionProfile = RequireCurveDerivedCreated(_geometryCreationSkill.Create(new CreateCirclesRequest
        {
            FilePath = filePath,
            Items = new List<CircleItemRequest> { new() { CenterX = 2, CenterY = 0, CenterZ = 0, Radius = 0.35, NormalZ = 1 } },
            Common = common
        }), "Create extrusion profile");

        Guid sweepRail = RequireCurveDerivedCreated(_geometryCreationSkill.Create(new CreateLinesRequest
        {
            FilePath = filePath,
            Items = new List<LineItemRequest> { new() { StartX = 4, StartY = 0, StartZ = 0, EndX = 6, EndY = 0, EndZ = 0 } },
            Common = common
        }), "Create sweep rail");

        Guid sweepProfile = RequireCurveDerivedCreated(_geometryCreationSkill.Create(new CreateCirclesRequest
        {
            FilePath = filePath,
            Items = new List<CircleItemRequest> { new() { CenterX = 4, CenterY = 0, CenterZ = 0, Radius = 0.2, NormalX = 1, NormalZ = 0 } },
            Common = common
        }), "Create sweep profile");

        Guid offsetCurve = RequireCurveDerivedCreated(_geometryCreationSkill.Create(new CreatePolylinesRequest
        {
            FilePath = filePath,
            Items = new List<PolylineItemRequest> { new() { Points = CurveDerivedPolylinePoints(7), Closed = true } },
            Common = common
        }), "Create offset source");

        Guid pipeRail = RequireCurveDerivedCreated(_geometryCreationSkill.Create(new CreateLinesRequest
        {
            FilePath = filePath,
            Items = new List<LineItemRequest> { new() { StartX = 10, StartY = 0, StartZ = 0, EndX = 12, EndY = 1, EndZ = 0 } },
            Common = common
        }), "Create pipe rail");

        Guid projectionCurve = RequireCurveDerivedCreated(_geometryCreationSkill.Create(new CreateLinesRequest
        {
            FilePath = filePath,
            Items = new List<LineItemRequest> { new() { StartX = 0, StartY = 3, StartZ = 2, EndX = 2, EndY = 4, EndZ = 2 } },
            Common = common
        }), "Create projection curve");

        Guid projectionTarget = RequireCurveDerivedCreated(_geometryCreationSkill.Create(new CreateSurfacesRequest
        {
            FilePath = filePath,
            Items = new List<SurfaceItemRequest>
            {
                new()
                {
                    Mode = SurfaceConstructionMode.FourCorners,
                    Corner0X = -1, Corner0Y = 2, Corner0Z = 0,
                    Corner1X = 3, Corner1Y = 2, Corner1Z = 0,
                    Corner2X = 3, Corner2Y = 5, Corner2Z = 0,
                    Corner3X = -1, Corner3Y = 5, Corner3Z = 0
                }
            },
            Common = common
        }), "Create projection target surface");

        Guid splitSource = RequireCurveDerivedCreated(_geometryCreationSkill.Create(new CreateLinesRequest
        {
            FilePath = filePath,
            Items = new List<LineItemRequest> { new() { StartX = 0, StartY = 6, StartZ = 0, EndX = 4, EndY = 6, EndZ = 0 } },
            Common = common
        }), "Create split source");

        Guid replaceSplitSource = RequireCurveDerivedCreated(_geometryCreationSkill.Create(new CreateLinesRequest
        {
            FilePath = filePath,
            Items = new List<LineItemRequest> { new() { StartX = 0, StartY = 7, StartZ = 0, EndX = 4, EndY = 7, EndZ = 0 } },
            Common = common
        }), "Create replace split source");

        RequireCurveDerivedCreatedObjects(_curveDerivedGeometryService.CreateLofts(new CreateLoftsRequest
        {
            FilePath = filePath,
            Entries = new List<LoftEntryRequest> { new() { CurveObjectIds = new List<Guid> { loftProfile0, loftProfile1 }, Name = "smoke loft" } },
            Common = common
        }), "CreateLofts");

        RequireCurveDerivedCreatedObjects(_curveDerivedGeometryService.CreateCurveExtrusions(new CreateCurveExtrusionsRequest
        {
            FilePath = filePath,
            Entries = new List<CurveExtrusionEntryRequest> { new() { CurveObjectId = extrusionProfile, VectorZ = 1.25, Cap = true, Name = "smoke extrusion" } },
            Common = common
        }), "CreateCurveExtrusions");

        RequireCurveDerivedCreatedObjects(_curveDerivedGeometryService.CreateSweepOneRail(new CreateSweepOneRailRequest
        {
            FilePath = filePath,
            Entries = new List<SweepOneRailEntryRequest> { new() { RailCurveObjectId = sweepRail, ProfileCurveObjectIds = new List<Guid> { sweepProfile }, Name = "smoke sweep" } },
            Common = common
        }), "CreateSweepOneRail");

        RequireCurveDerivedCreatedObjects(_curveDerivedGeometryService.CreateCurveOffsets(new CreateCurveOffsetsRequest
        {
            FilePath = filePath,
            Entries = new List<CurveOffsetEntryRequest> { new() { CurveObjectId = offsetCurve, Distance = 0.2, PlaneNormalZ = 1, Name = "smoke offset" } },
            Common = common
        }), "CreateCurveOffsets");

        RequireCurveDerivedCreatedObjects(_curveDerivedGeometryService.CreatePipes(new CreatePipesRequest
        {
            FilePath = filePath,
            Entries = new List<PipeEntryRequest> { new() { CurveObjectId = pipeRail, Radius = 0.1, Name = "smoke pipe" } },
            Common = common
        }), "CreatePipes");

        RequireCurveDerivedCreatedObjects(_curveDerivedGeometryService.ProjectCurves(new ProjectCurvesRequest
        {
            FilePath = filePath,
            Entries = new List<CurveProjectionEntryRequest>
            {
                new()
                {
                    CurveObjectId = projectionCurve,
                    TargetObjectIds = new List<Guid> { projectionTarget },
                    DirectionZ = -1,
                    Name = "smoke projection"
                }
            },
            Common = common
        }), "ProjectCurves");

        CurveSplitPreviewResponse preview = RequireCurveDerivedSuccess(_curveDerivedGeometryService.PreviewSplitCurves(new PreviewSplitCurvesRequest
        {
            FilePath = filePath,
            Entries = new List<CurveSplitEntryRequest>
            {
                new()
                {
                    CurveObjectId = splitSource,
                    Points = new List<CurveSplitPointRequest> { new() { X = 2, Y = 6, Z = 0 } }
                }
            }
        }), "PreviewSplitCurves");

        if (preview.PreviewedCount != 1 || preview.Results[0].ExpectedSegmentCount < 2)
        {
            throw new InvalidOperationException("PreviewSplitCurves did not predict split segments.");
        }

        RequireCurveDerivedCreatedObjects(_curveDerivedGeometryService.CreateSplitCurveSegments(new CreateSplitCurveSegmentsRequest
        {
            FilePath = filePath,
            Entries = new List<CurveSplitEntryRequest>
            {
                new()
                {
                    CurveObjectId = splitSource,
                    Points = new List<CurveSplitPointRequest> { new() { X = 2, Y = 6, Z = 0 } },
                    Name = "smoke split segment"
                }
            },
            Common = common
        }), "CreateSplitCurveSegments");

        CurveDerivedGeometryResponse replace = RequireCurveDerivedSuccess(_curveDerivedGeometryService.ReplaceSplitCurves(new ReplaceSplitCurvesRequest
        {
            FilePath = filePath,
            Entries = new List<CurveSplitEntryRequest>
            {
                new()
                {
                    CurveObjectId = replaceSplitSource,
                    Points = new List<CurveSplitPointRequest> { new() { X = 2, Y = 7, Z = 0 } },
                    Name = "smoke replace split segment"
                }
            },
            Common = common
        }), "ReplaceSplitCurves");

        if (replace.DeletedSourceObjectCount != 1)
        {
            throw new InvalidOperationException("ReplaceSplitCurves did not delete the source curve.");
        }

        Console.WriteLine("[OK] curve-derived-geometry-tools live smoke completed.");
    }

    private static GeometryCreationCommonOptions CurveDerivedSmokeCommon(string layerFullPath)
    {
        return new GeometryCreationCommonOptions
        {
            LayerFullPath = layerFullPath,
            Name = "mcp curve derived smoke",
            Color = new ObjectColorRequest { R = 220, G = 120, B = 50 },
            UserText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mcp.smoke"] = "curve-derived-geometry-tools"
            }
        };
    }

    private static List<GeneralPrimitivePointRequest> CurveDerivedPolylinePoints(double offsetX)
    {
        return new List<GeneralPrimitivePointRequest>
        {
            new() { X = offsetX, Y = 0, Z = 0 },
            new() { X = offsetX + 1, Y = 0, Z = 0 },
            new() { X = offsetX + 1, Y = 1, Z = 0 },
            new() { X = offsetX, Y = 1, Z = 0 }
        };
    }

    private static void RequireCurveDerivedLiveRequired<T>(OperationResponse<T> response, string label)
    {
        if (response.Success || !string.Equals(response.Message, "LIVE_RHINO_REQUIRED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} should return LIVE_RHINO_REQUIRED in CLI fallback mode; got Success={response.Success}, Message={response.Message}");
        }
    }

    private static Guid RequireCurveDerivedCreated(OperationResponse<GeometryCreationResponse> response, string label)
    {
        GeometryCreationResponse data = RequireCurveDerivedSuccess(response, label);
        if (data.CreatedObjects.Count == 0)
        {
            throw new InvalidOperationException($"{label} did not create an object.");
        }

        return data.CreatedObjects[0].ObjectId;
    }

    private static Guid RequireCurveDerivedCreated(OperationResponse<GeneralPrimitiveCreationResponse> response, string label)
    {
        GeneralPrimitiveCreationResponse data = RequireCurveDerivedSuccess(response, label);
        if (data.CreatedObjects.Count == 0)
        {
            throw new InvalidOperationException($"{label} did not create an object.");
        }

        return data.CreatedObjects[0].ObjectId;
    }

    private static void RequireCurveDerivedCreatedObjects(OperationResponse<CurveDerivedGeometryResponse> response, string label)
    {
        CurveDerivedGeometryResponse data = RequireCurveDerivedSuccess(response, label);
        if (data.CreatedObjectCount == 0 || data.SucceededCount == 0)
        {
            throw new InvalidOperationException($"{label} did not create curve-derived geometry. FailedCount={data.FailedCount}.");
        }
    }

    private static T RequireCurveDerivedSuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }
}

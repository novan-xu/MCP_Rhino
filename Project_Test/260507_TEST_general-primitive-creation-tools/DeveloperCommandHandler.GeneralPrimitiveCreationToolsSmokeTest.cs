using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.Plugin;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string GeneralPrimitiveCreationToolsSlug = "general-primitive-creation-tools-smoke-test";

    partial void RegisterGeneralPrimitiveCreationToolsHandlers()
    {
        _extensionHandlers[GeneralPrimitiveCreationToolsSlug] = HandleGeneralPrimitiveCreationToolsSmokeTest;
    }

    private bool HandleGeneralPrimitiveCreationToolsSmokeTest(string[] args)
    {
        try
        {
            if (McpRhinoPlugin.Instance is null)
            {
                RunGeneralPrimitiveCreationToolsCliFallbackSmoke();
            }
            else
            {
                string filePath = args.Length > 1 ? args[1] : string.Empty;
                RunGeneralPrimitiveCreationToolsLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"General primitive creation tools smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunGeneralPrimitiveCreationToolsCliFallbackSmoke()
    {
        string filePath = "C:/mcp-rhino/general-primitive-smoke.3dm";
        GeometryCreationCommonOptions common = GeneralPrimitiveSmokeCommon("MCP::GENERAL_PRIMITIVE_SMOKE");

        RequireGeneralPrimitiveLiveRequired(_geometryCreationSkill.Create(new CreateCirclesRequest
        {
            FilePath = filePath,
            Items = new List<CircleItemRequest> { new() { Radius = 1 } },
            Common = common
        }), "CreateCircles");

        RequireGeneralPrimitiveLiveRequired(_geometryCreationSkill.Create(new CreateEllipsesRequest
        {
            FilePath = filePath,
            Items = new List<EllipseItemRequest> { new() { RadiusX = 2, RadiusY = 1 } },
            Common = common
        }), "CreateEllipses");

        RequireGeneralPrimitiveLiveRequired(_geometryCreationSkill.Create(new CreatePolylinesRequest
        {
            FilePath = filePath,
            Items = new List<PolylineItemRequest> { new() { Points = GeneralPrimitiveSmokePolylinePoints() } },
            Common = common
        }), "CreatePolylines");

        RequireGeneralPrimitiveLiveRequired(_geometryCreationSkill.Create(new CreateNurbsCurvesRequest
        {
            FilePath = filePath,
            Items = new List<NurbsCurveItemRequest> { new() { ControlPoints = GeneralPrimitiveSmokeCurvePoints(), Degree = 3 } },
            Common = common
        }), "CreateNurbsCurves");

        RequireGeneralPrimitiveLiveRequired(_geometryCreationSkill.Create(new CreateSpheresRequest
        {
            FilePath = filePath,
            Items = new List<SphereItemRequest> { new() { Radius = 1 } },
            Common = common
        }), "CreateSpheres");

        RequireGeneralPrimitiveLiveRequired(_geometryCreationSkill.Create(new CreateConesRequest
        {
            FilePath = filePath,
            Items = new List<ConeItemRequest> { new() { Radius = 1, Height = 2 } },
            Common = common
        }), "CreateCones");

        RequireGeneralPrimitiveLiveRequired(_geometryCreationSkill.Create(new CreateCylindersRequest
        {
            FilePath = filePath,
            Items = new List<CylinderItemRequest> { new() { Radius = 1, Height = 2 } },
            Common = common
        }), "CreateCylinders");

        Console.WriteLine("[OK] general-primitive-creation-tools CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.");
    }

    private void RunGeneralPrimitiveCreationToolsLiveSmoke(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Live general primitive smoke requires a saved active document path.");
        }

        string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string smokeLayer = $"MCP::GENERAL_PRIMITIVE_SMOKE_{suffix}";
        GeometryCreationCommonOptions common = GeneralPrimitiveSmokeCommon(smokeLayer);

        RequireGeneralPrimitiveSuccess(_layerManagementService.Create(new CreateLayersRequest
        {
            FilePath = filePath,
            Entries = new List<LayerCreationEntryRequest>
            {
                new() { FullPath = smokeLayer }
            }
        }), "Create smoke layer");

        List<GeneralPrimitiveCreationResponse> responses = new()
        {
            RequireGeneralPrimitiveSuccess(_geometryCreationSkill.Create(new CreateCirclesRequest
            {
                FilePath = filePath,
                Items = new List<CircleItemRequest>
                {
                    new() { CenterX = 0, CenterY = 0, CenterZ = 0, Radius = 0.5, Name = "smoke circle" }
                },
                Common = common
            }), "CreateCircles"),

            RequireGeneralPrimitiveSuccess(_geometryCreationSkill.Create(new CreateEllipsesRequest
            {
                FilePath = filePath,
                Items = new List<EllipseItemRequest>
                {
                    new() { CenterX = 2, CenterY = 0, CenterZ = 0, RadiusX = 0.8, RadiusY = 0.35, Name = "smoke ellipse" }
                },
                Common = common
            }), "CreateEllipses"),

            RequireGeneralPrimitiveSuccess(_geometryCreationSkill.Create(new CreatePolylinesRequest
            {
                FilePath = filePath,
                Items = new List<PolylineItemRequest>
                {
                    new() { Points = GeneralPrimitiveSmokePolylinePoints(4), Closed = true, Name = "smoke closed polyline" }
                },
                Common = common
            }), "CreatePolylines"),

            RequireGeneralPrimitiveSuccess(_geometryCreationSkill.Create(new CreateNurbsCurvesRequest
            {
                FilePath = filePath,
                Items = new List<NurbsCurveItemRequest>
                {
                    new() { ControlPoints = GeneralPrimitiveSmokeCurvePoints(6), Degree = 3, Name = "smoke nurbs curve" }
                },
                Common = common
            }), "CreateNurbsCurves"),

            RequireGeneralPrimitiveSuccess(_geometryCreationSkill.Create(new CreateSpheresRequest
            {
                FilePath = filePath,
                Items = new List<SphereItemRequest>
                {
                    new() { CenterX = 8, CenterY = 0, CenterZ = 0.5, Radius = 0.5, Name = "smoke sphere" }
                },
                Common = common
            }), "CreateSpheres"),

            RequireGeneralPrimitiveSuccess(_geometryCreationSkill.Create(new CreateConesRequest
            {
                FilePath = filePath,
                Items = new List<ConeItemRequest>
                {
                    new() { BaseX = 10, BaseY = 0, BaseZ = 0, Radius = 0.5, Height = 1.25, Name = "smoke cone" }
                },
                Common = common
            }), "CreateCones"),

            RequireGeneralPrimitiveSuccess(_geometryCreationSkill.Create(new CreateCylindersRequest
            {
                FilePath = filePath,
                Items = new List<CylinderItemRequest>
                {
                    new() { BaseX = 12, BaseY = 0, BaseZ = 0, Radius = 0.5, Height = 1.25, Name = "smoke cylinder" }
                },
                Common = common
            }), "CreateCylinders")
        };

        GeneralPrimitiveKind[] expectedKinds =
        {
            GeneralPrimitiveKind.Circle,
            GeneralPrimitiveKind.Ellipse,
            GeneralPrimitiveKind.Polyline,
            GeneralPrimitiveKind.NurbsCurve,
            GeneralPrimitiveKind.Sphere,
            GeneralPrimitiveKind.Cone,
            GeneralPrimitiveKind.Cylinder
        };

        for (int i = 0; i < responses.Count; i++)
        {
            RequireGeneralPrimitiveCreated(responses[i], expectedKinds[i], smokeLayer);
        }

        int totalCreated = responses.Sum(response => response.CreatedCount);
        Console.WriteLine("[OK] general-primitive-creation-tools live smoke completed.");
        Console.WriteLine($"[OK] Created {totalCreated} primitives on layer {smokeLayer}.");
    }

    private static GeometryCreationCommonOptions GeneralPrimitiveSmokeCommon(string layerFullPath)
    {
        return new GeometryCreationCommonOptions
        {
            LayerFullPath = layerFullPath,
            Name = "mcp general primitive smoke",
            Color = new ObjectColorRequest { R = 40, G = 160, B = 220 },
            UserText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mcp.smoke"] = "general-primitive-creation-tools"
            }
        };
    }

    private static List<GeneralPrimitivePointRequest> GeneralPrimitiveSmokePolylinePoints(double offsetX = 0d)
    {
        return new List<GeneralPrimitivePointRequest>
        {
            new() { X = offsetX, Y = 0, Z = 0 },
            new() { X = offsetX + 1, Y = 0, Z = 0 },
            new() { X = offsetX + 1, Y = 1, Z = 0 },
            new() { X = offsetX, Y = 1, Z = 0 }
        };
    }

    private static List<GeneralPrimitivePointRequest> GeneralPrimitiveSmokeCurvePoints(double offsetX = 0d)
    {
        return new List<GeneralPrimitivePointRequest>
        {
            new() { X = offsetX, Y = 0, Z = 0 },
            new() { X = offsetX + 0.5, Y = 1, Z = 0 },
            new() { X = offsetX + 1.5, Y = -0.5, Z = 0 },
            new() { X = offsetX + 2, Y = 0.75, Z = 0 }
        };
    }

    private static void RequireGeneralPrimitiveLiveRequired<T>(OperationResponse<T> response, string label)
    {
        if (response.Success || !string.Equals(response.Message, "LIVE_RHINO_REQUIRED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} should return LIVE_RHINO_REQUIRED in CLI fallback mode; got Success={response.Success}, Message={response.Message}");
        }
    }

    private static T RequireGeneralPrimitiveSuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireGeneralPrimitiveCreated(
        GeneralPrimitiveCreationResponse response,
        GeneralPrimitiveKind expectedKind,
        string expectedLayer)
    {
        if (response.CreatedCount != 1 || response.CreatedObjects.Count != 1)
        {
            throw new InvalidOperationException($"Expected one created {expectedKind}, got {response.CreatedCount}.");
        }

        GeneralPrimitiveCreatedObjectResponse created = response.CreatedObjects[0];
        if (created.Kind != expectedKind)
        {
            throw new InvalidOperationException($"Expected created kind {expectedKind}, got {created.Kind}.");
        }

        if (!string.Equals(created.LayerFullPath, expectedLayer, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected layer {expectedLayer}, got {created.LayerFullPath}.");
        }

        if (created.ObjectId == Guid.Empty || created.BoundingBox is null || string.IsNullOrWhiteSpace(created.GeometryTypeName))
        {
            throw new InvalidOperationException($"Created {expectedKind} did not return id, type name, and bounding box.");
        }
    }
}

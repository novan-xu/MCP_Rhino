using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.Plugin;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string ReferenceImageObjectModelingToolsSlug = "reference-image-object-modeling-tools-smoke-test";

    partial void RegisterReferenceImageObjectModelingToolsHandlers()
    {
        _extensionHandlers[ReferenceImageObjectModelingToolsSlug] = HandleReferenceImageObjectModelingToolsSmokeTest;
    }

    private bool HandleReferenceImageObjectModelingToolsSmokeTest(string[] args)
    {
        try
        {
            if (McpRhinoPlugin.Instance is null)
            {
                RunReferenceImageObjectModelingToolsCliFallbackSmoke();
            }
            else
            {
                string filePath = args.Length > 1 ? args[1] : string.Empty;
                RunReferenceImageObjectModelingToolsLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Reference image object modeling tools smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunReferenceImageObjectModelingToolsCliFallbackSmoke()
    {
        string filePath = "C:/mcp-rhino/reference-image-object-modeling-tools-smoke.3dm";
        GeometryCreationCommonOptions common = ReferenceImageObjectModelingToolsCommon("MCP::REFERENCE_IMAGE_OBJECT_MODELING_TOOLS_SMOKE");

        RequireReferenceImageObjectModelingLiveRequired(_geometryCreationSkill.Create(new CreateRoundedBoxesRequest
        {
            FilePath = filePath,
            Items = new List<RoundedBoxItemRequest>
            {
                new() { Width = 2, Depth = 1, Height = 0.5, Radius = 0.1 }
            },
            Common = common
        }), "CreateRoundedBoxes");

        RequireReferenceImageObjectModelingLiveRequired(_geometryCreationSkill.Create(new CreateEllipsoidsRequest
        {
            FilePath = filePath,
            Items = new List<EllipsoidItemRequest>
            {
                new() { RadiusX = 1, RadiusY = 0.5, RadiusZ = 0.25 }
            },
            Common = common
        }), "CreateEllipsoids");

        RequireReferenceImageObjectModelingLiveRequired(_geometryCreationSkill.Create(new CreateCapsulesRequest
        {
            FilePath = filePath,
            Items = new List<CapsuleItemRequest>
            {
                new() { StartX = 0, StartY = 0, StartZ = 0, EndX = 2, EndY = 0, EndZ = 0, Radius = 0.15 }
            },
            Common = common
        }), "CreateCapsules");

        RequireReferenceImageObjectModelingLiveRequired(_geometryCreationSkill.Create(new CreateToriRequest
        {
            FilePath = filePath,
            Items = new List<TorusItemRequest>
            {
                new() { MajorRadius = 0.8, MinorRadius = 0.12 }
            },
            Common = common
        }), "CreateTori");

        RequireReferenceImageObjectModelingLiveRequired(_geometryCreationSkill.Create(new CreateRaisedStripsRequest
        {
            FilePath = filePath,
            Items = new List<RaisedStripItemRequest>
            {
                new() { StartX = 0, StartY = 0, StartZ = 0, EndX = 2, EndY = 0, EndZ = 0, Width = 0.08, Height = 0.05 }
            },
            Common = common
        }), "CreateRaisedStrips");

        RequireReferenceImageObjectModelingLiveRequired(_rhinoMaterialService.Create(new CreateRenderMaterialsRequest
        {
            FilePath = filePath,
            Items = new List<RenderMaterialItemRequest>
            {
                new()
                {
                    Name = "reference image object modeling smoke fabric",
                    BaseColor = new ObjectColorRequest { R = 142, G = 92, B = 73 },
                    Roughness = 0.85
                }
            }
        }), "CreateRenderMaterials");

        RequireReferenceImageObjectModelingLiveRequired(_rhinoMaterialService.Apply(new ApplyObjectMaterialsRequest
        {
            FilePath = filePath,
            Assignments = new List<ObjectMaterialAssignmentRequest>
            {
                new()
                {
                    MaterialName = "reference image object modeling smoke fabric",
                    ObjectIds = new List<Guid> { Guid.Parse("11111111-1111-1111-1111-111111111111") }
                }
            }
        }), "ApplyObjectMaterials");

        Console.WriteLine("[OK] reference-image-object-modeling-tools CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.");
    }

    private void RunReferenceImageObjectModelingToolsLiveSmoke(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Live reference image object modeling tools smoke requires a saved active document path.");
        }

        string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string smokeLayer = $"MCP::REFERENCE_IMAGE_OBJECT_MODELING_TOOLS_SMOKE_{suffix}";
        GeometryCreationCommonOptions common = ReferenceImageObjectModelingToolsCommon(smokeLayer);

        RequireReferenceImageObjectModelingSuccess(_layerManagementService.Create(new CreateLayersRequest
        {
            FilePath = filePath,
            Entries = new List<LayerCreationEntryRequest>
            {
                new() { FullPath = smokeLayer }
            }
        }), "Create object modeling tools smoke layer");

        List<GeneralPrimitiveCreationResponse> primitiveResponses = new()
        {
            RequireReferenceImageObjectModelingSuccess(_geometryCreationSkill.Create(new CreateRoundedBoxesRequest
            {
                FilePath = filePath,
                Items = new List<RoundedBoxItemRequest>
                {
                    new() { CenterX = 0, CenterY = 0, CenterZ = 0.4, Width = 2.4, Depth = 1.1, Height = 0.45, Radius = 0.12, Name = "smoke rounded cushion" }
                },
                Common = common
            }), "CreateRoundedBoxes"),

            RequireReferenceImageObjectModelingSuccess(_geometryCreationSkill.Create(new CreateEllipsoidsRequest
            {
                FilePath = filePath,
                Items = new List<EllipsoidItemRequest>
                {
                    new() { CenterX = 3, CenterY = 0, CenterZ = 0.6, RadiusX = 0.9, RadiusY = 0.42, RadiusZ = 0.28, Name = "smoke ellipsoid cushion" }
                },
                Common = common
            }), "CreateEllipsoids"),

            RequireReferenceImageObjectModelingSuccess(_geometryCreationSkill.Create(new CreateCapsulesRequest
            {
                FilePath = filePath,
                Items = new List<CapsuleItemRequest>
                {
                    new() { StartX = -1.1, StartY = 0.75, StartZ = 0.95, EndX = 1.1, EndY = 0.75, EndZ = 0.95, Radius = 0.14, Name = "smoke capsule roll" }
                },
                Common = common
            }), "CreateCapsules"),

            RequireReferenceImageObjectModelingSuccess(_geometryCreationSkill.Create(new CreateToriRequest
            {
                FilePath = filePath,
                Items = new List<TorusItemRequest>
                {
                    new() { CenterX = 5.2, CenterY = 0, CenterZ = 0.7, MajorRadius = 0.45, MinorRadius = 0.08, Name = "smoke torus ring" }
                },
                Common = common
            }), "CreateTori"),

            RequireReferenceImageObjectModelingSuccess(_geometryCreationSkill.Create(new CreateRaisedStripsRequest
            {
                FilePath = filePath,
                Items = new List<RaisedStripItemRequest>
                {
                    new() { StartX = -1.1, StartY = -0.58, StartZ = 0.64, EndX = 1.1, EndY = -0.58, EndZ = 0.64, Width = 0.08, Height = 0.05, Name = "smoke raised seam" }
                },
                Common = common
            }), "CreateRaisedStrips")
        };

        GeneralPrimitiveKind[] expectedKinds =
        {
            GeneralPrimitiveKind.RoundedBox,
            GeneralPrimitiveKind.Ellipsoid,
            GeneralPrimitiveKind.Capsule,
            GeneralPrimitiveKind.Torus,
            GeneralPrimitiveKind.RaisedStrip
        };

        for (int i = 0; i < primitiveResponses.Count; i++)
        {
            RequireReferenceImageObjectModelingCreated(primitiveResponses[i], expectedKinds[i], smokeLayer);
        }

        string materialName = $"reference image object modeling smoke fabric {suffix}";
        RenderMaterialCreationResponse materials = RequireReferenceImageObjectModelingSuccess(_rhinoMaterialService.Create(new CreateRenderMaterialsRequest
        {
            FilePath = filePath,
            Items = new List<RenderMaterialItemRequest>
            {
                new()
                {
                    Name = materialName,
                    BaseColor = new ObjectColorRequest { R = 142, G = 92, B = 73 },
                    Roughness = 0.85,
                    Transparency = 0
                }
            }
        }), "CreateRenderMaterials");

        if (materials.CreatedCount != 1 || materials.Materials.Count != 1)
        {
            throw new InvalidOperationException($"Expected one created material, got CreatedCount={materials.CreatedCount}, Count={materials.Materials.Count}.");
        }

        List<Guid> createdObjectIds = primitiveResponses
            .SelectMany(response => response.CreatedObjects.Select(item => item.ObjectId))
            .ToList();
        ObjectMaterialAssignmentResponse assignment = RequireReferenceImageObjectModelingSuccess(_rhinoMaterialService.Apply(new ApplyObjectMaterialsRequest
        {
            FilePath = filePath,
            Assignments = new List<ObjectMaterialAssignmentRequest>
            {
                new()
                {
                    MaterialName = materialName,
                    ObjectIds = createdObjectIds
                }
            }
        }), "ApplyObjectMaterials");

        if (assignment.AssignedCount != createdObjectIds.Count || assignment.FailedCount != 0)
        {
            throw new InvalidOperationException($"Expected material assignment to cover {createdObjectIds.Count} objects, got Assigned={assignment.AssignedCount}, Failed={assignment.FailedCount}.");
        }

        Console.WriteLine("[OK] reference-image-object-modeling-tools live smoke completed.");
        Console.WriteLine($"[OK] Created {createdObjectIds.Count} soft/detail objects and assigned material {materialName} on layer {smokeLayer}.");
    }

    private static GeometryCreationCommonOptions ReferenceImageObjectModelingToolsCommon(string layerFullPath)
    {
        return new GeometryCreationCommonOptions
        {
            LayerFullPath = layerFullPath,
            Name = "mcp reference image object modeling tools smoke",
            Color = new ObjectColorRequest { R = 120, G = 130, B = 160 },
            UserText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mcp.smoke"] = "reference-image-object-modeling-tools"
            }
        };
    }

    private static void RequireReferenceImageObjectModelingLiveRequired<T>(OperationResponse<T> response, string label)
    {
        if (response.Success || !string.Equals(response.Message, "LIVE_RHINO_REQUIRED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} should return LIVE_RHINO_REQUIRED in CLI fallback mode; got Success={response.Success}, Message={response.Message}");
        }
    }

    private static T RequireReferenceImageObjectModelingSuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireReferenceImageObjectModelingCreated(
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

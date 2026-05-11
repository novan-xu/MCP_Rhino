using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Infrastructure.Plugin;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string ArchitecturalModelingPrimitivesSlug = "architectural-modeling-primitives-smoke-test";

    partial void RegisterArchitecturalModelingPrimitivesHandlers()
    {
        _extensionHandlers[ArchitecturalModelingPrimitivesSlug] = HandleArchitecturalModelingPrimitivesSmokeTest;
    }

    private bool HandleArchitecturalModelingPrimitivesSmokeTest(string[] args)
    {
        try
        {
            if (McpRhinoPlugin.Instance is null)
            {
                RunArchitecturalModelingPrimitivesCliFallbackSmoke();
            }
            else
            {
                string filePath = args.Length > 1 ? args[1] : string.Empty;
                RunArchitecturalModelingPrimitivesLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Architectural modeling primitives smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunArchitecturalModelingPrimitivesCliFallbackSmoke()
    {
        string filePath = "C:/mcp-rhino/architectural-smoke.3dm";
        GeometryCreationCommonOptions common = SmokeCommon("A-ARCH::SMOKE");
        var metadata = new ArchitecturalMetadataRequest { SourceTag = "cli-fallback-smoke" };

        RequireLiveRequired(_architecturalPrimitiveCreationSkill.CreateBoxes(new CreateBoxesRequest
        {
            FilePath = filePath,
            Items = new List<BoxItemRequest> { new() { SizeX = 1, SizeY = 1, SizeZ = 1 } },
            Common = common,
            Metadata = metadata
        }), "CreateBoxes");

        RequireLiveRequired(_architecturalPrimitiveCreationSkill.CreateExtrusions(new CreateExtrusionsRequest
        {
            FilePath = filePath,
            Items = new List<ExtrusionItemRequest> { new() { Height = 1, ProfilePoints = SmokeClosedFootprint() } },
            Common = common,
            Metadata = metadata
        }), "CreateExtrusions");

        RequireLiveRequired(_architecturalPrimitiveCreationSkill.CreatePlanarBreps(new CreatePlanarBrepsRequest
        {
            FilePath = filePath,
            Items = new List<PlanarBrepItemRequest> { new() { Loops = new List<PlanarLoopRequest> { new() { Points = SmokeClosedFootprint() } } } },
            Common = common,
            Metadata = metadata
        }), "CreatePlanarBreps");

        RequireLiveRequired(_architecturalPrimitiveCreationSkill.CreateSlabs(new CreateSlabsRequest
        {
            FilePath = filePath,
            Items = new List<SlabItemRequest> { new() { Footprint = SmokeClosedFootprint(), Thickness = 0.2 } },
            Common = common,
            Metadata = metadata
        }), "CreateSlabs");

        RequireLiveRequired(_architecturalPrimitiveCreationSkill.CreateColumns(new CreateColumnsRequest
        {
            FilePath = filePath,
            Items = new List<ColumnItemRequest> { new() { Width = 0.2, Depth = 0.2, Height = 2.5 } },
            Common = common,
            Metadata = metadata
        }), "CreateColumns");

        RequireLiveRequired(_architecturalPrimitiveCreationSkill.CreateWalls(new CreateWallsRequest
        {
            FilePath = filePath,
            Items = new List<WallItemRequest> { SmokeWall() },
            Common = common,
            Metadata = metadata
        }), "CreateWalls");

        RequireLiveRequired(_architecturalPrimitiveCreationSkill.CreateBeams(new CreateBeamsRequest
        {
            FilePath = filePath,
            Items = new List<BeamItemRequest> { new() { StartX = 0, StartY = 0, StartZ = 3, EndX = 4, EndY = 0, EndZ = 3, Width = 0.2, Depth = 0.3 } },
            Common = common,
            Metadata = metadata
        }), "CreateBeams");

        RequireLiveRequired(_architecturalBooleanSkill.Preview(new PreviewBooleanObjectsRequest
        {
            FilePath = filePath,
            Entries = new List<BooleanOperationEntryRequest> { SmokeBooleanEntry() }
        }), "PreviewBooleanObjects");

        RequireLiveRequired(_architecturalBooleanSkill.Apply(new ApplyBooleanObjectsRequest
        {
            FilePath = filePath,
            Entries = new List<BooleanOperationEntryRequest> { SmokeBooleanEntry() }
        }), "ApplyBooleanObjects");

        RequireLiveRequired(_architecturalBooleanSkill.PreviewOpenings(new PreviewOpeningsRequest
        {
            FilePath = filePath,
            Items = new List<OpeningItemRequest> { SmokeOpening(Guid.NewGuid()) }
        }), "PreviewOpenings");

        RequireLiveRequired(_architecturalBooleanSkill.ApplyOpenings(new ApplyOpeningsRequest
        {
            FilePath = filePath,
            Items = new List<OpeningItemRequest> { SmokeOpening(Guid.NewGuid()) },
            Common = common,
            Metadata = metadata
        }), "ApplyOpenings");

        RequireLiveRequired(_rhinoBlockDefinitionService.CreateDefinitions(filePath, new List<BlockDefinitionSpec>
        {
            new() { Name = "SmokeBlock", SourceObjectIds = new[] { Guid.NewGuid() } }
        }), "CreateBlockDefinitions");

        RequireLiveRequired(_rhinoBlockDefinitionService.InsertInstances(filePath, new List<BlockInstanceSpec>
        {
            new() { DefinitionName = "SmokeBlock" }
        }, ArchitecturalSmokeAttributeMapper.ArchitecturalObjectAttributesSpecFrom(common)), "InsertBlockInstances");

        Console.WriteLine("[OK] architectural-modeling-primitives CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.");
    }

    private void RunArchitecturalModelingPrimitivesLiveSmoke(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Live architectural smoke requires a saved active document path.");
        }

        GeometryCreationCommonOptions common = SmokeCommon("A-ARCH::SMOKE");
        var metadata = new ArchitecturalMetadataRequest { SourceTag = "live-smoke" };

        ArchitecturalCreationResponse boxes = RequireSuccess(_architecturalPrimitiveCreationSkill.CreateBoxes(new CreateBoxesRequest
        {
            FilePath = filePath,
            Items = new List<BoxItemRequest> { new() { OriginX = -2, OriginY = -2, OriginZ = 0, SizeX = 1, SizeY = 1, SizeZ = 1, Name = "smoke mass" } },
            Common = common,
            Metadata = new ArchitecturalMetadataRequest { Category = "massing", SourceTag = "live-smoke" },
            AutoCreateLayers = true
        }), "CreateBoxes");

        RequireSuccess(_architecturalPrimitiveCreationSkill.CreateSlabs(new CreateSlabsRequest
        {
            FilePath = filePath,
            Items = new List<SlabItemRequest> { new() { Footprint = SmokeClosedFootprint(), Thickness = 0.2, Name = "smoke slab" } },
            Common = common,
            Metadata = metadata,
            AutoCreateLayers = true
        }), "CreateSlabs");

        RequireSuccess(_architecturalPrimitiveCreationSkill.CreateColumns(new CreateColumnsRequest
        {
            FilePath = filePath,
            Items = new List<ColumnItemRequest>
            {
                new() { CenterX = 0.3, CenterY = 0.3, Width = 0.2, Depth = 0.2, Height = 2.5, Name = "smoke rectangular column" },
                new() { CenterX = 1.5, CenterY = 0.3, ProfileShape = ProfileShapeKind.Circular, Radius = 0.12, Height = 2.5, Name = "smoke circular column" }
            },
            Common = common,
            Metadata = metadata,
            AutoCreateLayers = true
        }), "CreateColumns");

        ArchitecturalCreationResponse wall = RequireSuccess(_architecturalPrimitiveCreationSkill.CreateWalls(new CreateWallsRequest
        {
            FilePath = filePath,
            Items = new List<WallItemRequest> { SmokeWall() },
            Common = common,
            Metadata = metadata,
            AutoCreateLayers = true
        }), "CreateWalls");

        RequireSuccess(_architecturalPrimitiveCreationSkill.CreateBeams(new CreateBeamsRequest
        {
            FilePath = filePath,
            Items = new List<BeamItemRequest> { new() { StartX = 0, StartY = 0, StartZ = 3, EndX = 4, EndY = 0, EndZ = 3, Width = 0.2, Depth = 0.3, Name = "smoke beam" } },
            Common = common,
            Metadata = metadata,
            AutoCreateLayers = true
        }), "CreateBeams");

        Guid wallId = wall.CreatedObjects.First().ObjectId;
        RequireSuccess(_architecturalBooleanSkill.PreviewOpenings(new PreviewOpeningsRequest
        {
            FilePath = filePath,
            Items = new List<OpeningItemRequest> { SmokeOpening(wallId) }
        }), "PreviewOpenings");

        RequireSuccess(_architecturalBooleanSkill.ApplyOpenings(new ApplyOpeningsRequest
        {
            FilePath = filePath,
            Items = new List<OpeningItemRequest> { SmokeOpening(wallId) },
            Common = common,
            Metadata = metadata
        }), "ApplyOpenings");

        string blockName = $"McpSmokeBlock_{DateTime.UtcNow:yyyyMMddHHmmss}";
        RequireSuccess(_rhinoBlockDefinitionService.CreateDefinitions(filePath, new List<BlockDefinitionSpec>
        {
            new() { Name = blockName, SourceObjectIds = new[] { boxes.CreatedObjects.First().ObjectId } }
        }), "CreateBlockDefinitions");

        RequireSuccess(_rhinoBlockDefinitionService.InsertInstances(filePath, new List<BlockInstanceSpec>
        {
            new() { DefinitionName = blockName, OriginX = 4, OriginY = 0, OriginZ = 0 },
            new() { DefinitionName = blockName, OriginX = 5, OriginY = 0, OriginZ = 0 }
        }, ArchitecturalSmokeAttributeMapper.ArchitecturalObjectAttributesSpecFrom(common)), "InsertBlockInstances");

        Console.WriteLine("[OK] architectural-modeling-primitives live smoke completed.");
    }

    private static GeometryCreationCommonOptions SmokeCommon(string layerFullPath)
    {
        return new GeometryCreationCommonOptions
        {
            LayerFullPath = layerFullPath,
            Name = "mcp architectural smoke"
        };
    }

    private static List<ArchitecturalPointRequest> SmokeClosedFootprint(double offsetX = 0d)
    {
        return new List<ArchitecturalPointRequest>
        {
            new() { X = offsetX, Y = 0, Z = 0 },
            new() { X = offsetX + 2, Y = 0, Z = 0 },
            new() { X = offsetX + 2, Y = 1, Z = 0 },
            new() { X = offsetX, Y = 1, Z = 0 },
            new() { X = offsetX, Y = 0, Z = 0 }
        };
    }

    private static WallItemRequest SmokeWall()
    {
        return new WallItemRequest
        {
            Baseline = new List<ArchitecturalPointRequest>
            {
                new() { X = 0, Y = 2, Z = 0 },
                new() { X = 4, Y = 2, Z = 0 }
            },
            Height = 3,
            Thickness = 0.3,
            Name = "smoke wall"
        };
    }

    private static OpeningItemRequest SmokeOpening(Guid targetObjectId)
    {
        return new OpeningItemRequest
        {
            TargetObjectId = targetObjectId,
            Kind = OpeningKind.Rectangular,
            CenterX = 2,
            CenterY = 2,
            CenterZ = 1.4,
            Width = 0.8,
            Height = 1.2,
            Depth = 1.0
        };
    }

    private static BooleanOperationEntryRequest SmokeBooleanEntry()
    {
        return new BooleanOperationEntryRequest
        {
            TargetObjectIds = new List<Guid> { Guid.NewGuid() },
            CutterObjectIds = new List<Guid> { Guid.NewGuid() }
        };
    }

    private static void RequireLiveRequired<T>(OperationResponse<T> response, string label)
    {
        if (response.Success || !string.Equals(response.Message, "LIVE_RHINO_REQUIRED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} should return LIVE_RHINO_REQUIRED in CLI fallback mode; got Success={response.Success}, Message={response.Message}");
        }
    }

    private static T RequireSuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }
}

internal static class ArchitecturalSmokeAttributeMapper
{
    public static ArchitecturalObjectAttributesSpec ArchitecturalObjectAttributesSpecFrom(GeometryCreationCommonOptions common)
    {
        return new ArchitecturalObjectAttributesSpec
        {
            LayerFullPath = common.LayerFullPath,
            Name = common.Name,
            UserText = new Dictionary<string, string>(common.UserText, StringComparer.OrdinalIgnoreCase),
            AutoCreateLayer = true
        };
    }
}

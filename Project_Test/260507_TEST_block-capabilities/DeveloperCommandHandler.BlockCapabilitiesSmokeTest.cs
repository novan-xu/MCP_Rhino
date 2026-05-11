using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.Plugin;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string BlockCapabilitiesSlug = "block-capabilities-smoke-test";

    partial void RegisterBlockCapabilitiesHandlers()
    {
        _extensionHandlers[BlockCapabilitiesSlug] = HandleBlockCapabilitiesSmokeTest;
    }

    private bool HandleBlockCapabilitiesSmokeTest(string[] args)
    {
        try
        {
            if (McpRhinoPlugin.Instance is null)
            {
                RunBlockCapabilitiesCliFallbackSmoke();
            }
            else
            {
                string filePath = args.Length > 1 ? args[1] : string.Empty;
                RunBlockCapabilitiesLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Block capabilities smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunBlockCapabilitiesCliFallbackSmoke()
    {
        string filePath = "C:/mcp-rhino/block-capabilities-smoke.3dm";
        Guid objectId = Guid.NewGuid();
        var common = new GeometryCreationCommonOptions { LayerFullPath = "A-BLOCK::SMOKE", Name = "mcp block smoke" };

        RequireBlockLiveRequired(_rhinoBlockInspectionService.ListDefinitions(new ListBlockDefinitionsRequest
        {
            FilePath = filePath
        }), "ListBlockDefinitions");

        RequireBlockLiveRequired(_rhinoBlockInspectionService.GetDefinitionDetails(new GetBlockDefinitionDetailsRequest
        {
            FilePath = filePath,
            DefinitionName = "SmokeBlock"
        }), "GetBlockDefinitionDetails");

        RequireBlockLiveRequired(_rhinoBlockInspectionService.ListInstances(new ListBlockInstancesRequest
        {
            FilePath = filePath
        }), "ListBlockInstances");

        RequireBlockLiveRequired(_rhinoBlockInspectionService.GetInstanceDetails(new GetBlockInstanceDetailsRequest
        {
            FilePath = filePath,
            ObjectId = objectId
        }), "GetBlockInstanceDetails");

        RequireBlockLiveRequired(_blockLifecycleSkill.PreviewCreate(new PreviewCreateBlockDefinitionsRequest
        {
            FilePath = filePath,
            Items = new List<BlockDefinitionSourceItemRequest> { SmokeDefinitionItem("SmokeBlock", objectId) }
        }), "PreviewCreateBlockDefinitions");

        RequireBlockLiveRequired(_blockLifecycleSkill.ApplyCreate(new ApplyCreateBlockDefinitionsRequest
        {
            FilePath = filePath,
            Items = new List<BlockDefinitionSourceItemRequest> { SmokeDefinitionItem("SmokeBlock", objectId) }
        }), "ApplyCreateBlockDefinitions");

        RequireBlockLiveRequired(_blockLifecycleSkill.PreviewInsert(new PreviewInsertBlockInstancesRequest
        {
            FilePath = filePath,
            Items = new List<BlockInstancePlacementRequest> { SmokePlacement("SmokeBlock") },
            Common = common,
            AutoCreateLayers = true
        }), "PreviewInsertBlockInstances");

        RequireBlockLiveRequired(_blockLifecycleSkill.ApplyInsert(new ApplyInsertBlockInstancesRequest
        {
            FilePath = filePath,
            Items = new List<BlockInstancePlacementRequest> { SmokePlacement("SmokeBlock") },
            Common = common,
            AutoCreateLayers = true
        }), "ApplyInsertBlockInstances");

        RequireBlockLiveRequired(_blockLifecycleSkill.PreviewTransform(new PreviewTransformBlockInstancesRequest
        {
            FilePath = filePath,
            Items = new List<BlockInstanceTransformRequest> { SmokeTransform(objectId) }
        }), "PreviewTransformBlockInstances");

        RequireBlockLiveRequired(_blockLifecycleSkill.ApplyTransform(new ApplyTransformBlockInstancesRequest
        {
            FilePath = filePath,
            Items = new List<BlockInstanceTransformRequest> { SmokeTransform(objectId) }
        }), "ApplyTransformBlockInstances");

        RequireBlockLiveRequired(_blockLifecycleSkill.PreviewExplode(new PreviewExplodeBlockInstancesRequest
        {
            FilePath = filePath,
            Items = new List<BlockExplodeInstanceRequest> { new() { ObjectId = objectId } }
        }), "PreviewExplodeBlockInstances");

        RequireBlockLiveRequired(_blockLifecycleSkill.ApplyExplode(new ApplyExplodeBlockInstancesRequest
        {
            FilePath = filePath,
            Items = new List<BlockExplodeInstanceRequest> { new() { ObjectId = objectId } }
        }), "ApplyExplodeBlockInstances");

        RequireBlockLiveRequired(_blockLifecycleSkill.PreviewPurge(new PreviewPurgeUnusedBlockDefinitionsRequest
        {
            FilePath = filePath,
            DefinitionNames = new List<string> { "SmokeBlock" },
            IncludeAllUnused = false
        }), "PreviewPurgeUnusedBlockDefinitions");

        RequireBlockLiveRequired(_blockLifecycleSkill.ApplyPurge(new ApplyPurgeUnusedBlockDefinitionsRequest
        {
            FilePath = filePath,
            DefinitionNames = new List<string> { "SmokeBlock" },
            IncludeAllUnused = false
        }), "ApplyPurgeUnusedBlockDefinitions");

        Console.WriteLine("[OK] block-capabilities CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.");
    }

    private void RunBlockCapabilitiesLiveSmoke(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Live block capabilities smoke requires a saved active document path.");
        }

        string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string blockName = $"McpBlockSmoke_{suffix}";
        string unusedBlockName = $"McpBlockSmokeUnused_{suffix}";
        var common = new GeometryCreationCommonOptions { LayerFullPath = "A-BLOCK::SMOKE", Name = "mcp block smoke" };

        ArchitecturalCreationResponse source = RequireBlockSuccess(_architecturalPrimitiveCreationSkill.CreateBoxes(new CreateBoxesRequest
        {
            FilePath = filePath,
            Items = new List<BoxItemRequest>
            {
                new()
                {
                    OriginX = 12,
                    OriginY = 0,
                    OriginZ = 0,
                    SizeX = 1,
                    SizeY = 1,
                    SizeZ = 1,
                    Name = "mcp block smoke source"
                }
            },
            Common = common,
            Metadata = new ArchitecturalMetadataRequest { Category = "block-source", SourceTag = "block-capabilities-live-smoke" },
            AutoCreateLayers = true
        }), "Create block source geometry");

        Guid sourceObjectId = source.CreatedObjects.First().ObjectId;
        var createItem = SmokeDefinitionItem(blockName, sourceObjectId);
        RequireBlockSuccess(_blockLifecycleSkill.PreviewCreate(new PreviewCreateBlockDefinitionsRequest
        {
            FilePath = filePath,
            Items = new List<BlockDefinitionSourceItemRequest> { createItem }
        }), "PreviewCreateBlockDefinitions");

        RequireBlockSuccess(_blockLifecycleSkill.ApplyCreate(new ApplyCreateBlockDefinitionsRequest
        {
            FilePath = filePath,
            Items = new List<BlockDefinitionSourceItemRequest> { createItem }
        }), "ApplyCreateBlockDefinitions");

        BlockDefinitionListResponse definitions = RequireBlockSuccess(_rhinoBlockInspectionService.ListDefinitions(new ListBlockDefinitionsRequest
        {
            FilePath = filePath,
            IncludeLinked = true,
            IncludeNestedSummary = true
        }), "ListBlockDefinitions");

        if (!definitions.Definitions.Any(definition => string.Equals(definition.Name, blockName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Created block definition was not listed: {blockName}");
        }

        RequireBlockSuccess(_rhinoBlockInspectionService.GetDefinitionDetails(new GetBlockDefinitionDetailsRequest
        {
            FilePath = filePath,
            DefinitionName = blockName
        }), "GetBlockDefinitionDetails");

        var placements = new List<BlockInstancePlacementRequest>
        {
            SmokePlacement(blockName, 14, 0, 0),
            SmokePlacement(blockName, 16, 0, 0)
        };

        RequireBlockSuccess(_blockLifecycleSkill.PreviewInsert(new PreviewInsertBlockInstancesRequest
        {
            FilePath = filePath,
            Items = placements,
            Common = common,
            AutoCreateLayers = true
        }), "PreviewInsertBlockInstances");

        BlockMutationApplyResponse insert = RequireBlockSuccess(_blockLifecycleSkill.ApplyInsert(new ApplyInsertBlockInstancesRequest
        {
            FilePath = filePath,
            Items = placements,
            Common = common,
            AutoCreateLayers = true
        }), "ApplyInsertBlockInstances");

        List<Guid> instanceIds = insert.Results.SelectMany(result => result.CreatedObjectIds).ToList();
        if (instanceIds.Count < 2)
        {
            throw new InvalidOperationException($"Expected at least two inserted block instances, got {instanceIds.Count}.");
        }

        BlockInstanceListResponse instances = RequireBlockSuccess(_rhinoBlockInspectionService.ListInstances(new ListBlockInstancesRequest
        {
            FilePath = filePath,
            DefinitionName = blockName
        }), "ListBlockInstances");

        if (instances.Instances.Count < 2)
        {
            throw new InvalidOperationException($"Expected at least two listed block instances for {blockName}, got {instances.Instances.Count}.");
        }

        RequireBlockSuccess(_rhinoBlockInspectionService.GetInstanceDetails(new GetBlockInstanceDetailsRequest
        {
            FilePath = filePath,
            ObjectId = instanceIds[0]
        }), "GetBlockInstanceDetails");

        var transformItem = SmokeTransform(instanceIds[0]);
        RequireBlockSuccess(_blockLifecycleSkill.PreviewTransform(new PreviewTransformBlockInstancesRequest
        {
            FilePath = filePath,
            Items = new List<BlockInstanceTransformRequest> { transformItem }
        }), "PreviewTransformBlockInstances");

        RequireBlockSuccess(_blockLifecycleSkill.ApplyTransform(new ApplyTransformBlockInstancesRequest
        {
            FilePath = filePath,
            Items = new List<BlockInstanceTransformRequest> { transformItem }
        }), "ApplyTransformBlockInstances");

        RequireBlockSuccess(_blockLifecycleSkill.PreviewExplode(new PreviewExplodeBlockInstancesRequest
        {
            FilePath = filePath,
            Items = new List<BlockExplodeInstanceRequest> { new() { ObjectId = instanceIds[0] } }
        }), "PreviewExplodeBlockInstances");

        RequireBlockSuccess(_blockLifecycleSkill.ApplyExplode(new ApplyExplodeBlockInstancesRequest
        {
            FilePath = filePath,
            Items = new List<BlockExplodeInstanceRequest> { new() { ObjectId = instanceIds[0] } }
        }), "ApplyExplodeBlockInstances");

        var unusedCreateItem = SmokeDefinitionItem(unusedBlockName, sourceObjectId);
        RequireBlockSuccess(_blockLifecycleSkill.ApplyCreate(new ApplyCreateBlockDefinitionsRequest
        {
            FilePath = filePath,
            Items = new List<BlockDefinitionSourceItemRequest> { unusedCreateItem }
        }), "ApplyCreateBlockDefinitions for unused definition");

        RequireBlockSuccess(_blockLifecycleSkill.PreviewPurge(new PreviewPurgeUnusedBlockDefinitionsRequest
        {
            FilePath = filePath,
            DefinitionNames = new List<string> { unusedBlockName },
            IncludeAllUnused = false
        }), "PreviewPurgeUnusedBlockDefinitions");

        RequireBlockSuccess(_blockLifecycleSkill.ApplyPurge(new ApplyPurgeUnusedBlockDefinitionsRequest
        {
            FilePath = filePath,
            DefinitionNames = new List<string> { unusedBlockName },
            IncludeAllUnused = false
        }), "ApplyPurgeUnusedBlockDefinitions");

        BlockDefinitionListResponse afterPurge = RequireBlockSuccess(_rhinoBlockInspectionService.ListDefinitions(new ListBlockDefinitionsRequest
        {
            FilePath = filePath,
            IncludeLinked = true
        }), "ListBlockDefinitions after purge");

        if (afterPurge.Definitions.Any(definition => string.Equals(definition.Name, unusedBlockName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Purged block definition is still active: {unusedBlockName}");
        }

        Console.WriteLine("[OK] block-capabilities live smoke completed.");
    }

    private static BlockDefinitionSourceItemRequest SmokeDefinitionItem(string name, Guid sourceObjectId)
    {
        return new BlockDefinitionSourceItemRequest
        {
            Name = name,
            Description = "MCP block capabilities smoke definition",
            SourceObjectIds = new List<Guid> { sourceObjectId },
            BasePoint = new BlockPointRequest()
        };
    }

    private static BlockInstancePlacementRequest SmokePlacement(string definitionName, double x = 0d, double y = 0d, double z = 0d)
    {
        return new BlockInstancePlacementRequest
        {
            DefinitionName = definitionName,
            Origin = new BlockPointRequest { X = x, Y = y, Z = z },
            RotationDegrees = 0d,
            Scale = 1d
        };
    }

    private static BlockInstanceTransformRequest SmokeTransform(Guid objectId)
    {
        return new BlockInstanceTransformRequest
        {
            ObjectId = objectId,
            TranslationX = 0.5,
            TranslationY = 0.25,
            TranslationZ = 0,
            RotationDegrees = 10,
            Scale = 1
        };
    }

    private static void RequireBlockLiveRequired<T>(OperationResponse<T> response, string label)
    {
        if (response.Success || !string.Equals(response.Message, "LIVE_RHINO_REQUIRED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} should return LIVE_RHINO_REQUIRED in CLI fallback mode; got Success={response.Success}, Message={response.Message}");
        }
    }

    private static T RequireBlockSuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }
}

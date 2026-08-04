extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using InstanceDefinition = rhinocommon::Rhino.DocObjects.InstanceDefinition;
using InstanceObject = rhinocommon::Rhino.DocObjects.InstanceObject;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Transform = rhinocommon::Rhino.Geometry.Transform;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveBlockLifecycleOperator : ILiveBlockLifecycleOperator
{
    private const string CreateOperation = "CreateBlockDefinitions";
    private const string InsertOperation = "InsertBlockInstances";
    private const string TransformOperation = "TransformBlockInstances";
    private const string ExplodeOperation = "ExplodeBlockInstances";
    private const string PurgeOperation = "PurgeUnusedBlockDefinitions";

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveBlockLifecycleOperator(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewCreateDefinitions(
        string filePath,
        IReadOnlyList<BlockDefinitionCreationSpec> specs)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            OperationResponse<IReadOnlyList<PreparedDefinitionCreation>> prepared = PrepareDefinitionCreations(document, specs);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<BlockMutationPreviewResponse>.Fail(prepared.Message);
            }

            var results = prepared.Data.Select(item => new BlockOperationPreviewItemResponse
            {
                DefinitionName = item.FinalName,
                Success = true,
                Message = item.FinalName == item.Spec.Name
                    ? "Block definition can be created."
                    : $"Block definition can be created with versioned name [{item.FinalName}].",
                Impact = new BlockImpactResponse
                {
                    SourceObjectCount = item.Spec.SourceObjectIds.Count,
                    ObjectCount = item.Spec.SourceObjectIds.Count
                }
            }).ToList();

            return OperationResponse<BlockMutationPreviewResponse>.Ok(CreatePreviewResponse(
                filePath,
                CreateOperation,
                specs.Count,
                results,
                results.Count), "Block definition creation preview completed.");
        });
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyCreateDefinitions(
        string filePath,
        IReadOnlyList<BlockDefinitionCreationSpec> specs)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: ApplyCreateBlockDefinitions", document =>
        {
            OperationResponse<IReadOnlyList<PreparedDefinitionCreation>> prepared = PrepareDefinitionCreations(document, specs);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail(prepared.Message);
            }

            var results = new List<BlockOperationResultResponse>(prepared.Data.Count);
            int createdCount = 0;
            int hiddenSourceCount = 0;

            foreach (PreparedDefinitionCreation item in prepared.Data)
            {
                int index = document.InstanceDefinitions.Add(
                    item.FinalName,
                    item.Spec.Description,
                    new Point3d(item.Spec.BasePoint.X, item.Spec.BasePoint.Y, item.Spec.BasePoint.Z),
                    item.Geometry,
                    item.Attributes);

                if (index < 0)
                {
                    return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail($"Failed to create block definition: {item.FinalName}");
                }

                createdCount++;
                if (item.Spec.SourceObjectPolicy == BlockSourceObjectPolicy.HideSourceObjects)
                {
                    hiddenSourceCount += HideSourceObjects(document, item.Spec.SourceObjectIds);
                }

                InstanceDefinition? definition = document.InstanceDefinitions.Find(item.FinalName);
                results.Add(new BlockOperationResultResponse
                {
                    DefinitionId = definition?.Id ?? Guid.Empty,
                    DefinitionIndex = index,
                    DefinitionName = item.FinalName,
                    Success = true,
                    Message = "Created block definition."
                });
            }

            if (createdCount > 0 || hiddenSourceCount > 0)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Ok(
                (createdCount > 0 || hiddenSourceCount > 0, new BlockMutationApplyResponse
                {
                    FilePath = filePath,
                    Operation = CreateOperation,
                    RequestedCount = specs.Count,
                    ChangedCount = createdCount,
                    CreatedCount = createdCount,
                    FailedCount = results.Count(result => !result.Success),
                    Results = results,
                    Warnings = hiddenSourceCount > 0
                        ? new[]
                        {
                            new ObjectEditWarning
                            {
                                Code = "SOURCE_OBJECTS_HIDDEN",
                                Message = $"Hidden source objects: {hiddenSourceCount}"
                            }
                        }
                        : Array.Empty<ObjectEditWarning>()
                }),
                "Block definition creation applied.");
        });
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewInsertInstances(
        string filePath,
        IReadOnlyList<BlockInstancePlacementSpec> specs,
        ArchitecturalObjectAttributesSpec attributes)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            OperationResponse<PreparedInsertBatch> prepared = PrepareInsertBatch(document, specs, attributes);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<BlockMutationPreviewResponse>.Fail(prepared.Message);
            }

            var results = prepared.Data.Items.Select(item => new BlockOperationPreviewItemResponse
            {
                DefinitionId = item.Definition.Id,
                DefinitionIndex = item.Definition.Index,
                DefinitionName = item.Definition.Name ?? item.Spec.DefinitionName,
                Success = true,
                Message = "Block instance can be inserted.",
                Impact = new BlockImpactResponse { CreatedObjectCount = 1 }
            }).ToList();

            return OperationResponse<BlockMutationPreviewResponse>.Ok(CreatePreviewResponse(
                filePath,
                InsertOperation,
                specs.Count,
                results,
                results.Count), "Block instance insertion preview completed.");
        });
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyInsertInstances(
        string filePath,
        IReadOnlyList<BlockInstancePlacementSpec> specs,
        ArchitecturalObjectAttributesSpec attributes)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: ApplyInsertBlockInstances", document =>
        {
            OperationResponse<PreparedInsertBatch> prepared = PrepareInsertBatch(document, specs, attributes);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail(prepared.Message);
            }

            var results = new List<BlockOperationResultResponse>(prepared.Data.Items.Count);
            foreach (PreparedInsert item in prepared.Data.Items)
            {
                ObjectAttributes objectAttributes = LiveArchitecturalGeometryBuilder.CreateAttributes(
                    prepared.Data.LayerIndex,
                    attributes,
                    new ArchitecturalPrimitiveSpec
                    {
                        Kind = ArchitecturalPrimitiveKind.Box,
                        Name = attributes.Name,
                        Metadata = new ArchitecturalMetadataSpec { Category = "block-instance" }
                    });

                Guid objectId = document.Objects.AddInstanceObject(
                    item.Definition.Index,
                    BuildPlacementTransform(item.Spec),
                    objectAttributes);

                if (objectId == Guid.Empty)
                {
                    return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail($"Failed to insert block instance: {item.Spec.DefinitionName}");
                }

                results.Add(new BlockOperationResultResponse
                {
                    TargetObjectId = objectId,
                    DefinitionId = item.Definition.Id,
                    DefinitionIndex = item.Definition.Index,
                    DefinitionName = item.Definition.Name ?? item.Spec.DefinitionName,
                    CreatedObjectIds = new[] { objectId },
                    Success = true,
                    Message = "Inserted block instance."
                });
            }

            if (results.Count > 0)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Ok(
                (results.Count > 0, new BlockMutationApplyResponse
                {
                    FilePath = filePath,
                    Operation = InsertOperation,
                    RequestedCount = specs.Count,
                    ChangedCount = results.Count,
                    CreatedCount = results.Count,
                    FailedCount = results.Count(result => !result.Success),
                    Results = results
                }),
                "Block instance insertion applied.");
        });
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewTransformInstances(
        string filePath,
        IReadOnlyList<BlockInstanceTransformSpec> specs)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            OperationResponse<IReadOnlyList<PreparedTransform>> prepared = PrepareTransforms(document, specs);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<BlockMutationPreviewResponse>.Fail(prepared.Message);
            }

            var results = prepared.Data.Select(item => new BlockOperationPreviewItemResponse
            {
                TargetObjectId = item.Spec.ObjectId,
                DefinitionId = item.Definition.Id,
                DefinitionIndex = item.Definition.Index,
                DefinitionName = item.Definition.Name ?? string.Empty,
                Success = true,
                Message = "Block instance can be transformed."
            }).ToList();

            return OperationResponse<BlockMutationPreviewResponse>.Ok(CreatePreviewResponse(
                filePath,
                TransformOperation,
                specs.Count,
                results,
                results.Count), "Block instance transform preview completed.");
        });
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyTransformInstances(
        string filePath,
        IReadOnlyList<BlockInstanceTransformSpec> specs)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: ApplyTransformBlockInstances", document =>
        {
            OperationResponse<IReadOnlyList<PreparedTransform>> prepared = PrepareTransforms(document, specs);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail(prepared.Message);
            }

            var results = new List<BlockOperationResultResponse>(prepared.Data.Count);
            foreach (PreparedTransform item in prepared.Data)
            {
                GeometryBase? geometry = item.Instance.Geometry?.Duplicate();
                if (geometry is null)
                {
                    return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail($"Block instance geometry is empty: {item.Spec.ObjectId}");
                }

                if (!geometry.Transform(item.Transform))
                {
                    return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail($"Transform failed: {item.Spec.ObjectId}");
                }

                if (!document.Objects.Replace(item.Spec.ObjectId, geometry, false))
                {
                    return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail($"Replace after transform failed: {item.Spec.ObjectId}");
                }

                results.Add(new BlockOperationResultResponse
                {
                    TargetObjectId = item.Spec.ObjectId,
                    DefinitionId = item.Definition.Id,
                    DefinitionIndex = item.Definition.Index,
                    DefinitionName = item.Definition.Name ?? string.Empty,
                    Success = true,
                    Message = "Transformed block instance."
                });
            }

            if (results.Count > 0)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Ok(
                (results.Count > 0, new BlockMutationApplyResponse
                {
                    FilePath = filePath,
                    Operation = TransformOperation,
                    RequestedCount = specs.Count,
                    ChangedCount = results.Count,
                    FailedCount = results.Count(result => !result.Success),
                    Results = results
                }),
                "Block instance transform applied.");
        });
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewExplodeInstances(
        string filePath,
        IReadOnlyList<BlockExplodeSpec> specs)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            OperationResponse<IReadOnlyList<PreparedExplode>> prepared = PrepareExplodes(document, specs);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<BlockMutationPreviewResponse>.Fail(prepared.Message);
            }

            var results = prepared.Data.Select(item => new BlockOperationPreviewItemResponse
            {
                TargetObjectId = item.Spec.ObjectId,
                DefinitionId = item.Definition.Id,
                DefinitionIndex = item.Definition.Index,
                DefinitionName = item.Definition.Name ?? string.Empty,
                Success = true,
                Message = $"Block instance can be exploded into {item.Pieces.Count} objects.",
                Impact = new BlockImpactResponse
                {
                    CreatedObjectCount = item.Pieces.Count,
                    DeletedObjectCount = 1
                }
            }).ToList();

            return OperationResponse<BlockMutationPreviewResponse>.Ok(CreatePreviewResponse(
                filePath,
                ExplodeOperation,
                specs.Count,
                results,
                results.Count), "Block instance explode preview completed.");
        });
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyExplodeInstances(
        string filePath,
        IReadOnlyList<BlockExplodeSpec> specs)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: ApplyExplodeBlockInstances", document =>
        {
            OperationResponse<IReadOnlyList<PreparedExplode>> prepared = PrepareExplodes(document, specs);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail(prepared.Message);
            }

            var results = new List<BlockOperationResultResponse>(prepared.Data.Count);
            int createdCount = 0;
            int deletedCount = 0;

            foreach (PreparedExplode item in prepared.Data)
            {
                var createdIds = new List<Guid>(item.Pieces.Count);
                foreach (PreparedExplodePiece piece in item.Pieces)
                {
                    Guid createdId = document.Objects.Add(piece.Geometry, piece.Attributes);
                    if (createdId == Guid.Empty)
                    {
                        return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail($"Failed to add exploded piece for block instance: {item.Spec.ObjectId}");
                    }

                    createdIds.Add(createdId);
                }

                if (!document.Objects.Delete(item.Spec.ObjectId, true))
                {
                    return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail($"Failed to delete exploded block instance: {item.Spec.ObjectId}");
                }

                createdCount += createdIds.Count;
                deletedCount++;
                results.Add(new BlockOperationResultResponse
                {
                    TargetObjectId = item.Spec.ObjectId,
                    DefinitionId = item.Definition.Id,
                    DefinitionIndex = item.Definition.Index,
                    DefinitionName = item.Definition.Name ?? string.Empty,
                    CreatedObjectIds = createdIds,
                    DeletedObjectIds = new[] { item.Spec.ObjectId },
                    Success = true,
                    Message = $"Exploded block instance into {createdIds.Count} objects."
                });
            }

            if (createdCount > 0 || deletedCount > 0)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Ok(
                (createdCount > 0 || deletedCount > 0, new BlockMutationApplyResponse
                {
                    FilePath = filePath,
                    Operation = ExplodeOperation,
                    RequestedCount = specs.Count,
                    ChangedCount = results.Count,
                    CreatedCount = createdCount,
                    DeletedCount = deletedCount,
                    FailedCount = results.Count(result => !result.Success),
                    Results = results
                }),
                "Block instance explode applied.");
        });
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewPurgeUnusedDefinitions(
        string filePath,
        BlockPurgeSpec spec)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            OperationResponse<IReadOnlyList<PreparedPurge>> prepared = PreparePurge(document, spec);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<BlockMutationPreviewResponse>.Fail(prepared.Message);
            }

            var results = prepared.Data.Select(item => new BlockOperationPreviewItemResponse
            {
                DefinitionId = item.Definition.Id,
                DefinitionIndex = item.Definition.Index,
                DefinitionName = item.Definition.Name ?? string.Empty,
                Success = true,
                Message = "Unused local block definition can be purged.",
                Impact = new BlockImpactResponse
                {
                    ObjectCount = item.Definition.ObjectCount
                }
            }).ToList();

            return OperationResponse<BlockMutationPreviewResponse>.Ok(CreatePreviewResponse(
                filePath,
                PurgeOperation,
                RequestedPurgeCount(document, spec),
                results,
                results.Count), "Unused block definition purge preview completed.");
        });
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyPurgeUnusedDefinitions(
        string filePath,
        BlockPurgeSpec spec)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: ApplyPurgeUnusedBlockDefinitions", document =>
        {
            OperationResponse<IReadOnlyList<PreparedPurge>> prepared = PreparePurge(document, spec);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail(prepared.Message);
            }

            var results = new List<BlockOperationResultResponse>(prepared.Data.Count);
            foreach (PreparedPurge item in prepared.Data)
            {
                bool purged = document.InstanceDefinitions.Purge(item.Definition.Index);
                if (!purged && !document.InstanceDefinitions.Delete(item.Definition.Index, false, true))
                {
                    return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Fail($"Failed to purge block definition: {item.Definition.Name}");
                }

                results.Add(new BlockOperationResultResponse
                {
                    DefinitionId = item.Definition.Id,
                    DefinitionIndex = item.Definition.Index,
                    DefinitionName = item.Definition.Name ?? string.Empty,
                    Success = true,
                    Message = purged
                        ? "Purged unused local block definition."
                        : "Deleted unused local block definition after Rhino purge was blocked by undo references."
                });
            }

            if (results.Count > 0)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, BlockMutationApplyResponse Result)>.Ok(
                (results.Count > 0, new BlockMutationApplyResponse
                {
                    FilePath = filePath,
                    Operation = PurgeOperation,
                    RequestedCount = RequestedPurgeCount(document, spec),
                    ChangedCount = results.Count,
                    DeletedCount = results.Count,
                    FailedCount = results.Count(result => !result.Success),
                    Results = results
                }),
                "Unused block definition purge applied.");
        });
    }

    private static OperationResponse<IReadOnlyList<PreparedDefinitionCreation>> PrepareDefinitionCreations(
        RhinoDoc document,
        IReadOnlyList<BlockDefinitionCreationSpec> specs)
    {
        var prepared = new List<PreparedDefinitionCreation>(specs.Count);
        var namesInRequest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (BlockDefinitionCreationSpec spec in specs)
        {
            if (string.IsNullOrWhiteSpace(spec.Name))
            {
                return OperationResponse<IReadOnlyList<PreparedDefinitionCreation>>.Fail("Block definition name is required.");
            }

            string requestedName = spec.Name.Trim();
            if (!namesInRequest.Add(requestedName) && spec.DuplicateDefinitionPolicy == BlockDuplicateDefinitionPolicy.Reject)
            {
                return OperationResponse<IReadOnlyList<PreparedDefinitionCreation>>.Fail($"Duplicate block definition name in request: {requestedName}");
            }

            if (spec.SourceObjectIds.Count == 0)
            {
                return OperationResponse<IReadOnlyList<PreparedDefinitionCreation>>.Fail($"Block definition [{requestedName}] requires source object ids.");
            }

            if (spec.SourceObjectPolicy is not BlockSourceObjectPolicy.KeepVisible and not BlockSourceObjectPolicy.HideSourceObjects)
            {
                return OperationResponse<IReadOnlyList<PreparedDefinitionCreation>>.Fail($"Unsupported source object policy for v1: {spec.SourceObjectPolicy}");
            }

            string finalName = requestedName;
            InstanceDefinition? existing = document.InstanceDefinitions.Find(requestedName);
            if (existing is not null && !existing.IsDeleted)
            {
                if (spec.DuplicateDefinitionPolicy == BlockDuplicateDefinitionPolicy.Reject)
                {
                    return OperationResponse<IReadOnlyList<PreparedDefinitionCreation>>.Fail($"Block definition already exists: {requestedName}");
                }

                if (spec.DuplicateDefinitionPolicy == BlockDuplicateDefinitionPolicy.VersionedName)
                {
                    finalName = document.InstanceDefinitions.GetUnusedInstanceDefinitionName(requestedName);
                }
                else
                {
                    return OperationResponse<IReadOnlyList<PreparedDefinitionCreation>>.Fail($"Unsupported duplicate definition policy for v1: {spec.DuplicateDefinitionPolicy}");
                }
            }

            var geometry = new List<GeometryBase>(spec.SourceObjectIds.Count);
            var attributes = new List<ObjectAttributes>(spec.SourceObjectIds.Count);
            foreach (Guid objectId in spec.SourceObjectIds)
            {
                RhinoObject? source = document.Objects.FindId(objectId);
                if (source is null || source.IsDeleted || source.Geometry is null)
                {
                    return OperationResponse<IReadOnlyList<PreparedDefinitionCreation>>.Fail($"Source object not found for block definition [{requestedName}]: {objectId}");
                }

                if (source.IsReference)
                {
                    return OperationResponse<IReadOnlyList<PreparedDefinitionCreation>>.Fail($"Reference objects cannot be used as block definition source objects: {objectId}");
                }

                if (spec.SourceObjectPolicy == BlockSourceObjectPolicy.HideSourceObjects && source.IsLocked)
                {
                    return OperationResponse<IReadOnlyList<PreparedDefinitionCreation>>.Fail($"Locked source objects cannot be hidden by block creation: {objectId}");
                }

                GeometryBase? duplicate = source.Geometry.Duplicate();
                if (duplicate is null)
                {
                    return OperationResponse<IReadOnlyList<PreparedDefinitionCreation>>.Fail($"Failed to duplicate source geometry for block definition [{requestedName}]: {objectId}");
                }

                geometry.Add(duplicate);
                attributes.Add(source.Attributes.Duplicate());
            }

            prepared.Add(new PreparedDefinitionCreation(spec, finalName, geometry, attributes));
        }

        return OperationResponse<IReadOnlyList<PreparedDefinitionCreation>>.Ok(prepared);
    }

    private static OperationResponse<PreparedInsertBatch> PrepareInsertBatch(
        RhinoDoc document,
        IReadOnlyList<BlockInstancePlacementSpec> specs,
        ArchitecturalObjectAttributesSpec attributes)
    {
        int layerIndex = ResolveLayerIndex(document, attributes.LayerFullPath, attributes.AutoCreateLayer);
        if (layerIndex < 0)
        {
            return OperationResponse<PreparedInsertBatch>.Fail($"Target layer was not found: {attributes.LayerFullPath}");
        }

        var items = new List<PreparedInsert>(specs.Count);
        foreach (BlockInstancePlacementSpec spec in specs)
        {
            if (string.IsNullOrWhiteSpace(spec.DefinitionName))
            {
                return OperationResponse<PreparedInsertBatch>.Fail("DefinitionName is required for block instance insertion.");
            }

            if (spec.Scale <= 0d)
            {
                return OperationResponse<PreparedInsertBatch>.Fail($"Scale must be greater than zero for block instance [{spec.DefinitionName}].");
            }

            InstanceDefinition? definition = document.InstanceDefinitions.Find(spec.DefinitionName);
            if (definition is null || definition.IsDeleted)
            {
                return OperationResponse<PreparedInsertBatch>.Fail($"Block definition not found: {spec.DefinitionName}");
            }

            items.Add(new PreparedInsert(spec, definition));
        }

        return OperationResponse<PreparedInsertBatch>.Ok(new PreparedInsertBatch(layerIndex, items));
    }

    private static OperationResponse<IReadOnlyList<PreparedTransform>> PrepareTransforms(
        RhinoDoc document,
        IReadOnlyList<BlockInstanceTransformSpec> specs)
    {
        var prepared = new List<PreparedTransform>(specs.Count);
        foreach (BlockInstanceTransformSpec spec in specs)
        {
            if (spec.ObjectId == Guid.Empty)
            {
                return OperationResponse<IReadOnlyList<PreparedTransform>>.Fail("ObjectId is required for block instance transform.");
            }

            if (spec.Scale <= 0d)
            {
                return OperationResponse<IReadOnlyList<PreparedTransform>>.Fail($"Scale must be greater than zero for block instance transform: {spec.ObjectId}");
            }

            RhinoObject? rhinoObject = document.Objects.FindId(spec.ObjectId);
            if (rhinoObject is not InstanceObject instanceObject || rhinoObject.IsDeleted)
            {
                return OperationResponse<IReadOnlyList<PreparedTransform>>.Fail($"Block instance not found: {spec.ObjectId}");
            }

            if (rhinoObject.IsLocked || rhinoObject.IsReference)
            {
                return OperationResponse<IReadOnlyList<PreparedTransform>>.Fail($"Locked or reference block instances cannot be transformed: {spec.ObjectId}");
            }

            InstanceDefinition? definition = instanceObject.InstanceDefinition;
            if (definition is null || definition.IsDeleted)
            {
                return OperationResponse<IReadOnlyList<PreparedTransform>>.Fail($"Block instance definition not found for object: {spec.ObjectId}");
            }

            prepared.Add(new PreparedTransform(spec, instanceObject, definition, BuildTransform(spec)));
        }

        return OperationResponse<IReadOnlyList<PreparedTransform>>.Ok(prepared);
    }

    private static OperationResponse<IReadOnlyList<PreparedExplode>> PrepareExplodes(
        RhinoDoc document,
        IReadOnlyList<BlockExplodeSpec> specs)
    {
        var prepared = new List<PreparedExplode>(specs.Count);
        foreach (BlockExplodeSpec spec in specs)
        {
            if (spec.ObjectId == Guid.Empty)
            {
                return OperationResponse<IReadOnlyList<PreparedExplode>>.Fail("ObjectId is required for block instance explode.");
            }

            RhinoObject? rhinoObject = document.Objects.FindId(spec.ObjectId);
            if (rhinoObject is not InstanceObject instanceObject || rhinoObject.IsDeleted)
            {
                return OperationResponse<IReadOnlyList<PreparedExplode>>.Fail($"Block instance not found: {spec.ObjectId}");
            }

            if (rhinoObject.IsLocked || rhinoObject.IsReference)
            {
                return OperationResponse<IReadOnlyList<PreparedExplode>>.Fail($"Locked or reference block instances cannot be exploded: {spec.ObjectId}");
            }

            InstanceDefinition? definition = instanceObject.InstanceDefinition;
            if (definition is null || definition.IsDeleted)
            {
                return OperationResponse<IReadOnlyList<PreparedExplode>>.Fail($"Block instance definition not found for object: {spec.ObjectId}");
            }

            if (!spec.ExplodeNestedInstances && LiveBlockInspector.HasNestedDefinitions(definition))
            {
                return OperationResponse<IReadOnlyList<PreparedExplode>>.Fail($"Nested block instance explode requires ExplodeNestedInstances=true: {spec.ObjectId}");
            }

            instanceObject.Explode(
                spec.SkipHiddenPieces,
                Guid.Empty,
                spec.ExplodeNestedInstances,
                out RhinoObject[] pieces,
                out ObjectAttributes[] pieceAttributes,
                out Transform[] pieceTransforms);

            if (pieces.Length == 0)
            {
                return OperationResponse<IReadOnlyList<PreparedExplode>>.Fail($"Block instance produced no exploded pieces: {spec.ObjectId}");
            }

            if (pieces.Length != pieceAttributes.Length || pieces.Length != pieceTransforms.Length)
            {
                return OperationResponse<IReadOnlyList<PreparedExplode>>.Fail($"Block instance explode returned inconsistent piece data: {spec.ObjectId}");
            }

            var preparedPieces = new List<PreparedExplodePiece>(pieces.Length);
            for (int i = 0; i < pieces.Length; i++)
            {
                GeometryBase? geometry = pieces[i].Geometry?.Duplicate();
                if (geometry is null)
                {
                    return OperationResponse<IReadOnlyList<PreparedExplode>>.Fail($"Exploded piece geometry is empty for block instance: {spec.ObjectId}");
                }

                if (!geometry.Transform(pieceTransforms[i]))
                {
                    return OperationResponse<IReadOnlyList<PreparedExplode>>.Fail($"Failed to transform exploded piece for block instance: {spec.ObjectId}");
                }

                ObjectAttributes attributes = pieceAttributes[i].Duplicate();
                attributes.ObjectId = Guid.Empty;
                preparedPieces.Add(new PreparedExplodePiece(geometry, attributes));
            }

            prepared.Add(new PreparedExplode(spec, definition, preparedPieces));
        }

        return OperationResponse<IReadOnlyList<PreparedExplode>>.Ok(prepared);
    }

    private static OperationResponse<IReadOnlyList<PreparedPurge>> PreparePurge(RhinoDoc document, BlockPurgeSpec spec)
    {
        if (spec.Policy != BlockPurgePolicy.UnusedLocalOnly)
        {
            return OperationResponse<IReadOnlyList<PreparedPurge>>.Fail($"Unsupported block purge policy: {spec.Policy}");
        }

        var requestedNames = spec.DefinitionNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (requestedNames.Count == 0 && !spec.IncludeAllUnused)
        {
            return OperationResponse<IReadOnlyList<PreparedPurge>>.Fail("DefinitionNames are required when IncludeAllUnused is false.");
        }

        var foundNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<PreparedPurge>();
        foreach (InstanceDefinition definition in LiveBlockInspector.ActiveDefinitions(document))
        {
            bool explicitlyRequested = requestedNames.Contains(definition.Name ?? string.Empty);
            if (!spec.IncludeAllUnused && !explicitlyRequested)
            {
                continue;
            }

            if (requestedNames.Count > 0 && !spec.IncludeAllUnused && !explicitlyRequested)
            {
                continue;
            }

            if (explicitlyRequested)
            {
                foundNames.Add(definition.Name ?? string.Empty);
            }

            BlockLinkStatus linkStatus = LiveBlockInspector.ToLinkStatus(definition);
            if (linkStatus != BlockLinkStatus.Local)
            {
                if (explicitlyRequested)
                {
                    return OperationResponse<IReadOnlyList<PreparedPurge>>.Fail($"Only unused local block definitions can be purged in v1: {definition.Name}");
                }

                continue;
            }

            definition.UseCount(out int topLevelCount, out int nestedCount);
            if (topLevelCount > 0 || nestedCount > 0)
            {
                if (explicitlyRequested)
                {
                    return OperationResponse<IReadOnlyList<PreparedPurge>>.Fail($"Block definition is still in use and cannot be purged: {definition.Name}");
                }

                continue;
            }

            candidates.Add(new PreparedPurge(definition));
        }

        foreach (string requestedName in requestedNames)
        {
            if (!foundNames.Contains(requestedName))
            {
                return OperationResponse<IReadOnlyList<PreparedPurge>>.Fail($"Block definition not found: {requestedName}");
            }
        }

        return OperationResponse<IReadOnlyList<PreparedPurge>>.Ok(candidates);
    }

    private static int HideSourceObjects(RhinoDoc document, IReadOnlyList<Guid> sourceObjectIds)
    {
        int hidden = 0;
        foreach (Guid objectId in sourceObjectIds)
        {
            RhinoObject? source = document.Objects.FindId(objectId);
            if (source is null || source.IsDeleted)
            {
                continue;
            }

            ObjectAttributes attributes = source.Attributes.Duplicate();
            if (!attributes.Visible)
            {
                continue;
            }

            attributes.Visible = false;
            if (document.Objects.ModifyAttributes(objectId, attributes, true))
            {
                hidden++;
            }
        }

        return hidden;
    }

    private static Transform BuildPlacementTransform(BlockInstancePlacementSpec spec)
    {
        Transform transform = Transform.Identity;
        if (Math.Abs(spec.Scale - 1d) > double.Epsilon)
        {
            transform = Transform.Scale(Point3d.Origin, spec.Scale) * transform;
        }

        if (Math.Abs(spec.RotationDegrees) > double.Epsilon)
        {
            transform = Transform.Rotation(spec.RotationDegrees * Math.PI / 180d, Vector3d.ZAxis, Point3d.Origin) * transform;
        }

        return Transform.Translation(spec.Origin.X, spec.Origin.Y, spec.Origin.Z) * transform;
    }

    private static Transform BuildTransform(BlockInstanceTransformSpec spec)
    {
        Transform transform = Transform.Identity;
        if (Math.Abs(spec.Scale - 1d) > double.Epsilon)
        {
            transform = Transform.Scale(Point3d.Origin, spec.Scale) * transform;
        }

        if (Math.Abs(spec.RotationDegrees) > double.Epsilon)
        {
            transform = Transform.Rotation(spec.RotationDegrees * Math.PI / 180d, Vector3d.ZAxis, Point3d.Origin) * transform;
        }

        if (Math.Abs(spec.TranslationX) > double.Epsilon
            || Math.Abs(spec.TranslationY) > double.Epsilon
            || Math.Abs(spec.TranslationZ) > double.Epsilon)
        {
            transform = Transform.Translation(spec.TranslationX, spec.TranslationY, spec.TranslationZ) * transform;
        }

        return transform;
    }

    private static int ResolveLayerIndex(RhinoDoc document, string layerFullPath, bool autoCreate)
    {
        if (string.IsNullOrWhiteSpace(layerFullPath))
        {
            return document.Layers.CurrentLayerIndex;
        }

        int layerIndex = document.Layers.FindByFullPath(layerFullPath, -1);
        if (layerIndex >= 0 || !autoCreate)
        {
            return layerIndex;
        }

        int createdIndex = document.Layers.AddPath(layerFullPath);
        return createdIndex >= 0 ? document.Layers.FindByFullPath(layerFullPath, -1) : -1;
    }

    private static BlockMutationPreviewResponse CreatePreviewResponse(
        string filePath,
        string operation,
        int requestedCount,
        IReadOnlyList<BlockOperationPreviewItemResponse> results,
        int wouldChangeCount)
    {
        return new BlockMutationPreviewResponse
        {
            FilePath = filePath,
            Operation = operation,
            RequestedCount = requestedCount,
            MatchedCount = results.Count(result => result.Success),
            WouldChangeCount = wouldChangeCount,
            FailedCount = results.Count(result => !result.Success),
            Results = results
        };
    }

    private static int RequestedPurgeCount(RhinoDoc document, BlockPurgeSpec spec)
    {
        int requestedNames = spec.DefinitionNames.Count(name => !string.IsNullOrWhiteSpace(name));
        if (requestedNames > 0 && !spec.IncludeAllUnused)
        {
            return requestedNames;
        }

        return spec.IncludeAllUnused
            ? LiveBlockInspector.ActiveDefinitions(document).Count
            : requestedNames;
    }

    private sealed record PreparedDefinitionCreation(
        BlockDefinitionCreationSpec Spec,
        string FinalName,
        IReadOnlyList<GeometryBase> Geometry,
        IReadOnlyList<ObjectAttributes> Attributes);

    private sealed record PreparedInsert(BlockInstancePlacementSpec Spec, InstanceDefinition Definition);

    private sealed record PreparedInsertBatch(int LayerIndex, IReadOnlyList<PreparedInsert> Items);

    private sealed record PreparedTransform(
        BlockInstanceTransformSpec Spec,
        InstanceObject Instance,
        InstanceDefinition Definition,
        Transform Transform);

    private sealed record PreparedExplode(
        BlockExplodeSpec Spec,
        InstanceDefinition Definition,
        IReadOnlyList<PreparedExplodePiece> Pieces);

    private sealed record PreparedExplodePiece(GeometryBase Geometry, ObjectAttributes Attributes);

    private sealed record PreparedPurge(InstanceDefinition Definition);
}

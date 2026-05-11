extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using InstanceDefinition = rhinocommon::Rhino.DocObjects.InstanceDefinition;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Transform = rhinocommon::Rhino.Geometry.Transform;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveBlockDefinitionOperator : ILiveBlockDefinitionOperator
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveBlockDefinitionOperator(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<BlockDefinitionMutationResponse> CreateDefinitions(
        string filePath,
        IReadOnlyList<BlockDefinitionSpec> specs)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: CreateArchitecturalBlockDefinitions", document =>
        {
            var results = new List<BlockDefinitionResultResponse>(specs.Count);
            int createdCount = 0;

            foreach (BlockDefinitionSpec spec in specs)
            {
                OperationResponse<BlockDefinitionResultResponse> result = CreateDefinition(document, spec);
                if (!result.Success || result.Data is null)
                {
                    return OperationResponse<(bool Mutated, BlockDefinitionMutationResponse Result)>.Fail(result.Message);
                }

                if (result.Data.Success)
                {
                    createdCount++;
                }

                results.Add(result.Data);
            }

            if (createdCount > 0)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, BlockDefinitionMutationResponse Result)>.Ok(
                (createdCount > 0, new BlockDefinitionMutationResponse
                {
                    FilePath = filePath,
                    RequestedCount = specs.Count,
                    CreatedCount = createdCount,
                    Results = results
                }),
                "Block definition creation completed.");
        });
    }

    public OperationResponse<BlockInstanceCreationResponse> InsertInstances(
        string filePath,
        IReadOnlyList<BlockInstanceSpec> specs,
        ArchitecturalObjectAttributesSpec attributes)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: InsertArchitecturalBlockInstances", document =>
        {
            int layerIndex = ResolveLayerIndex(document, attributes.LayerFullPath, attributes.AutoCreateLayer);
            if (layerIndex < 0)
            {
                return OperationResponse<(bool Mutated, BlockInstanceCreationResponse Result)>.Fail($"Target layer was not found: {attributes.LayerFullPath}");
            }

            var results = new List<BlockInstanceResultResponse>(specs.Count);
            int createdCount = 0;
            foreach (BlockInstanceSpec spec in specs)
            {
                InstanceDefinition? definition = document.InstanceDefinitions.Find(spec.DefinitionName);
                if (definition is null || definition.IsDeleted)
                {
                    return OperationResponse<(bool Mutated, BlockInstanceCreationResponse Result)>.Fail($"Block definition not found: {spec.DefinitionName}");
                }

                ObjectAttributes objectAttributes = LiveArchitecturalGeometryBuilder.CreateAttributes(
                    layerIndex,
                    attributes,
                    new ArchitecturalPrimitiveSpec
                    {
                        Kind = ArchitecturalPrimitiveKind.Box,
                        Metadata = new ArchitecturalMetadataSpec { Category = "block-instance" }
                    });

                Guid objectId = document.Objects.AddInstanceObject(definition.Index, BuildTransform(spec), objectAttributes);
                if (objectId == Guid.Empty)
                {
                    return OperationResponse<(bool Mutated, BlockInstanceCreationResponse Result)>.Fail($"Failed to insert block instance: {spec.DefinitionName}");
                }

                createdCount++;
                results.Add(new BlockInstanceResultResponse
                {
                    ObjectId = objectId,
                    DefinitionName = spec.DefinitionName,
                    LayerFullPath = attributes.LayerFullPath,
                    Success = true,
                    Message = "Inserted block instance."
                });
            }

            if (createdCount > 0)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, BlockInstanceCreationResponse Result)>.Ok(
                (createdCount > 0, new BlockInstanceCreationResponse
                {
                    FilePath = filePath,
                    RequestedCount = specs.Count,
                    CreatedCount = createdCount,
                    Results = results
                }),
                "Block instance insertion completed.");
        });
    }

    private static OperationResponse<BlockDefinitionResultResponse> CreateDefinition(RhinoDoc document, BlockDefinitionSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.Name))
        {
            return OperationResponse<BlockDefinitionResultResponse>.Fail("Block definition name is required.");
        }

        if (document.InstanceDefinitions.Find(spec.Name) is not null)
        {
            return OperationResponse<BlockDefinitionResultResponse>.Fail($"Block definition already exists: {spec.Name}");
        }

        if (spec.SourceObjectIds.Count == 0)
        {
            return OperationResponse<BlockDefinitionResultResponse>.Fail($"Block definition [{spec.Name}] requires source object ids.");
        }

        var geometry = new List<GeometryBase>(spec.SourceObjectIds.Count);
        var attributes = new List<ObjectAttributes>(spec.SourceObjectIds.Count);
        foreach (Guid objectId in spec.SourceObjectIds)
        {
            RhinoObject? source = document.Objects.FindId(objectId);
            if (source is null || source.IsDeleted || source.Geometry is null)
            {
                return OperationResponse<BlockDefinitionResultResponse>.Fail($"Source object not found for block definition [{spec.Name}]: {objectId}");
            }

            GeometryBase? duplicate = source.Geometry.Duplicate();
            if (duplicate is null)
            {
                return OperationResponse<BlockDefinitionResultResponse>.Fail($"Failed to duplicate source geometry for block definition [{spec.Name}]: {objectId}");
            }

            geometry.Add(duplicate);
            attributes.Add(source.Attributes.Duplicate());
        }

        int index = document.InstanceDefinitions.Add(
            spec.Name,
            spec.Description,
            new Point3d(spec.BasePointX, spec.BasePointY, spec.BasePointZ),
            geometry,
            attributes);

        if (index < 0)
        {
            return OperationResponse<BlockDefinitionResultResponse>.Fail($"Failed to create block definition: {spec.Name}");
        }

        if (spec.HideSourceObjects)
        {
            foreach (Guid objectId in spec.SourceObjectIds)
            {
                RhinoObject? source = document.Objects.FindId(objectId);
                if (source is null)
                {
                    continue;
                }

                ObjectAttributes sourceAttributes = source.Attributes.Duplicate();
                sourceAttributes.Visible = false;
                document.Objects.ModifyAttributes(objectId, sourceAttributes, true);
            }
        }

        return OperationResponse<BlockDefinitionResultResponse>.Ok(new BlockDefinitionResultResponse
        {
            Name = spec.Name,
            DefinitionIndex = index,
            Success = true,
            Message = "Created block definition."
        });
    }

    private static Transform BuildTransform(BlockInstanceSpec spec)
    {
        Transform transform = Transform.Identity;
        if (spec.Scale > 0d && Math.Abs(spec.Scale - 1d) > double.Epsilon)
        {
            transform = Transform.Scale(Point3d.Origin, spec.Scale) * transform;
        }

        if (Math.Abs(spec.RotationDegrees) > double.Epsilon)
        {
            transform = Transform.Rotation(spec.RotationDegrees * Math.PI / 180d, Vector3d.ZAxis, Point3d.Origin) * transform;
        }

        return Transform.Translation(spec.OriginX, spec.OriginY, spec.OriginZ) * transform;
    }

    private static int ResolveLayerIndex(RhinoDoc document, string layerFullPath, bool autoCreate)
    {
        if (string.IsNullOrWhiteSpace(layerFullPath))
        {
            return -1;
        }

        int layerIndex = document.Layers.FindByFullPath(layerFullPath, -1);
        if (layerIndex >= 0 || !autoCreate)
        {
            return layerIndex;
        }

        int createdIndex = document.Layers.AddPath(layerFullPath);
        return createdIndex >= 0 ? document.Layers.FindByFullPath(layerFullPath, -1) : -1;
    }
}


extern alias rhinocommon;

using System.Collections.Specialized;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using InstanceDefinition = rhinocommon::Rhino.DocObjects.InstanceDefinition;
using InstanceDefinitionUpdateType = rhinocommon::Rhino.DocObjects.InstanceDefinitionUpdateType;
using InstanceObject = rhinocommon::Rhino.DocObjects.InstanceObject;
using InstanceReferenceGeometry = rhinocommon::Rhino.Geometry.InstanceReferenceGeometry;
using Layer = rhinocommon::Rhino.DocObjects.Layer;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Transform = rhinocommon::Rhino.Geometry.Transform;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveBlockInspector : ILiveBlockInspector
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveBlockInspector(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<BlockDefinitionListResponse> ListDefinitions(
        string filePath,
        bool includeLinked,
        bool includeNestedSummary)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            var definitions = ActiveDefinitions(document)
                .Where(definition => includeLinked || ToLinkStatus(definition) == BlockLinkStatus.Local)
                .Select(definition => BuildDefinitionSummary(document, definition, includeNestedSummary))
                .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return OperationResponse<BlockDefinitionListResponse>.Ok(new BlockDefinitionListResponse
            {
                FilePath = filePath,
                DefinitionCount = definitions.Count,
                Definitions = definitions
            }, "Block definitions listed from live document.");
        });
    }

    public OperationResponse<BlockDefinitionDetailResponse> GetDefinitionDetails(
        string filePath,
        string definitionName,
        Guid definitionId,
        bool includeNestedSummary)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            InstanceDefinition? definition = ResolveDefinition(document, definitionName, definitionId);
            if (definition is null || definition.IsDeleted)
            {
                return OperationResponse<BlockDefinitionDetailResponse>.Fail("Block definition not found.");
            }

            return OperationResponse<BlockDefinitionDetailResponse>.Ok(new BlockDefinitionDetailResponse
            {
                FilePath = filePath,
                Definition = BuildDefinitionSummary(document, definition, includeNestedSummary)
            }, "Block definition details read from live document.");
        });
    }

    public OperationResponse<BlockInstanceListResponse> ListInstances(
        string filePath,
        string definitionName,
        bool includeHidden)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            var instances = new List<BlockInstanceSummaryResponse>();
            foreach (RhinoObject rhinoObject in document.Objects)
            {
                if (rhinoObject is not InstanceObject instanceObject || rhinoObject.IsDeleted)
                {
                    continue;
                }

                if (!includeHidden && !rhinoObject.Visible)
                {
                    continue;
                }

                InstanceDefinition? definition = instanceObject.InstanceDefinition;
                if (!string.IsNullOrWhiteSpace(definitionName)
                    && !string.Equals(definition?.Name, definitionName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                instances.Add(BuildInstanceSummary(document, instanceObject));
            }

            return OperationResponse<BlockInstanceListResponse>.Ok(new BlockInstanceListResponse
            {
                FilePath = filePath,
                InstanceCount = instances.Count,
                Instances = instances.OrderBy(instance => instance.DefinitionName, StringComparer.OrdinalIgnoreCase).ToList()
            }, "Block instances listed from live document.");
        });
    }

    public OperationResponse<BlockInstanceDetailResponse> GetInstanceDetails(string filePath, Guid objectId)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject is not InstanceObject instanceObject || rhinoObject.IsDeleted)
            {
                return OperationResponse<BlockInstanceDetailResponse>.Fail($"Block instance not found: {objectId}");
            }

            return OperationResponse<BlockInstanceDetailResponse>.Ok(new BlockInstanceDetailResponse
            {
                FilePath = filePath,
                Instance = BuildInstanceSummary(document, instanceObject)
            }, "Block instance details read from live document.");
        });
    }

    internal static IReadOnlyList<InstanceDefinition> ActiveDefinitions(RhinoDoc document)
    {
        return document.InstanceDefinitions
            .GetList(true)
            .Where(definition => definition is not null && !definition.IsDeleted)
            .ToList();
    }

    internal static InstanceDefinition? ResolveDefinition(RhinoDoc document, string definitionName, Guid definitionId)
    {
        if (definitionId != Guid.Empty)
        {
            return document.InstanceDefinitions.Find(definitionId, true);
        }

        return string.IsNullOrWhiteSpace(definitionName)
            ? null
            : document.InstanceDefinitions.Find(definitionName);
    }

    internal static BlockDefinitionSummaryResponse BuildDefinitionSummary(
        RhinoDoc document,
        InstanceDefinition definition,
        bool includeNestedSummary)
    {
        GetUseCounts(definition, out int topLevelCount, out int nestedCount);

        IReadOnlyList<string> nestedNames = includeNestedSummary
            ? FindNestedDefinitionNames(document, definition)
            : Array.Empty<string>();

        return new BlockDefinitionSummaryResponse
        {
            DefinitionId = definition.Id,
            DefinitionIndex = definition.Index,
            Name = definition.Name ?? string.Empty,
            Description = definition.Description ?? string.Empty,
            LinkStatus = ToLinkStatus(definition),
            UpdateType = definition.UpdateType.ToString(),
            SourceArchive = definition.SourceArchive ?? string.Empty,
            ArchiveFileStatus = definition.ArchiveFileStatus.ToString(),
            IsReference = definition.IsReference,
            ObjectCount = definition.ObjectCount,
            TopLevelInstanceCount = topLevelCount,
            NestedInstanceCount = nestedCount,
            NestedDefinitionNames = nestedNames,
            BoundingBox = BuildDefinitionBoundingBox(definition),
            UserText = ReadUserText(definition.GetUserStrings())
        };
    }

    internal static BlockInstanceSummaryResponse BuildInstanceSummary(RhinoDoc document, InstanceObject instanceObject)
    {
        InstanceDefinition? definition = instanceObject.InstanceDefinition;
        ObjectAttributes attributes = instanceObject.Attributes;
        BoundingBox box = instanceObject.Geometry.GetBoundingBox(true);

        return new BlockInstanceSummaryResponse
        {
            ObjectId = instanceObject.Id,
            DefinitionId = definition?.Id ?? Guid.Empty,
            DefinitionIndex = definition?.Index ?? -1,
            DefinitionName = definition?.Name ?? string.Empty,
            DefinitionLinkStatus = definition is null ? BlockLinkStatus.Unknown : ToLinkStatus(definition),
            LayerFullPath = ResolveLayerFullPath(document, attributes.LayerIndex),
            Name = attributes.Name ?? string.Empty,
            Visible = attributes.Visible,
            ColorSource = attributes.ColorSource.ToString(),
            Transform = ToTransformResponse(instanceObject.InstanceXform),
            BoundingBox = box.IsValid ? ToBoundingBoxResponse(box) : null,
            UserText = ReadUserText(attributes.GetUserStrings())
        };
    }

    internal static BlockLinkStatus ToLinkStatus(InstanceDefinition definition)
    {
        if (definition.IsReference)
        {
            return BlockLinkStatus.Reference;
        }

        string updateType = definition.UpdateType.ToString();
        if (string.Equals(updateType, "Embedded", StringComparison.OrdinalIgnoreCase))
        {
            return BlockLinkStatus.Embedded;
        }

        return definition.UpdateType switch
        {
            InstanceDefinitionUpdateType.Static => string.IsNullOrWhiteSpace(definition.SourceArchive)
                ? BlockLinkStatus.Local
                : BlockLinkStatus.Embedded,
            InstanceDefinitionUpdateType.Linked => BlockLinkStatus.Linked,
            InstanceDefinitionUpdateType.LinkedAndEmbedded => BlockLinkStatus.LinkedAndEmbedded,
            _ => BlockLinkStatus.Unknown
        };
    }

    internal static BlockTransformResponse ToTransformResponse(Transform transform)
    {
        double scaleX = Math.Sqrt((transform.M00 * transform.M00) + (transform.M10 * transform.M10) + (transform.M20 * transform.M20));
        double scaleY = Math.Sqrt((transform.M01 * transform.M01) + (transform.M11 * transform.M11) + (transform.M21 * transform.M21));
        double scaleZ = Math.Sqrt((transform.M02 * transform.M02) + (transform.M12 * transform.M12) + (transform.M22 * transform.M22));
        double rotationZ = scaleX > double.Epsilon
            ? Math.Atan2(transform.M10 / scaleX, transform.M00 / scaleX) * 180d / Math.PI
            : 0d;

        return new BlockTransformResponse
        {
            Matrix = new[]
            {
                transform.M00, transform.M01, transform.M02, transform.M03,
                transform.M10, transform.M11, transform.M12, transform.M13,
                transform.M20, transform.M21, transform.M22, transform.M23,
                transform.M30, transform.M31, transform.M32, transform.M33
            },
            TranslationX = transform.M03,
            TranslationY = transform.M13,
            TranslationZ = transform.M23,
            EstimatedScaleX = scaleX,
            EstimatedScaleY = scaleY,
            EstimatedScaleZ = scaleZ,
            EstimatedRotationZDegrees = rotationZ
        };
    }

    internal static BlockBoundingBoxResponse ToBoundingBoxResponse(BoundingBox box)
    {
        return new BlockBoundingBoxResponse
        {
            MinX = box.Min.X,
            MinY = box.Min.Y,
            MinZ = box.Min.Z,
            MaxX = box.Max.X,
            MaxY = box.Max.Y,
            MaxZ = box.Max.Z
        };
    }

    internal static IReadOnlyList<string> FindNestedDefinitionNames(RhinoDoc document, InstanceDefinition definition)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (RhinoObject rhinoObject in definition.GetObjects())
        {
            if (rhinoObject.Geometry is not InstanceReferenceGeometry referenceGeometry)
            {
                continue;
            }

            InstanceDefinition? nested = document.InstanceDefinitions.Find(referenceGeometry.ParentIdefId, true);
            if (nested is not null && !nested.IsDeleted && !string.IsNullOrWhiteSpace(nested.Name))
            {
                names.Add(nested.Name);
            }
        }

        return names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static bool HasNestedDefinitions(InstanceDefinition definition)
    {
        return definition.GetObjects().Any(rhinoObject => rhinoObject.Geometry is InstanceReferenceGeometry);
    }

    private static BlockBoundingBoxResponse? BuildDefinitionBoundingBox(InstanceDefinition definition)
    {
        bool hasBox = false;
        BoundingBox aggregate = default;
        foreach (RhinoObject rhinoObject in definition.GetObjects())
        {
            if (rhinoObject.Geometry is null)
            {
                continue;
            }

            BoundingBox box = rhinoObject.Geometry.GetBoundingBox(true);
            if (!box.IsValid)
            {
                continue;
            }

            if (!hasBox)
            {
                aggregate = box;
                hasBox = true;
            }
            else
            {
                aggregate.Union(box);
            }
        }

        return hasBox ? ToBoundingBoxResponse(aggregate) : null;
    }

    private static void GetUseCounts(InstanceDefinition definition, out int topLevelCount, out int nestedCount)
    {
        try
        {
            definition.UseCount(out topLevelCount, out nestedCount);
        }
        catch
        {
            topLevelCount = definition.GetReferences(0).Length;
            nestedCount = Math.Max(0, definition.GetReferences(1).Length - topLevelCount);
        }
    }

    private static string ResolveLayerFullPath(RhinoDoc document, int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= document.Layers.Count)
        {
            return string.Empty;
        }

        Layer layer = document.Layers[layerIndex];
        return layer is not null && !layer.IsDeleted ? layer.FullPath : string.Empty;
    }

    private static Dictionary<string, string> ReadUserText(NameValueCollection? userStrings)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (userStrings is null)
        {
            return result;
        }

        foreach (string? key in userStrings.AllKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            result[key] = userStrings[key] ?? string.Empty;
        }

        return result;
    }
}

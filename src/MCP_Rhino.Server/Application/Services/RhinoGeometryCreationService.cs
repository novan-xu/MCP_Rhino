extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoGeometryCreationService
{
    private const double LargeBBoxDiagonalThreshold = 1e12;

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILiveGeometryValidator _validator;
    private readonly ILiveGeometryBuilder _builder;
    private readonly IEditResultFormatter _formatter;

    public RhinoGeometryCreationService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILiveGeometryValidator validator,
        ILiveGeometryBuilder builder,
        IEditResultFormatter formatter)
    {
        _documentAccessor = documentAccessor;
        _validator = validator;
        _builder = builder;
        _formatter = formatter;
    }

    public OperationResponse<GeometryCreationResponse> Create(
        string filePath,
        IReadOnlyList<GeometryCreationSpec> specs,
        GeometryObjectAttributesSpec attributes)
    {
        if (specs.Count == 0)
        {
            return OperationResponse<GeometryCreationResponse>.Fail("At least one geometry creation item is required.");
        }

        var warnings = new List<ObjectEditWarning>();
        if (specs.Count > 10000)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "LARGE_BATCH",
                Message = "Batch size exceeds 10000 items."
            });
        }

        return _documentAccessor.ExecuteWithUndo(
            filePath,
            "MCP: CreateGeometry",
            document =>
            {
                foreach (GeometryCreationSpec spec in specs)
                {
                    OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _validator.Validate(spec, attributes);
                    if (!validation.Success)
                    {
                        return OperationResponse<(bool Mutated, GeometryCreationResponse Result)>.Fail(validation.Message);
                    }

                    warnings.AddRange(validation.Data ?? Array.Empty<ObjectEditWarning>());
                }

                int layerIndex = document.Layers.FindByFullPath(attributes.LayerFullPath, -1);
                if (layerIndex < 0)
                {
                    return OperationResponse<(bool Mutated, GeometryCreationResponse Result)>.Fail(
                        $"Target layer was not found: {attributes.LayerFullPath}");
                }

                var createdObjects = new List<GeometryCreatedObjectResponse>(specs.Count);
                foreach (GeometryCreationSpec spec in specs)
                {
                    OperationResponse<GeometryBase> buildResult = _builder.Build(spec);
                    if (!buildResult.Success || buildResult.Data is null)
                    {
                        return OperationResponse<(bool Mutated, GeometryCreationResponse Result)>.Fail(buildResult.Message);
                    }

                    AddLargeBBoxWarning(buildResult.Data, warnings);

                    ObjectAttributes objectAttributes = CreateAttributes(layerIndex, attributes);
                    Guid objectId = document.Objects.Add(buildResult.Data, objectAttributes);
                    if (objectId == Guid.Empty)
                    {
                        return OperationResponse<(bool Mutated, GeometryCreationResponse Result)>.Fail("Failed to add geometry to the active Rhino document.");
                    }

                    createdObjects.Add(new GeometryCreatedObjectResponse
                    {
                        ObjectId = objectId,
                        Primitive = spec.Primitive,
                        LayerFullPath = attributes.LayerFullPath
                    });
                }

                if (createdObjects.Count > 0)
                {
                    document.Views.Redraw();
                }

                var response = new GeometryCreationResponse
                {
                    FilePath = filePath,
                    RequestedCount = specs.Count,
                    CreatedCount = createdObjects.Count,
                    CreatedObjects = createdObjects,
                    Warnings = warnings
                };

                return OperationResponse<(bool Mutated, GeometryCreationResponse Result)>.Ok(
                    (createdObjects.Count > 0, response),
                    "Geometry creation completed.");
            });
    }

    public string Format(GeometryCreationResponse response)
    {
        return _formatter.FormatGeometryCreation(response);
    }

    private static ObjectAttributes CreateAttributes(int layerIndex, GeometryObjectAttributesSpec attributesSpec)
    {
        var attributes = new ObjectAttributes
        {
            LayerIndex = layerIndex,
            Name = attributesSpec.Name ?? string.Empty
        };

        if (attributesSpec.Color is not null)
        {
            attributes.ObjectColor = attributesSpec.Color.ToColor();
            attributes.ColorSource = ObjectColorSource.ColorFromObject;
        }

        foreach ((string key, string value) in attributesSpec.UserText)
        {
            attributes.SetUserString(key, value);
        }

        return attributes;
    }

    private static void AddLargeBBoxWarning(GeometryBase geometry, ICollection<ObjectEditWarning> warnings)
    {
        BoundingBox boundingBox = geometry.GetBoundingBox(true);
        if (boundingBox.IsValid && boundingBox.Min.DistanceTo(boundingBox.Max) > LargeBBoxDiagonalThreshold)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "LARGE_BBOX",
                Message = "Geometry bounding box diagonal exceeds 1e12."
            });
        }
    }
}

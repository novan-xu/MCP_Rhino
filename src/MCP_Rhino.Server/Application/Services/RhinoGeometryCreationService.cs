using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Rhino.DocObjects;
using Rhino.FileIO;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoGeometryCreationService
{
    private const double LargeBBoxDiagonalThreshold = 1e12;

    private readonly IRhinoDocumentRepository _repository;
    private readonly IGeometryValidator _validator;
    private readonly IGeometryBuilder _builder;
    private readonly IFileMutationSafeguard _fileMutationSafeguard;
    private readonly IEditResultFormatter _formatter;

    public RhinoGeometryCreationService(
        IRhinoDocumentRepository repository,
        IGeometryValidator validator,
        IGeometryBuilder builder,
        IFileMutationSafeguard fileMutationSafeguard,
        IEditResultFormatter formatter)
    {
        _repository = repository;
        _validator = validator;
        _builder = builder;
        _fileMutationSafeguard = fileMutationSafeguard;
        _formatter = formatter;
    }

    public OperationResponse<GeometryCreationResponse> Create(
        string filePath,
        IReadOnlyList<GeometryCreationSpec> specs,
        GeometryObjectAttributesSpec attributes)
    {
        if (!_repository.Exists(filePath))
        {
            return OperationResponse<GeometryCreationResponse>.Fail($"错误：未找到文件 {filePath}");
        }

        if (specs.Count == 0)
        {
            return OperationResponse<GeometryCreationResponse>.Fail("错误：至少需要提供一个几何创建条目。");
        }

        try
        {
            using var model = _repository.Read(filePath);
            if (!LayerExists(model, attributes.LayerFullPath))
            {
                return OperationResponse<GeometryCreationResponse>.Fail($"错误：目标图层不存在 [{attributes.LayerFullPath}]。");
            }

            var warnings = new List<ObjectEditWarning>();
            if (specs.Count > 10000)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "LARGE_BATCH",
                    Message = "批量创建条目超过 10000，请确认调用规模。"
                });
            }

            foreach (GeometryCreationSpec spec in specs)
            {
                OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _validator.Validate(spec, attributes);
                if (!validation.Success)
                {
                    return OperationResponse<GeometryCreationResponse>.Fail(validation.Message);
                }

                warnings.AddRange(validation.Data ?? Array.Empty<ObjectEditWarning>());
            }

            OperationResponse<FileMutationPreflightResponse> safeguard = _fileMutationSafeguard.BeforeOverwrite(filePath, specs.Count);
            if (!safeguard.Success)
            {
                return OperationResponse<GeometryCreationResponse>.Fail(safeguard.Message);
            }

            if (safeguard.Data is not null)
            {
                warnings.AddRange(safeguard.Data.Warnings.Select(message => new ObjectEditWarning
                {
                    Code = "FILE_MUTATION_PREFLIGHT",
                    Message = message
                }));
            }

            var createdObjects = new List<GeometryCreatedObjectResponse>(specs.Count);
            bool writeSucceeded = false;

            try
            {
                foreach (GeometryCreationSpec spec in specs)
                {
                    OperationResponse<Rhino.Geometry.GeometryBase> buildResult = _builder.Build(spec);
                    if (!buildResult.Success || buildResult.Data is null)
                    {
                        return OperationResponse<GeometryCreationResponse>.Fail(buildResult.Message);
                    }

                    AddLargeBBoxWarning(buildResult.Data, warnings);

                    ObjectAttributes objectAttributes = CreateAttributes(model, attributes);
                    Guid objectId = model.Objects.Add(buildResult.Data, objectAttributes);
                    if (objectId == Guid.Empty)
                    {
                        return OperationResponse<GeometryCreationResponse>.Fail("写入新几何对象失败。");
                    }

                    createdObjects.Add(new GeometryCreatedObjectResponse
                    {
                        ObjectId = objectId,
                        Primitive = spec.Primitive,
                        LayerFullPath = attributes.LayerFullPath
                    });
                }

                writeSucceeded = _repository.Write(model, filePath);
                if (!writeSucceeded)
                {
                    return OperationResponse<GeometryCreationResponse>.Fail("几何创建写回失败。请检查文件是否可写。");
                }
            }
            finally
            {
                _fileMutationSafeguard.AfterOverwrite(filePath, writeSucceeded);
            }

            var response = new GeometryCreationResponse
            {
                FilePath = filePath,
                RequestedCount = specs.Count,
                CreatedCount = createdObjects.Count,
                CreatedObjects = createdObjects,
                Warnings = warnings
            };

            return OperationResponse<GeometryCreationResponse>.Ok(response, "几何创建执行完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryCreationResponse>.Fail($"几何创建执行失败: {ex.Message}");
        }
    }

    public string Format(GeometryCreationResponse response)
    {
        return _formatter.FormatGeometryCreation(response);
    }

    private static bool LayerExists(File3dm model, string layerFullPath)
    {
        return model.AllLayers.Any(layer =>
            !layer.IsDeleted
            && string.Equals(layer.FullPath, layerFullPath, StringComparison.OrdinalIgnoreCase));
    }

    private static ObjectAttributes CreateAttributes(File3dm model, GeometryObjectAttributesSpec attributesSpec)
    {
        Layer? layer = model.AllLayers.First(layer =>
            !layer.IsDeleted
            && string.Equals(layer.FullPath, attributesSpec.LayerFullPath, StringComparison.OrdinalIgnoreCase));

        var attributes = new ObjectAttributes
        {
            LayerIndex = layer.Index,
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

    private static void AddLargeBBoxWarning(Rhino.Geometry.GeometryBase geometry, ICollection<ObjectEditWarning> warnings)
    {
        Rhino.Geometry.BoundingBox boundingBox = geometry.GetBoundingBox(true);
        if (boundingBox.IsValid && boundingBox.Min.DistanceTo(boundingBox.Max) > LargeBBoxDiagonalThreshold)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "LARGE_BBOX",
                Message = "几何 bbox 对角线超过 1e12，疑似单位不匹配。"
            });
        }
    }
}

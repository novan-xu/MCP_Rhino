extern alias rhinocommon;

using System.Reflection;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using Mesh = rhinocommon::Rhino.Geometry.Mesh;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveSubDModelingOperator : ILiveSubDModelingOperator
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveSubDModelingOperator(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<SubDCreationResponse> Create(
        string filePath,
        IReadOnlyList<SubDCageSpec> cages,
        GeometryObjectAttributesSpec attributes,
        SubDModelingOperationKind operation)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, $"MCP: CreateSubD{operation}", document =>
        {
            OperationResponse<int> layer = ResolveLayerIndex(document, attributes);
            if (!layer.Success)
            {
                return OperationResponse<(bool Mutated, SubDCreationResponse Result)>.Fail(layer.Message);
            }

            var created = new List<SubDCreatedObjectResponse>();
            var warnings = new List<ObjectEditWarning>();
            for (int i = 0; i < cages.Count; i++)
            {
                SubDCageSpec cage = cages[i];
                OperationResponse validation = RhinoSubDModelingService.ValidateCage(cage);
                if (!validation.Success)
                {
                    warnings.Add(new ObjectEditWarning
                    {
                        Code = "SUBD_CAGE_REJECTED",
                        Message = $"{ResolveName(attributes.Name, cage.Name, i, cages.Count)}: {validation.Message}"
                    });
                    continue;
                }

                OperationResponse<GeometryBase> geometry = BuildSubDGeometry(cage);
                if (!geometry.Success || geometry.Data is null)
                {
                    warnings.Add(new ObjectEditWarning
                    {
                        Code = "SUBD_CREATE_FAILED",
                        Message = $"{ResolveName(attributes.Name, cage.Name, i, cages.Count)}: {geometry.Message}"
                    });
                    continue;
                }

                ObjectAttributes objectAttributes = CreateAttributes(layer.Data, attributes, cage.Name, i, cages.Count);
                Guid objectId = document.Objects.Add(geometry.Data, objectAttributes);
                if (objectId == Guid.Empty)
                {
                    warnings.Add(new ObjectEditWarning
                    {
                        Code = "SUBD_ADD_FAILED",
                        Message = $"{ResolveName(attributes.Name, cage.Name, i, cages.Count)}: Rhino failed to add SubD geometry."
                    });
                    continue;
                }

                BoundingBox box = geometry.Data.GetBoundingBox(true);
                created.Add(new SubDCreatedObjectResponse
                {
                    ObjectId = objectId,
                    Name = objectAttributes.Name,
                    GeometryTypeName = geometry.Data.GetType().Name,
                    LayerFullPath = attributes.LayerFullPath,
                    BoundingBox = box.IsValid ? ToResponse(box) : null
                });
            }

            if (created.Count > 0)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, SubDCreationResponse Result)>.Ok(
                (created.Count > 0, new SubDCreationResponse
                {
                    FilePath = filePath,
                    Operation = operation,
                    RequestedCount = cages.Count,
                    CreatedCount = created.Count,
                    FailedCount = cages.Count - created.Count,
                    CreatedObjects = created,
                    Warnings = warnings
                }),
                $"{operation} SubD creation completed.");
        });
    }

    public OperationResponse<SubDInspectionResponse> Inspect(
        string filePath,
        IReadOnlyList<Guid> objectIds)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            var results = new List<SubDInspectionEntryResponse>(objectIds.Count);
            foreach (Guid objectId in objectIds)
            {
                RhinoObject? rhinoObject = document.Objects.FindId(objectId);
                GeometryBase? geometry = rhinoObject?.Geometry;
                if (geometry is null)
                {
                    results.Add(new SubDInspectionEntryResponse
                    {
                        ObjectId = objectId,
                        Success = false,
                        Message = "Object was not found."
                    });
                    continue;
                }

                string typeName = geometry.GetType().FullName ?? geometry.GetType().Name;
                if (!typeName.Contains("SubD", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SubDInspectionEntryResponse
                    {
                        ObjectId = objectId,
                        Success = false,
                        Message = "Object is not SubD geometry.",
                        GeometryTypeName = typeName
                    });
                    continue;
                }

                BoundingBox box = geometry.GetBoundingBox(true);
                results.Add(new SubDInspectionEntryResponse
                {
                    ObjectId = objectId,
                    Success = true,
                    Message = "SubD object inspected.",
                    GeometryTypeName = typeName,
                    VertexCount = TryGetNestedCount(geometry, "Vertices"),
                    EdgeCount = TryGetNestedCount(geometry, "Edges"),
                    FaceCount = TryGetNestedCount(geometry, "Faces"),
                    BoundingBox = box.IsValid ? ToResponse(box) : null
                });
            }

            return OperationResponse<SubDInspectionResponse>.Ok(new SubDInspectionResponse
            {
                FilePath = filePath,
                RequestedCount = objectIds.Count,
                InspectedCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Results = results
            }, "SubD inspection completed.");
        });
    }

    private static OperationResponse<GeometryBase> BuildSubDGeometry(SubDCageSpec cage)
    {
        Mesh mesh = BuildMesh(cage);
        Type? subDType = typeof(Mesh).Assembly.GetType("Rhino.Geometry.SubD");
        if (subDType is null)
        {
            return OperationResponse<GeometryBase>.Fail("SUBD_API_UNAVAILABLE: RhinoCommon SubD type was not found in the loaded Rhino runtime.");
        }

        MethodInfo? createFromMesh = subDType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name.Equals("CreateFromMesh", StringComparison.Ordinal))
            .FirstOrDefault(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length >= 1 && parameters[0].ParameterType == typeof(Mesh);
            });
        if (createFromMesh is null)
        {
            return OperationResponse<GeometryBase>.Fail("SUBD_API_UNAVAILABLE: RhinoCommon SubD.CreateFromMesh(Mesh, ...) was not found.");
        }

        object?[] args = BuildReflectionArguments(createFromMesh, mesh);
        object? result = createFromMesh.Invoke(null, args);
        return result is GeometryBase geometry && geometry.IsValid
            ? OperationResponse<GeometryBase>.Ok(geometry)
            : OperationResponse<GeometryBase>.Fail("RhinoCommon SubD.CreateFromMesh did not return valid GeometryBase.");
    }

    private static object?[] BuildReflectionArguments(MethodInfo method, Mesh mesh)
    {
        ParameterInfo[] parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = mesh;
        for (int i = 1; i < parameters.Length; i++)
        {
            args[i] = parameters[i].HasDefaultValue
                ? parameters[i].DefaultValue
                : GetDefault(parameters[i].ParameterType);
        }

        return args;
    }

    private static object? GetDefault(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static Mesh BuildMesh(SubDCageSpec cage)
    {
        var mesh = new Mesh();
        foreach (SubDPointSpec vertex in cage.Vertices)
        {
            mesh.Vertices.Add(vertex.X, vertex.Y, vertex.Z);
        }

        foreach (SubDFaceSpec face in cage.Faces)
        {
            IReadOnlyList<int> indices = face.VertexIndices;
            if (indices.Count == 3)
            {
                mesh.Faces.AddFace(indices[0], indices[1], indices[2]);
            }
            else
            {
                mesh.Faces.AddFace(indices[0], indices[1], indices[2], indices[3]);
            }
        }

        mesh.Normals.ComputeNormals();
        mesh.Compact();
        return mesh;
    }

    private static OperationResponse<int> ResolveLayerIndex(
        rhinocommon::Rhino.RhinoDoc document,
        GeometryObjectAttributesSpec attributes)
    {
        OperationResponse validation = ValidateAttributes(attributes);
        if (!validation.Success)
        {
            return OperationResponse<int>.Fail(validation.Message);
        }

        int layerIndex = document.Layers.FindByFullPath(attributes.LayerFullPath, -1);
        return layerIndex < 0
            ? OperationResponse<int>.Fail($"Target layer was not found: {attributes.LayerFullPath}")
            : OperationResponse<int>.Ok(layerIndex);
    }

    private static OperationResponse ValidateAttributes(GeometryObjectAttributesSpec attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes.LayerFullPath))
        {
            return OperationResponse.Fail("LayerFullPath is required for SubD creation.");
        }

        if (attributes.Color is not null
            && (attributes.Color.R is < 0 or > 255
                || attributes.Color.G is < 0 or > 255
                || attributes.Color.B is < 0 or > 255))
        {
            return OperationResponse.Fail("Object color values must be in the 0-255 range.");
        }

        foreach ((string key, _) in attributes.UserText)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return OperationResponse.Fail("UserText keys cannot be empty.");
            }
        }

        return OperationResponse.Ok();
    }

    private static ObjectAttributes CreateAttributes(
        int layerIndex,
        GeometryObjectAttributesSpec attributesSpec,
        string itemName,
        int index,
        int count)
    {
        var attributes = new ObjectAttributes
        {
            LayerIndex = layerIndex,
            Name = ResolveName(attributesSpec.Name, itemName, index, count)
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

    private static string ResolveName(string commonName, string itemName, int index, int count)
    {
        if (!string.IsNullOrWhiteSpace(itemName))
        {
            return itemName.Trim();
        }

        if (string.IsNullOrWhiteSpace(commonName))
        {
            return string.Empty;
        }

        string name = commonName.Trim();
        return count == 1 ? name : $"{name} {index + 1:D3}";
    }

    private static int? TryGetNestedCount(GeometryBase geometry, string propertyName)
    {
        object? nested = geometry.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(geometry);
        object? count = nested?.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance)?.GetValue(nested);
        return count is int value ? value : null;
    }

    private static GeneralPrimitiveBoundingBoxResponse ToResponse(BoundingBox box)
    {
        return new GeneralPrimitiveBoundingBoxResponse
        {
            MinX = box.Min.X,
            MinY = box.Min.Y,
            MinZ = box.Min.Z,
            MaxX = box.Max.X,
            MaxY = box.Max.Y,
            MaxZ = box.Max.Z
        };
    }
}

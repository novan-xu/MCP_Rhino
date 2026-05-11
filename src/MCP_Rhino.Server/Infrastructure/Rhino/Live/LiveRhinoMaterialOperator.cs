extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using Circle = rhinocommon::Rhino.Geometry.Circle;
using Cylinder = rhinocommon::Rhino.Geometry.Cylinder;
using Interval = rhinocommon::Rhino.Geometry.Interval;
using Material = rhinocommon::Rhino.DocObjects.Material;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectMaterialSource = rhinocommon::Rhino.DocObjects.ObjectMaterialSource;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Sphere = rhinocommon::Rhino.Geometry.Sphere;
using Texture = rhinocommon::Rhino.DocObjects.Texture;
using TextureMapping = rhinocommon::Rhino.Render.TextureMapping;
using Transform = rhinocommon::Rhino.Geometry.Transform;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoMaterialOperator : ILiveRhinoMaterialOperator
{
    private const double MaxRhinoShine = 255d;

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveRhinoMaterialOperator(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<RenderMaterialCreationResponse> CreateRenderMaterials(
        string filePath,
        IReadOnlyList<RenderMaterialCreationSpec> specs,
        bool reuseExistingByName)
    {
        return _documentAccessor.ExecuteWithUndo(
            filePath,
            "MCP: CreateRenderMaterials",
            document =>
            {
                var materials = new List<RenderMaterialCreatedResponse>(specs.Count);
                var warnings = new List<ObjectEditWarning>();
                int createdCount = 0;
                int reusedCount = 0;

                foreach (RenderMaterialCreationSpec spec in specs)
                {
                    int existingIndex = document.Materials.Find(spec.Name, true);
                    if (reuseExistingByName && existingIndex >= 0)
                    {
                        Material existing = document.Materials[existingIndex];
                        materials.Add(ToResponse(existing, spec, reusedExisting: true));
                        reusedCount++;
                        continue;
                    }

                    if (existingIndex >= 0)
                    {
                        warnings.Add(new ObjectEditWarning
                        {
                            Code = "DUPLICATE_MATERIAL_NAME",
                            Message = $"Creating an additional material named {spec.Name} because ReuseExistingByName is false."
                        });
                    }

                    var material = new Material
                    {
                        Name = spec.Name,
                        DiffuseColor = spec.BaseColor.ToColor(),
                        Transparency = spec.Transparency,
                        Shine = (1d - spec.Roughness) * MaxRhinoShine
                    };

                    int materialIndex = document.Materials.Add(material);
                    if (materialIndex < 0)
                    {
                        return OperationResponse<(bool Mutated, RenderMaterialCreationResponse Result)>.Fail(
                            $"Failed to add material: {spec.Name}");
                    }

                    Material created = document.Materials[materialIndex];
                    materials.Add(ToResponse(created, spec, reusedExisting: false));
                    createdCount++;
                }

                if (createdCount > 0)
                {
                    document.Views.Redraw();
                }

                var response = new RenderMaterialCreationResponse
                {
                    FilePath = document.Path,
                    RequestedCount = specs.Count,
                    CreatedCount = createdCount,
                    ReusedCount = reusedCount,
                    Materials = materials,
                    Warnings = warnings
                };

                return OperationResponse<(bool Mutated, RenderMaterialCreationResponse Result)>.Ok(
                    (createdCount > 0, response),
                    "Render material creation completed.");
            });
    }

    public OperationResponse<TexturedRenderMaterialCreationResponse> CreateTexturedRenderMaterials(
        string filePath,
        IReadOnlyList<TexturedRenderMaterialCreationSpec> specs,
        bool reuseExistingByName)
    {
        return _documentAccessor.ExecuteWithUndo(
            filePath,
            "MCP: CreateTexturedRenderMaterials",
            document =>
            {
                var materials = new List<TexturedRenderMaterialCreatedResponse>(specs.Count);
                var warnings = new List<ObjectEditWarning>();
                int createdCount = 0;
                int updatedExistingCount = 0;

                foreach (TexturedRenderMaterialCreationSpec spec in specs)
                {
                    int existingIndex = document.Materials.Find(spec.Name, true);
                    if (existingIndex >= 0 && !reuseExistingByName)
                    {
                        warnings.Add(new ObjectEditWarning
                        {
                            Code = "DUPLICATE_MATERIAL_NAME",
                            Message = $"Creating an additional textured material named {spec.Name} because ReuseExistingByName is false."
                        });
                    }

                    bool updateExisting = existingIndex >= 0 && reuseExistingByName;
                    Material material = updateExisting
                        ? new Material(document.Materials[existingIndex])
                        : new Material();

                    ApplyMaterialProperties(material, spec);

                    if (updateExisting)
                    {
                        if (!document.Materials.Modify(material, existingIndex, true))
                        {
                            return OperationResponse<(bool Mutated, TexturedRenderMaterialCreationResponse Result)>.Fail(
                                $"Failed to update textured material: {spec.Name}");
                        }

                        Material updated = document.Materials[existingIndex];
                        materials.Add(ToTexturedResponse(updated, spec, createdNew: false, updatedExisting: true));
                        updatedExistingCount++;
                    }
                    else
                    {
                        int materialIndex = document.Materials.Add(material);
                        if (materialIndex < 0)
                        {
                            return OperationResponse<(bool Mutated, TexturedRenderMaterialCreationResponse Result)>.Fail(
                                $"Failed to add textured material: {spec.Name}");
                        }

                        Material created = document.Materials[materialIndex];
                        materials.Add(ToTexturedResponse(created, spec, createdNew: true, updatedExisting: false));
                        createdCount++;
                    }
                }

                if (createdCount > 0 || updatedExistingCount > 0)
                {
                    document.Views.Redraw();
                }

                var response = new TexturedRenderMaterialCreationResponse
                {
                    FilePath = document.Path,
                    RequestedCount = specs.Count,
                    CreatedCount = createdCount,
                    UpdatedExistingCount = updatedExistingCount,
                    Materials = materials,
                    Warnings = warnings
                };

                return OperationResponse<(bool Mutated, TexturedRenderMaterialCreationResponse Result)>.Ok(
                    (createdCount > 0 || updatedExistingCount > 0, response),
                    "Textured render material creation completed.");
            });
    }

    public OperationResponse<RenderMaterialTextureInspectionResponse> InspectRenderMaterialTextures(
        string filePath,
        IReadOnlyList<string> materialNames)
    {
        return _documentAccessor.Execute(
            filePath,
            document =>
            {
                var inspected = new List<RenderMaterialTextureInspectionItemResponse>();
                var warnings = new List<ObjectEditWarning>();
                HashSet<string>? nameFilter = materialNames.Count > 0
                    ? materialNames.ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : null;

                for (int i = 0; i < document.Materials.Count; i++)
                {
                    Material material = document.Materials[i];
                    string materialName = string.IsNullOrWhiteSpace(material.Name) ? $"Material {i}" : material.Name;
                    if (nameFilter is not null && !nameFilter.Contains(materialName))
                    {
                        continue;
                    }

                    inspected.Add(ToTextureInspectionResponse(material));
                }

                if (nameFilter is not null)
                {
                    foreach (string requested in nameFilter)
                    {
                        if (!inspected.Any(item => string.Equals(item.Name, requested, StringComparison.OrdinalIgnoreCase)))
                        {
                            warnings.Add(new ObjectEditWarning
                            {
                                Code = "MATERIAL_NOT_FOUND",
                                Message = $"Material not found: {requested}"
                            });
                        }
                    }
                }

                return OperationResponse<RenderMaterialTextureInspectionResponse>.Ok(
                    new RenderMaterialTextureInspectionResponse
                    {
                        FilePath = document.Path,
                        InspectedCount = inspected.Count,
                        Materials = inspected,
                        Warnings = warnings
                    },
                    "Render material texture inspection completed.");
            });
    }

    public OperationResponse<ObjectMaterialAssignmentResponse> ApplyObjectMaterials(
        string filePath,
        IReadOnlyList<ObjectMaterialAssignmentSpec> assignments)
    {
        return _documentAccessor.ExecuteWithUndo(
            filePath,
            "MCP: ApplyObjectMaterials",
            document =>
            {
                var results = new List<ObjectMaterialAssignmentResultResponse>();
                var warnings = new List<ObjectEditWarning>();
                int requestedObjectCount = assignments.Sum(item => item.ObjectIds.Count);
                int assignedCount = 0;

                foreach (ObjectMaterialAssignmentSpec assignment in assignments)
                {
                    int materialIndex = document.Materials.Find(assignment.MaterialName, true);
                    if (materialIndex < 0)
                    {
                        foreach (Guid objectId in assignment.ObjectIds)
                        {
                            results.Add(FailAssignment(objectId, assignment.MaterialName, "Material not found."));
                        }

                        warnings.Add(new ObjectEditWarning
                        {
                            Code = "MATERIAL_NOT_FOUND",
                            Message = $"Material not found: {assignment.MaterialName}"
                        });
                        continue;
                    }

                    foreach (Guid objectId in assignment.ObjectIds)
                    {
                        RhinoObject? rhinoObject = document.Objects.FindId(objectId);
                        if (rhinoObject is null || rhinoObject.IsDeleted)
                        {
                            results.Add(FailAssignment(objectId, assignment.MaterialName, "Object not found."));
                            continue;
                        }

                        ObjectAttributes attributes = rhinoObject.Attributes.Duplicate();
                        attributes.MaterialIndex = materialIndex;
                        attributes.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                        attributes.SetUserString("mcp.capability", "reference-image-object-modeling");
                        attributes.SetUserString("mcp.modeling.stage", "material");
                        attributes.SetUserString("mcp.material.name", assignment.MaterialName);
                        foreach ((string key, string value) in assignment.UserText)
                        {
                            attributes.SetUserString(key, value);
                        }

                        if (!document.Objects.ModifyAttributes(objectId, attributes, true))
                        {
                            results.Add(FailAssignment(objectId, assignment.MaterialName, "ModifyAttributes failed."));
                            continue;
                        }

                        results.Add(new ObjectMaterialAssignmentResultResponse
                        {
                            ObjectId = objectId,
                            MaterialName = assignment.MaterialName,
                            MaterialIndex = materialIndex,
                            Assigned = true,
                            Message = "Assigned."
                        });
                        assignedCount++;
                    }
                }

                if (assignedCount > 0)
                {
                    document.Views.Redraw();
                }

                var response = new ObjectMaterialAssignmentResponse
                {
                    FilePath = document.Path,
                    RequestedObjectCount = requestedObjectCount,
                    AssignedCount = assignedCount,
                    FailedCount = requestedObjectCount - assignedCount,
                    Results = results,
                    Warnings = warnings
                };

                return OperationResponse<(bool Mutated, ObjectMaterialAssignmentResponse Result)>.Ok(
                    (assignedCount > 0, response),
                    "Object material assignment completed.");
            });
    }

    public OperationResponse<TextureMappingPreviewResponse> PreviewTextureMapping(
        string filePath,
        IReadOnlyList<TextureMappingSpec> mappings)
    {
        return _documentAccessor.Execute(
            filePath,
            document =>
            {
                var results = new List<TextureMappingResultResponse>(mappings.Count);
                var warnings = new List<ObjectEditWarning>();
                foreach (TextureMappingSpec mapping in mappings)
                {
                    results.Add(PreviewMapping(document, mapping, warnings));
                }

                int successCount = results.Count(result => result.Success);
                return OperationResponse<TextureMappingPreviewResponse>.Ok(
                    new TextureMappingPreviewResponse
                    {
                        FilePath = document.Path,
                        RequestedObjectCount = mappings.Count,
                        PreviewableCount = successCount,
                        FailedCount = mappings.Count - successCount,
                        Results = results,
                        Warnings = warnings
                    },
                    "Texture mapping preview completed.");
            });
    }

    public OperationResponse<TextureMappingApplicationResponse> ApplyTextureMapping(
        string filePath,
        IReadOnlyList<TextureMappingSpec> mappings)
    {
        return _documentAccessor.ExecuteWithUndo(
            filePath,
            "MCP: ApplyTextureMapping",
            document =>
            {
                var results = new List<TextureMappingResultResponse>(mappings.Count);
                var warnings = new List<ObjectEditWarning>();
                int appliedCount = 0;

                foreach (TextureMappingSpec mapping in mappings)
                {
                    TextureMappingResultResponse preview = PreviewMapping(document, mapping, warnings);
                    if (!preview.Success)
                    {
                        results.Add(preview);
                        continue;
                    }

                    RhinoObject? rhinoObject = document.Objects.FindId(mapping.ObjectId);
                    if (rhinoObject is null || rhinoObject.IsDeleted)
                    {
                        results.Add(FailTextureMapping(mapping, "Object not found."));
                        continue;
                    }

                    TextureMapping textureMapping = BuildTextureMapping(rhinoObject, mapping, out _, out _, out _);
                    if (!document.Objects.ModifyTextureMapping(mapping.ObjectId, mapping.MappingChannel, textureMapping))
                    {
                        results.Add(FailTextureMapping(mapping, "ModifyTextureMapping failed."));
                        continue;
                    }

                    results.Add(preview);
                    appliedCount++;
                }

                if (appliedCount > 0)
                {
                    document.Views.Redraw();
                }

                return OperationResponse<(bool Mutated, TextureMappingApplicationResponse Result)>.Ok(
                    (appliedCount > 0, new TextureMappingApplicationResponse
                    {
                        FilePath = document.Path,
                        RequestedObjectCount = mappings.Count,
                        AppliedCount = appliedCount,
                        FailedCount = mappings.Count - appliedCount,
                        Results = results,
                        Warnings = warnings
                    }),
                    "Texture mapping application completed.");
            });
    }

    private static RenderMaterialCreatedResponse ToResponse(
        Material material,
        RenderMaterialCreationSpec spec,
        bool reusedExisting)
    {
        return new RenderMaterialCreatedResponse
        {
            MaterialIndex = material.Index,
            MaterialId = material.Id,
            Name = string.IsNullOrWhiteSpace(material.Name) ? spec.Name : material.Name,
            BaseColor = new ObjectColorResponse
            {
                R = spec.BaseColor.R,
                G = spec.BaseColor.G,
                B = spec.BaseColor.B
            },
            Roughness = spec.Roughness,
            Transparency = spec.Transparency,
            ReusedExisting = reusedExisting
        };
    }

    private static void ApplyMaterialProperties(Material material, TexturedRenderMaterialCreationSpec spec)
    {
        material.Name = spec.Name;
        material.DiffuseColor = spec.BaseColor.ToColor();
        material.Transparency = spec.Transparency;
        material.Shine = (1d - spec.Roughness) * MaxRhinoShine;
        material.SetBitmapTexture(spec.DiffuseTextureImagePath);

        Texture? texture = material.GetBitmapTexture();
        if (texture is not null)
        {
            texture.Enabled = true;
            material.SetBitmapTexture(texture);
        }
    }

    private static TexturedRenderMaterialCreatedResponse ToTexturedResponse(
        Material material,
        TexturedRenderMaterialCreationSpec spec,
        bool createdNew,
        bool updatedExisting)
    {
        Texture? texture = material.GetBitmapTexture();
        return new TexturedRenderMaterialCreatedResponse
        {
            MaterialIndex = material.Index,
            MaterialId = material.Id,
            Name = string.IsNullOrWhiteSpace(material.Name) ? spec.Name : material.Name,
            BaseColor = new ObjectColorResponse
            {
                R = spec.BaseColor.R,
                G = spec.BaseColor.G,
                B = spec.BaseColor.B
            },
            Roughness = spec.Roughness,
            Transparency = spec.Transparency,
            DiffuseTextureImagePath = texture?.FileName ?? spec.DiffuseTextureImagePath,
            MappingChannel = texture?.MappingChannelId ?? spec.MappingChannel,
            CreatedNew = createdNew,
            UpdatedExisting = updatedExisting
        };
    }

    private static RenderMaterialTextureInspectionItemResponse ToTextureInspectionResponse(Material material)
    {
        Texture? texture = material.GetBitmapTexture();
        return new RenderMaterialTextureInspectionItemResponse
        {
            MaterialIndex = material.Index,
            MaterialId = material.Id,
            Name = string.IsNullOrWhiteSpace(material.Name) ? $"Material {material.Index}" : material.Name,
            DiffuseTextureImagePath = texture?.FileName ?? string.Empty,
            MappingChannel = texture?.MappingChannelId ?? 0,
            HasDiffuseTexture = texture is not null && !string.IsNullOrWhiteSpace(texture.FileName)
        };
    }

    private static TextureMappingResultResponse PreviewMapping(
        RhinoDoc document,
        TextureMappingSpec mapping,
        List<ObjectEditWarning> warnings)
    {
        RhinoObject? rhinoObject = document.Objects.FindId(mapping.ObjectId);
        if (rhinoObject is null || rhinoObject.IsDeleted)
        {
            return FailTextureMapping(mapping, "Object not found.");
        }

        try
        {
            BuildTextureMapping(rhinoObject, mapping, out double width, out double depth, out double height);
            return new TextureMappingResultResponse
            {
                ObjectId = mapping.ObjectId,
                MappingChannel = mapping.MappingChannel,
                MappingKind = mapping.MappingKind,
                Success = true,
                Message = "Texture mapping can be applied.",
                Width = width,
                Depth = depth,
                Height = height
            };
        }
        catch (NotSupportedException ex)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "TEXTURE_MAPPING_UNSUPPORTED",
                Message = ex.Message
            });
            return FailTextureMapping(mapping, ex.Message);
        }
    }

    private static TextureMapping BuildTextureMapping(
        RhinoObject rhinoObject,
        TextureMappingSpec spec,
        out double width,
        out double depth,
        out double height)
    {
        BoundingBox box = rhinoObject.Geometry.GetBoundingBox(true);
        if (!box.IsValid)
        {
            throw new NotSupportedException($"Object {spec.ObjectId} does not have a valid bounding box.");
        }

        width = spec.Width > 0d ? spec.Width : Math.Max(box.Max.X - box.Min.X, 0.001d);
        depth = spec.Depth > 0d ? spec.Depth : Math.Max(box.Max.Y - box.Min.Y, 0.001d);
        height = spec.Height > 0d ? spec.Height : Math.Max(box.Max.Z - box.Min.Z, 0.001d);
        Point3d center = box.Center + new Vector3d(spec.OffsetX, spec.OffsetY, spec.OffsetZ);
        Plane plane = BuildMappingPlane(center, spec.RotationDegrees);

        return spec.MappingKind switch
        {
            TextureMappingKind.Plane => TextureMapping.CreatePlaneMapping(
                plane,
                new Interval(-width * 0.5d, width * 0.5d),
                new Interval(-depth * 0.5d, depth * 0.5d),
                new Interval(-height * 0.5d, height * 0.5d)),
            TextureMappingKind.SurfaceParameter => TextureMapping.CreateSurfaceParameterMapping(),
            TextureMappingKind.Cylindrical => TextureMapping.CreateCylinderMapping(
                new Cylinder(new Circle(plane, Math.Max(width, depth) * 0.5d), height),
                spec.Capped),
            TextureMappingKind.Spherical => TextureMapping.CreateSphereMapping(
                new Sphere(center, Math.Max(width, Math.Max(depth, height)) * 0.5d)),
            _ => TextureMapping.CreateBoxMapping(
                plane,
                new Interval(-width * 0.5d, width * 0.5d),
                new Interval(-depth * 0.5d, depth * 0.5d),
                new Interval(-height * 0.5d, height * 0.5d),
                spec.Capped)
        };
    }

    private static Plane BuildMappingPlane(Point3d origin, double rotationDegrees)
    {
        Vector3d xAxis = Vector3d.XAxis;
        Vector3d yAxis = Vector3d.YAxis;
        if (Math.Abs(rotationDegrees) > double.Epsilon)
        {
            Transform rotation = Transform.Rotation(
                rotationDegrees * Math.PI / 180d,
                Vector3d.ZAxis,
                origin);
            xAxis.Transform(rotation);
            yAxis.Transform(rotation);
        }

        return new Plane(origin, xAxis, yAxis);
    }

    private static TextureMappingResultResponse FailTextureMapping(TextureMappingSpec mapping, string message)
    {
        return new TextureMappingResultResponse
        {
            ObjectId = mapping.ObjectId,
            MappingChannel = mapping.MappingChannel,
            MappingKind = mapping.MappingKind,
            Success = false,
            Message = message
        };
    }

    private static ObjectMaterialAssignmentResultResponse FailAssignment(
        Guid objectId,
        string materialName,
        string message)
    {
        return new ObjectMaterialAssignmentResultResponse
        {
            ObjectId = objectId,
            MaterialName = materialName,
            MaterialIndex = -1,
            Assigned = false,
            Message = message
        };
    }
}

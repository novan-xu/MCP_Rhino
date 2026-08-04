using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoMaterialService
{
    private readonly ILiveRhinoMaterialOperator _operator;

    public RhinoMaterialService(ILiveRhinoMaterialOperator @operator)
    {
        _operator = @operator;
    }

    public OperationResponse<RenderMaterialCreationResponse> Create(CreateRenderMaterialsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<RenderMaterialCreationResponse>.Fail("FilePath is required.");
        }

        if (request.Items.Count == 0)
        {
            return OperationResponse<RenderMaterialCreationResponse>.Fail("At least one material item is required.");
        }

        var specs = new List<RenderMaterialCreationSpec>(request.Items.Count);
        foreach (RenderMaterialItemRequest item in request.Items)
        {
            OperationResponse validation = ValidateMaterialItem(item);
            if (!validation.Success)
            {
                return OperationResponse<RenderMaterialCreationResponse>.Fail(validation.Message);
            }

            specs.Add(new RenderMaterialCreationSpec
            {
                Name = item.Name.Trim(),
                BaseColor = new RhinoDisplayColor
                {
                    R = item.BaseColor.R,
                    G = item.BaseColor.G,
                    B = item.BaseColor.B
                },
                Roughness = item.Roughness,
                Transparency = item.Transparency
            });
        }

        return _operator.CreateRenderMaterials(request.FilePath, specs, request.ReuseExistingByName);
    }

    public OperationResponse<TexturedRenderMaterialCreationResponse> CreateTextured(
        CreateTexturedRenderMaterialsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<TexturedRenderMaterialCreationResponse>.Fail("FilePath is required.");
        }

        if (request.Items.Count == 0)
        {
            return OperationResponse<TexturedRenderMaterialCreationResponse>.Fail("At least one textured material item is required.");
        }

        var specs = new List<TexturedRenderMaterialCreationSpec>(request.Items.Count);
        foreach (TexturedRenderMaterialItemRequest item in request.Items)
        {
            OperationResponse validation = ValidateTexturedMaterialItem(item);
            if (!validation.Success)
            {
                return OperationResponse<TexturedRenderMaterialCreationResponse>.Fail(validation.Message);
            }

            specs.Add(new TexturedRenderMaterialCreationSpec
            {
                Name = item.Name.Trim(),
                BaseColor = new RhinoDisplayColor
                {
                    R = item.BaseColor.R,
                    G = item.BaseColor.G,
                    B = item.BaseColor.B
                },
                Roughness = item.Roughness,
                Transparency = item.Transparency,
                DiffuseTextureImagePath = Path.GetFullPath(item.DiffuseTextureImagePath.Trim()),
                MappingChannel = item.MappingChannel
            });
        }

        return _operator.CreateTexturedRenderMaterials(request.FilePath, specs, request.ReuseExistingByName);
    }

    public OperationResponse<RenderMaterialTextureInspectionResponse> InspectTextures(
        InspectRenderMaterialTexturesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<RenderMaterialTextureInspectionResponse>.Fail("FilePath is required.");
        }

        return _operator.InspectRenderMaterialTextures(
            request.FilePath,
            request.MaterialNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    public OperationResponse<ObjectMaterialAssignmentResponse> Apply(ApplyObjectMaterialsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<ObjectMaterialAssignmentResponse>.Fail("FilePath is required.");
        }

        if (request.Assignments.Count == 0)
        {
            return OperationResponse<ObjectMaterialAssignmentResponse>.Fail("At least one material assignment is required.");
        }

        var specs = new List<ObjectMaterialAssignmentSpec>(request.Assignments.Count);
        foreach (ObjectMaterialAssignmentRequest assignment in request.Assignments)
        {
            OperationResponse validation = ValidateAssignment(assignment);
            if (!validation.Success)
            {
                return OperationResponse<ObjectMaterialAssignmentResponse>.Fail(validation.Message);
            }

            specs.Add(new ObjectMaterialAssignmentSpec
            {
                MaterialName = assignment.MaterialName.Trim(),
                ObjectIds = assignment.ObjectIds.Distinct().ToList(),
                UserText = new Dictionary<string, string>(assignment.UserText, StringComparer.OrdinalIgnoreCase)
            });
        }

        return _operator.ApplyObjectMaterials(request.FilePath, specs);
    }

    public OperationResponse<TextureMappingPreviewResponse> PreviewTextureMapping(PreviewTextureMappingRequest request)
    {
        OperationResponse<IReadOnlyList<TextureMappingSpec>> validation = ValidateTextureMappings(
            request.FilePath,
            request.Items);
        if (!validation.Success || validation.Data is null)
        {
            return OperationResponse<TextureMappingPreviewResponse>.Fail(validation.Message);
        }

        return _operator.PreviewTextureMapping(request.FilePath, validation.Data);
    }

    public OperationResponse<TextureMappingApplicationResponse> ApplyTextureMapping(ApplyTextureMappingRequest request)
    {
        OperationResponse<IReadOnlyList<TextureMappingSpec>> validation = ValidateTextureMappings(
            request.FilePath,
            request.Items);
        if (!validation.Success || validation.Data is null)
        {
            return OperationResponse<TextureMappingApplicationResponse>.Fail(validation.Message);
        }

        return _operator.ApplyTextureMapping(request.FilePath, validation.Data);
    }

    private static OperationResponse ValidateMaterialItem(RenderMaterialItemRequest item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            return OperationResponse.Fail("Material name is required.");
        }

        if (item.BaseColor.R is < 0 or > 255 || item.BaseColor.G is < 0 or > 255 || item.BaseColor.B is < 0 or > 255)
        {
            return OperationResponse.Fail("Material BaseColor values must be in the 0-255 range.");
        }

        if (!IsUnitInterval(item.Roughness))
        {
            return OperationResponse.Fail("Material Roughness must be between 0 and 1.");
        }

        if (!IsUnitInterval(item.Transparency))
        {
            return OperationResponse.Fail("Material Transparency must be between 0 and 1.");
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateTexturedMaterialItem(TexturedRenderMaterialItemRequest item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            return OperationResponse.Fail("Material name is required.");
        }

        if (item.BaseColor.R is < 0 or > 255 || item.BaseColor.G is < 0 or > 255 || item.BaseColor.B is < 0 or > 255)
        {
            return OperationResponse.Fail("Material BaseColor values must be in the 0-255 range.");
        }

        if (!IsUnitInterval(item.Roughness))
        {
            return OperationResponse.Fail("Material Roughness must be between 0 and 1.");
        }

        if (!IsUnitInterval(item.Transparency))
        {
            return OperationResponse.Fail("Material Transparency must be between 0 and 1.");
        }

        if (string.IsNullOrWhiteSpace(item.DiffuseTextureImagePath))
        {
            return OperationResponse.Fail("DiffuseTextureImagePath is required.");
        }

        string texturePath = Path.GetFullPath(item.DiffuseTextureImagePath.Trim());
        if (!File.Exists(texturePath))
        {
            return OperationResponse.Fail($"Diffuse texture image was not found: {texturePath}");
        }

        if (item.MappingChannel < 1)
        {
            return OperationResponse.Fail("MappingChannel must be greater than zero.");
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateAssignment(ObjectMaterialAssignmentRequest assignment)
    {
        if (string.IsNullOrWhiteSpace(assignment.MaterialName))
        {
            return OperationResponse.Fail("Assignment MaterialName is required.");
        }

        if (assignment.ObjectIds.Count == 0)
        {
            return OperationResponse.Fail("Assignment requires at least one ObjectId.");
        }

        if (assignment.ObjectIds.Any(id => id == Guid.Empty))
        {
            return OperationResponse.Fail("Assignment ObjectIds cannot include an empty Guid.");
        }

        foreach ((string key, _) in assignment.UserText)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return OperationResponse.Fail("Assignment UserText keys cannot be empty.");
            }
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse<IReadOnlyList<TextureMappingSpec>> ValidateTextureMappings(
        string filePath,
        List<TextureMappingItemRequest> items)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResponse<IReadOnlyList<TextureMappingSpec>>.Fail("FilePath is required.");
        }

        if (items.Count == 0)
        {
            return OperationResponse<IReadOnlyList<TextureMappingSpec>>.Fail("At least one texture mapping item is required.");
        }

        var specs = new List<TextureMappingSpec>(items.Count);
        foreach (TextureMappingItemRequest item in items)
        {
            if (item.ObjectId == Guid.Empty)
            {
                return OperationResponse<IReadOnlyList<TextureMappingSpec>>.Fail("Texture mapping ObjectId cannot be empty.");
            }

            if (item.MappingChannel < 1)
            {
                return OperationResponse<IReadOnlyList<TextureMappingSpec>>.Fail("Texture mapping MappingChannel must be greater than zero.");
            }

            if (!IsFiniteNonNegative(item.Width) || !IsFiniteNonNegative(item.Depth) || !IsFiniteNonNegative(item.Height))
            {
                return OperationResponse<IReadOnlyList<TextureMappingSpec>>.Fail("Texture mapping Width, Depth, and Height must be finite values greater than or equal to zero.");
            }

            if (!IsFinite(item.OffsetX) || !IsFinite(item.OffsetY) || !IsFinite(item.OffsetZ) || !IsFinite(item.RotationDegrees))
            {
                return OperationResponse<IReadOnlyList<TextureMappingSpec>>.Fail("Texture mapping offsets and rotation must be finite values.");
            }

            specs.Add(new TextureMappingSpec
            {
                ObjectId = item.ObjectId,
                MappingKind = item.MappingKind,
                MappingChannel = item.MappingChannel,
                Width = item.Width,
                Depth = item.Depth,
                Height = item.Height,
                OffsetX = item.OffsetX,
                OffsetY = item.OffsetY,
                OffsetZ = item.OffsetZ,
                RotationDegrees = item.RotationDegrees,
                Capped = item.Capped
            });
        }

        return OperationResponse<IReadOnlyList<TextureMappingSpec>>.Ok(specs);
    }

    private static bool IsUnitInterval(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value is >= 0d and <= 1d;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool IsFiniteNonNegative(double value)
    {
        return IsFinite(value) && value >= 0d;
    }
}

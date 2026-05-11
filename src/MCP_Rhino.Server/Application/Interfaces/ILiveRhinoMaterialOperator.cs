using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveRhinoMaterialOperator
{
    OperationResponse<RenderMaterialCreationResponse> CreateRenderMaterials(
        string filePath,
        IReadOnlyList<RenderMaterialCreationSpec> specs,
        bool reuseExistingByName);

    OperationResponse<TexturedRenderMaterialCreationResponse> CreateTexturedRenderMaterials(
        string filePath,
        IReadOnlyList<TexturedRenderMaterialCreationSpec> specs,
        bool reuseExistingByName);

    OperationResponse<RenderMaterialTextureInspectionResponse> InspectRenderMaterialTextures(
        string filePath,
        IReadOnlyList<string> materialNames);

    OperationResponse<ObjectMaterialAssignmentResponse> ApplyObjectMaterials(
        string filePath,
        IReadOnlyList<ObjectMaterialAssignmentSpec> assignments);

    OperationResponse<TextureMappingPreviewResponse> PreviewTextureMapping(
        string filePath,
        IReadOnlyList<TextureMappingSpec> mappings);

    OperationResponse<TextureMappingApplicationResponse> ApplyTextureMapping(
        string filePath,
        IReadOnlyList<TextureMappingSpec> mappings);
}

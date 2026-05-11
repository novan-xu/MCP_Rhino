using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Materials;

[McpServerToolType]
public sealed class CreateRenderMaterialsTool
{
    private readonly RhinoMaterialService _service;

    public CreateRenderMaterialsTool(RhinoMaterialService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create named Rhino document materials from structured color, roughness, and transparency values in the current live Rhino document. Use after reference-image material brief extraction; this does not analyze images or read texture files.")]
    public OperationResponse<RenderMaterialCreationResponse> CreateRenderMaterials(
        string filePath,
        List<RenderMaterialItemRequest> items,
        bool reuseExistingByName = true)
    {
        return _service.Create(new CreateRenderMaterialsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<RenderMaterialItemRequest>(),
            ReuseExistingByName = reuseExistingByName
        });
    }
}

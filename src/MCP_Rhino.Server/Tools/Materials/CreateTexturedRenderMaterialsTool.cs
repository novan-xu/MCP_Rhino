using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Materials;

[McpServerToolType]
public sealed class CreateTexturedRenderMaterialsTool
{
    private readonly RhinoMaterialService _service;

    public CreateTexturedRenderMaterialsTool(RhinoMaterialService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Create or update named Rhino document materials with an explicit diffuse bitmap texture image path, base color, roughness, transparency, and mapping channel. This reads only caller-supplied texture paths and mutates the current live Rhino document material table.")]
    public OperationResponse<TexturedRenderMaterialCreationResponse> CreateTexturedRenderMaterials(
        string filePath,
        List<TexturedRenderMaterialItemRequest> items,
        bool reuseExistingByName = true)
    {
        return _service.CreateTextured(new CreateTexturedRenderMaterialsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<TexturedRenderMaterialItemRequest>(),
            ReuseExistingByName = reuseExistingByName
        });
    }
}

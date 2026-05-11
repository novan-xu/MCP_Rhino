using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Materials;

[McpServerToolType]
public sealed class InspectRenderMaterialTexturesTool
{
    private readonly RhinoMaterialService _service;

    public InspectRenderMaterialTexturesTool(RhinoMaterialService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Inspect Rhino document materials and report diffuse bitmap texture paths and mapping channels. This reads only the current live Rhino document material table and does not inspect external image files.")]
    public OperationResponse<RenderMaterialTextureInspectionResponse> InspectRenderMaterialTextures(
        string filePath,
        List<string>? materialNames = null)
    {
        return _service.InspectTextures(new InspectRenderMaterialTexturesRequest
        {
            FilePath = filePath,
            MaterialNames = materialNames ?? new List<string>()
        });
    }
}

using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Materials;

[McpServerToolType]
public sealed class PreviewTextureMappingTool
{
    private readonly RhinoMaterialService _service;

    public PreviewTextureMappingTool(RhinoMaterialService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview texture mapping for explicit object ids in the current live Rhino document. Reports resolved mapping size and failures without mutating objects.")]
    public OperationResponse<TextureMappingPreviewResponse> PreviewTextureMapping(
        string filePath,
        List<TextureMappingItemRequest> items)
    {
        return _service.PreviewTextureMapping(new PreviewTextureMappingRequest
        {
            FilePath = filePath,
            Items = items ?? new List<TextureMappingItemRequest>()
        });
    }
}

using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Materials;

[McpServerToolType]
public sealed class ApplyTextureMappingTool
{
    private readonly RhinoMaterialService _service;

    public ApplyTextureMappingTool(RhinoMaterialService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Apply box, plane, surface-parameter, cylindrical, or spherical texture mapping to explicit object ids in the current live Rhino document. Use after textured material assignment; reports every changed or failed object.")]
    public OperationResponse<TextureMappingApplicationResponse> ApplyTextureMapping(
        string filePath,
        List<TextureMappingItemRequest> items)
    {
        return _service.ApplyTextureMapping(new ApplyTextureMappingRequest
        {
            FilePath = filePath,
            Items = items ?? new List<TextureMappingItemRequest>()
        });
    }
}

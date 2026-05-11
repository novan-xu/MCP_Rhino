using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.SubD;

[McpServerToolType]
public sealed class CreateSubDCushionsTool
{
    private readonly RhinoSubDModelingService _service;

    public CreateSubDCushionsTool(RhinoSubDModelingService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create crowned and lightly bulged cushion-like SubD cages in the current live Rhino document on an existing layer. Use for upholstered cushions, pillows, and soft pads when dimensions and orientation are known; fabric weave, color variation, highlights, and shadows belong to materials or lighting, not SubD geometry.")]
    public OperationResponse<SubDCreationResponse> CreateSubDCushions(
        string filePath,
        List<SubDCushionItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreateCushions(new CreateSubDCushionsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<SubDCushionItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}

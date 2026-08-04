using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.SubD;

[McpServerToolType]
public sealed class CreateSubDCageTool
{
    private readonly RhinoSubDModelingService _service;

    public CreateSubDCageTool(RhinoSubDModelingService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create SubD objects from explicit bounded cage vertices and triangular or quad faces in the current live Rhino document on an existing layer. Use only when the caller owns the topology for soft or organic geometry; do not use for hard wooden frames, texture, shadow, lighting, labels, or room context.")]
    public OperationResponse<SubDCreationResponse> CreateSubDCage(
        string filePath,
        List<SubDCageItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreateCage(new CreateSubDCageRequest
        {
            FilePath = filePath,
            Items = items ?? new List<SubDCageItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}

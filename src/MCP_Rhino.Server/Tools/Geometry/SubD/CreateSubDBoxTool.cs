using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.SubD;

[McpServerToolType]
public sealed class CreateSubDBoxTool
{
    private readonly RhinoSubDModelingService _service;

    public CreateSubDBoxTool(RhinoSubDModelingService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create oriented box-like SubD control cages in the current live Rhino document on an existing layer. Use for soft rectangular forms where continuous curvature is needed, such as simple cushions or pillows; use tapered/profile/Brep tools for hard planar product members.")]
    public OperationResponse<SubDCreationResponse> CreateSubDBox(
        string filePath,
        List<SubDBoxItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreateBox(new CreateSubDBoxRequest
        {
            FilePath = filePath,
            Items = items ?? new List<SubDBoxItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}

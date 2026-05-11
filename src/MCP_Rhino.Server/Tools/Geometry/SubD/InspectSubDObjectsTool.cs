using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.SubD;

[McpServerToolType]
public sealed class InspectSubDObjectsTool
{
    private readonly RhinoSubDModelingService _service;

    public InspectSubDObjectsTool(RhinoSubDModelingService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Inspect SubD object ids in the current live Rhino document and report type, available topology counts, and bounding boxes without mutation. Use after SubD creation or when deciding whether a soft product form is already represented as SubD.")]
    public OperationResponse<SubDInspectionResponse> InspectSubDObjects(
        string filePath,
        List<Guid> objectIds)
    {
        return _service.Inspect(new InspectSubDObjectsRequest
        {
            FilePath = filePath,
            ObjectIds = objectIds ?? new List<Guid>()
        });
    }
}

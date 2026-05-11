using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Materials;

[McpServerToolType]
public sealed class ApplyObjectMaterialsTool
{
    private readonly RhinoMaterialService _service;

    public ApplyObjectMaterialsTool(RhinoMaterialService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Assign existing named Rhino document materials directly to explicit object ids in the current live Rhino document. This sets object material source to material-from-object and records reference-image material metadata.")]
    public OperationResponse<ObjectMaterialAssignmentResponse> ApplyObjectMaterials(
        string filePath,
        List<ObjectMaterialAssignmentRequest> assignments)
    {
        return _service.Apply(new ApplyObjectMaterialsRequest
        {
            FilePath = filePath,
            Assignments = assignments ?? new List<ObjectMaterialAssignmentRequest>()
        });
    }
}

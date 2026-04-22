using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetMassPropertiesInLiveTool
{
    private readonly RhinoGeometryIntersectionService _service;

    public GetMassPropertiesInLiveTool(RhinoGeometryIntersectionService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Read live mass properties from the active Rhino document. Supports Length, Area, Volume, and Auto selection by geometry type.")]
    public OperationResponse<GetMassPropertiesInLiveResponse> GetMassPropertiesInLive(
        string filePath,
        List<Guid> objectIds,
        GeometryMassKind kind = GeometryMassKind.Auto)
    {
        return _service.GetMassPropertiesInLive(new GetMassPropertiesInLiveRequest
        {
            FilePath = filePath,
            Kind = kind,
            ObjectIds = objectIds ?? new List<Guid>()
        });
    }
}

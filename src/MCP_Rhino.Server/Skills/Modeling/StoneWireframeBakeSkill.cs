using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class StoneWireframeBakeSkill
{
    private readonly RhinoStoneWireframeBakeService _service;

    public StoneWireframeBakeSkill(RhinoStoneWireframeBakeService service)
    {
        _service = service;
    }

    public OperationResponse<StoneWireframeBakeResponse> Preview(StoneWireframeBakeRequest request)
    {
        return _service.Preview(request.FilePath, request.AssemblyDefinitionNames, request.OutputMode, request.SurfaceHeightInches);
    }

    public OperationResponse<StoneWireframeBakeResponse> Apply(StoneWireframeBakeRequest request)
    {
        return _service.Apply(request.FilePath, request.AssemblyDefinitionNames, request.OutputMode, request.SurfaceHeightInches);
    }
}

using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class ReferenceImageProductGeometryPlanningSkill
{
    private readonly ReferenceImageProductGeometryPlanningService _service;

    public ReferenceImageProductGeometryPlanningSkill(
        ReferenceImageProductGeometryPlanningService service)
    {
        _service = service;
    }

    public OperationResponse<ReferenceImageProductGeometryPlanResponse> Plan(
        PlanReferenceImageProductGeometryRequest request)
    {
        return _service.Plan(request);
    }
}

using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class ReferenceImageDetailRefinementSkill
{
    private readonly ReferenceImageDetailRefinementPlanningService _service;

    public ReferenceImageDetailRefinementSkill(ReferenceImageDetailRefinementPlanningService service)
    {
        _service = service;
    }

    public OperationResponse<ReferenceImageDetailRefinementPlanResponse> Plan(
        PlanReferenceImageDetailRefinementRequest request)
    {
        return _service.Plan(request);
    }
}

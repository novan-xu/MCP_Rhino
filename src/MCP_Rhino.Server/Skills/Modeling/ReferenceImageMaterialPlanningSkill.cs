using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class ReferenceImageMaterialPlanningSkill
{
    private readonly ReferenceImageMaterialPlanningService _service;

    public ReferenceImageMaterialPlanningSkill(ReferenceImageMaterialPlanningService service)
    {
        _service = service;
    }

    public OperationResponse<ReferenceImageMaterialPlanningResponse> Plan(
        PlanReferenceImageMaterialsRequest request)
    {
        return _service.Plan(request);
    }
}

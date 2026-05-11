using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class ReferenceImageIterationDecisionSkill
{
    private readonly ReferenceImageIterationDecisionService _service;

    public ReferenceImageIterationDecisionSkill(ReferenceImageIterationDecisionService service)
    {
        _service = service;
    }

    public OperationResponse<ReferenceImageIterationDecisionResponse> Decide(
        DecideReferenceImageIterationRequest request)
    {
        return _service.Decide(request);
    }
}

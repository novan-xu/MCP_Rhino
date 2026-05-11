using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class ReferenceImageModelBriefSkill
{
    private readonly ReferenceImageModelingBriefService _service;

    public ReferenceImageModelBriefSkill(ReferenceImageModelingBriefService service)
    {
        _service = service;
    }

    public OperationResponse<ReferenceImageModelBriefResponse> Build(BuildReferenceImageModelBriefRequest request)
    {
        return _service.Build(request);
    }
}

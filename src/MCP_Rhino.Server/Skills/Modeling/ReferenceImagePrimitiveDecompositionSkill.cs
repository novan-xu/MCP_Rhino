using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class ReferenceImagePrimitiveDecompositionSkill
{
    private readonly ReferenceImagePrimitiveDecompositionService _service;

    public ReferenceImagePrimitiveDecompositionSkill(ReferenceImagePrimitiveDecompositionService service)
    {
        _service = service;
    }

    public OperationResponse<ReferenceImagePrimitiveDecompositionResponse> Decompose(
        DecomposeReferenceImagePrimitivesRequest request)
    {
        return _service.Decompose(request);
    }
}

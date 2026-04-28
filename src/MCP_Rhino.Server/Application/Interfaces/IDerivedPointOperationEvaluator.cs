using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IDerivedPointOperationEvaluator
{
    OperationResponse<DerivedPointEvaluationResult> Evaluate(
        string filePath,
        Guid objectId,
        EditableGeometryDescriptor descriptor,
        CurveEditSpec editSpec);

    OperationResponse<DerivedPointEvaluationResult> EvaluateSurface(
        string filePath,
        Guid objectId,
        EditableGeometryDescriptor descriptor,
        SurfaceEditSpec editSpec);
}

using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IGeometryReconstructor
{
    OperationResponse<CurveReconstructionResult> ReconstructCurve(
        string filePath,
        Guid objectId,
        EditableGeometryDescriptor descriptor,
        IReadOnlyList<EditablePointInput> points);

    OperationResponse<SurfaceReconstructionResult> ReconstructSurface(
        string filePath,
        Guid objectId,
        EditableGeometryDescriptor descriptor,
        IReadOnlyList<EditablePointInput> points);
}

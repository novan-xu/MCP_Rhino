using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IBoundaryReferenceCurveAnalyzer
{
    OperationResponse<SurfaceRebuildDescriptor> Analyze(
        string filePath,
        Guid objectId,
        SurfaceRebuildSpec spec);
}

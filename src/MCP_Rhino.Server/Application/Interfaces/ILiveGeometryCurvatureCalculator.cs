extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveGeometryCurvatureCalculator
{
    OperationResponse<IReadOnlyList<GeometryCurvatureSample>> GetCurvatureSamples(
        CurvatureSampleEntryRequest entry,
        Guid objectId,
        string geometryTypeName,
        GeometryBase geometry);

    OperationResponse<GeometryContinuityResult> CheckContinuity(
        ContinuityCheckEntryRequest entry,
        string firstGeometryTypeName,
        GeometryBase firstGeometry,
        string secondGeometryTypeName,
        GeometryBase secondGeometry);
}

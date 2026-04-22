extern alias rhinocommon;

using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveGeometryIntersectionCalculator
{
    OperationResponse<GeometryMassResult> GetMassProperties(Guid objectId, string geometryTypeName, GeometryBase geometry, GeometryMassKind kind);

    OperationResponse<GeometryIntersectionResult> Intersect(GeometryIntersectionEntryRequest entry, ResolvedGeometryReference firstReference, ResolvedGeometryReference secondReference, double tolerance);

    OperationResponse<GeometryClosestPointResult> GetClosestPoints(GeometryClosestPointEntryRequest entry, ResolvedGeometryReference sourceReference, ResolvedGeometryReference targetReference);

    OperationResponse<GeometryContourResult> GetContourCurves(GeometryContourEntryRequest entry, Guid objectId, string geometryTypeName, GeometryBase geometry);
}

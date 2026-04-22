extern alias rhinocommon;

using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveGeometryMetricsCalculator
{
    OperationResponse<GeometryMetricsResult> GetObjectMetrics(Guid objectId, string geometryTypeName, GeometryBase geometry);

    OperationResponse<GeometryDistanceResult> MeasureDistance(string entryId, ResolvedGeometryReference fromReference, ResolvedGeometryReference toReference, double tolerance);

    OperationResponse<GeometryAngleResult> MeasureThreePointAngle(string entryId, Point3d pointA, Point3d vertex, Point3d pointB);

    OperationResponse<GeometryAngleResult> MeasureVectorAngle(string entryId, Vector3d firstVector, Vector3d secondVector);

    OperationResponse<GeometryFrameResult> GetFrame(string entryId, Guid objectId, string geometryTypeName, GeometryBase geometry, GeometryFrameKind kind, double? parameter, double? u, double? v);
}

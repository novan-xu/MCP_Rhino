using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryIntersectionResult
{
    public string EntryId { get; set; } = string.Empty;
    public GeometryIntersectionKind Kind { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string FirstGeometryTypeName { get; set; } = string.Empty;
    public string SecondGeometryTypeName { get; set; } = string.Empty;
    public IReadOnlyList<GeometryCurvePreview> Curves { get; set; } = Array.Empty<GeometryCurvePreview>();
    public IReadOnlyList<GeometryCurvePreview> OverlapCurves { get; set; } = Array.Empty<GeometryCurvePreview>();
    public IReadOnlyList<GeometryPointData> Points { get; set; } = Array.Empty<GeometryPointData>();
}

namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryContourResult
{
    public string EntryId { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public string GeometryTypeName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<GeometryCurvePreview> Curves { get; set; } = Array.Empty<GeometryCurvePreview>();
}

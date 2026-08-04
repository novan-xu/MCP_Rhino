using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GeometryIntersectionEntryRequest
{
    public string EntryId { get; set; } = string.Empty;
    public GeometryIntersectionKind Kind { get; set; } = GeometryIntersectionKind.Auto;
    public GeometryAnalysisReferenceRequest First { get; set; } = new();
    public GeometryAnalysisReferenceRequest Second { get; set; } = new();
    public double? Tolerance { get; set; }
}

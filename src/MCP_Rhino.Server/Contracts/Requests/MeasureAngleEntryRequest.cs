using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class MeasureAngleEntryRequest
{
    public string EntryId { get; set; } = string.Empty;
    public GeometryAngleMeasurementMode Mode { get; set; } = GeometryAngleMeasurementMode.TwoVectors;
    public GeometryAnalysisReferenceRequest? PointA { get; set; }
    public GeometryAnalysisReferenceRequest? Vertex { get; set; }
    public GeometryAnalysisReferenceRequest? PointB { get; set; }
    public GeometryAnalysisVectorRequest? FirstVector { get; set; }
    public GeometryAnalysisVectorRequest? SecondVector { get; set; }
    public GeometryAnalysisReferenceRequest? First { get; set; }
    public GeometryAnalysisReferenceRequest? Second { get; set; }
}

using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class CurvatureSampleEntryRequest
{
    public string EntryId { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public GeometryCurvatureSamplingMode Mode { get; set; } = GeometryCurvatureSamplingMode.EvenByCount;
    public int SampleCount { get; set; } = 10;
    public double StepLength { get; set; }
    public List<double> Parameters { get; set; } = new();
    public List<GeometryAnalysisUvSampleRequest> UvSamples { get; set; } = new();
}

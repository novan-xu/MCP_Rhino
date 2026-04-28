using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GeometryFrameParameterSpecRequest
{
    public GeometryFrameParameterSpecKind Kind { get; set; } = GeometryFrameParameterSpecKind.Legacy;
    public List<double> Parameters { get; set; } = new();
    public List<GeometryAnalysisUvSampleRequest> UvParameters { get; set; } = new();
    public List<double> Fractions { get; set; } = new();
}

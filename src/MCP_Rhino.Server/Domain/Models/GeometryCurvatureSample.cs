namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryCurvatureSample
{
    public string EntryId { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public string GeometryTypeName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double? Parameter { get; set; }
    public double? U { get; set; }
    public double? V { get; set; }
    public GeometryPointData? Point { get; set; }
    public GeometryVectorData? CurvatureVector { get; set; }
    public double? CurvatureMagnitude { get; set; }
    public double? GaussianCurvature { get; set; }
    public double? MeanCurvature { get; set; }
}

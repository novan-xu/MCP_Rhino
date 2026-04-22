namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryPointData
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class GeometryVectorData
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class GeometryCurvePreview
{
    public string CurveTypeName { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public double? Length { get; set; }
    public IReadOnlyList<GeometryPointData> SamplePoints { get; set; } = Array.Empty<GeometryPointData>();
}

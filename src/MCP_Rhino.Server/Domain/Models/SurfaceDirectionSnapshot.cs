namespace MCP_Rhino.Server.Domain.Models;

public sealed class SurfaceDirectionSnapshot
{
    public string GeometryKind { get; set; } = string.Empty;
    public SurfaceDirectionDomainData UDomain { get; set; } = new();
    public SurfaceDirectionDomainData VDomain { get; set; } = new();
    public GeometryPointData SamplePoint { get; set; } = new();
    public GeometryVectorData UTangent { get; set; } = new();
    public GeometryVectorData VTangent { get; set; } = new();
    public GeometryVectorData Normal { get; set; } = new();
    public bool? FaceOrientationIsReversed { get; set; }
    public int? SolidOrientation { get; set; }
}

public sealed class SurfaceDirectionDomainData
{
    public double T0 { get; set; }
    public double T1 { get; set; }
}

namespace MCP_Rhino.Server.Domain.Models;

public sealed class BrepDowngradeResult
{
    public bool IsUntrimmedSingleFaceBrep { get; set; }
    public string Reason { get; set; } = string.Empty;
    public double Tolerance { get; set; }
    public string SurfaceTypeName { get; set; } = string.Empty;
}

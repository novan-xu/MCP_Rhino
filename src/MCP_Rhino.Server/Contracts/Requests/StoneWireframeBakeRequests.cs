using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class StoneWireframeBakeRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> AssemblyDefinitionNames { get; set; } = new();
    public StoneBakeOutputMode OutputMode { get; set; } = StoneBakeOutputMode.Curve;
    public double SurfaceHeightInches { get; set; } = 5;
}

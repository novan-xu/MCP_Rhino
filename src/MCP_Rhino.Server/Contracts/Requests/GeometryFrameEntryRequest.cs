using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GeometryFrameEntryRequest
{
    public string EntryId { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public GeometryFrameKind Kind { get; set; } = GeometryFrameKind.SurfaceFrame;
    public double? Parameter { get; set; }
    public double? U { get; set; }
    public double? V { get; set; }
    public GeometryFrameParameterSpecRequest? ParameterSpec { get; set; }
}

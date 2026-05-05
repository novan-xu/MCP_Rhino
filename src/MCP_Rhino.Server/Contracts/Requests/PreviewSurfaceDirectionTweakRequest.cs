using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class PreviewSurfaceDirectionTweakRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<Guid> ConfirmedObjectIds { get; set; } = new();
    public List<SurfaceDirectionTweakKind> Operations { get; set; } = new();
}

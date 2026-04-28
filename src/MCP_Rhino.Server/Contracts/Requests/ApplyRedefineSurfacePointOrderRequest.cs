using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ApplyRedefineSurfacePointOrderRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<Guid> ConfirmedObjectIds { get; set; } = new();
    public SurfaceRebuildSpec Spec { get; set; } = new();
    public bool ReplaceOriginal { get; set; } = true;
}

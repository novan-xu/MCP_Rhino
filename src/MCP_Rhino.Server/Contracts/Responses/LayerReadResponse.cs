using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class LayerReadResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public IReadOnlyList<RhinoLayerDetail> Entries { get; set; } = Array.Empty<RhinoLayerDetail>();
}

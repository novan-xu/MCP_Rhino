using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class SelectObjectsInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<Guid> ObjectIds { get; set; } = new();
    public FilterObjectsRequest? Filter { get; set; }
    public RhinoSelectionMode SelectionMode { get; set; } = RhinoSelectionMode.Replace;
}

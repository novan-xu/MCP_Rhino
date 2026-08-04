using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class PreviewTransformObjectsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public GeometryTransformSpec Transform { get; set; } = new();
    public List<Guid> ConfirmedObjectIds { get; set; } = new();
    public List<string> LayerQueries { get; set; } = new();
    public List<string> ConfirmedLayerFullPaths { get; set; } = new();
    public List<string> ObjectTypes { get; set; } = new();
    public List<UserAttributeConditionRequest> UserAttributeConditions { get; set; } = new();
    public FilterMatchMode MatchMode { get; set; } = FilterMatchMode.All;
    public FilterMatchMode UserAttributeMatchMode { get; set; } = FilterMatchMode.All;
}

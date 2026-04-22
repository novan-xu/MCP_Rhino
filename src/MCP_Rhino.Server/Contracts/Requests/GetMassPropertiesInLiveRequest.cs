using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GetMassPropertiesInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public GeometryMassKind Kind { get; set; } = GeometryMassKind.Auto;
    public List<Guid> ObjectIds { get; set; } = new();
}

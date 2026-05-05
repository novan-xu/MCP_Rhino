namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ApplySurfaceFrontBackFlipRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<Guid> ConfirmedObjectIds { get; set; } = new();
}

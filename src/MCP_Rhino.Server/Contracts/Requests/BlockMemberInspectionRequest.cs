namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class BlockMemberInspectionRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<Guid> ObjectIds { get; set; } = new();
    public int MaxLeaves { get; set; } = 10000;
}

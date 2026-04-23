namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class UpdateLinkedBlockRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> DefinitionNames { get; set; } = new();
}

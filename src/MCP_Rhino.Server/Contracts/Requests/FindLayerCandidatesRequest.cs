namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class FindLayerCandidatesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string LayerQuery { get; set; } = string.Empty;
    public bool ExactMatch { get; set; }
}
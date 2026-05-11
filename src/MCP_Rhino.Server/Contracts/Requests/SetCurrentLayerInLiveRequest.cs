namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class SetCurrentLayerInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public Guid? LayerId { get; set; }
    public string? FullPath { get; set; }
    public string? LayerQuery { get; set; }
    public bool ExactMatch { get; set; } = true;
}

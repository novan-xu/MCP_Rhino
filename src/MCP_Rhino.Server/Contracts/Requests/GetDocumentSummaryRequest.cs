namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GetDocumentSummaryRequest
{
    public string FilePath { get; set; } = string.Empty;
    public int MaxObjectSummaries { get; set; } = 20;
    public int MaxLayerSummaries { get; set; } = 100;
    public int MaxNamedViews { get; set; } = 20;
    public int MaxMaterials { get; set; } = 20;
}

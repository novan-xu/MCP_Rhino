namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ObjectEditOperationResult
{
    public Guid ObjectId { get; set; }
    public string LayerFullPath { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public IReadOnlyList<string> Messages { get; set; } = Array.Empty<string>();
}
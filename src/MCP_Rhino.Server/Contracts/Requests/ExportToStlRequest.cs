namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ExportToStlRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public List<Guid> SelectedObjectIds { get; set; } = new();
    public bool OverwriteExisting { get; set; } = true;
    public Dictionary<string, string> FormatOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

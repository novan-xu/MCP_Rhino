namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ExportToPdfRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string? ViewName { get; set; }
    public PageSizeMmRequest? PageSizeMm { get; set; }
    public double? DotsPerInch { get; set; }
    public bool OverwriteExisting { get; set; } = true;
}

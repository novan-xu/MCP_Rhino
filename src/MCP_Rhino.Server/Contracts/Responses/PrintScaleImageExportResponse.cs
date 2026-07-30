namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class PrintScaleImageExportResponse
{
    public string FilePath { get; set; } = string.Empty;
    public double AppliedModelScale { get; set; }
    public int WidthPx { get; set; }
    public int HeightPx { get; set; }
    public double DotsPerInch { get; set; }
    public bool BackgroundRestored { get; set; }
    public long DurationMs { get; set; }
    public IReadOnlyList<PrintScaleImageExportItemResponse> Images { get; set; } = Array.Empty<PrintScaleImageExportItemResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class PrintScaleImageExportItemResponse
{
    public string ViewName { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public long OutputFileSizeBytes { get; set; }
}

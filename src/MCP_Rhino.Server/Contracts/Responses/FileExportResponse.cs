using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class FileExportResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public FileExportFormat Format { get; set; }
    public int ExportedObjectCount { get; set; }
    public long OutputFileSizeBytes { get; set; }
    public long DurationMs { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

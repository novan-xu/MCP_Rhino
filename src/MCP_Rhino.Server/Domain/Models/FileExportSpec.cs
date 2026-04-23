using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class FileExportSpec
{
    public string OutputPath { get; set; } = string.Empty;
    public FileExportFormat Format { get; set; }
    public bool OverwriteExisting { get; set; } = true;
    public IReadOnlyList<Guid> SelectedObjectIds { get; set; } = Array.Empty<Guid>();
    public string? ViewName { get; set; }
    public FileExportImageSize? ImageSizePx { get; set; }
    public FileExportPageSize? PageSizeMm { get; set; }
    public double? DotsPerInch { get; set; }
    public bool BackgroundTransparent { get; set; }
    public IReadOnlyDictionary<string, string> FormatOptions { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class FileExportImageSize
{
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class FileExportPageSize
{
    public double WidthMm { get; set; }
    public double HeightMm { get; set; }
}

public sealed class FileExportExecutionResult
{
    public int ExportedObjectCount { get; set; }
    public long OutputFileSizeBytes { get; set; }
    public long DurationMs { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

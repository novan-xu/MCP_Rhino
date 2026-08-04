using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class PrintScaleImageExportSpec
{
    public IReadOnlyList<PrintScaleImageViewSpec> Views { get; set; } = Array.Empty<PrintScaleImageViewSpec>();
    public FileExportImageSize ImageSizePx { get; set; } = new();
    public double DotsPerInch { get; set; }
    public PrintScaleImageMode ScaleMode { get; set; }
    public double? ModelScale { get; set; }
    public double FitScaleMultiplier { get; set; }
    public double MarginMm { get; set; }
    public bool BackgroundTransparent { get; set; }
    public RhinoDisplayColor? SolidBackgroundColor { get; set; }
    public bool OverwriteExisting { get; set; }
}

public sealed class PrintScaleImageViewSpec
{
    public string ViewName { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
}

public sealed class PrintScaleImageExportExecutionResult
{
    public double AppliedModelScale { get; set; }
    public bool BackgroundRestored { get; set; }
    public long DurationMs { get; set; }
    public IReadOnlyList<PrintScaleImageExportItemResult> Images { get; set; } = Array.Empty<PrintScaleImageExportItemResult>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class PrintScaleImageExportItemResult
{
    public string ViewName { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public long OutputFileSizeBytes { get; set; }
}

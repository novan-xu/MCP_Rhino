namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class LotVisualizationPreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int TargetObjectCount { get; set; }
    public int AssignedObjectCount { get; set; }
    public int UnassignedObjectCount { get; set; }
    public IReadOnlyList<string> ResolvedLotNumberKeys { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> LayerFullPaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<LotVisualizationLotResponse> Lots { get; set; } = Array.Empty<LotVisualizationLotResponse>();
    public IReadOnlyList<string> ViewNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> OutputPaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class LotVisualizationLotResponse
{
    public string LotNumber { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int ObjectCount { get; set; }
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }
}

public sealed class LotVisualizationExportResponse
{
    public string FilePath { get; set; } = string.Empty;
    public LotVisualizationPreviewResponse Plan { get; set; } = new();
    public int ModifiedObjectCount { get; set; }
    public int CreatedGroupCount { get; set; }
    public int ReusedGroupCount { get; set; }
    public IReadOnlyList<string> CreatedViewNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> UpdatedViewNames { get; set; } = Array.Empty<string>();
    public double AppliedModelScale { get; set; }
    public bool BackgroundRestored { get; set; }
    public IReadOnlyList<PrintScaleImageExportItemResponse> Images { get; set; } = Array.Empty<PrintScaleImageExportItemResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

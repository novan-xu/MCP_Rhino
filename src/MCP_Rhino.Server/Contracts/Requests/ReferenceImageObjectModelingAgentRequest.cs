namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ReferenceImageObjectModelingAgentRequest
{
    public string FilePath { get; set; } = string.Empty;
    public BuildReferenceImageModelBriefRequest BriefRequest { get; set; } = new();
    public string TargetLayerFullPath { get; set; } = string.Empty;
    public double Scale { get; set; } = 1d;
    public bool CreateTargetLayer { get; set; } = true;
    public bool RunVisualQa { get; set; } = true;
    public bool ApplyMaterials { get; set; } = true;
    public bool ExecuteSimpleRaisedStripDetails { get; set; } = true;
    public int MaxMassingCorrectionLoops { get; set; } = 1;
    public int MaxDetailMaterialCorrectionLoops { get; set; } = 1;
    public ImageSizePxRequest? QaImageSizePx { get; set; }
    public List<string> QaFindings { get; set; } = new();
}

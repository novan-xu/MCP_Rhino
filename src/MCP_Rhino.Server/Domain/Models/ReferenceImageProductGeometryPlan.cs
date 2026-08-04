using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class ReferenceImageProductGeometryPlan
{
    public string ReferenceImageLabel { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public int PlannedObjectCount { get; set; }
    public int BlockingGapCount { get; set; }
    public List<ReferenceImageProductGeometryPlanPart> Parts { get; set; } = new();
    public List<string> MaterialOnlyCues { get; set; } = new();
    public List<string> ReferenceOnlyCues { get; set; } = new();
    public List<string> CapabilityGaps { get; set; } = new();
}

public sealed class ReferenceImageProductGeometryPlanPart
{
    public string PartName { get; set; } = string.Empty;
    public ReferenceImagePartRole Role { get; set; }
    public ReferenceImagePrimitiveVocabularyKind Primitive { get; set; }
    public ReferenceImageProductGeometryStrategyKind Strategy { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public bool HasLocalFrame { get; set; }
    public bool HasTaper { get; set; }
    public bool HasProfile { get; set; }
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double SizeX { get; set; }
    public double SizeY { get; set; }
    public double SizeZ { get; set; }
    public List<string> Notes { get; set; } = new();
}

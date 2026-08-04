using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class ReferenceImageMaterialPlan
{
    public string ReferenceImageLabel { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public List<ReferenceImagePlannedMaterial> Materials { get; set; } = new();
    public List<ReferenceImageMaterialAssignmentPlan> Assignments { get; set; } = new();
    public List<string> Fallbacks { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class ReferenceImagePlannedMaterial
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public RhinoDisplayColor BaseColor { get; set; } = new() { R = 160, G = 160, B = 160 };
    public ReferenceImageMaterialIntent Intent { get; set; } = ReferenceImageMaterialIntent.Unknown;
    public double Roughness { get; set; } = 0.65d;
    public double Transparency { get; set; }
    public bool TextureLikely { get; set; }
    public string TextureFallback { get; set; } = string.Empty;
}

public sealed class ReferenceImageMaterialAssignmentPlan
{
    public string MaterialKey { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public List<string> PartNames { get; set; } = new();
    public List<Guid> ObjectIds { get; set; } = new();
}

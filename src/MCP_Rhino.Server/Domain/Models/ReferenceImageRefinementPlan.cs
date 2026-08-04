using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class ReferenceImageRefinementPlan
{
    public string ReferenceImageLabel { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public List<ReferenceImageRefinementAction> Actions { get; set; } = new();
    public List<string> SuppressedTextureLikeDetails { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class ReferenceImageRefinementAction
{
    public ReferenceImageRefinementActionKind Kind { get; set; }
    public string PartName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ReferenceImagePrimitiveVocabularyKind SuggestedPrimitive { get; set; } = ReferenceImagePrimitiveVocabularyKind.Unknown;
    public ReferenceImageToolFamily ToolFamily { get; set; } = ReferenceImageToolFamily.None;
    public double Priority { get; set; } = 0.5d;
    public string Rationale { get; set; } = string.Empty;
}

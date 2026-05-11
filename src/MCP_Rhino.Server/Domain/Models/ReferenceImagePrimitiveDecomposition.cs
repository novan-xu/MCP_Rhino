using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class ReferenceImagePrimitiveDecomposition
{
    public string ReferenceImageLabel { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public double Scale { get; set; } = 1d;
    public List<ReferenceImagePrimitivePart> Parts { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class ReferenceImagePrimitivePart
{
    public string PartName { get; set; } = string.Empty;
    public string ParentPartName { get; set; } = string.Empty;
    public ReferenceImagePartRole Role { get; set; }
    public ReferenceImageObjectEdgeCharacter EdgeCharacter { get; set; }
    public ReferenceImagePrimitiveVocabularyKind PreferredPrimitive { get; set; }
    public ReferenceImagePrimitiveVocabularyKind FallbackPrimitive { get; set; }
    public ReferenceImageToolFamily RequiredToolFamily { get; set; }
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double SizeX { get; set; } = 1d;
    public double SizeY { get; set; } = 1d;
    public double SizeZ { get; set; } = 1d;
    public double Radius { get; set; }
    public bool HasLocalFrame { get; set; }
    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; } = 1d;
    public double XAxisX { get; set; } = 1d;
    public double XAxisY { get; set; }
    public double XAxisZ { get; set; }
    public bool HasTaper { get; set; }
    public double StartWidth { get; set; }
    public double StartDepth { get; set; }
    public double EndWidth { get; set; }
    public double EndDepth { get; set; }
    public string MaterialKey { get; set; } = string.Empty;
    public List<ReferenceImageBriefAnchor> Anchors { get; set; } = new();
    public List<ReferenceImageBriefProfilePoint> ProfilePoints { get; set; } = new();
    public List<ReferenceImagePrimitiveCandidate> Candidates { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public sealed class ReferenceImagePrimitiveCandidate
{
    public ReferenceImagePrimitiveVocabularyKind Kind { get; set; }
    public double Score { get; set; }
    public string Reason { get; set; } = string.Empty;
}

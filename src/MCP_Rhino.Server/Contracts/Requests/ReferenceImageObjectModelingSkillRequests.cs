using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class BuildReferenceImageModelBriefRequest
{
    public string ReferenceImagePath { get; set; } = string.Empty;
    public string ReferenceImageLabel { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public double ObjectTypeConfidence { get; set; } = 0.5d;
    public int VisibleObjectCount { get; set; } = 1;
    public string PrimaryTargetObject { get; set; } = string.Empty;
    public string ViewpointAssumption { get; set; } = string.Empty;
    public double WidthRatio { get; set; } = 1d;
    public double DepthRatio { get; set; } = 1d;
    public double HeightRatio { get; set; } = 1d;
    public ReferenceImageObjectEdgeCharacter OverallEdgeCharacter { get; set; } = ReferenceImageObjectEdgeCharacter.Mixed;
    public List<string> SymmetryAssumptions { get; set; } = new();
    public List<ReferenceImageBriefPartRequest> Parts { get; set; } = new();
    public List<ReferenceImageVisibleDetailCueRequest> DetailCues { get; set; } = new();
    public List<ReferenceImageMaterialCueRequest> MaterialCues { get; set; } = new();
    public List<string> Unknowns { get; set; } = new();
    public List<string> RiskyAssumptions { get; set; } = new();
}

public sealed class ReferenceImageBriefPartRequest
{
    public string Name { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public ReferenceImagePartRole Role { get; set; } = ReferenceImagePartRole.StructuralMass;
    public ReferenceImageObjectEdgeCharacter EdgeCharacter { get; set; } = ReferenceImageObjectEdgeCharacter.Unknown;
    public ReferenceImagePrimitiveVocabularyKind PreferredPrimitiveHint { get; set; } = ReferenceImagePrimitiveVocabularyKind.Unknown;
    public double RelativeCenterX { get; set; }
    public double RelativeCenterY { get; set; }
    public double RelativeCenterZ { get; set; }
    public double RelativeWidth { get; set; } = 1d;
    public double RelativeDepth { get; set; } = 1d;
    public double RelativeHeight { get; set; } = 1d;
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
    public List<ReferenceImageBriefAnchorRequest> Anchors { get; set; } = new();
    public List<ReferenceImageBriefProfilePointRequest> ProfilePoints { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public sealed class ReferenceImageBriefAnchorRequest
{
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class ReferenceImageBriefProfilePointRequest
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class ReferenceImageVisibleDetailCueRequest
{
    public string PartName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ReferenceImagePartRole Role { get; set; } = ReferenceImagePartRole.SurfaceDetail;
    public ReferenceImagePrimitiveVocabularyKind PreferredRepresentation { get; set; } = ReferenceImagePrimitiveVocabularyKind.Unknown;
}

public sealed class ReferenceImageMaterialCueRequest
{
    public string Key { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ObjectColorRequest? BaseColor { get; set; }
    public ReferenceImageMaterialIntent Intent { get; set; } = ReferenceImageMaterialIntent.Unknown;
    public double Roughness { get; set; } = 0.65d;
    public double Transparency { get; set; }
    public bool TextureLikely { get; set; }
}

public sealed class DecomposeReferenceImagePrimitivesRequest
{
    public ReferenceImageModelBrief Brief { get; set; } = new();
    public double Scale { get; set; } = 1d;
}

public sealed class PlanReferenceImageProductGeometryRequest
{
    public ReferenceImageModelBrief Brief { get; set; } = new();
    public double Scale { get; set; } = 1d;
}

public sealed class CreateReferenceImageInitialMassingRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string LayerFullPath { get; set; } = string.Empty;
    public ReferenceImagePrimitiveDecomposition Decomposition { get; set; } = new();
    public bool CreateLayer { get; set; } = true;
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class PlanReferenceImageDetailRefinementRequest
{
    public ReferenceImageModelBrief Brief { get; set; } = new();
    public ReferenceImagePrimitiveDecomposition Decomposition { get; set; } = new();
    public ReferenceImageVisualQaCaptureResponse? VisualQa { get; set; }
    public List<string> QaFindings { get; set; } = new();
}

public sealed class PlanReferenceImageMaterialsRequest
{
    public ReferenceImageModelBrief Brief { get; set; } = new();
    public ReferenceImagePrimitiveDecomposition Decomposition { get; set; } = new();
    public Dictionary<string, List<Guid>> ObjectIdsByPartName { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DecideReferenceImageIterationRequest
{
    public ReferenceImageModelingTrace Trace { get; set; } = new();
    public ReferenceImageVisualQaCaptureResponse? VisualQa { get; set; }
    public List<string> QaFindings { get; set; } = new();
    public int MaxIterations { get; set; } = 3;
}

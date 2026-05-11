using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ReferenceImageModelBriefResponse
{
    public ReferenceImageModelBrief Brief { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ReferenceImagePrimitiveDecompositionResponse
{
    public ReferenceImagePrimitiveDecomposition Decomposition { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ReferenceImageProductGeometryPlanResponse
{
    public ReferenceImageProductGeometryPlan Plan { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ReferenceImageInitialMassingResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string LayerFullPath { get; set; } = string.Empty;
    public int RequestedPartCount { get; set; }
    public int CreatedObjectCount { get; set; }
    public IReadOnlyList<ReferenceImageCreatedPartObjectResponse> CreatedObjects { get; set; } = Array.Empty<ReferenceImageCreatedPartObjectResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ReferenceImageCreatedPartObjectResponse
{
    public string PartName { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public string PrimitiveKind { get; set; } = string.Empty;
    public string GeometryTypeName { get; set; } = string.Empty;
}

public sealed class ReferenceImageDetailRefinementPlanResponse
{
    public ReferenceImageRefinementPlan Plan { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ReferenceImageMaterialPlanningResponse
{
    public ReferenceImageMaterialPlan Plan { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ReferenceImageIterationDecisionResponse
{
    public ReferenceImageIterationDecision Decision { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

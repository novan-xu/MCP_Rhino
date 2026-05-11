using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ReferenceImageObjectModelingAgentResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string TargetLayerFullPath { get; set; } = string.Empty;
    public ReferenceImageObjectModelingStatus Status { get; set; }
    public string StatusReason { get; set; } = string.Empty;
    public ReferenceImageObjectModelingPlan Plan { get; set; } = new();
    public ReferenceImageInitialMassingResponse? InitialMassing { get; set; }
    public IReadOnlyList<ReferenceImageCreatedPartObjectResponse> DetailObjects { get; set; } = Array.Empty<ReferenceImageCreatedPartObjectResponse>();
    public RenderMaterialCreationResponse? MaterialCreation { get; set; }
    public ObjectMaterialAssignmentResponse? MaterialAssignment { get; set; }
    public IReadOnlyList<ReferenceImageVisualQaCheckpointResponse> QaCheckpoints { get; set; } = Array.Empty<ReferenceImageVisualQaCheckpointResponse>();
    public ReferenceImageIterationDecision? IterationDecision { get; set; }
    public IReadOnlyList<ReferenceImageCapabilityGapResponse> CapabilityGaps { get; set; } = Array.Empty<ReferenceImageCapabilityGapResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ReferenceImageVisualQaCheckpointResponse
{
    public string Name { get; set; } = string.Empty;
    public ReferenceImageVisualQaCheckpointKind CheckpointKind { get; set; }
    public ReferenceImageVisualQaCaptureResponse? Capture { get; set; }
}

public sealed class ReferenceImageCapabilityGapResponse
{
    public ReferenceImageCapabilityGapLayer Layer { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

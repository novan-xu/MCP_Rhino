using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class SurfacePointOrderRebuildSkill
{
    private readonly ISurfaceRebuildOrchestrator _orchestrator;

    public SurfacePointOrderRebuildSkill(ISurfaceRebuildOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public OperationResponse<SurfaceRebuildDescriptorResponse> Inspect(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        Guid? referenceCurveObjectId = null,
        int? referenceEdgeIndex = null)
    {
        return _orchestrator.Inspect(new InspectSurfaceRebuildDescriptorRequest
        {
            FilePath = filePath,
            ConfirmedObjectIds = objectIds.ToList(),
            ReferenceCurveObjectId = referenceCurveObjectId,
            ReferenceEdgeIndex = referenceEdgeIndex
        });
    }

    public OperationResponse<SurfacePointOrderPreviewResponse> Preview(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        SurfaceRebuildSpec spec)
    {
        return _orchestrator.Preview(new PreviewRedefineSurfacePointOrderRequest
        {
            FilePath = filePath,
            ConfirmedObjectIds = objectIds.ToList(),
            Spec = spec
        });
    }

    public OperationResponse<SurfacePointOrderApplyResponse> Apply(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        SurfaceRebuildSpec spec)
    {
        return _orchestrator.Apply(new ApplyRedefineSurfacePointOrderRequest
        {
            FilePath = filePath,
            ConfirmedObjectIds = objectIds.ToList(),
            Spec = spec
        });
    }
}

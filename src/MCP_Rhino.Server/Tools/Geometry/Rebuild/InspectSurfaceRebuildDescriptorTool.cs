using System.ComponentModel;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Rebuild;

[McpServerToolType]
public sealed class InspectSurfaceRebuildDescriptorTool
{
    private readonly ISurfaceRebuildOrchestrator _orchestrator;

    public InspectSurfaceRebuildDescriptorTool(ISurfaceRebuildOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Inspect live Rhino surface boundary data for quad-only point-order rebuild. Returns candidate reference edges, selected reference edge, boundary loop, topology, and route without modifying the document.")]
    public OperationResponse<SurfaceRebuildDescriptorResponse> InspectSurfaceRebuildDescriptor(
        string filePath,
        IReadOnlyList<Guid> confirmedObjectIds,
        Guid? referenceCurveObjectId = null,
        int? referenceEdgeIndex = null)
    {
        return _orchestrator.Inspect(new InspectSurfaceRebuildDescriptorRequest
        {
            FilePath = filePath,
            ConfirmedObjectIds = confirmedObjectIds.ToList(),
            ReferenceCurveObjectId = referenceCurveObjectId,
            ReferenceEdgeIndex = referenceEdgeIndex
        });
    }
}

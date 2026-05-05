using System.ComponentModel;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Rebuild;

[McpServerToolType]
public sealed class ApplyFlipSurfaceFrontBackTool
{
    private readonly ISurfaceFrontBackFlipOrchestrator _orchestrator;

    public ApplyFlipSurfaceFrontBackTool(ISurfaceFrontBackFlipOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [McpServerTool]
    [Description("Apply Rhino Flip-style front/back face orientation flip to surfaces or Breps in the active Rhino document. Use this after rebuilding surfaces and before Dir-style UV tweaks when front/back display must be forced to flip.")]
    public OperationResponse<SurfaceFrontBackFlipApplyResponse> ApplyFlipSurfaceFrontBack(
        string filePath,
        IReadOnlyList<Guid> confirmedObjectIds)
    {
        return _orchestrator.Apply(new ApplySurfaceFrontBackFlipRequest
        {
            FilePath = filePath,
            ConfirmedObjectIds = confirmedObjectIds.ToList()
        });
    }
}

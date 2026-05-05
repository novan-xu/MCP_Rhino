using System.ComponentModel;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Rebuild;

[McpServerToolType]
public sealed class ApplyTweakSurfaceDirectionsTool
{
    private readonly ISurfaceDirectionTweakOrchestrator _orchestrator;

    public ApplyTweakSurfaceDirectionsTool(ISurfaceDirectionTweakOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [McpServerTool]
    [Description("Apply Rhino Dir-style surface direction tweaks in the active Rhino document. If operations is omitted or empty, defaults to the standard post-rebuild Dir fix: SwapUV. Supports ReverseU, ReverseV, SwapUV, and explicit FlipNormal for surfaces and single-face Breps, using one Undo record and preserving object metadata.")]
    public OperationResponse<SurfaceDirectionTweakApplyResponse> ApplyTweakSurfaceDirections(
        string filePath,
        IReadOnlyList<Guid> confirmedObjectIds,
        IReadOnlyList<SurfaceDirectionTweakKind>? operations = null)
    {
        return _orchestrator.Apply(new ApplySurfaceDirectionTweakRequest
        {
            FilePath = filePath,
            ConfirmedObjectIds = confirmedObjectIds.ToList(),
            Operations = operations?.ToList() ?? new List<SurfaceDirectionTweakKind>()
        });
    }
}

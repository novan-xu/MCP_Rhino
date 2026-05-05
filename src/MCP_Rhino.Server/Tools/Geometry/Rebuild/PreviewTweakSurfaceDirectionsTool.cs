using System.ComponentModel;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Rebuild;

[McpServerToolType]
public sealed class PreviewTweakSurfaceDirectionsTool
{
    private readonly ISurfaceDirectionTweakOrchestrator _orchestrator;

    public PreviewTweakSurfaceDirectionsTool(ISurfaceDirectionTweakOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [McpServerTool]
    [Description("Preview Rhino Dir-style surface direction tweaks in the active Rhino document without changing geometry. If operations is omitted or empty, defaults to the standard post-rebuild Dir fix: SwapUV. Supports ReverseU, ReverseV, SwapUV, and explicit FlipNormal for surfaces and single-face Breps.")]
    public OperationResponse<SurfaceDirectionTweakPreviewResponse> PreviewTweakSurfaceDirections(
        string filePath,
        IReadOnlyList<Guid> confirmedObjectIds,
        IReadOnlyList<SurfaceDirectionTweakKind>? operations = null)
    {
        return _orchestrator.Preview(new PreviewSurfaceDirectionTweakRequest
        {
            FilePath = filePath,
            ConfirmedObjectIds = confirmedObjectIds.ToList(),
            Operations = operations?.ToList() ?? new List<SurfaceDirectionTweakKind>()
        });
    }
}

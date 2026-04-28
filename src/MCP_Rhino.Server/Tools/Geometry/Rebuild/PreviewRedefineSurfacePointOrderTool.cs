using System.ComponentModel;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Rebuild;

[McpServerToolType]
public sealed class PreviewRedefineSurfacePointOrderTool
{
    private readonly ISurfaceRebuildOrchestrator _orchestrator;

    public PreviewRedefineSurfacePointOrderTool(ISurfaceRebuildOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [McpServerTool]
    [Description("Preview a boundary-driven surface point-order rebuild against the active Rhino document. Only quad outer boundaries are executable; non-quad targets are skipped without modifying the document.")]
    public OperationResponse<SurfacePointOrderPreviewResponse> PreviewRedefineSurfacePointOrder(
        string filePath,
        IReadOnlyList<Guid> confirmedObjectIds,
        SurfaceRebuildSpec spec)
    {
        return _orchestrator.Preview(new PreviewRedefineSurfacePointOrderRequest
        {
            FilePath = filePath,
            ConfirmedObjectIds = confirmedObjectIds.ToList(),
            Spec = spec
        });
    }
}

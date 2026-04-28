using System.ComponentModel;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Rebuild;

[McpServerToolType]
public sealed class ApplyRedefineSurfacePointOrderTool
{
    private readonly ISurfaceRebuildOrchestrator _orchestrator;

    public ApplyRedefineSurfacePointOrderTool(ISurfaceRebuildOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [McpServerTool]
    [Description("Apply a boundary-driven surface point-order rebuild in the active Rhino document. Only quad outer boundaries are executable; successful targets share one Undo record and keep whitelisted object metadata.")]
    public OperationResponse<SurfacePointOrderApplyResponse> ApplyRedefineSurfacePointOrder(
        string filePath,
        IReadOnlyList<Guid> confirmedObjectIds,
        SurfaceRebuildSpec spec,
        bool replaceOriginal = true)
    {
        return _orchestrator.Apply(new ApplyRedefineSurfacePointOrderRequest
        {
            FilePath = filePath,
            ConfirmedObjectIds = confirmedObjectIds.ToList(),
            Spec = spec,
            ReplaceOriginal = replaceOriginal
        });
    }
}

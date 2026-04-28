using System.ComponentModel;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Edit;

[McpServerToolType]
public sealed class ApplyEditSurfaceGeometryTool
{
    private readonly ISurfaceEditOrchestrator _orchestrator;

    public ApplyEditSurfaceGeometryTool(ISurfaceEditOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [McpServerTool]
    [Description("Apply a surface geometry edit in the active Rhino document. Uses one Undo record and replays whitelisted object metadata after reconstruction.")]
    public OperationResponse<GeometryEditApplyResponse> ApplyEditSurfaceGeometry(
        string filePath,
        Guid objectId,
        SurfaceEditSpec editSpec,
        GeometryEditStrategyKind? expectedStrategy = null)
    {
        return _orchestrator.Apply(new ApplyEditSurfaceGeometryRequest
        {
            FilePath = filePath,
            ObjectId = objectId,
            EditSpec = editSpec,
            ExpectedStrategy = expectedStrategy
        });
    }
}

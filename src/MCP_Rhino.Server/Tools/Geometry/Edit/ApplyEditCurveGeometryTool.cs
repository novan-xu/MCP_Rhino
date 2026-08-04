using System.ComponentModel;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Edit;

[McpServerToolType]
public sealed class ApplyEditCurveGeometryTool
{
    private readonly ICurveEditOrchestrator _orchestrator;

    public ApplyEditCurveGeometryTool(ICurveEditOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Apply a DirectOverride curve geometry edit in the active Rhino document. Uses one Undo record and replays whitelisted object metadata after replacement.")]
    public OperationResponse<GeometryEditApplyResponse> ApplyEditCurveGeometry(
        string filePath,
        Guid objectId,
        CurveEditSpec editSpec,
        GeometryEditStrategyKind? expectedStrategy = null)
    {
        return _orchestrator.Apply(new ApplyEditCurveGeometryRequest
        {
            FilePath = filePath,
            ObjectId = objectId,
            EditSpec = editSpec,
            ExpectedStrategy = expectedStrategy
        });
    }
}

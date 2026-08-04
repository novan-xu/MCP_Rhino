using System.ComponentModel;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Edit;

[McpServerToolType]
public sealed class PreviewEditCurveGeometryTool
{
    private readonly ICurveEditOrchestrator _orchestrator;

    public PreviewEditCurveGeometryTool(ICurveEditOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview a DirectOverride curve geometry edit against the active Rhino document. Reconstructs a curve from a full replacement point list without modifying the document.")]
    public OperationResponse<GeometryEditPreviewResponse> PreviewEditCurveGeometry(
        string filePath,
        Guid objectId,
        CurveEditSpec editSpec,
        GeometryEditStrategyKind? expectedStrategy = null)
    {
        return _orchestrator.Preview(new PreviewEditCurveGeometryRequest
        {
            FilePath = filePath,
            ObjectId = objectId,
            EditSpec = editSpec,
            ExpectedStrategy = expectedStrategy
        });
    }
}

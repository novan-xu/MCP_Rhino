using System.ComponentModel;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Edit;

[McpServerToolType]
public sealed class PreviewEditSurfaceGeometryTool
{
    private readonly ISurfaceEditOrchestrator _orchestrator;

    public PreviewEditSurfaceGeometryTool(ISurfaceEditOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [McpServerTool]
    [Description("Preview a surface geometry edit against the active Rhino document. Supports direct control-point-grid replacement and derived point operations without modifying the document.")]
    public OperationResponse<GeometryEditPreviewResponse> PreviewEditSurfaceGeometry(
        string filePath,
        Guid objectId,
        SurfaceEditSpec editSpec,
        GeometryEditStrategyKind? expectedStrategy = null)
    {
        return _orchestrator.Preview(new PreviewEditSurfaceGeometryRequest
        {
            FilePath = filePath,
            ObjectId = objectId,
            EditSpec = editSpec,
            ExpectedStrategy = expectedStrategy
        });
    }
}

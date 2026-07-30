extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveDrawingViewManager
{
    OperationResponse<DrawingLayerScopeResult> ResolveLayerScope(
        RhinoDoc document,
        IReadOnlyList<string> layerQueries,
        IReadOnlyList<string> confirmedLayerFullPaths);

    OperationResponse<DrawingViewSetupResult> SetupViews(
        RhinoDoc document,
        DrawingViewPreset preset,
        IReadOnlyList<int> layerIndices,
        IReadOnlyList<string> resolvedLayerFullPaths,
        double fitMarginPercent,
        IReadOnlyList<Guid>? targetObjectIds = null);
}

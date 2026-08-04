using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Inspection;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetContourCurvesByFilterTool
{
    private readonly SelectionScopedAnalysisSkill _analysisSkill;

    public GetContourCurvesByFilterTool(SelectionScopedAnalysisSkill analysisSkill)
    {
        _analysisSkill = analysisSkill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview live contour curves for objects resolved server-side from layer, type, and/or user attribute filters. Applies one common contour recipe to all matched objects to avoid large per-object entry payloads.")]
    public OperationResponse<GetContourCurvesInLiveResponse> GetContourCurvesByFilter(
        string filePath,
        double startX,
        double startY,
        double startZ,
        double endX,
        double endY,
        double endZ,
        double interval = 1d,
        string? entryIdPrefix = null,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _analysisSkill.GetContourCurvesByFilter(new GetContourCurvesByFilterRequest
        {
            FilePath = filePath,
            EntryIdPrefix = entryIdPrefix ?? "contour",
            StartX = startX,
            StartY = startY,
            StartZ = startZ,
            EndX = endX,
            EndY = endY,
            EndZ = endZ,
            Interval = interval,
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectTypes = objectTypes ?? new List<string>(),
            UserAttributeConditions = userAttributeConditions ?? new List<UserAttributeConditionRequest>(),
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }
}

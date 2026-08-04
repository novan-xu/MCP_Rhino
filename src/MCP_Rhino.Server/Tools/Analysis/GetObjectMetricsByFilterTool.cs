using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Inspection;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetObjectMetricsByFilterTool
{
    private readonly SelectionScopedAnalysisSkill _analysisSkill;

    public GetObjectMetricsByFilterTool(SelectionScopedAnalysisSkill analysisSkill)
    {
        _analysisSkill = analysisSkill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read live Rhino object metrics for objects resolved server-side from layer, type, and/or user attribute filters. Use this to avoid sending large ObjectId payloads; returns the same metric records as GetObjectMetricsInLive.")]
    public OperationResponse<GetObjectMetricsInLiveResponse> GetObjectMetricsByFilter(
        string filePath,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _analysisSkill.GetObjectMetricsByFilter(new GetObjectMetricsByFilterRequest
        {
            FilePath = filePath,
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectTypes = objectTypes ?? new List<string>(),
            UserAttributeConditions = userAttributeConditions ?? new List<UserAttributeConditionRequest>(),
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }
}

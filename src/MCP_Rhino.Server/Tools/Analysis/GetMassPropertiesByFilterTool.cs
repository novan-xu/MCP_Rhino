using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Inspection;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetMassPropertiesByFilterTool
{
    private readonly SelectionScopedAnalysisSkill _analysisSkill;

    public GetMassPropertiesByFilterTool(SelectionScopedAnalysisSkill analysisSkill)
    {
        _analysisSkill = analysisSkill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read live Rhino mass properties for objects resolved server-side from layer, type, and/or user attribute filters. Use this to avoid sending large ObjectId payloads; supports Length, Area, Volume, and Auto.")]
    public OperationResponse<GetMassPropertiesInLiveResponse> GetMassPropertiesByFilter(
        string filePath,
        GeometryMassKind kind = GeometryMassKind.Auto,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _analysisSkill.GetMassPropertiesByFilter(new GetMassPropertiesByFilterRequest
        {
            FilePath = filePath,
            Kind = kind,
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectTypes = objectTypes ?? new List<string>(),
            UserAttributeConditions = userAttributeConditions ?? new List<UserAttributeConditionRequest>(),
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }
}

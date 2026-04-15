using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Inspection;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class FilterObjectsTool
{
    private readonly CompositeObjectFilterSkill _compositeObjectFilterSkill;

    public FilterObjectsTool(CompositeObjectFilterSkill compositeObjectFilterSkill)
    {
        _compositeObjectFilterSkill = compositeObjectFilterSkill;
    }

    [McpServerTool]
    [Description("按单一条件或任意组合条件筛查 Rhino 物体。当前支持 layer、object type、user attributes，并支持 AND/OR 组合。")]
    public string FilterObjects(
        string filePath,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _compositeObjectFilterSkill.Filter(new FilterObjectsRequest
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
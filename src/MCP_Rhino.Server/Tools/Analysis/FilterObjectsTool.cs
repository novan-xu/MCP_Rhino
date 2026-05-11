using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Skills.Editing;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class FilterObjectsTool
{
    private readonly LiveObjectSelectionSkill _objectSelectionSkill;

    public FilterObjectsTool(LiveObjectSelectionSkill objectSelectionSkill)
    {
        _objectSelectionSkill = objectSelectionSkill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Filter objects in the current live Rhino document by confirmed layer path, unambiguous layer query, object type, and/or user attributes. Returns structured object metadata; ambiguous layer queries fail with candidate layers so the caller can retry with confirmedLayerFullPaths.")]
    public OperationResponse<RhinoObjectFilterResult> FilterObjects(
        string filePath,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _objectSelectionSkill.Select(new FilterObjectsRequest
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

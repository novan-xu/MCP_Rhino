using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class DeleteObjectsTool
{
    private readonly GeometryModificationSkill _skill;

    public DeleteObjectsTool(GeometryModificationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool]
    [Description("删除 Rhino 对象，并覆盖写回原文件。")]
    public OperationResponse<GeometryModificationResponse> DeleteObjects(
        string filePath,
        List<Guid>? confirmedObjectIds = null,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _skill.Apply(new DeleteObjectsRequest
        {
            FilePath = filePath,
            ConfirmedObjectIds = confirmedObjectIds ?? new List<Guid>(),
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectTypes = objectTypes ?? new List<string>(),
            UserAttributeConditions = userAttributeConditions ?? new List<UserAttributeConditionRequest>(),
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }
}

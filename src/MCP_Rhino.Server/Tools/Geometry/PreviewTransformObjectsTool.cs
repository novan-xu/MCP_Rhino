using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class PreviewTransformObjectsTool
{
    private readonly GeometryModificationSkill _skill;

    public PreviewTransformObjectsTool(GeometryModificationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview Rhino object transforms in the current live document without mutating the document.")]
    public OperationResponse<GeometryModificationPreviewResponse> PreviewTransformObjects(
        string filePath,
        GeometryTransformSpec transform,
        List<Guid>? confirmedObjectIds = null,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _skill.Preview(new PreviewTransformObjectsRequest
        {
            FilePath = filePath,
            Transform = transform ?? new GeometryTransformSpec(),
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

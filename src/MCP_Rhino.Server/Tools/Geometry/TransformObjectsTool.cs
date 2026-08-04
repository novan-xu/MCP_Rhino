using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class TransformObjectsTool
{
    private readonly GeometryModificationSkill _skill;

    public TransformObjectsTool(GeometryModificationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Apply Translate, Rotate, or UniformScale transforms to objects in the current live Rhino document. Uses one Rhino undo record and does not read or write external files.")]
    public OperationResponse<GeometryModificationResponse> TransformObjects(
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
        return _skill.Apply(new TransformObjectsRequest
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

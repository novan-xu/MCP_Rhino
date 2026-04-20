using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class ReplaceGeometryTool
{
    private readonly GeometryModificationSkill _skill;

    public ReplaceGeometryTool(GeometryModificationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool]
    [Description("按 ObjectId 替换 Rhino 对象的几何，但保留对象属性与 ObjectId。")]
    public OperationResponse<GeometryModificationResponse> ReplaceGeometry(
        string filePath,
        List<GeometryReplacementEntryRequest> entries)
    {
        return _skill.Apply(new ReplaceGeometryRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<GeometryReplacementEntryRequest>()
        });
    }
}

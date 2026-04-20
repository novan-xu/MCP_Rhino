using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class EditControlPointsTool
{
    private readonly GeometryModificationSkill _skill;

    public EditControlPointsTool(GeometryModificationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool]
    [Description("按 ObjectId 编辑 NurbsCurve / NurbsSurface 的控制点，并覆盖写回原文件。")]
    public OperationResponse<GeometryModificationResponse> EditControlPoints(
        string filePath,
        List<ControlPointEditEntryRequest> entries)
    {
        return _skill.Apply(new EditControlPointsRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<ControlPointEditEntryRequest>()
        });
    }
}

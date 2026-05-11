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

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Edit NurbsCurve or NurbsSurface control points in the current live Rhino document by ObjectId. Uses one Rhino undo record and does not read or write external files.")]
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

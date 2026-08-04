using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class PreviewEditControlPointsTool
{
    private readonly GeometryModificationSkill _skill;

    public PreviewEditControlPointsTool(GeometryModificationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview NurbsCurve or NurbsSurface control point edits in the current live Rhino document without mutating the document.")]
    public OperationResponse<GeometryModificationPreviewResponse> PreviewEditControlPoints(
        string filePath,
        List<ControlPointEditEntryRequest> entries)
    {
        return _skill.Preview(new PreviewEditControlPointsRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<ControlPointEditEntryRequest>()
        });
    }
}

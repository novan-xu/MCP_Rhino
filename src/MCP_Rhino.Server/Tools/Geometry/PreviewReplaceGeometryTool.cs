using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class PreviewReplaceGeometryTool
{
    private readonly GeometryModificationSkill _skill;

    public PreviewReplaceGeometryTool(GeometryModificationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("预览按 ObjectId 替换几何的结果，不落盘。")]
    public OperationResponse<GeometryModificationPreviewResponse> PreviewReplaceGeometry(
        string filePath,
        List<GeometryReplacementEntryRequest> entries)
    {
        return _skill.Preview(new PreviewReplaceGeometryRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<GeometryReplacementEntryRequest>()
        });
    }
}

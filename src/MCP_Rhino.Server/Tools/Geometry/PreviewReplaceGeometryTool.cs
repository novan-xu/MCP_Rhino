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
    [Description("Preview geometry replacement by ObjectId in the current live Rhino document without mutating the document.")]
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

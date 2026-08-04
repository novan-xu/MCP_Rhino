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

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Replace Rhino object geometry by ObjectId in the current live document while preserving object attributes and ObjectId. Uses one Rhino undo record.")]
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

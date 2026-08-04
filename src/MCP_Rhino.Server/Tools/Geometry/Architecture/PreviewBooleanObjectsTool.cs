using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Architecture;

[McpServerToolType]
public sealed class PreviewBooleanObjectsTool
{
    private readonly ArchitecturalBooleanSkill _skill;

    public PreviewBooleanObjectsTool(ArchitecturalBooleanSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview Brep boolean operations without mutating the live Rhino document.")]
    public OperationResponse<ArchitecturalBooleanPreviewResponse> PreviewBooleanObjects(
        string filePath,
        List<BooleanOperationEntryRequest> entries,
        double tolerance = 0d)
    {
        return _skill.Preview(new PreviewBooleanObjectsRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<BooleanOperationEntryRequest>(),
            Tolerance = tolerance
        });
    }
}


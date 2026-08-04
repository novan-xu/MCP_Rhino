using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Architecture;

[McpServerToolType]
public sealed class PreviewOpeningsTool
{
    private readonly ArchitecturalBooleanSkill _skill;

    public PreviewOpeningsTool(ArchitecturalBooleanSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview rectangular or circular opening cuts without mutating the live Rhino document.")]
    public OperationResponse<ArchitecturalBooleanPreviewResponse> PreviewOpenings(
        string filePath,
        List<OpeningItemRequest> items,
        double tolerance = 0d)
    {
        return _skill.PreviewOpenings(new PreviewOpeningsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<OpeningItemRequest>(),
            Tolerance = tolerance
        });
    }
}


using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Architecture;

[McpServerToolType]
public sealed class ApplyBooleanObjectsTool
{
    private readonly ArchitecturalBooleanSkill _skill;

    public ApplyBooleanObjectsTool(ArchitecturalBooleanSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Apply Brep boolean operations by replacing/deleting live Rhino source geometry.")]
    public OperationResponse<ArchitecturalBooleanApplyResponse> ApplyBooleanObjects(
        string filePath,
        List<BooleanOperationEntryRequest> entries,
        double tolerance = 0d)
    {
        return _skill.Apply(new ApplyBooleanObjectsRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<BooleanOperationEntryRequest>(),
            Tolerance = tolerance
        });
    }
}


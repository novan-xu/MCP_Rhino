using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Architecture;

[McpServerToolType]
public sealed class ApplyOpeningsTool
{
    private readonly ArchitecturalBooleanSkill _skill;

    public ApplyOpeningsTool(ArchitecturalBooleanSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Apply rectangular or circular opening cuts to live Rhino target geometry.")]
    public OperationResponse<ArchitecturalBooleanApplyResponse> ApplyOpenings(
        string filePath,
        List<OpeningItemRequest> items,
        GeometryCreationCommonOptions? common = null,
        ArchitecturalMetadataRequest? metadata = null,
        bool autoCreateLayers = false,
        double tolerance = 0d)
    {
        return _skill.ApplyOpenings(new ApplyOpeningsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<OpeningItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions(),
            Metadata = metadata ?? new ArchitecturalMetadataRequest(),
            AutoCreateLayers = autoCreateLayers,
            Tolerance = tolerance
        });
    }
}


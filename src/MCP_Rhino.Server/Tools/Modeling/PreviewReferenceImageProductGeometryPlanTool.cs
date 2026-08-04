using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Modeling;

[McpServerToolType]
public sealed class PreviewReferenceImageProductGeometryPlanTool
{
    private readonly ReferenceImageModelBriefSkill _briefSkill;
    private readonly ReferenceImageProductGeometryPlanningSkill _skill;

    public PreviewReferenceImageProductGeometryPlanTool(
        ReferenceImageModelBriefSkill briefSkill,
        ReferenceImageProductGeometryPlanningSkill skill)
    {
        _briefSkill = briefSkill;
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview the structured reference-image product geometry strategy without mutating Rhino. Use before modeling furniture or product references to classify physical parts, tapered/profile/loft/pipe/SubD strategies, and material, texture, lighting, shadow, label, or room-context cues that must not become geometry.")]
    public OperationResponse<ReferenceImageProductGeometryPlanResponse> PreviewReferenceImageProductGeometryPlan(
        BuildReferenceImageModelBriefRequest briefRequest,
        double scale = 1d)
    {
        OperationResponse<ReferenceImageModelBriefResponse> brief = _briefSkill.Build(
            briefRequest ?? new BuildReferenceImageModelBriefRequest());
        if (!brief.Success || brief.Data is null)
        {
            return OperationResponse<ReferenceImageProductGeometryPlanResponse>.Fail(brief.Message);
        }

        return _skill.Plan(new PlanReferenceImageProductGeometryRequest
        {
            Brief = brief.Data.Brief,
            Scale = scale
        });
    }
}

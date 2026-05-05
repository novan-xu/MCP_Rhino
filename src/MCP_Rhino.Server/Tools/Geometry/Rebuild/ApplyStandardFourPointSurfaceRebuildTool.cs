using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Rebuild;

[McpServerToolType]
public sealed class ApplyStandardFourPointSurfaceRebuildTool
{
    private readonly StandardFourPointSurfaceRebuildSkill _skill;

    public ApplyStandardFourPointSurfaceRebuildTool(StandardFourPointSurfaceRebuildSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool]
    [Description("Default standard tool for rebuilding four-point Rhino surfaces/panels. Use this whenever the user asks to rebuild 4 point surfaces unless they explicitly request a lower-level operation. It rebuilds with surface-local gravity coordinates, lower-left as point 1, clockwise point order, then applies Rhino Flip to force front/back face orientation and the standard Dir fix: SwapUV.")]
    public OperationResponse<StandardFourPointSurfaceRebuildResponse> ApplyStandardFourPointSurfaceRebuild(
        string filePath,
        IReadOnlyList<Guid> confirmedObjectIds)
    {
        return _skill.Apply(filePath, confirmedObjectIds);
    }
}

using System.ComponentModel;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class ApplyStoneWireframeBakeTool
{
    private readonly StoneWireframeBakeSkill _skill;

    public ApplyStoneWireframeBakeTool(StoneWireframeBakeSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create or reuse WF_ blocks for nested stones in STN-MET-GA- assemblies, excluding _EL, _V, and legacy GA names. Prefer this fixed bake workflow over atomic creation. Curve (default) copies each upper horizontal edge; WorldZSurface extends that exact placed edge in world +Z by surfaceHeightInches (default 5), converted to model units, independent of parent rotation or scale. Retains left-end base points, shared WF names, and exact parent Assembly_ID. Preserves sources, rejects conflicting output definitions, and skips equivalent existing instances. One live Rhino undo record. Does not convert existing incompatible WF definitions.")]
    public OperationResponse<StoneWireframeBakeResponse> ApplyStoneWireframeBake(
        string filePath,
        List<string>? assemblyDefinitionNames = null,
        StoneBakeOutputMode outputMode = StoneBakeOutputMode.Curve,
        double surfaceHeightInches = 5)
    {
        return _skill.Apply(new StoneWireframeBakeRequest
        {
            FilePath = filePath,
            AssemblyDefinitionNames = assemblyDefinitionNames ?? new List<string>(),
            OutputMode = outputMode,
            SurfaceHeightInches = surfaceHeightInches
        });
    }
}

using System.ComponentModel;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class PreviewStoneWireframeBakeTool
{
    private readonly StoneWireframeBakeSkill _skill;

    public PreviewStoneWireframeBakeTool(StoneWireframeBakeSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview WF_ blocks for nested stones in STN-MET-GA- assemblies, excluding _EL, _V, and legacy GA names. Prefer this fixed bake workflow over atomic creation. Curve (default) copies each upper horizontal edge; WorldZSurface extends that exact placed edge in world +Z by surfaceHeightInches (default 5), converted to model units, independent of parent rotation or scale. Retains left-end base points, shared WF names, and exact parent Assembly_ID. Preserves sources, rejects conflicting output definitions, and skips equivalent existing instances. Read-only preview; no mutation.")]
    public OperationResponse<StoneWireframeBakeResponse> PreviewStoneWireframeBake(
        string filePath,
        List<string>? assemblyDefinitionNames = null,
        StoneBakeOutputMode outputMode = StoneBakeOutputMode.Curve,
        double surfaceHeightInches = 5)
    {
        return _skill.Preview(new StoneWireframeBakeRequest
        {
            FilePath = filePath,
            AssemblyDefinitionNames = assemblyDefinitionNames ?? new List<string>(),
            OutputMode = outputMode,
            SurfaceHeightInches = surfaceHeightInches
        });
    }
}

using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class CreateRaisedStripsTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateRaisedStripsTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch raised strip Breps between explicit start-end points in the current live Rhino document on an existing layer. Use for seams, ribs, trim, bezels, and raised borders in reference-image object modeling; created objects receive detail-stage metadata.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateRaisedStrips(
        string filePath,
        List<RaisedStripItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateRaisedStripsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<RaisedStripItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}

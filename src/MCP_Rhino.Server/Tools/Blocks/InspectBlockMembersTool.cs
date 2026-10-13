using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class InspectBlockMembersTool
{
    private readonly RhinoBlockMemberInspectionService _service;
    public InspectBlockMembersTool(RhinoBlockMemberInspectionService service) => _service = service;

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Inspect leaves inside explicit live top-level block instance IDs, including hidden/locked instances and nested blocks, without exploding or changing the document. Returns each member's own user text, full ancestry, composed transform, and placed planar straight-edge quad corners/edge/diagonal lengths in model units. Other shapes are explicitly unsupported for size comparison; never infer matching geometry from area alone. At most 1000 roots and 50000 leaves; traversal limits fail the whole call, so retry smaller batches. Does not inspect unused definitions or read disk files.")]
    public OperationResponse<BlockMemberInspectionResponse> InspectBlockMembers(string filePath, List<Guid> objectIds, int maxLeaves = 10000)
        => _service.Inspect(new BlockMemberInspectionRequest { FilePath = filePath, ObjectIds = objectIds, MaxLeaves = maxLeaves });
}

using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.Reference;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Reference;

[McpServerToolType]
public sealed class ListRhinoReferenceModulesTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("List curated RhinoCommon reference modules available in MCP_Rhino. This is reference-only and does not access the live Rhino document.")]
    public RhinoReferenceModuleListResponse ListRhinoReferenceModules(string? query = null, int limit = 20)
    {
        return RhinoReferenceIndex.ListModules(query, limit);
    }
}

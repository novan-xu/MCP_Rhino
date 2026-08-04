using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.Reference;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Reference;

[McpServerToolType]
public sealed class SearchRhinoReferenceTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Search curated RhinoCommon reference notes by keyword. This is reference-only and returns bounded summaries without executing code or accessing Rhino documents.")]
    public RhinoReferenceSearchResponse SearchRhinoReference(string query, int limit = 10)
    {
        return RhinoReferenceIndex.Search(query, limit);
    }
}

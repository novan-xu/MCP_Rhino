using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.Reference;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Reference;

[McpServerToolType]
public sealed class GetMcpRhinoToolHelpTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Get MCP_Rhino tool help generated from MCP tool metadata, filtered by tool name and/or family. This is reference-only and does not call any Rhino tool.")]
    public McpRhinoToolHelpResponse GetMcpRhinoToolHelp(string? toolName = null, string? family = null, int limit = 20)
    {
        return McpRhinoToolHelpIndex.Search(toolName, family, limit);
    }
}

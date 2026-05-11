using System.ComponentModel;
using MCP_Rhino.Server.Infrastructure.Reference;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Resources;

[McpServerResourceType]
public sealed class McpRhinoToolHelpResource
{
    [McpServerResource(UriTemplate = "mcp-rhino-tools://families", Name = "MCP_Rhino Tool Families", MimeType = "text/markdown")]
    [Description("List MCP_Rhino tool families generated from MCP tool metadata. Reference-only; does not inspect or mutate a live Rhino document.")]
    public static string ListMcpRhinoToolFamiliesResource()
    {
        return McpRhinoToolHelpIndex.FormatFamiliesMarkdown();
    }

    [McpServerResource(UriTemplate = "mcp-rhino-tools://tool/{toolName}", Name = "MCP_Rhino Tool Help", MimeType = "text/markdown")]
    [Description("Get MCP_Rhino tool help generated from MCP tool metadata. Reference-only; does not inspect or mutate a live Rhino document.")]
    public static string GetMcpRhinoToolHelpResource(string toolName)
    {
        return McpRhinoToolHelpIndex.FormatToolMarkdown(toolName);
    }
}

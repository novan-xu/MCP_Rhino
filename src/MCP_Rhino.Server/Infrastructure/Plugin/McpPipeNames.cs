namespace MCP_Rhino.Server.Infrastructure.Plugin;

using MCP_Rhino.Transport.Routing;

internal static class McpPipeNames
{
    public const string DeveloperDebugPipeName = "mcp_rhino";

    public static string ForPanelBoundDocument(uint runtimeSerialNumber)
    {
        return $"mcp_rhino_{Environment.ProcessId}_{runtimeSerialNumber}";
    }

    public static string ForRoutedDocument(uint runtimeSerialNumber)
    {
        return RouteProtocol.ForDocument(Environment.ProcessId, runtimeSerialNumber);
    }
}

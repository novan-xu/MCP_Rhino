namespace MCP_Rhino.Server.Infrastructure.Plugin;

using MCP_Rhino.Transport.Routing;

internal static class McpPipeNames
{
    public static string ForRoutedDocument(uint runtimeSerialNumber)
    {
        return RouteProtocol.ForDocument(Environment.ProcessId, runtimeSerialNumber);
    }
}

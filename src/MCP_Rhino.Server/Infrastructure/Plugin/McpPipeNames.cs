namespace MCP_Rhino.Server.Infrastructure.Plugin;

internal static class McpPipeNames
{
    public const string DeveloperDebugPipeName = "mcp_rhino";

    public static string ForPanelBoundDocument(uint runtimeSerialNumber)
    {
        return $"mcp_rhino_{Environment.ProcessId}_{runtimeSerialNumber}";
    }
}

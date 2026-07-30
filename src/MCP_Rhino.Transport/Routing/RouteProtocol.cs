namespace MCP_Rhino.Transport.Routing;

public static class RouteProtocol
{
    public const int RegistrySchemaVersion = 1;
    public const int RouteProtocolVersion = 1;
    public const string AttestationRequestMethod = "mcp-rhino/routed-document-info";
    public const string RegistryDirectoryName = "v1";
    public const string RoutePipePrefix = "mcp_rhino_route_";

    public static string ForDocument(int processId, uint runtimeSerialNumber)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        return $"{RoutePipePrefix}{processId}_{runtimeSerialNumber}";
    }

    public static bool IsRoutePipe(string? pipeName)
    {
        return !string.IsNullOrWhiteSpace(pipeName)
            && pipeName.StartsWith(RoutePipePrefix, StringComparison.Ordinal);
    }
}

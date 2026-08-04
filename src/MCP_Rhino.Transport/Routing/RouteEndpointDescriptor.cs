namespace MCP_Rhino.Transport.Routing;

public sealed record RouteEndpointDescriptor
{
    public int SchemaVersion { get; init; } = RouteProtocol.RegistrySchemaVersion;

    public int ProtocolVersion { get; init; } = RouteProtocol.RouteProtocolVersion;

    public required string DocumentSessionId { get; init; }

    public required string PluginInstanceGeneration { get; init; }

    public long DocumentLifecycleGeneration { get; init; }

    public required string EndpointNonce { get; init; }

    public int ProcessId { get; init; }

    public long ProcessStartTimeUtcTicks { get; init; }

    public uint RuntimeSerialNumber { get; init; }

    public required string PipeName { get; init; }

    public string? FilePath { get; init; }

    public required string DisplayName { get; init; }

    public bool Routable { get; init; }

    public required string PluginVersion { get; init; }

    public DateTimeOffset LastUpdatedUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? Status { get; init; }

    public bool IsStructurallyValid()
    {
        return SchemaVersion == RouteProtocol.RegistrySchemaVersion
            && ProtocolVersion == RouteProtocol.RouteProtocolVersion
            && Guid.TryParseExact(DocumentSessionId, "N", out _)
            && Guid.TryParseExact(PluginInstanceGeneration, "N", out _)
            && DocumentLifecycleGeneration > 0
            && Guid.TryParseExact(EndpointNonce, "N", out _)
            && ProcessId > 0
            && ProcessStartTimeUtcTicks > 0
            && RouteProtocol.IsRoutePipe(PipeName)
            && !string.IsNullOrWhiteSpace(DisplayName)
            && !string.IsNullOrWhiteSpace(PluginVersion)
            && (!Routable || !string.IsNullOrWhiteSpace(FilePath));
    }
}

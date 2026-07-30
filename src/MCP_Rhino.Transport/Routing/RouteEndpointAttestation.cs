namespace MCP_Rhino.Transport.Routing;

public sealed record RouteEndpointAttestation
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

    public required string FilePath { get; init; }

    public required string ServerVersion { get; init; }

    public bool Matches(RouteEndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return SchemaVersion == descriptor.SchemaVersion
            && ProtocolVersion == descriptor.ProtocolVersion
            && string.Equals(DocumentSessionId, descriptor.DocumentSessionId, StringComparison.Ordinal)
            && string.Equals(PluginInstanceGeneration, descriptor.PluginInstanceGeneration, StringComparison.Ordinal)
            && DocumentLifecycleGeneration == descriptor.DocumentLifecycleGeneration
            && string.Equals(EndpointNonce, descriptor.EndpointNonce, StringComparison.Ordinal)
            && ProcessId == descriptor.ProcessId
            && ProcessStartTimeUtcTicks == descriptor.ProcessStartTimeUtcTicks
            && RuntimeSerialNumber == descriptor.RuntimeSerialNumber
            && string.Equals(PipeName, descriptor.PipeName, StringComparison.Ordinal)
            && RoutePath.Equals(FilePath, descriptor.FilePath)
            && string.Equals(ServerVersion, descriptor.PluginVersion, StringComparison.Ordinal);
    }
}

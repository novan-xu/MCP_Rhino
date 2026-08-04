using System.Diagnostics;
using MCP_Rhino.Transport.Routing;

namespace MCP_Rhino.Router.Routing;

public sealed class RouteDiscovery
{
    private readonly RouteRegistry _registry;

    public RouteDiscovery(RouteRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public RouteDiscoverySnapshot Discover()
    {
        var documents = new List<RouteDocumentStatus>();
        foreach (RouteRegistryEntry entry in _registry.ReadAll())
        {
            if (entry.Descriptor is null)
            {
                documents.Add(new RouteDocumentStatus(null, entry.DescriptorPath, false, entry.Diagnostic ?? "INVALID_DESCRIPTOR"));
                continue;
            }

            RouteEndpointDescriptor descriptor = entry.Descriptor;
            ProcessValidation validation = ValidateProcess(descriptor);
            if (validation == ProcessValidation.Gone)
            {
                _registry.DeleteIfGenerationMatches(descriptor);
                documents.Add(new RouteDocumentStatus(descriptor, entry.DescriptorPath, false, "OWNER_PROCESS_GONE"));
                continue;
            }

            if (validation == ProcessValidation.Unknown)
            {
                documents.Add(new RouteDocumentStatus(descriptor, entry.DescriptorPath, false, "OWNER_PROCESS_UNVERIFIED"));
                continue;
            }

            documents.Add(new RouteDocumentStatus(
                descriptor,
                entry.DescriptorPath,
                descriptor.Routable,
                descriptor.Routable ? "DISCOVERED" : descriptor.Status ?? "NOT_ROUTABLE"));
        }

        return new RouteDiscoverySnapshot(
            documents
                .OrderBy(status => status.Descriptor?.DisplayName ?? status.DescriptorPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(status => status.Descriptor?.DocumentSessionId, StringComparer.Ordinal)
                .ToArray());
    }

    private static ProcessValidation ValidateProcess(RouteEndpointDescriptor descriptor)
    {
        try
        {
            using Process process = Process.GetProcessById(descriptor.ProcessId);
            long startTicks = process.StartTime.ToUniversalTime().Ticks;
            return startTicks == descriptor.ProcessStartTimeUtcTicks
                ? ProcessValidation.Live
                : ProcessValidation.Gone;
        }
        catch (ArgumentException)
        {
            return ProcessValidation.Gone;
        }
        catch (InvalidOperationException)
        {
            return ProcessValidation.Gone;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return ProcessValidation.Unknown;
        }
    }

    private enum ProcessValidation
    {
        Live,
        Gone,
        Unknown
    }
}

public sealed record RouteDiscoverySnapshot(IReadOnlyList<RouteDocumentStatus> Documents)
{
    public IReadOnlyList<RouteEndpointDescriptor> RoutableDescriptors => Documents
        .Where(status => status.Routable && status.Descriptor is not null)
        .Select(status => status.Descriptor!)
        .ToArray();
}

public sealed record RouteDocumentStatus(
    RouteEndpointDescriptor? Descriptor,
    string DescriptorPath,
    bool Routable,
    string Status);

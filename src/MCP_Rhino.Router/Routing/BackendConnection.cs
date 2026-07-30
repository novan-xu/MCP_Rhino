using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MCP_Rhino.Transport.Routing;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MCP_Rhino.Router.Routing;

public sealed class BackendConnection : IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly NamedPipeClientStream _pipe;

    private BackendConnection(
        RouteEndpointDescriptor descriptor,
        NamedPipeClientStream pipe,
        McpClient client,
        IReadOnlyList<Tool> tools,
        IReadOnlyList<Resource> resources,
        IReadOnlyList<ResourceTemplate> resourceTemplates,
        string surfaceFingerprint)
    {
        Descriptor = descriptor;
        _pipe = pipe;
        Client = client;
        Tools = tools;
        Resources = resources;
        ResourceTemplates = resourceTemplates;
        SurfaceFingerprint = surfaceFingerprint;
    }

    public RouteEndpointDescriptor Descriptor { get; }

    public McpClient Client { get; }

    public IReadOnlyList<Tool> Tools { get; }

    public IReadOnlyList<Resource> Resources { get; }

    public IReadOnlyList<ResourceTemplate> ResourceTemplates { get; }

    public string SurfaceFingerprint { get; }

    public static async Task<BackendConnection> ConnectAsync(
        RouteEndpointDescriptor descriptor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var pipe = new NamedPipeClientStream(
            ".",
            descriptor.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        McpClient? client = null;
        try
        {
            await pipe.ConnectAsync(3000, cancellationToken).ConfigureAwait(false);
            var transport = new StreamClientTransport(pipe, pipe, loggerFactory);
            client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions
                {
                    ClientInfo = new Implementation
                    {
                        Name = "MCP_Rhino.Router",
                        Version = typeof(BackendConnection).Assembly.GetName().Version?.ToString() ?? "0.0.0.0"
                    }
                },
                loggerFactory,
                cancellationToken).ConfigureAwait(false);

            RouteEndpointAttestation attestation = await client.SendRequestAsync<AttestationRequest, RouteEndpointAttestation>(
                RouteProtocol.AttestationRequestMethod,
                new AttestationRequest(),
                SerializerOptions,
                default,
                cancellationToken).ConfigureAwait(false);
            if (!attestation.Matches(descriptor))
            {
                throw new InvalidDataException("ROUTE_ATTESTATION_MISMATCH");
            }

            IReadOnlyList<Tool> tools = (await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
                .Select(tool => tool.ProtocolTool)
                .OrderBy(tool => tool.Name, StringComparer.Ordinal)
                .ToArray();
            IReadOnlyList<Resource> resources = (await client.ListResourcesAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
                .Select(resource => resource.ProtocolResource)
                .OrderBy(resource => resource.Uri, StringComparer.Ordinal)
                .ToArray();
            IReadOnlyList<ResourceTemplate> templates = (await client.ListResourceTemplatesAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
                .Select(template => template.ProtocolResourceTemplate)
                .OrderBy(template => template.UriTemplate, StringComparer.Ordinal)
                .ToArray();

            string fingerprint = ComputeFingerprint(tools, resources, templates);
            return new BackendConnection(descriptor, pipe, client, tools, resources, templates, fingerprint);
        }
        catch
        {
            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }

            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync().ConfigureAwait(false);
        await _pipe.DisposeAsync().ConfigureAwait(false);
    }

    private static string ComputeFingerprint(
        IReadOnlyList<Tool> tools,
        IReadOnlyList<Resource> resources,
        IReadOnlyList<ResourceTemplate> templates)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
            new SurfaceFingerprintPayload(tools, resources, templates),
            SerializerOptions);
        return Convert.ToHexString(SHA256.HashData(json));
    }

    private sealed record AttestationRequest;

    private sealed record SurfaceFingerprintPayload(
        IReadOnlyList<Tool> Tools,
        IReadOnlyList<Resource> Resources,
        IReadOnlyList<ResourceTemplate> ResourceTemplates);
}

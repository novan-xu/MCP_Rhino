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
    private const int PipeConnectTimeoutMilliseconds = 3000;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly NamedPipeClientStream _pipe;

    private BackendConnection(
        RouteEndpointDescriptor descriptor,
        NamedPipeClientStream pipe,
        McpClient client,
        BackendSurface? surface)
    {
        Descriptor = descriptor;
        _pipe = pipe;
        Client = client;
        Surface = surface;
    }

    public RouteEndpointDescriptor Descriptor { get; }

    public McpClient Client { get; }

    // Null for attestation-only probes.
    public BackendSurface? Surface { get; }

    public static async Task<BackendConnection> ConnectAsync(
        RouteEndpointDescriptor descriptor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        bool includeSurface = true)
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
            try
            {
                await pipe.ConnectAsync(PipeConnectTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // A timeout means either every instance is held by other clients or no listener exists.
                throw RoutePipeProbe.Exists(descriptor.PipeName)
                    ? new BackendConnectException(
                        BackendConnectException.EndpointBusy,
                        "Every connection slot of this document's route is held by other agent sessions.")
                    : new BackendConnectException(
                        BackendConnectException.EndpointNotListening,
                        "The document's route listener is not running.");
            }

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
                throw new BackendConnectException(
                    BackendConnectException.AttestationMismatch,
                    "The endpoint's private attestation does not match its published descriptor.");
            }

            BackendSurface? surface = includeSurface
                ? await ReadSurfaceAsync(client, cancellationToken).ConfigureAwait(false)
                : null;
            return new BackendConnection(descriptor, pipe, client, surface);
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

    private static async Task<BackendSurface> ReadSurfaceAsync(McpClient client, CancellationToken cancellationToken)
    {
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

        return new BackendSurface(tools, resources, templates, ComputeFingerprint(tools, resources, templates));
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

public sealed record BackendSurface(
    IReadOnlyList<Tool> Tools,
    IReadOnlyList<Resource> Resources,
    IReadOnlyList<ResourceTemplate> ResourceTemplates,
    string Fingerprint);

public sealed class BackendConnectException : Exception
{
    public const string EndpointBusy = "ENDPOINT_BUSY";
    public const string EndpointNotListening = "ENDPOINT_NOT_LISTENING";
    public const string AttestationMismatch = "ROUTE_ATTESTATION_MISMATCH";

    public BackendConnectException(string diagnostic, string message)
        : base(message)
    {
        Diagnostic = diagnostic;
    }

    public string Diagnostic { get; }
}

public static class RoutePipeProbe
{
    // Lists the pipe namespace instead of opening the pipe, so the check never consumes an instance.
    public static bool Exists(string pipeName)
    {
        try
        {
            return Directory.EnumerateFiles(@"\\.\pipe\")
                .Any(path => string.Equals(Path.GetFileName(path), pipeName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Unknown: report busy so the caller retries instead of concluding the document is gone.
            return true;
        }
    }
}

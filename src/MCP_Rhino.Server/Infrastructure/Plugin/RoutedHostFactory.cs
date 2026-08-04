using System.Text.Json;
using System.Text.Json.Nodes;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Infrastructure.Rhino.Live;
using MCP_Rhino.Server.Server;
using MCP_Rhino.Transport.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

public static class RoutedHostFactory
{
    public static Func<Stream, Stream, IHost> For(
        uint runtimeSerialNumber,
        RoutedAttestationProvider attestationProvider)
    {
        ArgumentNullException.ThrowIfNull(attestationProvider);
        return (input, output) => CreateConnectionHost(
            input,
            output,
            runtimeSerialNumber,
            attestationProvider);
    }

    private static IHost CreateConnectionHost(
        Stream input,
        Stream output,
        uint runtimeSerialNumber,
        RoutedAttestationProvider attestationProvider)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        PluginServiceRegistration.Configure(builder.Services);

        builder.Services.RemoveAll<ILiveRhinoDocumentAccessor>();
        builder.Services.AddSingleton<ILiveRhinoDocumentAccessor>(
            new RoutedLiveRhinoDocumentAccessor(runtimeSerialNumber));

        builder.Services
            .AddMcpServer()
            .AddRhinoTools()
            .AddRhinoResources()
            .WithMessageFilters(filters => filters.AddIncomingFilter(next => async (context, cancellationToken) =>
            {
                if (context.JsonRpcMessage is JsonRpcRequest request
                    && string.Equals(request.Method, RouteProtocol.AttestationRequestMethod, StringComparison.Ordinal))
                {
                    JsonNode? result = JsonNode.Parse(attestationProvider.Get());
                    await context.Server.SendMessageAsync(
                        new JsonRpcResponse
                        {
                            Id = request.Id,
                            Result = result
                        },
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                await next(context, cancellationToken).ConfigureAwait(false);
            }))
            .WithStreamServerTransport(input, output);

        return builder.Build();
    }
}

public sealed class RoutedAttestationProvider
{
    private string _json;

    public RoutedAttestationProvider(string json)
    {
        _json = RequireJson(json);
    }

    public string Get()
    {
        return Volatile.Read(ref _json);
    }

    public void Update(string json)
    {
        Volatile.Write(ref _json, RequireJson(json));
    }

    private static string RequireJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Attestation JSON is required.", nameof(json));
        }

        using JsonDocument _ = JsonDocument.Parse(json);
        return json;
    }
}

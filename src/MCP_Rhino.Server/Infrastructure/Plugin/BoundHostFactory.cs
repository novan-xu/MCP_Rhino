using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

public static class BoundHostFactory
{
    public static Func<Stream, Stream, IHost> For(uint runtimeSerialNumber)
    {
        return (input, output) => CreateConnectionHost(input, output, runtimeSerialNumber);
    }

    private static IHost CreateConnectionHost(Stream input, Stream output, uint runtimeSerialNumber)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        McpRhinoPlugin.ConfigurePluginServices(builder.Services);

        builder.Services.RemoveAll<ILiveRhinoDocumentAccessor>();
        builder.Services.AddSingleton<ILiveRhinoDocumentAccessor>(sp =>
            sp.GetRequiredService<ILiveRhinoDocumentAccessorFactory>().For(runtimeSerialNumber));

        builder.Services
            .AddMcpServer()
            .AddRhinoTools()
            .AddRhinoResources()
            .WithStreamServerTransport(input, output);

        return builder.Build();
    }
}

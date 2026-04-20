using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Infrastructure.CLI;
using MCP_Rhino.Server.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRhinoCore();
builder.Services.AddRhinoAgents();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .AddRhinoTools();

using var host = builder.Build();

using (IServiceScope scope = host.Services.CreateScope())
{
    var handler = scope.ServiceProvider.GetRequiredService<DeveloperCommandHandler>();
    if (handler.TryHandle(args))
    {
        return;
    }
}

await host.RunAsync();

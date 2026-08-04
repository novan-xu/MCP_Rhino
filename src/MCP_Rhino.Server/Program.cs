using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MCP_Rhino.Server.Infrastructure.CLI;
using MCP_Rhino.Server.Infrastructure.Runtime;
using MCP_Rhino.Server.Server;

RhinoRuntimeBootstrap.Initialize();

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddCliFallbackLiveRhinoAdapters()
    .AddRhinoApplication()
    .AddRhinoAgents();

using var host = builder.Build();

using IServiceScope scope = host.Services.CreateScope();
var handler = scope.ServiceProvider.GetRequiredService<DeveloperCommandHandler>();

if (handler.TryHandle(args))
{
    return;
}

Console.Error.WriteLine("CLI mode only supports explicit developer commands. Load the Rhino plugin and use MCP_Rhino.Router for MCP transport.");
Environment.ExitCode = 1;

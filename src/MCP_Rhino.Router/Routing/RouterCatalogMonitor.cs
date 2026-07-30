using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MCP_Rhino.Router.Routing;

public sealed class RouterCatalogMonitor : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly RouterRuntime _runtime;
    private readonly ILogger<RouterCatalogMonitor> _logger;

    public RouterCatalogMonitor(RouterRuntime runtime, ILogger<RouterCatalogMonitor> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await _runtime.CheckForToolSurfaceChangeAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MCP_Rhino Router catalog refresh failed.");
            }
        }
    }
}

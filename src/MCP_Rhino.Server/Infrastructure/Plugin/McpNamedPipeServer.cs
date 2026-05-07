extern alias rhinocommon;

using System.IO.Pipes;
using Microsoft.Extensions.Hosting;
using RhinoApp = rhinocommon::Rhino.RhinoApp;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

public sealed class McpNamedPipeServer : IDisposable
{
    private readonly string _pipeName;
    private readonly Func<Stream, Stream, IHost> _hostFactory;
    private readonly bool _stopOnPipeCreateFailure;
    private readonly string? _pipeCreateFailureHint;
    private readonly CancellationTokenSource _shutdown = new();

    private Task? _acceptLoop;

    public McpNamedPipeServer(
        string pipeName,
        Func<Stream, Stream, IHost> hostFactory,
        bool stopOnPipeCreateFailure = false,
        string? pipeCreateFailureHint = null)
    {
        _pipeName = pipeName;
        _hostFactory = hostFactory;
        _stopOnPipeCreateFailure = stopOnPipeCreateFailure;
        _pipeCreateFailureHint = pipeCreateFailureHint;
    }

    public void Start()
    {
        if (_acceptLoop is not null)
        {
            throw new InvalidOperationException("Named pipe server has already started.");
        }

        _acceptLoop = Task.Run(() => AcceptLoopAsync(_shutdown.Token));
    }

    public void Dispose()
    {
        _shutdown.Cancel();

        try
        {
            _acceptLoop?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
        }
        finally
        {
            _shutdown.Dispose();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;

            try
            {
                pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                RhinoApp.WriteLine($"MCP_Rhino pipe client connected: \\\\.\\pipe\\{_pipeName}");

                using IHost host = _hostFactory(pipe, pipe);
                await host.RunAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException ex) when (pipe is null && _stopOnPipeCreateFailure)
            {
                RhinoApp.WriteLine(
                    $"MCP_Rhino named pipe unavailable: \\\\.\\pipe\\{_pipeName}. {ex.Message}");
                if (!string.IsNullOrWhiteSpace(_pipeCreateFailureHint))
                {
                    RhinoApp.WriteLine(_pipeCreateFailureHint);
                }

                break;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"MCP_Rhino pipe server error: {ex}");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (pipe is not null)
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }
}

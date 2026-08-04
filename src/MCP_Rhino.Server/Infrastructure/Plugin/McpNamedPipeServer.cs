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
    private readonly int _maxConcurrentConnections;
    private readonly PipeOptions _pipeOptions;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _connectionSlots;
    private readonly object _connectionLock = new();
    private readonly object _stopLock = new();
    private readonly Dictionary<long, Task> _connectionTasks = new();
    private readonly TaskCompletionSource<bool> _listenerReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Task? _acceptLoop;
    private Task? _stopTask;
    private long _nextConnectionId;
    private int _disposed;
    private int _resourcesDisposed;

    public McpNamedPipeServer(
        string pipeName,
        Func<Stream, Stream, IHost> hostFactory,
        bool stopOnPipeCreateFailure = false,
        string? pipeCreateFailureHint = null,
        int maxConcurrentConnections = 1,
        bool currentUserOnly = false)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("A pipe name is required.", nameof(pipeName));
        }

        if (maxConcurrentConnections is < 1 or > 254)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentConnections));
        }

        _pipeName = pipeName;
        _hostFactory = hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));
        _stopOnPipeCreateFailure = stopOnPipeCreateFailure;
        _pipeCreateFailureHint = pipeCreateFailureHint;
        _maxConcurrentConnections = maxConcurrentConnections;
        _pipeOptions = PipeOptions.Asynchronous
            | (currentUserOnly ? PipeOptions.CurrentUserOnly : PipeOptions.None);
        _connectionSlots = new SemaphoreSlim(maxConcurrentConnections, maxConcurrentConnections);
    }

    public void Start()
    {
        ThrowIfDisposed();
        if (_acceptLoop is not null)
        {
            throw new InvalidOperationException("Named pipe server has already started.");
        }

        _acceptLoop = Task.Run(() => AcceptLoopAsync(_shutdown.Token));
    }

    public bool StartAndWait(TimeSpan timeout)
    {
        Start();
        try
        {
            return _listenerReady.Task.Wait(timeout) && _listenerReady.Task.Result;
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
            return false;
        }
    }

    public Task StopAsync()
    {
        lock (_stopLock)
        {
            return _stopTask ??= StopCoreAsync();
        }
    }

    public void DisposeAfterStopRequested()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Task stopTask = StopAsync();
        if (stopTask.IsCompleted)
        {
            DisposeResources();
            return;
        }

        _ = stopTask.ContinueWith(
            _ => DisposeResources(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task StopCoreAsync()
    {
        _shutdown.Cancel();

        Task? acceptLoop = _acceptLoop;
        if (acceptLoop is not null)
        {
            try
            {
                await acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        Task[] connectionTasks;
        lock (_connectionLock)
        {
            connectionTasks = _connectionTasks.Values.ToArray();
        }

        try
        {
            await Task.WhenAll(connectionTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Task stopTask = StopAsync();
        bool completed = false;
        try
        {
            completed = stopTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
        }
        finally
        {
            if (completed || stopTask.IsCompleted)
            {
                DisposeResources();
            }
            else
            {
                _ = stopTask.ContinueWith(
                    _ => DisposeResources(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            bool slotAcquired = false;

            try
            {
                await _connectionSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
                slotAcquired = true;

                pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    _maxConcurrentConnections,
                    PipeTransmissionMode.Byte,
                    _pipeOptions);

                _listenerReady.TrySetResult(true);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                RhinoApp.WriteLine($"MCP_Rhino pipe client connected: \\\\.\\pipe\\{_pipeName}");

                NamedPipeServerStream connectedPipe = pipe;
                pipe = null;
                slotAcquired = false;
                TrackConnection(RunConnectionAsync(connectedPipe, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _listenerReady.TrySetResult(false);
                break;
            }
            catch (IOException ex) when (pipe is null && _stopOnPipeCreateFailure)
            {
                _listenerReady.TrySetResult(false);
                RhinoApp.WriteLine($"MCP_Rhino named pipe unavailable: \\\\.\\pipe\\{_pipeName}. {ex.Message}");
                if (!string.IsNullOrWhiteSpace(_pipeCreateFailureHint))
                {
                    RhinoApp.WriteLine(_pipeCreateFailureHint);
                }

                break;
            }
            catch (Exception ex)
            {
                _listenerReady.TrySetResult(false);
                RhinoApp.WriteLine($"MCP_Rhino pipe server error: {ex}");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
            finally
            {
                if (pipe is not null)
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                }

                if (slotAcquired)
                {
                    _connectionSlots.Release();
                }
            }
        }
    }

    private async Task RunConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            await using (pipe.ConfigureAwait(false))
            using (IHost host = _hostFactory(pipe, pipe))
            {
                await host.RunAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"MCP_Rhino pipe connection error: {ex}");
        }
        finally
        {
            _connectionSlots.Release();
        }
    }

    private void TrackConnection(Task task)
    {
        long connectionId = Interlocked.Increment(ref _nextConnectionId);
        lock (_connectionLock)
        {
            _connectionTasks.Add(connectionId, task);
        }

        _ = task.ContinueWith(
            _ =>
            {
                lock (_connectionLock)
                {
                    _connectionTasks.Remove(connectionId);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }

    private void DisposeResources()
    {
        if (Interlocked.Exchange(ref _resourcesDisposed, 1) != 0)
        {
            return;
        }

        _connectionSlots.Dispose();
        _shutdown.Dispose();
    }
}

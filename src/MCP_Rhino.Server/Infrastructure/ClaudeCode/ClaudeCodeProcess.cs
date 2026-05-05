using System.Diagnostics;

namespace MCP_Rhino.Server.Infrastructure.ClaudeCode;

public sealed class ClaudeCodeProcess : IClaudeCodeProcess
{
    private readonly CancellationTokenSource _shutdown = new();
    private Process? _process;
    private Task? _stdoutTask;
    private Task? _stderrTask;

    public event EventHandler<ClaudeCodeStreamEvent>? EventReceived;

    public event EventHandler<string>? ErrorReceived;

    public event EventHandler<int?>? Exited;

    public bool IsRunning => _process is { HasExited: false };

    public void Start(string executablePath, IReadOnlyList<string> arguments, string workingDirectory)
    {
        if (_process is not null)
        {
            throw new InvalidOperationException("Claude Code process has already started.");
        }

        _process = new Process
        {
            StartInfo = ClaudeCodeAvailability.CreateStartInfo(executablePath, arguments, workingDirectory),
            EnableRaisingEvents = true
        };
        _process.Exited += (_, _) => Exited?.Invoke(this, _process?.ExitCode);
        _process.Start();

        _stdoutTask = StreamJsonReader.ReadEventsAsync(
            _process.StandardOutput,
            streamEvent => EventReceived?.Invoke(this, streamEvent),
            line => ErrorReceived?.Invoke(this, $"Unparsed stdout: {line}"),
            _shutdown.Token);

        _stderrTask = ReadStderrAsync(_process.StandardError, _shutdown.Token);
    }

    public Task SendUserMessageAsync(string text, CancellationToken cancellationToken)
    {
        if (_process is null || _process.HasExited)
        {
            throw new InvalidOperationException("Claude Code process is not running.");
        }

        return StreamJsonWriter.WriteUserMessageAsync(_process.StandardInput, text, cancellationToken);
    }

    public void Stop()
    {
        if (_process is null)
        {
            return;
        }

        _shutdown.Cancel();

        try
        {
            if (!_process.HasExited)
            {
                _process.StandardInput.Close();
                if (!_process.WaitForExit(200))
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        Stop();
        try
        {
            Task.WaitAll(new[] { _stdoutTask, _stderrTask }.Where(task => task is not null).Cast<Task>().ToArray(), 1000);
        }
        catch (AggregateException)
        {
        }

        _process?.Dispose();
        _shutdown.Dispose();
    }

    private async Task ReadStderrAsync(TextReader reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            ErrorReceived?.Invoke(this, line);
        }
    }
}

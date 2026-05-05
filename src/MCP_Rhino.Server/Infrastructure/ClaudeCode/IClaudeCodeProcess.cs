namespace MCP_Rhino.Server.Infrastructure.ClaudeCode;

public interface IClaudeCodeProcess : IDisposable
{
    event EventHandler<ClaudeCodeStreamEvent>? EventReceived;

    event EventHandler<string>? ErrorReceived;

    event EventHandler<int?>? Exited;

    bool IsRunning { get; }

    void Start(string executablePath, IReadOnlyList<string> arguments, string workingDirectory);

    Task SendUserMessageAsync(string text, CancellationToken cancellationToken);

    void Stop();
}

namespace MCP_Rhino.Companion;

public interface IAgentSession : IDisposable
{
    event EventHandler<CompanionUiEvent>? EventReceived;

    Task StartAsync(CancellationToken cancellationToken);

    Task SendUserMessageAsync(string text, CancellationToken cancellationToken);

    void Stop();
}

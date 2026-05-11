namespace MCP_Rhino.Companion;

public interface IAgentSession : IDisposable
{
    event EventHandler<CompanionUiEvent>? EventReceived;

    Task StartAsync(CancellationToken cancellationToken);

    Task SendUserMessageAsync(CompanionUserMessage message, CancellationToken cancellationToken);

    void Stop();
}

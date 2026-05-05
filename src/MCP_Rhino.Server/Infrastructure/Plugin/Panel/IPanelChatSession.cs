using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Panel;

public sealed record PanelChatEvent(
    ChatRole Role,
    string Text,
    string? ToolName = null,
    ChatToolCallStatus? ToolStatus = null);

public interface IPanelChatSession : IDisposable
{
    event EventHandler<PanelChatEvent>? EventReceived;

    uint RuntimeSerialNumber { get; }

    string DocumentPath { get; }

    string PipeName { get; }

    bool IsInputEnabled { get; }

    Task StartAsync(string? modelId, CancellationToken cancellationToken);

    Task SendUserMessageAsync(string text, CancellationToken cancellationToken);

    void Stop();
}

extern alias rhinocommon;
using System.Text.Json;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live.Scripting;

public sealed class LiveRhinoScriptGlobals(RhinoDoc document, JsonElement parameters, CancellationToken cancellationToken)
{
    private readonly object _gate = new();
    private readonly List<string> _messages = [];
    public RhinoDoc doc { get; } = document;
    public JsonElement Parameters { get; } = parameters;
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public object? Result { get; set; }
    public void Checkpoint() => CancellationToken.ThrowIfCancellationRequested();
    public void Log(string message)
    {
        Checkpoint();
        lock (_gate)
        {
            if (_messages.Count >= 100 || message is null || message.Length > 4096)
                throw new InvalidOperationException("SCRIPT_LOG_LIMIT: at most 100 messages of 4096 characters.");
            _messages.Add(message);
        }
    }
    internal string[] ReadLogs() { lock (_gate) return _messages.ToArray(); }
}

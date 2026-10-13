using System.Text.Json;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class RhinoScriptPreviewResponse
{
    public string PreviewToken { get; init; } = "";
    public string FilePath { get; init; } = "";
    public uint RuntimeSerialNumber { get; init; }
    public string ScriptSha256 { get; init; } = "";
    public DateTimeOffset ExpiresAt { get; init; }
    public int TimeoutSeconds { get; init; }
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
    public string PreviewKind { get; init; } = "CompilationOnly";
    public string ExecutionContract { get; init; } = "Trusted synchronous C#; doc is the routed live document. Preview does not run code or predict model effects. Apply executes once against current live state. Use doc, Parameters, Result, Log(string), CancellationToken and Checkpoint(). No background Rhino work, manual Undo, document switching, save or close. Cancellation/deadlines are cooperative, not force termination. Full-trust code is not sandboxed; external effects cannot be rolled back.";
}

public sealed class RhinoScriptStatusResponse
{
    public string ExecutionId { get; init; } = "";
    public string FilePath { get; init; } = "";
    public uint RuntimeSerialNumber { get; init; }
    public string State { get; init; } = "Queued";
    public bool IsTerminal { get; init; }
    public bool CancellationRequested { get; init; }
    public double ElapsedSeconds { get; init; }
    public uint UndoRecordSerialNumber { get; init; }
    public string Rollback { get; init; } = "NotNeeded";
    public string Error { get; init; } = "";
    public JsonElement? Result { get; init; }
    public IReadOnlyList<string> Logs { get; init; } = [];
}

extern alias rhinocommon;
using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.Scripting;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live.Scripting;

public sealed class LiveRhinoScriptExecutor(ILiveRhinoDocumentAccessor accessor) : ILiveRhinoScriptExecutor, IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Submission> _submissions = new(StringComparer.Ordinal);
    private bool _disposed;

    public OperationResponse<RhinoScriptPreviewResponse> Preview(RhinoScriptRequest request)
    {
        // Resolve first: CLI fallback and closed/conflicting documents never compile or execute code.
        var identity = accessor.Execute(request.FilePath, doc => OperationResponse<uint>.Ok(doc.RuntimeSerialNumber));
        if (!identity.Success) return OperationResponse<RhinoScriptPreviewResponse>.Fail(identity.Message);
        lock (_gate)
        {
            Cleanup();
            if (_disposed || _submissions.Count >= 16)
                return OperationResponse<RhinoScriptPreviewResponse>.Fail("SCRIPT_SESSION_CAPACITY: this connection retains at most 16 submissions for ten minutes.");
            try
            {
                var compiled = LiveRhinoScriptCompiler.Compile(request.Script);
                using var parameters = JsonDocument.Parse(request.ParametersJson);
                var preview = new RhinoScriptPreviewResponse
                {
                    PreviewToken = Guid.NewGuid().ToString("N"), FilePath = Path.GetFullPath(request.FilePath),
                    RuntimeSerialNumber = identity.Data, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                    ScriptSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Script))),
                    TimeoutSeconds = request.TimeoutSeconds, Diagnostics = compiled.Diagnostics
                };
                _submissions.Add(preview.PreviewToken, new Submission(preview, compiled.Runner, parameters.RootElement.Clone()));
                return OperationResponse<RhinoScriptPreviewResponse>.Ok(preview, "Compiled only; code has not executed.");
            }
            catch (Exception ex) { return OperationResponse<RhinoScriptPreviewResponse>.Fail(ex.Message); }
        }
    }

    public OperationResponse<RhinoScriptStatusResponse> Apply(string filePath, string previewToken)
    {
        lock (_gate)
        {
            if (_disposed) return Failure("SCRIPT_CONNECTION_CLOSED");
            if (!_submissions.TryGetValue(previewToken, out var submission) || !SamePath(filePath, submission.Preview.FilePath))
                return Failure("SCRIPT_PREVIEW_NOT_FOUND_OR_TARGET_MISMATCH");
            if (submission.Execution is not null) return OperationResponse<RhinoScriptStatusResponse>.Ok(Snapshot(submission.Execution));
            if (DateTimeOffset.UtcNow > submission.Preview.ExpiresAt) return Failure("SCRIPT_PREVIEW_EXPIRED");
            var execution = new Execution(submission);
            submission.Execution = execution; // Consume once BEFORE scheduling any Rhino work.
            execution.Work = Task.Run(() => Execute(execution));
            return OperationResponse<RhinoScriptStatusResponse>.Ok(Snapshot(execution), "Execution accepted once; poll get_rhino_script_status. Do not submit another script to retry a running execution.");
        }
    }

    public OperationResponse<RhinoScriptStatusResponse> GetStatus(string filePath, string executionId)
    {
        lock (_gate)
        {
            var execution = Find(filePath, executionId);
            return execution is null ? Failure("SCRIPT_EXECUTION_NOT_FOUND_OR_TARGET_MISMATCH")
                : OperationResponse<RhinoScriptStatusResponse>.Ok(Snapshot(execution));
        }
    }

    public OperationResponse<RhinoScriptStatusResponse> Cancel(string filePath, string executionId)
    {
        Execution? execution;
        lock (_gate) execution = Find(filePath, executionId);
        if (execution is null) return Failure("SCRIPT_EXECUTION_NOT_FOUND_OR_TARGET_MISMATCH");
        lock (execution.Gate)
        {
            if (Terminal(execution.State)) return OperationResponse<RhinoScriptStatusResponse>.Ok(Snapshot(execution));
        }
        RequestCancellation(execution);
        return OperationResponse<RhinoScriptStatusResponse>.Ok(Snapshot(execution), "Cancellation requested. Running full-trust code must cooperate or return; it has not been force-stopped.");
    }

    private void Execute(Execution execution)
    {
        try
        {
            // No finite UI wait: a timeout must never return failure while a queued mutation later runs.
            // The caller already has an execution id and can inspect/cancel without waiting on Rhino.
            var response = accessor.Execute(execution.Submission.Preview.FilePath, doc =>
            {
                RunOnRhinoThread(doc, execution);
                return OperationResponse<bool>.Ok(true);
            }, Timeout.InfiniteTimeSpan);
            if (!response.Success) Finish(execution, "Failed", response.Message);
        }
        catch (Exception ex) { Finish(execution, "Failed", ex.ToString()); }
    }

    private static void RunOnRhinoThread(RhinoDoc doc, Execution execution)
    {
        var submission = execution.Submission;
        if (execution.Cancellation.IsCancellationRequested) { Finish(execution, "Cancelled", "Cancelled before execution."); return; }
        if (doc.RuntimeSerialNumber != submission.Preview.RuntimeSerialNumber || !SamePath(doc.Path, submission.Preview.FilePath))
        { Finish(execution, "Failed", "SCRIPT_DOCUMENT_TARGET_CHANGED"); return; }
        if (DateTimeOffset.UtcNow > submission.Preview.ExpiresAt)
        { Finish(execution, "Failed", "SCRIPT_PREVIEW_EXPIRED_WHILE_QUEUED"); return; }
        if (!doc.UndoRecordingEnabled || doc.UndoRecordingIsActive)
        { Finish(execution, "Failed", "SCRIPT_REQUIRES_IDLE_DOCUMENT_WITH_UNDO_ENABLED"); return; }

        uint undo = 0;
        bool ended = false, markerInstalled = false, rollbackObserved = false;
        lock (execution.Gate) { execution.State = "Running"; execution.StartedAt = DateTimeOffset.UtcNow; }
        // Catch exceptions thrown by script-registered cancellation callbacks; a CTS timer
        // would otherwise allow them to escape on the thread pool and terminate Rhino.
        using var deadline = new System.Threading.Timer(_ => RequestCancellation(execution), null,
            TimeSpan.FromSeconds(submission.Preview.TimeoutSeconds), Timeout.InfiniteTimeSpan);
        try
        {
            execution.Cancellation.Token.ThrowIfCancellationRequested();
            undo = doc.BeginUndoRecord("MCP: Custom Rhino script");
            if (undo == 0) throw new InvalidOperationException("SCRIPT_UNDO_RECORD_UNAVAILABLE");
            lock (execution.Gate) execution.UndoRecord = undo;
            // Even a failure before the first edit owns a real record. Undo must never pop user work.
            markerInstalled = doc.AddCustomUndoEvent("MCP script rollback boundary", (_, _) => rollbackObserved = true);
            if (!markerInstalled) throw new InvalidOperationException("SCRIPT_UNDO_BOUNDARY_UNAVAILABLE");
            var globals = new LiveRhinoScriptGlobals(doc, submission.Parameters, execution.Cancellation.Token);
            lock (execution.Gate) execution.Globals = globals;
            submission.Runner(globals, execution.Cancellation.Token).GetAwaiter().GetResult();
            globals.Checkpoint();
            JsonElement result = JsonSerializer.SerializeToElement(globals.Result, new JsonSerializerOptions { MaxDepth = 32 });
            if (Encoding.UTF8.GetByteCount(result.GetRawText()) > 262144)
                throw new InvalidOperationException("SCRIPT_RESULT_LIMIT: JSON result exceeds 256 KiB.");
            globals.Checkpoint();
            if (doc.CurrentUndoRecordSerialNumber != undo)
                throw new InvalidOperationException("SCRIPT_UNDO_OWNERSHIP_LOST: scripts must not manipulate Undo or run Rhino commands.");
            ended = doc.EndUndoRecord(undo);
            if (!ended) throw new InvalidOperationException("SCRIPT_UNDO_END_FAILED");
            lock (execution.Gate) execution.Result = result;
            Finish(execution, "Succeeded", "");
        }
        catch (Exception ex)
        {
            string rollback = "NotNeeded";
            if (undo != 0)
            {
                bool owned = !ended && doc.CurrentUndoRecordSerialNumber == undo;
                if (owned) ended = doc.EndUndoRecord(undo);
                if (owned && ended && markerInstalled)
                {
                    try
                    {
                        // Rhino may start an inverse record while replaying custom Undo events.
                        // Close only the new serial created by this synchronous Undo invocation.
                        uint inverseRecord = doc.NextUndoRecordSerialNumber;
                        bool undone = doc.Undo();
                        bool inverseClosed = doc.CurrentUndoRecordSerialNumber != inverseRecord || doc.EndUndoRecord(inverseRecord);
                        rollback = undone && rollbackObserved && inverseClosed ? "Applied" : "Failed";
                    }
                    catch (Exception rollbackError) { rollback = "Failed: " + rollbackError.Message; }
                }
                else rollback = "NotAttempted: Undo ownership/boundary could not be established; inspect for partial changes.";
            }
            lock (execution.Gate) execution.Rollback = rollback;
            Finish(execution, execution.Cancellation.IsCancellationRequested ? "Cancelled" : "Failed", ex.ToString());
        }
        finally
        {
            if (!ended && undo != 0 && doc.CurrentUndoRecordSerialNumber == undo) doc.EndUndoRecord(undo);
            try { doc.Views.Redraw(); } catch { /* Display refresh must not change the execution outcome. */ }
        }
    }

    private Execution? Find(string path, string id) => _submissions.Values.Select(s => s.Execution)
        .FirstOrDefault(e => e is not null && e.Id == id && SamePath(path, e.Submission.Preview.FilePath));
    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    private static bool Terminal(string state) => state is "Succeeded" or "Failed" or "Cancelled";
    private static OperationResponse<RhinoScriptStatusResponse> Failure(string message) => OperationResponse<RhinoScriptStatusResponse>.Fail(message);
    private static void Finish(Execution e, string state, string error)
    {
        lock (e.Gate)
        {
            e.State = state; e.Error = error.Length > 16384 ? error[..16384] : error;
            e.FinishedAt = DateTimeOffset.UtcNow;
        }
    }
    private static RhinoScriptStatusResponse Snapshot(Execution e)
    {
        lock (e.Gate) return new()
        {
            ExecutionId = e.Id, FilePath = e.Submission.Preview.FilePath,
            RuntimeSerialNumber = e.Submission.Preview.RuntimeSerialNumber, State = e.State,
            IsTerminal = Terminal(e.State), CancellationRequested = e.Cancellation.IsCancellationRequested,
            ElapsedSeconds = ((e.FinishedAt ?? DateTimeOffset.UtcNow) - (e.StartedAt ?? e.CreatedAt)).TotalSeconds,
            UndoRecordSerialNumber = e.UndoRecord, Rollback = e.Rollback, Error = e.Error,
            Result = e.Result, Logs = e.Globals?.ReadLogs() ?? []
        };
    }
    private void Cleanup()
    {
        foreach (var item in _submissions.ToArray())
        {
            var e = item.Value.Execution;
            if (e is null ? DateTimeOffset.UtcNow > item.Value.Preview.ExpiresAt
                : e.Work?.IsCompleted == true && DateTimeOffset.UtcNow > e.FinishedAt?.AddMinutes(10))
            { _submissions.Remove(item.Key); e?.Cancellation.Dispose(); }
        }
    }
    private static void RequestCancellation(Execution e)
    {
        lock (e.Gate) { if (Terminal(e.State)) return; }
        try { e.Cancellation.Cancel(); }
        catch (AggregateException error) { lock (e.Gate) e.Error = "Cancellation callback failed: " + error.Message; }
        catch (ObjectDisposedException) { /* A completed expired job can race timer disposal. */ }
    }
    public void Dispose()
    {
        Execution[] running;
        lock (_gate)
        {
            _disposed = true;
            running = _submissions.Values.Select(s => s.Execution).OfType<Execution>().Where(e => !Terminal(Snapshot(e).State)).ToArray();
        }
        foreach (var execution in running) RequestCancellation(execution);
    }
    private sealed class Submission(RhinoScriptPreviewResponse preview, ScriptRunner<object?> runner, JsonElement parameters)
    {
        public RhinoScriptPreviewResponse Preview { get; } = preview;
        public ScriptRunner<object?> Runner { get; } = runner;
        public JsonElement Parameters { get; } = parameters;
        public Execution? Execution { get; set; }
    }
    private sealed class Execution(Submission submission)
    {
        public object Gate { get; } = new();
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public Submission Submission { get; } = submission;
        public CancellationTokenSource Cancellation { get; } = new();
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string State { get; set; } = "Queued";
        public string Error { get; set; } = "";
        public string Rollback { get; set; } = "NotNeeded";
        public uint UndoRecord { get; set; }
        public JsonElement? Result { get; set; }
        public LiveRhinoScriptGlobals? Globals { get; set; }
        public Task? Work { get; set; }
    }
}

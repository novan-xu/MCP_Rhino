using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Infrastructure.CLI;

// All types in this file belong to the temp smoke unit under
// Project_Test/_temp_live_document_smoke/. Everything is internal
// so that deleting the folder does not surface an unused public API.

internal enum LiveSmokeStatus
{
    Pending,
    Pass,
    Fail,
    Skip
}

internal sealed class LiveSmokeCheckpoint
{
    public required string Stage { get; init; }
    public required string Feature { get; init; }
    public required IReadOnlyList<string> CodeLocations { get; init; }
    public required string InputSummary { get; init; }
    public required string Expectation { get; init; }
    public required IReadOnlyList<string> Suspects { get; init; }

    public LiveSmokeStatus Status { get; set; } = LiveSmokeStatus.Pending;
    public bool? ObservedSuccess { get; set; }
    public string? ObservedMessage { get; set; }
    public string? ObservedDataSummary { get; set; }
    // UndoDelta is informational only — inside a Rhino command the outer command
    // already opens an undo record, so nested BeginUndoRecord calls typically
    // return 0 and NextUndoRecordSerialNumber does not advance. Do NOT assert on
    // this value as proof of mutation; use ObjectsDelta/LayersDelta instead.
    public int UndoDelta { get; set; }
    public int ObjectsDelta { get; set; }
    public int LayersDelta { get; set; }
    public string? Evidence { get; set; }
    public string? FailureReason { get; set; }

    public void RecordResponse<T>(OperationResponse<T> response)
    {
        ObservedSuccess = response.Success;
        ObservedMessage = response.Message;
    }

    public void RecordResponse(OperationResponse response)
    {
        ObservedSuccess = response.Success;
        ObservedMessage = response.Message;
    }
}

// Thrown by checkpoint bodies to explicitly mark the checkpoint as SKIP
// (precondition unmet — e.g. fixture file contains no objects). Any OTHER
// exception is treated as FAIL.
internal sealed class LiveSmokeSkipException : Exception
{
    public LiveSmokeSkipException(string message) : base(message) { }
}

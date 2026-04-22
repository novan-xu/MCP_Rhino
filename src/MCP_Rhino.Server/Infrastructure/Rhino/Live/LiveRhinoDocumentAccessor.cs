extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoDocumentAccessor : ILiveRhinoDocumentAccessor
{
    private static readonly TimeSpan MainThreadTimeout = TimeSpan.FromSeconds(10);

    public OperationResponse<T> Execute<T>(string filePath, Func<RhinoDoc, OperationResponse<T>> work)
    {
        return InvokeOnMainThread(() =>
        {
            OperationResponse<RhinoDoc> document = ResolveDocument(filePath);
            if (!document.Success || document.Data is null)
            {
                return OperationResponse<T>.Fail(document.Message);
            }

            return work(document.Data);
        });
    }

    /// <summary>
    /// Runs <paramref name="work"/> against the live RhinoDoc wrapped in a
    /// BeginUndoRecord/EndUndoRecord pair so a single MCP mutation produces one
    /// undo step.
    /// </summary>
    /// <remarks>
    /// Caveat for test/telemetry callers — BeginUndoRecord may return 0 when
    /// this method runs inside an outer undo scope (e.g. nested under a Rhino
    /// command's RunCommand). In that case Rhino attaches the mutations to the
    /// outer command's undo record and NextUndoRecordSerialNumber does not
    /// advance for this call. The document changes still happen; only the
    /// undo-serial telemetry is misleading. Do NOT treat a
    /// NextUndoRecordSerialNumber delta as proof that a mutation ran — use
    /// document.Modified or before/after object/layer count deltas instead.
    /// </remarks>
    public OperationResponse<T> ExecuteWithUndo<T>(
        string filePath,
        string undoDescription,
        Func<RhinoDoc, OperationResponse<(bool Mutated, T Result)>> work)
    {
        return Execute(filePath, document =>
        {
            // RhinoCommon 8 only exposes Begin/EndUndoRecord; there is no CancelUndoRecord on RhinoDoc.
            // An empty Undo record (no document changes between Begin and End) is discarded by Rhino's UndoManager.
            // BeginUndoRecord may return 0 when already inside an outer undo scope (nested under a Rhino
            // command) — in that case CloseUndoRecord below is a no-op and the mutations attach to the
            // outer command's record. See the <remarks> on this method for the testing caveat.
            uint undoRecord = document.BeginUndoRecord(undoDescription);
            bool closed = false;

            try
            {
                OperationResponse<(bool Mutated, T Result)> response = work(document);
                CloseUndoRecord(document, undoRecord);
                closed = true;

                if (!response.Success)
                {
                    return OperationResponse<T>.Fail(response.Message);
                }

                (bool Mutated, T Result) payload = response.Data;
                return OperationResponse<T>.Ok(payload.Result, response.Message);
            }
            finally
            {
                if (!closed)
                {
                    CloseUndoRecord(document, undoRecord);
                }
            }
        });
    }

    private static void CloseUndoRecord(RhinoDoc document, uint undoRecord)
    {
        if (undoRecord == 0)
        {
            return;
        }

        document.EndUndoRecord(undoRecord);
    }

    public bool TryGetActiveDocumentState(string filePath, out bool hasUnsavedChanges)
    {
        bool matched = false;
        bool modified = false;

        InvokeOnMainThread(() =>
        {
            RhinoDoc? document = RhinoDoc.ActiveDoc;
            if (document is null || string.IsNullOrWhiteSpace(document.Path))
            {
                return OperationResponse<bool>.Ok(false);
            }

            if (PathsEqual(document.Path, filePath))
            {
                matched = true;
                modified = document.Modified;
            }

            return OperationResponse<bool>.Ok(true);
        });

        hasUnsavedChanges = modified;
        return matched;
    }

    private static OperationResponse<RhinoDoc> ResolveDocument(string filePath)
    {
        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null)
        {
            return OperationResponse<RhinoDoc>.Fail("NO_ACTIVE_DOCUMENT");
        }

        if (string.IsNullOrWhiteSpace(document.Path))
        {
            return OperationResponse<RhinoDoc>.Fail("ACTIVE_DOC_UNSAVED");
        }

        if (!PathsEqual(document.Path, filePath))
        {
            return OperationResponse<RhinoDoc>.Fail("FILE_NOT_ACTIVE");
        }

        return OperationResponse<RhinoDoc>.Ok(document);
    }

    private static bool PathsEqual(string left, string right)
    {
        string normalizedLeft = Path.GetFullPath(left);
        string normalizedRight = Path.GetFullPath(right);
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static OperationResponse<T> InvokeOnMainThread<T>(Func<OperationResponse<T>> work)
    {
        if (!RhinoApp.InvokeRequired)
        {
            return work();
        }

        using var signal = new ManualResetEventSlim(false);
        OperationResponse<T>? response = null;
        Exception? capturedException = null;

        RhinoApp.InvokeOnUiThread(new Action(() =>
        {
            try
            {
                response = work();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                signal.Set();
            }
        }));

        if (!signal.Wait(MainThreadTimeout))
        {
            return OperationResponse<T>.Fail("RHINO_MAIN_THREAD_BUSY");
        }

        if (capturedException is not null)
        {
            return OperationResponse<T>.Fail(capturedException.Message);
        }

        return response ?? OperationResponse<T>.Fail("RHINO_MAIN_THREAD_BUSY");
    }
}

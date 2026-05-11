extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public abstract class LiveRhinoDocumentAccessorBase : ILiveRhinoDocumentAccessor
{
    private static readonly TimeSpan MainThreadTimeout = TimeSpan.FromSeconds(10);

    public OperationResponse<T> Execute<T>(
        string filePath,
        Func<RhinoDoc, OperationResponse<T>> work,
        TimeSpan? timeout = null)
    {
        return InvokeOnMainThread(() =>
        {
            OperationResponse<RhinoDoc> document = ResolveDocument(filePath);
            if (!document.Success || document.Data is null)
            {
                return OperationResponse<T>.Fail(document.Message);
            }

            return work(document.Data);
        }, timeout ?? MainThreadTimeout);
    }

    public OperationResponse<T> ExecuteWithUndo<T>(
        string filePath,
        string undoDescription,
        Func<RhinoDoc, OperationResponse<(bool Mutated, T Result)>> work,
        TimeSpan? timeout = null)
    {
        return Execute(filePath, document =>
        {
            uint currentUndoRecordBefore = document.CurrentUndoRecordSerialNumber;
            uint undoRecord = document.BeginUndoRecord(undoDescription);
            bool closed = false;

            try
            {
                OperationResponse<(bool Mutated, T Result)> response;
                try
                {
                    response = work(document);
                }
                catch (Exception ex)
                {
                    CloseUndoRecord(document, undoRecord);
                    closed = true;
                    OperationResponse rollback = RollBackFailedUndoRecord(document, currentUndoRecordBefore);
                    return OperationResponse<T>.Fail(ComposeFailureWithRollbackStatus(ex.Message, rollback));
                }

                CloseUndoRecord(document, undoRecord);
                closed = true;

                if (!response.Success)
                {
                    OperationResponse rollback = RollBackFailedUndoRecord(document, currentUndoRecordBefore);
                    return OperationResponse<T>.Fail(ComposeFailureWithRollbackStatus(response.Message, rollback));
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
        }, timeout);
    }

    public abstract bool TryGetActiveDocumentState(string filePath, out bool hasUnsavedChanges);

    protected abstract OperationResponse<RhinoDoc> ResolveDocument(string filePath);

    protected static void CloseUndoRecord(RhinoDoc document, uint undoRecord)
    {
        if (undoRecord == 0)
        {
            return;
        }

        document.EndUndoRecord(undoRecord);
    }

    private static OperationResponse RollBackFailedUndoRecord(RhinoDoc document, uint currentUndoRecordBefore)
    {
        if (document.CurrentUndoRecordSerialNumber == currentUndoRecordBefore)
        {
            return OperationResponse.Ok();
        }

        try
        {
            if (document.Undo())
            {
                document.Views.Redraw();
                return OperationResponse.Ok("ROLLBACK_APPLIED");
            }

            return OperationResponse.Fail("ROLLBACK_FAILED: failed to undo the failed MCP mutation; the live document may contain partial changes.");
        }
        catch (Exception ex)
        {
            return OperationResponse.Fail($"ROLLBACK_FAILED: {ex.Message}");
        }
    }

    private static string ComposeFailureWithRollbackStatus(string message, OperationResponse rollback)
    {
        if (rollback.Success)
        {
            return message;
        }

        return string.IsNullOrWhiteSpace(message)
            ? rollback.Message
            : $"{message} {rollback.Message}";
    }

    protected static bool PathsEqual(string left, string right)
    {
        string normalizedLeft = Path.GetFullPath(left);
        string normalizedRight = Path.GetFullPath(right);
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    protected static OperationResponse<T> InvokeOnMainThread<T>(
        Func<OperationResponse<T>> work,
        TimeSpan? timeout = null)
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

        if (!signal.Wait(timeout ?? MainThreadTimeout))
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

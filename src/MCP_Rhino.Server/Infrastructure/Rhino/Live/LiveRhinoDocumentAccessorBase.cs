extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public abstract class LiveRhinoDocumentAccessorBase : ILiveRhinoDocumentAccessor
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

    public OperationResponse<T> ExecuteWithUndo<T>(
        string filePath,
        string undoDescription,
        Func<RhinoDoc, OperationResponse<(bool Mutated, T Result)>> work)
    {
        return Execute(filePath, document =>
        {
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

    protected static bool PathsEqual(string left, string right)
    {
        string normalizedLeft = Path.GetFullPath(left);
        string normalizedRight = Path.GetFullPath(right);
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    protected static OperationResponse<T> InvokeOnMainThread<T>(Func<OperationResponse<T>> work)
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

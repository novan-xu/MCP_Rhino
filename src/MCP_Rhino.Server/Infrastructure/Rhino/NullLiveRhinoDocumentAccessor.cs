extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class NullLiveRhinoDocumentAccessor : ILiveRhinoDocumentAccessor
{
    private const string LiveRhinoRequired = "LIVE_RHINO_REQUIRED";

    public OperationResponse<T> Execute<T>(string filePath, Func<RhinoDoc, OperationResponse<T>> work)
    {
        return OperationResponse<T>.Fail(LiveRhinoRequired);
    }

    public OperationResponse<T> ExecuteWithUndo<T>(
        string filePath,
        string undoDescription,
        Func<RhinoDoc, OperationResponse<(bool Mutated, T Result)>> work)
    {
        return OperationResponse<T>.Fail(LiveRhinoRequired);
    }

    public bool TryGetActiveDocumentState(string filePath, out bool hasUnsavedChanges)
    {
        hasUnsavedChanges = false;
        return false;
    }
}

extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveRhinoDocumentAccessor
{
    OperationResponse<T> Execute<T>(string filePath, Func<RhinoDoc, OperationResponse<T>> work);

    OperationResponse<T> ExecuteWithUndo<T>(
        string filePath,
        string undoDescription,
        Func<RhinoDoc, OperationResponse<(bool Mutated, T Result)>> work);

    bool TryGetActiveDocumentState(string filePath, out bool hasUnsavedChanges);
}

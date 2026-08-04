using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveRhinoSelectionOperator
{
    OperationResponse<SelectedObjectsResponse> GetSelectedObjects(string filePath);

    OperationResponse<SelectionMutationResponse> SelectObjects(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        RhinoSelectionMode selectionMode,
        int requestedObjectCount,
        IReadOnlyList<Guid> missingObjectIds);
}

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IObjectEditSpecValidator
{
    OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(
        IReadOnlyList<RhinoObjectEditOperation> operations,
        IReadOnlyList<RhinoObjectInfo> matchedObjects);
}

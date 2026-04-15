using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Rhino.FileIO;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IObjectEditValidator
{
    OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(
        File3dm model,
        IReadOnlyList<RhinoObjectEditOperation> operations,
        IReadOnlyList<RhinoObjectInfo> matchedObjects);
}
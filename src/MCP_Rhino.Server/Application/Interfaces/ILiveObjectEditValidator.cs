extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveObjectEditValidator
{
    OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(
        IReadOnlyList<RhinoObjectEditOperation> operations,
        IReadOnlyList<RhinoObjectInfo> matchedObjects);

    OperationResponse ValidateAgainstDocument(
        RhinoDoc document,
        IReadOnlyList<RhinoObjectEditOperation> operations,
        IReadOnlyList<RhinoObjectInfo> matchedObjects);
}

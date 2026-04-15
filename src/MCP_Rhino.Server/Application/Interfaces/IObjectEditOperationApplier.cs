using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Rhino.FileIO;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IObjectEditOperationApplier
{
    OperationResponse<ObjectEditOperationResult> Apply(
        File3dm model,
        RhinoObjectInfo objectInfo,
        IReadOnlyList<RhinoObjectEditOperation> operations);
}
extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoObjectEditValidator : ILiveObjectEditValidator
{
    public OperationResponse ValidateAgainstDocument(
        RhinoDoc document,
        IReadOnlyList<RhinoObjectEditOperation> operations,
        IReadOnlyList<RhinoObjectInfo> matchedObjects)
    {
        foreach (RhinoObjectInfo matchedObject in matchedObjects)
        {
            if (document.Objects.FindId(matchedObject.ObjectId) is null)
            {
                return OperationResponse.Fail($"Object not found in active document: {matchedObject.ObjectId}");
            }
        }

        foreach (RhinoObjectEditOperation operation in operations)
        {
            if (operation.OperationType == ObjectEditOperationType.SetLayer)
            {
                int layerIndex = document.Layers.FindByFullPath(operation.TargetLayerFullPath!, -1);
                if (layerIndex < 0)
                {
                    return OperationResponse.Fail($"Target layer not found: {operation.TargetLayerFullPath}");
                }
            }
        }

        return OperationResponse.Ok();
    }
}

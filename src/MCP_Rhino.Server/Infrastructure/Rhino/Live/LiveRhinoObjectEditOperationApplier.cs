extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoObjectEditOperationApplier : IObjectEditOperationApplier
{
    public OperationResponse<ObjectEditOperationResult> Apply(
        RhinoDoc document,
        RhinoObjectInfo objectInfo,
        IReadOnlyList<RhinoObjectEditOperation> operations)
    {
        RhinoObject? currentObject = document.Objects.FindId(objectInfo.ObjectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Object not found: {objectInfo.ObjectId}");
        }

        ObjectAttributes attributes = currentObject.Attributes.Duplicate();
        var messages = new List<string>();

        foreach (RhinoObjectEditOperation operation in operations)
        {
            switch (operation.OperationType)
            {
                case ObjectEditOperationType.SetUserText:
                    attributes.SetUserString(operation.Key, operation.Value ?? string.Empty);
                    messages.Add($"SetUserText: {operation.Key}={operation.Value}");
                    break;

                case ObjectEditOperationType.RemoveUserText:
                    attributes.DeleteUserString(operation.Key);
                    messages.Add($"RemoveUserText: {operation.Key}");
                    break;

                case ObjectEditOperationType.SetLayer:
                    int layerIndex = document.Layers.FindByFullPath(operation.TargetLayerFullPath!, -1);
                    if (layerIndex < 0)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail($"Target layer not found: {operation.TargetLayerFullPath}");
                    }

                    attributes.LayerIndex = layerIndex;
                    messages.Add($"SetLayer: {operation.TargetLayerFullPath}");
                    break;

                case ObjectEditOperationType.SetDisplayColor:
                    if (operation.Color is null)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail("Missing color for SetDisplayColor.");
                    }

                    attributes.ObjectColor = operation.Color.ToColor();
                    attributes.ColorSource = ObjectColorSource.ColorFromObject;
                    messages.Add($"SetDisplayColor: ({operation.Color.R},{operation.Color.G},{operation.Color.B})");
                    break;
            }
        }

        if (!document.Objects.ModifyAttributes(objectInfo.ObjectId, attributes, true))
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"ModifyAttributes failed: {objectInfo.ObjectId}");
        }

        string resolvedLayer = operations.LastOrDefault(operation => operation.OperationType == ObjectEditOperationType.SetLayer)?.TargetLayerFullPath
            ?? objectInfo.LayerFullPath;

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = objectInfo.ObjectId,
            LayerFullPath = resolvedLayer,
            Success = true,
            Messages = messages
        });
    }
}

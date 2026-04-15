using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.FileIO;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class RhinoObjectEditOperationApplier : IObjectEditOperationApplier
{
    public OperationResponse<ObjectEditOperationResult> Apply(
        File3dm model,
        RhinoObjectInfo objectInfo,
        IReadOnlyList<RhinoObjectEditOperation> operations)
    {
        File3dmObject? currentObject = FindModelObject(model, objectInfo.ObjectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象不存在: {objectInfo.ObjectId}");
        }

        var attributes = currentObject.Attributes.Duplicate();
        attributes.ObjectId = currentObject.Attributes.ObjectId;
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
                    Layer? targetLayer = model.AllLayers
                        .FirstOrDefault(layer => !layer.IsDeleted
                            && string.Equals(layer.FullPath, operation.TargetLayerFullPath, StringComparison.OrdinalIgnoreCase));

                    if (targetLayer is null)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail($"目标图层不存在: {operation.TargetLayerFullPath}");
                    }

                    attributes.LayerIndex = targetLayer.Index;
                    messages.Add($"SetLayer: {operation.TargetLayerFullPath}");
                    break;

                case ObjectEditOperationType.SetDisplayColor:
                    if (operation.Color is null)
                    {
                        return OperationResponse<ObjectEditOperationResult>.Fail("SetDisplayColor 缺少颜色参数。");
                    }

                    attributes.ObjectColor = operation.Color.ToColor();
                    attributes.ColorSource = ObjectColorSource.ColorFromObject;
                    messages.Add($"SetDisplayColor: ({operation.Color.R},{operation.Color.G},{operation.Color.B})");
                    break;
            }
        }

        global::Rhino.Geometry.GeometryBase? geometry = currentObject.Geometry?.Duplicate();
        if (geometry is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象几何为空，无法更新: {objectInfo.ObjectId}");
        }

        bool deleted = model.Objects.Delete(currentObject.Attributes.ObjectId);
        if (!deleted)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"删除原对象失败: {objectInfo.ObjectId}");
        }

        Guid newObjectId = model.Objects.Add(geometry, attributes);
        if (newObjectId == Guid.Empty)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"重新写入对象失败: {objectInfo.ObjectId}");
        }

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = attributes.ObjectId != Guid.Empty ? attributes.ObjectId : newObjectId,
            LayerFullPath = operations.LastOrDefault(operation => operation.OperationType == ObjectEditOperationType.SetLayer)?.TargetLayerFullPath
                ?? objectInfo.LayerFullPath,
            Success = true,
            Messages = messages
        });
    }

    private static File3dmObject? FindModelObject(File3dm model, Guid objectId)
    {
        foreach (File3dmObject modelObject in model.Objects)
        {
            if (modelObject.Attributes.ObjectId == objectId)
            {
                return modelObject;
            }
        }

        return null;
    }
}
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Rhino.FileIO;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class RhinoObjectEditValidator : IObjectEditValidator
{
    public OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(
        File3dm model,
        IReadOnlyList<RhinoObjectEditOperation> operations,
        IReadOnlyList<RhinoObjectInfo> matchedObjects)
    {
        if (operations.Count == 0)
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("错误：至少需要一个编辑操作。");
        }

        var warnings = new List<ObjectEditWarning>();
        var layerLookup = model.AllLayers
            .Where(layer => !layer.IsDeleted)
            .Select(layer => layer.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (RhinoObjectEditOperation operation in operations)
        {
            switch (operation.OperationType)
            {
                case ObjectEditOperationType.SetUserText:
                    if (string.IsNullOrWhiteSpace(operation.Key))
                    {
                        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("错误：SetUserText 操作要求提供非空 key。");
                    }
                    break;

                case ObjectEditOperationType.RemoveUserText:
                    if (string.IsNullOrWhiteSpace(operation.Key))
                    {
                        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("错误：RemoveUserText 操作要求提供非空 key。");
                    }

                    if (matchedObjects.Count > 0 && matchedObjects.All(obj => obj.GetUserAttributeValue(operation.Key) is null))
                    {
                        warnings.Add(new ObjectEditWarning
                        {
                            Code = "USER_TEXT_NOT_FOUND",
                            Message = $"匹配对象中没有发现 user text key [{operation.Key}]。"
                        });
                    }
                    break;

                case ObjectEditOperationType.SetLayer:
                    if (string.IsNullOrWhiteSpace(operation.TargetLayerFullPath))
                    {
                        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("错误：SetLayer 操作要求提供目标图层 full path。");
                    }

                    if (!layerLookup.Contains(operation.TargetLayerFullPath))
                    {
                        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail($"错误：目标图层不存在 [{operation.TargetLayerFullPath}]。");
                    }
                    break;

                case ObjectEditOperationType.SetDisplayColor:
                    if (operation.Color is null)
                    {
                        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("错误：SetDisplayColor 操作要求提供颜色值。");
                    }

                    if (operation.Color.R is < 0 or > 255
                        || operation.Color.G is < 0 or > 255
                        || operation.Color.B is < 0 or > 255)
                    {
                        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("错误：颜色值必须在 0-255 范围内。");
                    }
                    break;
            }
        }

        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Ok(warnings);
    }
}
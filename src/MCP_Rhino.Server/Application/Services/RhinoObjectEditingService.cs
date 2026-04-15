using System.Drawing;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Rhino.DocObjects;
using Rhino.FileIO;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoObjectEditingService
{
    private const int PreviewLimit = 20;

    private readonly IRhinoDocumentRepository _repository;
    private readonly IObjectEditValidator _validator;
    private readonly IObjectEditOperationApplier _operationApplier;
    private readonly IFileMutationSafeguard _fileMutationSafeguard;
    private readonly IEditResultFormatter _formatter;

    public RhinoObjectEditingService(
        IRhinoDocumentRepository repository,
        IObjectEditValidator validator,
        IObjectEditOperationApplier operationApplier,
        IFileMutationSafeguard fileMutationSafeguard,
        IEditResultFormatter formatter)
    {
        _repository = repository;
        _validator = validator;
        _operationApplier = operationApplier;
        _fileMutationSafeguard = fileMutationSafeguard;
        _formatter = formatter;
    }

    public OperationResponse<ObjectEditPreviewResponse> Preview(
        PreviewObjectEditsRequest request,
        RhinoObjectFilterResult selection)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<ObjectEditPreviewResponse>.Fail($"错误：未找到文件 {request.FilePath}");
        }

        List<RhinoObjectEditOperation> operations = CreateOperations(request.Operations);
        if (operations.Count == 0)
        {
            return OperationResponse<ObjectEditPreviewResponse>.Fail("错误：至少需要提供一个编辑操作。");
        }

        try
        {
            using var model = _repository.Read(request.FilePath);
            OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _validator.Validate(model, operations, selection.Objects);
            if (!validation.Success)
            {
                return OperationResponse<ObjectEditPreviewResponse>.Fail(validation.Message);
            }

            var warnings = validation.Data?.ToList() ?? new List<ObjectEditWarning>();
            if (selection.Objects.Count == 0)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "NO_MATCHED_OBJECTS",
                    Message = "没有匹配对象，预览为空。"
                });
            }

            List<ObjectEditOperationResult> previewResults = selection.Objects
                .Take(PreviewLimit)
                .Select(objectInfo => BuildPreviewResult(model, objectInfo, operations))
                .ToList();

            if (selection.Objects.Count > PreviewLimit)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "PREVIEW_TRUNCATED",
                    Message = $"预览仅展示前 {PreviewLimit} 个对象。"
                });
            }

            var response = new ObjectEditPreviewResponse
            {
                FilePath = request.FilePath,
                CriteriaSummary = selection.CriteriaSummary,
                MatchedObjectCount = selection.MatchedCount,
                PreviewObjectCount = previewResults.Count,
                OperationCount = operations.Count,
                Warnings = warnings,
                ObjectResults = previewResults
            };

            return OperationResponse<ObjectEditPreviewResponse>.Ok(response, "对象编辑预览生成完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<ObjectEditPreviewResponse>.Fail($"对象编辑预览失败: {ex.Message}");
        }
    }

    public OperationResponse<ObjectEditExecutionResponse> Apply(
        ApplyObjectEditsRequest request,
        RhinoObjectFilterResult selection)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail($"错误：未找到文件 {request.FilePath}");
        }

        List<RhinoObjectEditOperation> operations = CreateOperations(request.Operations);
        if (operations.Count == 0)
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail("错误：至少需要提供一个编辑操作。");
        }

        if (selection.Objects.Count == 0)
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail("错误：没有匹配对象，未执行任何修改。");
        }

        try
        {
            using var model = _repository.Read(request.FilePath);
            OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _validator.Validate(model, operations, selection.Objects);
            if (!validation.Success)
            {
                return OperationResponse<ObjectEditExecutionResponse>.Fail(validation.Message);
            }

            OperationResponse<FileMutationPreflightResponse> safeguard = _fileMutationSafeguard.BeforeOverwrite(request.FilePath, selection.Objects.Count);
            if (!safeguard.Success)
            {
                return OperationResponse<ObjectEditExecutionResponse>.Fail(safeguard.Message);
            }

            var warnings = validation.Data?.ToList() ?? new List<ObjectEditWarning>();
            if (safeguard.Data is not null)
            {
                warnings.AddRange(safeguard.Data.Warnings.Select(message => new ObjectEditWarning
                {
                    Code = "FILE_MUTATION_PREFLIGHT",
                    Message = message
                }));
            }

            var operationResults = new List<ObjectEditOperationResult>();
            bool writeSucceeded = false;

            try
            {
                foreach (RhinoObjectInfo objectInfo in selection.Objects)
                {
                    OperationResponse<ObjectEditOperationResult> applyResult = _operationApplier.Apply(model, objectInfo, operations);
                    if (applyResult.Success && applyResult.Data is not null)
                    {
                        operationResults.Add(applyResult.Data);
                    }
                    else
                    {
                        operationResults.Add(new ObjectEditOperationResult
                        {
                            ObjectId = objectInfo.ObjectId,
                            LayerFullPath = objectInfo.LayerFullPath,
                            Success = false,
                            Messages = new[] { applyResult.Message }
                        });
                    }
                }

                writeSucceeded = operationResults.All(result => result.Success) && _repository.Write(model, request.FilePath);
                if (!writeSucceeded)
                {
                    return OperationResponse<ObjectEditExecutionResponse>.Fail("对象编辑写回失败。请检查文件是否可写。" );
                }
            }
            finally
            {
                _fileMutationSafeguard.AfterOverwrite(request.FilePath, writeSucceeded);
            }

            if (operationResults.Count > PreviewLimit)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "RESULT_TRUNCATED",
                    Message = $"执行结果仅展示前 {PreviewLimit} 个对象。"
                });
            }

            var response = new ObjectEditExecutionResponse
            {
                FilePath = request.FilePath,
                CriteriaSummary = selection.CriteriaSummary,
                MatchedObjectCount = selection.MatchedCount,
                UpdatedObjectCount = operationResults.Count(result => result.Success),
                FailedObjectCount = operationResults.Count(result => !result.Success),
                OperationCount = operations.Count,
                Warnings = warnings,
                ObjectResults = operationResults.Take(PreviewLimit).ToList()
            };

            return OperationResponse<ObjectEditExecutionResponse>.Ok(response, "对象编辑执行完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail($"对象编辑执行失败: {ex.Message}");
        }
    }

    public string FormatPreview(ObjectEditPreviewResponse response)
    {
        return _formatter.FormatPreview(response);
    }

    public string FormatExecution(ObjectEditExecutionResponse response)
    {
        return _formatter.FormatExecution(response);
    }

    private static List<RhinoObjectEditOperation> CreateOperations(IEnumerable<ObjectEditOperationRequest> requests)
    {
        return requests.Select(request => new RhinoObjectEditOperation
        {
            OperationType = request.OperationType,
            Key = request.Key,
            Value = request.Value,
            TargetLayerFullPath = request.TargetLayerFullPath,
            Color = request.Color is null
                ? null
                : new RhinoDisplayColor
                {
                    R = request.Color.R,
                    G = request.Color.G,
                    B = request.Color.B
                }
        }).ToList();
    }

    private static ObjectEditOperationResult BuildPreviewResult(
        Rhino.FileIO.File3dm model,
        RhinoObjectInfo objectInfo,
        IReadOnlyList<RhinoObjectEditOperation> operations)
    {
        File3dmObject? modelObject = FindModelObject(model, objectInfo.ObjectId);
        var messages = new List<string>();
        foreach (RhinoObjectEditOperation operation in operations)
        {
            messages.Add(DescribeOperation(modelObject, objectInfo, operation));
        }

        return new ObjectEditOperationResult
        {
            ObjectId = objectInfo.ObjectId,
            LayerFullPath = objectInfo.LayerFullPath,
            Success = modelObject is not null,
            Messages = modelObject is not null
                ? messages
                : new[] { "对象不存在于当前模型中，无法预览。" }
        };
    }

    private static string DescribeOperation(
        File3dmObject? modelObject,
        RhinoObjectInfo objectInfo,
        RhinoObjectEditOperation operation)
    {
        if (modelObject is null)
        {
            return "对象不存在于当前模型中。";
        }

        return operation.OperationType switch
        {
            Domain.Enums.ObjectEditOperationType.SetUserText => DescribeSetUserText(modelObject, operation),
            Domain.Enums.ObjectEditOperationType.RemoveUserText => DescribeRemoveUserText(modelObject, operation),
            Domain.Enums.ObjectEditOperationType.SetLayer => $"SetLayer: [{objectInfo.LayerFullPath}] -> [{operation.TargetLayerFullPath}]",
            Domain.Enums.ObjectEditOperationType.SetDisplayColor => DescribeSetDisplayColor(modelObject, operation.Color),
            _ => $"未知操作: {operation.OperationType}"
        };
    }

    private static string DescribeSetUserText(File3dmObject modelObject, RhinoObjectEditOperation operation)
    {
        string? currentValue = modelObject.Attributes.GetUserString(operation.Key);
        string fromValue = currentValue is null ? "<missing>" : currentValue;
        return $"SetUserText: {operation.Key} [{fromValue}] -> [{operation.Value}]";
    }

    private static string DescribeRemoveUserText(File3dmObject modelObject, RhinoObjectEditOperation operation)
    {
        string? currentValue = modelObject.Attributes.GetUserString(operation.Key);
        string fromValue = currentValue is null ? "<missing>" : currentValue;
        return $"RemoveUserText: {operation.Key} [{fromValue}] -> <removed>";
    }

    private static string DescribeSetDisplayColor(File3dmObject modelObject, RhinoDisplayColor? targetColor)
    {
        Color currentColor = modelObject.Attributes.ObjectColor;
        string current = $"({currentColor.R},{currentColor.G},{currentColor.B})/{modelObject.Attributes.ColorSource}";
        string target = targetColor is null
            ? "<invalid>"
            : $"({targetColor.R},{targetColor.G},{targetColor.B})/ColorFromObject";

        return $"SetDisplayColor: {current} -> {target}";
    }

    private static File3dmObject? FindModelObject(Rhino.FileIO.File3dm model, Guid objectId)
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
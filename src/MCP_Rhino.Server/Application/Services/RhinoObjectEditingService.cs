extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoObjectEditingService
{
    private const int PreviewLimit = 20;

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILiveObjectEditValidator _liveValidator;
    private readonly IObjectEditOperationApplier _operationApplier;
    private readonly IEditResultFormatter _formatter;

    public RhinoObjectEditingService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILiveObjectEditValidator liveValidator,
        IObjectEditOperationApplier operationApplier,
        IEditResultFormatter formatter)
    {
        _documentAccessor = documentAccessor;
        _liveValidator = liveValidator;
        _operationApplier = operationApplier;
        _formatter = formatter;
    }

    public OperationResponse<ObjectEditPreviewResponse> Preview(
        PreviewObjectEditsRequest request,
        RhinoObjectFilterResult selection)
    {
        List<RhinoObjectEditOperation> operations = CreateOperations(request.Operations);
        if (operations.Count == 0)
        {
            return OperationResponse<ObjectEditPreviewResponse>.Fail("At least one edit operation is required.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _liveValidator.Validate(operations, selection.Objects);
            if (!validation.Success)
            {
                return OperationResponse<ObjectEditPreviewResponse>.Fail(validation.Message);
            }

            OperationResponse liveValidation = _liveValidator.ValidateAgainstDocument(document, operations, selection.Objects);
            if (!liveValidation.Success)
            {
                return OperationResponse<ObjectEditPreviewResponse>.Fail(liveValidation.Message);
            }

            var warnings = validation.Data?.ToList() ?? new List<ObjectEditWarning>();
            if (selection.Objects.Count == 0)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "NO_MATCHED_OBJECTS",
                    Message = "No objects matched the selection."
                });
            }

            List<ObjectEditOperationResult> previewResults = selection.Objects
                .Take(PreviewLimit)
                .Select(objectInfo => BuildPreviewResult(document, objectInfo, operations))
                .ToList();

            if (selection.Objects.Count > PreviewLimit)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "PREVIEW_TRUNCATED",
                    Message = $"Preview only shows the first {PreviewLimit} objects."
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

            return OperationResponse<ObjectEditPreviewResponse>.Ok(response, "Object edit preview generated.");
        });
    }

    public OperationResponse<ObjectEditExecutionResponse> Apply(
        ApplyObjectEditsRequest request,
        RhinoObjectFilterResult selection)
    {
        List<RhinoObjectEditOperation> operations = CreateOperations(request.Operations);
        if (operations.Count == 0)
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail("At least one edit operation is required.");
        }

        if (selection.Objects.Count == 0)
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail("No objects matched the selection.");
        }

        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: ApplyObjectEdits", document =>
        {
            OperationResponse<IReadOnlyList<ObjectEditWarning>> validation = _liveValidator.Validate(operations, selection.Objects);
            if (!validation.Success)
            {
                return OperationResponse<(bool Mutated, ObjectEditExecutionResponse Result)>.Fail(validation.Message);
            }

            OperationResponse liveValidation = _liveValidator.ValidateAgainstDocument(document, operations, selection.Objects);
            if (!liveValidation.Success)
            {
                return OperationResponse<(bool Mutated, ObjectEditExecutionResponse Result)>.Fail(liveValidation.Message);
            }

            var warnings = validation.Data?.ToList() ?? new List<ObjectEditWarning>();
            var operationResults = new List<ObjectEditOperationResult>();

            foreach (RhinoObjectInfo objectInfo in selection.Objects)
            {
                OperationResponse<ObjectEditOperationResult> applyResult = _operationApplier.Apply(objectInfo, operations);
                operationResults.Add(ToOperationResult(objectInfo, applyResult));
            }

            if (operationResults.Any(result => result.Success))
            {
                document.Views.Redraw();
            }

            if (operationResults.Count > PreviewLimit)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "RESULT_TRUNCATED",
                    Message = $"Result list only shows the first {PreviewLimit} objects."
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

            return OperationResponse<(bool Mutated, ObjectEditExecutionResponse Result)>.Ok(
                (operationResults.Any(result => result.Success), response),
                "Object edits completed.");
        });
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
        RhinoDoc document,
        RhinoObjectInfo objectInfo,
        IReadOnlyList<RhinoObjectEditOperation> operations)
    {
        RhinoObject? currentObject = document.Objects.FindId(objectInfo.ObjectId);
        var messages = new List<string>();
        foreach (RhinoObjectEditOperation operation in operations)
        {
            messages.Add(DescribeOperation(currentObject, objectInfo, operation));
        }

        return new ObjectEditOperationResult
        {
            ObjectId = objectInfo.ObjectId,
            LayerFullPath = objectInfo.LayerFullPath,
            Success = currentObject is not null,
            Messages = currentObject is not null
                ? messages
                : new[] { "Object was not found in the active document." }
        };
    }

    private static string DescribeOperation(
        RhinoObject? currentObject,
        RhinoObjectInfo objectInfo,
        RhinoObjectEditOperation operation)
    {
        if (currentObject is null)
        {
            return "Object was not found in the active document.";
        }

        return operation.OperationType switch
        {
            Domain.Enums.ObjectEditOperationType.SetUserText => DescribeSetUserText(currentObject, operation),
            Domain.Enums.ObjectEditOperationType.RemoveUserText => DescribeRemoveUserText(currentObject, operation),
            Domain.Enums.ObjectEditOperationType.SetLayer => $"SetLayer: [{objectInfo.LayerFullPath}] -> [{operation.TargetLayerFullPath}]",
            Domain.Enums.ObjectEditOperationType.SetDisplayColor => DescribeSetDisplayColor(currentObject, operation.Color),
            _ => $"Unknown operation: {operation.OperationType}"
        };
    }

    private static string DescribeSetUserText(RhinoObject currentObject, RhinoObjectEditOperation operation)
    {
        string? currentValue = currentObject.Attributes.GetUserString(operation.Key);
        string fromValue = currentValue is null ? "<missing>" : currentValue;
        return $"SetUserText: {operation.Key} [{fromValue}] -> [{operation.Value}]";
    }

    private static string DescribeRemoveUserText(RhinoObject currentObject, RhinoObjectEditOperation operation)
    {
        string? currentValue = currentObject.Attributes.GetUserString(operation.Key);
        string fromValue = currentValue is null ? "<missing>" : currentValue;
        return $"RemoveUserText: {operation.Key} [{fromValue}] -> <removed>";
    }

    private static string DescribeSetDisplayColor(RhinoObject currentObject, RhinoDisplayColor? targetColor)
    {
        Color currentColor = currentObject.Attributes.ObjectColor;
        string current = $"({currentColor.R},{currentColor.G},{currentColor.B})/{currentObject.Attributes.ColorSource}";
        string target = targetColor is null
            ? "<invalid>"
            : $"({targetColor.R},{targetColor.G},{targetColor.B})/ColorFromObject";

        return $"SetDisplayColor: {current} -> {target}";
    }

    private static ObjectEditOperationResult ToOperationResult(
        RhinoObjectInfo objectInfo,
        OperationResponse<ObjectEditOperationResult> result)
    {
        if (result.Success && result.Data is not null)
        {
            return result.Data;
        }

        return new ObjectEditOperationResult
        {
            ObjectId = objectInfo.ObjectId,
            LayerFullPath = objectInfo.LayerFullPath,
            Success = false,
            Messages = new[] { result.Message }
        };
    }
}

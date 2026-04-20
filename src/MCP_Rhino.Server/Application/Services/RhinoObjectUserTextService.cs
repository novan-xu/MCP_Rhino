using System.Text;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using Rhino.FileIO;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoObjectUserTextService
{
    private const int PreviewLimit = 20;

    private readonly IRhinoDocumentRepository _repository;
    private readonly IFileMutationSafeguard _fileMutationSafeguard;
    private readonly IEditResultFormatter _formatter;

    public RhinoObjectUserTextService(
        IRhinoDocumentRepository repository,
        IFileMutationSafeguard fileMutationSafeguard,
        IEditResultFormatter formatter)
    {
        _repository = repository;
        _fileMutationSafeguard = fileMutationSafeguard;
        _formatter = formatter;
    }

    public OperationResponse<ObjectEditPreviewResponse> Preview(ObjectUserTextBatchWriteRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<ObjectEditPreviewResponse>.Fail($"错误：未找到文件 {request.FilePath}");
        }

        try
        {
            using var model = _repository.Read(request.FilePath);
            OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>> validation = ValidateEntries(model, request.Entries);
            if (!validation.Success || validation.Data is null)
            {
                return OperationResponse<ObjectEditPreviewResponse>.Fail(validation.Message);
            }

            Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>> entriesByObjectId = validation.Data;
            List<ObjectEditOperationResult> previewResults = entriesByObjectId
                .Take(PreviewLimit)
                .Select(entryGroup => BuildPreviewResult(model, entryGroup.Key, entryGroup.Value))
                .ToList();

            var warnings = new List<ObjectEditWarning>();
            if (entriesByObjectId.Count > PreviewLimit)
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
                CriteriaSummary = SummarizeEntries(request.Entries, entriesByObjectId.Count),
                MatchedObjectCount = entriesByObjectId.Count,
                PreviewObjectCount = previewResults.Count,
                OperationCount = request.Entries.Count,
                Warnings = warnings,
                ObjectResults = previewResults
            };

            return OperationResponse<ObjectEditPreviewResponse>.Ok(response, "对象级 user text 写入预览生成完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<ObjectEditPreviewResponse>.Fail($"对象级 user text 写入预览失败: {ex.Message}");
        }
    }

    public OperationResponse<ObjectEditExecutionResponse> Apply(ObjectUserTextBatchWriteRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail($"错误：未找到文件 {request.FilePath}");
        }

        try
        {
            using var model = _repository.Read(request.FilePath);
            OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>> validation = ValidateEntries(model, request.Entries);
            if (!validation.Success || validation.Data is null)
            {
                return OperationResponse<ObjectEditExecutionResponse>.Fail(validation.Message);
            }

            Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>> entriesByObjectId = validation.Data;
            OperationResponse<FileMutationPreflightResponse> safeguard = _fileMutationSafeguard.BeforeOverwrite(request.FilePath, entriesByObjectId.Count);
            if (!safeguard.Success)
            {
                return OperationResponse<ObjectEditExecutionResponse>.Fail(safeguard.Message);
            }

            var warnings = new List<ObjectEditWarning>();
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
                foreach ((Guid objectId, List<ObjectScopedUserTextEntryRequest> objectEntries) in entriesByObjectId)
                {
                    OperationResponse<ObjectEditOperationResult> applyResult = ApplyEntriesToObject(model, objectId, objectEntries);
                    if (applyResult.Success && applyResult.Data is not null)
                    {
                        operationResults.Add(applyResult.Data);
                    }
                    else
                    {
                        operationResults.Add(new ObjectEditOperationResult
                        {
                            ObjectId = objectId,
                            LayerFullPath = "Unknown",
                            Success = false,
                            Messages = new[] { applyResult.Message }
                        });
                    }
                }

                writeSucceeded = operationResults.All(result => result.Success) && _repository.Write(model, request.FilePath);
                if (!writeSucceeded)
                {
                    return OperationResponse<ObjectEditExecutionResponse>.Fail("对象级 user text 写回失败。请检查文件是否可写。");
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
                CriteriaSummary = SummarizeEntries(request.Entries, entriesByObjectId.Count),
                MatchedObjectCount = entriesByObjectId.Count,
                UpdatedObjectCount = operationResults.Count(result => result.Success),
                FailedObjectCount = operationResults.Count(result => !result.Success),
                OperationCount = request.Entries.Count,
                Warnings = warnings,
                ObjectResults = operationResults.Take(PreviewLimit).ToList()
            };

            return OperationResponse<ObjectEditExecutionResponse>.Ok(response, "对象级 user text 写入执行完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail($"对象级 user text 写入执行失败: {ex.Message}");
        }
    }

    public OperationResponse<ObjectUserTextReadResponse> Read(ObjectUserTextReadRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<ObjectUserTextReadResponse>.Fail($"错误：未找到文件 {request.FilePath}");
        }

        if (request.ObjectIds.Count == 0)
        {
            return OperationResponse<ObjectUserTextReadResponse>.Fail("错误：至少需要提供一个 ObjectId。");
        }

        try
        {
            using var model = _repository.Read(request.FilePath);
            var distinctObjectIds = request.ObjectIds
                .Where(objectId => objectId != Guid.Empty)
                .Distinct()
                .ToList();

            if (distinctObjectIds.Count == 0)
            {
                return OperationResponse<ObjectUserTextReadResponse>.Fail("错误：所有 ObjectId 均为空 GUID。");
            }

            var records = new List<ObjectUserTextRecordResponse>(distinctObjectIds.Count);
            int totalEntries = 0;
            foreach (Guid objectId in distinctObjectIds)
            {
                File3dmObject? modelObject = FindModelObject(model, objectId);
                if (modelObject is null)
                {
                    records.Add(new ObjectUserTextRecordResponse
                    {
                        ObjectId = objectId,
                        LayerFullPath = "Unknown",
                        ObjectName = string.Empty,
                        Found = false,
                        Message = "对象不存在于当前模型中。",
                        Entries = Array.Empty<ObjectUserTextEntryResponse>()
                    });
                    continue;
                }

                var entries = ReadObjectUserStrings(modelObject);
                totalEntries += entries.Count;
                records.Add(new ObjectUserTextRecordResponse
                {
                    ObjectId = objectId,
                    LayerFullPath = ResolveLayerFullPath(model, modelObject.Attributes.LayerIndex),
                    ObjectName = modelObject.Attributes.Name ?? string.Empty,
                    Found = true,
                    Message = $"读取到 {entries.Count} 条 user string。",
                    Entries = entries
                });
            }

            var response = new ObjectUserTextReadResponse
            {
                FilePath = request.FilePath,
                RequestedObjectCount = distinctObjectIds.Count,
                FoundObjectCount = records.Count(record => record.Found),
                MissingObjectCount = records.Count(record => !record.Found),
                TotalEntryCount = totalEntries,
                Records = records
            };

            return OperationResponse<ObjectUserTextReadResponse>.Ok(response, "对象级 user string 读取完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<ObjectUserTextReadResponse>.Fail($"对象级 user string 读取失败: {ex.Message}");
        }
    }

    public OperationResponse<ObjectEditExecutionResponse> Delete(ObjectUserTextDeleteRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail($"错误：未找到文件 {request.FilePath}");
        }

        try
        {
            using var model = _repository.Read(request.FilePath);
            OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>> validation = ValidateDeleteEntries(model, request.Entries);
            if (!validation.Success || validation.Data is null)
            {
                return OperationResponse<ObjectEditExecutionResponse>.Fail(validation.Message);
            }

            Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>> entriesByObjectId = validation.Data;
            OperationResponse<FileMutationPreflightResponse> safeguard = _fileMutationSafeguard.BeforeOverwrite(request.FilePath, entriesByObjectId.Count);
            if (!safeguard.Success)
            {
                return OperationResponse<ObjectEditExecutionResponse>.Fail(safeguard.Message);
            }

            var warnings = new List<ObjectEditWarning>();
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
                foreach ((Guid objectId, List<ObjectScopedUserTextKeyRequest> objectEntries) in entriesByObjectId)
                {
                    OperationResponse<ObjectEditOperationResult> applyResult = DeleteEntriesFromObject(model, objectId, objectEntries);
                    if (applyResult.Success && applyResult.Data is not null)
                    {
                        operationResults.Add(applyResult.Data);
                    }
                    else
                    {
                        operationResults.Add(new ObjectEditOperationResult
                        {
                            ObjectId = objectId,
                            LayerFullPath = "Unknown",
                            Success = false,
                            Messages = new[] { applyResult.Message }
                        });
                    }
                }

                writeSucceeded = operationResults.All(result => result.Success) && _repository.Write(model, request.FilePath);
                if (!writeSucceeded)
                {
                    return OperationResponse<ObjectEditExecutionResponse>.Fail("对象级 user text 删除写回失败。请检查文件是否可写。");
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
                CriteriaSummary = SummarizeDeleteEntries(request.Entries, entriesByObjectId.Count),
                MatchedObjectCount = entriesByObjectId.Count,
                UpdatedObjectCount = operationResults.Count(result => result.Success),
                FailedObjectCount = operationResults.Count(result => !result.Success),
                OperationCount = request.Entries.Count,
                Warnings = warnings,
                ObjectResults = operationResults.Take(PreviewLimit).ToList()
            };

            return OperationResponse<ObjectEditExecutionResponse>.Ok(response, "对象级 user text 删除完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail($"对象级 user text 删除失败: {ex.Message}");
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

    public string FormatRead(ObjectUserTextReadResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Object User Strings");
        builder.AppendLine($"- 文件: {response.FilePath}");
        builder.AppendLine($"- 请求对象数: {response.RequestedObjectCount}");
        builder.AppendLine($"- 命中对象数: {response.FoundObjectCount}");
        builder.AppendLine($"- 缺失对象数: {response.MissingObjectCount}");
        builder.AppendLine($"- user string 总数: {response.TotalEntryCount}");

        if (response.Records.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("没有对象记录可展示。");
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine("Records:");
        foreach (ObjectUserTextRecordResponse record in response.Records.Take(PreviewLimit))
        {
            builder.AppendLine($"- {record.ObjectId} | Found={record.Found} | Layer={record.LayerFullPath} | Name={record.ObjectName}");
            builder.AppendLine($"  - {record.Message}");
            foreach (ObjectUserTextEntryResponse entry in record.Entries)
            {
                builder.AppendLine($"  - {entry.Key} = {entry.Value}");
            }
        }

        if (response.Records.Count > PreviewLimit)
        {
            builder.AppendLine($"... 仅展示前 {PreviewLimit} 个对象。");
        }

        return builder.ToString();
    }

    private static OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>> ValidateEntries(
        File3dm model,
        IReadOnlyList<ObjectScopedUserTextEntryRequest> entries)
    {
        if (entries.Count == 0)
        {
            return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Fail("错误：至少需要提供一个对象级 user text 写入项。");
        }

        var duplicateLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ObjectScopedUserTextEntryRequest entry in entries)
        {
            if (entry.ObjectId == Guid.Empty)
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Fail("错误：存在空 ObjectId。\n请为每条 user text 写入项提供有效对象 GUID。");
            }

            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Fail($"错误：对象 [{entry.ObjectId}] 的 user text key 不能为空。");
            }

            string duplicateKey = $"{entry.ObjectId:N}|{entry.Key}";
            if (!duplicateLookup.Add(duplicateKey))
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Fail($"错误：对象 [{entry.ObjectId}] 的 user text key [{entry.Key}] 被重复指定。\n请确保同一对象的同一个 key 仅出现一次。");
            }
        }

        var modelObjectIds = model.Objects
            .Select(modelObject => modelObject.Attributes.ObjectId)
            .ToHashSet();

        Guid? missingObjectId = entries
            .Select(entry => entry.ObjectId)
            .Distinct()
            .FirstOrDefault(objectId => !modelObjectIds.Contains(objectId));

        if (missingObjectId.HasValue && missingObjectId.Value != Guid.Empty)
        {
            return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Fail($"错误：文件中不存在对象 [{missingObjectId.Value}]。");
        }

        Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>> groupedEntries = entries
            .GroupBy(entry => entry.ObjectId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Ok(groupedEntries);
    }

    private static ObjectEditOperationResult BuildPreviewResult(
        File3dm model,
        Guid objectId,
        IReadOnlyList<ObjectScopedUserTextEntryRequest> entries)
    {
        File3dmObject? modelObject = FindModelObject(model, objectId);
        if (modelObject is null)
        {
            return new ObjectEditOperationResult
            {
                ObjectId = objectId,
                LayerFullPath = "Unknown",
                Success = false,
                Messages = new[] { "对象不存在于当前模型中，无法预览。" }
            };
        }

        return new ObjectEditOperationResult
        {
            ObjectId = objectId,
            LayerFullPath = ResolveLayerFullPath(model, modelObject.Attributes.LayerIndex),
            Success = true,
            Messages = entries.Select(entry => DescribeEntry(modelObject, entry)).ToList()
        };
    }

    private static OperationResponse<ObjectEditOperationResult> ApplyEntriesToObject(
        File3dm model,
        Guid objectId,
        IReadOnlyList<ObjectScopedUserTextEntryRequest> entries)
    {
        File3dmObject? currentObject = FindModelObject(model, objectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象不存在: {objectId}");
        }

        var attributes = currentObject.Attributes.Duplicate();
        attributes.ObjectId = currentObject.Attributes.ObjectId;
        var messages = new List<string>();

        foreach (ObjectScopedUserTextEntryRequest entry in entries)
        {
            attributes.SetUserString(entry.Key, entry.Value ?? string.Empty);
            messages.Add($"SetUserText: {entry.Key}={entry.Value}");
        }

        global::Rhino.Geometry.GeometryBase? geometry = currentObject.Geometry?.Duplicate();
        if (geometry is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象几何为空，无法更新: {objectId}");
        }

        bool deleted = model.Objects.Delete(currentObject.Attributes.ObjectId);
        if (!deleted)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"删除原对象失败: {objectId}");
        }

        Guid newObjectId = model.Objects.Add(geometry, attributes);
        if (newObjectId == Guid.Empty)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"重新写入对象失败: {objectId}");
        }

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = attributes.ObjectId != Guid.Empty ? attributes.ObjectId : newObjectId,
            LayerFullPath = ResolveLayerFullPath(model, attributes.LayerIndex),
            Success = true,
            Messages = messages
        });
    }

    private static string DescribeEntry(File3dmObject modelObject, ObjectScopedUserTextEntryRequest entry)
    {
        string? currentValue = modelObject.Attributes.GetUserString(entry.Key);
        string fromValue = currentValue is null ? "<missing>" : currentValue;
        return $"SetUserText: {entry.Key} [{fromValue}] -> [{entry.Value}]";
    }

    private static string SummarizeEntries(IReadOnlyList<ObjectScopedUserTextEntryRequest> entries, int objectCount)
    {
        return $"ObjectScopedUserTextEntries={entries.Count}; Objects={objectCount}";
    }

    private static string ResolveLayerFullPath(File3dm model, int layerIndex)
    {
        return model.AllLayers
            .FirstOrDefault(layer => !layer.IsDeleted && layer.Index == layerIndex)?.FullPath
            ?? "Unknown";
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

    private static List<ObjectUserTextEntryResponse> ReadObjectUserStrings(File3dmObject modelObject)
    {
        var entries = new List<ObjectUserTextEntryResponse>();
        var userStrings = modelObject.Attributes.GetUserStrings();
        if (userStrings is null)
        {
            return entries;
        }

        foreach (string? key in userStrings.AllKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            entries.Add(new ObjectUserTextEntryResponse
            {
                Key = key,
                Value = userStrings[key] ?? string.Empty
            });
        }

        return entries;
    }

    private static OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>> ValidateDeleteEntries(
        File3dm model,
        IReadOnlyList<ObjectScopedUserTextKeyRequest> entries)
    {
        if (entries.Count == 0)
        {
            return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Fail("错误：至少需要提供一个对象级 user text 删除项。");
        }

        var duplicateLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ObjectScopedUserTextKeyRequest entry in entries)
        {
            if (entry.ObjectId == Guid.Empty)
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Fail("错误：存在空 ObjectId。\n请为每条 user text 删除项提供有效对象 GUID。");
            }

            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Fail($"错误：对象 [{entry.ObjectId}] 的 user text key 不能为空。");
            }

            string duplicateKey = $"{entry.ObjectId:N}|{entry.Key}";
            if (!duplicateLookup.Add(duplicateKey))
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Fail($"错误：对象 [{entry.ObjectId}] 的 user text key [{entry.Key}] 被重复指定。\n请确保同一对象的同一个 key 仅出现一次。");
            }
        }

        var modelObjectIds = model.Objects
            .Select(modelObject => modelObject.Attributes.ObjectId)
            .ToHashSet();

        Guid? missingObjectId = entries
            .Select(entry => entry.ObjectId)
            .Distinct()
            .FirstOrDefault(objectId => !modelObjectIds.Contains(objectId));

        if (missingObjectId.HasValue && missingObjectId.Value != Guid.Empty)
        {
            return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Fail($"错误：文件中不存在对象 [{missingObjectId.Value}]。");
        }

        Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>> grouped = entries
            .GroupBy(entry => entry.ObjectId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Ok(grouped);
    }

    private static OperationResponse<ObjectEditOperationResult> DeleteEntriesFromObject(
        File3dm model,
        Guid objectId,
        IReadOnlyList<ObjectScopedUserTextKeyRequest> entries)
    {
        File3dmObject? currentObject = FindModelObject(model, objectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象不存在: {objectId}");
        }

        var attributes = currentObject.Attributes.Duplicate();
        attributes.ObjectId = currentObject.Attributes.ObjectId;
        var messages = new List<string>();

        foreach (ObjectScopedUserTextKeyRequest entry in entries)
        {
            string? currentValue = attributes.GetUserString(entry.Key);
            attributes.DeleteUserString(entry.Key);
            string fromValue = currentValue is null ? "<missing>" : currentValue;
            messages.Add($"DeleteUserText: {entry.Key} [{fromValue}] -> <removed>");
        }

        global::Rhino.Geometry.GeometryBase? geometry = currentObject.Geometry?.Duplicate();
        if (geometry is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"对象几何为空，无法更新: {objectId}");
        }

        bool deleted = model.Objects.Delete(currentObject.Attributes.ObjectId);
        if (!deleted)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"删除原对象失败: {objectId}");
        }

        Guid newObjectId = model.Objects.Add(geometry, attributes);
        if (newObjectId == Guid.Empty)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"重新写入对象失败: {objectId}");
        }

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = attributes.ObjectId != Guid.Empty ? attributes.ObjectId : newObjectId,
            LayerFullPath = ResolveLayerFullPath(model, attributes.LayerIndex),
            Success = true,
            Messages = messages
        });
    }

    private static string SummarizeDeleteEntries(IReadOnlyList<ObjectScopedUserTextKeyRequest> entries, int objectCount)
    {
        return $"ObjectScopedUserTextDeleteEntries={entries.Count}; Objects={objectCount}";
    }
}
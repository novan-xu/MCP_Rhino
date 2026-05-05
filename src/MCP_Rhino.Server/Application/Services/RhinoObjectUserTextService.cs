extern alias rhinocommon;

using System.Text;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoObjectUserTextService
{
    private const int PreviewLimit = 20;

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly IEditResultFormatter _formatter;

    public RhinoObjectUserTextService(
        ILiveRhinoDocumentAccessor documentAccessor,
        IEditResultFormatter formatter)
    {
        _documentAccessor = documentAccessor;
        _formatter = formatter;
    }

    // Live variant: validates and previews against the currently-open RhinoDoc
    // so the "current value" shown in each preview line reflects unsaved edits.
    // Fails with LIVE_RHINO_REQUIRED when the file is not open in a running Rhino.
    public OperationResponse<ObjectEditPreviewResponse> PreviewInLive(ObjectUserTextBatchWriteRequest request)
    {
        return _documentAccessor.Execute(request.FilePath, document =>
        {
            OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>> validation = ValidateEntries(document, request.Entries);
            if (!validation.Success || validation.Data is null)
            {
                return OperationResponse<ObjectEditPreviewResponse>.Fail(validation.Message);
            }

            Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>> entriesByObjectId = validation.Data;
            List<ObjectEditOperationResult> previewResults = entriesByObjectId
                .Take(PreviewLimit)
                .Select(entryGroup => BuildLivePreviewResult(document, entryGroup.Key, entryGroup.Value))
                .ToList();

            var warnings = new List<ObjectEditWarning>();
            if (entriesByObjectId.Count > PreviewLimit)
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
                CriteriaSummary = SummarizeEntries(request.Entries, entriesByObjectId.Count),
                MatchedObjectCount = entriesByObjectId.Count,
                PreviewObjectCount = previewResults.Count,
                OperationCount = request.Entries.Count,
                Warnings = warnings,
                ObjectResults = previewResults
            };

            return OperationResponse<ObjectEditPreviewResponse>.Ok(response, "Object user text preview generated from live document.");
        });
    }

    // Resolves each object from RhinoDoc.Objects.
    public OperationResponse<ObjectUserTextReadResponse> ReadInLive(ObjectUserTextReadRequest request)
    {
        if (request.ObjectIds.Count == 0)
        {
            return OperationResponse<ObjectUserTextReadResponse>.Fail("At least one ObjectId is required.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var distinctObjectIds = request.ObjectIds
                .Where(objectId => objectId != Guid.Empty)
                .Distinct()
                .ToList();

            if (distinctObjectIds.Count == 0)
            {
                return OperationResponse<ObjectUserTextReadResponse>.Fail("All requested ObjectIds were empty GUID values.");
            }

            var records = new List<ObjectUserTextRecordResponse>(distinctObjectIds.Count);
            int totalEntries = 0;
            foreach (Guid objectId in distinctObjectIds)
            {
                RhinoObject? rhinoObject = document.Objects.FindId(objectId);
                if (rhinoObject is null)
                {
                    records.Add(new ObjectUserTextRecordResponse
                    {
                        ObjectId = objectId,
                        LayerFullPath = "Unknown",
                        ObjectName = string.Empty,
                        Found = false,
                        Message = "Object was not found in the live document.",
                        Entries = Array.Empty<ObjectUserTextEntryResponse>()
                    });
                    continue;
                }

                var entries = ReadLiveObjectUserStrings(rhinoObject);
                totalEntries += entries.Count;
                records.Add(new ObjectUserTextRecordResponse
                {
                    ObjectId = objectId,
                    LayerFullPath = ResolveLayerFullPath(document, rhinoObject.Attributes.LayerIndex),
                    ObjectName = rhinoObject.Attributes.Name ?? string.Empty,
                    Found = true,
                    Message = $"Read {entries.Count} user string entries.",
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
                Warnings = Array.Empty<ObjectEditWarning>(),
                Records = records
            };

            return OperationResponse<ObjectUserTextReadResponse>.Ok(response, "Object user strings read from live document.");
        });
    }

    public OperationResponse<ObjectEditPreviewResponse> Preview(ObjectUserTextBatchWriteRequest request)
    {
        return PreviewInLive(request);
    }

    public OperationResponse<ObjectEditExecutionResponse> Apply(ObjectUserTextBatchWriteRequest request)
    {
        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: ApplyObjectUserTextWrites", document =>
        {
            OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>> validation = ValidateEntries(document, request.Entries);
            if (!validation.Success || validation.Data is null)
            {
                return OperationResponse<(bool Mutated, ObjectEditExecutionResponse Result)>.Fail(validation.Message);
            }

            Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>> entriesByObjectId = validation.Data;
            var operationResults = new List<ObjectEditOperationResult>(entriesByObjectId.Count);

            foreach ((Guid objectId, List<ObjectScopedUserTextEntryRequest> objectEntries) in entriesByObjectId)
            {
                OperationResponse<ObjectEditOperationResult> applyResult = ApplyEntriesToObject(document, objectId, objectEntries);
                operationResults.Add(ToOperationResult(document, objectId, applyResult));
            }

            if (operationResults.Any(result => result.Success))
            {
                document.Views.Redraw();
            }

            var warnings = new List<ObjectEditWarning>();
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
                CriteriaSummary = SummarizeEntries(request.Entries, entriesByObjectId.Count),
                MatchedObjectCount = entriesByObjectId.Count,
                UpdatedObjectCount = operationResults.Count(result => result.Success),
                FailedObjectCount = operationResults.Count(result => !result.Success),
                OperationCount = request.Entries.Count,
                Warnings = warnings,
                ObjectResults = operationResults.Take(PreviewLimit).ToList()
            };

            return OperationResponse<(bool Mutated, ObjectEditExecutionResponse Result)>.Ok(
                (operationResults.Any(result => result.Success), response),
                "Object user text write completed.");
        });
    }

    public OperationResponse<ObjectUserTextReadResponse> Read(ObjectUserTextReadRequest request)
    {
        return ReadInLive(request);
    }

    public OperationResponse<ObjectEditExecutionResponse> Delete(ObjectUserTextDeleteRequest request)
    {
        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: DeleteObjectUserText", document =>
        {
            OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>> validation = ValidateDeleteEntries(document, request.Entries);
            if (!validation.Success || validation.Data is null)
            {
                return OperationResponse<(bool Mutated, ObjectEditExecutionResponse Result)>.Fail(validation.Message);
            }

            Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>> entriesByObjectId = validation.Data;
            var operationResults = new List<ObjectEditOperationResult>(entriesByObjectId.Count);

            foreach ((Guid objectId, List<ObjectScopedUserTextKeyRequest> objectEntries) in entriesByObjectId)
            {
                OperationResponse<ObjectEditOperationResult> applyResult = DeleteEntriesFromObject(document, objectId, objectEntries);
                operationResults.Add(ToOperationResult(document, objectId, applyResult));
            }

            if (operationResults.Any(result => result.Success))
            {
                document.Views.Redraw();
            }

            var warnings = new List<ObjectEditWarning>();
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
                CriteriaSummary = SummarizeDeleteEntries(request.Entries, entriesByObjectId.Count),
                MatchedObjectCount = entriesByObjectId.Count,
                UpdatedObjectCount = operationResults.Count(result => result.Success),
                FailedObjectCount = operationResults.Count(result => !result.Success),
                OperationCount = request.Entries.Count,
                Warnings = warnings,
                ObjectResults = operationResults.Take(PreviewLimit).ToList()
            };

            return OperationResponse<(bool Mutated, ObjectEditExecutionResponse Result)>.Ok(
                (operationResults.Any(result => result.Success), response),
                "Object user text delete completed.");
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

    public string FormatRead(ObjectUserTextReadResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Object User Strings");
        builder.AppendLine($"- File: {response.FilePath}");
        builder.AppendLine($"- Requested objects: {response.RequestedObjectCount}");
        builder.AppendLine($"- Found objects: {response.FoundObjectCount}");
        builder.AppendLine($"- Missing objects: {response.MissingObjectCount}");
        builder.AppendLine($"- Total user strings: {response.TotalEntryCount}");

        if (response.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Warnings:");
            foreach (ObjectEditWarning warning in response.Warnings)
            {
                builder.AppendLine($"- [{warning.Code}] {warning.Message}");
            }
        }

        if (response.Records.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("No object records to display.");
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
            builder.AppendLine($"... only the first {PreviewLimit} objects are shown.");
        }

        return builder.ToString();
    }

    private static OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>> ValidateEntries(
        RhinoDoc document,
        IReadOnlyList<ObjectScopedUserTextEntryRequest> entries)
    {
        return ValidateEntries(entries, objectId => document.Objects.FindId(objectId) is not null);
    }

    private static OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>> ValidateEntries(
        IReadOnlyList<ObjectScopedUserTextEntryRequest> entries,
        Func<Guid, bool> objectExists)
    {
        if (entries.Count == 0)
        {
            return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Fail("At least one object-scoped user text entry is required.");
        }

        var duplicateLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ObjectScopedUserTextEntryRequest entry in entries)
        {
            if (entry.ObjectId == Guid.Empty)
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Fail("ObjectId cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Fail($"User text key cannot be empty for object [{entry.ObjectId}].");
            }

            string duplicateKey = $"{entry.ObjectId:N}|{entry.Key}";
            if (!duplicateLookup.Add(duplicateKey))
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Fail(
                    $"Duplicate user text key [{entry.Key}] was provided for object [{entry.ObjectId}].");
            }
        }

        Guid? missingObjectId = entries
            .Select(entry => entry.ObjectId)
            .Distinct()
            .FirstOrDefault(objectId => !objectExists(objectId));

        if (missingObjectId.HasValue && missingObjectId.Value != Guid.Empty)
        {
            return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Fail($"Object was not found: {missingObjectId.Value}");
        }

        return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextEntryRequest>>>.Ok(
            entries.GroupBy(entry => entry.ObjectId).ToDictionary(group => group.Key, group => group.ToList()));
    }

    private static ObjectEditOperationResult BuildLivePreviewResult(
        RhinoDoc document,
        Guid objectId,
        IReadOnlyList<ObjectScopedUserTextEntryRequest> entries)
    {
        RhinoObject? rhinoObject = document.Objects.FindId(objectId);
        if (rhinoObject is null)
        {
            return new ObjectEditOperationResult
            {
                ObjectId = objectId,
                LayerFullPath = "Unknown",
                Success = false,
                Messages = new[] { "Object was not found in the live document." }
            };
        }

        return new ObjectEditOperationResult
        {
            ObjectId = objectId,
            LayerFullPath = ResolveLayerFullPath(document, rhinoObject.Attributes.LayerIndex),
            Success = true,
            Messages = entries.Select(entry => DescribeLiveEntry(rhinoObject, entry)).ToList()
        };
    }

    private static string DescribeLiveEntry(RhinoObject rhinoObject, ObjectScopedUserTextEntryRequest entry)
    {
        string? currentValue = rhinoObject.Attributes.GetUserString(entry.Key);
        string fromValue = currentValue is null ? "<missing>" : currentValue;
        return $"SetUserText: {entry.Key} [{fromValue}] -> [{entry.Value}]";
    }

    private static List<ObjectUserTextEntryResponse> ReadLiveObjectUserStrings(RhinoObject rhinoObject)
    {
        var entries = new List<ObjectUserTextEntryResponse>();
        var userStrings = rhinoObject.Attributes.GetUserStrings();
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

    private static OperationResponse<ObjectEditOperationResult> ApplyEntriesToObject(
        RhinoDoc document,
        Guid objectId,
        IReadOnlyList<ObjectScopedUserTextEntryRequest> entries)
    {
        RhinoObject? currentObject = document.Objects.FindId(objectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Object was not found: {objectId}");
        }

        ObjectAttributes attributes = currentObject.Attributes.Duplicate();
        var messages = new List<string>();

        foreach (ObjectScopedUserTextEntryRequest entry in entries)
        {
            attributes.SetUserString(entry.Key, entry.Value ?? string.Empty);
            messages.Add($"SetUserText: {entry.Key}={entry.Value}");
        }

        if (!document.Objects.ModifyAttributes(objectId, attributes, true))
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"ModifyAttributes failed: {objectId}");
        }

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = objectId,
            LayerFullPath = ResolveLayerFullPath(document, attributes.LayerIndex),
            Success = true,
            Messages = messages
        });
    }

    private static string SummarizeEntries(IReadOnlyList<ObjectScopedUserTextEntryRequest> entries, int objectCount)
    {
        return $"ObjectScopedUserTextEntries={entries.Count}; Objects={objectCount}";
    }

    private static string ResolveLayerFullPath(RhinoDoc document, int layerIndex)
    {
        return document.Layers.FindIndex(layerIndex)?.FullPath ?? "Unknown";
    }

    private static OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>> ValidateDeleteEntries(
        RhinoDoc document,
        IReadOnlyList<ObjectScopedUserTextKeyRequest> entries)
    {
        if (entries.Count == 0)
        {
            return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Fail("At least one object-scoped user text delete entry is required.");
        }

        var duplicateLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ObjectScopedUserTextKeyRequest entry in entries)
        {
            if (entry.ObjectId == Guid.Empty)
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Fail("ObjectId cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Fail($"User text key cannot be empty for object [{entry.ObjectId}].");
            }

            string duplicateKey = $"{entry.ObjectId:N}|{entry.Key}";
            if (!duplicateLookup.Add(duplicateKey))
            {
                return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Fail(
                    $"Duplicate user text key [{entry.Key}] was provided for object [{entry.ObjectId}].");
            }
        }

        Guid? missingObjectId = entries
            .Select(entry => entry.ObjectId)
            .Distinct()
            .FirstOrDefault(objectId => document.Objects.FindId(objectId) is null);

        if (missingObjectId.HasValue && missingObjectId.Value != Guid.Empty)
        {
            return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Fail($"Object was not found: {missingObjectId.Value}");
        }

        return OperationResponse<Dictionary<Guid, List<ObjectScopedUserTextKeyRequest>>>.Ok(
            entries.GroupBy(entry => entry.ObjectId).ToDictionary(group => group.Key, group => group.ToList()));
    }

    private static OperationResponse<ObjectEditOperationResult> DeleteEntriesFromObject(
        RhinoDoc document,
        Guid objectId,
        IReadOnlyList<ObjectScopedUserTextKeyRequest> entries)
    {
        RhinoObject? currentObject = document.Objects.FindId(objectId);
        if (currentObject is null)
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"Object was not found: {objectId}");
        }

        ObjectAttributes attributes = currentObject.Attributes.Duplicate();
        var messages = new List<string>();

        foreach (ObjectScopedUserTextKeyRequest entry in entries)
        {
            string? currentValue = attributes.GetUserString(entry.Key);
            attributes.DeleteUserString(entry.Key);
            string fromValue = currentValue is null ? "<missing>" : currentValue;
            messages.Add($"DeleteUserText: {entry.Key} [{fromValue}] -> <removed>");
        }

        if (!document.Objects.ModifyAttributes(objectId, attributes, true))
        {
            return OperationResponse<ObjectEditOperationResult>.Fail($"ModifyAttributes failed: {objectId}");
        }

        return OperationResponse<ObjectEditOperationResult>.Ok(new ObjectEditOperationResult
        {
            ObjectId = objectId,
            LayerFullPath = ResolveLayerFullPath(document, attributes.LayerIndex),
            Success = true,
            Messages = messages
        });
    }

    private static string SummarizeDeleteEntries(IReadOnlyList<ObjectScopedUserTextKeyRequest> entries, int objectCount)
    {
        return $"ObjectScopedUserTextDeleteEntries={entries.Count}; Objects={objectCount}";
    }

    private static ObjectEditOperationResult ToOperationResult(
        RhinoDoc document,
        Guid objectId,
        OperationResponse<ObjectEditOperationResult> result)
    {
        if (result.Success && result.Data is not null)
        {
            return result.Data;
        }

        RhinoObject? currentObject = document.Objects.FindId(objectId);
        return new ObjectEditOperationResult
        {
            ObjectId = objectId,
            LayerFullPath = currentObject is null ? "Unknown" : ResolveLayerFullPath(document, currentObject.Attributes.LayerIndex),
            Success = false,
            Messages = new[] { result.Message }
        };
    }
}

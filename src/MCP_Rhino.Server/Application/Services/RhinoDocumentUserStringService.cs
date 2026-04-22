extern alias rhinocommon;

using System.Text;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using Rhino.FileIO;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using StringTable = rhinocommon::Rhino.DocObjects.Tables.StringTable;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoDocumentUserStringService
{
    private const int DisplayLimit = 50;

    private readonly IRhinoDocumentRepository _repository;
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public RhinoDocumentUserStringService(
        IRhinoDocumentRepository repository,
        ILiveRhinoDocumentAccessor documentAccessor)
    {
        _repository = repository;
        _documentAccessor = documentAccessor;
    }

    // Live variant: reads directly from RhinoDoc.ActiveDoc.Strings so LLM agents
    // can observe in-memory edits that have not yet been saved to disk. Fails
    // with LIVE_RHINO_REQUIRED when the file is not open in a running Rhino.
    public OperationResponse<DocumentUserStringReadResponse> ReadInLive(DocumentUserStringReadRequest request)
    {
        return _documentAccessor.Execute(request.FilePath, document =>
        {
            StringTable strings = document.Strings;
            int count = strings.Count;

            var entries = new List<DocumentUserStringEntryResponse>(count);
            for (int i = 0; i < count; i++)
            {
                string key = strings.GetKey(i) ?? string.Empty;
                string value = strings.GetValue(i) ?? string.Empty;
                entries.Add(new DocumentUserStringEntryResponse
                {
                    Section = null,
                    Key = key,
                    Value = value
                });
            }

            var response = new DocumentUserStringReadResponse
            {
                FilePath = request.FilePath,
                TotalCount = count,
                Warnings = Array.Empty<ObjectEditWarning>(),
                Entries = entries
            };

            return OperationResponse<DocumentUserStringReadResponse>.Ok(
                response, "Document user strings read from live document.");
        });
    }

    public OperationResponse<DocumentUserStringReadResponse> Read(DocumentUserStringReadRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<DocumentUserStringReadResponse>.Fail($"File was not found: {request.FilePath}");
        }

        try
        {
            using var model = _repository.Read(request.FilePath);
            File3dmStringTable strings = model.Strings;
            int count = strings.Count;

            var entries = new List<DocumentUserStringEntryResponse>(count);
            for (int i = 0; i < count; i++)
            {
                string key = strings.GetKey(i) ?? string.Empty;
                string value = strings.GetValue(i) ?? string.Empty;
                entries.Add(new DocumentUserStringEntryResponse
                {
                    Section = null,
                    Key = key,
                    Value = value
                });
            }

            var response = new DocumentUserStringReadResponse
            {
                FilePath = request.FilePath,
                TotalCount = count,
                Warnings = CreateOfflineWarnings(request.FilePath),
                Entries = entries
            };

            return OperationResponse<DocumentUserStringReadResponse>.Ok(response, "Document user strings read completed.");
        }
        catch (Exception ex)
        {
            return OperationResponse<DocumentUserStringReadResponse>.Fail($"Document user string read failed: {ex.Message}");
        }
    }

    public OperationResponse<DocumentUserStringMutationResponse> Set(DocumentUserStringWriteRequest request)
    {
        OperationResponse<List<DocumentUserStringEntryRequest>> validation = ValidateEntries(request.Entries, requireValue: true);
        if (!validation.Success || validation.Data is null)
        {
            return OperationResponse<DocumentUserStringMutationResponse>.Fail(validation.Message);
        }

        return Mutate(
            request.FilePath,
            "MCP: SetDocumentUserStrings",
            validation.Data,
            (table, entry) =>
            {
                if (string.IsNullOrEmpty(entry.Section))
                {
                    table.SetString(entry.Key, entry.Value ?? string.Empty);
                    return $"SetString: {entry.Key}=[{entry.Value}]";
                }

                table.SetString(entry.Section, entry.Key, entry.Value ?? string.Empty);
                return $"SetString: {entry.Section}|{entry.Key}=[{entry.Value}]";
            },
            "Document user strings updated.");
    }

    public OperationResponse<DocumentUserStringMutationResponse> Delete(DocumentUserStringDeleteRequest request)
    {
        OperationResponse<List<DocumentUserStringEntryRequest>> validation = ValidateEntries(request.Entries, requireValue: false);
        if (!validation.Success || validation.Data is null)
        {
            return OperationResponse<DocumentUserStringMutationResponse>.Fail(validation.Message);
        }

        return Mutate(
            request.FilePath,
            "MCP: DeleteDocumentUserStrings",
            validation.Data,
            (table, entry) =>
            {
                if (string.IsNullOrEmpty(entry.Section))
                {
                    table.Delete(entry.Key);
                    return $"Delete: {entry.Key}";
                }

                table.Delete(entry.Section, entry.Key);
                return $"Delete: {entry.Section}|{entry.Key}";
            },
            "Document user strings deleted.");
    }

    public string FormatRead(DocumentUserStringReadResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Document User Strings");
        builder.AppendLine($"- File: {response.FilePath}");
        builder.AppendLine($"- Total: {response.TotalCount}");

        if (response.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Warnings:");
            foreach (ObjectEditWarning warning in response.Warnings)
            {
                builder.AppendLine($"- [{warning.Code}] {warning.Message}");
            }
        }

        if (response.Entries.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("No document user strings.");
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine("Entries:");
        foreach (DocumentUserStringEntryResponse entry in response.Entries.Take(DisplayLimit))
        {
            string label = string.IsNullOrEmpty(entry.Section) ? entry.Key : $"{entry.Section}|{entry.Key}";
            builder.AppendLine($"- {label} = {entry.Value}");
        }

        if (response.Entries.Count > DisplayLimit)
        {
            builder.AppendLine($"... only the first {DisplayLimit} entries are shown.");
        }

        return builder.ToString();
    }

    public string FormatMutation(DocumentUserStringMutationResponse response, string title)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine($"- File: {response.FilePath}");
        builder.AppendLine($"- Requested: {response.RequestedCount}");
        builder.AppendLine($"- Succeeded: {response.SucceededCount}");
        builder.AppendLine($"- Failed: {response.FailedCount}");

        if (response.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Warnings:");
            foreach (ObjectEditWarning warning in response.Warnings)
            {
                builder.AppendLine($"- [{warning.Code}] {warning.Message}");
            }
        }

        if (response.Results.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Results:");
            foreach (DocumentUserStringMutationResultResponse result in response.Results.Take(DisplayLimit))
            {
                string label = string.IsNullOrEmpty(result.Section) ? result.Key : $"{result.Section}|{result.Key}";
                builder.AppendLine($"- {label} | Success={result.Success} | {result.Message}");
            }

            if (response.Results.Count > DisplayLimit)
            {
                builder.AppendLine($"... only the first {DisplayLimit} entries are shown.");
            }
        }

        return builder.ToString();
    }

    private List<ObjectEditWarning> CreateOfflineWarnings(string filePath)
    {
        var warnings = new List<ObjectEditWarning>();
        if (_documentAccessor.TryGetActiveDocumentState(filePath, out bool hasUnsavedChanges) && hasUnsavedChanges)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "OFFLINE_READ_STALE",
                Message = "The target file is open in Rhino with unsaved changes, so offline read results may be stale."
            });
        }

        return warnings;
    }

    private OperationResponse<DocumentUserStringMutationResponse> Mutate(
        string filePath,
        string undoDescription,
        List<DocumentUserStringEntryRequest> entries,
        Func<StringTable, DocumentUserStringEntryRequest, string> apply,
        string successMessage)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, undoDescription, document =>
        {
            var results = new List<DocumentUserStringMutationResultResponse>(entries.Count);
            foreach (DocumentUserStringEntryRequest entry in entries)
            {
                try
                {
                    string message = apply(document.Strings, entry);
                    results.Add(new DocumentUserStringMutationResultResponse
                    {
                        Section = entry.Section,
                        Key = entry.Key,
                        Success = true,
                        Message = message
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new DocumentUserStringMutationResultResponse
                    {
                        Section = entry.Section,
                        Key = entry.Key,
                        Success = false,
                        Message = ex.Message
                    });
                }
            }

            if (results.Any(result => result.Success))
            {
                document.Views.Redraw();
            }

            var response = new DocumentUserStringMutationResponse
            {
                FilePath = filePath,
                RequestedCount = entries.Count,
                SucceededCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Warnings = Array.Empty<ObjectEditWarning>(),
                Results = results
            };

            return OperationResponse<(bool Mutated, DocumentUserStringMutationResponse Result)>.Ok(
                (results.Any(result => result.Success), response),
                successMessage);
        });
    }

    private static OperationResponse<List<DocumentUserStringEntryRequest>> ValidateEntries(
        IReadOnlyList<DocumentUserStringEntryRequest> entries,
        bool requireValue)
    {
        if (entries.Count == 0)
        {
            return OperationResponse<List<DocumentUserStringEntryRequest>>.Fail("At least one document user string entry is required.");
        }

        var duplicateLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<DocumentUserStringEntryRequest>(entries.Count);
        foreach (DocumentUserStringEntryRequest entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                return OperationResponse<List<DocumentUserStringEntryRequest>>.Fail("Document user string key cannot be empty.");
            }

            string section = string.IsNullOrWhiteSpace(entry.Section) ? string.Empty : entry.Section.Trim();
            string key = entry.Key.Trim();
            string compositeKey = string.IsNullOrEmpty(section) ? key : $"{section}|{key}";
            if (!duplicateLookup.Add(compositeKey))
            {
                return OperationResponse<List<DocumentUserStringEntryRequest>>.Fail($"Duplicate document user string entry: {compositeKey}");
            }

            normalized.Add(new DocumentUserStringEntryRequest
            {
                Section = string.IsNullOrEmpty(section) ? null : section,
                Key = key,
                Value = requireValue ? (entry.Value ?? string.Empty) : string.Empty
            });
        }

        return OperationResponse<List<DocumentUserStringEntryRequest>>.Ok(normalized);
    }
}

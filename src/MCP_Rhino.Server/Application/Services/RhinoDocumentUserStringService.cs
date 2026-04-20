using System.Text;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using Rhino.FileIO;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoDocumentUserStringService
{
    private const int DisplayLimit = 50;

    private readonly IRhinoDocumentRepository _repository;
    private readonly IFileMutationSafeguard _fileMutationSafeguard;

    public RhinoDocumentUserStringService(
        IRhinoDocumentRepository repository,
        IFileMutationSafeguard fileMutationSafeguard)
    {
        _repository = repository;
        _fileMutationSafeguard = fileMutationSafeguard;
    }

    public OperationResponse<DocumentUserStringReadResponse> Read(DocumentUserStringReadRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<DocumentUserStringReadResponse>.Fail($"错误：未找到文件 {request.FilePath}");
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
                Entries = entries
            };

            return OperationResponse<DocumentUserStringReadResponse>.Ok(response, "文档级 user string 读取完成。");
        }
        catch (Exception ex)
        {
            return OperationResponse<DocumentUserStringReadResponse>.Fail($"文档级 user string 读取失败: {ex.Message}");
        }
    }

    public OperationResponse<DocumentUserStringMutationResponse> Set(DocumentUserStringWriteRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<DocumentUserStringMutationResponse>.Fail($"错误：未找到文件 {request.FilePath}");
        }

        OperationResponse<List<DocumentUserStringEntryRequest>> validation = ValidateEntries(request.Entries, requireValue: true);
        if (!validation.Success || validation.Data is null)
        {
            return OperationResponse<DocumentUserStringMutationResponse>.Fail(validation.Message);
        }

        return Mutate(
            request.FilePath,
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
            successMessage: "文档级 user string 写入完成。",
            failureFallbackMessage: "文档级 user string 写回失败。请检查文件是否可写。");
    }

    public OperationResponse<DocumentUserStringMutationResponse> Delete(DocumentUserStringDeleteRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<DocumentUserStringMutationResponse>.Fail($"错误：未找到文件 {request.FilePath}");
        }

        OperationResponse<List<DocumentUserStringEntryRequest>> validation = ValidateEntries(request.Entries, requireValue: false);
        if (!validation.Success || validation.Data is null)
        {
            return OperationResponse<DocumentUserStringMutationResponse>.Fail(validation.Message);
        }

        return Mutate(
            request.FilePath,
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
            successMessage: "文档级 user string 删除完成。",
            failureFallbackMessage: "文档级 user string 删除写回失败。请检查文件是否可写。");
    }

    public string FormatRead(DocumentUserStringReadResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Document User Strings");
        builder.AppendLine($"- 文件: {response.FilePath}");
        builder.AppendLine($"- 总数: {response.TotalCount}");

        if (response.Entries.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("没有文档级 user string。");
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
            builder.AppendLine($"... 仅展示前 {DisplayLimit} 项。");
        }

        return builder.ToString();
    }

    public string FormatMutation(DocumentUserStringMutationResponse response, string title)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine($"- 文件: {response.FilePath}");
        builder.AppendLine($"- 请求项数: {response.RequestedCount}");
        builder.AppendLine($"- 成功: {response.SucceededCount}");
        builder.AppendLine($"- 失败: {response.FailedCount}");

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
                builder.AppendLine($"... 仅展示前 {DisplayLimit} 项。");
            }
        }

        return builder.ToString();
    }

    private OperationResponse<DocumentUserStringMutationResponse> Mutate(
        string filePath,
        List<DocumentUserStringEntryRequest> entries,
        Func<File3dmStringTable, DocumentUserStringEntryRequest, string> apply,
        string successMessage,
        string failureFallbackMessage)
    {
        try
        {
            using var model = _repository.Read(filePath);
            OperationResponse<FileMutationPreflightResponse> safeguard = _fileMutationSafeguard.BeforeOverwrite(filePath, entries.Count);
            if (!safeguard.Success)
            {
                return OperationResponse<DocumentUserStringMutationResponse>.Fail(safeguard.Message);
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

            var results = new List<DocumentUserStringMutationResultResponse>(entries.Count);
            bool writeSucceeded = false;

            try
            {
                File3dmStringTable table = model.Strings;
                foreach (DocumentUserStringEntryRequest entry in entries)
                {
                    try
                    {
                        string message = apply(table, entry);
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

                writeSucceeded = results.All(result => result.Success) && _repository.Write(model, filePath);
                if (!writeSucceeded)
                {
                    return OperationResponse<DocumentUserStringMutationResponse>.Fail(failureFallbackMessage);
                }
            }
            finally
            {
                _fileMutationSafeguard.AfterOverwrite(filePath, writeSucceeded);
            }

            var response = new DocumentUserStringMutationResponse
            {
                FilePath = filePath,
                RequestedCount = entries.Count,
                SucceededCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Warnings = warnings,
                Results = results
            };

            return OperationResponse<DocumentUserStringMutationResponse>.Ok(response, successMessage);
        }
        catch (Exception ex)
        {
            return OperationResponse<DocumentUserStringMutationResponse>.Fail($"文档级 user string 操作失败: {ex.Message}");
        }
    }

    private static OperationResponse<List<DocumentUserStringEntryRequest>> ValidateEntries(
        IReadOnlyList<DocumentUserStringEntryRequest> entries,
        bool requireValue)
    {
        if (entries.Count == 0)
        {
            return OperationResponse<List<DocumentUserStringEntryRequest>>.Fail("错误：至少需要提供一个文档级 user string 条目。");
        }

        var duplicateLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<DocumentUserStringEntryRequest>(entries.Count);
        foreach (DocumentUserStringEntryRequest entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                return OperationResponse<List<DocumentUserStringEntryRequest>>.Fail("错误：文档级 user string key 不能为空。");
            }

            string section = string.IsNullOrWhiteSpace(entry.Section) ? string.Empty : entry.Section.Trim();
            string key = entry.Key.Trim();
            string compositeKey = string.IsNullOrEmpty(section) ? key : $"{section}|{key}";
            if (!duplicateLookup.Add(compositeKey))
            {
                return OperationResponse<List<DocumentUserStringEntryRequest>>.Fail($"错误：文档级 user string 条目 [{compositeKey}] 被重复指定。");
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

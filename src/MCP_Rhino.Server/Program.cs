using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Agents.File;
using MCP_Rhino.Server.Agents.Editing;
using MCP_Rhino.Server.Agents.Inspection;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Server;
using MCP_Rhino.Server.Skills.Inspection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRhinoCore();
builder.Services.AddRhinoAgents();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .AddRhinoTools();

using var host = builder.Build();

if (TryHandleDeveloperCommands(host.Services, args))
{
    return;
}

await host.RunAsync();

static bool TryHandleDeveloperCommands(IServiceProvider services, string[] args)
{
    if (args.Length == 0)
    {
        return false;
    }

    using IServiceScope scope = services.CreateScope();
    var filterService = scope.ServiceProvider.GetRequiredService<RhinoObjectFilterService>();
    var editingService = scope.ServiceProvider.GetRequiredService<RhinoObjectEditingService>();
    var userTextService = scope.ServiceProvider.GetRequiredService<RhinoObjectUserTextService>();
    var layerSkill = scope.ServiceProvider.GetRequiredService<LayerObjectFilterSkill>();
    var typeSkill = scope.ServiceProvider.GetRequiredService<ObjectTypeFilterSkill>();
    var userAttributeSkill = scope.ServiceProvider.GetRequiredService<UserAttributeObjectFilterSkill>();
    var agent = scope.ServiceProvider.GetRequiredService<RhinoObjectFilterAgent>();
    var editingAgent = scope.ServiceProvider.GetRequiredService<RhinoObjectEditingAgent>();
    var fileArchiveAgent = scope.ServiceProvider.GetRequiredService<FileArchiveAgent>();

    if (args[0].Equals("find-layer-candidates", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- find-layer-candidates <3dm文件路径> <layerQuery> [exactMatch]");
            return true;
        }

        bool exactMatch = args.Length >= 4 && bool.TryParse(args[3], out var parsedExactMatch)
            ? parsedExactMatch
            : false;

        var result = filterService.FindLayerCandidates(new FindLayerCandidatesRequest
        {
            FilePath = args[1],
            LayerQuery = args[2],
            ExactMatch = exactMatch
        });

        Console.WriteLine(filterService.FormatLayerCandidates(args[2], result.Success ? result.Data : null, result.Message));
        return true;
    }

    if (args[0].Equals("filter-objects-by-layer", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- filter-objects-by-layer <3dm文件路径> <layerQuery> [confirmedLayerFullPath]");
            return true;
        }

        string? confirmedLayerFullPath = args.Length >= 4 ? args[3] : null;
        Console.WriteLine(layerSkill.Filter(args[1], args[2], confirmedLayerFullPath));
        return true;
    }

    if (args[0].Equals("filter-objects-by-type", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- filter-objects-by-type <3dm文件路径> <type1,type2,...>");
            return true;
        }

        Console.WriteLine(typeSkill.Filter(args[1], ParseCsv(args[2])));
        return true;
    }

    if (args[0].Equals("filter-objects-by-user-attributes", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- filter-objects-by-user-attributes <3dm文件路径> <attrSpec> [attrMode]");
            return true;
        }

        FilterMatchMode attributeMatchMode = args.Length >= 4
            ? ParseFilterMatchMode(args[3])
            : FilterMatchMode.All;

        Console.WriteLine(userAttributeSkill.Filter(args[1], ParseUserAttributeConditions(args[2]), attributeMatchMode));
        return true;
    }

    if (args[0].Equals("filter-objects-agent", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- filter-objects-agent <3dm文件路径> [layers=...] [layerpaths=...] [types=...] [attrs=...] [mode=all|any] [attrmode=all|any]");
            return true;
        }

        var request = new FilterObjectsRequest
        {
            FilePath = args[1]
        };

        for (int i = 2; i < args.Length; i++)
        {
            string token = args[i];
            if (token.StartsWith("layers=", StringComparison.OrdinalIgnoreCase))
            {
                request.LayerQueries = ParseCsv(token[7..]);
            }
            else if (token.StartsWith("layerpaths=", StringComparison.OrdinalIgnoreCase))
            {
                request.ConfirmedLayerFullPaths = ParseCsv(token[11..]);
            }
            else if (token.StartsWith("types=", StringComparison.OrdinalIgnoreCase))
            {
                request.ObjectTypes = ParseCsv(token[6..]);
            }
            else if (token.StartsWith("attrs=", StringComparison.OrdinalIgnoreCase))
            {
                request.UserAttributeConditions = ParseUserAttributeConditions(token[6..]);
            }
            else if (token.StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
            {
                request.MatchMode = ParseFilterMatchMode(token[5..]);
            }
            else if (token.StartsWith("attrmode=", StringComparison.OrdinalIgnoreCase))
            {
                request.UserAttributeMatchMode = ParseFilterMatchMode(token[9..]);
            }
        }

        Console.WriteLine(agent.Filter(request));
        return true;
    }

    if (args[0].Equals("preview-object-edits", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- preview-object-edits <3dm文件路径> <editSpec> [layers=...] [layerpaths=...] [types=...] [attrs=...] [mode=all|any] [attrmode=all|any]。editSpec 支持 set-user:key=value、remove-user:key、set-layer:fullPath、set-color:r,g,b");
            return true;
        }

        try
        {
            var request = BuildPreviewObjectEditsRequest(args);
            var result = editingAgent.Preview(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? editingService.FormatPreview(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"对象编辑预览参数解析失败: {ex.Message}");
        }

        return true;
    }

    if (args[0].Equals("apply-object-edits", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- apply-object-edits <3dm文件路径> <editSpec> [layers=...] [layerpaths=...] [types=...] [attrs=...] [mode=all|any] [attrmode=all|any]。editSpec 支持 set-user:key=value、remove-user:key、set-layer:fullPath、set-color:r,g,b");
            return true;
        }

        try
        {
            var request = BuildApplyObjectEditsRequest(args);
            var result = editingAgent.Apply(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? editingService.FormatExecution(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"对象编辑执行参数解析失败: {ex.Message}");
        }

        return true;
    }

    if (args[0].Equals("preview-object-user-text-writes", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- preview-object-user-text-writes <3dm文件路径> <entrySpec>。entrySpec 格式: objectId|key=value;objectId|key=value");
            return true;
        }

        try
        {
            var request = BuildObjectUserTextBatchWriteRequest(args);
            var result = userTextService.Preview(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? userTextService.FormatPreview(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"对象级 user text 预览参数解析失败: {ex.Message}");
        }

        return true;
    }

    if (args[0].Equals("apply-object-user-text-writes", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- apply-object-user-text-writes <3dm文件路径> <entrySpec>。entrySpec 格式: objectId|key=value;objectId|key=value");
            return true;
        }

        try
        {
            var request = BuildObjectUserTextBatchWriteRequest(args);
            var result = userTextService.Apply(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? userTextService.FormatExecution(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"对象级 user text 执行参数解析失败: {ex.Message}");
        }

        return true;
    }

    if (args[0].Equals("inspect-file-mutation-readiness", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- inspect-file-mutation-readiness <3dm文件路径>");
            return true;
        }

        var result = fileArchiveAgent.InspectReadiness(args[1]);
        Console.WriteLine(result.Success && result.Data is not null
            ? FormatFileMutationReadiness(result.Data)
            : result.Message);
        return true;
    }

    if (args[0].Equals("create-archive-snapshot", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- create-archive-snapshot <3dm文件路径>");
            return true;
        }

        var result = fileArchiveAgent.CreateSnapshot(args[1]);
        Console.WriteLine(result.Success && result.Data is not null
            ? FormatArchiveSnapshot(result.Data)
            : result.Message);
        return true;
    }

    if (args[0].Equals("cleanup-archive", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- cleanup-archive <3dm文件路径>");
            return true;
        }

        var result = fileArchiveAgent.CleanupArchive(args[1]);
        Console.WriteLine(result.Success && result.Data is not null
            ? FormatArchiveCleanup(result.Data)
            : result.Message);
        return true;
    }

    return false;
}

static string FormatFileMutationReadiness(FileMutationReadinessResponse response)
{
    var lines = new List<string>
    {
        "# File Mutation Readiness",
        $"- 文件: {response.FilePath}",
        $"- 可修改: {response.IsReady}",
        $"- 已锁定: {response.IsLocked}",
        $"- 检测到 .rhl: {response.LockFileDetected}",
        $"- 说明: {response.Message}"
    };

    if (response.Signals.Count > 0)
    {
        lines.Add(string.Empty);
        lines.Add("Signals:");
        lines.AddRange(response.Signals.Select(signal => $"- {signal}"));
    }

    return string.Join(Environment.NewLine, lines);
}

static string FormatArchiveSnapshot(ArchiveSnapshotResponse response)
{
    return string.Join(Environment.NewLine,
        "# Archive Snapshot",
        $"- 源文件: {response.SourceFilePath}",
        $"- archive目录: {response.ArchiveDirectoryPath}",
        $"- 备份文件: {response.ArchiveFilePath}",
        $"- 新建archive目录: {response.ArchiveDirectoryCreated}",
        $"- 覆盖同名备份: {response.OverwroteExistingSnapshot}");
}

static string FormatArchiveCleanup(ArchiveCleanupResponse response)
{
    var lines = new List<string>
    {
        "# Archive Cleanup",
        $"- 源文件: {response.SourceFilePath}",
        $"- archive目录: {response.ArchiveDirectoryPath}",
        $"- 删除数量: {response.DeletedFileCount}"
    };

    if (response.DeletedFiles.Count > 0)
    {
        lines.Add(string.Empty);
        lines.Add("Deleted:");
        lines.AddRange(response.DeletedFiles.Select(file => $"- {file}"));
    }

    return string.Join(Environment.NewLine, lines);
}

static PreviewObjectEditsRequest BuildPreviewObjectEditsRequest(string[] args)
{
    return new PreviewObjectEditsRequest
    {
        FilePath = args[1],
        Operations = ParseEditOperations(args[2]),
        LayerQueries = ParseNamedCsv(args, "layers="),
        ConfirmedLayerFullPaths = ParseNamedCsv(args, "layerpaths="),
        ObjectTypes = ParseNamedCsv(args, "types="),
        UserAttributeConditions = ParseNamedUserAttributes(args, "attrs="),
        MatchMode = ParseNamedFilterMatchMode(args, "mode="),
        UserAttributeMatchMode = ParseNamedFilterMatchMode(args, "attrmode=")
    };
}

static ApplyObjectEditsRequest BuildApplyObjectEditsRequest(string[] args)
{
    return new ApplyObjectEditsRequest
    {
        FilePath = args[1],
        Operations = ParseEditOperations(args[2]),
        LayerQueries = ParseNamedCsv(args, "layers="),
        ConfirmedLayerFullPaths = ParseNamedCsv(args, "layerpaths="),
        ObjectTypes = ParseNamedCsv(args, "types="),
        UserAttributeConditions = ParseNamedUserAttributes(args, "attrs="),
        MatchMode = ParseNamedFilterMatchMode(args, "mode="),
        UserAttributeMatchMode = ParseNamedFilterMatchMode(args, "attrmode=")
    };
}

static ObjectUserTextBatchWriteRequest BuildObjectUserTextBatchWriteRequest(string[] args)
{
    return new ObjectUserTextBatchWriteRequest
    {
        FilePath = args[1],
        Entries = ParseObjectScopedUserTextEntries(args[2])
    };
}

static List<ObjectEditOperationRequest> ParseEditOperations(string input)
{
    var operations = new List<ObjectEditOperationRequest>();
    foreach (string token in input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        int separatorIndex = token.IndexOf(':');
        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException($"无效 editSpec 片段: {token}");
        }

        string operationName = token[..separatorIndex].Trim().ToLowerInvariant();
        string payload = token[(separatorIndex + 1)..].Trim();

        switch (operationName)
        {
            case "set-user":
            case "set-user-text":
                int equalIndex = payload.IndexOf('=');
                if (equalIndex <= 0)
                {
                    throw new InvalidOperationException($"SetUserText 格式应为 set-user:key=value，收到: {token}");
                }

                operations.Add(new ObjectEditOperationRequest
                {
                    OperationType = ObjectEditOperationType.SetUserText,
                    Key = payload[..equalIndex],
                    Value = payload[(equalIndex + 1)..]
                });
                break;

            case "remove-user":
            case "remove-user-text":
                operations.Add(new ObjectEditOperationRequest
                {
                    OperationType = ObjectEditOperationType.RemoveUserText,
                    Key = payload
                });
                break;

            case "set-layer":
                operations.Add(new ObjectEditOperationRequest
                {
                    OperationType = ObjectEditOperationType.SetLayer,
                    TargetLayerFullPath = payload
                });
                break;

            case "set-color":
            case "set-display-color":
                string[] colorParts = payload.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (colorParts.Length != 3
                    || !int.TryParse(colorParts[0], out int r)
                    || !int.TryParse(colorParts[1], out int g)
                    || !int.TryParse(colorParts[2], out int b))
                {
                    throw new InvalidOperationException($"SetDisplayColor 格式应为 set-color:r,g,b，收到: {token}");
                }

                operations.Add(new ObjectEditOperationRequest
                {
                    OperationType = ObjectEditOperationType.SetDisplayColor,
                    Color = new ObjectColorRequest
                    {
                        R = r,
                        G = g,
                        B = b
                    }
                });
                break;

            default:
                throw new InvalidOperationException($"未知编辑操作: {operationName}");
        }
    }

    return operations;
}

static List<ObjectScopedUserTextEntryRequest> ParseObjectScopedUserTextEntries(string input)
{
    var entries = new List<ObjectScopedUserTextEntryRequest>();
    foreach (string token in input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        int pipeIndex = token.IndexOf('|');
        if (pipeIndex <= 0)
        {
            throw new InvalidOperationException($"无效 entrySpec 片段: {token}");
        }

        if (!Guid.TryParse(token[..pipeIndex], out Guid objectId))
        {
            throw new InvalidOperationException($"无效 ObjectId: {token[..pipeIndex]}");
        }

        string payload = token[(pipeIndex + 1)..].Trim();
        int equalIndex = payload.IndexOf('=');
        if (equalIndex <= 0)
        {
            throw new InvalidOperationException($"entrySpec 格式应为 objectId|key=value，收到: {token}");
        }

        entries.Add(new ObjectScopedUserTextEntryRequest
        {
            ObjectId = objectId,
            Key = payload[..equalIndex],
            Value = payload[(equalIndex + 1)..]
        });
    }

    return entries;
}

static List<string> ParseNamedCsv(string[] args, string prefix)
{
    string? token = args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    return token is null ? new List<string>() : ParseCsv(token[prefix.Length..]);
}

static List<UserAttributeConditionRequest> ParseNamedUserAttributes(string[] args, string prefix)
{
    string? token = args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    return token is null ? new List<UserAttributeConditionRequest>() : ParseUserAttributeConditions(token[prefix.Length..]);
}

static FilterMatchMode ParseNamedFilterMatchMode(string[] args, string prefix)
{
    string? token = args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    return token is null ? FilterMatchMode.All : ParseFilterMatchMode(token[prefix.Length..]);
}

static List<string> ParseCsv(string input)
{
    return input
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .ToList();
}

static List<UserAttributeConditionRequest> ParseUserAttributeConditions(string input)
{
    var conditions = new List<UserAttributeConditionRequest>();
    foreach (string token in input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        int containsIndex = token.IndexOf('~');
        int exactIndex = token.IndexOf('=');

        if (containsIndex > 0)
        {
            conditions.Add(new UserAttributeConditionRequest
            {
                Key = token[..containsIndex],
                ExpectedValue = token[(containsIndex + 1)..],
                ComparisonMode = UserAttributeComparisonMode.Contains
            });
            continue;
        }

        if (exactIndex > 0)
        {
            conditions.Add(new UserAttributeConditionRequest
            {
                Key = token[..exactIndex],
                ExpectedValue = token[(exactIndex + 1)..],
                ComparisonMode = UserAttributeComparisonMode.Exact
            });
            continue;
        }

        conditions.Add(new UserAttributeConditionRequest
        {
            Key = token,
            ComparisonMode = UserAttributeComparisonMode.Exists
        });
    }

    return conditions;
}

static FilterMatchMode ParseFilterMatchMode(string value)
{
    return Enum.TryParse<FilterMatchMode>(value, true, out var parsed)
        ? parsed
        : FilterMatchMode.All;
}
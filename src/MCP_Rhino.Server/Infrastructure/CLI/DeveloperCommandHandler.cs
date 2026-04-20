using MCP_Rhino.Server.Agents.Editing;
using MCP_Rhino.Server.Agents.File;
using MCP_Rhino.Server.Agents.Inspection;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Inspection;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private readonly RhinoObjectFilterService _filterService;
    private readonly RhinoObjectEditingService _editingService;
    private readonly RhinoObjectUserTextService _userTextService;
    private readonly LayerObjectFilterSkill _layerSkill;
    private readonly ObjectTypeFilterSkill _typeSkill;
    private readonly UserAttributeObjectFilterSkill _userAttributeSkill;
    private readonly RhinoObjectFilterAgent _filterAgent;
    private readonly RhinoObjectEditingAgent _editingAgent;
    private readonly FileArchiveAgent _fileArchiveAgent;

    public DeveloperCommandHandler(
        RhinoObjectFilterService filterService,
        RhinoObjectEditingService editingService,
        RhinoObjectUserTextService userTextService,
        LayerObjectFilterSkill layerSkill,
        ObjectTypeFilterSkill typeSkill,
        UserAttributeObjectFilterSkill userAttributeSkill,
        RhinoObjectFilterAgent filterAgent,
        RhinoObjectEditingAgent editingAgent,
        FileArchiveAgent fileArchiveAgent)
    {
        _filterService = filterService;
        _editingService = editingService;
        _userTextService = userTextService;
        _layerSkill = layerSkill;
        _typeSkill = typeSkill;
        _userAttributeSkill = userAttributeSkill;
        _filterAgent = filterAgent;
        _editingAgent = editingAgent;
        _fileArchiveAgent = fileArchiveAgent;
    }

    public bool TryHandle(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        return args[0].ToLowerInvariant() switch
        {
            "find-layer-candidates" => HandleFindLayerCandidates(args),
            "filter-objects-by-layer" => HandleFilterObjectsByLayer(args),
            "filter-objects-by-type" => HandleFilterObjectsByType(args),
            "filter-objects-by-user-attributes" => HandleFilterObjectsByUserAttributes(args),
            "filter-objects-agent" => HandleFilterObjectsAgent(args),
            "preview-object-edits" => HandlePreviewObjectEdits(args),
            "apply-object-edits" => HandleApplyObjectEdits(args),
            "preview-object-user-text-writes" => HandlePreviewObjectUserTextWrites(args),
            "apply-object-user-text-writes" => HandleApplyObjectUserTextWrites(args),
            "inspect-file-mutation-readiness" => HandleInspectFileMutationReadiness(args),
            "create-archive-snapshot" => HandleCreateArchiveSnapshot(args),
            "cleanup-archive" => HandleCleanupArchive(args),
            _ => false
        };
    }

    private bool HandleFindLayerCandidates(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- find-layer-candidates <3dm文件路径> <layerQuery> [exactMatch]");
            return true;
        }

        bool exactMatch = args.Length >= 4 && bool.TryParse(args[3], out var parsedExactMatch)
            ? parsedExactMatch
            : false;

        var result = _filterService.FindLayerCandidates(new FindLayerCandidatesRequest
        {
            FilePath = args[1],
            LayerQuery = args[2],
            ExactMatch = exactMatch
        });

        Console.WriteLine(_filterService.FormatLayerCandidates(args[2], result.Success ? result.Data : null, result.Message));
        return true;
    }

    private bool HandleFilterObjectsByLayer(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- filter-objects-by-layer <3dm文件路径> <layerQuery> [confirmedLayerFullPath]");
            return true;
        }

        string? confirmedLayerFullPath = args.Length >= 4 ? args[3] : null;
        Console.WriteLine(_layerSkill.Filter(args[1], args[2], confirmedLayerFullPath));
        return true;
    }

    private bool HandleFilterObjectsByType(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- filter-objects-by-type <3dm文件路径> <type1,type2,...>");
            return true;
        }

        Console.WriteLine(_typeSkill.Filter(args[1], ParseCsv(args[2])));
        return true;
    }

    private bool HandleFilterObjectsByUserAttributes(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- filter-objects-by-user-attributes <3dm文件路径> <attrSpec> [attrMode]");
            return true;
        }

        FilterMatchMode attributeMatchMode = args.Length >= 4
            ? ParseFilterMatchMode(args[3])
            : FilterMatchMode.All;

        Console.WriteLine(_userAttributeSkill.Filter(args[1], ParseUserAttributeConditions(args[2]), attributeMatchMode));
        return true;
    }

    private bool HandleFilterObjectsAgent(string[] args)
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

        Console.WriteLine(_filterAgent.Filter(request));
        return true;
    }

    private bool HandlePreviewObjectEdits(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- preview-object-edits <3dm文件路径> <editSpec> [layers=...] [layerpaths=...] [types=...] [attrs=...] [mode=all|any] [attrmode=all|any]。editSpec 支持 set-user:key=value、remove-user:key、set-layer:fullPath、set-color:r,g,b");
            return true;
        }

        try
        {
            var request = BuildPreviewObjectEditsRequest(args);
            var result = _editingAgent.Preview(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _editingService.FormatPreview(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"对象编辑预览参数解析失败: {ex.Message}");
        }

        return true;
    }

    private bool HandleApplyObjectEdits(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- apply-object-edits <3dm文件路径> <editSpec> [layers=...] [layerpaths=...] [types=...] [attrs=...] [mode=all|any] [attrmode=all|any]。editSpec 支持 set-user:key=value、remove-user:key、set-layer:fullPath、set-color:r,g,b");
            return true;
        }

        try
        {
            var request = BuildApplyObjectEditsRequest(args);
            var result = _editingAgent.Apply(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _editingService.FormatExecution(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"对象编辑执行参数解析失败: {ex.Message}");
        }

        return true;
    }

    private bool HandlePreviewObjectUserTextWrites(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- preview-object-user-text-writes <3dm文件路径> <entrySpec>。entrySpec 格式: objectId|key=value;objectId|key=value");
            return true;
        }

        try
        {
            var request = BuildObjectUserTextBatchWriteRequest(args);
            var result = _userTextService.Preview(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _userTextService.FormatPreview(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"对象级 user text 预览参数解析失败: {ex.Message}");
        }

        return true;
    }

    private bool HandleApplyObjectUserTextWrites(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- apply-object-user-text-writes <3dm文件路径> <entrySpec>。entrySpec 格式: objectId|key=value;objectId|key=value");
            return true;
        }

        try
        {
            var request = BuildObjectUserTextBatchWriteRequest(args);
            var result = _userTextService.Apply(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _userTextService.FormatExecution(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"对象级 user text 执行参数解析失败: {ex.Message}");
        }

        return true;
    }

    private bool HandleInspectFileMutationReadiness(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- inspect-file-mutation-readiness <3dm文件路径>");
            return true;
        }

        var result = _fileArchiveAgent.InspectReadiness(args[1]);
        Console.WriteLine(result.Success && result.Data is not null
            ? FormatFileMutationReadiness(result.Data)
            : result.Message);
        return true;
    }

    private bool HandleCreateArchiveSnapshot(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- create-archive-snapshot <3dm文件路径>");
            return true;
        }

        var result = _fileArchiveAgent.CreateSnapshot(args[1]);
        Console.WriteLine(result.Success && result.Data is not null
            ? FormatArchiveSnapshot(result.Data)
            : result.Message);
        return true;
    }

    private bool HandleCleanupArchive(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- cleanup-archive <3dm文件路径>");
            return true;
        }

        var result = _fileArchiveAgent.CleanupArchive(args[1]);
        Console.WriteLine(result.Success && result.Data is not null
            ? FormatArchiveCleanup(result.Data)
            : result.Message);
        return true;
    }
}

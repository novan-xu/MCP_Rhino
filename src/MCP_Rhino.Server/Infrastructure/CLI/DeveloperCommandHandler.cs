using MCP_Rhino.Server.Agents.Editing;
using MCP_Rhino.Server.Agents.Inspection;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Inspection;
using MCP_Rhino.Server.Skills.Modeling;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private readonly RhinoObjectFilterService _filterService;
    private readonly RhinoObjectEditingService _editingService;
    private readonly RhinoObjectUserTextService _userTextService;
    private readonly RhinoDocumentUserStringService _documentUserStringService;
    private readonly LayerObjectFilterSkill _layerSkill;
    private readonly ObjectTypeFilterSkill _typeSkill;
    private readonly UserAttributeObjectFilterSkill _userAttributeSkill;
    private readonly RhinoObjectFilterAgent _filterAgent;
    private readonly RhinoObjectEditingAgent _editingAgent;
    private readonly GeometryCreationSkill _geometryCreationSkill;
    private readonly GeometryModificationSkill _geometryModificationSkill;

    public DeveloperCommandHandler(
        RhinoObjectFilterService filterService,
        RhinoObjectEditingService editingService,
        RhinoObjectUserTextService userTextService,
        RhinoDocumentUserStringService documentUserStringService,
        LayerObjectFilterSkill layerSkill,
        ObjectTypeFilterSkill typeSkill,
        UserAttributeObjectFilterSkill userAttributeSkill,
        RhinoObjectFilterAgent filterAgent,
        RhinoObjectEditingAgent editingAgent,
        GeometryCreationSkill geometryCreationSkill,
        GeometryModificationSkill geometryModificationSkill)
    {
        _filterService = filterService;
        _editingService = editingService;
        _userTextService = userTextService;
        _documentUserStringService = documentUserStringService;
        _layerSkill = layerSkill;
        _typeSkill = typeSkill;
        _userAttributeSkill = userAttributeSkill;
        _filterAgent = filterAgent;
        _editingAgent = editingAgent;
        _geometryCreationSkill = geometryCreationSkill;
        _geometryModificationSkill = geometryModificationSkill;
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
            "get-object-user-strings" => HandleGetObjectUserStrings(args),
            "delete-object-user-text" => HandleDeleteObjectUserText(args),
            "get-document-user-strings" => HandleGetDocumentUserStrings(args),
            "set-document-user-strings" => HandleSetDocumentUserStrings(args),
            "delete-document-user-strings" => HandleDeleteDocumentUserStrings(args),
            "geometry-smoke-test" => HandleGeometrySmokeTest(args),
            "online-mutation-refactor-smoke-test" => HandleOnlineMutationRefactorSmokeTest(args),
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

    private bool HandleGetObjectUserStrings(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- get-object-user-strings <3dm文件路径> <guid1,guid2,...>");
            return true;
        }

        try
        {
            var request = new ObjectUserTextReadRequest
            {
                FilePath = args[1],
                ObjectIds = ParseGuidCsv(args[2])
            };

            var result = _userTextService.Read(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _userTextService.FormatRead(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"对象级 user string 读取参数解析失败: {ex.Message}");
        }

        return true;
    }

    private bool HandleDeleteObjectUserText(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- delete-object-user-text <3dm文件路径> <entrySpec>。entrySpec 格式: objectId|key;objectId|key");
            return true;
        }

        try
        {
            var request = new ObjectUserTextDeleteRequest
            {
                FilePath = args[1],
                Entries = ParseObjectScopedUserTextKeys(args[2])
            };

            var result = _userTextService.Delete(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _userTextService.FormatExecution(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"对象级 user text 删除参数解析失败: {ex.Message}");
        }

        return true;
    }

    private bool HandleGetDocumentUserStrings(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- get-document-user-strings <3dm文件路径>");
            return true;
        }

        var result = _documentUserStringService.Read(new DocumentUserStringReadRequest
        {
            FilePath = args[1]
        });

        Console.WriteLine(result.Success && result.Data is not null
            ? _documentUserStringService.FormatRead(result.Data)
            : result.Message);
        return true;
    }

    private bool HandleSetDocumentUserStrings(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- set-document-user-strings <3dm文件路径> <entrySpec>。entrySpec 格式: key=value;section|entry=value");
            return true;
        }

        try
        {
            var request = new DocumentUserStringWriteRequest
            {
                FilePath = args[1],
                Entries = ParseDocumentUserStringWriteEntries(args[2])
            };

            var result = _documentUserStringService.Set(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _documentUserStringService.FormatMutation(result.Data, "Document User String Write")
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"文档级 user string 写入参数解析失败: {ex.Message}");
        }

        return true;
    }

    private bool HandleDeleteDocumentUserStrings(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- delete-document-user-strings <3dm文件路径> <entrySpec>。entrySpec 格式: key;section|entry");
            return true;
        }

        try
        {
            var request = new DocumentUserStringDeleteRequest
            {
                FilePath = args[1],
                Entries = ParseDocumentUserStringDeleteEntries(args[2])
            };

            var result = _documentUserStringService.Delete(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _documentUserStringService.FormatMutation(result.Data, "Document User String Delete")
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"文档级 user string 删除参数解析失败: {ex.Message}");
        }

        return true;
    }

}

using System.Text;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Rhino.DocObjects;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoObjectFilterService
{
    private readonly IRhinoDocumentRepository _repository;
    private readonly IEnumerable<IObjectFilterCriterionEvaluator> _evaluators;

    public RhinoObjectFilterService(
        IRhinoDocumentRepository repository,
        IEnumerable<IObjectFilterCriterionEvaluator> evaluators)
    {
        _repository = repository;
        _evaluators = evaluators;
    }

    public OperationResponse<IReadOnlyList<RhinoLayerCandidate>> FindLayerCandidates(FindLayerCandidatesRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<IReadOnlyList<RhinoLayerCandidate>>.Fail($"错误：未找到文件 {request.FilePath}");
        }

        if (string.IsNullOrWhiteSpace(request.LayerQuery))
        {
            return OperationResponse<IReadOnlyList<RhinoLayerCandidate>>.Fail("错误：layerQuery 不能为空");
        }

        try
        {
            using var model = _repository.Read(request.FilePath);

            var candidates = model.AllLayers
                .Where(layer => !layer.IsDeleted)
                .Where(layer => MatchesLayer(layer, request.LayerQuery, request.ExactMatch))
                .Select(layer => new RhinoLayerCandidate
                {
                    LayerIndex = layer.Index,
                    LayerName = layer.Name,
                    FullPath = layer.FullPath,
                    ObjectCount = model.Objects.Count(obj => obj.Attributes.LayerIndex == layer.Index)
                })
                .OrderBy(layer => layer.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return OperationResponse<IReadOnlyList<RhinoLayerCandidate>>.Ok(candidates, candidates.Count == 0
                ? "未找到匹配图层。"
                : $"已找到 {candidates.Count} 个匹配图层。");
        }
        catch (Exception ex)
        {
            return OperationResponse<IReadOnlyList<RhinoLayerCandidate>>.Fail($"读取图层失败: {ex.Message}");
        }
    }

    public OperationResponse<RhinoObjectFilterResult> FilterByLayer(FilterObjectsByLayerRequest request)
    {
        return Filter(new FilterObjectsRequest
        {
            FilePath = request.FilePath,
            ConfirmedLayerFullPaths = new List<string> { request.LayerFullPath }
        });
    }

    public OperationResponse<RhinoObjectFilterResult> FilterByType(FilterObjectsByTypeRequest request)
    {
        return Filter(new FilterObjectsRequest
        {
            FilePath = request.FilePath,
            ObjectTypes = request.ObjectTypes
        });
    }

    public OperationResponse<RhinoObjectFilterResult> FilterByUserAttributes(FilterObjectsByUserAttributesRequest request)
    {
        return Filter(new FilterObjectsRequest
        {
            FilePath = request.FilePath,
            UserAttributeConditions = request.Conditions,
            UserAttributeMatchMode = request.MatchMode
        });
    }

    public OperationResponse<RhinoObjectFilterResult> Filter(FilterObjectsRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail($"错误：未找到文件 {request.FilePath}");
        }

        RhinoObjectFilterCriteria criteria = CreateCriteria(request);
        if (!criteria.HasAnyCriteria())
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail("错误：至少需要提供一个筛查条件。");
        }

        try
        {
            using var model = _repository.Read(request.FilePath);
            var objectInfos = BuildObjectInfos(model);
            var activeEvaluators = _evaluators.Where(evaluator => evaluator.CanEvaluate(criteria)).ToList();

            var matchedObjects = objectInfos
                .Where(objectInfo => MatchesAllCriteria(objectInfo, criteria, activeEvaluators))
                .ToList();

            var result = new RhinoObjectFilterResult
            {
                FilePath = request.FilePath,
                TotalObjectCount = objectInfos.Count,
                MatchedCount = matchedObjects.Count,
                CriteriaSummary = SummarizeCriteria(criteria),
                Objects = matchedObjects
            };

            return OperationResponse<RhinoObjectFilterResult>.Ok(result, $"筛查完成，匹配到 {matchedObjects.Count} 个对象。");
        }
        catch (Exception ex)
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail($"对象筛查失败: {ex.Message}");
        }
    }

    public OperationResponse<RhinoObjectFilterResult> ResolveByObjectIds(string filePath, IReadOnlyList<Guid> objectIds)
    {
        if (!_repository.Exists(filePath))
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail($"错误：未找到文件 {filePath}");
        }

        List<Guid> distinctObjectIds = objectIds
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctObjectIds.Count == 0)
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail("错误：至少需要提供一个非空 ObjectId。");
        }

        try
        {
            using var model = _repository.Read(filePath);
            List<RhinoObjectInfo> objectInfos = BuildObjectInfos(model);
            Dictionary<Guid, RhinoObjectInfo> objectLookup = objectInfos.ToDictionary(objectInfo => objectInfo.ObjectId);

            var resolvedObjects = new List<RhinoObjectInfo>(distinctObjectIds.Count);
            var missingObjectIds = new List<Guid>();

            foreach (Guid objectId in distinctObjectIds)
            {
                if (objectLookup.TryGetValue(objectId, out RhinoObjectInfo? objectInfo))
                {
                    resolvedObjects.Add(objectInfo);
                }
                else
                {
                    missingObjectIds.Add(objectId);
                }
            }

            if (missingObjectIds.Count > 0)
            {
                return OperationResponse<RhinoObjectFilterResult>.Fail(
                    $"错误：以下 ObjectId 在当前模型中不存在: {string.Join(", ", missingObjectIds)}");
            }

            var result = new RhinoObjectFilterResult
            {
                FilePath = filePath,
                TotalObjectCount = objectInfos.Count,
                MatchedCount = resolvedObjects.Count,
                CriteriaSummary = SummarizeObjectIds(distinctObjectIds),
                Objects = resolvedObjects
            };

            return OperationResponse<RhinoObjectFilterResult>.Ok(result, $"已解析 {resolvedObjects.Count} 个显式 ObjectId。");
        }
        catch (Exception ex)
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail($"按 ObjectId 解析对象失败: {ex.Message}");
        }
    }

    public string FormatLayerCandidates(string layerQuery, IReadOnlyList<RhinoLayerCandidate>? candidates, string message)
    {
        if (candidates is null || candidates.Count == 0)
        {
            return string.IsNullOrWhiteSpace(message)
                ? $"未找到与 [{layerQuery}] 匹配的图层。"
                : message;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"找到 {candidates.Count} 个与 [{layerQuery}] 匹配的图层：");
        for (int i = 0; i < candidates.Count; i++)
        {
            RhinoLayerCandidate candidate = candidates[i];
            builder.AppendLine($"{i + 1}. {candidate.FullPath} (LayerIndex: {candidate.LayerIndex}, Objects: {candidate.ObjectCount})");
        }

        return builder.ToString();
    }

    public string FormatFilterResult(RhinoObjectFilterResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Rhino Object Filter Result");
        builder.AppendLine($"- 文件: {result.FilePath}");
        builder.AppendLine($"- 总对象数: {result.TotalObjectCount}");
        builder.AppendLine($"- 匹配对象数: {result.MatchedCount}");
        builder.AppendLine($"- 条件摘要: {result.CriteriaSummary}");
        builder.AppendLine();

        if (result.Objects.Count == 0)
        {
            builder.AppendLine("没有匹配对象。");
            return builder.ToString();
        }

        int previewCount = Math.Min(result.Objects.Count, 20);
        builder.AppendLine($"以下展示前 {previewCount} 个匹配对象：");
        for (int i = 0; i < previewCount; i++)
        {
            RhinoObjectInfo objectInfo = result.Objects[i];
            builder.AppendLine($"{i + 1}. {objectInfo.ObjectId}");
            builder.AppendLine($"   - Type: {objectInfo.NormalizedObjectType} ({objectInfo.GeometryTypeName})");
            builder.AppendLine($"   - Layer: {objectInfo.LayerFullPath}");

            if (!string.IsNullOrWhiteSpace(objectInfo.Name))
            {
                builder.AppendLine($"   - Name: {objectInfo.Name}");
            }

            if (objectInfo.UserAttributes.Count > 0)
            {
                string preview = string.Join(", ", objectInfo.UserAttributes.Take(3).Select(entry => $"{entry.Key}={entry.Value}"));
                builder.AppendLine($"   - UserAttributes: {preview}");
            }
        }

        return builder.ToString();
    }

    private static bool MatchesLayer(Layer layer, string query, bool exactMatch)
    {
        if (exactMatch)
        {
            return string.Equals(layer.Name, query, StringComparison.OrdinalIgnoreCase)
                || string.Equals(layer.FullPath, query, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(layer.Name, query, StringComparison.OrdinalIgnoreCase)
            || layer.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private RhinoObjectFilterCriteria CreateCriteria(FilterObjectsRequest request)
    {
        return new RhinoObjectFilterCriteria
        {
            LayerQueries = request.LayerQueries.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            LayerFullPaths = request.ConfirmedLayerFullPaths.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ObjectTypes = request.ObjectTypes.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            UserAttributeConditions = request.UserAttributeConditions
                .Where(condition => !string.IsNullOrWhiteSpace(condition.Key))
                .Select(condition => new RhinoUserAttributeCondition
                {
                    Key = condition.Key,
                    ExpectedValue = condition.ExpectedValue,
                    ComparisonMode = condition.ComparisonMode
                })
                .ToList(),
            MatchMode = request.MatchMode,
            UserAttributeMatchMode = request.UserAttributeMatchMode
        };
    }

    private List<RhinoObjectInfo> BuildObjectInfos(Rhino.FileIO.File3dm model)
    {
        var layerLookup = model.AllLayers
            .Where(layer => !layer.IsDeleted)
            .ToDictionary(layer => layer.Index);

        var objectInfos = new List<RhinoObjectInfo>();
        foreach (var modelObject in model.Objects)
        {
            layerLookup.TryGetValue(modelObject.Attributes.LayerIndex, out Layer? layer);
            var userAttributes = new List<RhinoObjectUserAttributeEntry>();
            var userStrings = modelObject.Attributes.GetUserStrings();
            if (userStrings is not null)
            {
                foreach (string? key in userStrings.AllKeys)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    userAttributes.Add(new RhinoObjectUserAttributeEntry
                    {
                        Key = key,
                        Value = userStrings[key] ?? string.Empty
                    });
                }
            }

            string rawObjectType = modelObject.Geometry?.ObjectType.ToString() ?? "Unknown";
            string geometryTypeName = modelObject.Geometry?.GetType().Name ?? rawObjectType;

            objectInfos.Add(new RhinoObjectInfo
            {
                ObjectId = modelObject.Attributes.ObjectId,
                ObjectTypeName = rawObjectType,
                NormalizedObjectType = NormalizeObjectType(rawObjectType, geometryTypeName),
                GeometryTypeName = geometryTypeName,
                LayerIndex = modelObject.Attributes.LayerIndex,
                LayerName = layer?.Name ?? "Unknown",
                LayerFullPath = layer?.FullPath ?? "Unknown",
                Name = modelObject.Attributes.Name ?? string.Empty,
                UserAttributes = userAttributes
            });
        }

        return objectInfos;
    }

    private bool MatchesAllCriteria(
        RhinoObjectInfo objectInfo,
        RhinoObjectFilterCriteria criteria,
        IReadOnlyList<IObjectFilterCriterionEvaluator> activeEvaluators)
    {
        if (activeEvaluators.Count == 0)
        {
            return false;
        }

        IEnumerable<bool> evaluations = activeEvaluators.Select(evaluator => evaluator.Evaluate(objectInfo, criteria));
        return criteria.MatchMode == FilterMatchMode.All
            ? evaluations.All(result => result)
            : evaluations.Any(result => result);
    }

    private static RhinoObjectType NormalizeObjectType(string rawObjectType, string geometryTypeName)
    {
        if (geometryTypeName.Contains("TextDot", StringComparison.OrdinalIgnoreCase)
            || geometryTypeName.Contains("AnnotationDot", StringComparison.OrdinalIgnoreCase))
        {
            return RhinoObjectType.AnnotationDot;
        }

        return rawObjectType switch
        {
            "Point" => RhinoObjectType.Point,
            "Curve" => RhinoObjectType.Curve,
            "Surface" => RhinoObjectType.Surface,
            "Brep" => RhinoObjectType.Brep,
            "Mesh" => RhinoObjectType.Mesh,
            "InstanceReference" => RhinoObjectType.BlockInstance,
            "Annotation" => RhinoObjectType.Annotation,
            _ => RhinoObjectType.Unknown
        };
    }

    private static string SummarizeCriteria(RhinoObjectFilterCriteria criteria)
    {
        var fragments = new List<string>();
        if (criteria.LayerFullPaths.Count > 0)
        {
            fragments.Add($"LayerFullPaths=[{string.Join(", ", criteria.LayerFullPaths)}]");
        }

        if (criteria.LayerQueries.Count > 0)
        {
            fragments.Add($"LayerQueries=[{string.Join(", ", criteria.LayerQueries)}]");
        }

        if (criteria.ObjectTypes.Count > 0)
        {
            fragments.Add($"ObjectTypes=[{string.Join(", ", criteria.ObjectTypes)}]");
        }

        if (criteria.UserAttributeConditions.Count > 0)
        {
            string conditions = string.Join(", ", criteria.UserAttributeConditions.Select(condition =>
                condition.ComparisonMode switch
                {
                    UserAttributeComparisonMode.Exists => $"{condition.Key} exists",
                    UserAttributeComparisonMode.Contains => $"{condition.Key} contains {condition.ExpectedValue}",
                    _ => $"{condition.Key} = {condition.ExpectedValue}"
                }));
            fragments.Add($"UserAttributes=[{conditions}] ({criteria.UserAttributeMatchMode})");
        }

        fragments.Add($"MatchMode={criteria.MatchMode}");
        return string.Join("; ", fragments);
    }

    private static string SummarizeObjectIds(IReadOnlyList<Guid> objectIds)
    {
        if (objectIds.Count <= 5)
        {
            return $"ConfirmedObjectIds=[{string.Join(", ", objectIds)}]";
        }

        return $"ConfirmedObjectIds.Count={objectIds.Count}";
    }
}

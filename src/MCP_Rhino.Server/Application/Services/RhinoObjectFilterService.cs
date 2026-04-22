extern alias rhinocommon;

using System.Text;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Rhino.DocObjects;
using LiveLayer = rhinocommon::Rhino.DocObjects.Layer;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoObjectFilterService
{
    private readonly IRhinoDocumentRepository _repository;
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly IEnumerable<IObjectFilterCriterionEvaluator> _evaluators;

    public RhinoObjectFilterService(
        IRhinoDocumentRepository repository,
        ILiveRhinoDocumentAccessor documentAccessor,
        IEnumerable<IObjectFilterCriterionEvaluator> evaluators)
    {
        _repository = repository;
        _documentAccessor = documentAccessor;
        _evaluators = evaluators;
    }

    // Live variant: searches the active RhinoDoc's layer table so unsaved layer
    // additions/renames are visible. Object counts reflect the live document.
    public OperationResponse<IReadOnlyList<RhinoLayerCandidate>> FindLayerCandidatesInLive(FindLayerCandidatesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LayerQuery))
        {
            return OperationResponse<IReadOnlyList<RhinoLayerCandidate>>.Fail("LayerQuery cannot be empty.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            var counts = BuildLiveLayerObjectCounts(document);
            var candidates = new List<RhinoLayerCandidate>();
            for (int i = 0; i < document.Layers.Count; i++)
            {
                LiveLayer layer = document.Layers[i];
                if (layer.IsDeleted)
                {
                    continue;
                }

                if (!MatchesLiveLayer(layer, request.LayerQuery, request.ExactMatch))
                {
                    continue;
                }

                counts.TryGetValue(layer.Index, out int objectCount);
                candidates.Add(new RhinoLayerCandidate
                {
                    LayerIndex = layer.Index,
                    LayerName = layer.Name,
                    FullPath = layer.FullPath,
                    ObjectCount = objectCount
                });
            }

            var ordered = candidates
                .OrderBy(layer => layer.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return OperationResponse<IReadOnlyList<RhinoLayerCandidate>>.Ok(
                ordered,
                ordered.Count == 0 ? "No matching layers were found in live document." : $"Found {ordered.Count} matching layers in live document.");
        });
    }

    // Live variant: runs the same filter criteria evaluators against live
    // document state instead of File3dm-on-disk snapshot.
    public OperationResponse<RhinoObjectFilterResult> FilterInLive(FilterObjectsRequest request)
    {
        RhinoObjectFilterCriteria criteria = CreateCriteria(request);
        if (!criteria.HasAnyCriteria())
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail("At least one filter criterion is required.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            List<RhinoObjectInfo> objectInfos = BuildLiveObjectInfos(document);
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
                Warnings = Array.Empty<ObjectEditWarning>(),
                Objects = matchedObjects
            };

            return OperationResponse<RhinoObjectFilterResult>.Ok(
                result, $"Live filter completed. Matched {matchedObjects.Count} of {objectInfos.Count} live objects.");
        });
    }

    // Live variant: resolves each ObjectId against the active RhinoDoc.
    public OperationResponse<RhinoObjectFilterResult> ResolveByObjectIdsInLive(string filePath, IReadOnlyList<Guid> objectIds)
    {
        List<Guid> distinctObjectIds = objectIds
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctObjectIds.Count == 0)
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail("At least one non-empty ObjectId is required.");
        }

        return _documentAccessor.Execute(filePath, document =>
        {
            List<RhinoObjectInfo> objectInfos = BuildLiveObjectInfos(document);
            Dictionary<Guid, RhinoObjectInfo> objectLookup = objectInfos.ToDictionary(info => info.ObjectId);

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
                    $"The following ObjectIds were not found in the live document: {string.Join(", ", missingObjectIds)}");
            }

            var result = new RhinoObjectFilterResult
            {
                FilePath = filePath,
                TotalObjectCount = objectInfos.Count,
                MatchedCount = resolvedObjects.Count,
                CriteriaSummary = SummarizeObjectIds(distinctObjectIds),
                Warnings = Array.Empty<ObjectEditWarning>(),
                Objects = resolvedObjects
            };

            return OperationResponse<RhinoObjectFilterResult>.Ok(
                result, $"Resolved {resolvedObjects.Count} live ObjectIds.");
        });
    }

    public OperationResponse<IReadOnlyList<RhinoLayerCandidate>> FindLayerCandidates(FindLayerCandidatesRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<IReadOnlyList<RhinoLayerCandidate>>.Fail($"File was not found: {request.FilePath}");
        }

        if (string.IsNullOrWhiteSpace(request.LayerQuery))
        {
            return OperationResponse<IReadOnlyList<RhinoLayerCandidate>>.Fail("LayerQuery cannot be empty.");
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

            return OperationResponse<IReadOnlyList<RhinoLayerCandidate>>.Ok(
                candidates,
                candidates.Count == 0 ? "No matching layers were found." : $"Found {candidates.Count} matching layers.");
        }
        catch (Exception ex)
        {
            return OperationResponse<IReadOnlyList<RhinoLayerCandidate>>.Fail($"Layer lookup failed: {ex.Message}");
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
            return OperationResponse<RhinoObjectFilterResult>.Fail($"File was not found: {request.FilePath}");
        }

        RhinoObjectFilterCriteria criteria = CreateCriteria(request);
        if (!criteria.HasAnyCriteria())
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail("At least one filter criterion is required.");
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
                Warnings = CreateOfflineWarnings(request.FilePath),
                Objects = matchedObjects
            };

            return OperationResponse<RhinoObjectFilterResult>.Ok(result, $"Filter completed. Matched {matchedObjects.Count} objects.");
        }
        catch (Exception ex)
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail($"Object filter failed: {ex.Message}");
        }
    }

    public OperationResponse<RhinoObjectFilterResult> ResolveByObjectIds(string filePath, IReadOnlyList<Guid> objectIds)
    {
        if (!_repository.Exists(filePath))
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail($"File was not found: {filePath}");
        }

        List<Guid> distinctObjectIds = objectIds
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctObjectIds.Count == 0)
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail("At least one non-empty ObjectId is required.");
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
                    $"The following ObjectIds were not found in the file: {string.Join(", ", missingObjectIds)}");
            }

            var result = new RhinoObjectFilterResult
            {
                FilePath = filePath,
                TotalObjectCount = objectInfos.Count,
                MatchedCount = resolvedObjects.Count,
                CriteriaSummary = SummarizeObjectIds(distinctObjectIds),
                Warnings = CreateOfflineWarnings(filePath),
                Objects = resolvedObjects
            };

            return OperationResponse<RhinoObjectFilterResult>.Ok(result, $"Resolved {resolvedObjects.Count} explicit ObjectIds.");
        }
        catch (Exception ex)
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail($"ObjectId resolution failed: {ex.Message}");
        }
    }

    public string FormatLayerCandidates(string layerQuery, IReadOnlyList<RhinoLayerCandidate>? candidates, string message)
    {
        if (candidates is null || candidates.Count == 0)
        {
            return string.IsNullOrWhiteSpace(message)
                ? $"No layers matched [{layerQuery}]."
                : message;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Found {candidates.Count} layers matching [{layerQuery}]:");
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
        builder.AppendLine($"- File: {result.FilePath}");
        builder.AppendLine($"- Total objects: {result.TotalObjectCount}");
        builder.AppendLine($"- Matched objects: {result.MatchedCount}");
        builder.AppendLine($"- Criteria: {result.CriteriaSummary}");

        if (result.Warnings.Count > 0)
        {
            builder.AppendLine("- Warnings:");
            foreach (ObjectEditWarning warning in result.Warnings)
            {
                builder.AppendLine($"  - [{warning.Code}] {warning.Message}");
            }
        }

        builder.AppendLine();

        if (result.Objects.Count == 0)
        {
            builder.AppendLine("No matching objects.");
            return builder.ToString();
        }

        int previewCount = Math.Min(result.Objects.Count, 20);
        builder.AppendLine($"Showing the first {previewCount} matched objects:");
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

    private IReadOnlyList<ObjectEditWarning> CreateOfflineWarnings(string filePath)
    {
        if (_documentAccessor.TryGetActiveDocumentState(filePath, out bool hasUnsavedChanges) && hasUnsavedChanges)
        {
            return new[]
            {
                new ObjectEditWarning
                {
                    Code = "OFFLINE_READ_STALE",
                    Message = "The target file is open in Rhino with unsaved changes, so offline read results may be stale."
                }
            };
        }

        return Array.Empty<ObjectEditWarning>();
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

    private static Dictionary<int, int> BuildLiveLayerObjectCounts(RhinoDoc document)
    {
        var counts = new Dictionary<int, int>();
        foreach (var rhinoObject in document.Objects)
        {
            int layerIndex = rhinoObject.Attributes.LayerIndex;
            counts[layerIndex] = counts.TryGetValue(layerIndex, out int existing) ? existing + 1 : 1;
        }
        return counts;
    }

    private static bool MatchesLiveLayer(LiveLayer layer, string query, bool exactMatch)
    {
        if (exactMatch)
        {
            return string.Equals(layer.Name, query, StringComparison.OrdinalIgnoreCase)
                || string.Equals(layer.FullPath, query, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(layer.Name, query, StringComparison.OrdinalIgnoreCase)
            || layer.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private List<RhinoObjectInfo> BuildLiveObjectInfos(RhinoDoc document)
    {
        var layerLookup = new Dictionary<int, LiveLayer>();
        for (int i = 0; i < document.Layers.Count; i++)
        {
            LiveLayer layer = document.Layers[i];
            if (!layer.IsDeleted)
            {
                layerLookup[layer.Index] = layer;
            }
        }

        var objectInfos = new List<RhinoObjectInfo>();
        foreach (var rhinoObject in document.Objects)
        {
            layerLookup.TryGetValue(rhinoObject.Attributes.LayerIndex, out LiveLayer? layer);
            var userAttributes = new List<RhinoObjectUserAttributeEntry>();
            var userStrings = rhinoObject.Attributes.GetUserStrings();
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

            string rawObjectType = rhinoObject.Geometry?.ObjectType.ToString() ?? "Unknown";
            string geometryTypeName = rhinoObject.Geometry?.GetType().Name ?? rawObjectType;

            objectInfos.Add(new RhinoObjectInfo
            {
                ObjectId = rhinoObject.Attributes.ObjectId,
                ObjectTypeName = rawObjectType,
                NormalizedObjectType = NormalizeObjectType(rawObjectType, geometryTypeName),
                GeometryTypeName = geometryTypeName,
                LayerIndex = rhinoObject.Attributes.LayerIndex,
                LayerName = layer?.Name ?? "Unknown",
                LayerFullPath = layer?.FullPath ?? "Unknown",
                Name = rhinoObject.Attributes.Name ?? string.Empty,
                UserAttributes = userAttributes
            });
        }

        return objectInfos;
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

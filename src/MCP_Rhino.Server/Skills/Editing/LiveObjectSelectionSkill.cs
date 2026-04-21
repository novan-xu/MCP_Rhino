extern alias rhinocommon;

using System.Text;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Layer = rhinocommon::Rhino.DocObjects.Layer;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Skills.Editing;

public sealed class LiveObjectSelectionSkill
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly IEnumerable<IObjectFilterCriterionEvaluator> _evaluators;

    public LiveObjectSelectionSkill(
        ILiveRhinoDocumentAccessor documentAccessor,
        IEnumerable<IObjectFilterCriterionEvaluator> evaluators)
    {
        _documentAccessor = documentAccessor;
        _evaluators = evaluators;
    }

    public OperationResponse<RhinoObjectFilterResult> Select(FilterObjectsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail("FilePath cannot be empty.");
        }

        var resolvedRequest = new FilterObjectsRequest
        {
            FilePath = request.FilePath,
            LayerQueries = new List<string>(request.LayerQueries),
            ConfirmedLayerFullPaths = new List<string>(request.ConfirmedLayerFullPaths),
            ObjectTypes = new List<string>(request.ObjectTypes),
            UserAttributeConditions = new List<UserAttributeConditionRequest>(request.UserAttributeConditions),
            MatchMode = request.MatchMode,
            UserAttributeMatchMode = request.UserAttributeMatchMode
        };

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            if (resolvedRequest.LayerQueries.Count > 0)
            {
                OperationResponse<List<string>> resolvedLayers = ResolveLayerQueries(document, resolvedRequest);
                if (!resolvedLayers.Success || resolvedLayers.Data is null)
                {
                    return OperationResponse<RhinoObjectFilterResult>.Fail(resolvedLayers.Message);
                }

                resolvedRequest.ConfirmedLayerFullPaths = resolvedLayers.Data;
                resolvedRequest.LayerQueries.Clear();
            }

            RhinoObjectFilterCriteria criteria = CreateCriteria(resolvedRequest);
            if (!criteria.HasAnyCriteria())
            {
                return OperationResponse<RhinoObjectFilterResult>.Fail("At least one filter criterion is required.");
            }

            List<RhinoObjectInfo> objectInfos = BuildObjectInfos(document);
            var activeEvaluators = _evaluators.Where(evaluator => evaluator.CanEvaluate(criteria)).ToList();
            var matchedObjects = objectInfos
                .Where(objectInfo => MatchesAllCriteria(objectInfo, criteria, activeEvaluators))
                .ToList();

            return OperationResponse<RhinoObjectFilterResult>.Ok(new RhinoObjectFilterResult
            {
                FilePath = request.FilePath,
                TotalObjectCount = objectInfos.Count,
                MatchedCount = matchedObjects.Count,
                CriteriaSummary = SummarizeCriteria(criteria),
                Objects = matchedObjects
            });
        });
    }

    public OperationResponse<RhinoObjectFilterResult> ResolveByObjectIds(string filePath, IReadOnlyList<Guid> objectIds)
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
            List<RhinoObjectInfo> objectInfos = BuildObjectInfos(document);
            Dictionary<Guid, RhinoObjectInfo> lookup = objectInfos.ToDictionary(objectInfo => objectInfo.ObjectId);
            var resolved = new List<RhinoObjectInfo>(distinctObjectIds.Count);
            var missing = new List<Guid>();

            foreach (Guid objectId in distinctObjectIds)
            {
                if (lookup.TryGetValue(objectId, out RhinoObjectInfo? objectInfo))
                {
                    resolved.Add(objectInfo);
                }
                else
                {
                    missing.Add(objectId);
                }
            }

            if (missing.Count > 0)
            {
                return OperationResponse<RhinoObjectFilterResult>.Fail(
                    $"The following ObjectIds were not found in the active document: {string.Join(", ", missing)}");
            }

            return OperationResponse<RhinoObjectFilterResult>.Ok(new RhinoObjectFilterResult
            {
                FilePath = filePath,
                TotalObjectCount = objectInfos.Count,
                MatchedCount = resolved.Count,
                CriteriaSummary = SummarizeObjectIds(distinctObjectIds),
                Objects = resolved
            });
        });
    }

    private static OperationResponse<List<string>> ResolveLayerQueries(RhinoDoc document, FilterObjectsRequest request)
    {
        var resolvedLayers = new List<string>(request.ConfirmedLayerFullPaths);
        var ambiguityMessages = new List<string>();

        foreach (string layerQuery in request.LayerQueries)
        {
            List<Layer> matches = document.Layers
                .Where(layer => !layer.IsDeleted && MatchesLayer(layer, layerQuery, exactMatch: false))
                .OrderBy(layer => layer.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matches.Count == 0)
            {
                ambiguityMessages.Add($"No layer matched [{layerQuery}].");
                continue;
            }

            if (matches.Count == 1)
            {
                resolvedLayers.Add(matches[0].FullPath);
                continue;
            }

            bool alreadyConfirmed = matches.Any(candidate =>
                request.ConfirmedLayerFullPaths.Any(confirmed =>
                    string.Equals(confirmed, candidate.FullPath, StringComparison.OrdinalIgnoreCase)));

            if (!alreadyConfirmed)
            {
                var builder = new StringBuilder();
                builder.AppendLine($"Layer query [{layerQuery}] is ambiguous:");
                foreach (Layer match in matches)
                {
                    builder.AppendLine($"- {match.FullPath}");
                }
                ambiguityMessages.Add(builder.ToString().TrimEnd());
            }
        }

        if (ambiguityMessages.Count > 0)
        {
            return OperationResponse<List<string>>.Fail(string.Join(System.Environment.NewLine, ambiguityMessages));
        }

        return OperationResponse<List<string>>.Ok(
            resolvedLayers.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
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

    private static RhinoObjectFilterCriteria CreateCriteria(FilterObjectsRequest request)
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

    private static List<RhinoObjectInfo> BuildObjectInfos(RhinoDoc document)
    {
        var objectInfos = new List<RhinoObjectInfo>();
        foreach (RhinoObject rhinoObject in document.Objects)
        {
            Layer? layer = document.Layers.FindIndex(rhinoObject.Attributes.LayerIndex);
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
                ObjectId = rhinoObject.Id,
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

    private static bool MatchesAllCriteria(
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

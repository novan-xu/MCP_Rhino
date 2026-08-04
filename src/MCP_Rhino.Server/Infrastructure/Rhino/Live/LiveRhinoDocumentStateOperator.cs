extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using LiveLayer = rhinocommon::Rhino.DocObjects.Layer;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoDocumentStateOperator : ILiveRhinoDocumentStateOperator
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveRhinoDocumentStateOperator(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<DocumentSummaryResponse> GetSummary(GetDocumentSummaryRequest request)
    {
        return _documentAccessor.Execute(request.FilePath, document =>
        {
            List<RhinoObjectInfo> objectInfos = LiveRhinoObjectInfoMapper.BuildObjectInfos(document);
            Dictionary<int, int> layerObjectCounts = BuildLayerObjectCounts(document);
            List<ObjectEditWarning> warnings = new();

            int maxObjectSummaries = Clamp(request.MaxObjectSummaries, 0, 100);
            int maxLayerSummaries = Clamp(request.MaxLayerSummaries, 0, 500);
            int maxNamedViews = Clamp(request.MaxNamedViews, 0, 100);
            int maxMaterials = Clamp(request.MaxMaterials, 0, 100);

            AddTruncationWarning(warnings, "OBJECT_SUMMARIES_TRUNCATED", objectInfos.Count, maxObjectSummaries);

            List<DocumentLayerObjectCountResponse> layerCounts = BuildLayerCounts(document, layerObjectCounts);
            AddTruncationWarning(warnings, "LAYER_SUMMARIES_TRUNCATED", layerCounts.Count, maxLayerSummaries);

            List<string> namedViews = BuildNamedViews(document);
            AddTruncationWarning(warnings, "NAMED_VIEWS_TRUNCATED", namedViews.Count, maxNamedViews);

            List<DocumentMaterialSummaryResponse> materials = BuildMaterialSummaries(document);
            AddTruncationWarning(warnings, "MATERIALS_TRUNCATED", materials.Count, maxMaterials);

            var response = new DocumentSummaryResponse
            {
                FilePath = document.Path,
                DocumentName = Path.GetFileName(document.Path),
                UnitSystem = document.ModelUnitSystem.ToString(),
                AbsoluteTolerance = document.ModelAbsoluteTolerance,
                AngleToleranceDegrees = document.ModelAngleToleranceDegrees,
                ObjectCount = objectInfos.Count,
                LayerCount = CountLiveLayers(document),
                CurrentLayer = CreateCurrentLayerResponse(document, request.FilePath, layerObjectCounts),
                SelectedObjectCount = document.Objects.GetSelectedObjects(true, true).Count(),
                ObjectCountsByType = objectInfos
                    .GroupBy(item => item.NormalizedObjectType)
                    .Select(group => new DocumentObjectTypeCountResponse { ObjectType = group.Key, Count = group.Count() })
                    .OrderBy(item => item.ObjectType.ToString(), StringComparer.Ordinal)
                    .ToList(),
                ObjectCountsByLayer = layerCounts.Take(maxLayerSummaries).ToList(),
                NamedViews = namedViews.Take(maxNamedViews).ToList(),
                Materials = materials.Take(maxMaterials).ToList(),
                ObjectSummaries = objectInfos.Take(maxObjectSummaries).ToList(),
                Warnings = warnings
            };

            return OperationResponse<DocumentSummaryResponse>.Ok(response, "Document summary read from live Rhino document.");
        });
    }

    public OperationResponse<CurrentLayerResponse> GetCurrentLayer(GetCurrentLayerInLiveRequest request)
    {
        return _documentAccessor.Execute(request.FilePath, document =>
        {
            CurrentLayerResponse? currentLayer = CreateCurrentLayerResponse(document, request.FilePath, BuildLayerObjectCounts(document));
            return currentLayer is null
                ? OperationResponse<CurrentLayerResponse>.Fail("CURRENT_LAYER_NOT_FOUND")
                : OperationResponse<CurrentLayerResponse>.Ok(currentLayer, "Current layer read from live Rhino document.");
        });
    }

    public OperationResponse<CurrentLayerMutationResponse> SetCurrentLayer(SetCurrentLayerInLiveRequest request)
    {
        return _documentAccessor.Execute(request.FilePath, document =>
        {
            Dictionary<int, int> beforeCounts = BuildLayerObjectCounts(document);
            CurrentLayerResponse? previousLayer = CreateCurrentLayerResponse(document, request.FilePath, beforeCounts);
            OperationResponse<int> target = ResolveLayerIndex(document, request);
            if (!target.Success)
            {
                return OperationResponse<CurrentLayerMutationResponse>.Fail(target.Message);
            }

            int targetIndex = target.Data;
            if (targetIndex < 0 || targetIndex >= document.Layers.Count || document.Layers[targetIndex].IsDeleted)
            {
                return OperationResponse<CurrentLayerMutationResponse>.Fail("CURRENT_LAYER_TARGET_NOT_FOUND");
            }

            bool changed = document.Layers.CurrentLayerIndex != targetIndex;
            if (changed && !document.Layers.SetCurrentLayerIndex(targetIndex, true))
            {
                return OperationResponse<CurrentLayerMutationResponse>.Fail("SET_CURRENT_LAYER_FAILED");
            }

            Dictionary<int, int> afterCounts = BuildLayerObjectCounts(document);
            CurrentLayerResponse? currentLayer = CreateCurrentLayerResponse(document, request.FilePath, afterCounts);
            if (changed)
            {
                document.Views.Redraw();
            }

            return OperationResponse<CurrentLayerMutationResponse>.Ok(new CurrentLayerMutationResponse
            {
                FilePath = document.Path,
                PreviousLayer = previousLayer,
                CurrentLayer = currentLayer,
                Changed = changed
            }, changed ? "Current layer changed." : "Current layer was already set to the requested layer.");
        });
    }

    private static OperationResponse<int> ResolveLayerIndex(RhinoDoc document, SetCurrentLayerInLiveRequest request)
    {
        if (request.LayerId.HasValue && request.LayerId.Value != Guid.Empty)
        {
            int byId = document.Layers.Find(request.LayerId.Value, true, -1);
            return byId >= 0
                ? OperationResponse<int>.Ok(byId)
                : OperationResponse<int>.Fail("CURRENT_LAYER_TARGET_NOT_FOUND");
        }

        if (!string.IsNullOrWhiteSpace(request.FullPath))
        {
            int byFullPath = document.Layers.FindByFullPath(request.FullPath.Trim(), -1);
            return byFullPath >= 0
                ? OperationResponse<int>.Ok(byFullPath)
                : OperationResponse<int>.Fail("CURRENT_LAYER_TARGET_NOT_FOUND");
        }

        if (!string.IsNullOrWhiteSpace(request.LayerQuery))
        {
            List<LiveLayer> matches = FindLayerQueryMatches(document, request.LayerQuery.Trim(), request.ExactMatch);
            if (matches.Count == 1)
            {
                return OperationResponse<int>.Ok(matches[0].Index);
            }

            if (matches.Count == 0)
            {
                return OperationResponse<int>.Fail("CURRENT_LAYER_TARGET_NOT_FOUND");
            }

            string candidates = string.Join(", ", matches.Take(10).Select(layer => layer.FullPath));
            return OperationResponse<int>.Fail($"CURRENT_LAYER_AMBIGUOUS: {matches.Count} layers matched. Candidates: {candidates}");
        }

        return OperationResponse<int>.Fail("LayerId, FullPath, or LayerQuery is required.");
    }

    private static List<LiveLayer> FindLayerQueryMatches(RhinoDoc document, string query, bool exactMatch)
    {
        var matches = new List<LiveLayer>();
        for (int i = 0; i < document.Layers.Count; i++)
        {
            LiveLayer layer = document.Layers[i];
            if (layer.IsDeleted)
            {
                continue;
            }

            bool matched = exactMatch
                ? string.Equals(layer.Name, query, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(layer.FullPath, query, StringComparison.OrdinalIgnoreCase)
                : string.Equals(layer.Name, query, StringComparison.OrdinalIgnoreCase)
                    || layer.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase);

            if (matched)
            {
                matches.Add(layer);
            }
        }

        return matches.OrderBy(layer => layer.FullPath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static CurrentLayerResponse? CreateCurrentLayerResponse(RhinoDoc document, string filePath, IReadOnlyDictionary<int, int> objectCounts)
    {
        int currentIndex = document.Layers.CurrentLayerIndex;
        if (currentIndex < 0 || currentIndex >= document.Layers.Count)
        {
            return null;
        }

        LiveLayer layer = document.Layers[currentIndex];
        if (layer.IsDeleted)
        {
            return null;
        }

        return new CurrentLayerResponse
        {
            FilePath = document.Path,
            LayerId = layer.Id,
            LayerIndex = layer.Index,
            LayerName = layer.Name,
            FullPath = layer.FullPath,
            Visible = layer.IsVisible,
            Locked = layer.IsLocked,
            ObjectCount = objectCounts.GetValueOrDefault(layer.Index)
        };
    }

    private static Dictionary<int, int> BuildLayerObjectCounts(RhinoDoc document)
    {
        var counts = new Dictionary<int, int>();
        foreach (RhinoObject rhinoObject in document.Objects)
        {
            if (rhinoObject.IsDeleted)
            {
                continue;
            }

            int layerIndex = rhinoObject.Attributes.LayerIndex;
            counts[layerIndex] = counts.GetValueOrDefault(layerIndex) + 1;
        }

        return counts;
    }

    private static List<DocumentLayerObjectCountResponse> BuildLayerCounts(RhinoDoc document, IReadOnlyDictionary<int, int> objectCounts)
    {
        var layers = new List<DocumentLayerObjectCountResponse>();
        for (int i = 0; i < document.Layers.Count; i++)
        {
            LiveLayer layer = document.Layers[i];
            if (layer.IsDeleted)
            {
                continue;
            }

            layers.Add(new DocumentLayerObjectCountResponse
            {
                LayerIndex = layer.Index,
                LayerName = layer.Name,
                FullPath = layer.FullPath,
                ObjectCount = objectCounts.GetValueOrDefault(layer.Index)
            });
        }

        return layers.OrderBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> BuildNamedViews(RhinoDoc document)
    {
        var names = new List<string>();
        for (int i = 0; i < document.NamedViews.Count; i++)
        {
            string? name = document.NamedViews[i]?.Name;
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        return names.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<DocumentMaterialSummaryResponse> BuildMaterialSummaries(RhinoDoc document)
    {
        var materials = new List<DocumentMaterialSummaryResponse>();
        for (int i = 0; i < document.Materials.Count; i++)
        {
            var material = document.Materials[i];
            if (material.IsDeleted)
            {
                continue;
            }

            materials.Add(new DocumentMaterialSummaryResponse
            {
                MaterialIndex = material.Index,
                Name = string.IsNullOrWhiteSpace(material.Name) ? $"Material {material.Index}" : material.Name
            });
        }

        return materials.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static int CountLiveLayers(RhinoDoc document)
    {
        int count = 0;
        for (int i = 0; i < document.Layers.Count; i++)
        {
            if (!document.Layers[i].IsDeleted)
            {
                count++;
            }
        }

        return count;
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(max, Math.Max(min, value));
    }

    private static void AddTruncationWarning(ICollection<ObjectEditWarning> warnings, string code, int totalCount, int maxCount)
    {
        if (totalCount > maxCount)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = code,
                Message = $"Returned {maxCount} of {totalCount} entries."
            });
        }
    }
}

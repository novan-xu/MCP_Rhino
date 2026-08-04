extern alias rhinocommon;

using System.Collections.Specialized;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using AreaMassProperties = rhinocommon::Rhino.Geometry.AreaMassProperties;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using LiveLayer = rhinocommon::Rhino.DocObjects.Layer;
using Mesh = rhinocommon::Rhino.Geometry.Mesh;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Surface = rhinocommon::Rhino.Geometry.Surface;
using VolumeMassProperties = rhinocommon::Rhino.Geometry.VolumeMassProperties;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveTakeoffMetricReader : ILiveTakeoffMetricReader
{
    public OperationResponse<TakeoffLiveSnapshot> Read(RhinoDoc document, string filePath, TakeoffScopeRequest? scope)
    {
        try
        {
            Dictionary<int, LiveLayer> layerLookup = LiveRhinoObjectInfoMapper.BuildLayerLookup(document);
            HashSet<Guid> selectedObjectIds = document.Objects.GetSelectedObjects(true, true)
                .Where(rhinoObject => !rhinoObject.IsDeleted)
                .Select(rhinoObject => rhinoObject.Id)
                .ToHashSet();

            var snapshots = new List<TakeoffObjectSnapshot>();
            int totalCount = 0;
            foreach (RhinoObject rhinoObject in document.Objects)
            {
                if (rhinoObject.IsDeleted)
                {
                    continue;
                }

                totalCount++;
                TakeoffObjectSnapshot snapshot = BuildSnapshot(document, rhinoObject, layerLookup, selectedObjectIds);
                if (MatchesScope(snapshot, scope))
                {
                    snapshots.Add(snapshot);
                }
            }

            return OperationResponse<TakeoffLiveSnapshot>.Ok(new TakeoffLiveSnapshot
            {
                FilePath = document.Path,
                TotalObjectCount = totalCount,
                MatchedObjectCount = snapshots.Count,
                Objects = snapshots
            }, $"Read {snapshots.Count} take-off objects from live Rhino document.");
        }
        catch (Exception ex)
        {
            return OperationResponse<TakeoffLiveSnapshot>.Fail($"TAKEOFF_LIVE_READ_FAILED: {ex.Message}");
        }
    }

    private static TakeoffObjectSnapshot BuildSnapshot(
        RhinoDoc document,
        RhinoObject rhinoObject,
        IReadOnlyDictionary<int, LiveLayer> layerLookup,
        IReadOnlySet<Guid> selectedObjectIds)
    {
        RhinoObjectInfo info = LiveRhinoObjectInfoMapper.BuildObjectInfo(rhinoObject, layerLookup);
        GeometryBase? geometry = rhinoObject.Geometry;
        return new TakeoffObjectSnapshot
        {
            ObjectId = info.ObjectId,
            ObjectName = info.Name,
            ObjectType = info.NormalizedObjectType.ToString(),
            GeometryType = info.GeometryTypeName,
            LayerName = info.LayerName,
            LayerFullPath = info.LayerFullPath,
            MaterialName = ResolveMaterialName(document, rhinoObject),
            IsSelected = selectedObjectIds.Contains(rhinoObject.Id),
            UserText = BuildUserTextDictionary(rhinoObject.Attributes.GetUserStrings()),
            Metrics = geometry is null ? new TakeoffGeometryMetrics() : BuildMetrics(geometry)
        };
    }

    private static IReadOnlyDictionary<string, string> BuildUserTextDictionary(NameValueCollection? userStrings)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (userStrings is null)
        {
            return values;
        }

        foreach (string? key in userStrings.AllKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            values[key] = userStrings[key] ?? string.Empty;
        }

        return values;
    }

    private static TakeoffGeometryMetrics BuildMetrics(GeometryBase geometry)
    {
        var metrics = new TakeoffGeometryMetrics();
        try
        {
            var boundingBox = geometry.GetBoundingBox(true);
            if (boundingBox.IsValid)
            {
                metrics.BoundingBoxSizeX = Math.Abs(boundingBox.Max.X - boundingBox.Min.X);
                metrics.BoundingBoxSizeY = Math.Abs(boundingBox.Max.Y - boundingBox.Min.Y);
                metrics.BoundingBoxSizeZ = Math.Abs(boundingBox.Max.Z - boundingBox.Min.Z);
            }

            switch (geometry)
            {
                case Curve curve:
                    metrics.Length = curve.GetLength();
                    break;
                case Surface surface:
                    using (AreaMassProperties? area = AreaMassProperties.Compute(surface))
                    {
                        metrics.Area = area?.Area;
                    }
                    break;
                case Brep brep:
                    using (AreaMassProperties? area = AreaMassProperties.Compute(brep))
                    {
                        metrics.Area = area?.Area;
                    }

                    if (brep.IsSolid)
                    {
                        using VolumeMassProperties? volume = VolumeMassProperties.Compute(brep);
                        metrics.Volume = volume?.Volume;
                    }
                    break;
                case Mesh mesh:
                    using (AreaMassProperties? area = AreaMassProperties.Compute(mesh))
                    {
                        metrics.Area = area?.Area;
                    }

                    if (mesh.IsClosed)
                    {
                        using VolumeMassProperties? volume = VolumeMassProperties.Compute(mesh);
                        metrics.Volume = volume?.Volume;
                    }
                    break;
            }
        }
        catch
        {
            return metrics;
        }

        return metrics;
    }

    private static string ResolveMaterialName(RhinoDoc document, RhinoObject rhinoObject)
    {
        int materialIndex = rhinoObject.Attributes.MaterialIndex;
        if (materialIndex >= 0 && materialIndex < document.Materials.Count)
        {
            string? name = document.Materials[materialIndex].Name;
            return name ?? string.Empty;
        }

        return string.Empty;
    }

    private static bool MatchesScope(TakeoffObjectSnapshot snapshot, TakeoffScopeRequest? scope)
    {
        if (scope is null)
        {
            return true;
        }

        var checks = new List<bool>();
        if (scope.ObjectIds.Count > 0)
        {
            checks.Add(scope.ObjectIds.Contains(snapshot.ObjectId));
        }

        if (scope.UseCurrentSelection)
        {
            checks.Add(snapshot.IsSelected);
        }

        if (scope.ConfirmedLayerFullPaths.Count > 0)
        {
            checks.Add(scope.ConfirmedLayerFullPaths.Any(layer =>
                string.Equals(layer, snapshot.LayerFullPath, StringComparison.OrdinalIgnoreCase)));
        }

        if (scope.ObjectTypes.Count > 0)
        {
            checks.Add(scope.ObjectTypes.Any(objectType =>
                string.Equals(objectType, snapshot.ObjectType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(objectType, snapshot.GeometryType, StringComparison.OrdinalIgnoreCase)));
        }

        if (scope.UserAttributeConditions.Count > 0)
        {
            checks.Add(MatchesUserAttributes(snapshot, scope.UserAttributeConditions, scope.UserAttributeMatchMode));
        }

        if (checks.Count == 0)
        {
            return true;
        }

        return scope.MatchMode == FilterMatchMode.All
            ? checks.All(check => check)
            : checks.Any(check => check);
    }

    private static bool MatchesUserAttributes(
        TakeoffObjectSnapshot snapshot,
        IReadOnlyList<UserAttributeConditionRequest> conditions,
        FilterMatchMode matchMode)
    {
        IEnumerable<bool> results = conditions
            .Where(condition => !string.IsNullOrWhiteSpace(condition.Key))
            .Select(condition => MatchesUserAttribute(snapshot.UserText, condition));

        return matchMode == FilterMatchMode.All
            ? results.All(result => result)
            : results.Any(result => result);
    }

    private static bool MatchesUserAttribute(
        IReadOnlyDictionary<string, string> userText,
        UserAttributeConditionRequest condition)
    {
        bool hasValue = userText.TryGetValue(condition.Key, out string? actualValue);
        return condition.ComparisonMode switch
        {
            UserAttributeComparisonMode.Exists => hasValue,
            UserAttributeComparisonMode.Contains => hasValue
                && actualValue is not null
                && actualValue.Contains(condition.ExpectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            _ => hasValue
                && string.Equals(actualValue, condition.ExpectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase)
        };
    }
}

extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using BrepFace = rhinocommon::Rhino.Geometry.BrepFace;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using GeometryMetadataSnapshot = MCP_Rhino.Server.Domain.Models.GeometryMetadataSnapshot;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Surface = rhinocommon::Rhino.Geometry.Surface;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveSurfaceDirectionTweakService : ILiveSurfaceDirectionTweakService
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly IGeometryMetadataOperator _metadataOperator;

    public LiveSurfaceDirectionTweakService(
        ILiveRhinoDocumentAccessor documentAccessor,
        IGeometryMetadataOperator metadataOperator)
    {
        _documentAccessor = documentAccessor;
        _metadataOperator = metadataOperator;
    }

    public OperationResponse<IReadOnlyList<SurfaceDirectionTweakPreviewItem>> Preview(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        IReadOnlyList<SurfaceDirectionTweakKind> operations)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            var results = new List<SurfaceDirectionTweakPreviewItem>();
            foreach (Guid objectId in objectIds)
            {
                OperationResponse<PreparedSurfaceDirectionTweak> prepared = Prepare(document, objectId, operations);
                if (!prepared.Success || prepared.Data is null)
                {
                    results.Add(new SurfaceDirectionTweakPreviewItem
                    {
                        ObjectId = objectId,
                        Operations = operations.ToList(),
                        Skipped = true,
                        SkipReason = prepared.Message
                    });
                    continue;
                }

                results.Add(new SurfaceDirectionTweakPreviewItem
                {
                    ObjectId = objectId,
                    GeometryKind = prepared.Data.GeometryKind,
                    Operations = prepared.Data.Operations,
                    Before = prepared.Data.Before,
                    After = prepared.Data.After,
                    Warnings = prepared.Data.Warnings
                });
            }

            return OperationResponse<IReadOnlyList<SurfaceDirectionTweakPreviewItem>>.Ok(results);
        });
    }

    public OperationResponse<IReadOnlyList<SurfaceDirectionTweakApplyItem>> Apply(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        IReadOnlyList<SurfaceDirectionTweakKind> operations,
        string undoRecordName)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, undoRecordName, document =>
        {
            var preparedItems = new List<PreparedSurfaceDirectionTweak>();
            var results = new List<SurfaceDirectionTweakApplyItem>();

            foreach (Guid objectId in objectIds)
            {
                OperationResponse<PreparedSurfaceDirectionTweak> prepared = Prepare(document, objectId, operations);
                if (!prepared.Success || prepared.Data is null)
                {
                    results.Add(new SurfaceDirectionTweakApplyItem
                    {
                        OriginalObjectId = objectId,
                        ObjectId = objectId,
                        UndoRecordName = undoRecordName,
                        Operations = operations.ToList(),
                        Skipped = true,
                        SkipReason = prepared.Message
                    });
                    continue;
                }

                preparedItems.Add(prepared.Data);
            }

            if (preparedItems.Count == 0)
            {
                return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceDirectionTweakApplyItem> Result)>.Ok((false, results));
            }

            var snapshots = new Dictionary<Guid, GeometryMetadataSnapshot>();
            var originals = new Dictionary<Guid, GeometryBase>();
            foreach (PreparedSurfaceDirectionTweak prepared in preparedItems)
            {
                RhinoObject? rhinoObject = document.Objects.FindId(prepared.ObjectId);
                if (rhinoObject?.Geometry is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceDirectionTweakApplyItem> Result)>.Fail($"OBJECT_NOT_FOUND: {prepared.ObjectId}");
                }

                GeometryBase? original = rhinoObject.Geometry.Duplicate();
                if (original is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceDirectionTweakApplyItem> Result)>.Fail("APPLY_FAILED_ROLLED_BACK: failed to duplicate original geometry.");
                }

                originals[prepared.ObjectId] = original;
                snapshots[prepared.ObjectId] = _metadataOperator.Snapshot(rhinoObject);
            }

            foreach (PreparedSurfaceDirectionTweak prepared in preparedItems)
            {
                GeometryBase? replacement = prepared.Geometry.Duplicate();
                if (replacement is null)
                {
                    string rollback = RollBackMany(document, originals, snapshots);
                    return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceDirectionTweakApplyItem> Result)>.Fail(
                        $"APPLY_FAILED_ROLLED_BACK: failed to duplicate tweaked geometry.{rollback}");
                }

                if (!document.Objects.Replace(prepared.ObjectId, replacement, false))
                {
                    string rollback = RollBackMany(document, originals, snapshots);
                    return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceDirectionTweakApplyItem> Result)>.Fail(
                        $"APPLY_FAILED_ROLLED_BACK: replace failed for {prepared.ObjectId}{rollback}");
                }

                OperationResponse<IReadOnlyList<ObjectEditWarning>> replay = _metadataOperator.Replay(
                    document,
                    prepared.ObjectId,
                    snapshots[prepared.ObjectId]);
                if (!replay.Success)
                {
                    string rollback = RollBackMany(document, originals, snapshots);
                    return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceDirectionTweakApplyItem> Result)>.Fail(
                        $"APPLY_FAILED_ROLLED_BACK: {replay.Message}{rollback}");
                }

                IReadOnlyList<ObjectEditWarning> warnings = CombineWarnings(
                    prepared.Warnings,
                    replay.Data ?? Array.Empty<ObjectEditWarning>());
                results.Add(new SurfaceDirectionTweakApplyItem
                {
                    OriginalObjectId = prepared.ObjectId,
                    ObjectId = prepared.ObjectId,
                    GeometryKind = prepared.GeometryKind,
                    UndoRecordName = undoRecordName,
                    Operations = prepared.Operations,
                    Before = prepared.Before,
                    After = prepared.After,
                    MetadataDropped = warnings
                        .Where(warning => string.Equals(warning.Code, "METADATA_FIELDS_DROPPED", StringComparison.Ordinal))
                        .Select(warning => warning.Message)
                        .ToList(),
                    Warnings = warnings
                });
            }

            document.Views.Redraw();
            return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceDirectionTweakApplyItem> Result)>.Ok((true, results));
        });
    }

    private static OperationResponse<PreparedSurfaceDirectionTweak> Prepare(
        RhinoDoc document,
        Guid objectId,
        IReadOnlyList<SurfaceDirectionTweakKind> operations)
    {
        RhinoObject? rhinoObject = document.Objects.FindId(objectId);
        if (rhinoObject?.Geometry is null)
        {
            return OperationResponse<PreparedSurfaceDirectionTweak>.Fail($"OBJECT_NOT_FOUND: {objectId}");
        }

        return rhinoObject.Geometry switch
        {
            Surface surface => PrepareSurface(objectId, surface, operations),
            Brep brep => PrepareBrep(objectId, brep, operations),
            _ => OperationResponse<PreparedSurfaceDirectionTweak>.Fail($"EDITABLE_KIND_UNSUPPORTED: {rhinoObject.Geometry.GetType().Name}")
        };
    }

    private static OperationResponse<PreparedSurfaceDirectionTweak> PrepareSurface(
        Guid objectId,
        Surface surface,
        IReadOnlyList<SurfaceDirectionTweakKind> operations)
    {
        GeometryBase? duplicateGeometry = surface.Duplicate();
        if (duplicateGeometry is not Surface duplicate)
        {
            return OperationResponse<PreparedSurfaceDirectionTweak>.Fail("SURFACE_DIRECTION_TWEAK_DUPLICATE_FAILED");
        }

        SurfaceDirectionSnapshot before = LiveSurfaceDirectionSnapshotFactory.Create(surface, surface.GetType().Name);
        ApplyOperations(duplicate, operations);
        if (!duplicate.IsValid)
        {
            return OperationResponse<PreparedSurfaceDirectionTweak>.Fail("TWEAKED_GEOMETRY_INVALID");
        }

        return OperationResponse<PreparedSurfaceDirectionTweak>.Ok(new PreparedSurfaceDirectionTweak
        {
            ObjectId = objectId,
            GeometryKind = surface.GetType().Name,
            Operations = operations.ToList(),
            Geometry = duplicate,
            Before = before,
            After = LiveSurfaceDirectionSnapshotFactory.Create(duplicate, duplicate.GetType().Name)
        });
    }

    private static OperationResponse<PreparedSurfaceDirectionTweak> PrepareBrep(
        Guid objectId,
        Brep brep,
        IReadOnlyList<SurfaceDirectionTweakKind> operations)
    {
        if (brep.Faces.Count != 1)
        {
            return OperationResponse<PreparedSurfaceDirectionTweak>.Fail($"SURFACE_DIRECTION_TWEAK_REQUIRES_SINGLE_FACE_BREP: face count {brep.Faces.Count}");
        }

        BrepFace sourceFace = brep.Faces[0];
        Brep? duplicate = brep.DuplicateBrep();
        if (duplicate is null || duplicate.Faces.Count != 1)
        {
            return OperationResponse<PreparedSurfaceDirectionTweak>.Fail("SURFACE_DIRECTION_TWEAK_DUPLICATE_FAILED");
        }

        SurfaceDirectionSnapshot before = LiveSurfaceDirectionSnapshotFactory.Create(brep, nameof(Brep));
        BrepFace targetFace = duplicate.Faces[0];
        ApplyOperations(targetFace, operations);
        if (!duplicate.IsValid)
        {
            return OperationResponse<PreparedSurfaceDirectionTweak>.Fail("TWEAKED_GEOMETRY_INVALID");
        }

        return OperationResponse<PreparedSurfaceDirectionTweak>.Ok(new PreparedSurfaceDirectionTweak
        {
            ObjectId = objectId,
            GeometryKind = nameof(Brep),
            Operations = operations.ToList(),
            Geometry = duplicate,
            Before = before,
            After = LiveSurfaceDirectionSnapshotFactory.Create(duplicate, nameof(Brep))
        });
    }

    private static void ApplyOperations(
        Surface surface,
        IReadOnlyList<SurfaceDirectionTweakKind> operations)
    {
        foreach (SurfaceDirectionTweakKind operation in operations)
        {
            switch (operation)
            {
                case SurfaceDirectionTweakKind.ReverseU:
                    surface.Reverse(0, true);
                    break;
                case SurfaceDirectionTweakKind.ReverseV:
                    surface.Reverse(1, true);
                    break;
                case SurfaceDirectionTweakKind.SwapUV:
                    surface.Transpose(true);
                    break;
                case SurfaceDirectionTweakKind.FlipNormal:
                    if (surface is BrepFace face)
                    {
                        face.OrientationIsReversed = !face.OrientationIsReversed;
                    }
                    else
                    {
                        surface.Reverse(0, true);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unsupported surface direction tweak: {operation}");
            }
        }
    }

    private string RollBackMany(
        RhinoDoc document,
        IReadOnlyDictionary<Guid, GeometryBase> originalGeometry,
        IReadOnlyDictionary<Guid, GeometryMetadataSnapshot> snapshots)
    {
        bool ok = true;
        foreach ((Guid objectId, GeometryBase geometry) in originalGeometry)
        {
            ok &= document.Objects.Replace(objectId, geometry, false);
            if (snapshots.TryGetValue(objectId, out GeometryMetadataSnapshot? snapshot))
            {
                ok &= _metadataOperator.Replay(document, objectId, snapshot).Success;
            }
        }

        return ok
            ? string.Empty
            : " Rollback attempted but could not fully restore every object.";
    }

    private static IReadOnlyList<ObjectEditWarning> CombineWarnings(params IEnumerable<ObjectEditWarning>[] warnings)
    {
        return warnings
            .SelectMany(source => source)
            .ToList();
    }

    private sealed class PreparedSurfaceDirectionTweak
    {
        public Guid ObjectId { get; set; }
        public string GeometryKind { get; set; } = string.Empty;
        public IReadOnlyList<SurfaceDirectionTweakKind> Operations { get; set; } = Array.Empty<SurfaceDirectionTweakKind>();
        public GeometryBase Geometry { get; set; } = null!;
        public SurfaceDirectionSnapshot Before { get; set; } = new();
        public SurfaceDirectionSnapshot After { get; set; } = new();
        public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    }
}

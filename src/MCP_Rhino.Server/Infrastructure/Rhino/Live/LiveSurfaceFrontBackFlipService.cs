extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using GeometryMetadataSnapshot = MCP_Rhino.Server.Domain.Models.GeometryMetadataSnapshot;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Surface = rhinocommon::Rhino.Geometry.Surface;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveSurfaceFrontBackFlipService : ILiveSurfaceFrontBackFlipService
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly IGeometryMetadataOperator _metadataOperator;

    public LiveSurfaceFrontBackFlipService(
        ILiveRhinoDocumentAccessor documentAccessor,
        IGeometryMetadataOperator metadataOperator)
    {
        _documentAccessor = documentAccessor;
        _metadataOperator = metadataOperator;
    }

    public OperationResponse<IReadOnlyList<SurfaceFrontBackFlipApplyItem>> Apply(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        string undoRecordName)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, undoRecordName, document =>
        {
            var preparedItems = new List<PreparedSurfaceFrontBackFlip>();
            var results = new List<SurfaceFrontBackFlipApplyItem>();

            foreach (Guid objectId in objectIds)
            {
                OperationResponse<PreparedSurfaceFrontBackFlip> prepared = Prepare(document, objectId);
                if (!prepared.Success || prepared.Data is null)
                {
                    results.Add(new SurfaceFrontBackFlipApplyItem
                    {
                        OriginalObjectId = objectId,
                        ObjectId = objectId,
                        UndoRecordName = undoRecordName,
                        Skipped = true,
                        SkipReason = prepared.Message
                    });
                    continue;
                }

                preparedItems.Add(prepared.Data);
            }

            if (preparedItems.Count == 0)
            {
                return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceFrontBackFlipApplyItem> Result)>.Ok((false, results));
            }

            var snapshots = new Dictionary<Guid, GeometryMetadataSnapshot>();
            var originals = new Dictionary<Guid, GeometryBase>();
            foreach (PreparedSurfaceFrontBackFlip prepared in preparedItems)
            {
                RhinoObject? rhinoObject = document.Objects.FindId(prepared.ObjectId);
                if (rhinoObject?.Geometry is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceFrontBackFlipApplyItem> Result)>.Fail($"OBJECT_NOT_FOUND: {prepared.ObjectId}");
                }

                GeometryBase? original = rhinoObject.Geometry.Duplicate();
                if (original is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceFrontBackFlipApplyItem> Result)>.Fail("APPLY_FAILED_ROLLED_BACK: failed to duplicate original geometry.");
                }

                originals[prepared.ObjectId] = original;
                snapshots[prepared.ObjectId] = _metadataOperator.Snapshot(rhinoObject);
            }

            foreach (PreparedSurfaceFrontBackFlip prepared in preparedItems)
            {
                GeometryBase? replacement = prepared.Geometry.Duplicate();
                if (replacement is null)
                {
                    string rollback = RollBackMany(document, originals, snapshots);
                    return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceFrontBackFlipApplyItem> Result)>.Fail(
                        $"APPLY_FAILED_ROLLED_BACK: failed to duplicate flipped geometry.{rollback}");
                }

                if (!document.Objects.Replace(prepared.ObjectId, replacement, false))
                {
                    string rollback = RollBackMany(document, originals, snapshots);
                    return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceFrontBackFlipApplyItem> Result)>.Fail(
                        $"APPLY_FAILED_ROLLED_BACK: replace failed for {prepared.ObjectId}{rollback}");
                }

                OperationResponse<IReadOnlyList<ObjectEditWarning>> replay = _metadataOperator.Replay(
                    document,
                    prepared.ObjectId,
                    snapshots[prepared.ObjectId]);
                if (!replay.Success)
                {
                    string rollback = RollBackMany(document, originals, snapshots);
                    return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceFrontBackFlipApplyItem> Result)>.Fail(
                        $"APPLY_FAILED_ROLLED_BACK: {replay.Message}{rollback}");
                }

                IReadOnlyList<ObjectEditWarning> warnings = replay.Data ?? Array.Empty<ObjectEditWarning>();
                results.Add(new SurfaceFrontBackFlipApplyItem
                {
                    OriginalObjectId = prepared.ObjectId,
                    ObjectId = prepared.ObjectId,
                    GeometryKind = prepared.GeometryKind,
                    UndoRecordName = undoRecordName,
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
            return OperationResponse<(bool Mutated, IReadOnlyList<SurfaceFrontBackFlipApplyItem> Result)>.Ok((true, results));
        });
    }

    private static OperationResponse<PreparedSurfaceFrontBackFlip> Prepare(RhinoDoc document, Guid objectId)
    {
        RhinoObject? rhinoObject = document.Objects.FindId(objectId);
        if (rhinoObject?.Geometry is null)
        {
            return OperationResponse<PreparedSurfaceFrontBackFlip>.Fail($"OBJECT_NOT_FOUND: {objectId}");
        }

        return rhinoObject.Geometry switch
        {
            Surface surface => PrepareSurface(objectId, surface),
            Brep brep => PrepareBrep(objectId, brep),
            _ => OperationResponse<PreparedSurfaceFrontBackFlip>.Fail($"EDITABLE_KIND_UNSUPPORTED: {rhinoObject.Geometry.GetType().Name}")
        };
    }

    private static OperationResponse<PreparedSurfaceFrontBackFlip> PrepareSurface(Guid objectId, Surface surface)
    {
        GeometryBase? duplicateGeometry = surface.Duplicate();
        if (duplicateGeometry is not Surface duplicate)
        {
            return OperationResponse<PreparedSurfaceFrontBackFlip>.Fail("SURFACE_FRONT_BACK_FLIP_DUPLICATE_FAILED");
        }

        SurfaceDirectionSnapshot before = LiveSurfaceDirectionSnapshotFactory.Create(surface, surface.GetType().Name);
        duplicate.Reverse(0, true);
        if (!duplicate.IsValid)
        {
            return OperationResponse<PreparedSurfaceFrontBackFlip>.Fail("FLIPPED_GEOMETRY_INVALID");
        }

        return OperationResponse<PreparedSurfaceFrontBackFlip>.Ok(new PreparedSurfaceFrontBackFlip
        {
            ObjectId = objectId,
            GeometryKind = surface.GetType().Name,
            Geometry = duplicate,
            Before = before,
            After = LiveSurfaceDirectionSnapshotFactory.Create(duplicate, duplicate.GetType().Name)
        });
    }

    private static OperationResponse<PreparedSurfaceFrontBackFlip> PrepareBrep(Guid objectId, Brep brep)
    {
        Brep? duplicate = brep.DuplicateBrep();
        if (duplicate is null)
        {
            return OperationResponse<PreparedSurfaceFrontBackFlip>.Fail("SURFACE_FRONT_BACK_FLIP_DUPLICATE_FAILED");
        }

        SurfaceDirectionSnapshot before = LiveSurfaceDirectionSnapshotFactory.Create(brep, nameof(Brep));
        duplicate.Flip();
        if (!duplicate.IsValid)
        {
            return OperationResponse<PreparedSurfaceFrontBackFlip>.Fail("FLIPPED_GEOMETRY_INVALID");
        }

        return OperationResponse<PreparedSurfaceFrontBackFlip>.Ok(new PreparedSurfaceFrontBackFlip
        {
            ObjectId = objectId,
            GeometryKind = nameof(Brep),
            Geometry = duplicate,
            Before = before,
            After = LiveSurfaceDirectionSnapshotFactory.Create(duplicate, nameof(Brep))
        });
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

    private sealed class PreparedSurfaceFrontBackFlip
    {
        public Guid ObjectId { get; set; }
        public string GeometryKind { get; set; } = string.Empty;
        public GeometryBase Geometry { get; set; } = null!;
        public SurfaceDirectionSnapshot Before { get; set; } = new();
        public SurfaceDirectionSnapshot After { get; set; } = new();
    }
}

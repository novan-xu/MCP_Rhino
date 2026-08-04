extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveGeometryMutationService : IGeometryMutationService
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly IGeometryMetadataOperator _metadataOperator;

    public LiveGeometryMutationService(
        ILiveRhinoDocumentAccessor documentAccessor,
        IGeometryMetadataOperator metadataOperator)
    {
        _documentAccessor = documentAccessor;
        _metadataOperator = metadataOperator;
    }

    public OperationResponse<IReadOnlyList<ObjectEditWarning>> ReplaceWithMetadata(
        string filePath,
        Guid objectId,
        GeometryBase geometry,
        string undoRecordName)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, undoRecordName, document =>
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject?.Geometry is null)
            {
                return OperationResponse<(bool Mutated, IReadOnlyList<ObjectEditWarning> Result)>.Fail($"OBJECT_NOT_FOUND: {objectId}");
            }

            GeometryBase? originalGeometry = rhinoObject.Geometry.Duplicate();
            GeometryBase? replacementGeometry = geometry.Duplicate();
            if (originalGeometry is null || replacementGeometry is null)
            {
                return OperationResponse<(bool Mutated, IReadOnlyList<ObjectEditWarning> Result)>.Fail("APPLY_FAILED_ROLLED_BACK: failed to duplicate geometry.");
            }

            GeometryMetadataSnapshot metadataSnapshot = _metadataOperator.Snapshot(rhinoObject);
            if (!document.Objects.Replace(objectId, replacementGeometry, false))
            {
                return OperationResponse<(bool Mutated, IReadOnlyList<ObjectEditWarning> Result)>.Fail($"APPLY_FAILED_ROLLED_BACK: replace failed for {objectId}");
            }

            OperationResponse<IReadOnlyList<ObjectEditWarning>> replay = _metadataOperator.Replay(document, objectId, metadataSnapshot);
            if (!replay.Success)
            {
                string rollbackMessage = RollBack(document, objectId, originalGeometry, metadataSnapshot);
                return OperationResponse<(bool Mutated, IReadOnlyList<ObjectEditWarning> Result)>.Fail(
                    $"APPLY_FAILED_ROLLED_BACK: {replay.Message}{rollbackMessage}");
            }

            document.Views.Redraw();
            return OperationResponse<(bool Mutated, IReadOnlyList<ObjectEditWarning> Result)>.Ok(
                (true, replay.Data ?? Array.Empty<ObjectEditWarning>()),
                "Geometry replaced with metadata replay.");
        });
    }

    public OperationResponse<IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEditWarning>>> ReplaceManyWithMetadata(
        string filePath,
        IReadOnlyList<GeometryReplacementWithMetadata> replacements,
        string undoRecordName)
    {
        if (replacements.Count == 0)
        {
            return OperationResponse<IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEditWarning>>>.Fail("At least one geometry replacement is required.");
        }

        return _documentAccessor.ExecuteWithUndo(filePath, undoRecordName, document =>
        {
            var snapshots = new Dictionary<Guid, GeometryMetadataSnapshot>();
            var originalGeometry = new Dictionary<Guid, GeometryBase>();
            var warnings = new Dictionary<Guid, IReadOnlyList<ObjectEditWarning>>();

            foreach (GeometryReplacementWithMetadata replacement in replacements)
            {
                RhinoObject? rhinoObject = document.Objects.FindId(replacement.ObjectId);
                if (rhinoObject?.Geometry is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEditWarning>> Result)>.Fail($"OBJECT_NOT_FOUND: {replacement.ObjectId}");
                }

                GeometryBase? original = rhinoObject.Geometry.Duplicate();
                if (original is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEditWarning>> Result)>.Fail("APPLY_FAILED_ROLLED_BACK: failed to duplicate original geometry.");
                }

                snapshots[replacement.ObjectId] = _metadataOperator.Snapshot(rhinoObject);
                originalGeometry[replacement.ObjectId] = original;
            }

            foreach (GeometryReplacementWithMetadata replacement in replacements)
            {
                GeometryBase? duplicate = replacement.Geometry.Duplicate();
                if (duplicate is null)
                {
                    string rollback = RollBackMany(document, originalGeometry, snapshots);
                    return OperationResponse<(bool Mutated, IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEditWarning>> Result)>.Fail(
                        $"APPLY_FAILED_ROLLED_BACK: failed to duplicate replacement geometry.{rollback}");
                }

                if (!document.Objects.Replace(replacement.ObjectId, duplicate, false))
                {
                    string rollback = RollBackMany(document, originalGeometry, snapshots);
                    return OperationResponse<(bool Mutated, IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEditWarning>> Result)>.Fail(
                        $"APPLY_FAILED_ROLLED_BACK: replace failed for {replacement.ObjectId}{rollback}");
                }

                OperationResponse<IReadOnlyList<ObjectEditWarning>> replay = _metadataOperator.Replay(
                    document,
                    replacement.ObjectId,
                    snapshots[replacement.ObjectId]);
                if (!replay.Success)
                {
                    string rollback = RollBackMany(document, originalGeometry, snapshots);
                    return OperationResponse<(bool Mutated, IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEditWarning>> Result)>.Fail(
                        $"APPLY_FAILED_ROLLED_BACK: {replay.Message}{rollback}");
                }

                warnings[replacement.ObjectId] = replay.Data ?? Array.Empty<ObjectEditWarning>();
            }

            document.Views.Redraw();
            return OperationResponse<(bool Mutated, IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEditWarning>> Result)>.Ok(
                (true, warnings),
                "Geometry batch replaced with metadata replay.");
        });
    }

    private string RollBack(
        rhinocommon::Rhino.RhinoDoc document,
        Guid objectId,
        GeometryBase originalGeometry,
        GeometryMetadataSnapshot metadataSnapshot)
    {
        bool geometryRestored = document.Objects.Replace(objectId, originalGeometry, false);
        OperationResponse<IReadOnlyList<ObjectEditWarning>> metadataRestored = _metadataOperator.Replay(document, objectId, metadataSnapshot);
        return geometryRestored && metadataRestored.Success
            ? string.Empty
            : " Rollback attempted but could not fully restore the object.";
    }

    private string RollBackMany(
        rhinocommon::Rhino.RhinoDoc document,
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
}

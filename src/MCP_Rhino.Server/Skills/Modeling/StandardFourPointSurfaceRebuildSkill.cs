using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Domain.Rules;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class StandardFourPointSurfaceRebuildSkill
{
    private readonly ISurfaceRebuildOrchestrator _rebuildOrchestrator;
    private readonly ISurfaceFrontBackFlipOrchestrator _frontBackFlipOrchestrator;
    private readonly ISurfaceDirectionTweakOrchestrator _directionTweakOrchestrator;

    public StandardFourPointSurfaceRebuildSkill(
        ISurfaceRebuildOrchestrator rebuildOrchestrator,
        ISurfaceFrontBackFlipOrchestrator frontBackFlipOrchestrator,
        ISurfaceDirectionTweakOrchestrator directionTweakOrchestrator)
    {
        _rebuildOrchestrator = rebuildOrchestrator;
        _frontBackFlipOrchestrator = frontBackFlipOrchestrator;
        _directionTweakOrchestrator = directionTweakOrchestrator;
    }

    public OperationResponse<StandardFourPointSurfaceRebuildResponse> Apply(
        string filePath,
        IReadOnlyList<Guid> objectIds)
    {
        SurfaceRebuildSpec spec = CreateStandardRebuildSpec();
        OperationResponse<SurfacePointOrderApplyResponse> rebuild = _rebuildOrchestrator.Apply(new ApplyRedefineSurfacePointOrderRequest
        {
            FilePath = filePath,
            ConfirmedObjectIds = objectIds.ToList(),
            Spec = spec,
            ReplaceOriginal = true
        });
        if (!rebuild.Success || rebuild.Data is null)
        {
            return OperationResponse<StandardFourPointSurfaceRebuildResponse>.Fail(rebuild.Message);
        }

        IReadOnlyList<SurfaceDirectionTweakKind> directionOperations = SurfaceDirectionTweakDefaults.StandardPostRebuildOperations();
        List<Guid> rebuiltObjectIds = rebuild.Data.Results
            .Where(result => !result.Skipped)
            .Select(result => result.ObjectId)
            .ToList();

        SurfaceFrontBackFlipApplyResponse frontBackFlipData = new()
        {
            FilePath = filePath
        };
        if (rebuiltObjectIds.Count > 0)
        {
            OperationResponse<SurfaceFrontBackFlipApplyResponse> frontBackFlip = _frontBackFlipOrchestrator.Apply(new ApplySurfaceFrontBackFlipRequest
            {
                FilePath = filePath,
                ConfirmedObjectIds = rebuiltObjectIds
            });
            if (!frontBackFlip.Success || frontBackFlip.Data is null)
            {
                return OperationResponse<StandardFourPointSurfaceRebuildResponse>.Fail(
                    $"POST_REBUILD_FRONT_BACK_FLIP_FAILED: {frontBackFlip.Message}");
            }

            frontBackFlipData = frontBackFlip.Data;
        }

        List<Guid> frontBackFlippedObjectIds = frontBackFlipData.Results
            .Where(result => !result.Skipped)
            .Select(result => result.ObjectId)
            .ToList();

        SurfaceDirectionTweakApplyResponse directionData = new()
        {
            FilePath = filePath
        };
        if (frontBackFlippedObjectIds.Count > 0)
        {
            OperationResponse<SurfaceDirectionTweakApplyResponse> direction = _directionTweakOrchestrator.Apply(new ApplySurfaceDirectionTweakRequest
            {
                FilePath = filePath,
                ConfirmedObjectIds = frontBackFlippedObjectIds,
                Operations = directionOperations.ToList()
            });
            if (!direction.Success || direction.Data is null)
            {
                return OperationResponse<StandardFourPointSurfaceRebuildResponse>.Fail(
                    $"POST_REBUILD_DIRECTION_TWEAK_FAILED: {direction.Message}");
            }

            directionData = direction.Data;
        }

        return OperationResponse<StandardFourPointSurfaceRebuildResponse>.Ok(new StandardFourPointSurfaceRebuildResponse
        {
            FilePath = filePath,
            RebuildUndoRecordName = rebuild.Data.UndoRecordName,
            FrontBackFlipUndoRecordName = frontBackFlipData.UndoRecordName,
            DirectionUndoRecordName = directionData.UndoRecordName,
            PostRebuildDirectionOperations = directionOperations,
            Results = ComposeResults(rebuild.Data.Results, frontBackFlipData.Results, directionData.Results)
        }, "Standard four-point surface rebuild applied.");
    }

    private static SurfaceRebuildSpec CreateStandardRebuildSpec()
    {
        return new SurfaceRebuildSpec
        {
            GuideMode = SurfacePointOrderGuideMode.Auto,
            Direction = SurfacePointOrderDirection.Clockwise,
            StartAnchorMode = SurfacePointOrderStartAnchorMode.ReferenceStart
        };
    }

    private static IReadOnlyList<StandardFourPointSurfaceRebuildItem> ComposeResults(
        IReadOnlyList<SurfacePointOrderApplyItem> rebuildResults,
        IReadOnlyList<SurfaceFrontBackFlipApplyItem> frontBackFlipResults,
        IReadOnlyList<SurfaceDirectionTweakApplyItem> directionResults)
    {
        var frontBackFlipById = frontBackFlipResults.ToDictionary(result => result.ObjectId);
        var directionById = directionResults.ToDictionary(result => result.ObjectId);
        var results = new List<StandardFourPointSurfaceRebuildItem>(rebuildResults.Count);
        foreach (SurfacePointOrderApplyItem rebuild in rebuildResults)
        {
            frontBackFlipById.TryGetValue(rebuild.ObjectId, out SurfaceFrontBackFlipApplyItem? frontBackFlip);
            directionById.TryGetValue(rebuild.ObjectId, out SurfaceDirectionTweakApplyItem? direction);
            IReadOnlyList<ObjectEditWarning> warnings = CombineWarnings(
                rebuild.Warnings,
                frontBackFlip?.Warnings ?? Array.Empty<ObjectEditWarning>(),
                direction?.Warnings ?? Array.Empty<ObjectEditWarning>());
            IReadOnlyList<string> metadataDropped = rebuild.MetadataDropped
                .Concat(frontBackFlip?.MetadataDropped ?? Array.Empty<string>())
                .Concat(direction?.MetadataDropped ?? Array.Empty<string>())
                .ToList();

            results.Add(new StandardFourPointSurfaceRebuildItem
            {
                OriginalObjectId = rebuild.OriginalObjectId,
                ObjectId = rebuild.ObjectId,
                RebuildSkipped = rebuild.Skipped,
                RebuildSkipReason = rebuild.SkipReason,
                FrontBackFlipSkipped = !rebuild.Skipped && (frontBackFlip is null || frontBackFlip.Skipped),
                FrontBackFlipSkipReason = rebuild.Skipped
                    ? string.Empty
                    : frontBackFlip?.SkipReason ?? "POST_REBUILD_FRONT_BACK_FLIP_NOT_RUN",
                DirectionSkipped = !rebuild.Skipped && frontBackFlip is not null && !frontBackFlip.Skipped && (direction is null || direction.Skipped),
                DirectionSkipReason = rebuild.Skipped || frontBackFlip is null || frontBackFlip.Skipped
                    ? string.Empty
                    : direction?.SkipReason ?? "POST_REBUILD_DIRECTION_TWEAK_NOT_RUN",
                MetadataDropped = metadataDropped,
                Warnings = warnings
            });
        }

        return results;
    }

    private static IReadOnlyList<ObjectEditWarning> CombineWarnings(params IEnumerable<ObjectEditWarning>[] warnings)
    {
        return warnings
            .SelectMany(source => source)
            .ToList();
    }
}

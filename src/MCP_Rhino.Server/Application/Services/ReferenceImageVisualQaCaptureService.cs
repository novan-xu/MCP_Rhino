using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class ReferenceImageVisualQaCaptureService
{
    private const int DefaultMaxObjectSummaries = 20;
    private const int MaxCaptureViews = 4;

    private readonly RhinoViewportCaptureService _viewportCaptureService;
    private readonly RhinoObjectFilterService _filterService;

    public ReferenceImageVisualQaCaptureService(
        RhinoViewportCaptureService viewportCaptureService,
        RhinoObjectFilterService filterService)
    {
        _viewportCaptureService = viewportCaptureService;
        _filterService = filterService;
    }

    public OperationResponse<ReferenceImageVisualQaCaptureResponse> Capture(CaptureReferenceImageModelingQaViewsRequest request)
    {
        var warnings = new List<ObjectEditWarning>();
        OperationResponse<RhinoObjectFilterResult>? targetResolution = ResolveTargets(request);
        if (targetResolution is { Success: false })
        {
            return OperationResponse<ReferenceImageVisualQaCaptureResponse>.Fail($"Target resolution failed: {targetResolution.Message}");
        }

        RhinoObjectFilterResult? targetData = targetResolution?.Data;
        if (targetData?.Warnings is not null)
        {
            warnings.AddRange(targetData.Warnings);
        }

        IReadOnlyList<RhinoObjectInfo> targetObjects = targetData?.Objects ?? Array.Empty<RhinoObjectInfo>();
        int maxObjectSummaries = request.MaxObjectSummaries <= 0 ? DefaultMaxObjectSummaries : request.MaxObjectSummaries;
        IReadOnlyList<RhinoObjectInfo> objectSummaries = targetObjects.Take(maxObjectSummaries).ToList();
        if (targetObjects.Count > objectSummaries.Count)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "QA_OBJECT_SUMMARIES_TRUNCATED",
                Message = $"Target object summaries were truncated from {targetObjects.Count} to {objectSummaries.Count}."
            });
        }

        List<string?> viewNames = NormalizeViewNames(request.ViewNames, warnings);
        var captures = new List<ReferenceImageVisualQaViewCaptureResponse>();
        foreach (string? viewName in viewNames)
        {
            OperationResponse<ViewportCaptureResponse> capture = _viewportCaptureService.Capture(new CaptureViewportImageRequest
            {
                FilePath = request.FilePath,
                ViewName = viewName,
                ImageSizePx = request.ImageSizePx,
                BackgroundTransparent = request.BackgroundTransparent
            });

            if (!capture.Success || capture.Data is null)
            {
                return OperationResponse<ReferenceImageVisualQaCaptureResponse>.Fail(
                    $"Viewport capture failed for [{viewName ?? "current"}]: {capture.Message}");
            }

            warnings.AddRange(capture.Data.Warnings);
            captures.Add(new ReferenceImageVisualQaViewCaptureResponse
            {
                RequestedViewName = viewName ?? string.Empty,
                CapturedViewName = capture.Data.ViewName,
                Width = capture.Data.Width,
                Height = capture.Data.Height,
                ContentType = capture.Data.ContentType,
                DataBase64 = capture.Data.DataBase64,
                ByteCount = capture.Data.ByteCount
            });
        }

        var response = new ReferenceImageVisualQaCaptureResponse
        {
            FilePath = request.FilePath,
            ReferenceImagePath = request.ReferenceImagePath ?? string.Empty,
            ReferenceImageLabel = request.ReferenceImageLabel ?? string.Empty,
            CheckpointKind = request.CheckpointKind,
            TargetObjectCount = targetObjects.Count,
            TargetCriteriaSummary = targetData?.CriteriaSummary ?? "No target object filter supplied; captured requested viewport state.",
            ObjectSummaries = objectSummaries,
            Captures = captures,
            Checklist = BuildChecklist(request.CheckpointKind),
            Warnings = warnings
        };

        return OperationResponse<ReferenceImageVisualQaCaptureResponse>.Ok(
            response,
            $"Captured {captures.Count} visual QA view(s) for {request.CheckpointKind}.");
    }

    private OperationResponse<RhinoObjectFilterResult>? ResolveTargets(CaptureReferenceImageModelingQaViewsRequest request)
    {
        if (request.ObjectIds.Count > 0)
        {
            return _filterService.ResolveByObjectIds(request.FilePath, request.ObjectIds);
        }

        if (!HasFilterCriteria(request))
        {
            return null;
        }

        return _filterService.Filter(new FilterObjectsRequest
        {
            FilePath = request.FilePath,
            LayerQueries = request.LayerQueries,
            ConfirmedLayerFullPaths = request.ConfirmedLayerFullPaths,
            ObjectTypes = request.ObjectTypes,
            UserAttributeConditions = request.UserAttributeConditions,
            MatchMode = request.MatchMode,
            UserAttributeMatchMode = request.UserAttributeMatchMode
        });
    }

    private static bool HasFilterCriteria(CaptureReferenceImageModelingQaViewsRequest request)
    {
        return request.LayerQueries.Any(item => !string.IsNullOrWhiteSpace(item))
            || request.ConfirmedLayerFullPaths.Any(item => !string.IsNullOrWhiteSpace(item))
            || request.ObjectTypes.Any(item => !string.IsNullOrWhiteSpace(item))
            || request.UserAttributeConditions.Any(condition => !string.IsNullOrWhiteSpace(condition.Key));
    }

    private static List<string?> NormalizeViewNames(IReadOnlyList<string> requestedViewNames, ICollection<ObjectEditWarning> warnings)
    {
        if (requestedViewNames.Count == 0)
        {
            return new List<string?> { null };
        }

        List<string?> viewNames = requestedViewNames
            .Select(item => string.IsNullOrWhiteSpace(item) ? null : item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxCaptureViews)
            .ToList();

        if (requestedViewNames.Count > viewNames.Count)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "QA_CAPTURE_VIEWS_TRUNCATED",
                Message = $"Requested views were truncated to {MaxCaptureViews}."
            });
        }

        return viewNames.Count == 0 ? new List<string?> { null } : viewNames;
    }

    private static IReadOnlyList<string> BuildChecklist(ReferenceImageVisualQaCheckpointKind checkpointKind)
    {
        return checkpointKind switch
        {
            ReferenceImageVisualQaCheckpointKind.DetailRefinement => new[]
            {
                "Did the added details improve recognizability instead of adding visual noise?",
                "Are seams, grooves, handles, feet, rims, labels, holes, or repeated features placed close enough to the reference?",
                "Are any details represented as geometry when material or texture would be more appropriate?",
                "Did refinement preserve the already-approved massing proportions?"
            },
            ReferenceImageVisualQaCheckpointKind.MaterialReview => new[]
            {
                "Are the dominant colors close to the reference image?",
                "Are material groups assigned to the correct object parts?",
                "Does the model need texture, transparency, gloss, roughness, or only color changes?",
                "Would additional geometry improve the material read, or would it create unnecessary noise?"
            },
            ReferenceImageVisualQaCheckpointKind.FinalAcceptance => new[]
            {
                "Is the object recognizable as the same object category as the reference?",
                "What are the largest remaining mismatches in silhouette, part count, proportions, details, or material?",
                "Are remaining mismatches caused by missing tools, ambiguous image context, or poor modeling choices?",
                "Should the agent accept the model, iterate once more, or stop with a capability gap report?"
            },
            _ => new[]
            {
                "Does the model read as the same object category as the reference?",
                "Is the large silhouette close enough before adding details?",
                "Are major parts present and in roughly correct positions?",
                "Are width, height, and inferred depth proportions plausible?",
                "Are obvious negative spaces, openings, or supports accounted for?",
                "Is there detail noise that should be delayed or removed before refinement?"
            }
        };
    }
}

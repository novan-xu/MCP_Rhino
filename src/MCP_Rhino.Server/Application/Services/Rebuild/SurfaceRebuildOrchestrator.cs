using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services.Rebuild;

public sealed class SurfaceRebuildOrchestrator : ISurfaceRebuildOrchestrator
{
    internal const string UndoRecordName = "MCP:RedefineSurfacePointOrder";

    private readonly IBoundaryReferenceCurveAnalyzer _referenceCurveAnalyzer;
    private readonly ISurfaceLocalCoordinateSystemBuilder _localCoordinateSystemBuilder;
    private readonly ISurfaceBoundaryPointOrderer _pointOrderer;
    private readonly IBoundaryDrivenSurfaceReconstructor _reconstructor;
    private readonly IGeometryMutationService _mutationService;

    public SurfaceRebuildOrchestrator(
        IBoundaryReferenceCurveAnalyzer referenceCurveAnalyzer,
        ISurfaceLocalCoordinateSystemBuilder localCoordinateSystemBuilder,
        ISurfaceBoundaryPointOrderer pointOrderer,
        IBoundaryDrivenSurfaceReconstructor reconstructor,
        IGeometryMutationService mutationService)
    {
        _referenceCurveAnalyzer = referenceCurveAnalyzer;
        _localCoordinateSystemBuilder = localCoordinateSystemBuilder;
        _pointOrderer = pointOrderer;
        _reconstructor = reconstructor;
        _mutationService = mutationService;
    }

    public OperationResponse<SurfaceRebuildDescriptorResponse> Inspect(InspectSurfaceRebuildDescriptorRequest request)
    {
        OperationResponse validation = ValidateIds(request.ConfirmedObjectIds);
        if (!validation.Success)
        {
            return OperationResponse<SurfaceRebuildDescriptorResponse>.Fail(validation.Message);
        }

        SurfaceRebuildSpec spec = CreateInspectSpec(request);
        var descriptors = new List<SurfaceRebuildDescriptor>();
        foreach (Guid objectId in request.ConfirmedObjectIds)
        {
            OperationResponse<SurfaceRebuildDescriptor> descriptor = _referenceCurveAnalyzer.Analyze(request.FilePath, objectId, spec);
            if (!descriptor.Success || descriptor.Data is null)
            {
                return OperationResponse<SurfaceRebuildDescriptorResponse>.Fail(descriptor.Message);
            }

            descriptors.Add(descriptor.Data);
        }

        return OperationResponse<SurfaceRebuildDescriptorResponse>.Ok(new SurfaceRebuildDescriptorResponse
        {
            FilePath = request.FilePath,
            Descriptors = descriptors
        }, "Surface rebuild descriptors inspected.");
    }

    public OperationResponse<SurfacePointOrderPreviewResponse> Preview(PreviewRedefineSurfacePointOrderRequest request)
    {
        OperationResponse validation = ValidateIds(request.ConfirmedObjectIds);
        if (!validation.Success)
        {
            return OperationResponse<SurfacePointOrderPreviewResponse>.Fail(validation.Message);
        }

        var results = new List<SurfacePointOrderPreviewItem>();
        foreach (Guid objectId in request.ConfirmedObjectIds)
        {
            OperationResponse<PreparedSurfaceRebuild> prepared = Prepare(request.FilePath, objectId, request.Spec);
            if (!prepared.Success || prepared.Data is null)
            {
                if (IsDocumentLevelFailure(prepared.Message))
                {
                    return OperationResponse<SurfacePointOrderPreviewResponse>.Fail(prepared.Message);
                }

                results.Add(new SurfacePointOrderPreviewItem
                {
                    ObjectId = objectId,
                    Skipped = true,
                    SkipReason = prepared.Message
                });
                continue;
            }

            results.Add(new SurfacePointOrderPreviewItem
            {
                ObjectId = objectId,
                Plan = prepared.Data.Plan,
                PreviewSummary = prepared.Data.Rebuild.Summary,
                Warnings = CombineWarnings(
                    ToWarningObjects(prepared.Data.Descriptor.Warnings),
                    ToWarningObjects(prepared.Data.Plan.Warnings),
                    prepared.Data.Rebuild.Warnings)
            });
        }

        return OperationResponse<SurfacePointOrderPreviewResponse>.Ok(new SurfacePointOrderPreviewResponse
        {
            FilePath = request.FilePath,
            Results = results
        }, "Surface point order rebuild preview generated.");
    }

    public OperationResponse<SurfacePointOrderApplyResponse> Apply(ApplyRedefineSurfacePointOrderRequest request)
    {
        OperationResponse validation = ValidateIds(request.ConfirmedObjectIds);
        if (!validation.Success)
        {
            return OperationResponse<SurfacePointOrderApplyResponse>.Fail(validation.Message);
        }

        if (!request.ReplaceOriginal)
        {
            return OperationResponse<SurfacePointOrderApplyResponse>.Fail("REPLACE_ORIGINAL_FALSE_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        var preparedItems = new List<PreparedSurfaceRebuild>();
        var results = new List<SurfacePointOrderApplyItem>();
        foreach (Guid objectId in request.ConfirmedObjectIds)
        {
            OperationResponse<PreparedSurfaceRebuild> prepared = Prepare(request.FilePath, objectId, request.Spec);
            if (!prepared.Success || prepared.Data is null)
            {
                if (IsDocumentLevelFailure(prepared.Message))
                {
                    return OperationResponse<SurfacePointOrderApplyResponse>.Fail(prepared.Message);
                }

                results.Add(new SurfacePointOrderApplyItem
                {
                    OriginalObjectId = objectId,
                    ObjectId = objectId,
                    UndoRecordName = UndoRecordName,
                    Skipped = true,
                    SkipReason = prepared.Message
                });
                continue;
            }

            if (!prepared.Data.Rebuild.Summary.IsValid)
            {
                results.Add(new SurfacePointOrderApplyItem
                {
                    OriginalObjectId = objectId,
                    ObjectId = objectId,
                    UndoRecordName = UndoRecordName,
                    Skipped = true,
                    SkipReason = "RECONSTRUCTION_INVALID",
                    Warnings = CombineWarnings(
                        ToWarningObjects(prepared.Data.Descriptor.Warnings),
                        ToWarningObjects(prepared.Data.Plan.Warnings),
                        prepared.Data.Rebuild.Warnings)
                });
                continue;
            }

            preparedItems.Add(prepared.Data);
        }

        if (preparedItems.Count == 0)
        {
            return OperationResponse<SurfacePointOrderApplyResponse>.Ok(new SurfacePointOrderApplyResponse
            {
                FilePath = request.FilePath,
                UndoRecordName = UndoRecordName,
                Results = results
            }, "Surface point order rebuild apply skipped all targets.");
        }

        OperationResponse<IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEditWarning>>> mutation = _mutationService.ReplaceManyWithMetadata(
            request.FilePath,
            preparedItems
                .Select(item => new GeometryReplacementWithMetadata
                {
                    ObjectId = item.Descriptor.ObjectId,
                    Geometry = item.Rebuild.Geometry
                })
                .ToList(),
            UndoRecordName);
        if (!mutation.Success || mutation.Data is null)
        {
            return OperationResponse<SurfacePointOrderApplyResponse>.Fail(mutation.Message);
        }

        foreach (PreparedSurfaceRebuild item in preparedItems)
        {
            IReadOnlyList<ObjectEditWarning> mutationWarnings = mutation.Data.TryGetValue(item.Descriptor.ObjectId, out IReadOnlyList<ObjectEditWarning>? objectWarnings)
                ? objectWarnings
                : Array.Empty<ObjectEditWarning>();
            IReadOnlyList<ObjectEditWarning> warnings = CombineWarnings(
                ToWarningObjects(item.Descriptor.Warnings),
                ToWarningObjects(item.Plan.Warnings),
                item.Rebuild.Warnings,
                mutationWarnings);
            results.Add(new SurfacePointOrderApplyItem
            {
                OriginalObjectId = item.Descriptor.ObjectId,
                ObjectId = item.Descriptor.ObjectId,
                UndoRecordName = UndoRecordName,
                MetadataDropped = warnings
                    .Where(warning => string.Equals(warning.Code, "METADATA_FIELDS_DROPPED", StringComparison.Ordinal))
                    .Select(warning => warning.Message)
                    .ToList(),
                Warnings = warnings
            });
        }

        return OperationResponse<SurfacePointOrderApplyResponse>.Ok(new SurfacePointOrderApplyResponse
        {
            FilePath = request.FilePath,
            UndoRecordName = UndoRecordName,
            Results = results
        }, "Surface point order rebuild applied.");
    }

    private OperationResponse<PreparedSurfaceRebuild> Prepare(string filePath, Guid objectId, SurfaceRebuildSpec spec)
    {
        OperationResponse<SurfaceRebuildDescriptor> descriptor = _referenceCurveAnalyzer.Analyze(filePath, objectId, spec);
        if (!descriptor.Success || descriptor.Data is null)
        {
            return OperationResponse<PreparedSurfaceRebuild>.Fail(descriptor.Message);
        }

        if (!IsSupportedQuadSurface(descriptor.Data))
        {
            return OperationResponse<PreparedSurfaceRebuild>.Fail("SURFACE_POINT_ORDER_REBUILD_REQUIRES_QUAD_SURFACE");
        }

        SurfaceReferenceCurveSpec? referenceCurve = descriptor.Data.SuggestedReferenceCurve;
        if (referenceCurve is null)
        {
            return OperationResponse<PreparedSurfaceRebuild>.Fail("REFERENCE_CURVE_AMBIGUOUS");
        }

        OperationResponse<SurfaceLocalCoordinateSystem> localFrame = _localCoordinateSystemBuilder.Build(descriptor.Data.OuterBoundaryLoop);
        if (!localFrame.Success || localFrame.Data is null)
        {
            return OperationResponse<PreparedSurfaceRebuild>.Fail(localFrame.Message);
        }

        if (localFrame.Data.IsDegenerate)
        {
            return OperationResponse<PreparedSurfaceRebuild>.Fail("DEGENERATE_LOCAL_FRAME");
        }

        OperationResponse<SurfacePointOrderPlan> plan = _pointOrderer.Order(
            descriptor.Data,
            referenceCurve,
            localFrame.Data,
            spec);
        if (!plan.Success || plan.Data is null)
        {
            return OperationResponse<PreparedSurfaceRebuild>.Fail(plan.Message);
        }

        OperationResponse<BoundaryRebuildResult> rebuild = _reconstructor.Reconstruct(
            plan.Data,
            descriptor.Data.OuterBoundaryLoop);
        if (!rebuild.Success || rebuild.Data is null)
        {
            return OperationResponse<PreparedSurfaceRebuild>.Fail(rebuild.Message);
        }

        return OperationResponse<PreparedSurfaceRebuild>.Ok(new PreparedSurfaceRebuild
        {
            Descriptor = descriptor.Data,
            Plan = plan.Data,
            Rebuild = rebuild.Data
        });
    }

    private static OperationResponse ValidateIds(IReadOnlyList<Guid> objectIds)
    {
        if (objectIds.Count == 0 || objectIds.Any(id => id == Guid.Empty))
        {
            return OperationResponse.Fail("ConfirmedObjectIds must contain at least one non-empty ObjectId.");
        }

        return OperationResponse.Ok();
    }

    private static SurfaceRebuildSpec CreateInspectSpec(InspectSurfaceRebuildDescriptorRequest request)
    {
        if (request.ReferenceCurveObjectId.HasValue)
        {
            return new SurfaceRebuildSpec
            {
                GuideMode = SurfacePointOrderGuideMode.ReferenceCurve,
                ReferenceCurveObjectId = request.ReferenceCurveObjectId
            };
        }

        if (request.ReferenceEdgeIndex.HasValue)
        {
            return new SurfaceRebuildSpec
            {
                GuideMode = SurfacePointOrderGuideMode.ReferenceEdgeIndex,
                ReferenceEdgeIndex = request.ReferenceEdgeIndex
            };
        }

        return new SurfaceRebuildSpec();
    }

    private static bool IsSupportedQuadSurface(SurfaceRebuildDescriptor descriptor)
    {
        return descriptor.Topology == SurfaceTopologyKind.Quad
            && descriptor.OuterBoundaryLoop.Vertices3d.Count == 4
            && !descriptor.OuterBoundaryLoop.HasInnerLoops;
    }

    private static IReadOnlyList<ObjectEditWarning> CombineWarnings(params IEnumerable<ObjectEditWarning>?[] warnings)
    {
        return warnings
            .Where(source => source is not null)
            .SelectMany(source => source!)
            .ToList();
    }

    private static IReadOnlyList<ObjectEditWarning> ToWarningObjects(IEnumerable<string> warnings)
    {
        return warnings
            .Select(code => new ObjectEditWarning
            {
                Code = code,
                Message = code
            })
            .ToList();
    }

    private static bool IsDocumentLevelFailure(string message)
    {
        return string.Equals(message, "LIVE_RHINO_REQUIRED", StringComparison.Ordinal)
            || string.Equals(message, "NO_ACTIVE_DOCUMENT", StringComparison.Ordinal)
            || string.Equals(message, "ACTIVE_DOC_UNSAVED", StringComparison.Ordinal)
            || string.Equals(message, "FILE_NOT_ACTIVE", StringComparison.Ordinal)
            || string.Equals(message, "RHINO_MAIN_THREAD_BUSY", StringComparison.Ordinal);
    }

    private sealed class PreparedSurfaceRebuild
    {
        public SurfaceRebuildDescriptor Descriptor { get; set; } = new();
        public SurfacePointOrderPlan Plan { get; set; } = new();
        public BoundaryRebuildResult Rebuild { get; set; } = new();
    }
}

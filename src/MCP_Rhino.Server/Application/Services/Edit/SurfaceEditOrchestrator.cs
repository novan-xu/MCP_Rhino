using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services.Edit;

public sealed class SurfaceEditOrchestrator : ISurfaceEditOrchestrator
{
    internal const string UndoRecordName = "MCP:EditSurfaceGeometry";

    private readonly IGeometryEditValidator _validator;
    private readonly IEditableGeometryDescriptorService _descriptorService;
    private readonly IGeometryEditStrategyResolver _strategyResolver;
    private readonly IDerivedPointOperationEvaluator _derivedPointOperationEvaluator;
    private readonly IGeometryReconstructor _reconstructor;
    private readonly IGeometryMutationService _mutationService;
    private readonly IGeometryTransformExecutionBridge _transformExecutionBridge;

    public SurfaceEditOrchestrator(
        IGeometryEditValidator validator,
        IEditableGeometryDescriptorService descriptorService,
        IGeometryEditStrategyResolver strategyResolver,
        IDerivedPointOperationEvaluator derivedPointOperationEvaluator,
        IGeometryReconstructor reconstructor,
        IGeometryMutationService mutationService,
        IGeometryTransformExecutionBridge transformExecutionBridge)
    {
        _validator = validator;
        _descriptorService = descriptorService;
        _strategyResolver = strategyResolver;
        _derivedPointOperationEvaluator = derivedPointOperationEvaluator;
        _reconstructor = reconstructor;
        _mutationService = mutationService;
        _transformExecutionBridge = transformExecutionBridge;
    }

    public OperationResponse<GeometryEditPreviewResponse> Preview(PreviewEditSurfaceGeometryRequest request)
    {
        OperationResponse<SurfaceEditPreparation> preparation = Prepare(
            request.FilePath,
            request.ObjectId,
            request.EditSpec,
            request.ExpectedStrategy);
        if (!preparation.Success || preparation.Data is null)
        {
            return OperationResponse<GeometryEditPreviewResponse>.Fail(preparation.Message);
        }

        SurfaceEditPreparation data = preparation.Data;
        if (data.Strategy.Strategy == GeometryEditStrategyKind.ExactTransform)
        {
            OperationResponse<GeometryModificationPreviewResponse> exactPreview = _transformExecutionBridge.PreviewSurfaceExactTransform(
                request.FilePath,
                request.ObjectId,
                request.EditSpec,
                data.Descriptor);
            if (!exactPreview.Success)
            {
                return OperationResponse<GeometryEditPreviewResponse>.Fail(exactPreview.Message);
            }

            return OperationResponse<GeometryEditPreviewResponse>.Ok(new GeometryEditPreviewResponse
            {
                FilePath = request.FilePath,
                ObjectId = request.ObjectId,
                Strategy = data.Strategy.Strategy,
                SurfaceDescriptorSummary = data.Descriptor.SurfaceStructure,
                DescriptorPointCount = data.Descriptor.ControlPointCount,
                ReconstructedCurveSummary = null,
                ResolvedPointIndices = data.DerivedEvaluation?.ResolvedPointIndices ?? Array.Empty<int>(),
                DerivedOperationApplied = data.DerivedEvaluation?.DerivedOperationApplied,
                StrategyResolutionTrace = data.Strategy.Trace,
                Warnings = CombineWarnings(
                    ToWarningObjects(data.Descriptor.Warnings),
                    data.Strategy.Warnings,
                    data.DerivedEvaluation?.Warnings,
                    exactPreview.Data?.Warnings)
            }, "Surface geometry edit exact-transform preview generated.");
        }

        IReadOnlyList<EditablePointInput> points = data.DerivedEvaluation?.NewPoints ?? data.DirectOverridePoints;
        OperationResponse<SurfaceReconstructionResult> reconstruction = _reconstructor.ReconstructSurface(
            request.FilePath,
            request.ObjectId,
            data.Descriptor,
            points);
        if (!reconstruction.Success || reconstruction.Data is null)
        {
            return OperationResponse<GeometryEditPreviewResponse>.Fail(reconstruction.Message);
        }

        var warnings = CombineWarnings(
            ToWarningObjects(data.Descriptor.Warnings),
            data.Strategy.Warnings,
            data.DerivedEvaluation?.Warnings,
            reconstruction.Data.Warnings);
        if (!reconstruction.Data.Summary.IsValid)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "RECONSTRUCTION_INVALID",
                Message = "The reconstructed surface is not valid."
            });
        }

        return OperationResponse<GeometryEditPreviewResponse>.Ok(new GeometryEditPreviewResponse
        {
            FilePath = request.FilePath,
            ObjectId = request.ObjectId,
            Strategy = data.Strategy.Strategy,
            SurfaceDescriptorSummary = data.Descriptor.SurfaceStructure,
            DescriptorPointCount = data.Descriptor.ControlPointCount,
            ReconstructedCurveSummary = null,
            SurfaceControlPointGridSnapshot = reconstruction.Data.Summary,
            ResolvedPointIndices = data.DerivedEvaluation?.ResolvedPointIndices ?? Array.Empty<int>(),
            DerivedOperationApplied = data.DerivedEvaluation?.DerivedOperationApplied,
            StrategyResolutionTrace = data.Strategy.Trace,
            Warnings = warnings
        }, "Surface geometry edit preview generated.");
    }

    public OperationResponse<GeometryEditApplyResponse> Apply(ApplyEditSurfaceGeometryRequest request)
    {
        OperationResponse<SurfaceEditPreparation> preparation = Prepare(
            request.FilePath,
            request.ObjectId,
            request.EditSpec,
            request.ExpectedStrategy);
        if (!preparation.Success || preparation.Data is null)
        {
            return OperationResponse<GeometryEditApplyResponse>.Fail(preparation.Message);
        }

        SurfaceEditPreparation data = preparation.Data;
        if (data.Strategy.Strategy == GeometryEditStrategyKind.ExactTransform)
        {
            OperationResponse<GeometryModificationResponse> exactApply = _transformExecutionBridge.ApplySurfaceExactTransform(
                request.FilePath,
                request.ObjectId,
                request.EditSpec,
                data.Descriptor,
                UndoRecordName);
            if (!exactApply.Success)
            {
                return OperationResponse<GeometryEditApplyResponse>.Fail(exactApply.Message);
            }

            return OperationResponse<GeometryEditApplyResponse>.Ok(new GeometryEditApplyResponse
            {
                FilePath = request.FilePath,
                ObjectId = request.ObjectId,
                Strategy = data.Strategy.Strategy,
                UndoRecordName = UndoRecordName,
                ResolvedPointIndices = data.DerivedEvaluation?.ResolvedPointIndices ?? Array.Empty<int>(),
                DerivedOperationApplied = data.DerivedEvaluation?.DerivedOperationApplied,
                Warnings = CombineWarnings(
                    ToWarningObjects(data.Descriptor.Warnings),
                    data.Strategy.Warnings,
                    data.DerivedEvaluation?.Warnings,
                    exactApply.Data?.Warnings)
            }, "Surface geometry edit exact-transform applied.");
        }

        IReadOnlyList<EditablePointInput> points = data.DerivedEvaluation?.NewPoints ?? data.DirectOverridePoints;
        OperationResponse<SurfaceReconstructionResult> reconstruction = _reconstructor.ReconstructSurface(
            request.FilePath,
            request.ObjectId,
            data.Descriptor,
            points);
        if (!reconstruction.Success || reconstruction.Data is null)
        {
            return OperationResponse<GeometryEditApplyResponse>.Fail(reconstruction.Message);
        }

        if (!reconstruction.Data.Summary.IsValid)
        {
            return OperationResponse<GeometryEditApplyResponse>.Fail("RECONSTRUCTION_INVALID");
        }

        OperationResponse<IReadOnlyList<ObjectEditWarning>> mutation = _mutationService.ReplaceWithMetadata(
            request.FilePath,
            request.ObjectId,
            reconstruction.Data.Geometry,
            UndoRecordName);
        if (!mutation.Success)
        {
            return OperationResponse<GeometryEditApplyResponse>.Fail(mutation.Message);
        }

        var warnings = CombineWarnings(
            ToWarningObjects(data.Descriptor.Warnings),
            data.Strategy.Warnings,
            data.DerivedEvaluation?.Warnings,
            reconstruction.Data.Warnings,
            mutation.Data);

        return OperationResponse<GeometryEditApplyResponse>.Ok(new GeometryEditApplyResponse
        {
            FilePath = request.FilePath,
            ObjectId = request.ObjectId,
            Strategy = data.Strategy.Strategy,
            UndoRecordName = UndoRecordName,
            SurfaceControlPointGridSnapshot = reconstruction.Data.Summary,
            MetadataDropped = warnings
                .Where(warning => string.Equals(warning.Code, "METADATA_FIELDS_DROPPED", StringComparison.Ordinal))
                .Select(warning => warning.Message)
                .ToList(),
            ResolvedPointIndices = data.DerivedEvaluation?.ResolvedPointIndices ?? Array.Empty<int>(),
            DerivedOperationApplied = data.DerivedEvaluation?.DerivedOperationApplied,
            Warnings = warnings
        }, "Surface geometry edit applied.");
    }

    private OperationResponse<SurfaceEditPreparation> Prepare(
        string filePath,
        Guid objectId,
        SurfaceEditSpec editSpec,
        GeometryEditStrategyKind? expectedStrategy)
    {
        OperationResponse requestValidation = _validator.ValidateSurfaceRequest(objectId, editSpec);
        if (!requestValidation.Success)
        {
            return OperationResponse<SurfaceEditPreparation>.Fail(requestValidation.Message);
        }

        OperationResponse<EditableGeometryDescriptorResponse> descriptorResponse = ReadDescriptor(filePath, objectId, DescriptorDetail.Summary);
        if (!descriptorResponse.Success || descriptorResponse.Data?.Descriptor is null)
        {
            return OperationResponse<SurfaceEditPreparation>.Fail(descriptorResponse.Message);
        }

        EditableGeometryDescriptor descriptor = descriptorResponse.Data.Descriptor;
        if (descriptor.Kind != EditableGeometryKind.Surface)
        {
            return OperationResponse<SurfaceEditPreparation>.Fail("EDITABLE_KIND_UNSUPPORTED");
        }

        OperationResponse pointValidation = _validator.ValidateSurfacePointInputs(editSpec, descriptor);
        if (!pointValidation.Success)
        {
            return OperationResponse<SurfaceEditPreparation>.Fail(pointValidation.Message);
        }

        OperationResponse<StrategyResolutionResult> strategy = _strategyResolver.ResolveSurface(editSpec, descriptor);
        if (!strategy.Success || strategy.Data is null)
        {
            return OperationResponse<SurfaceEditPreparation>.Fail(strategy.Message);
        }

        if (expectedStrategy.HasValue && expectedStrategy.Value != strategy.Data.Strategy)
        {
            return OperationResponse<SurfaceEditPreparation>.Fail($"STRATEGY_EXPECTATION_MISMATCH: resolver selected {strategy.Data.Strategy}");
        }

        var preparation = new SurfaceEditPreparation
        {
            Descriptor = descriptor,
            Strategy = strategy.Data
        };

        if (editSpec.Operation.Kind == GeometryEditOperationKind.DirectOverride)
        {
            preparation.DirectOverridePoints = NormalizeDirectOverrideGrid(editSpec.Grid!, descriptor.SurfaceStructure!);
            return OperationResponse<SurfaceEditPreparation>.Ok(preparation);
        }

        if (editSpec.Operation.Kind != GeometryEditOperationKind.DerivedOperation)
        {
            return OperationResponse<SurfaceEditPreparation>.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        if (strategy.Data.Strategy == GeometryEditStrategyKind.ExactTransform)
        {
            preparation.DerivedEvaluation = CreateExactTransformEvaluation(descriptor, editSpec);
            return OperationResponse<SurfaceEditPreparation>.Ok(preparation);
        }

        OperationResponse<EditableGeometryDescriptorResponse> fullDescriptorResponse = ReadDescriptor(filePath, objectId, DescriptorDetail.Full);
        if (!fullDescriptorResponse.Success || fullDescriptorResponse.Data?.Descriptor is null)
        {
            return OperationResponse<SurfaceEditPreparation>.Fail(fullDescriptorResponse.Message);
        }

        descriptor = fullDescriptorResponse.Data.Descriptor;
        pointValidation = _validator.ValidateSurfacePointInputs(editSpec, descriptor);
        if (!pointValidation.Success)
        {
            return OperationResponse<SurfaceEditPreparation>.Fail(pointValidation.Message);
        }

        OperationResponse<DerivedPointEvaluationResult> evaluation = _derivedPointOperationEvaluator.EvaluateSurface(
            filePath,
            objectId,
            descriptor,
            editSpec);
        if (!evaluation.Success || evaluation.Data is null)
        {
            return OperationResponse<SurfaceEditPreparation>.Fail(evaluation.Message);
        }

        preparation.Descriptor = descriptor;
        preparation.DerivedEvaluation = evaluation.Data;
        return OperationResponse<SurfaceEditPreparation>.Ok(preparation);
    }

    private OperationResponse<EditableGeometryDescriptorResponse> ReadDescriptor(string filePath, Guid objectId, DescriptorDetail detail)
    {
        return _descriptorService.Read(new GetEditableGeometryDescriptorRequest
        {
            FilePath = filePath,
            ObjectId = objectId,
            Detail = detail
        });
    }

    private static DerivedPointEvaluationResult CreateExactTransformEvaluation(
        EditableGeometryDescriptor descriptor,
        SurfaceEditSpec editSpec)
    {
        SurfaceEditDerivedOperationParameters parameters = editSpec.Operation.DerivedParameters ?? new SurfaceEditDerivedOperationParameters();
        var indices = Enumerable.Range(0, descriptor.ControlPointCount).ToList();
        DerivedOperationApplied applied = editSpec.Operation.DerivedKind switch
        {
            DerivedPointOperationKind.ScaleAboutCentroid => new DerivedOperationApplied
            {
                Kind = DerivedPointOperationKind.ScaleAboutCentroid,
                CentroidWorld = descriptor.ControlPointCentroidWorld,
                ScaleX = parameters.ScaleX,
                ScaleY = parameters.ScaleY,
                ScaleZ = parameters.ScaleZ
            },
            DerivedPointOperationKind.TranslateByVector => new DerivedOperationApplied
            {
                Kind = DerivedPointOperationKind.TranslateByVector,
                Vector = new GeometryVectorData
                {
                    X = parameters.VectorX,
                    Y = parameters.VectorY,
                    Z = parameters.VectorZ
                }
            },
            _ => new DerivedOperationApplied()
        };

        return new DerivedPointEvaluationResult
        {
            ResolvedPointIndices = indices,
            DerivedOperationApplied = applied
        };
    }

    private static IReadOnlyList<EditablePointInput> NormalizeDirectOverrideGrid(
        EditableSurfacePointGrid grid,
        EditableSurfaceStructure structure)
    {
        if (grid.Layout == SurfacePointLayoutKind.Flat)
        {
            return grid.Points
                .Select(point => new EditablePointInput
                {
                    Index = point.Index,
                    X = point.X,
                    Y = point.Y,
                    Z = point.Z
                })
                .ToList();
        }

        var points = new List<EditablePointInput>(structure.CountU * structure.CountV);
        for (int u = 0; u < structure.CountU; u++)
        {
            for (int v = 0; v < structure.CountV; v++)
            {
                EditablePointInput point = grid.Rows[u][v];
                points.Add(new EditablePointInput
                {
                    Index = (u * structure.CountV) + v,
                    X = point.X,
                    Y = point.Y,
                    Z = point.Z
                });
            }
        }

        return points;
    }

    private static List<ObjectEditWarning> CombineWarnings(params IEnumerable<ObjectEditWarning>?[] warnings)
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

    private sealed class SurfaceEditPreparation
    {
        public EditableGeometryDescriptor Descriptor { get; set; } = new();
        public StrategyResolutionResult Strategy { get; set; } = new();
        public DerivedPointEvaluationResult? DerivedEvaluation { get; set; }
        public IReadOnlyList<EditablePointInput> DirectOverridePoints { get; set; } = Array.Empty<EditablePointInput>();
    }
}

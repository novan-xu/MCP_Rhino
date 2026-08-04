using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services.Edit;

public sealed class CurveEditOrchestrator : ICurveEditOrchestrator
{
    internal const string UndoRecordName = "MCP:EditCurveGeometry";

    private readonly IGeometryEditValidator _validator;
    private readonly IEditableGeometryDescriptorService _descriptorService;
    private readonly IGeometryEditStrategyResolver _strategyResolver;
    private readonly IDerivedPointOperationEvaluator _derivedPointOperationEvaluator;
    private readonly IGeometryReconstructor _reconstructor;
    private readonly IGeometryMutationService _mutationService;
    private readonly IGeometryTransformExecutionBridge _transformExecutionBridge;

    public CurveEditOrchestrator(
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

    public OperationResponse<GeometryEditPreviewResponse> Preview(PreviewEditCurveGeometryRequest request)
    {
        OperationResponse<CurveEditPreparation> preparation = Prepare(
            request.FilePath,
            request.ObjectId,
            request.EditSpec,
            request.ExpectedStrategy);
        if (!preparation.Success || preparation.Data is null)
        {
            return OperationResponse<GeometryEditPreviewResponse>.Fail(preparation.Message);
        }

        CurveEditPreparation data = preparation.Data;
        if (data.Strategy.Strategy == GeometryEditStrategyKind.ExactTransform)
        {
            OperationResponse<GeometryModificationPreviewResponse> exactPreview = _transformExecutionBridge.PreviewExactTransform(
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
                DescriptorSummary = data.Descriptor.CurveStructure,
                DescriptorPointCount = data.Descriptor.ControlPointCount,
                ReconstructedCurveSummary = null,
                ResolvedPointIndices = data.DerivedEvaluation?.ResolvedPointIndices ?? Array.Empty<int>(),
                DerivedOperationApplied = data.DerivedEvaluation?.DerivedOperationApplied,
                StrategyResolutionTrace = data.Strategy.Trace,
                Warnings = CombineWarnings(
                    data.Strategy.Warnings,
                    data.DerivedEvaluation?.Warnings,
                    exactPreview.Data?.Warnings)
            }, "Curve geometry edit exact-transform preview generated.");
        }

        IReadOnlyList<EditablePointInput> points = data.DerivedEvaluation?.NewPoints ?? request.EditSpec.Points;
        OperationResponse<CurveReconstructionResult> reconstruction = _reconstructor.ReconstructCurve(
            request.FilePath,
            request.ObjectId,
            data.Descriptor,
            points);
        if (!reconstruction.Success || reconstruction.Data is null)
        {
            return OperationResponse<GeometryEditPreviewResponse>.Fail(reconstruction.Message);
        }

        var warnings = CombineWarnings(
            data.Strategy.Warnings,
            data.DerivedEvaluation?.Warnings,
            reconstruction.Data.Warnings);
        if (!reconstruction.Data.Summary.IsValid)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "RECONSTRUCTION_INVALID",
                Message = "The reconstructed curve is not valid."
            });
        }

        return OperationResponse<GeometryEditPreviewResponse>.Ok(new GeometryEditPreviewResponse
        {
            FilePath = request.FilePath,
            ObjectId = request.ObjectId,
            Strategy = data.Strategy.Strategy,
            DescriptorSummary = data.Descriptor.CurveStructure,
            DescriptorPointCount = data.Descriptor.ControlPointCount,
            ReconstructedCurveSummary = reconstruction.Data.Summary,
            ResolvedPointIndices = data.DerivedEvaluation?.ResolvedPointIndices ?? Array.Empty<int>(),
            DerivedOperationApplied = data.DerivedEvaluation?.DerivedOperationApplied,
            StrategyResolutionTrace = data.Strategy.Trace,
            Warnings = warnings
        }, "Curve geometry edit preview generated.");
    }

    public OperationResponse<GeometryEditApplyResponse> Apply(ApplyEditCurveGeometryRequest request)
    {
        OperationResponse<CurveEditPreparation> preparation = Prepare(
            request.FilePath,
            request.ObjectId,
            request.EditSpec,
            request.ExpectedStrategy);
        if (!preparation.Success || preparation.Data is null)
        {
            return OperationResponse<GeometryEditApplyResponse>.Fail(preparation.Message);
        }

        CurveEditPreparation data = preparation.Data;
        if (data.Strategy.Strategy == GeometryEditStrategyKind.ExactTransform)
        {
            OperationResponse<GeometryModificationResponse> exactApply = _transformExecutionBridge.ApplyExactTransform(
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
                    data.Strategy.Warnings,
                    data.DerivedEvaluation?.Warnings,
                    exactApply.Data?.Warnings)
            }, "Curve geometry edit exact-transform applied.");
        }

        IReadOnlyList<EditablePointInput> points = data.DerivedEvaluation?.NewPoints ?? request.EditSpec.Points;
        OperationResponse<CurveReconstructionResult> reconstruction = _reconstructor.ReconstructCurve(
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
            reconstruction.Data.Curve,
            UndoRecordName);
        if (!mutation.Success)
        {
            return OperationResponse<GeometryEditApplyResponse>.Fail(mutation.Message);
        }

        var warnings = CombineWarnings(
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
            MetadataDropped = warnings
                .Where(warning => string.Equals(warning.Code, "METADATA_FIELDS_DROPPED", StringComparison.Ordinal))
                .Select(warning => warning.Message)
                .ToList(),
            ResolvedPointIndices = data.DerivedEvaluation?.ResolvedPointIndices ?? Array.Empty<int>(),
            DerivedOperationApplied = data.DerivedEvaluation?.DerivedOperationApplied,
            Warnings = warnings
        }, "Curve geometry edit applied.");
    }

    private OperationResponse<CurveEditPreparation> Prepare(
        string filePath,
        Guid objectId,
        CurveEditSpec editSpec,
        GeometryEditStrategyKind? expectedStrategy)
    {
        OperationResponse requestValidation = _validator.ValidateRequest(objectId, editSpec);
        if (!requestValidation.Success)
        {
            return OperationResponse<CurveEditPreparation>.Fail(requestValidation.Message);
        }

        OperationResponse<EditableGeometryDescriptorResponse> descriptorResponse = _descriptorService.Read(new GetEditableGeometryDescriptorRequest
        {
            FilePath = filePath,
            ObjectId = objectId,
            Detail = DescriptorDetail.Full
        });
        if (!descriptorResponse.Success || descriptorResponse.Data?.Descriptor is null)
        {
            return OperationResponse<CurveEditPreparation>.Fail(descriptorResponse.Message);
        }

        EditableGeometryDescriptor descriptor = descriptorResponse.Data.Descriptor;
        if (descriptor.Kind != EditableGeometryKind.Curve)
        {
            return OperationResponse<CurveEditPreparation>.Fail("EDITABLE_KIND_UNSUPPORTED");
        }

        OperationResponse pointValidation = _validator.ValidatePointInputs(editSpec, descriptor);
        if (!pointValidation.Success)
        {
            return OperationResponse<CurveEditPreparation>.Fail(pointValidation.Message);
        }

        OperationResponse<StrategyResolutionResult> strategy = _strategyResolver.Resolve(editSpec, descriptor);
        if (!strategy.Success || strategy.Data is null)
        {
            return OperationResponse<CurveEditPreparation>.Fail(strategy.Message);
        }

        if (expectedStrategy.HasValue && expectedStrategy.Value != strategy.Data.Strategy)
        {
            return OperationResponse<CurveEditPreparation>.Fail($"STRATEGY_EXPECTATION_MISMATCH: resolver selected {strategy.Data.Strategy}");
        }

        DerivedPointEvaluationResult? derivedEvaluation = null;
        if (editSpec.Operation.Kind == GeometryEditOperationKind.DerivedOperation)
        {
            OperationResponse<DerivedPointEvaluationResult> evaluation = _derivedPointOperationEvaluator.Evaluate(
                filePath,
                objectId,
                descriptor,
                editSpec);
            if (!evaluation.Success || evaluation.Data is null)
            {
                return OperationResponse<CurveEditPreparation>.Fail(evaluation.Message);
            }

            derivedEvaluation = evaluation.Data;
        }

        return OperationResponse<CurveEditPreparation>.Ok(new CurveEditPreparation
        {
            Descriptor = descriptor,
            Strategy = strategy.Data,
            DerivedEvaluation = derivedEvaluation
        });
    }

    private static List<ObjectEditWarning> CombineWarnings(params IEnumerable<ObjectEditWarning>?[] warnings)
    {
        return warnings
            .Where(source => source is not null)
            .SelectMany(source => source!)
            .ToList();
    }

    private sealed class CurveEditPreparation
    {
        public EditableGeometryDescriptor Descriptor { get; set; } = new();
        public StrategyResolutionResult Strategy { get; set; } = new();
        public DerivedPointEvaluationResult? DerivedEvaluation { get; set; }
    }
}

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class CurveGeometryEditService
{
    internal const string UndoRecordName = "MCP:EditCurveGeometry";

    private readonly IGeometryEditValidator _validator;
    private readonly IEditableGeometryDescriptorService _descriptorService;
    private readonly IGeometryReconstructor _reconstructor;
    private readonly IGeometryMutationService _mutationService;

    public CurveGeometryEditService(
        IGeometryEditValidator validator,
        IEditableGeometryDescriptorService descriptorService,
        IGeometryReconstructor reconstructor,
        IGeometryMutationService mutationService)
    {
        _validator = validator;
        _descriptorService = descriptorService;
        _reconstructor = reconstructor;
        _mutationService = mutationService;
    }

    public OperationResponse<GeometryEditPreviewResponse> Preview(PreviewEditCurveGeometryRequest request)
    {
        OperationResponse<CurveEditPreparation> preparation = Prepare(request.FilePath, request.ObjectId, request.EditSpec);
        if (!preparation.Success || preparation.Data is null)
        {
            return OperationResponse<GeometryEditPreviewResponse>.Fail(preparation.Message);
        }

        CurveEditPreparation data = preparation.Data;
        var warnings = data.Reconstruction.Warnings.ToList();
        if (!data.Reconstruction.Summary.IsValid)
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
            Strategy = data.Reconstruction.Strategy,
            DescriptorSummary = data.Descriptor.CurveStructure,
            DescriptorPointCount = data.Descriptor.ControlPointCount,
            ReconstructedCurveSummary = data.Reconstruction.Summary,
            Warnings = warnings
        }, "Curve geometry edit preview generated.");
    }

    public OperationResponse<GeometryEditApplyResponse> Apply(ApplyEditCurveGeometryRequest request)
    {
        OperationResponse<CurveEditPreparation> preparation = Prepare(request.FilePath, request.ObjectId, request.EditSpec);
        if (!preparation.Success || preparation.Data is null)
        {
            return OperationResponse<GeometryEditApplyResponse>.Fail(preparation.Message);
        }

        CurveEditPreparation data = preparation.Data;
        if (!data.Reconstruction.Summary.IsValid)
        {
            return OperationResponse<GeometryEditApplyResponse>.Fail("RECONSTRUCTION_INVALID");
        }

        OperationResponse<IReadOnlyList<ObjectEditWarning>> mutation = _mutationService.ReplaceWithMetadata(
            request.FilePath,
            request.ObjectId,
            data.Reconstruction.Curve,
            UndoRecordName);
        if (!mutation.Success)
        {
            return OperationResponse<GeometryEditApplyResponse>.Fail(mutation.Message);
        }

        var warnings = data.Reconstruction.Warnings
            .Concat(mutation.Data ?? Array.Empty<ObjectEditWarning>())
            .ToList();

        return OperationResponse<GeometryEditApplyResponse>.Ok(new GeometryEditApplyResponse
        {
            FilePath = request.FilePath,
            ObjectId = request.ObjectId,
            Strategy = data.Reconstruction.Strategy,
            UndoRecordName = UndoRecordName,
            MetadataDropped = warnings
                .Where(warning => string.Equals(warning.Code, "METADATA_FIELDS_DROPPED", StringComparison.Ordinal))
                .Select(warning => warning.Message)
                .ToList(),
            Warnings = warnings
        }, "Curve geometry edit applied.");
    }

    private OperationResponse<CurveEditPreparation> Prepare(string filePath, Guid objectId, CurveEditSpec editSpec)
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

        OperationResponse<CurveReconstructionResult> reconstruction = _reconstructor.ReconstructCurve(
            filePath,
            objectId,
            descriptor,
            editSpec.Points);
        if (!reconstruction.Success || reconstruction.Data is null)
        {
            return OperationResponse<CurveEditPreparation>.Fail(reconstruction.Message);
        }

        return OperationResponse<CurveEditPreparation>.Ok(new CurveEditPreparation
        {
            Descriptor = descriptor,
            Reconstruction = reconstruction.Data
        });
    }

    private sealed class CurveEditPreparation
    {
        public EditableGeometryDescriptor Descriptor { get; set; } = new();
        public CurveReconstructionResult Reconstruction { get; set; } = new();
    }
}

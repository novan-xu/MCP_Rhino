using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Skills.Modeling;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveGeometryTransformExecutionBridge : IGeometryTransformExecutionBridge
{
    private readonly GeometryModificationSkill _geometryModificationSkill;

    public LiveGeometryTransformExecutionBridge(GeometryModificationSkill geometryModificationSkill)
    {
        _geometryModificationSkill = geometryModificationSkill;
    }

    public OperationResponse<GeometryModificationPreviewResponse> PreviewExactTransform(
        string filePath,
        Guid objectId,
        CurveEditSpec editSpec,
        EditableGeometryDescriptor descriptor)
    {
        OperationResponse<GeometryTransformSpec> transform = BuildTransform(editSpec, descriptor);
        if (!transform.Success || transform.Data is null)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail(transform.Message);
        }

        return _geometryModificationSkill.PreviewTransformForGeometryEdit(new PreviewTransformObjectsRequest
        {
            FilePath = filePath,
            Transform = transform.Data,
            ConfirmedObjectIds = new List<Guid> { objectId }
        });
    }

    public OperationResponse<GeometryModificationResponse> ApplyExactTransform(
        string filePath,
        Guid objectId,
        CurveEditSpec editSpec,
        EditableGeometryDescriptor descriptor,
        string undoDescription)
    {
        OperationResponse<GeometryTransformSpec> transform = BuildTransform(editSpec, descriptor);
        if (!transform.Success || transform.Data is null)
        {
            return OperationResponse<GeometryModificationResponse>.Fail(transform.Message);
        }

        return _geometryModificationSkill.ApplyTransformForGeometryEdit(new TransformObjectsRequest
        {
            FilePath = filePath,
            Transform = transform.Data,
            ConfirmedObjectIds = new List<Guid> { objectId }
        }, undoDescription);
    }

    public OperationResponse<GeometryModificationPreviewResponse> PreviewSurfaceExactTransform(
        string filePath,
        Guid objectId,
        SurfaceEditSpec editSpec,
        EditableGeometryDescriptor descriptor)
    {
        OperationResponse<GeometryTransformSpec> transform = BuildSurfaceTransform(editSpec, descriptor);
        if (!transform.Success || transform.Data is null)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail(transform.Message);
        }

        return _geometryModificationSkill.PreviewTransformForGeometryEdit(new PreviewTransformObjectsRequest
        {
            FilePath = filePath,
            Transform = transform.Data,
            ConfirmedObjectIds = new List<Guid> { objectId }
        });
    }

    public OperationResponse<GeometryModificationResponse> ApplySurfaceExactTransform(
        string filePath,
        Guid objectId,
        SurfaceEditSpec editSpec,
        EditableGeometryDescriptor descriptor,
        string undoDescription)
    {
        OperationResponse<GeometryTransformSpec> transform = BuildSurfaceTransform(editSpec, descriptor);
        if (!transform.Success || transform.Data is null)
        {
            return OperationResponse<GeometryModificationResponse>.Fail(transform.Message);
        }

        return _geometryModificationSkill.ApplyTransformForGeometryEdit(new TransformObjectsRequest
        {
            FilePath = filePath,
            Transform = transform.Data,
            ConfirmedObjectIds = new List<Guid> { objectId }
        }, undoDescription);
    }

    private static OperationResponse<GeometryTransformSpec> BuildTransform(
        CurveEditSpec editSpec,
        EditableGeometryDescriptor descriptor)
    {
        if (editSpec.Operation.Kind != GeometryEditOperationKind.DerivedOperation || !editSpec.Operation.DerivedKind.HasValue)
        {
            return OperationResponse<GeometryTransformSpec>.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        DerivedPointOperationParameters parameters = editSpec.Operation.DerivedParameters ?? new DerivedPointOperationParameters();
        return editSpec.Operation.DerivedKind.Value switch
        {
            DerivedPointOperationKind.TranslateByVector => OperationResponse<GeometryTransformSpec>.Ok(new GeometryTransformSpec
            {
                Kind = GeometryTransformKind.Translate,
                VectorX = parameters.VectorX,
                VectorY = parameters.VectorY,
                VectorZ = parameters.VectorZ
            }),
            DerivedPointOperationKind.ScaleAboutCentroid => OperationResponse<GeometryTransformSpec>.Ok(new GeometryTransformSpec
            {
                Kind = GeometryTransformKind.UniformScale,
                CenterX = descriptor.ControlPointCentroidWorld.X,
                CenterY = descriptor.ControlPointCentroidWorld.Y,
                CenterZ = descriptor.ControlPointCentroidWorld.Z,
                ScaleFactor = parameters.ScaleX
            }),
            _ => OperationResponse<GeometryTransformSpec>.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE")
        };
    }

    private static OperationResponse<GeometryTransformSpec> BuildSurfaceTransform(
        SurfaceEditSpec editSpec,
        EditableGeometryDescriptor descriptor)
    {
        if (editSpec.Operation.Kind != GeometryEditOperationKind.DerivedOperation || !editSpec.Operation.DerivedKind.HasValue)
        {
            return OperationResponse<GeometryTransformSpec>.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE");
        }

        SurfaceEditDerivedOperationParameters parameters = editSpec.Operation.DerivedParameters ?? new SurfaceEditDerivedOperationParameters();
        return editSpec.Operation.DerivedKind.Value switch
        {
            DerivedPointOperationKind.TranslateByVector => OperationResponse<GeometryTransformSpec>.Ok(new GeometryTransformSpec
            {
                Kind = GeometryTransformKind.Translate,
                VectorX = parameters.VectorX,
                VectorY = parameters.VectorY,
                VectorZ = parameters.VectorZ
            }),
            DerivedPointOperationKind.ScaleAboutCentroid => OperationResponse<GeometryTransformSpec>.Ok(new GeometryTransformSpec
            {
                Kind = GeometryTransformKind.UniformScale,
                CenterX = descriptor.ControlPointCentroidWorld.X,
                CenterY = descriptor.ControlPointCentroidWorld.Y,
                CenterZ = descriptor.ControlPointCentroidWorld.Z,
                ScaleFactor = parameters.ScaleX
            }),
            _ => OperationResponse<GeometryTransformSpec>.Fail("EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE")
        };
    }
}

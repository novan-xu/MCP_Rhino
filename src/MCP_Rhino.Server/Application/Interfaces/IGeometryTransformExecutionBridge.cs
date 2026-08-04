using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IGeometryTransformExecutionBridge
{
    OperationResponse<GeometryModificationPreviewResponse> PreviewExactTransform(
        string filePath,
        Guid objectId,
        CurveEditSpec editSpec,
        EditableGeometryDescriptor descriptor);

    OperationResponse<GeometryModificationResponse> ApplyExactTransform(
        string filePath,
        Guid objectId,
        CurveEditSpec editSpec,
        EditableGeometryDescriptor descriptor,
        string undoDescription);

    OperationResponse<GeometryModificationPreviewResponse> PreviewSurfaceExactTransform(
        string filePath,
        Guid objectId,
        SurfaceEditSpec editSpec,
        EditableGeometryDescriptor descriptor);

    OperationResponse<GeometryModificationResponse> ApplySurfaceExactTransform(
        string filePath,
        Guid objectId,
        SurfaceEditSpec editSpec,
        EditableGeometryDescriptor descriptor,
        string undoDescription);
}

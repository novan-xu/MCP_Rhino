using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveCurveDerivedGeometryOperator
{
    OperationResponse<CurveDerivedGeometryResponse> CreateLofts(string filePath, IReadOnlyList<LoftSpec> entries, GeometryObjectAttributesSpec attributes);
    OperationResponse<CurveDerivedGeometryResponse> CreateCurveExtrusions(string filePath, IReadOnlyList<CurveExtrusionSpec> entries, GeometryObjectAttributesSpec attributes);
    OperationResponse<CurveDerivedGeometryResponse> CreateSweepOneRail(string filePath, IReadOnlyList<SweepOneRailSpec> entries, GeometryObjectAttributesSpec attributes);
    OperationResponse<CurveDerivedGeometryResponse> CreateProfileExtrusionsFromPoints(string filePath, IReadOnlyList<ProfileExtrusionFromPointsSpec> entries, GeometryObjectAttributesSpec attributes);
    OperationResponse<CurveDerivedGeometryResponse> CreateLoftsFromProfiles(string filePath, IReadOnlyList<LoftFromProfilesSpec> entries, GeometryObjectAttributesSpec attributes);
    OperationResponse<CurveDerivedGeometryResponse> CreatePipesFromPoints(string filePath, IReadOnlyList<PipeFromPointsSpec> entries, GeometryObjectAttributesSpec attributes);
    OperationResponse<CurveDerivedGeometryResponse> CreateCurveOffsets(string filePath, IReadOnlyList<CurveOffsetSpec> entries, GeometryObjectAttributesSpec attributes);
    OperationResponse<CurveDerivedGeometryResponse> CreatePipes(string filePath, IReadOnlyList<PipeSpec> entries, GeometryObjectAttributesSpec attributes);
    OperationResponse<CurveDerivedGeometryResponse> ProjectCurves(string filePath, IReadOnlyList<CurveProjectionSpec> entries, GeometryObjectAttributesSpec attributes);
    OperationResponse<CurveSplitPreviewResponse> PreviewSplitCurves(string filePath, IReadOnlyList<CurveSplitSpec> entries);
    OperationResponse<CurveDerivedGeometryResponse> CreateSplitCurveSegments(string filePath, IReadOnlyList<CurveSplitSpec> entries, GeometryObjectAttributesSpec attributes);
    OperationResponse<CurveDerivedGeometryResponse> ReplaceSplitCurves(string filePath, IReadOnlyList<CurveSplitSpec> entries, GeometryObjectAttributesSpec attributes);
}

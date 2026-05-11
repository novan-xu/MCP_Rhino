using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoCurveDerivedGeometryService
{
    private readonly ILiveCurveDerivedGeometryOperator _operator;

    public RhinoCurveDerivedGeometryService(ILiveCurveDerivedGeometryOperator curveOperator)
    {
        _operator = curveOperator;
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateLofts(CreateLoftsRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Entries.Count, "CreateLofts");
        return validation.Success
            ? _operator.CreateLofts(request.FilePath, request.Entries.Select(MapLoft).ToList(), MapAttributes(request.Common))
            : OperationResponse<CurveDerivedGeometryResponse>.Fail(validation.Message);
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateCurveExtrusions(CreateCurveExtrusionsRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Entries.Count, "CreateCurveExtrusions");
        return validation.Success
            ? _operator.CreateCurveExtrusions(request.FilePath, request.Entries.Select(MapCurveExtrusion).ToList(), MapAttributes(request.Common))
            : OperationResponse<CurveDerivedGeometryResponse>.Fail(validation.Message);
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateSweepOneRail(CreateSweepOneRailRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Entries.Count, "CreateSweepOneRail");
        return validation.Success
            ? _operator.CreateSweepOneRail(request.FilePath, request.Entries.Select(MapSweepOneRail).ToList(), MapAttributes(request.Common))
            : OperationResponse<CurveDerivedGeometryResponse>.Fail(validation.Message);
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateProfileExtrusionsFromPoints(
        CreateProfileExtrusionsFromPointsRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Entries.Count, "CreateProfileExtrusionsFromPoints");
        return validation.Success
            ? _operator.CreateProfileExtrusionsFromPoints(request.FilePath, request.Entries.Select(MapProfileExtrusionFromPoints).ToList(), MapAttributes(request.Common))
            : OperationResponse<CurveDerivedGeometryResponse>.Fail(validation.Message);
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateLoftsFromProfiles(
        CreateLoftsFromProfilesRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Entries.Count, "CreateLoftsFromProfiles");
        return validation.Success
            ? _operator.CreateLoftsFromProfiles(request.FilePath, request.Entries.Select(MapLoftFromProfiles).ToList(), MapAttributes(request.Common))
            : OperationResponse<CurveDerivedGeometryResponse>.Fail(validation.Message);
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreatePipesFromPoints(
        CreatePipesFromPointsRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Entries.Count, "CreatePipesFromPoints");
        return validation.Success
            ? _operator.CreatePipesFromPoints(request.FilePath, request.Entries.Select(MapPipeFromPoints).ToList(), MapAttributes(request.Common))
            : OperationResponse<CurveDerivedGeometryResponse>.Fail(validation.Message);
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateCurveOffsets(CreateCurveOffsetsRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Entries.Count, "CreateCurveOffsets");
        return validation.Success
            ? _operator.CreateCurveOffsets(request.FilePath, request.Entries.Select(MapCurveOffset).ToList(), MapAttributes(request.Common))
            : OperationResponse<CurveDerivedGeometryResponse>.Fail(validation.Message);
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreatePipes(CreatePipesRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Entries.Count, "CreatePipes");
        return validation.Success
            ? _operator.CreatePipes(request.FilePath, request.Entries.Select(MapPipe).ToList(), MapAttributes(request.Common))
            : OperationResponse<CurveDerivedGeometryResponse>.Fail(validation.Message);
    }

    public OperationResponse<CurveDerivedGeometryResponse> ProjectCurves(ProjectCurvesRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Entries.Count, "ProjectCurves");
        return validation.Success
            ? _operator.ProjectCurves(request.FilePath, request.Entries.Select(MapProjection).ToList(), MapAttributes(request.Common))
            : OperationResponse<CurveDerivedGeometryResponse>.Fail(validation.Message);
    }

    public OperationResponse<CurveSplitPreviewResponse> PreviewSplitCurves(PreviewSplitCurvesRequest request)
    {
        OperationResponse validation = ValidateReadRequest(request.FilePath, request.Entries.Count, "PreviewSplitCurves");
        return validation.Success
            ? _operator.PreviewSplitCurves(request.FilePath, request.Entries.Select(MapSplit).ToList())
            : OperationResponse<CurveSplitPreviewResponse>.Fail(validation.Message);
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateSplitCurveSegments(CreateSplitCurveSegmentsRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Entries.Count, "CreateSplitCurveSegments");
        return validation.Success
            ? _operator.CreateSplitCurveSegments(request.FilePath, request.Entries.Select(MapSplit).ToList(), MapAttributes(request.Common))
            : OperationResponse<CurveDerivedGeometryResponse>.Fail(validation.Message);
    }

    public OperationResponse<CurveDerivedGeometryResponse> ReplaceSplitCurves(ReplaceSplitCurvesRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Entries.Count, "ReplaceSplitCurves");
        return validation.Success
            ? _operator.ReplaceSplitCurves(request.FilePath, request.Entries.Select(MapSplit).ToList(), MapAttributes(request.Common))
            : OperationResponse<CurveDerivedGeometryResponse>.Fail(validation.Message);
    }

    private static OperationResponse ValidateCreateRequest(string filePath, int entryCount, string operation)
    {
        OperationResponse readValidation = ValidateReadRequest(filePath, entryCount, operation);
        if (!readValidation.Success)
        {
            return readValidation;
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateReadRequest(string filePath, int entryCount, string operation)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResponse.Fail("FilePath is required.");
        }

        return entryCount == 0
            ? OperationResponse.Fail($"At least one {operation} entry is required.")
            : OperationResponse.Ok();
    }

    private static LoftSpec MapLoft(LoftEntryRequest request)
    {
        return new LoftSpec
        {
            CurveObjectIds = request.CurveObjectIds,
            LoftStyle = request.LoftStyle,
            Closed = request.Closed,
            Name = request.Name
        };
    }

    private static CurveExtrusionSpec MapCurveExtrusion(CurveExtrusionEntryRequest request)
    {
        return new CurveExtrusionSpec
        {
            CurveObjectId = request.CurveObjectId,
            VectorX = request.VectorX,
            VectorY = request.VectorY,
            VectorZ = request.VectorZ,
            Cap = request.Cap,
            Name = request.Name
        };
    }

    private static SweepOneRailSpec MapSweepOneRail(SweepOneRailEntryRequest request)
    {
        return new SweepOneRailSpec
        {
            RailCurveObjectId = request.RailCurveObjectId,
            ProfileCurveObjectIds = request.ProfileCurveObjectIds,
            Name = request.Name
        };
    }

    private static ProfileExtrusionFromPointsSpec MapProfileExtrusionFromPoints(
        ProfileExtrusionFromPointsEntryRequest request)
    {
        return new ProfileExtrusionFromPointsSpec
        {
            ProfilePoints = request.ProfilePoints.Select(MapPointSpec).ToList(),
            VectorX = request.VectorX,
            VectorY = request.VectorY,
            VectorZ = request.VectorZ,
            Cap = request.Cap,
            Name = request.Name
        };
    }

    private static LoftFromProfilesSpec MapLoftFromProfiles(LoftFromProfilesEntryRequest request)
    {
        return new LoftFromProfilesSpec
        {
            Profiles = request.Profiles.Select(profile => new CurvePointProfileSpec
            {
                Points = profile.Points.Select(MapPointSpec).ToList(),
                Closed = profile.Closed
            }).ToList(),
            LoftStyle = request.LoftStyle,
            ClosedLoft = request.ClosedLoft,
            Name = request.Name
        };
    }

    private static PipeFromPointsSpec MapPipeFromPoints(PipeFromPointsEntryRequest request)
    {
        return new PipeFromPointsSpec
        {
            Points = request.Points.Select(MapPointSpec).ToList(),
            Radius = request.Radius,
            CapStyle = request.CapStyle,
            LocalBlending = request.LocalBlending,
            FitRail = request.FitRail,
            Name = request.Name
        };
    }

    private static CurveOffsetSpec MapCurveOffset(CurveOffsetEntryRequest request)
    {
        return new CurveOffsetSpec
        {
            CurveObjectId = request.CurveObjectId,
            Distance = request.Distance,
            PlaneOriginX = request.PlaneOriginX,
            PlaneOriginY = request.PlaneOriginY,
            PlaneOriginZ = request.PlaneOriginZ,
            PlaneNormalX = request.PlaneNormalX,
            PlaneNormalY = request.PlaneNormalY,
            PlaneNormalZ = request.PlaneNormalZ,
            CornerStyle = request.CornerStyle,
            Name = request.Name
        };
    }

    private static CurveSplitPointSpec MapPointSpec(GeneralPrimitivePointRequest request)
    {
        return new CurveSplitPointSpec
        {
            X = request.X,
            Y = request.Y,
            Z = request.Z
        };
    }

    private static PipeSpec MapPipe(PipeEntryRequest request)
    {
        return new PipeSpec
        {
            CurveObjectId = request.CurveObjectId,
            Radius = request.Radius,
            CapStyle = request.CapStyle,
            LocalBlending = request.LocalBlending,
            FitRail = request.FitRail,
            Name = request.Name
        };
    }

    private static CurveProjectionSpec MapProjection(CurveProjectionEntryRequest request)
    {
        return new CurveProjectionSpec
        {
            CurveObjectId = request.CurveObjectId,
            TargetObjectIds = request.TargetObjectIds,
            DirectionX = request.DirectionX,
            DirectionY = request.DirectionY,
            DirectionZ = request.DirectionZ,
            Name = request.Name
        };
    }

    private static CurveSplitSpec MapSplit(CurveSplitEntryRequest request)
    {
        return new CurveSplitSpec
        {
            CurveObjectId = request.CurveObjectId,
            Parameters = request.Parameters,
            Points = request.Points.Select(point => new CurveSplitPointSpec
            {
                X = point.X,
                Y = point.Y,
                Z = point.Z
            }).ToList(),
            PointTolerance = request.PointTolerance,
            Name = request.Name
        };
    }

    private static GeometryObjectAttributesSpec MapAttributes(GeometryCreationCommonOptions? options)
    {
        GeometryCreationCommonOptions common = options ?? new GeometryCreationCommonOptions();
        return new GeometryObjectAttributesSpec
        {
            LayerFullPath = common.LayerFullPath,
            Color = common.Color is null
                ? null
                : new RhinoDisplayColor
                {
                    R = common.Color.R,
                    G = common.Color.G,
                    B = common.Color.B
                },
            Name = common.Name,
            UserText = new Dictionary<string, string>(common.UserText, StringComparer.OrdinalIgnoreCase)
        };
    }
}

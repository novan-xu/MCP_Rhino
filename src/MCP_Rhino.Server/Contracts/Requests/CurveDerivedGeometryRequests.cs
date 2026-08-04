using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class CreateLoftsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<LoftEntryRequest> Entries { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class LoftEntryRequest
{
    public List<Guid> CurveObjectIds { get; set; } = new();
    public CurveLoftStyle LoftStyle { get; set; } = CurveLoftStyle.Normal;
    public bool Closed { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateCurveExtrusionsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<CurveExtrusionEntryRequest> Entries { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class CurveExtrusionEntryRequest
{
    public Guid CurveObjectId { get; set; }
    public double VectorX { get; set; }
    public double VectorY { get; set; }
    public double VectorZ { get; set; } = 1d;
    public bool Cap { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateSweepOneRailRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<SweepOneRailEntryRequest> Entries { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class SweepOneRailEntryRequest
{
    public Guid RailCurveObjectId { get; set; }
    public List<Guid> ProfileCurveObjectIds { get; set; } = new();
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateProfileExtrusionsFromPointsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<ProfileExtrusionFromPointsEntryRequest> Entries { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class ProfileExtrusionFromPointsEntryRequest
{
    public List<GeneralPrimitivePointRequest> ProfilePoints { get; set; } = new();
    public double VectorX { get; set; }
    public double VectorY { get; set; }
    public double VectorZ { get; set; } = 1d;
    public bool Cap { get; set; } = true;
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateLoftsFromProfilesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<LoftFromProfilesEntryRequest> Entries { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class LoftFromProfilesEntryRequest
{
    public List<CurvePointProfileRequest> Profiles { get; set; } = new();
    public CurveLoftStyle LoftStyle { get; set; } = CurveLoftStyle.Normal;
    public bool ClosedLoft { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CurvePointProfileRequest
{
    public List<GeneralPrimitivePointRequest> Points { get; set; } = new();
    public bool Closed { get; set; } = true;
}

public sealed class CreatePipesFromPointsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<PipeFromPointsEntryRequest> Entries { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class PipeFromPointsEntryRequest
{
    public List<GeneralPrimitivePointRequest> Points { get; set; } = new();
    public double Radius { get; set; }
    public CurvePipeCapStyle CapStyle { get; set; } = CurvePipeCapStyle.Flat;
    public bool LocalBlending { get; set; }
    public bool FitRail { get; set; } = true;
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateCurveOffsetsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<CurveOffsetEntryRequest> Entries { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class CurveOffsetEntryRequest
{
    public Guid CurveObjectId { get; set; }
    public double Distance { get; set; }
    public double PlaneOriginX { get; set; }
    public double PlaneOriginY { get; set; }
    public double PlaneOriginZ { get; set; }
    public double PlaneNormalX { get; set; }
    public double PlaneNormalY { get; set; }
    public double PlaneNormalZ { get; set; } = 1d;
    public CurveOffsetCornerStyle CornerStyle { get; set; } = CurveOffsetCornerStyle.Sharp;
    public string Name { get; set; } = string.Empty;
}

public sealed class CreatePipesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<PipeEntryRequest> Entries { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class PipeEntryRequest
{
    public Guid CurveObjectId { get; set; }
    public double Radius { get; set; }
    public CurvePipeCapStyle CapStyle { get; set; } = CurvePipeCapStyle.Flat;
    public bool LocalBlending { get; set; }
    public bool FitRail { get; set; } = true;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProjectCurvesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<CurveProjectionEntryRequest> Entries { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class CurveProjectionEntryRequest
{
    public Guid CurveObjectId { get; set; }
    public List<Guid> TargetObjectIds { get; set; } = new();
    public double DirectionX { get; set; }
    public double DirectionY { get; set; }
    public double DirectionZ { get; set; } = -1d;
    public string Name { get; set; } = string.Empty;
}

public sealed class PreviewSplitCurvesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<CurveSplitEntryRequest> Entries { get; set; } = new();
}

public sealed class CreateSplitCurveSegmentsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<CurveSplitEntryRequest> Entries { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class ReplaceSplitCurvesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<CurveSplitEntryRequest> Entries { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class CurveSplitEntryRequest
{
    public Guid CurveObjectId { get; set; }
    public List<double> Parameters { get; set; } = new();
    public List<CurveSplitPointRequest> Points { get; set; } = new();
    public double PointTolerance { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CurveSplitPointRequest
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

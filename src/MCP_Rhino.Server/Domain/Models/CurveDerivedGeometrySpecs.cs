using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class CurveSplitPointSpec
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class LoftSpec
{
    public IReadOnlyList<Guid> CurveObjectIds { get; set; } = Array.Empty<Guid>();
    public CurveLoftStyle LoftStyle { get; set; } = CurveLoftStyle.Normal;
    public bool Closed { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CurveExtrusionSpec
{
    public Guid CurveObjectId { get; set; }
    public double VectorX { get; set; }
    public double VectorY { get; set; }
    public double VectorZ { get; set; }
    public bool Cap { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class SweepOneRailSpec
{
    public Guid RailCurveObjectId { get; set; }
    public IReadOnlyList<Guid> ProfileCurveObjectIds { get; set; } = Array.Empty<Guid>();
    public string Name { get; set; } = string.Empty;
}

public sealed class ProfileExtrusionFromPointsSpec
{
    public IReadOnlyList<CurveSplitPointSpec> ProfilePoints { get; set; } = Array.Empty<CurveSplitPointSpec>();
    public double VectorX { get; set; }
    public double VectorY { get; set; }
    public double VectorZ { get; set; } = 1d;
    public bool Cap { get; set; } = true;
    public string Name { get; set; } = string.Empty;
}

public sealed class LoftFromProfilesSpec
{
    public IReadOnlyList<CurvePointProfileSpec> Profiles { get; set; } = Array.Empty<CurvePointProfileSpec>();
    public CurveLoftStyle LoftStyle { get; set; } = CurveLoftStyle.Normal;
    public bool ClosedLoft { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CurvePointProfileSpec
{
    public IReadOnlyList<CurveSplitPointSpec> Points { get; set; } = Array.Empty<CurveSplitPointSpec>();
    public bool Closed { get; set; } = true;
}

public sealed class PipeFromPointsSpec
{
    public IReadOnlyList<CurveSplitPointSpec> Points { get; set; } = Array.Empty<CurveSplitPointSpec>();
    public double Radius { get; set; }
    public CurvePipeCapStyle CapStyle { get; set; } = CurvePipeCapStyle.Flat;
    public bool LocalBlending { get; set; }
    public bool FitRail { get; set; } = true;
    public string Name { get; set; } = string.Empty;
}

public sealed class CurveOffsetSpec
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

public sealed class PipeSpec
{
    public Guid CurveObjectId { get; set; }
    public double Radius { get; set; }
    public CurvePipeCapStyle CapStyle { get; set; } = CurvePipeCapStyle.Flat;
    public bool LocalBlending { get; set; }
    public bool FitRail { get; set; } = true;
    public string Name { get; set; } = string.Empty;
}

public sealed class CurveProjectionSpec
{
    public Guid CurveObjectId { get; set; }
    public IReadOnlyList<Guid> TargetObjectIds { get; set; } = Array.Empty<Guid>();
    public double DirectionX { get; set; }
    public double DirectionY { get; set; }
    public double DirectionZ { get; set; } = -1d;
    public string Name { get; set; } = string.Empty;
}

public sealed class CurveSplitSpec
{
    public Guid CurveObjectId { get; set; }
    public IReadOnlyList<double> Parameters { get; set; } = Array.Empty<double>();
    public IReadOnlyList<CurveSplitPointSpec> Points { get; set; } = Array.Empty<CurveSplitPointSpec>();
    public double PointTolerance { get; set; }
    public string Name { get; set; } = string.Empty;
}

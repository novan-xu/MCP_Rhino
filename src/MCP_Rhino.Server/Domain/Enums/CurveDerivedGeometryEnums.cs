namespace MCP_Rhino.Server.Domain.Enums;

public enum CurveDerivedOperationKind
{
    Loft,
    CurveExtrusion,
    SweepOneRail,
    ProfileExtrusionFromPoints,
    LoftFromProfiles,
    PipeFromPoints,
    CurveOffset,
    Pipe,
    Projection,
    SplitSegments,
    SplitReplace
}

public enum CurveLoftStyle
{
    Normal,
    Loose,
    Tight,
    Straight,
    Uniform
}

public enum CurveOffsetCornerStyle
{
    None,
    Sharp,
    Round,
    Smooth,
    Chamfer
}

public enum CurvePipeCapStyle
{
    None,
    Flat,
    Round
}

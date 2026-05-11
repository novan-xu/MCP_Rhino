using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public abstract class SelectionScopedAnalysisRequestBase
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> LayerQueries { get; set; } = new();
    public List<string> ConfirmedLayerFullPaths { get; set; } = new();
    public List<string> ObjectTypes { get; set; } = new();
    public List<UserAttributeConditionRequest> UserAttributeConditions { get; set; } = new();
    public FilterMatchMode MatchMode { get; set; } = FilterMatchMode.All;
    public FilterMatchMode UserAttributeMatchMode { get; set; } = FilterMatchMode.All;
}

public sealed class GetObjectMetricsByFilterRequest : SelectionScopedAnalysisRequestBase
{
}

public sealed class GetMassPropertiesByFilterRequest : SelectionScopedAnalysisRequestBase
{
    public GeometryMassKind Kind { get; set; } = GeometryMassKind.Auto;
}

public sealed class GetGeometryFramesByFilterRequest : SelectionScopedAnalysisRequestBase
{
    public string EntryIdPrefix { get; set; } = "frame";
    public GeometryFrameKind Kind { get; set; } = GeometryFrameKind.SurfaceFrame;
    public double? Parameter { get; set; }
    public double? U { get; set; }
    public double? V { get; set; }
    public GeometryFrameParameterSpecRequest? ParameterSpec { get; set; }
}

public sealed class GetCurvatureSamplesByFilterRequest : SelectionScopedAnalysisRequestBase
{
    public string EntryIdPrefix { get; set; } = "curvature";
    public GeometryCurvatureSamplingMode Mode { get; set; } = GeometryCurvatureSamplingMode.EvenByCount;
    public int SampleCount { get; set; } = 10;
    public double StepLength { get; set; }
    public List<double> Parameters { get; set; } = new();
    public List<GeometryAnalysisUvSampleRequest> UvSamples { get; set; } = new();
}

public sealed class GetContourCurvesByFilterRequest : SelectionScopedAnalysisRequestBase
{
    public string EntryIdPrefix { get; set; } = "contour";
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }
    public double Interval { get; set; } = 1d;
}

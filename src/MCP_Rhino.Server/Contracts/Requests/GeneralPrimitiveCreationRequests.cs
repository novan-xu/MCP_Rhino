namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GeneralPrimitivePointRequest
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class CreateCirclesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<CircleItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class CircleItemRequest
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double Radius { get; set; }
    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; } = 1d;
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateEllipsesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<EllipseItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class EllipseItemRequest
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double RadiusX { get; set; }
    public double RadiusY { get; set; }
    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; } = 1d;
    public double XAxisX { get; set; } = 1d;
    public double XAxisY { get; set; }
    public double XAxisZ { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreatePolylinesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<PolylineItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class PolylineItemRequest
{
    public List<GeneralPrimitivePointRequest> Points { get; set; } = new();
    public bool Closed { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateNurbsCurvesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<NurbsCurveItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class NurbsCurveItemRequest
{
    public List<GeneralPrimitivePointRequest> ControlPoints { get; set; } = new();
    public int Degree { get; set; } = 3;
    public bool Closed { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateSpheresRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<SphereItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class SphereItemRequest
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double Radius { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateEllipsoidsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<EllipsoidItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class EllipsoidItemRequest
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double RadiusX { get; set; }
    public double RadiusY { get; set; }
    public double RadiusZ { get; set; }
    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; } = 1d;
    public double XAxisX { get; set; } = 1d;
    public double XAxisY { get; set; }
    public double XAxisZ { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateCapsulesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<CapsuleItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class CapsuleItemRequest
{
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; } = 1d;
    public double Radius { get; set; }
    public double Tolerance { get; set; } = 0.01d;
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateToriRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<TorusItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class TorusItemRequest
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double MajorRadius { get; set; }
    public double MinorRadius { get; set; }
    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; } = 1d;
    public double XAxisX { get; set; } = 1d;
    public double XAxisY { get; set; }
    public double XAxisZ { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateRoundedBoxesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<RoundedBoxItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class RoundedBoxItemRequest
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double Width { get; set; }
    public double Depth { get; set; }
    public double Height { get; set; }
    public double Radius { get; set; }
    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; } = 1d;
    public double XAxisX { get; set; } = 1d;
    public double XAxisY { get; set; }
    public double XAxisZ { get; set; }
    public double Tolerance { get; set; } = 0.01d;
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateRaisedStripsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<RaisedStripItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class RaisedStripItemRequest
{
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double UpX { get; set; }
    public double UpY { get; set; }
    public double UpZ { get; set; } = 1d;
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateTaperedBoxesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<TaperedBoxItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class TaperedBoxItemRequest
{
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; } = 1d;
    public double StartWidth { get; set; }
    public double StartDepth { get; set; }
    public double EndWidth { get; set; }
    public double EndDepth { get; set; }
    public double UpX { get; set; }
    public double UpY { get; set; }
    public double UpZ { get; set; } = 1d;
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateConesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<ConeItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class ConeItemRequest
{
    public double BaseX { get; set; }
    public double BaseY { get; set; }
    public double BaseZ { get; set; }
    public double Radius { get; set; }
    public double Height { get; set; }
    public double AxisX { get; set; }
    public double AxisY { get; set; }
    public double AxisZ { get; set; } = 1d;
    public bool CapBottom { get; set; } = true;
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateCylindersRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<CylinderItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class CylinderItemRequest
{
    public double BaseX { get; set; }
    public double BaseY { get; set; }
    public double BaseZ { get; set; }
    public double Radius { get; set; }
    public double Height { get; set; }
    public double AxisX { get; set; }
    public double AxisY { get; set; }
    public double AxisZ { get; set; } = 1d;
    public bool CapBottom { get; set; } = true;
    public bool CapTop { get; set; } = true;
    public string Name { get; set; } = string.Empty;
}

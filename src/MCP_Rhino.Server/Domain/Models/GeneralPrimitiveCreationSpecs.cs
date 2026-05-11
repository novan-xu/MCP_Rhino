using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeneralPrimitivePointSpec
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class GeneralPrimitiveCreationSpec
{
    public GeneralPrimitiveKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;

    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }

    public double BaseX { get; set; }
    public double BaseY { get; set; }
    public double BaseZ { get; set; }

    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }

    public double Radius { get; set; }
    public double RadiusX { get; set; }
    public double RadiusY { get; set; }
    public double RadiusZ { get; set; }
    public double MajorRadius { get; set; }
    public double MinorRadius { get; set; }
    public double Width { get; set; }
    public double Depth { get; set; }
    public double Height { get; set; }
    public double StartWidth { get; set; }
    public double StartDepth { get; set; }
    public double EndWidth { get; set; }
    public double EndDepth { get; set; }
    public double Tolerance { get; set; } = 0.01d;

    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; } = 1d;

    public double AxisX { get; set; }
    public double AxisY { get; set; }
    public double AxisZ { get; set; } = 1d;

    public double XAxisX { get; set; } = 1d;
    public double XAxisY { get; set; }
    public double XAxisZ { get; set; }

    public double UpX { get; set; }
    public double UpY { get; set; }
    public double UpZ { get; set; } = 1d;

    public List<GeneralPrimitivePointSpec> Points { get; set; } = new();
    public int Degree { get; set; } = 3;
    public bool Closed { get; set; }
    public bool CapBottom { get; set; } = true;
    public bool CapTop { get; set; } = true;
}

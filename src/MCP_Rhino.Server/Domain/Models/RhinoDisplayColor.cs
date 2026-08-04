using System.Drawing;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class RhinoDisplayColor
{
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }

    public Color ToColor()
    {
        return Color.FromArgb(Clamp(R), Clamp(G), Clamp(B));
    }

    private static int Clamp(int value)
    {
        return Math.Max(0, Math.Min(255, value));
    }
}
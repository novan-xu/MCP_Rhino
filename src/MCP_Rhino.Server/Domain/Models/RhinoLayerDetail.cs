namespace MCP_Rhino.Server.Domain.Models;

public sealed class RhinoLayerDetail
{
    public int LayerIndex { get; set; }
    public string LayerName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string? ParentFullPath { get; set; }
    public int ObjectCount { get; set; }
    public RhinoDisplayColor Color { get; set; } = new();
    public bool Visible { get; set; }
    public bool Locked { get; set; }
    public bool IsCurrentLayer { get; set; }
    public RhinoDisplayColor? PlotColor { get; set; }
    public double? PlotWeight { get; set; }
    public string? LinetypeName { get; set; }
    public string? RenderMaterialName { get; set; }
}

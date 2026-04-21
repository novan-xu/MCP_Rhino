using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class LayerModificationEntryRequest
{
    public string FullPath { get; set; } = string.Empty;
    public string? NewName { get; set; }
    public string? NewParentFullPath { get; set; }
    public RhinoDisplayColor? Color { get; set; }
    public bool? Visible { get; set; }
    public bool? Locked { get; set; }
    public RhinoDisplayColor? PlotColor { get; set; }
    public double? PlotWeight { get; set; }
    public string? LinetypeName { get; set; }
    public string? RenderMaterialName { get; set; }
    public bool ClearPlotColor { get; set; }
    public bool ClearPlotWeight { get; set; }
    public bool ClearLinetype { get; set; }
    public bool ClearRenderMaterial { get; set; }
}

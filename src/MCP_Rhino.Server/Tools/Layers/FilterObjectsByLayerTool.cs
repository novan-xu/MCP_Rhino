using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Skills.Inspection;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class FilterObjectsByLayerTool
{
    private readonly LayerObjectFilterSkill _layerObjectFilterSkill;

    public FilterObjectsByLayerTool(LayerObjectFilterSkill layerObjectFilterSkill)
    {
        _layerObjectFilterSkill = layerObjectFilterSkill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("按图层筛查 Rhino 物体。支持通过 layerQuery 查找候选图层；如存在歧义，请提供 confirmedLayerFullPath。")]
    public string FilterObjectsByLayer(string filePath, string layerQuery, string? confirmedLayerFullPath = null)
    {
        return _layerObjectFilterSkill.Filter(filePath, layerQuery, confirmedLayerFullPath);
    }
}
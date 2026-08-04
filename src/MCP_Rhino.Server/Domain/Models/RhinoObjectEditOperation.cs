using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class RhinoObjectEditOperation
{
    public ObjectEditOperationType OperationType { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string TargetLayerFullPath { get; set; } = string.Empty;
    public RhinoDisplayColor? Color { get; set; }
}
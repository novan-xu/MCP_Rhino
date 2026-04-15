using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ObjectEditOperationRequest
{
    public ObjectEditOperationType OperationType { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string TargetLayerFullPath { get; set; } = string.Empty;
    public ObjectColorRequest? Color { get; set; }
}
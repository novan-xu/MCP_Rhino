namespace MCP_Rhino.Server.Domain.Models.Grasshopper;

public sealed class GrasshopperGraphSpec
{
    public List<GrasshopperNodeSpec> Nodes { get; init; } = [];
    public List<GrasshopperWireSpec> Wires { get; init; } = [];
    public bool SolveAfterApply { get; init; } = true;
}

public sealed class GrasshopperNodeSpec
{
    public string ClientKey { get; init; } = string.Empty;
    public GrasshopperNodeKind Kind { get; init; } = GrasshopperNodeKind.Component;
    public Guid? ComponentGuid { get; init; }
    public string? ComponentName { get; init; }
    public Guid? ExistingObjectId { get; init; }
    public float CanvasX { get; init; }
    public float CanvasY { get; init; }
    public string? NickName { get; init; }
    public decimal? SliderMinimum { get; init; }
    public decimal? SliderMaximum { get; init; }
    public decimal? SliderValue { get; init; }
    public int? SliderDecimalPlaces { get; init; }
}

public sealed class GrasshopperWireSpec
{
    public string? SourceNodeKey { get; init; }
    public Guid? SourceObjectId { get; init; }
    public GrasshopperParameterSelector SourceParameter { get; init; } = new();
    public string? DestinationNodeKey { get; init; }
    public Guid? DestinationObjectId { get; init; }
    public GrasshopperParameterSelector DestinationParameter { get; init; } = new();
}

public sealed class GrasshopperParameterSelector
{
    public int? Index { get; init; }
    public string? Name { get; init; }
    public string? NickName { get; init; }
}

public sealed class ResolvedGrasshopperNode
{
    public string ClientKey { get; init; } = string.Empty;
    public GrasshopperNodeKind Kind { get; init; }
    public Guid? ComponentGuid { get; init; }
    public Guid? ExistingObjectId { get; init; }
    public bool IsExecutableCodeComponent { get; init; }
}

public sealed class ResolvedGrasshopperWire
{
    public string? SourceNodeKey { get; init; }
    public Guid? SourceObjectId { get; init; }
    public int SourceParameterIndex { get; init; }
    public string? DestinationNodeKey { get; init; }
    public Guid? DestinationObjectId { get; init; }
    public int DestinationParameterIndex { get; init; }
}

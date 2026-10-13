namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class BlockMemberInspectionResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string UnitSystem { get; set; } = string.Empty;
    public double AbsoluteTolerance { get; set; }
    public int RootInstanceCount { get; set; }
    public int LeafCount => Members.Count;
    public IReadOnlyList<BlockMemberResponse> Members { get; set; } = Array.Empty<BlockMemberResponse>();
}

public sealed class BlockMemberAncestorResponse
{
    public Guid InstanceObjectId { get; set; }
    public Guid DefinitionId { get; set; }
    public string DefinitionName { get; set; } = string.Empty;
    public Dictionary<string, string> UserText { get; set; } = new();
}

public sealed class BlockMemberResponse
{
    public Guid RootInstanceId { get; set; }
    public Guid MemberObjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LayerFullPath { get; set; } = string.Empty;
    public string GeometryType { get; set; } = string.Empty;
    public Dictionary<string, string> UserText { get; set; } = new();
    public IReadOnlyList<BlockMemberAncestorResponse> Ancestry { get; set; } = Array.Empty<BlockMemberAncestorResponse>();
    public IReadOnlyList<double> WorldTransform { get; set; } = Array.Empty<double>();
    public BlockMemberGeometryResponse Geometry { get; set; } = new();
}

public sealed class BlockMemberGeometryResponse
{
    // All coordinates and linear metrics are in model units, after the full occurrence transform.
    public string Status { get; set; } = "Unsupported";
    public string Reason { get; set; } = string.Empty;
    public bool IsSurface { get; set; }
    public int FaceCount { get; set; }
    public int LoopCount { get; set; }
    public double? Area { get; set; }
    public double? Perimeter { get; set; }
    public IReadOnlyList<double[]> Corners { get; set; } = Array.Empty<double[]>();
    public IReadOnlyList<double> EdgeLengths { get; set; } = Array.Empty<double>();
    public IReadOnlyList<double> DiagonalLengths { get; set; } = Array.Empty<double>();
}

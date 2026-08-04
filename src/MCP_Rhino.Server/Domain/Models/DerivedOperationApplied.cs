using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class DerivedOperationApplied
{
    public DerivedPointOperationKind Kind { get; set; }
    public GeometryPointData? CentroidWorld { get; set; }
    public GeometryVectorData? Vector { get; set; }
    public double? ScaleX { get; set; }
    public double? ScaleY { get; set; }
    public double? ScaleZ { get; set; }
    public double? Distance { get; set; }
    public IReadOnlyList<PerPointOffsetData> PerPointOffsets { get; set; } = Array.Empty<PerPointOffsetData>();
    public IReadOnlyList<ResolvedUvSampleData> ResolvedUvSamples { get; set; } = Array.Empty<ResolvedUvSampleData>();
}

public sealed class PerPointOffsetData
{
    public int Index { get; set; }
    public GeometryVectorData Offset { get; set; } = new();
}

public sealed class ResolvedUvSampleData
{
    public int Index { get; set; }
    public int? UIndex { get; set; }
    public int? VIndex { get; set; }
    public double U { get; set; }
    public double V { get; set; }
}

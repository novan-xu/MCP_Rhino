namespace MCP_Rhino.Server.Domain.Models;

public sealed class SubDPointSpec
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class SubDFaceSpec
{
    public IReadOnlyList<int> VertexIndices { get; set; } = Array.Empty<int>();
}

public sealed class SubDCageSpec
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<SubDPointSpec> Vertices { get; set; } = Array.Empty<SubDPointSpec>();
    public IReadOnlyList<SubDFaceSpec> Faces { get; set; } = Array.Empty<SubDFaceSpec>();
}

public sealed class SubDCagePreview
{
    public int VertexCount { get; set; }
    public int FaceCount { get; set; }
    public int BoundaryEdgeCount { get; set; }
    public int NonManifoldEdgeCount { get; set; }
    public GeneralPrimitiveBoundingBox? BoundingBox { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public sealed class GeneralPrimitiveBoundingBox
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MinZ { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double MaxZ { get; set; }
}

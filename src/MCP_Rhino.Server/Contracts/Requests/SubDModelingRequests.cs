namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class SubDVertexRequest
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class SubDFaceRequest
{
    public List<int> VertexIndices { get; set; } = new();
}

public sealed class PreviewSubDCageRequest
{
    public List<SubDVertexRequest> Vertices { get; set; } = new();
    public List<SubDFaceRequest> Faces { get; set; } = new();
}

public sealed class CreateSubDCageRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<SubDCageItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class SubDCageItemRequest
{
    public string Name { get; set; } = string.Empty;
    public List<SubDVertexRequest> Vertices { get; set; } = new();
    public List<SubDFaceRequest> Faces { get; set; } = new();
}

public sealed class CreateSubDBoxRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<SubDBoxItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class SubDBoxItemRequest
{
    public string Name { get; set; } = string.Empty;
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double Width { get; set; }
    public double Depth { get; set; }
    public double Height { get; set; }
    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; } = 1d;
    public double XAxisX { get; set; } = 1d;
    public double XAxisY { get; set; }
    public double XAxisZ { get; set; }
}

public sealed class CreateSubDCushionsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<SubDCushionItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}

public sealed class SubDCushionItemRequest
{
    public string Name { get; set; } = string.Empty;
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double Width { get; set; }
    public double Depth { get; set; }
    public double Height { get; set; }
    public double Crown { get; set; } = 0.08d;
    public double SideBulge { get; set; } = 0.04d;
    public double EdgeCompression { get; set; } = 0.02d;
    public double BackTiltDegrees { get; set; }
    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; } = 1d;
    public double XAxisX { get; set; } = 1d;
    public double XAxisY { get; set; }
    public double XAxisZ { get; set; }
}

public sealed class InspectSubDObjectsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<Guid> ObjectIds { get; set; } = new();
}

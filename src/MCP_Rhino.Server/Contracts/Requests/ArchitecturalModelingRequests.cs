using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ArchitecturalMetadataRequest
{
    public string Category { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string SourceTag { get; set; } = string.Empty;
}

public sealed class ArchitecturalPointRequest
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class CreateBoxesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BoxItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
    public ArchitecturalMetadataRequest Metadata { get; set; } = new();
    public bool AutoCreateLayers { get; set; }
}

public sealed class BoxItemRequest
{
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double OriginZ { get; set; }
    public double SizeX { get; set; }
    public double SizeY { get; set; }
    public double SizeZ { get; set; }
    public ArchitecturalRepresentationKind Representation { get; set; } = ArchitecturalRepresentationKind.Brep;
    public string Name { get; set; } = string.Empty;
    public ArchitecturalMetadataRequest? Metadata { get; set; }
}

public sealed class CreateExtrusionsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<ExtrusionItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
    public ArchitecturalMetadataRequest Metadata { get; set; } = new();
    public bool AutoCreateLayers { get; set; }
}

public sealed class ExtrusionItemRequest
{
    public List<ArchitecturalPointRequest> ProfilePoints { get; set; } = new();
    public double Height { get; set; }
    public bool Cap { get; set; } = true;
    public ArchitecturalRepresentationKind Representation { get; set; } = ArchitecturalRepresentationKind.Extrusion;
    public string Name { get; set; } = string.Empty;
    public ArchitecturalMetadataRequest? Metadata { get; set; }
}

public sealed class CreatePlanarBrepsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<PlanarBrepItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
    public ArchitecturalMetadataRequest Metadata { get; set; } = new();
    public bool AutoCreateLayers { get; set; }
}

public sealed class PlanarBrepItemRequest
{
    public List<PlanarLoopRequest> Loops { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public ArchitecturalMetadataRequest? Metadata { get; set; }
}

public sealed class PlanarLoopRequest
{
    public List<ArchitecturalPointRequest> Points { get; set; } = new();
}

public sealed class CreateSlabsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<SlabItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
    public ArchitecturalMetadataRequest Metadata { get; set; } = new();
    public bool AutoCreateLayers { get; set; }
}

public sealed class SlabItemRequest
{
    public List<ArchitecturalPointRequest> Footprint { get; set; } = new();
    public double Elevation { get; set; }
    public double Thickness { get; set; }
    public ArchitecturalRepresentationKind Representation { get; set; } = ArchitecturalRepresentationKind.Brep;
    public string Name { get; set; } = string.Empty;
    public ArchitecturalMetadataRequest? Metadata { get; set; }
}

public sealed class CreateWallsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<WallItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
    public ArchitecturalMetadataRequest Metadata { get; set; } = new();
    public bool AutoCreateLayers { get; set; }
}

public sealed class WallItemRequest
{
    public List<ArchitecturalPointRequest> Baseline { get; set; } = new();
    public double Height { get; set; }
    public double Thickness { get; set; }
    public double BaseElevation { get; set; }
    public WallAlignmentKind Alignment { get; set; } = WallAlignmentKind.Center;
    public string Name { get; set; } = string.Empty;
    public ArchitecturalMetadataRequest? Metadata { get; set; }
}

public sealed class CreateColumnsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<ColumnItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
    public ArchitecturalMetadataRequest Metadata { get; set; } = new();
    public bool AutoCreateLayers { get; set; }
}

public sealed class ColumnItemRequest
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public ProfileShapeKind ProfileShape { get; set; } = ProfileShapeKind.Rectangular;
    public double Radius { get; set; }
    public double Width { get; set; }
    public double Depth { get; set; }
    public double BaseElevation { get; set; }
    public double Height { get; set; }
    public ArchitecturalRepresentationKind Representation { get; set; } = ArchitecturalRepresentationKind.Brep;
    public string Name { get; set; } = string.Empty;
    public ArchitecturalMetadataRequest? Metadata { get; set; }
}

public sealed class CreateBeamsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BeamItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
    public ArchitecturalMetadataRequest Metadata { get; set; } = new();
    public bool AutoCreateLayers { get; set; }
}

public sealed class BeamItemRequest
{
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }
    public ProfileShapeKind ProfileShape { get; set; } = ProfileShapeKind.Rectangular;
    public double Radius { get; set; }
    public double Width { get; set; }
    public double Depth { get; set; }
    public string Name { get; set; } = string.Empty;
    public ArchitecturalMetadataRequest? Metadata { get; set; }
}

public sealed class PreviewBooleanObjectsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BooleanOperationEntryRequest> Entries { get; set; } = new();
    public double Tolerance { get; set; }
}

public sealed class ApplyBooleanObjectsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BooleanOperationEntryRequest> Entries { get; set; } = new();
    public double Tolerance { get; set; }
}

public sealed class BooleanOperationEntryRequest
{
    public BooleanOperationKind Operation { get; set; } = BooleanOperationKind.Difference;
    public List<Guid> TargetObjectIds { get; set; } = new();
    public List<Guid> CutterObjectIds { get; set; } = new();
    public bool DeleteTargets { get; set; } = true;
    public bool DeleteCutters { get; set; }
}

public sealed class PreviewOpeningsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<OpeningItemRequest> Items { get; set; } = new();
    public double Tolerance { get; set; }
}

public sealed class ApplyOpeningsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<OpeningItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
    public ArchitecturalMetadataRequest Metadata { get; set; } = new();
    public bool AutoCreateLayers { get; set; }
    public double Tolerance { get; set; }
}

public sealed class OpeningItemRequest
{
    public Guid TargetObjectId { get; set; }
    public OpeningKind Kind { get; set; } = OpeningKind.Rectangular;
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Depth { get; set; }
    public double Radius { get; set; }
    public bool RetainCutter { get; set; }
}

public sealed class CreateBlockDefinitionsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BlockDefinitionItemRequest> Items { get; set; } = new();
}

public sealed class BlockDefinitionItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Guid> SourceObjectIds { get; set; } = new();
    public double BasePointX { get; set; }
    public double BasePointY { get; set; }
    public double BasePointZ { get; set; }
    public bool HideSourceObjects { get; set; }
}

public sealed class InsertBlockInstancesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BlockInstanceItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
    public bool AutoCreateLayers { get; set; }
}

public sealed class BlockInstanceItemRequest
{
    public string DefinitionName { get; set; } = string.Empty;
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double OriginZ { get; set; }
    public double RotationDegrees { get; set; }
    public double Scale { get; set; } = 1d;
}


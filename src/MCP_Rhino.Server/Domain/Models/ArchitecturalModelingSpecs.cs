using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class ArchitecturalPointSpec
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
}

public sealed class ArchitecturalMetadataSpec
{
    public string Category { get; init; } = string.Empty;
    public string LevelName { get; init; } = string.Empty;
    public string SystemName { get; init; } = string.Empty;
    public string SourceTag { get; init; } = string.Empty;
}

public sealed class ArchitecturalObjectAttributesSpec
{
    public string LayerFullPath { get; init; } = string.Empty;
    public RhinoDisplayColor? Color { get; init; }
    public string Name { get; init; } = string.Empty;
    public Dictionary<string, string> UserText { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public ArchitecturalMetadataSpec Metadata { get; init; } = new();
    public bool AutoCreateLayer { get; init; }
}

public sealed class ArchitecturalPrimitiveSpec
{
    public ArchitecturalPrimitiveKind Kind { get; init; }
    public ArchitecturalRepresentationKind Representation { get; init; } = ArchitecturalRepresentationKind.Brep;
    public ProfileShapeKind ProfileShape { get; init; } = ProfileShapeKind.Rectangular;
    public WallAlignmentKind WallAlignment { get; init; } = WallAlignmentKind.Center;
    public OpeningKind OpeningKind { get; init; } = OpeningKind.Rectangular;
    public IReadOnlyList<ArchitecturalPointSpec> Points { get; init; } = Array.Empty<ArchitecturalPointSpec>();
    public IReadOnlyList<IReadOnlyList<ArchitecturalPointSpec>> Loops { get; init; } = Array.Empty<IReadOnlyList<ArchitecturalPointSpec>>();
    public double OriginX { get; init; }
    public double OriginY { get; init; }
    public double OriginZ { get; init; }
    public double SizeX { get; init; }
    public double SizeY { get; init; }
    public double SizeZ { get; init; }
    public double CenterX { get; init; }
    public double CenterY { get; init; }
    public double CenterZ { get; init; }
    public double StartX { get; init; }
    public double StartY { get; init; }
    public double StartZ { get; init; }
    public double EndX { get; init; }
    public double EndY { get; init; }
    public double EndZ { get; init; }
    public double Width { get; init; }
    public double Depth { get; init; }
    public double Height { get; init; }
    public double Thickness { get; init; }
    public double Radius { get; init; }
    public double BaseElevation { get; init; }
    public double Elevation { get; init; }
    public bool Cap { get; init; } = true;
    public string Name { get; init; } = string.Empty;
    public ArchitecturalMetadataSpec Metadata { get; init; } = new();
}

public sealed class ArchitecturalBooleanOperationSpec
{
    public BooleanOperationKind Operation { get; init; } = BooleanOperationKind.Difference;
    public IReadOnlyList<Guid> TargetObjectIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> CutterObjectIds { get; init; } = Array.Empty<Guid>();
    public bool DeleteTargets { get; init; } = true;
    public bool DeleteCutters { get; init; }
    public double Tolerance { get; init; }
}

public sealed class ArchitecturalOpeningOperationSpec
{
    public Guid TargetObjectId { get; init; }
    public OpeningKind OpeningKind { get; init; } = OpeningKind.Rectangular;
    public double CenterX { get; init; }
    public double CenterY { get; init; }
    public double CenterZ { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Depth { get; init; }
    public double Radius { get; init; }
    public bool RetainCutter { get; init; }
    public double Tolerance { get; init; }
}

public sealed class BlockDefinitionSpec
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<Guid> SourceObjectIds { get; init; } = Array.Empty<Guid>();
    public double BasePointX { get; init; }
    public double BasePointY { get; init; }
    public double BasePointZ { get; init; }
    public bool HideSourceObjects { get; init; }
}

public sealed class BlockInstanceSpec
{
    public string DefinitionName { get; init; } = string.Empty;
    public double OriginX { get; init; }
    public double OriginY { get; init; }
    public double OriginZ { get; init; }
    public double RotationDegrees { get; init; }
    public double Scale { get; init; } = 1d;
}


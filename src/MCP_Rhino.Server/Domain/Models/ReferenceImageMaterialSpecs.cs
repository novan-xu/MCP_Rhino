using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class RenderMaterialCreationSpec
{
    public string Name { get; set; } = string.Empty;
    public RhinoDisplayColor BaseColor { get; set; } = new();
    public double Roughness { get; set; } = 0.5d;
    public double Transparency { get; set; }
}

public sealed class ObjectMaterialAssignmentSpec
{
    public string MaterialName { get; set; } = string.Empty;
    public List<Guid> ObjectIds { get; set; } = new();
    public Dictionary<string, string> UserText { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProceduralTextureImageSpec
{
    public ProceduralTexturePatternKind PatternKind { get; set; } = ProceduralTexturePatternKind.WovenFabric;
    public string OutputPath { get; set; } = string.Empty;
    public int WidthPx { get; set; } = 512;
    public int HeightPx { get; set; } = 512;
    public RhinoDisplayColor BaseColor { get; set; } = new() { R = 142, G = 92, B = 73 };
    public RhinoDisplayColor WarpColor { get; set; } = new() { R = 170, G = 125, B = 95 };
    public RhinoDisplayColor WeftColor { get; set; } = new() { R = 110, G = 72, B = 58 };
    public int ThreadSpacingPx { get; set; } = 16;
    public int ThreadThicknessPx { get; set; } = 6;
    public double NoiseAmount { get; set; } = 0.08d;
    public bool Tileable { get; set; } = true;
    public bool Overwrite { get; set; }
    public int Seed { get; set; } = 260509;
}

public sealed class TexturedRenderMaterialCreationSpec
{
    public string Name { get; set; } = string.Empty;
    public RhinoDisplayColor BaseColor { get; set; } = new();
    public double Roughness { get; set; } = 0.5d;
    public double Transparency { get; set; }
    public string DiffuseTextureImagePath { get; set; } = string.Empty;
    public int MappingChannel { get; set; } = 1;
}

public sealed class TextureMappingSpec
{
    public Guid ObjectId { get; set; }
    public TextureMappingKind MappingKind { get; set; } = TextureMappingKind.Box;
    public int MappingChannel { get; set; } = 1;
    public double Width { get; set; }
    public double Depth { get; set; }
    public double Height { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double OffsetZ { get; set; }
    public double RotationDegrees { get; set; }
    public bool Capped { get; set; } = true;
}

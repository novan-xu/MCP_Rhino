using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class CreateRenderMaterialsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<RenderMaterialItemRequest> Items { get; set; } = new();
    public bool ReuseExistingByName { get; set; } = true;
}

public sealed class RenderMaterialItemRequest
{
    public string Name { get; set; } = string.Empty;
    public ObjectColorRequest BaseColor { get; set; } = new();
    public double Roughness { get; set; } = 0.5d;
    public double Transparency { get; set; }
}

public sealed class ApplyObjectMaterialsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<ObjectMaterialAssignmentRequest> Assignments { get; set; } = new();
}

public sealed class ObjectMaterialAssignmentRequest
{
    public string MaterialName { get; set; } = string.Empty;
    public List<Guid> ObjectIds { get; set; } = new();
    public Dictionary<string, string> UserText { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class GenerateProceduralTextureImageRequest
{
    public ProceduralTexturePatternKind PatternKind { get; set; } = ProceduralTexturePatternKind.WovenFabric;
    public string OutputPath { get; set; } = string.Empty;
    public int WidthPx { get; set; } = 512;
    public int HeightPx { get; set; } = 512;
    public ObjectColorRequest BaseColor { get; set; } = new() { R = 142, G = 92, B = 73 };
    public ObjectColorRequest WarpColor { get; set; } = new() { R = 170, G = 125, B = 95 };
    public ObjectColorRequest WeftColor { get; set; } = new() { R = 110, G = 72, B = 58 };
    public int ThreadSpacingPx { get; set; } = 16;
    public int ThreadThicknessPx { get; set; } = 6;
    public double NoiseAmount { get; set; } = 0.08d;
    public bool Tileable { get; set; } = true;
    public bool Overwrite { get; set; }
    public int Seed { get; set; } = 260509;
}

public sealed class CreateTexturedRenderMaterialsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<TexturedRenderMaterialItemRequest> Items { get; set; } = new();
    public bool ReuseExistingByName { get; set; } = true;
}

public sealed class TexturedRenderMaterialItemRequest
{
    public string Name { get; set; } = string.Empty;
    public ObjectColorRequest BaseColor { get; set; } = new();
    public double Roughness { get; set; } = 0.5d;
    public double Transparency { get; set; }
    public string DiffuseTextureImagePath { get; set; } = string.Empty;
    public int MappingChannel { get; set; } = 1;
}

public sealed class InspectRenderMaterialTexturesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> MaterialNames { get; set; } = new();
}

public sealed class ApplyTextureMappingRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<TextureMappingItemRequest> Items { get; set; } = new();
}

public sealed class PreviewTextureMappingRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<TextureMappingItemRequest> Items { get; set; } = new();
}

public sealed class TextureMappingItemRequest
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

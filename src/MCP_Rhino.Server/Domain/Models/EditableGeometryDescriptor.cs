using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class EditableGeometryDescriptor
{
    public Guid ObjectId { get; set; }
    public EditableGeometryKind Kind { get; set; }
    public DescriptorDetail Detail { get; set; }
    public EditableCurveStructure? CurveStructure { get; set; }
    public EditableSurfaceStructure? SurfaceStructure { get; set; }
    public GeometryPointData ControlPointCentroidWorld { get; set; } = new();
    public int ControlPointCount { get; set; }
    public IReadOnlyList<EditablePointDescriptor> Points { get; set; } = Array.Empty<EditablePointDescriptor>();
    public GeometryMetadataSummary MetadataSummary { get; set; } = new();
    public GeometryReconstructionSupportKind SupportKind { get; set; } = GeometryReconstructionSupportKind.Supported;
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

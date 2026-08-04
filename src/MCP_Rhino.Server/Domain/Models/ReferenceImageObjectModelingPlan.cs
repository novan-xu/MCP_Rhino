namespace MCP_Rhino.Server.Domain.Models;

public sealed class ReferenceImageObjectModelingPlan
{
    public ReferenceImageModelBrief Brief { get; set; } = new();
    public ReferenceImagePrimitiveDecomposition Decomposition { get; set; } = new();
    public ReferenceImageProductGeometryPlan ProductGeometryPlan { get; set; } = new();
    public ReferenceImageRefinementPlan DetailPlan { get; set; } = new();
    public ReferenceImageMaterialPlan MaterialPlan { get; set; } = new();
    public ReferenceImageModelingTrace Trace { get; set; } = new();
}

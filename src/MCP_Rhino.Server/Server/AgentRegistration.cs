using Microsoft.Extensions.DependencyInjection;
using MCP_Rhino.Server.Agents.Editing;
using MCP_Rhino.Server.Agents.Inspection;
using MCP_Rhino.Server.Agents.Modeling;
using MCP_Rhino.Server.Agents.Takeoff;
using MCP_Rhino.Server.Skills.Editing;
using MCP_Rhino.Server.Skills.Drawing;
using MCP_Rhino.Server.Skills.Inspection;
using MCP_Rhino.Server.Skills.Modeling;

namespace MCP_Rhino.Server.Server;

public static class AgentRegistration
{
    public static IServiceCollection AddRhinoAgents(this IServiceCollection services)
    {
        services.AddSingleton<LayerObjectFilterSkill>();
        services.AddSingleton<ObjectTypeFilterSkill>();
        services.AddSingleton<UserAttributeObjectFilterSkill>();
        services.AddSingleton<CompositeObjectFilterSkill>();
        services.AddSingleton<SelectionScopedAnalysisSkill>();
        services.AddSingleton<ObjectSelectionSkill>();
        services.AddSingleton<LiveObjectSelectionSkill>();
        services.AddSingleton<ObjectEditPreviewSkill>();
        services.AddSingleton<ObjectEditApplySkill>();
        services.AddSingleton<ObjectAttributeRecipeSkill>();
        services.AddSingleton<LotVisualizationExportSkill>();
        services.AddSingleton<GeometryCreationSkill>();
        services.AddSingleton<GeometryModificationSkill>();
        services.AddSingleton<ArchitecturalPrimitiveCreationSkill>();
        services.AddSingleton<ArchitecturalBooleanSkill>();
        services.AddSingleton<BlockLifecycleSkill>();
        services.AddSingleton<ReferenceImageModelBriefSkill>();
        services.AddSingleton<ReferenceImagePrimitiveDecompositionSkill>();
        services.AddSingleton<ReferenceImageProductGeometryPlanningSkill>();
        services.AddSingleton<ReferenceImageInitialMassingSkill>();
        services.AddSingleton<ReferenceImageDetailRefinementSkill>();
        services.AddSingleton<ReferenceImageMaterialPlanningSkill>();
        services.AddSingleton<ReferenceImageIterationDecisionSkill>();
        services.AddSingleton<SurfacePointOrderRebuildSkill>();
        services.AddSingleton<StandardFourPointSurfaceRebuildSkill>();
        services.AddSingleton<RhinoObjectFilterAgent>();
        services.AddSingleton<RhinoObjectEditingAgent>();
        services.AddSingleton<ReferenceImageObjectModelingAgent>();
        services.AddSingleton<TakeoffSpreadsheetAgent>();
        return services;
    }
}

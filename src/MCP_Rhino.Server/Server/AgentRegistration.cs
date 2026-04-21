using Microsoft.Extensions.DependencyInjection;
using MCP_Rhino.Server.Agents.Editing;
using MCP_Rhino.Server.Agents.Inspection;
using MCP_Rhino.Server.Skills.Editing;
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
        services.AddSingleton<ObjectSelectionSkill>();
        services.AddSingleton<LiveObjectSelectionSkill>();
        services.AddSingleton<ObjectEditPreviewSkill>();
        services.AddSingleton<ObjectEditApplySkill>();
        services.AddSingleton<GeometryCreationSkill>();
        services.AddSingleton<GeometryModificationSkill>();
        services.AddSingleton<RhinoObjectFilterAgent>();
        services.AddSingleton<RhinoObjectEditingAgent>();
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using MCP_Rhino.Server.Agents.File;
using MCP_Rhino.Server.Agents.Editing;
using MCP_Rhino.Server.Agents.Inspection;
using MCP_Rhino.Server.Skills.File;
using MCP_Rhino.Server.Skills.Editing;
using MCP_Rhino.Server.Skills.Inspection;

namespace MCP_Rhino.Server.Server;

public static class AgentRegistration
{
    public static IServiceCollection AddRhinoAgents(this IServiceCollection services)
    {
        services.AddSingleton<LayerObjectFilterSkill>();
        services.AddSingleton<ObjectTypeFilterSkill>();
        services.AddSingleton<UserAttributeObjectFilterSkill>();
        services.AddSingleton<CompositeObjectFilterSkill>();
        services.AddSingleton<FileOpenStateCheckSkill>();
        services.AddSingleton<ArchiveSnapshotSkill>();
        services.AddSingleton<ArchiveRetentionSkill>();
        services.AddSingleton<FileMutationPreflightSkill>();
        services.AddSingleton<ObjectSelectionSkill>();
        services.AddSingleton<ObjectEditPreviewSkill>();
        services.AddSingleton<ObjectEditApplySkill>();
        services.AddSingleton<FileArchiveAgent>();
        services.AddSingleton<RhinoObjectFilterAgent>();
        services.AddSingleton<RhinoObjectEditingAgent>();

        return services;
    }
}
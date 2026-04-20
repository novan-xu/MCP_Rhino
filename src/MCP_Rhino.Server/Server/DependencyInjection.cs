using Microsoft.Extensions.DependencyInjection;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Application.Services.Filters;
using MCP_Rhino.Server.Infrastructure.CLI;
using MCP_Rhino.Server.Infrastructure.Rhino;

namespace MCP_Rhino.Server.Server;

public static class DependencyInjection
{
    public static IServiceCollection AddRhinoCore(this IServiceCollection services)
    {
        services.AddSingleton<IRhinoDocumentRepository, RhinoDocumentRepository>();
        services.AddSingleton<IObjectFilterCriterionEvaluator, LayerFilterCriterionEvaluator>();
        services.AddSingleton<IObjectFilterCriterionEvaluator, ObjectTypeFilterCriterionEvaluator>();
        services.AddSingleton<IObjectFilterCriterionEvaluator, UserAttributeFilterCriterionEvaluator>();
        services.AddSingleton<IFileOpenStateInspector, RhinoFileOpenStateInspector>();
        services.AddSingleton<IArchiveSnapshotService, ArchiveSnapshotService>();
        services.AddSingleton<IArchiveRetentionService, ArchiveRetentionService>();
        services.AddSingleton<IObjectEditValidator, RhinoObjectEditValidator>();
        services.AddSingleton<IObjectEditOperationApplier, RhinoObjectEditOperationApplier>();
        services.AddSingleton<IGeometryBuilder, RhinoGeometryBuilder>();
        services.AddSingleton<IGeometryMutator, RhinoGeometryMutator>();
        services.AddSingleton<IGeometryValidator, RhinoGeometryValidator>();
        services.AddSingleton<IFileMutationSafeguard, RhinoFileMutationSafeguard>();
        services.AddSingleton<IEditResultFormatter, PassThroughEditResultFormatter>();
        services.AddSingleton<RhinoObjectFilterService>();
        services.AddSingleton<RhinoObjectEditingService>();
        services.AddSingleton<RhinoGeometryCreationService>();
        services.AddSingleton<RhinoGeometryModificationService>();
        services.AddSingleton<RhinoObjectUserTextService>();
        services.AddSingleton<RhinoDocumentUserStringService>();
        services.AddSingleton<DeveloperCommandHandler>();

        return services;
    }
}

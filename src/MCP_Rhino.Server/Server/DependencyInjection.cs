using Microsoft.Extensions.DependencyInjection;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Application.Services.Filters;
using MCP_Rhino.Server.Infrastructure.CLI;
using MCP_Rhino.Server.Infrastructure.Rhino;
using MCP_Rhino.Server.Infrastructure.Rhino.Live;

namespace MCP_Rhino.Server.Server;

public static class DependencyInjection
{
    public static IServiceCollection AddOfflineRhinoAdapters(this IServiceCollection services)
    {
        services.AddSingleton<IRhinoDocumentRepository, RhinoDocumentRepository>();
        services.AddSingleton<IObjectFilterCriterionEvaluator, LayerFilterCriterionEvaluator>();
        services.AddSingleton<IObjectFilterCriterionEvaluator, ObjectTypeFilterCriterionEvaluator>();
        services.AddSingleton<IObjectFilterCriterionEvaluator, UserAttributeFilterCriterionEvaluator>();
        services.AddSingleton<IObjectEditSpecValidator, RhinoObjectEditValidator>();
        services.AddSingleton<IGeometryValidator, RhinoGeometryValidator>();
        services.AddSingleton<IGeometryBuilder, RhinoGeometryBuilder>();
        services.AddSingleton<IEditResultFormatter, PassThroughEditResultFormatter>();
        return services;
    }

    public static IServiceCollection AddLiveRhinoAdapters(this IServiceCollection services)
    {
        services.AddSingleton<ILiveRhinoDocumentAccessor, LiveRhinoDocumentAccessor>();
        services.AddSingleton<ILiveGeometryBuilder, LiveRhinoGeometryBuilder>();
        services.AddSingleton<ILiveObjectEditValidator, LiveRhinoObjectEditValidator>();
        services.AddSingleton<ILiveGeometryValidator, LiveRhinoGeometryValidator>();
        services.AddSingleton<IObjectEditOperationApplier, LiveRhinoObjectEditOperationApplier>();
        services.AddSingleton<IGeometryMutator, LiveRhinoGeometryMutator>();
        services.AddSingleton<ILiveGeometryMetricsCalculator, LiveRhinoGeometryMetricsCalculator>();
        services.AddSingleton<ILiveGeometryCurvatureCalculator, LiveRhinoGeometryCurvatureCalculator>();
        services.AddSingleton<ILiveGeometryIntersectionCalculator, LiveRhinoGeometryIntersectionCalculator>();
        services.AddSingleton<ILiveFileExporter, LiveRhinoFileExporter>();
        services.AddSingleton<ILiveExternalReferenceManager, LiveRhinoExternalReferenceManager>();
        return services;
    }

    public static IServiceCollection AddCliFallbackLiveRhinoAdapters(this IServiceCollection services)
    {
        services.AddSingleton<ILiveRhinoDocumentAccessor, NullLiveRhinoDocumentAccessor>();
        services.AddSingleton<ILiveGeometryBuilder, LiveRhinoGeometryBuilder>();
        services.AddSingleton<ILiveObjectEditValidator, LiveRhinoObjectEditValidator>();
        services.AddSingleton<ILiveGeometryValidator, LiveRhinoGeometryValidator>();
        services.AddSingleton<IObjectEditOperationApplier, LiveRhinoObjectEditOperationApplier>();
        services.AddSingleton<IGeometryMutator, LiveRhinoGeometryMutator>();
        services.AddSingleton<ILiveGeometryMetricsCalculator, LiveRhinoGeometryMetricsCalculator>();
        services.AddSingleton<ILiveGeometryCurvatureCalculator, LiveRhinoGeometryCurvatureCalculator>();
        services.AddSingleton<ILiveGeometryIntersectionCalculator, LiveRhinoGeometryIntersectionCalculator>();
        services.AddSingleton<ILiveFileExporter, LiveRhinoFileExporter>();
        services.AddSingleton<ILiveExternalReferenceManager, LiveRhinoExternalReferenceManager>();
        return services;
    }

    public static IServiceCollection AddRhinoApplication(this IServiceCollection services)
    {
        services.AddSingleton<RhinoObjectFilterService>();
        services.AddSingleton<RhinoObjectEditingService>();
        services.AddSingleton<RhinoGeometryCreationService>();
        services.AddSingleton<RhinoGeometryModificationService>();
        services.AddSingleton<RhinoObjectUserTextService>();
        services.AddSingleton<RhinoDocumentUserStringService>();
        services.AddSingleton<RhinoLayerManagementService>();
        services.AddSingleton<RhinoGeometryMetricsService>();
        services.AddSingleton<RhinoGeometryCurvatureService>();
        services.AddSingleton<RhinoGeometryIntersectionService>();
        services.AddSingleton<RhinoFileExportService>();
        services.AddSingleton<RhinoExternalReferenceService>();
        services.AddSingleton<DeveloperCommandHandler>();
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Application.Services.Edit;
using MCP_Rhino.Server.Application.Services.Rebuild;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Application.Services.Filters;
using MCP_Rhino.Server.Infrastructure.CLI;
using MCP_Rhino.Server.Infrastructure.Rhino;
using MCP_Rhino.Server.Infrastructure.Rhino.Live;

namespace MCP_Rhino.Server.Server;

public static class DependencyInjection
{
    public static IServiceCollection AddLiveRhinoAdapters(this IServiceCollection services)
    {
        services.AddSingleton<ILiveRhinoDocumentAccessor, LiveRhinoDocumentAccessor>();
        services.AddSingleton<ILiveRhinoDocumentAccessorFactory, LiveRhinoDocumentAccessorFactory>();
        services.AddSingleton<ILiveGeometryBuilder, LiveRhinoGeometryBuilder>();
        services.AddSingleton<ILiveGeneralPrimitiveBuilder, LiveGeneralPrimitiveBuilder>();
        services.AddSingleton<ILiveCurveDerivedGeometryOperator, LiveCurveDerivedGeometryOperator>();
        services.AddSingleton<ILiveObjectEditValidator, LiveRhinoObjectEditValidator>();
        services.AddSingleton<ILiveGeometryValidator, LiveRhinoGeometryValidator>();
        services.AddSingleton<IObjectEditOperationApplier, LiveRhinoObjectEditOperationApplier>();
        services.AddSingleton<IGeometryMutator, LiveRhinoGeometryMutator>();
        services.AddSingleton<ILiveGeometryMetricsCalculator, LiveRhinoGeometryMetricsCalculator>();
        services.AddSingleton<ILiveGeometryCurvatureCalculator, LiveRhinoGeometryCurvatureCalculator>();
        services.AddSingleton<ILiveGeometryIntersectionCalculator, LiveRhinoGeometryIntersectionCalculator>();
        services.AddSingleton<ILiveFileExporter, LiveRhinoFileExporter>();
        services.AddSingleton<ILiveExternalReferenceManager, LiveRhinoExternalReferenceManager>();
        services.AddSingleton<IEditableGeometryDescriptorService, LiveEditableGeometryDescriptorService>();
        services.AddSingleton<IGeometryFrameSampler, LiveGeometryFrameSampler>();
        services.AddSingleton<IGeometryMetadataOperator, LiveGeometryMetadataOperator>();
        services.AddSingleton<IBrepSurfaceDowngrader, LiveBrepSurfaceDowngrader>();
        services.AddSingleton<IGeometryEditValidator, LiveGeometryEditValidator>();
        services.AddSingleton<IGeometryReconstructor, LiveGeometryReconstructor>();
        services.AddSingleton<IGeometryMutationService, LiveGeometryMutationService>();
        services.AddSingleton<IDerivedPointOperationEvaluator, LiveDerivedPointOperationEvaluator>();
        services.AddSingleton<IGeometryEditStrategyResolver, LiveGeometryEditStrategyResolver>();
        services.AddSingleton<IGeometryTransformExecutionBridge, LiveGeometryTransformExecutionBridge>();
        services.AddSingleton<IBoundaryReferenceCurveAnalyzer, LiveBoundaryReferenceCurveAnalyzer>();
        services.AddSingleton<ISurfaceLocalCoordinateSystemBuilder, LiveSurfaceLocalCoordinateSystemBuilder>();
        services.AddSingleton<IBoundaryDrivenSurfaceReconstructor, LiveBoundaryDrivenSurfaceReconstructor>();
        services.AddSingleton<ILiveSurfaceDirectionTweakService, LiveSurfaceDirectionTweakService>();
        services.AddSingleton<ILiveSurfaceFrontBackFlipService, LiveSurfaceFrontBackFlipService>();
        services.AddSingleton<ILiveDrawingViewManager, LiveDrawingViewManager>();
        services.AddSingleton<ILiveDrawingExportStateOperator, LiveDrawingExportStateOperator>();
        services.AddSingleton<ILiveArchitecturalGeometryBuilder, LiveArchitecturalGeometryBuilder>();
        services.AddSingleton<ILiveBooleanOperator, LiveBooleanOperator>();
        services.AddSingleton<ILiveBlockDefinitionOperator, LiveBlockDefinitionOperator>();
        services.AddSingleton<ILiveBlockInspector, LiveBlockInspector>();
        services.AddSingleton<ILiveBlockLifecycleOperator, LiveBlockLifecycleOperator>();
        services.AddSingleton<ILiveRhinoDocumentStateOperator, LiveRhinoDocumentStateOperator>();
        services.AddSingleton<ILiveRhinoSelectionOperator, LiveRhinoSelectionOperator>();
        services.AddSingleton<ILiveRhinoViewportCapture, LiveRhinoViewportCapture>();
        services.AddSingleton<ILiveRhinoMaterialOperator, LiveRhinoMaterialOperator>();
        services.AddSingleton<ILiveSubDModelingOperator, LiveSubDModelingOperator>();
        return services;
    }

    public static IServiceCollection AddCliFallbackLiveRhinoAdapters(this IServiceCollection services)
    {
        services.AddSingleton<ILiveRhinoDocumentAccessor, NullLiveRhinoDocumentAccessor>();
        services.AddSingleton<ILiveRhinoDocumentAccessorFactory, LiveRhinoDocumentAccessorFactory>();
        services.AddSingleton<ILiveGeometryBuilder, LiveRhinoGeometryBuilder>();
        services.AddSingleton<ILiveGeneralPrimitiveBuilder, LiveGeneralPrimitiveBuilder>();
        services.AddSingleton<ILiveCurveDerivedGeometryOperator, LiveCurveDerivedGeometryOperator>();
        services.AddSingleton<ILiveObjectEditValidator, LiveRhinoObjectEditValidator>();
        services.AddSingleton<ILiveGeometryValidator, LiveRhinoGeometryValidator>();
        services.AddSingleton<IObjectEditOperationApplier, LiveRhinoObjectEditOperationApplier>();
        services.AddSingleton<IGeometryMutator, LiveRhinoGeometryMutator>();
        services.AddSingleton<ILiveGeometryMetricsCalculator, LiveRhinoGeometryMetricsCalculator>();
        services.AddSingleton<ILiveGeometryCurvatureCalculator, LiveRhinoGeometryCurvatureCalculator>();
        services.AddSingleton<ILiveGeometryIntersectionCalculator, LiveRhinoGeometryIntersectionCalculator>();
        services.AddSingleton<ILiveFileExporter, LiveRhinoFileExporter>();
        services.AddSingleton<ILiveExternalReferenceManager, LiveRhinoExternalReferenceManager>();
        services.AddSingleton<IEditableGeometryDescriptorService, LiveEditableGeometryDescriptorService>();
        services.AddSingleton<IGeometryFrameSampler, LiveGeometryFrameSampler>();
        services.AddSingleton<IGeometryMetadataOperator, LiveGeometryMetadataOperator>();
        services.AddSingleton<IBrepSurfaceDowngrader, LiveBrepSurfaceDowngrader>();
        services.AddSingleton<IGeometryEditValidator, LiveGeometryEditValidator>();
        services.AddSingleton<IGeometryReconstructor, LiveGeometryReconstructor>();
        services.AddSingleton<IGeometryMutationService, LiveGeometryMutationService>();
        services.AddSingleton<IDerivedPointOperationEvaluator, LiveDerivedPointOperationEvaluator>();
        services.AddSingleton<IGeometryEditStrategyResolver, LiveGeometryEditStrategyResolver>();
        services.AddSingleton<IGeometryTransformExecutionBridge, LiveGeometryTransformExecutionBridge>();
        services.AddSingleton<IBoundaryReferenceCurveAnalyzer, LiveBoundaryReferenceCurveAnalyzer>();
        services.AddSingleton<ISurfaceLocalCoordinateSystemBuilder, LiveSurfaceLocalCoordinateSystemBuilder>();
        services.AddSingleton<IBoundaryDrivenSurfaceReconstructor, LiveBoundaryDrivenSurfaceReconstructor>();
        services.AddSingleton<ILiveSurfaceDirectionTweakService, LiveSurfaceDirectionTweakService>();
        services.AddSingleton<ILiveSurfaceFrontBackFlipService, LiveSurfaceFrontBackFlipService>();
        services.AddSingleton<ILiveDrawingViewManager, LiveDrawingViewManager>();
        services.AddSingleton<ILiveDrawingExportStateOperator, LiveDrawingExportStateOperator>();
        services.AddSingleton<ILiveArchitecturalGeometryBuilder, LiveArchitecturalGeometryBuilder>();
        services.AddSingleton<ILiveBooleanOperator, LiveBooleanOperator>();
        services.AddSingleton<ILiveBlockDefinitionOperator, LiveBlockDefinitionOperator>();
        services.AddSingleton<ILiveBlockInspector, LiveBlockInspector>();
        services.AddSingleton<ILiveBlockLifecycleOperator, LiveBlockLifecycleOperator>();
        services.AddSingleton<ILiveRhinoDocumentStateOperator, LiveRhinoDocumentStateOperator>();
        services.AddSingleton<ILiveRhinoSelectionOperator, LiveRhinoSelectionOperator>();
        services.AddSingleton<ILiveRhinoViewportCapture, LiveRhinoViewportCapture>();
        services.AddSingleton<ILiveRhinoMaterialOperator, LiveRhinoMaterialOperator>();
        services.AddSingleton<ILiveSubDModelingOperator, LiveSubDModelingOperator>();
        return services;
    }

    public static IServiceCollection AddRhinoApplication(this IServiceCollection services)
    {
        services.AddSingleton<IObjectFilterCriterionEvaluator, LayerFilterCriterionEvaluator>();
        services.AddSingleton<IObjectFilterCriterionEvaluator, ObjectTypeFilterCriterionEvaluator>();
        services.AddSingleton<IObjectFilterCriterionEvaluator, UserAttributeFilterCriterionEvaluator>();
        services.AddSingleton<IEditResultFormatter, PassThroughEditResultFormatter>();
        services.AddSingleton<RhinoObjectFilterService>();
        services.AddSingleton<RhinoObjectEditingService>();
        services.AddSingleton<RhinoObjectAttributeRecipeService>();
        services.AddSingleton<RhinoGeometryCreationService>();
        services.AddSingleton<RhinoGeneralPrimitiveCreationService>();
        services.AddSingleton<RhinoCurveDerivedGeometryService>();
        services.AddSingleton<RhinoGeometryModificationService>();
        services.AddSingleton<RhinoObjectUserTextService>();
        services.AddSingleton<RhinoDocumentUserStringService>();
        services.AddSingleton<RhinoLayerManagementService>();
        services.AddSingleton<RhinoGeometryMetricsService>();
        services.AddSingleton<RhinoGeometryCurvatureService>();
        services.AddSingleton<RhinoGeometryIntersectionService>();
        services.AddSingleton<RhinoFileExportService>();
        services.AddSingleton<IDrawingExportSnapshotStore, DrawingExportSnapshotStore>();
        services.AddSingleton<RhinoDrawingExportService>();
        services.AddSingleton<RhinoExternalReferenceService>();
        services.AddSingleton<RhinoArchitecturalPrimitiveService>();
        services.AddSingleton<RhinoArchitecturalBooleanService>();
        services.AddSingleton<RhinoBlockDefinitionService>();
        services.AddSingleton<RhinoBlockInspectionService>();
        services.AddSingleton<RhinoBlockLifecycleService>();
        services.AddSingleton<RhinoDocumentStateService>();
        services.AddSingleton<RhinoSelectionService>();
        services.AddSingleton<RhinoViewportCaptureService>();
        services.AddSingleton<ReferenceImageVisualQaCaptureService>();
        services.AddSingleton<RhinoMaterialService>();
        services.AddSingleton<RhinoSubDModelingService>();
        services.AddSingleton<ProceduralTextureImageService>();
        services.AddSingleton<ReferenceImageModelingBriefService>();
        services.AddSingleton<ReferenceImagePrimitiveDecompositionService>();
        services.AddSingleton<ReferenceImageProductGeometryPlanningService>();
        services.AddSingleton<ReferenceImageDetailRefinementPlanningService>();
        services.AddSingleton<ReferenceImageMaterialPlanningService>();
        services.AddSingleton<ReferenceImageIterationDecisionService>();
        services.AddSingleton<ICurveEditOrchestrator, CurveEditOrchestrator>();
        services.AddSingleton<ISurfaceEditOrchestrator, SurfaceEditOrchestrator>();
        services.AddSingleton<ISurfaceBoundaryPointOrderer, SurfaceBoundaryPointOrderer>();
        services.AddSingleton<ISurfaceRebuildOrchestrator, SurfaceRebuildOrchestrator>();
        services.AddSingleton<ISurfaceDirectionTweakOrchestrator, SurfaceDirectionTweakOrchestrator>();
        services.AddSingleton<ISurfaceFrontBackFlipOrchestrator, SurfaceFrontBackFlipOrchestrator>();
        services.AddSingleton<DeveloperCommandHandler>();
        return services;
    }
}

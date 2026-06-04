using MCP_Rhino.Server.Agents.Editing;
using MCP_Rhino.Server.Agents.Inspection;
using MCP_Rhino.Server.Agents.Modeling;
using MCP_Rhino.Server.Agents.Takeoff;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Inspection;
using MCP_Rhino.Server.Skills.Modeling;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private readonly RhinoObjectFilterService _filterService;
    private readonly RhinoObjectEditingService _editingService;
    private readonly RhinoObjectUserTextService _userTextService;
    private readonly RhinoDocumentUserStringService _documentUserStringService;
    private readonly RhinoLayerManagementService _layerManagementService;
    private readonly RhinoDocumentStateService _documentStateService;
    private readonly RhinoSelectionService _selectionService;
    private readonly RhinoViewportCaptureService _viewportCaptureService;
    private readonly ReferenceImageVisualQaCaptureService _referenceImageVisualQaCaptureService;
    private readonly RhinoMaterialService _rhinoMaterialService;
    private readonly LayerObjectFilterSkill _layerSkill;
    private readonly ObjectTypeFilterSkill _typeSkill;
    private readonly UserAttributeObjectFilterSkill _userAttributeSkill;
    private readonly RhinoObjectFilterAgent _filterAgent;
    private readonly RhinoObjectEditingAgent _editingAgent;
    private readonly ReferenceImageObjectModelingAgent _referenceImageObjectModelingAgent;
    private readonly GeometryCreationSkill _geometryCreationSkill;
    private readonly RhinoCurveDerivedGeometryService _curveDerivedGeometryService;
    private readonly GeometryModificationSkill _geometryModificationSkill;
    private readonly StandardFourPointSurfaceRebuildSkill _standardFourPointSurfaceRebuildSkill;
    private readonly ILiveRhinoDocumentAccessor _liveRhinoDocumentAccessor;
    private readonly IEditableGeometryDescriptorService _editableGeometryDescriptorService;
    private readonly IGeometryMetadataOperator _geometryMetadataOperator;
    private readonly ICurveEditOrchestrator _curveEditOrchestrator;
    private readonly ISurfaceEditOrchestrator _surfaceEditOrchestrator;
    private readonly ISurfaceRebuildOrchestrator _surfaceRebuildOrchestrator;
    private readonly ISurfaceDirectionTweakOrchestrator _surfaceDirectionTweakOrchestrator;
    private readonly ISurfaceFrontBackFlipOrchestrator _surfaceFrontBackFlipOrchestrator;
    private readonly RhinoGeometryMetricsService _geometryMetricsService;
    private readonly RhinoGeometryCurvatureService _geometryCurvatureService;
    private readonly RhinoGeometryIntersectionService _geometryIntersectionService;
    private readonly RhinoFileExportService _fileExportService;
    private readonly RhinoDrawingExportService _drawingExportService;
    private readonly RhinoExternalReferenceService _externalReferenceService;
    private readonly ArchitecturalPrimitiveCreationSkill _architecturalPrimitiveCreationSkill;
    private readonly ArchitecturalBooleanSkill _architecturalBooleanSkill;
    private readonly ReferenceImageModelBriefSkill _referenceImageModelBriefSkill;
    private readonly ReferenceImagePrimitiveDecompositionSkill _referenceImagePrimitiveDecompositionSkill;
    private readonly ReferenceImageInitialMassingSkill _referenceImageInitialMassingSkill;
    private readonly ReferenceImageDetailRefinementSkill _referenceImageDetailRefinementSkill;
    private readonly ReferenceImageMaterialPlanningSkill _referenceImageMaterialPlanningSkill;
    private readonly ReferenceImageIterationDecisionSkill _referenceImageIterationDecisionSkill;
    private readonly RhinoBlockDefinitionService _rhinoBlockDefinitionService;
    private readonly RhinoBlockInspectionService _rhinoBlockInspectionService;
    private readonly BlockLifecycleSkill _blockLifecycleSkill;
    private readonly RhinoTakeoffScheduleService _takeoffScheduleService;
    private readonly TakeoffSpreadsheetAgent _takeoffSpreadsheetAgent;
    private readonly Dictionary<string, Func<string[], bool>> _extensionHandlers = new(StringComparer.OrdinalIgnoreCase);

    // Optional hooks for Project_Test partials to register capability-specific smoke
    // commands without modifying the main switch below.
    partial void RegisterExtensionHandlers();
    partial void RegisterGeometryAnalysisHandlers();
    partial void RegisterFileImportExportHandlers();
    partial void RegisterGeometryEditMolecularFoundationHandlers();
    partial void RegisterGeometryEditCurveCompositeHandlers();
    partial void RegisterGeometryEditDerivedRoutingHandlers();
    partial void RegisterGeometryEditSurfaceCompositeHandlers();
    partial void RegisterSurfacePointOrderRebuildHandlers();
    partial void RegisterRhinoClaudeCodePanelHandlers();
    partial void RegisterRhinoClaudeCodeCompanionUiHandlers();
    partial void RegisterGravityAwareSurfacePointOrderHandlers();
    partial void RegisterSurfaceDirectionTweakHandlers();
    partial void RegisterStandardFourPointSurfaceRebuildHandlers();
    partial void RegisterSurfaceFrontBackFlipHandlers();
    partial void RegisterDrawingExportHandlers();
    partial void RegisterRhinoChatSaveSafetyHandlers();
    partial void RegisterMultiRhinoPanelPipeHandlers();
    partial void RegisterLlmPanelCliSwitchingHandlers();
    partial void RegisterMcpToolSafetyAnnotationHandlers();
    partial void RegisterRhinoChatFileLockSafetyHandlers();
    partial void RegisterPanelRuntimePolicyHandlers();
    partial void RegisterDebugBridgeOnlyPluginHandlers();
    partial void RegisterArchitecturalModelingPrimitivesHandlers();
    partial void RegisterBlockCapabilitiesHandlers();
    partial void RegisterMcpSurfaceStructureGovernanceHandlers();
    partial void RegisterDocumentVisualStateToolsHandlers();
    partial void RegisterGeneralPrimitiveCreationToolsHandlers();
    partial void RegisterCurveDerivedGeometryToolsHandlers();
    partial void RegisterRhinoReferenceResourcesHandlers();
    partial void RegisterMcpToolOverlapCleanupHandlers();
    partial void RegisterReviewFindingsFixHandlers();
    partial void RegisterRuntimeTextNormalizationHandlers();
    partial void RegisterBulkAttributeRecipesHandlers();
    partial void RegisterSelectionScopedAnalysisRecipeHandlers();
    partial void RegisterReferenceImageVisualQaHandlers();
    partial void RegisterReferenceImageObjectModelingToolsHandlers();
    partial void RegisterReferenceImageObjectModelingSkillsHandlers();
    partial void RegisterReferenceImageObjectModelingAgentHandlers();
    partial void RegisterFileExportReliabilityHandlers();
    partial void RegisterModelingSurfaceCleanupHandlers();
    partial void RegisterMaterialTextureCapabilityHandlers();
    partial void RegisterReferenceImageAgentAccessAndBriefingHandlers();
    partial void RegisterReferenceImageAccurateProductModelingHandlers();
    partial void RegisterSubDModelingToolsHandlers();
    partial void RegisterCompanionImageAttachmentsHandlers();
    partial void RegisterTakeoffSpreadsheetHandlers();

    partial void RegisterExtensionHandlers()
    {
        RegisterGeometryAnalysisHandlers();
        RegisterFileImportExportHandlers();
        RegisterGeometryEditMolecularFoundationHandlers();
        RegisterGeometryEditCurveCompositeHandlers();
        RegisterGeometryEditDerivedRoutingHandlers();
        RegisterGeometryEditSurfaceCompositeHandlers();
        RegisterSurfacePointOrderRebuildHandlers();
        RegisterRhinoClaudeCodePanelHandlers();
        RegisterRhinoClaudeCodeCompanionUiHandlers();
        RegisterGravityAwareSurfacePointOrderHandlers();
        RegisterSurfaceDirectionTweakHandlers();
        RegisterStandardFourPointSurfaceRebuildHandlers();
        RegisterSurfaceFrontBackFlipHandlers();
        RegisterDrawingExportHandlers();
        RegisterRhinoChatSaveSafetyHandlers();
        RegisterMultiRhinoPanelPipeHandlers();
        RegisterLlmPanelCliSwitchingHandlers();
        RegisterMcpToolSafetyAnnotationHandlers();
        RegisterRhinoChatFileLockSafetyHandlers();
        RegisterPanelRuntimePolicyHandlers();
        RegisterDebugBridgeOnlyPluginHandlers();
        RegisterArchitecturalModelingPrimitivesHandlers();
        RegisterBlockCapabilitiesHandlers();
        RegisterMcpSurfaceStructureGovernanceHandlers();
        RegisterDocumentVisualStateToolsHandlers();
        RegisterGeneralPrimitiveCreationToolsHandlers();
        RegisterCurveDerivedGeometryToolsHandlers();
        RegisterRhinoReferenceResourcesHandlers();
        RegisterMcpToolOverlapCleanupHandlers();
        RegisterReviewFindingsFixHandlers();
        RegisterRuntimeTextNormalizationHandlers();
        RegisterBulkAttributeRecipesHandlers();
        RegisterSelectionScopedAnalysisRecipeHandlers();
        RegisterReferenceImageVisualQaHandlers();
        RegisterReferenceImageObjectModelingToolsHandlers();
        RegisterReferenceImageObjectModelingSkillsHandlers();
        RegisterReferenceImageObjectModelingAgentHandlers();
        RegisterFileExportReliabilityHandlers();
        RegisterModelingSurfaceCleanupHandlers();
        RegisterMaterialTextureCapabilityHandlers();
        RegisterReferenceImageAgentAccessAndBriefingHandlers();
        RegisterReferenceImageAccurateProductModelingHandlers();
        RegisterSubDModelingToolsHandlers();
        RegisterCompanionImageAttachmentsHandlers();
        RegisterTakeoffSpreadsheetHandlers();
    }

    public DeveloperCommandHandler(
        RhinoObjectFilterService filterService,
        RhinoObjectEditingService editingService,
        RhinoObjectUserTextService userTextService,
        RhinoDocumentUserStringService documentUserStringService,
        RhinoLayerManagementService layerManagementService,
        RhinoDocumentStateService documentStateService,
        RhinoSelectionService selectionService,
        RhinoViewportCaptureService viewportCaptureService,
        ReferenceImageVisualQaCaptureService referenceImageVisualQaCaptureService,
        RhinoMaterialService rhinoMaterialService,
        LayerObjectFilterSkill layerSkill,
        ObjectTypeFilterSkill typeSkill,
        UserAttributeObjectFilterSkill userAttributeSkill,
        RhinoObjectFilterAgent filterAgent,
        RhinoObjectEditingAgent editingAgent,
        ReferenceImageObjectModelingAgent referenceImageObjectModelingAgent,
        GeometryCreationSkill geometryCreationSkill,
        RhinoCurveDerivedGeometryService curveDerivedGeometryService,
        GeometryModificationSkill geometryModificationSkill,
        StandardFourPointSurfaceRebuildSkill standardFourPointSurfaceRebuildSkill,
        ILiveRhinoDocumentAccessor liveRhinoDocumentAccessor,
        IEditableGeometryDescriptorService editableGeometryDescriptorService,
        IGeometryMetadataOperator geometryMetadataOperator,
        ICurveEditOrchestrator curveEditOrchestrator,
        ISurfaceEditOrchestrator surfaceEditOrchestrator,
        ISurfaceRebuildOrchestrator surfaceRebuildOrchestrator,
        ISurfaceDirectionTweakOrchestrator surfaceDirectionTweakOrchestrator,
        ISurfaceFrontBackFlipOrchestrator surfaceFrontBackFlipOrchestrator,
        RhinoGeometryMetricsService geometryMetricsService,
        RhinoGeometryCurvatureService geometryCurvatureService,
        RhinoGeometryIntersectionService geometryIntersectionService,
        RhinoFileExportService fileExportService,
        RhinoDrawingExportService drawingExportService,
        RhinoExternalReferenceService externalReferenceService,
        ArchitecturalPrimitiveCreationSkill architecturalPrimitiveCreationSkill,
        ArchitecturalBooleanSkill architecturalBooleanSkill,
        ReferenceImageModelBriefSkill referenceImageModelBriefSkill,
        ReferenceImagePrimitiveDecompositionSkill referenceImagePrimitiveDecompositionSkill,
        ReferenceImageInitialMassingSkill referenceImageInitialMassingSkill,
        ReferenceImageDetailRefinementSkill referenceImageDetailRefinementSkill,
        ReferenceImageMaterialPlanningSkill referenceImageMaterialPlanningSkill,
        ReferenceImageIterationDecisionSkill referenceImageIterationDecisionSkill,
        RhinoBlockDefinitionService rhinoBlockDefinitionService,
        RhinoBlockInspectionService rhinoBlockInspectionService,
        BlockLifecycleSkill blockLifecycleSkill,
        RhinoTakeoffScheduleService takeoffScheduleService,
        TakeoffSpreadsheetAgent takeoffSpreadsheetAgent)
    {
        _filterService = filterService;
        _editingService = editingService;
        _userTextService = userTextService;
        _documentUserStringService = documentUserStringService;
        _layerManagementService = layerManagementService;
        _documentStateService = documentStateService;
        _selectionService = selectionService;
        _viewportCaptureService = viewportCaptureService;
        _referenceImageVisualQaCaptureService = referenceImageVisualQaCaptureService;
        _rhinoMaterialService = rhinoMaterialService;
        _layerSkill = layerSkill;
        _typeSkill = typeSkill;
        _userAttributeSkill = userAttributeSkill;
        _filterAgent = filterAgent;
        _editingAgent = editingAgent;
        _referenceImageObjectModelingAgent = referenceImageObjectModelingAgent;
        _geometryCreationSkill = geometryCreationSkill;
        _curveDerivedGeometryService = curveDerivedGeometryService;
        _geometryModificationSkill = geometryModificationSkill;
        _standardFourPointSurfaceRebuildSkill = standardFourPointSurfaceRebuildSkill;
        _liveRhinoDocumentAccessor = liveRhinoDocumentAccessor;
        _editableGeometryDescriptorService = editableGeometryDescriptorService;
        _geometryMetadataOperator = geometryMetadataOperator;
        _curveEditOrchestrator = curveEditOrchestrator;
        _surfaceEditOrchestrator = surfaceEditOrchestrator;
        _surfaceRebuildOrchestrator = surfaceRebuildOrchestrator;
        _surfaceDirectionTweakOrchestrator = surfaceDirectionTweakOrchestrator;
        _surfaceFrontBackFlipOrchestrator = surfaceFrontBackFlipOrchestrator;
        _geometryMetricsService = geometryMetricsService;
        _geometryCurvatureService = geometryCurvatureService;
        _geometryIntersectionService = geometryIntersectionService;
        _fileExportService = fileExportService;
        _drawingExportService = drawingExportService;
        _externalReferenceService = externalReferenceService;
        _architecturalPrimitiveCreationSkill = architecturalPrimitiveCreationSkill;
        _architecturalBooleanSkill = architecturalBooleanSkill;
        _referenceImageModelBriefSkill = referenceImageModelBriefSkill;
        _referenceImagePrimitiveDecompositionSkill = referenceImagePrimitiveDecompositionSkill;
        _referenceImageInitialMassingSkill = referenceImageInitialMassingSkill;
        _referenceImageDetailRefinementSkill = referenceImageDetailRefinementSkill;
        _referenceImageMaterialPlanningSkill = referenceImageMaterialPlanningSkill;
        _referenceImageIterationDecisionSkill = referenceImageIterationDecisionSkill;
        _rhinoBlockDefinitionService = rhinoBlockDefinitionService;
        _rhinoBlockInspectionService = rhinoBlockInspectionService;
        _blockLifecycleSkill = blockLifecycleSkill;
        _takeoffScheduleService = takeoffScheduleService;
        _takeoffSpreadsheetAgent = takeoffSpreadsheetAgent;
        RegisterExtensionHandlers();
    }

    public bool TryHandle(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        string key = args[0].ToLowerInvariant();
        if (_extensionHandlers.TryGetValue(key, out Func<string[], bool>? extensionHandler))
        {
            return extensionHandler(args);
        }

        return key switch
        {
            "apply-object-edits" => HandleApplyObjectEdits(args),
            "apply-object-user-text-writes" => HandleApplyObjectUserTextWrites(args),
            "delete-object-user-text" => HandleDeleteObjectUserText(args),
            "set-document-user-strings" => HandleSetDocumentUserStrings(args),
            "delete-document-user-strings" => HandleDeleteDocumentUserStrings(args),
            "geometry-smoke-test" => HandleGeometrySmokeTest(args),
            "online-mutation-refactor-smoke-test" => HandleOnlineMutationRefactorSmokeTest(args),
            "layer-management-smoke-test" => HandleLayerManagementSmokeTest(args),
            "layer-behavior-probe" => HandleLayerBehaviorProbe(args),
            _ => false
        };
    }

    private bool HandleFindLayerCandidates(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- find-layer-candidates <active-3dm-file-path> <layerQuery> [exactMatch]");
            return true;
        }

        bool exactMatch = args.Length >= 4 && bool.TryParse(args[3], out var parsedExactMatch)
            ? parsedExactMatch
            : false;

        var result = _filterService.FindLayerCandidates(new FindLayerCandidatesRequest
        {
            FilePath = args[1],
            LayerQuery = args[2],
            ExactMatch = exactMatch
        });

        Console.WriteLine(_filterService.FormatLayerCandidates(args[2], result.Success ? result.Data : null, result.Message));
        return true;
    }

    private bool HandleFilterObjectsByLayer(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- filter-objects-by-layer <active-3dm-file-path> <layerQuery> [confirmedLayerFullPath]");
            return true;
        }

        string? confirmedLayerFullPath = args.Length >= 4 ? args[3] : null;
        Console.WriteLine(_layerSkill.Filter(args[1], args[2], confirmedLayerFullPath));
        return true;
    }

    private bool HandleFilterObjectsByType(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- filter-objects-by-type <active-3dm-file-path> <type1,type2,...>");
            return true;
        }

        Console.WriteLine(_typeSkill.Filter(args[1], ParseCsv(args[2])));
        return true;
    }

    private bool HandleFilterObjectsByUserAttributes(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- filter-objects-by-user-attributes <active-3dm-file-path> <attrSpec> [attrMode]");
            return true;
        }

        FilterMatchMode attributeMatchMode = args.Length >= 4
            ? ParseFilterMatchMode(args[3])
            : FilterMatchMode.All;

        Console.WriteLine(_userAttributeSkill.Filter(args[1], ParseUserAttributeConditions(args[2]), attributeMatchMode));
        return true;
    }

    private bool HandleFilterObjectsAgent(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- filter-objects-agent <active-3dm-file-path> [layers=...] [layerpaths=...] [types=...] [attrs=...] [mode=all|any] [attrmode=all|any]");
            return true;
        }

        var request = new FilterObjectsRequest
        {
            FilePath = args[1]
        };

        for (int i = 2; i < args.Length; i++)
        {
            string token = args[i];
            if (token.StartsWith("layers=", StringComparison.OrdinalIgnoreCase))
            {
                request.LayerQueries = ParseCsv(token[7..]);
            }
            else if (token.StartsWith("layerpaths=", StringComparison.OrdinalIgnoreCase))
            {
                request.ConfirmedLayerFullPaths = ParseCsv(token[11..]);
            }
            else if (token.StartsWith("types=", StringComparison.OrdinalIgnoreCase))
            {
                request.ObjectTypes = ParseCsv(token[6..]);
            }
            else if (token.StartsWith("attrs=", StringComparison.OrdinalIgnoreCase))
            {
                request.UserAttributeConditions = ParseUserAttributeConditions(token[6..]);
            }
            else if (token.StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
            {
                request.MatchMode = ParseFilterMatchMode(token[5..]);
            }
            else if (token.StartsWith("attrmode=", StringComparison.OrdinalIgnoreCase))
            {
                request.UserAttributeMatchMode = ParseFilterMatchMode(token[9..]);
            }
        }

        Console.WriteLine(_filterAgent.Filter(request));
        return true;
    }

    private bool HandlePreviewObjectEdits(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- preview-object-edits <active-3dm-file-path> <editSpec> [layers=...] [layerpaths=...] [types=...] [attrs=...] [mode=all|any] [attrmode=all|any]. editSpec supports set-user:key=value, remove-user:key, set-layer:fullPath, set-color:r,g,b");
            return true;
        }

        try
        {
            var request = BuildPreviewObjectEditsRequest(args);
            var result = _editingAgent.Preview(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _editingService.FormatPreview(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Object edit preview argument parsing failed: {ex.Message}");
        }

        return true;
    }

    private bool HandleApplyObjectEdits(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- apply-object-edits <active-3dm-file-path> <editSpec> [layers=...] [layerpaths=...] [types=...] [attrs=...] [mode=all|any] [attrmode=all|any]. editSpec supports set-user:key=value, remove-user:key, set-layer:fullPath, set-color:r,g,b");
            return true;
        }

        try
        {
            var request = BuildApplyObjectEditsRequest(args);
            var result = _editingAgent.Apply(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _editingService.FormatExecution(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Object edit apply argument parsing failed: {ex.Message}");
        }

        return true;
    }

    private bool HandlePreviewObjectUserTextWrites(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- preview-object-user-text-writes <active-3dm-file-path> <entrySpec>. entrySpec format: objectId|key=value;objectId|key=value");
            return true;
        }

        try
        {
            var request = BuildObjectUserTextBatchWriteRequest(args);
            var result = _userTextService.Preview(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _userTextService.FormatPreview(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Object user text preview argument parsing failed: {ex.Message}");
        }

        return true;
    }

    private bool HandleApplyObjectUserTextWrites(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- apply-object-user-text-writes <active-3dm-file-path> <entrySpec>. entrySpec format: objectId|key=value;objectId|key=value");
            return true;
        }

        try
        {
            var request = BuildObjectUserTextBatchWriteRequest(args);
            var result = _userTextService.Apply(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _userTextService.FormatExecution(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Object user text apply argument parsing failed: {ex.Message}");
        }

        return true;
    }

    private bool HandleGetObjectUserStrings(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- get-object-user-strings <active-3dm-file-path> <guid1,guid2,...>");
            return true;
        }

        try
        {
            var request = new ObjectUserTextReadRequest
            {
                FilePath = args[1],
                ObjectIds = ParseGuidCsv(args[2])
            };

            var result = _userTextService.Read(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _userTextService.FormatRead(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Object user string read argument parsing failed: {ex.Message}");
        }

        return true;
    }

    private bool HandleDeleteObjectUserText(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- delete-object-user-text <active-3dm-file-path> <entrySpec>. entrySpec format: objectId|key;objectId|key");
            return true;
        }

        try
        {
            var request = new ObjectUserTextDeleteRequest
            {
                FilePath = args[1],
                Entries = ParseObjectScopedUserTextKeys(args[2])
            };

            var result = _userTextService.Delete(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _userTextService.FormatExecution(result.Data)
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Object user text delete argument parsing failed: {ex.Message}");
        }

        return true;
    }

    private bool HandleGetDocumentUserStrings(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- get-document-user-strings <active-3dm-file-path>");
            return true;
        }

        var result = _documentUserStringService.Read(new DocumentUserStringReadRequest
        {
            FilePath = args[1]
        });

        Console.WriteLine(result.Success && result.Data is not null
            ? _documentUserStringService.FormatRead(result.Data)
            : result.Message);
        return true;
    }

    private bool HandleSetDocumentUserStrings(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- set-document-user-strings <active-3dm-file-path> <entrySpec>. entrySpec format: key=value;section|entry=value");
            return true;
        }

        try
        {
            var request = new DocumentUserStringWriteRequest
            {
                FilePath = args[1],
                Entries = ParseDocumentUserStringWriteEntries(args[2])
            };

            var result = _documentUserStringService.Set(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _documentUserStringService.FormatMutation(result.Data, "Document User String Write")
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Document user string write argument parsing failed: {ex.Message}");
        }

        return true;
    }

    private bool HandleDeleteDocumentUserStrings(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- delete-document-user-strings <active-3dm-file-path> <entrySpec>. entrySpec format: key;section|entry");
            return true;
        }

        try
        {
            var request = new DocumentUserStringDeleteRequest
            {
                FilePath = args[1],
                Entries = ParseDocumentUserStringDeleteEntries(args[2])
            };

            var result = _documentUserStringService.Delete(request);
            Console.WriteLine(result.Success && result.Data is not null
                ? _documentUserStringService.FormatMutation(result.Data, "Document User String Delete")
                : result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Document user string delete argument parsing failed: {ex.Message}");
        }

        return true;
    }

}

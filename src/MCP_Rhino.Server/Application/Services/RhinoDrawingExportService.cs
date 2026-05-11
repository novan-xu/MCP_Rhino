extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoDrawingExportService
{
    private const double DefaultFitMarginPercent = 10d;
    private const double DefaultImageDpi = 96d;
    private const double DefaultPdfDpi = 300d;
    private static readonly TimeSpan ExportTimeout = TimeSpan.FromSeconds(120);
    private static readonly FileExportImageSize DefaultImageSize = new() { Width = 1920, Height = 1080 };
    private static readonly FileExportPageSize DefaultPdfPageSize = new() { WidthMm = 420d, HeightMm = 297d };

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILiveDrawingViewManager _viewManager;
    private readonly ILiveDrawingExportStateOperator _stateOperator;
    private readonly IDrawingExportSnapshotStore _snapshotStore;
    private readonly ILiveFileExporter _fileExporter;

    public RhinoDrawingExportService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILiveDrawingViewManager viewManager,
        ILiveDrawingExportStateOperator stateOperator,
        IDrawingExportSnapshotStore snapshotStore,
        ILiveFileExporter fileExporter)
    {
        _documentAccessor = documentAccessor;
        _viewManager = viewManager;
        _stateOperator = stateOperator;
        _snapshotStore = snapshotStore;
        _fileExporter = fileExporter;
    }

    public OperationResponse<DrawingViewSetupResponse> SetupDrawingViews(SetupDrawingViewsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<DrawingViewSetupResponse>.Fail("FilePath is required.");
        }

        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: SetupDrawingViews", document =>
        {
            OperationResponse<DrawingLayerScopeResult> scope = _viewManager.ResolveLayerScope(
                document,
                NormalizeList(request.LayerQueries),
                NormalizeList(request.ConfirmedLayerFullPaths));

            if (!scope.Success || scope.Data is null)
            {
                return OperationResponse<(bool Mutated, DrawingViewSetupResponse Result)>.Fail(scope.Message);
            }

            if (scope.Data.NeedsLayerSelection)
            {
                return OperationResponse<(bool Mutated, DrawingViewSetupResponse Result)>.Ok(
                    (false, CreateNeedsLayerSelectionSetupResponse(request.FilePath, scope.Data.CandidateLayers)),
                    "Drawing export requires layer selection.");
            }

            OperationResponse<DrawingViewSetupResult> setup = _viewManager.SetupViews(
                document,
                request.Preset,
                scope.Data.ResolvedLayerIndices,
                scope.Data.ResolvedLayerFullPaths,
                request.FitMarginPercent ?? DefaultFitMarginPercent);

            if (!setup.Success || setup.Data is null)
            {
                return OperationResponse<(bool Mutated, DrawingViewSetupResponse Result)>.Fail(setup.Message);
            }

            return OperationResponse<(bool Mutated, DrawingViewSetupResponse Result)>.Ok(
                (true, CreateSetupResponse(request.FilePath, setup.Data)),
                setup.Message);
        });
    }

    public OperationResponse<DrawingExportStateResponse> CaptureDrawingExportState(CaptureDrawingExportStateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<DrawingExportStateResponse>.Fail("FilePath is required.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            OperationResponse<DrawingLayerScopeResult> scope = _viewManager.ResolveLayerScope(
                document,
                NormalizeList(request.LayerQueries),
                NormalizeList(request.ConfirmedLayerFullPaths));

            if (!scope.Success || scope.Data is null)
            {
                return OperationResponse<DrawingExportStateResponse>.Fail(scope.Message);
            }

            if (scope.Data.NeedsLayerSelection)
            {
                return OperationResponse<DrawingExportStateResponse>.Ok(new DrawingExportStateResponse
                {
                    FilePath = request.FilePath,
                    Status = DrawingExportResponseStatus.NeedsLayerSelection,
                    CandidateLayers = scope.Data.CandidateLayers
                }, "Drawing export requires layer selection.");
            }

            IReadOnlyList<Guid> objectIds = FilterObjectIds(document, scope.Data.ResolvedLayerIndices, request);
            DrawingExportSnapshot snapshot = _snapshotStore.Store(_stateOperator.Capture(document, request.FilePath, objectIds));

            return OperationResponse<DrawingExportStateResponse>.Ok(new DrawingExportStateResponse
            {
                FilePath = request.FilePath,
                SnapshotId = snapshot.SnapshotId,
                BackgroundStateCaptured = true,
                TouchedObjectCount = snapshot.ObjectSnapshots.Count,
                ExpiresUtc = snapshot.ExpiresUtc
            }, "Drawing export state captured.");
        });
    }

    public OperationResponse<DrawingExportStateResponse> ApplyDrawingExportStyle(ApplyDrawingExportStyleRequest request)
    {
        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: ApplyDrawingExportStyle", document =>
        {
            OperationResponse<DrawingExportSnapshot> snapshotResult = ResolveSnapshot(request.FilePath, request.SnapshotId);
            if (!snapshotResult.Success || snapshotResult.Data is null)
            {
                return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Fail(snapshotResult.Message);
            }

            OperationResponse validation = ValidateSnapshotForDocument(document, snapshotResult.Data);
            if (!validation.Success)
            {
                return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Fail(validation.Message);
            }

            OperationResponse applied = _stateOperator.ApplyObjectColor(document, snapshotResult.Data, ToDisplayColor(request.ObjectColor));
            if (!applied.Success)
            {
                return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Fail(applied.Message);
            }

            return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Ok(
                (request.ObjectColor is not null, CreateStateResponse(snapshotResult.Data)),
                applied.Message);
        });
    }

    public OperationResponse<DrawingExportStateResponse> SetDrawingExportBackground(SetDrawingExportBackgroundRequest request)
    {
        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: SetDrawingExportBackground", document =>
        {
            OperationResponse<DrawingExportSnapshot> snapshotResult = ResolveSnapshot(request.FilePath, request.SnapshotId);
            if (!snapshotResult.Success || snapshotResult.Data is null)
            {
                return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Fail(snapshotResult.Message);
            }

            OperationResponse validation = ValidateSnapshotForDocument(document, snapshotResult.Data);
            if (!validation.Success)
            {
                return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Fail(validation.Message);
            }

            OperationResponse applied = _stateOperator.SetBackground(document, snapshotResult.Data, ToDisplayColor(request.Color) ?? CreateWhite());
            if (!applied.Success)
            {
                return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Fail(applied.Message);
            }

            return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Ok(
                (true, CreateStateResponse(snapshotResult.Data)),
                applied.Message);
        });
    }

    public OperationResponse<DrawingExportStateResponse> RestoreDrawingExportState(RestoreDrawingExportStateRequest request)
    {
        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: RestoreDrawingExportState", document =>
        {
            OperationResponse<DrawingExportSnapshot> snapshotResult = ResolveSnapshot(request.FilePath, request.SnapshotId);
            if (!snapshotResult.Success || snapshotResult.Data is null)
            {
                return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Fail(snapshotResult.Message);
            }

            OperationResponse validation = ValidateSnapshotForDocument(document, snapshotResult.Data);
            if (!validation.Success)
            {
                return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Fail(validation.Message);
            }

            OperationResponse<IReadOnlyList<ObjectEditWarning>> restored = _stateOperator.Restore(document, snapshotResult.Data);
            _snapshotStore.Remove(snapshotResult.Data.SnapshotId);
            if (!restored.Success)
            {
                return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Fail(restored.Message);
            }

            DrawingExportStateResponse response = CreateStateResponse(snapshotResult.Data);
            response.Warnings = restored.Data ?? Array.Empty<ObjectEditWarning>();
            return OperationResponse<(bool Mutated, DrawingExportStateResponse Result)>.Ok(
                (true, response),
                restored.Message);
        });
    }

    public OperationResponse<DrawingExportPackageResponse> ExportDrawingPackage(ExportDrawingPackageRequest request)
    {
        OperationResponse outputValidation = ValidatePackageRequest(request);
        if (!outputValidation.Success)
        {
            return OperationResponse<DrawingExportPackageResponse>.Fail(outputValidation.Message);
        }

        OperationResponse<DrawingExportPackageResponse> response = _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: ExportDrawingPackage", document =>
        {
            OperationResponse<DrawingLayerScopeResult> scope = _viewManager.ResolveLayerScope(
                document,
                NormalizeList(request.LayerQueries),
                NormalizeList(request.ConfirmedLayerFullPaths));

            if (!scope.Success || scope.Data is null)
            {
                return OperationResponse<(bool Mutated, DrawingExportPackageResponse Result)>.Fail(scope.Message);
            }

            if (scope.Data.NeedsLayerSelection)
            {
                return OperationResponse<(bool Mutated, DrawingExportPackageResponse Result)>.Ok(
                    (false, CreateNeedsLayerSelectionPackageResponse(request.FilePath, scope.Data.CandidateLayers)),
                    "Drawing export requires layer selection.");
            }

            OperationResponse<IReadOnlyList<PreparedDrawingExport>> prepared = PrepareExportItems(request);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, DrawingExportPackageResponse Result)>.Fail(prepared.Message);
            }

            IReadOnlyList<Guid> styleObjectIds = FilterObjectIds(document, scope.Data.ResolvedLayerIndices, request);
            DrawingExportSnapshot snapshot = _stateOperator.Capture(document, request.FilePath, styleObjectIds);
            var warnings = new List<ObjectEditWarning>();
            var exportedItems = new List<DrawingExportItemResponse>();
            IReadOnlyList<ObjectEditWarning> restoreWarnings = Array.Empty<ObjectEditWarning>();
            bool restoreSucceeded = true;
            string? failureMessage = null;

            try
            {
                OperationResponse applied = _stateOperator.ApplyObjectColor(document, snapshot, ToDisplayColor(request.TemporaryObjectColor));
                if (!applied.Success)
                {
                    failureMessage = applied.Message;
                    return CreatePackageFailure(failureMessage);
                }

                OperationResponse<DrawingViewSetupResult> setup = _viewManager.SetupViews(
                    document,
                    request.Preset,
                    scope.Data.ResolvedLayerIndices,
                    scope.Data.ResolvedLayerFullPaths,
                    request.FitMarginPercent ?? DefaultFitMarginPercent);

                if (!setup.Success || setup.Data is null)
                {
                    failureMessage = setup.Message;
                    return CreatePackageFailure(failureMessage);
                }

                OperationResponse background = _stateOperator.SetBackground(document, snapshot, CreateWhite());
                if (!background.Success)
                {
                    failureMessage = background.Message;
                    return CreatePackageFailure(failureMessage);
                }

                foreach (PreparedDrawingExport item in prepared.Data)
                {
                    OperationResponse<FileExportExecutionResult> exported = _fileExporter.Export(document, item.Spec);
                    if (!exported.Success || exported.Data is null)
                    {
                        failureMessage = exported.Message;
                        return CreatePackageFailure(failureMessage);
                    }

                    exportedItems.Add(new DrawingExportItemResponse
                    {
                        ViewName = item.ViewName,
                        OutputPath = item.Spec.OutputPath,
                        Format = item.Spec.Format,
                        OutputFileSizeBytes = exported.Data.OutputFileSizeBytes,
                        DurationMs = exported.Data.DurationMs,
                        Warnings = exported.Data.Warnings
                    });
                    warnings.AddRange(exported.Data.Warnings);
                }
            }
            finally
            {
                OperationResponse<IReadOnlyList<ObjectEditWarning>> restored = _stateOperator.Restore(document, snapshot);
                restoreWarnings = restored.Data ?? Array.Empty<ObjectEditWarning>();
                restoreSucceeded = restored.Success && restoreWarnings.Count == 0;
                if (!restoreSucceeded)
                {
                    warnings.Add(new ObjectEditWarning
                    {
                        Code = "DRAWING_EXPORT_RESTORE_FAILED",
                        Message = restored.Success ? "Drawing export restore completed with warnings." : restored.Message
                    });
                    warnings.AddRange(restoreWarnings);
                }
            }

            if (!string.IsNullOrWhiteSpace(failureMessage))
            {
                return OperationResponse<(bool Mutated, DrawingExportPackageResponse Result)>.Fail(failureMessage);
            }

            var packageResponse = new DrawingExportPackageResponse
            {
                Status = DrawingExportResponseStatus.Completed,
                FilePath = request.FilePath,
                ResolvedLayerFullPaths = scope.Data.ResolvedLayerFullPaths,
                ViewNames = prepared.Data.Select(item => item.ViewName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                ExportedFiles = exportedItems,
                RestoreSucceeded = restoreSucceeded,
                RestoreWarnings = restoreWarnings,
                Warnings = warnings
            };

            return OperationResponse<(bool Mutated, DrawingExportPackageResponse Result)>.Ok(
                (true, packageResponse),
                restoreSucceeded ? "Drawing package exported." : "Drawing package exported with restore warnings.");
        }, ExportTimeout);

        return !response.Success && string.Equals(response.Message, "RHINO_MAIN_THREAD_BUSY", StringComparison.Ordinal)
            ? OperationResponse<DrawingExportPackageResponse>.Fail("EXPORT_COMMAND_TIMEOUT")
            : response;
    }

    private static OperationResponse<(bool Mutated, DrawingExportPackageResponse Result)> CreatePackageFailure(string message)
    {
        return OperationResponse<(bool Mutated, DrawingExportPackageResponse Result)>.Fail(message);
    }

    private static DrawingViewSetupResponse CreateNeedsLayerSelectionSetupResponse(string filePath, IReadOnlyList<RhinoLayerCandidate> candidates)
    {
        return new DrawingViewSetupResponse
        {
            Status = DrawingExportResponseStatus.NeedsLayerSelection,
            FilePath = filePath,
            CandidateLayers = candidates
        };
    }

    private static DrawingExportPackageResponse CreateNeedsLayerSelectionPackageResponse(string filePath, IReadOnlyList<RhinoLayerCandidate> candidates)
    {
        return new DrawingExportPackageResponse
        {
            Status = DrawingExportResponseStatus.NeedsLayerSelection,
            FilePath = filePath,
            CandidateLayers = candidates
        };
    }

    private static DrawingViewSetupResponse CreateSetupResponse(string filePath, DrawingViewSetupResult setup)
    {
        return new DrawingViewSetupResponse
        {
            Status = DrawingExportResponseStatus.Completed,
            FilePath = filePath,
            ResolvedLayerFullPaths = setup.ResolvedLayerFullPaths,
            CreatedViewNames = setup.CreatedViewNames,
            UpdatedViewNames = setup.UpdatedViewNames,
            ViewNames = setup.ViewNames,
            TargetObjectCount = setup.TargetObjectCount,
            Warnings = setup.Warnings
        };
    }

    private static DrawingExportStateResponse CreateStateResponse(DrawingExportSnapshot snapshot)
    {
        return new DrawingExportStateResponse
        {
            Status = DrawingExportResponseStatus.Completed,
            FilePath = snapshot.FilePath,
            SnapshotId = snapshot.SnapshotId,
            BackgroundStateCaptured = true,
            TouchedObjectCount = snapshot.ObjectSnapshots.Count,
            ExpiresUtc = snapshot.ExpiresUtc
        };
    }

    private OperationResponse<DrawingExportSnapshot> ResolveSnapshot(string filePath, string snapshotId)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResponse<DrawingExportSnapshot>.Fail("FilePath is required.");
        }

        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            return OperationResponse<DrawingExportSnapshot>.Fail("DRAWING_EXPORT_SNAPSHOT_NOT_FOUND");
        }

        if (!_snapshotStore.TryGet(snapshotId, out DrawingExportSnapshot? snapshot) || snapshot is null)
        {
            return OperationResponse<DrawingExportSnapshot>.Fail("DRAWING_EXPORT_SNAPSHOT_NOT_FOUND");
        }

        if (!PathsEqual(snapshot.FilePath, filePath) || snapshot.ExpiresUtc <= DateTime.UtcNow)
        {
            return OperationResponse<DrawingExportSnapshot>.Fail("DRAWING_EXPORT_SNAPSHOT_STALE");
        }

        return OperationResponse<DrawingExportSnapshot>.Ok(snapshot);
    }

    private static OperationResponse ValidateSnapshotForDocument(RhinoDoc document, DrawingExportSnapshot snapshot)
    {
        return document.RuntimeSerialNumber == snapshot.DocumentRuntimeSerialNumber
            ? OperationResponse.Ok()
            : OperationResponse.Fail("DRAWING_EXPORT_SNAPSHOT_STALE");
    }

    private static OperationResponse ValidatePackageRequest(ExportDrawingPackageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse.Fail("FilePath is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return OperationResponse.Fail("OutputDirectory is required.");
        }

        if (!Path.IsPathFullyQualified(request.OutputDirectory))
        {
            return OperationResponse.Fail("OutputDirectory must be an absolute path.");
        }

        if (!Directory.Exists(request.OutputDirectory))
        {
            return OperationResponse.Fail("EXPORT_OUTPUT_PARENT_NOT_FOUND");
        }

        if (!request.ExportPdf && !request.ExportJpg)
        {
            return OperationResponse.Fail("At least one drawing export format is required.");
        }

        if (request.ImageSizePx is not null && (request.ImageSizePx.Width <= 0 || request.ImageSizePx.Height <= 0))
        {
            return OperationResponse.Fail("ImageSizePx must have positive Width and Height.");
        }

        if (request.PageSizeMm is not null && (request.PageSizeMm.WidthMm <= 0d || request.PageSizeMm.HeightMm <= 0d))
        {
            return OperationResponse.Fail("PageSizeMm must have positive WidthMm and HeightMm.");
        }

        if (request.DotsPerInch.HasValue && request.DotsPerInch.Value <= 0d)
        {
            return OperationResponse.Fail("DotsPerInch must be greater than zero.");
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse<IReadOnlyList<PreparedDrawingExport>> PrepareExportItems(ExportDrawingPackageRequest request)
    {
        IReadOnlyList<string> viewNames = NormalizeList(request.ViewNames).Count > 0
            ? NormalizeList(request.ViewNames)
            : StandardViewNames();

        var items = new List<PreparedDrawingExport>();
        string outputDirectory = Path.GetFullPath(request.OutputDirectory);
        foreach (string viewName in viewNames)
        {
            string safeName = SanitizeFileName(viewName);
            if (request.ExportPdf)
            {
                string outputPath = Path.Combine(outputDirectory, $"{safeName}.pdf");
                if (File.Exists(outputPath) && !request.OverwriteExisting)
                {
                    return OperationResponse<IReadOnlyList<PreparedDrawingExport>>.Fail("EXPORT_OUTPUT_OVERWRITE_BLOCKED");
                }

                items.Add(new PreparedDrawingExport
                {
                    ViewName = viewName,
                    Spec = new FileExportSpec
                    {
                        OutputPath = outputPath,
                        Format = FileExportFormat.Pdf,
                        OverwriteExisting = request.OverwriteExisting,
                        ViewName = viewName,
                        PageSizeMm = request.PageSizeMm is null
                            ? new FileExportPageSize { WidthMm = DefaultPdfPageSize.WidthMm, HeightMm = DefaultPdfPageSize.HeightMm }
                            : new FileExportPageSize { WidthMm = request.PageSizeMm.WidthMm, HeightMm = request.PageSizeMm.HeightMm },
                        DotsPerInch = request.DotsPerInch ?? DefaultPdfDpi
                    }
                });
            }

            if (request.ExportJpg)
            {
                string outputPath = Path.Combine(outputDirectory, $"{safeName}.jpg");
                if (File.Exists(outputPath) && !request.OverwriteExisting)
                {
                    return OperationResponse<IReadOnlyList<PreparedDrawingExport>>.Fail("EXPORT_OUTPUT_OVERWRITE_BLOCKED");
                }

                items.Add(new PreparedDrawingExport
                {
                    ViewName = viewName,
                    Spec = new FileExportSpec
                    {
                        OutputPath = outputPath,
                        Format = FileExportFormat.Jpg,
                        OverwriteExisting = request.OverwriteExisting,
                        ViewName = viewName,
                        ImageSizePx = request.ImageSizePx is null
                            ? new FileExportImageSize { Width = DefaultImageSize.Width, Height = DefaultImageSize.Height }
                            : new FileExportImageSize { Width = request.ImageSizePx.Width, Height = request.ImageSizePx.Height },
                        DotsPerInch = request.DotsPerInch ?? DefaultImageDpi
                    }
                });
            }
        }

        return OperationResponse<IReadOnlyList<PreparedDrawingExport>>.Ok(items);
    }

    private static IReadOnlyList<Guid> FilterObjectIds(RhinoDoc document, IReadOnlyList<int> layerIndices, CaptureDrawingExportStateRequest request)
    {
        return FilterObjectIds(document, layerIndices, request.ObjectTypes, request.UserAttributeConditions, request.UserAttributeMatchMode);
    }

    private static IReadOnlyList<Guid> FilterObjectIds(RhinoDoc document, IReadOnlyList<int> layerIndices, ExportDrawingPackageRequest request)
    {
        return FilterObjectIds(document, layerIndices, request.ObjectTypes, request.UserAttributeConditions, request.UserAttributeMatchMode);
    }

    private static IReadOnlyList<Guid> FilterObjectIds(
        RhinoDoc document,
        IReadOnlyList<int> layerIndices,
        IReadOnlyList<string> objectTypes,
        IReadOnlyList<UserAttributeConditionRequest> userAttributeConditions,
        FilterMatchMode userAttributeMatchMode)
    {
        HashSet<int> layerIndexSet = layerIndices.ToHashSet();
        List<string> normalizedTypes = objectTypes
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Select(type => type.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var objectIds = new List<Guid>();
        foreach (RhinoObject rhinoObject in document.Objects)
        {
            if (rhinoObject.IsDeleted || !rhinoObject.Attributes.Visible || !layerIndexSet.Contains(rhinoObject.Attributes.LayerIndex))
            {
                continue;
            }

            if (normalizedTypes.Count > 0 && !normalizedTypes.Any(type => MatchesObjectType(rhinoObject, type)))
            {
                continue;
            }

            if (userAttributeConditions.Count > 0 && !MatchesUserAttributes(rhinoObject, userAttributeConditions, userAttributeMatchMode))
            {
                continue;
            }

            objectIds.Add(rhinoObject.Id);
        }

        return objectIds;
    }

    private static bool MatchesObjectType(RhinoObject rhinoObject, string requestedType)
    {
        string rawObjectType = rhinoObject.Geometry?.ObjectType.ToString() ?? string.Empty;
        string geometryTypeName = rhinoObject.Geometry?.GetType().Name ?? string.Empty;
        return string.Equals(rawObjectType, requestedType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(geometryTypeName, requestedType, StringComparison.OrdinalIgnoreCase)
            || NormalizeObjectType(rawObjectType, geometryTypeName).ToString().Equals(requestedType, StringComparison.OrdinalIgnoreCase);
    }

    private static RhinoObjectType NormalizeObjectType(string rawObjectType, string geometryTypeName)
    {
        if (geometryTypeName.Contains("TextDot", StringComparison.OrdinalIgnoreCase)
            || geometryTypeName.Contains("AnnotationDot", StringComparison.OrdinalIgnoreCase))
        {
            return RhinoObjectType.AnnotationDot;
        }

        return rawObjectType switch
        {
            "Point" => RhinoObjectType.Point,
            "Curve" => RhinoObjectType.Curve,
            "Surface" => RhinoObjectType.Surface,
            "Brep" => RhinoObjectType.Brep,
            "Mesh" => RhinoObjectType.Mesh,
            "InstanceReference" => RhinoObjectType.BlockInstance,
            "Annotation" => RhinoObjectType.Annotation,
            _ => RhinoObjectType.Unknown
        };
    }

    private static bool MatchesUserAttributes(
        RhinoObject rhinoObject,
        IReadOnlyList<UserAttributeConditionRequest> conditions,
        FilterMatchMode matchMode)
    {
        IEnumerable<bool> results = conditions
            .Where(condition => !string.IsNullOrWhiteSpace(condition.Key))
            .Select(condition => MatchesUserAttribute(rhinoObject, condition));

        return matchMode == FilterMatchMode.Any ? results.Any(result => result) : results.All(result => result);
    }

    private static bool MatchesUserAttribute(RhinoObject rhinoObject, UserAttributeConditionRequest condition)
    {
        string? value = rhinoObject.Attributes.GetUserString(condition.Key);
        return condition.ComparisonMode switch
        {
            UserAttributeComparisonMode.Exists => value is not null,
            UserAttributeComparisonMode.Contains => value?.Contains(condition.ExpectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            _ => string.Equals(value, condition.ExpectedValue, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static RhinoDisplayColor CreateWhite()
    {
        return new RhinoDisplayColor { R = 255, G = 255, B = 255 };
    }

    private static RhinoDisplayColor? ToDisplayColor(ObjectColorRequest? color)
    {
        return color is null
            ? null
            : new RhinoDisplayColor
            {
                R = color.R,
                G = color.G,
                B = color.B
            };
    }

    private static List<string> NormalizeList(IEnumerable<string>? values)
    {
        return values is null
            ? new List<string>()
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private static IReadOnlyList<string> StandardViewNames()
    {
        return new[]
        {
            "MCP_Elevation_Front",
            "MCP_Elevation_Back",
            "MCP_Elevation_Left",
            "MCP_Elevation_Right",
            "MCP_Iso_NE",
            "MCP_Iso_NW",
            "MCP_Iso_SE",
            "MCP_Iso_SW"
        };
    }

    private static string SanitizeFileName(string value)
    {
        string sanitized = string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "drawing-view" : sanitized;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PreparedDrawingExport
    {
        public string ViewName { get; init; } = string.Empty;
        public FileExportSpec Spec { get; init; } = new();
    }
}

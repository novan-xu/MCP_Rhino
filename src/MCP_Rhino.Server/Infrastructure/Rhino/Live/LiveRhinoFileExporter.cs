extern alias rhinocommon;

using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using ArchivableDictionary = rhinocommon::Rhino.Collections.ArchivableDictionary;
using FileDwgWriteOptions = rhinocommon::Rhino.FileIO.FileDwgWriteOptions;
using FilePdf = rhinocommon::Rhino.FileIO.FilePdf;
using FileStlWriteOptions = rhinocommon::Rhino.FileIO.FileStlWriteOptions;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using RhinoView = rhinocommon::Rhino.Display.RhinoView;
using ViewCaptureSettings = rhinocommon::Rhino.Display.ViewCaptureSettings;
using ViewTypeFilter = rhinocommon::Rhino.Display.ViewTypeFilter;
using ViewInfo = rhinocommon::Rhino.DocObjects.ViewInfo;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoFileExporter : ILiveFileExporter
{
    public OperationResponse<FileExportExecutionResult> Export(RhinoDoc document, FileExportSpec spec)
    {
        return spec.Format switch
        {
            FileExportFormat.Dwg or FileExportFormat.Dxf or FileExportFormat.Ifc or FileExportFormat.Stl => ExportExternalFile(document, spec),
            FileExportFormat.Jpg or FileExportFormat.Png or FileExportFormat.Bmp or FileExportFormat.Tiff => OperatingSystem.IsWindows()
                ? ExportImage(document, spec)
                : OperationResponse<FileExportExecutionResult>.Fail("Image export requires Windows."),
            FileExportFormat.Pdf => ExportPdf(document, spec),
            _ => OperationResponse<FileExportExecutionResult>.Fail($"Unsupported export format: {spec.Format}")
        };
    }

    private static OperationResponse<FileExportExecutionResult> ExportExternalFile(RhinoDoc document, FileExportSpec spec)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<ObjectEditWarning>();
        AddOverwriteWarning(spec.OutputPath, warnings);

        List<Guid> previousSelectedIds = document.Objects.GetSelectedObjects(true, true)
            .Select(item => item.Id)
            .ToList();

        bool selectionTemporarilyChanged = false;
        IReadOnlyList<Guid> matchedObjectIds = Array.Empty<Guid>();

        try
        {
            int exportedObjectCount;
            if (spec.SelectedObjectIds.Count > 0)
            {
                matchedObjectIds = ResolveExistingObjectIds(document, spec.SelectedObjectIds, out int missingCount);
                if (matchedObjectIds.Count == 0)
                {
                    return OperationResponse<FileExportExecutionResult>.Fail("No selectedObjectIds matched objects in the live document.");
                }

                if (missingCount > 0)
                {
                    warnings.Add(new ObjectEditWarning
                    {
                        Code = "SELECTED_OBJECT_IDS_NOT_FOUND",
                        Message = $"{missingCount} selectedObjectIds did not resolve in the live document and were skipped."
                    });
                }

                document.Objects.UnselectAll();
                document.Objects.Select(matchedObjectIds);
                selectionTemporarilyChanged = true;
                exportedObjectCount = matchedObjectIds.Count;
            }
            else
            {
                exportedObjectCount = CountLiveObjects(document);
            }

            ArchivableDictionary options = CreateExternalExportOptions(spec, warnings);
            bool wrote = spec.SelectedObjectIds.Count > 0
                ? document.ExportSelected(spec.OutputPath, options)
                : document.Export(spec.OutputPath, options);

            if (!wrote)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "CODE_DRIVEN_EXPORT_FAILED",
                    Message = "RhinoDoc.Export/ExportSelected returned false."
                });
            }

            if (!HasNonEmptyOutput(spec.OutputPath))
            {
                OperationResponse scripted = TryScriptedExportFallback(document, spec, matchedObjectIds, warnings);
                if (!scripted.Success)
                {
                    return OperationResponse<FileExportExecutionResult>.Fail(CreateExternalExportFailureMessage(spec, scripted.Message));
                }
            }

            stopwatch.Stop();
            OperationResponse<FileExportExecutionResult> result = CreateValidatedResult(
                spec.OutputPath,
                exportedObjectCount,
                stopwatch.ElapsedMilliseconds,
                warnings,
                $"{spec.Format} export");
            return result.Success
                ? result
                : OperationResponse<FileExportExecutionResult>.Fail(CreateExternalExportFailureMessage(spec, result.Message));
        }
        catch (Exception ex)
        {
            return OperationResponse<FileExportExecutionResult>.Fail($"File export failed: {ex.Message}");
        }
        finally
        {
            if (selectionTemporarilyChanged)
            {
                document.Objects.UnselectAll();
                if (previousSelectedIds.Count > 0)
                {
                    document.Objects.Select(previousSelectedIds);
                }

                document.Views.Redraw();
            }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static OperationResponse<FileExportExecutionResult> ExportImage(RhinoDoc document, FileExportSpec spec)
    {
        if (spec.ImageSizePx is null)
        {
            return OperationResponse<FileExportExecutionResult>.Fail("ImageSizePx is required for image export.");
        }

        OperationResponse<ResolvedView> resolvedViewResponse = ResolveView(document, spec.ViewName);
        if (!resolvedViewResponse.Success || resolvedViewResponse.Data is null)
        {
            return OperationResponse<FileExportExecutionResult>.Fail(resolvedViewResponse.Message);
        }

        using ResolvedView resolvedView = resolvedViewResponse.Data;
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<ObjectEditWarning>();
        warnings.AddRange(resolvedView.Warnings);
        AddOverwriteWarning(spec.OutputPath, warnings);

        if (spec.BackgroundTransparent && spec.Format == FileExportFormat.Jpg)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "IMAGE_TRANSPARENCY_UNSUPPORTED",
                Message = "JPEG does not support transparency; the exported background will be opaque."
            });
        }

        try
        {
            double dpi = spec.DotsPerInch ?? 96d;
            OperationResponse<RhinoCapturedBitmap> captured = RhinoViewBitmapCapture.Capture(
                resolvedView.View,
                new Size(spec.ImageSizePx.Width, spec.ImageSizePx.Height),
                dpi,
                !spec.BackgroundTransparent);
            if (!captured.Success || captured.Data is null)
            {
                return OperationResponse<FileExportExecutionResult>.Fail($"Image export failed: {captured.Message}");
            }

            using RhinoCapturedBitmap bitmap = captured.Data;
            OperationResponse saved = bitmap.Save(spec.OutputPath, dpi);
            if (!saved.Success)
            {
                return OperationResponse<FileExportExecutionResult>.Fail($"Image export failed: {saved.Message}");
            }

            stopwatch.Stop();
            return CreateValidatedResult(spec.OutputPath, CountLiveObjects(document), stopwatch.ElapsedMilliseconds, warnings, "Image export");
        }
        catch (Exception ex)
        {
            return OperationResponse<FileExportExecutionResult>.Fail($"Image export failed: {ex.Message}");
        }
    }

    private static OperationResponse<FileExportExecutionResult> ExportPdf(RhinoDoc document, FileExportSpec spec)
    {
        if (spec.PageSizeMm is null)
        {
            return OperationResponse<FileExportExecutionResult>.Fail("PageSizeMm is required for PDF export.");
        }

        OperationResponse<ResolvedView> resolvedViewResponse = ResolveView(document, spec.ViewName);
        if (!resolvedViewResponse.Success || resolvedViewResponse.Data is null)
        {
            return OperationResponse<FileExportExecutionResult>.Fail(resolvedViewResponse.Message);
        }

        using ResolvedView resolvedView = resolvedViewResponse.Data;
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<ObjectEditWarning>();
        warnings.AddRange(resolvedView.Warnings);
        AddOverwriteWarning(spec.OutputPath, warnings);

        try
        {
            double dpi = spec.DotsPerInch ?? 300d;
            Size mediaSize = new(
                Math.Max(1, (int)Math.Round(spec.PageSizeMm.WidthMm / 25.4d * dpi)),
                Math.Max(1, (int)Math.Round(spec.PageSizeMm.HeightMm / 25.4d * dpi)));

            using var settings = new ViewCaptureSettings(resolvedView.View, mediaSize, dpi);
            settings.DrawMargins = false;
            settings.DrawBackground = true;
            settings.DrawGrid = false;
            settings.DrawAxis = false;
            settings.MatchViewportAspectRatio();

            FilePdf pdf = FilePdf.Create();
            int pageIndex = pdf.AddPage(settings);
            if (pageIndex < 0)
            {
                return OperationResponse<FileExportExecutionResult>.Fail("PDF export failed: FilePdf.AddPage did not add a page.");
            }

            pdf.Write(spec.OutputPath);

            stopwatch.Stop();
            return CreateValidatedResult(spec.OutputPath, CountLiveObjects(document), stopwatch.ElapsedMilliseconds, warnings, "PDF export");
        }
        catch (Exception ex)
        {
            return OperationResponse<FileExportExecutionResult>.Fail($"PDF export failed: {ex.Message}");
        }
    }

    private static OperationResponse<ResolvedView> ResolveView(RhinoDoc document, string? requestedViewName)
    {
        if (string.IsNullOrWhiteSpace(requestedViewName))
        {
            RhinoView? activeView = document.Views.ActiveView;
            if (activeView is null)
            {
                return OperationResponse<ResolvedView>.Fail("EXPORT_VIEW_NOT_FOUND");
            }

            return OperationResponse<ResolvedView>.Ok(new ResolvedView
            {
                View = activeView
            });
        }

        RhinoView[] matches = document.Views
            .GetViewList(ViewTypeFilter.All)
            .Where(view => string.Equals(view.MainViewport.Name, requestedViewName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
        {
            int namedViewIndex = document.NamedViews.FindByName(requestedViewName);
            if (namedViewIndex < 0)
            {
                return OperationResponse<ResolvedView>.Fail("EXPORT_VIEW_NOT_FOUND");
            }

            RhinoView? activeView = document.Views.ActiveView;
            if (activeView is null)
            {
                return OperationResponse<ResolvedView>.Fail("EXPORT_VIEW_NOT_FOUND");
            }

            var originalView = new ViewInfo(activeView.MainViewport);
            bool restored = document.NamedViews.Restore(namedViewIndex, activeView.MainViewport);
            if (!restored)
            {
                originalView.Dispose();
                return OperationResponse<ResolvedView>.Fail("EXPORT_VIEW_NOT_FOUND");
            }

            activeView.MainViewport.Name = requestedViewName;
            activeView.Redraw();
            return OperationResponse<ResolvedView>.Ok(new ResolvedView
            {
                View = activeView,
                RestoreViewInfo = originalView,
                Warnings = new[]
                {
                    new ObjectEditWarning
                    {
                        Code = "EXPORT_NAMED_VIEW_MATERIALIZED",
                        Message = $"Named view [{requestedViewName}] was restored into the active viewport for capture."
                    }
                }
            });
        }

        var warnings = new List<ObjectEditWarning>();
        if (matches.Length > 1)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "EXPORT_VIEW_NAME_DUPLICATE",
                Message = $"Multiple views matched [{requestedViewName}]; using the first match."
            });
        }

        return OperationResponse<ResolvedView>.Ok(new ResolvedView
        {
            View = matches[0],
            Warnings = warnings
        });
    }

    private static List<Guid> ResolveExistingObjectIds(RhinoDoc document, IReadOnlyList<Guid> requestedIds, out int missingCount)
    {
        var matched = new List<Guid>(requestedIds.Count);
        missingCount = 0;

        foreach (Guid objectId in requestedIds)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject is null || rhinoObject.IsDeleted)
            {
                missingCount++;
                continue;
            }

            matched.Add(objectId);
        }

        return matched;
    }

    private static int CountLiveObjects(RhinoDoc document)
    {
        int count = 0;
        foreach (RhinoObject rhinoObject in document.Objects)
        {
            if (!rhinoObject.IsDeleted)
            {
                count++;
            }
        }

        return count;
    }

    private static ArchivableDictionary CreateExternalExportOptions(
        FileExportSpec spec,
        ICollection<ObjectEditWarning> warnings)
    {
        return spec.Format switch
        {
            FileExportFormat.Dwg or FileExportFormat.Dxf => CreateTypedOptionsDictionary(
                new FileDwgWriteOptions(),
                spec,
                warnings,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Version",
                    "UseLWPolylines",
                    "Flatten",
                    "FullLayerPath",
                    "ExportSurfacesAs",
                    "ExportMeshesAs",
                    "ExportSplinesAs"
                }),
            FileExportFormat.Stl => CreateTypedOptionsDictionary(
                new FileStlWriteOptions(),
                spec,
                warnings,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "BinaryFile",
                    "ExportOpenObjects"
                }),
            FileExportFormat.Ifc => CreateIfcOptionsDictionary(spec, warnings),
            _ => new ArchivableDictionary()
        };
    }

    private static ArchivableDictionary CreateTypedOptionsDictionary(
        object typedOptions,
        FileExportSpec spec,
        ICollection<ObjectEditWarning> warnings,
        IReadOnlySet<string> supportedOptionNames)
    {
        var applied = new List<string>();
        foreach ((string key, string value) in spec.FormatOptions)
        {
            if (!supportedOptionNames.Contains(key))
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "FORMAT_OPTION_NOT_SUPPORTED",
                    Message = $"{spec.Format} formatOptions key [{key}] is not mapped by this exporter."
                });
                continue;
            }

            if (TryApplyOptionProperty(typedOptions, key, value, out string error))
            {
                applied.Add(key);
                continue;
            }

            warnings.Add(new ObjectEditWarning
            {
                Code = "FORMAT_OPTION_NOT_APPLIED",
                Message = $"{spec.Format} formatOptions key [{key}] was not applied. {error}"
            });
        }

        if (applied.Count > 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "FORMAT_OPTIONS_APPLIED",
                Message = $"{spec.Format} formatOptions applied: {string.Join(", ", applied)}."
            });
        }

        MethodInfo? toDictionary = typedOptions.GetType().GetMethod(
            "ToDictionary",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        if (toDictionary is null)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "FORMAT_OPTIONS_DICTIONARY_UNAVAILABLE",
                Message = $"{typedOptions.GetType().Name}.ToDictionary() was not available; using an empty export options dictionary."
            });
            return new ArchivableDictionary();
        }

        object? dictionary = toDictionary.Invoke(typedOptions, null);
        if (dictionary is ArchivableDictionary archivableDictionary)
        {
            return archivableDictionary;
        }

        warnings.Add(new ObjectEditWarning
        {
            Code = "FORMAT_OPTIONS_DICTIONARY_UNAVAILABLE",
            Message = $"{typedOptions.GetType().Name}.ToDictionary() did not return an ArchivableDictionary; using an empty export options dictionary."
        });
        return new ArchivableDictionary();
    }

    private static ArchivableDictionary CreateIfcOptionsDictionary(
        FileExportSpec spec,
        ICollection<ObjectEditWarning> warnings)
    {
        foreach (string key in spec.FormatOptions.Keys)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "IFC_FORMAT_OPTION_NOT_MAPPED",
                Message = $"IFC formatOptions key [{key}] is not mapped because this RhinoCommon build exposes no FileIfcWriteOptions type."
            });
        }

        return new ArchivableDictionary();
    }

    private static bool TryApplyOptionProperty(object target, string propertyName, string value, out string error)
    {
        PropertyInfo? property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null)
        {
            error = "The typed RhinoCommon options object has no matching writable property.";
            return false;
        }

        if (!property.CanWrite)
        {
            error = "The matching RhinoCommon options property is read-only.";
            return false;
        }

        if (!TryConvertOptionValue(value, property.PropertyType, out object? converted, out error))
        {
            return false;
        }

        property.SetValue(target, converted);
        return true;
    }

    private static bool TryConvertOptionValue(
        string value,
        Type targetType,
        out object? converted,
        out string error)
    {
        Type concreteType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try
        {
            if (concreteType == typeof(string))
            {
                converted = value;
                error = string.Empty;
                return true;
            }

            if (concreteType == typeof(bool))
            {
                if (bool.TryParse(value, out bool boolValue))
                {
                    converted = boolValue;
                    error = string.Empty;
                    return true;
                }

                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intBool))
                {
                    converted = intBool != 0;
                    error = string.Empty;
                    return true;
                }
            }

            if (concreteType.IsEnum)
            {
                converted = Enum.Parse(concreteType, value, ignoreCase: true);
                error = string.Empty;
                return true;
            }

            converted = Convert.ChangeType(value, concreteType, CultureInfo.InvariantCulture);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            converted = null;
            error = $"Value [{value}] could not be converted to {concreteType.Name}: {ex.Message}";
            return false;
        }
    }

    private static OperationResponse TryScriptedExportFallback(
        RhinoDoc document,
        FileExportSpec spec,
        IReadOnlyList<Guid> matchedObjectIds,
        ICollection<ObjectEditWarning> warnings)
    {
        if (matchedObjectIds.Count == 0)
        {
            return OperationResponse.Fail("Scripted export fallback requires an explicit selectedObjectIds scope.");
        }

        if (spec.Format is not (FileExportFormat.Dwg or FileExportFormat.Dxf or FileExportFormat.Stl or FileExportFormat.Ifc))
        {
            return OperationResponse.Fail("Scripted export fallback is not supported for this format.");
        }

        TryDeleteEmptyOutput(spec.OutputPath);
        string command = $"_-Export \"{EscapeRhinoCommandPath(spec.OutputPath)}\" _Enter _Enter _Enter";
        bool ran = RhinoApp.RunScript(command, echo: false);
        if (!ran)
        {
            return OperationResponse.Fail("Rhino scripted export fallback returned false.");
        }

        if (!HasNonEmptyOutput(spec.OutputPath))
        {
            return OperationResponse.Fail("Rhino scripted export fallback did not produce a non-empty output file.");
        }

        warnings.Add(new ObjectEditWarning
        {
            Code = "SCRIPTED_EXPORT_FALLBACK_USED",
            Message = "RhinoDoc.Export/ExportSelected did not produce output; a non-interactive Rhino _-Export fallback produced the file."
        });
        return OperationResponse.Ok();
    }

    private static string CreateExternalExportFailureMessage(FileExportSpec spec, string detail)
    {
        if (spec.Format == FileExportFormat.Ifc)
        {
            return $"IFC_EXPORT_UNAVAILABLE: Rhino did not produce a non-empty IFC file. Confirm that IFC export support is installed and enabled for this Rhino host. {detail}";
        }

        return $"{spec.Format} export failed: {detail}";
    }

    private static bool HasNonEmptyOutput(string outputPath)
    {
        return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0L;
    }

    private static OperationResponse<FileExportExecutionResult> CreateValidatedResult(
        string outputPath,
        int exportedObjectCount,
        long durationMs,
        IReadOnlyList<ObjectEditWarning> warnings,
        string operationName)
    {
        if (!File.Exists(outputPath))
        {
            return OperationResponse<FileExportExecutionResult>.Fail($"{operationName} completed but output file was not created: {outputPath}");
        }

        long sizeBytes = new FileInfo(outputPath).Length;
        if (sizeBytes <= 0L)
        {
            return OperationResponse<FileExportExecutionResult>.Fail($"{operationName} completed but output file was empty: {outputPath}");
        }

        return OperationResponse<FileExportExecutionResult>.Ok(CreateResult(outputPath, exportedObjectCount, durationMs, warnings));
    }

    private static string EscapeRhinoCommandPath(string outputPath)
    {
        return outputPath.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void TryDeleteEmptyOutput(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath) && new FileInfo(outputPath).Length == 0L)
            {
                File.Delete(outputPath);
            }
        }
        catch
        {
        }
    }

    private static FileExportExecutionResult CreateResult(
        string outputPath,
        int exportedObjectCount,
        long durationMs,
        IReadOnlyList<ObjectEditWarning> warnings)
    {
        long sizeBytes = 0L;
        if (File.Exists(outputPath))
        {
            sizeBytes = new FileInfo(outputPath).Length;
        }

        return new FileExportExecutionResult
        {
            ExportedObjectCount = exportedObjectCount,
            OutputFileSizeBytes = sizeBytes,
            DurationMs = durationMs,
            Warnings = warnings
        };
    }

    private static void AddOverwriteWarning(string outputPath, ICollection<ObjectEditWarning> warnings)
    {
        if (File.Exists(outputPath))
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "EXPORT_OVERWRITING_EXISTING_FILE",
                Message = "The export target already existed and was overwritten."
            });
        }
    }

    private static void AddFormatOptionWarning(FileExportSpec spec, ICollection<ObjectEditWarning> warnings)
    {
        if (spec.FormatOptions.Count > 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "FORMAT_OPTIONS_NOT_APPLIED",
                Message = "formatOptions are accepted by the request schema but are not yet applied by the RhinoCommon export path."
            });
        }
    }

    private sealed class ResolvedView : IDisposable
    {
        public RhinoView View { get; init; } = null!;
        public IReadOnlyList<ObjectEditWarning> Warnings { get; init; } = Array.Empty<ObjectEditWarning>();
        public ViewInfo? RestoreViewInfo { get; init; }

        public void Dispose()
        {
            if (RestoreViewInfo is null)
            {
                return;
            }

            View.MainViewport.PushViewInfo(RestoreViewInfo, false);
            View.Redraw();
            RestoreViewInfo.Dispose();
        }
    }
}

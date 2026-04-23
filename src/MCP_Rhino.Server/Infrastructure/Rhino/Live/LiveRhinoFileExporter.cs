extern alias rhinocommon;

using System.Diagnostics;
using System.Drawing;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using FilePdf = rhinocommon::Rhino.FileIO.FilePdf;
using FileWriteOptions = rhinocommon::Rhino.FileIO.FileWriteOptions;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using RhinoView = rhinocommon::Rhino.Display.RhinoView;
using ViewCapture = rhinocommon::Rhino.Display.ViewCapture;
using ViewCaptureSettings = rhinocommon::Rhino.Display.ViewCaptureSettings;
using ViewTypeFilter = rhinocommon::Rhino.Display.ViewTypeFilter;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoFileExporter : ILiveFileExporter
{
    public OperationResponse<FileExportExecutionResult> Export(RhinoDoc document, FileExportSpec spec)
    {
        return spec.Format switch
        {
            FileExportFormat.Dwg or FileExportFormat.Dxf or FileExportFormat.Ifc or FileExportFormat.Stl => ExportWithWriteFile(document, spec),
            FileExportFormat.Jpg or FileExportFormat.Png or FileExportFormat.Bmp or FileExportFormat.Tiff => OperatingSystem.IsWindows()
                ? ExportImage(document, spec)
                : OperationResponse<FileExportExecutionResult>.Fail("Image export requires Windows."),
            FileExportFormat.Pdf => ExportPdf(document, spec),
            _ => OperationResponse<FileExportExecutionResult>.Fail($"Unsupported export format: {spec.Format}")
        };
    }

    private static OperationResponse<FileExportExecutionResult> ExportWithWriteFile(RhinoDoc document, FileExportSpec spec)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<ObjectEditWarning>();
        AddOverwriteWarning(spec.OutputPath, warnings);
        AddFormatOptionWarning(spec, warnings);

        List<Guid> previousSelectedIds = document.Objects.GetSelectedObjects(true, true)
            .Select(item => item.Id)
            .ToList();

        bool selectionTemporarilyChanged = false;

        try
        {
            int exportedObjectCount;
            if (spec.SelectedObjectIds.Count > 0)
            {
                List<Guid> matchedObjectIds = ResolveExistingObjectIds(document, spec.SelectedObjectIds, out int missingCount);
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

            var options = new FileWriteOptions
            {
                UpdateDocumentPath = false,
                WriteSelectedObjectsOnly = spec.SelectedObjectIds.Count > 0,
                SuppressDialogBoxes = true,
                WriteUserData = true
            };

            bool wrote = document.WriteFile(spec.OutputPath, options);
            if (!wrote)
            {
                return OperationResponse<FileExportExecutionResult>.Fail($"WriteFile export failed for [{spec.OutputPath}].");
            }

            stopwatch.Stop();
            return OperationResponse<FileExportExecutionResult>.Ok(CreateResult(spec.OutputPath, exportedObjectCount, stopwatch.ElapsedMilliseconds, warnings));
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

        OperationResponse<ResolvedView> resolvedView = ResolveView(document, spec.ViewName);
        if (!resolvedView.Success || resolvedView.Data is null)
        {
            return OperationResponse<FileExportExecutionResult>.Fail(resolvedView.Message);
        }

        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<ObjectEditWarning>();
        warnings.AddRange(resolvedView.Data.Warnings);
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
            var capture = new ViewCapture
            {
                Width = spec.ImageSizePx.Width,
                Height = spec.ImageSizePx.Height,
                TransparentBackground = spec.BackgroundTransparent,
                DrawGrid = false,
                DrawAxes = false,
                DrawGridAxes = false
            };

            using Bitmap? bitmap = capture.CaptureToBitmap(resolvedView.Data.View);
            if (bitmap is null)
            {
                return OperationResponse<FileExportExecutionResult>.Fail($"Image capture failed for view [{resolvedView.Data.View.MainViewport.Name}].");
            }

            float dpi = (float)(spec.DotsPerInch ?? 96d);
            bitmap.SetResolution(dpi, dpi);
            bitmap.Save(spec.OutputPath);

            stopwatch.Stop();
            return OperationResponse<FileExportExecutionResult>.Ok(CreateResult(spec.OutputPath, CountLiveObjects(document), stopwatch.ElapsedMilliseconds, warnings));
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

        OperationResponse<ResolvedView> resolvedView = ResolveView(document, spec.ViewName);
        if (!resolvedView.Success || resolvedView.Data is null)
        {
            return OperationResponse<FileExportExecutionResult>.Fail(resolvedView.Message);
        }

        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<ObjectEditWarning>();
        warnings.AddRange(resolvedView.Data.Warnings);
        AddOverwriteWarning(spec.OutputPath, warnings);

        try
        {
            double dpi = spec.DotsPerInch ?? 300d;
            Size mediaSize = new(
                Math.Max(1, (int)Math.Round(spec.PageSizeMm.WidthMm / 25.4d * dpi)),
                Math.Max(1, (int)Math.Round(spec.PageSizeMm.HeightMm / 25.4d * dpi)));

            using var settings = new ViewCaptureSettings(resolvedView.Data.View, mediaSize, dpi);
            settings.DrawMargins = false;
            settings.DrawBackground = true;
            settings.DrawGrid = false;
            settings.DrawAxis = false;
            settings.MatchViewportAspectRatio();

            FilePdf pdf = FilePdf.Create();
            pdf.AddPage(settings);
            pdf.Write(spec.OutputPath);

            stopwatch.Stop();
            return OperationResponse<FileExportExecutionResult>.Ok(CreateResult(spec.OutputPath, CountLiveObjects(document), stopwatch.ElapsedMilliseconds, warnings));
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
            return OperationResponse<ResolvedView>.Fail("EXPORT_VIEW_NOT_FOUND");
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

    private sealed class ResolvedView
    {
        public RhinoView View { get; init; } = null!;
        public IReadOnlyList<ObjectEditWarning> Warnings { get; init; } = Array.Empty<ObjectEditWarning>();
    }
}

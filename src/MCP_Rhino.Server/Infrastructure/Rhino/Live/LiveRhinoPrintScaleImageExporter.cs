extern alias rhinocommon;

using System.Diagnostics;
using System.Drawing;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using BackgroundStyle = rhinocommon::Rhino.Display.BackgroundStyle;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoView = rhinocommon::Rhino.Display.RhinoView;
using UnitSystem = rhinocommon::Rhino.UnitSystem;
using ViewAreaMapping = rhinocommon::Rhino.Display.ViewCaptureSettings.ViewAreaMapping;
using ViewCaptureSettings = rhinocommon::Rhino.Display.ViewCaptureSettings;
using ViewInfo = rhinocommon::Rhino.DocObjects.ViewInfo;
using ViewTypeFilter = rhinocommon::Rhino.Display.ViewTypeFilter;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoPrintScaleImageExporter : ILivePrintScaleImageExporter
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public OperationResponse<PrintScaleImageExportExecutionResult> Export(
        RhinoDoc document,
        PrintScaleImageExportSpec spec)
    {
        RenderBackgroundState? originalBackground = null;
        OperationResponse<PrintScaleImageExportExecutionResult> result;
        string? restoreFailure = null;

        try
        {
            if (spec.SolidBackgroundColor is not null)
            {
                originalBackground = CaptureRenderBackground(document);
                ApplySolidRenderBackground(document, spec.SolidBackgroundColor);
            }

            result = ExportCore(document, spec);
        }
        catch (Exception ex)
        {
            result = OperationResponse<PrintScaleImageExportExecutionResult>.Fail($"PRINT_SCALE_IMAGE_EXPORT_FAILED: {ex.Message}");
        }
        finally
        {
            if (originalBackground is not null)
            {
                try
                {
                    RestoreRenderBackground(document, originalBackground);
                }
                catch (Exception ex)
                {
                    restoreFailure = ex.Message;
                }
            }
        }

        if (restoreFailure is not null)
        {
            return OperationResponse<PrintScaleImageExportExecutionResult>.Fail($"PRINT_SCALE_BACKGROUND_RESTORE_FAILED: {restoreFailure}");
        }

        if (result.Success && result.Data is not null)
        {
            result.Data.BackgroundRestored = true;
        }

        return result;
    }

    private static OperationResponse<PrintScaleImageExportExecutionResult> ExportCore(
        RhinoDoc document,
        PrintScaleImageExportSpec spec)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<ObjectEditWarning>();
        var pending = new List<PendingCapture>(spec.Views.Count);

        try
        {
            double commonScale = spec.ScaleMode == PrintScaleImageMode.Explicit
                ? spec.ModelScale!.Value
                : ResolveCommonFitScale(document, spec, warnings);

            if (!double.IsFinite(commonScale) || commonScale <= 0d)
            {
                return OperationResponse<PrintScaleImageExportExecutionResult>.Fail("PRINT_SCALE_VALUE_INVALID");
            }

            foreach (PrintScaleImageViewSpec viewSpec in spec.Views)
            {
                OperationResponse<ResolvedView> resolved = ResolveView(document, viewSpec.ViewName);
                if (!resolved.Success || resolved.Data is null)
                {
                    return OperationResponse<PrintScaleImageExportExecutionResult>.Fail($"{resolved.Message}: {viewSpec.ViewName}");
                }

                using ResolvedView resolvedView = resolved.Data;
                warnings.AddRange(resolvedView.Warnings);
                using ViewCaptureSettings settings = CreateSettings(document, resolvedView.View, spec);
                settings.SetModelScaleToValue(commonScale);

                OperationResponse<RhinoCapturedBitmap> capture = RhinoViewBitmapCapture.Capture(settings, viewSpec.ViewName);
                if (!capture.Success || capture.Data is null)
                {
                    return OperationResponse<PrintScaleImageExportExecutionResult>.Fail($"PRINT_SCALE_IMAGE_CAPTURE_FAILED: {viewSpec.ViewName}: {capture.Message}");
                }

                RhinoCapturedBitmap bitmap = capture.Data;
                OperationResponse<bool> uniform = bitmap.IsUniform();
                if (!uniform.Success)
                {
                    bitmap.Dispose();
                    return OperationResponse<PrintScaleImageExportExecutionResult>.Fail($"PRINT_SCALE_IMAGE_INSPECTION_FAILED: {viewSpec.ViewName}: {uniform.Message}");
                }

                if (uniform.Data)
                {
                    bitmap.Dispose();
                    return OperationResponse<PrintScaleImageExportExecutionResult>.Fail($"PRINT_SCALE_IMAGE_BLANK: {viewSpec.ViewName}");
                }

                pending.Add(new PendingCapture
                {
                    View = viewSpec,
                    Bitmap = bitmap,
                    TemporaryPath = CreateTemporaryPath(viewSpec.OutputPath)
                });
            }

            foreach (PendingCapture item in pending)
            {
                OperationResponse saved = item.Bitmap.Save(item.TemporaryPath, spec.DotsPerInch);
                if (!saved.Success || !File.Exists(item.TemporaryPath) || new FileInfo(item.TemporaryPath).Length <= 0L)
                {
                    return OperationResponse<PrintScaleImageExportExecutionResult>.Fail($"PRINT_SCALE_IMAGE_SAVE_FAILED: {item.View.ViewName}: {saved.Message}");
                }
            }

            foreach (PendingCapture item in pending)
            {
                File.Move(item.TemporaryPath, item.View.OutputPath, spec.OverwriteExisting);
            }

            stopwatch.Stop();
            return OperationResponse<PrintScaleImageExportExecutionResult>.Ok(new PrintScaleImageExportExecutionResult
            {
                AppliedModelScale = commonScale,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Images = pending.Select(item => new PrintScaleImageExportItemResult
                {
                    ViewName = item.View.ViewName,
                    OutputPath = item.View.OutputPath,
                    OutputFileSizeBytes = new FileInfo(item.View.OutputPath).Length
                }).ToList(),
                Warnings = warnings
            });
        }
        catch (PrintScaleExportException ex)
        {
            return OperationResponse<PrintScaleImageExportExecutionResult>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return OperationResponse<PrintScaleImageExportExecutionResult>.Fail($"PRINT_SCALE_IMAGE_EXPORT_FAILED: {ex.Message}");
        }
        finally
        {
            foreach (PendingCapture item in pending)
            {
                item.Bitmap.Dispose();
                TryDelete(item.TemporaryPath);
            }
        }
    }

    private static RenderBackgroundState CaptureRenderBackground(RhinoDoc document)
    {
        return new RenderBackgroundState
        {
            Style = document.RenderSettings.BackgroundStyle,
            Top = document.RenderSettings.BackgroundColorTop,
            Bottom = document.RenderSettings.BackgroundColorBottom
        };
    }

    private static void ApplySolidRenderBackground(RhinoDoc document, RhinoDisplayColor color)
    {
        Color resolved = Color.FromArgb(255, color.R, color.G, color.B);
        document.RenderSettings.BackgroundStyle = BackgroundStyle.SolidColor;
        document.RenderSettings.BackgroundColorTop = resolved;
        document.RenderSettings.BackgroundColorBottom = resolved;
        document.Views.Redraw();
    }

    private static void RestoreRenderBackground(RhinoDoc document, RenderBackgroundState state)
    {
        document.RenderSettings.BackgroundStyle = state.Style;
        document.RenderSettings.BackgroundColorTop = state.Top;
        document.RenderSettings.BackgroundColorBottom = state.Bottom;
        document.Views.Redraw();
    }

    private static double ResolveCommonFitScale(
        RhinoDoc document,
        PrintScaleImageExportSpec spec,
        ICollection<ObjectEditWarning> warnings)
    {
        double commonFitScale = 0d;
        foreach (PrintScaleImageViewSpec viewSpec in spec.Views)
        {
            OperationResponse<ResolvedView> resolved = ResolveView(document, viewSpec.ViewName);
            if (!resolved.Success || resolved.Data is null)
            {
                throw new PrintScaleExportException($"{resolved.Message}: {viewSpec.ViewName}");
            }

            using ResolvedView resolvedView = resolved.Data;
            foreach (ObjectEditWarning warning in resolvedView.Warnings)
            {
                warnings.Add(warning);
            }
            using ViewCaptureSettings settings = CreateSettings(document, resolvedView.View, spec);
            settings.SetModelScaleToFit(false);
            double fitScale = settings.GetModelScale(document.ModelUnitSystem, document.ModelUnitSystem);
            if (!double.IsFinite(fitScale) || fitScale <= 0d)
            {
                throw new PrintScaleExportException($"PRINT_SCALE_FIT_FAILED: {viewSpec.ViewName}");
            }

            commonFitScale = Math.Max(commonFitScale, fitScale);
        }

        return commonFitScale * spec.FitScaleMultiplier;
    }

    private static ViewCaptureSettings CreateSettings(
        RhinoDoc document,
        RhinoView view,
        PrintScaleImageExportSpec spec)
    {
        var mediaSize = new Size(spec.ImageSizePx.Width, spec.ImageSizePx.Height);
        var settings = new ViewCaptureSettings(view, mediaSize, spec.DotsPerInch)
        {
            Document = document,
            ViewArea = ViewAreaMapping.Extents,
            RasterMode = true,
            DrawGrid = false,
            DrawAxis = false,
            DrawMargins = false,
            DrawBackground = !spec.BackgroundTransparent,
            DrawLockedObjects = true,
            DrawClippingPlanes = true,
            DrawLights = true
        };

        if (!settings.SetMargins(
                UnitSystem.Millimeters,
                spec.MarginMm,
                spec.MarginMm,
                spec.MarginMm,
                spec.MarginMm))
        {
            settings.Dispose();
            throw new PrintScaleExportException("PRINT_SCALE_MARGIN_CONFIGURATION_FAILED");
        }

        return settings;
    }

    private static OperationResponse<ResolvedView> ResolveView(RhinoDoc document, string requestedViewName)
    {
        RhinoView[] matches = document.Views
            .GetViewList(ViewTypeFilter.All)
            .Where(view => string.Equals(view.MainViewport.Name, requestedViewName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length > 0)
        {
            return OperationResponse<ResolvedView>.Ok(new ResolvedView
            {
                View = matches[0],
                Warnings = matches.Length > 1
                    ? new[]
                    {
                        new ObjectEditWarning
                        {
                            Code = "PRINT_SCALE_VIEW_NAME_DUPLICATE",
                            Message = $"Multiple live views matched [{requestedViewName}]; the first was used."
                        }
                    }
                    : Array.Empty<ObjectEditWarning>()
            });
        }

        int namedViewIndex = document.NamedViews.FindByName(requestedViewName);
        RhinoView? activeView = document.Views.ActiveView;
        if (namedViewIndex < 0 || activeView is null)
        {
            return OperationResponse<ResolvedView>.Fail("PRINT_SCALE_VIEW_NOT_FOUND");
        }

        var originalView = new ViewInfo(activeView.MainViewport);
        if (!document.NamedViews.Restore(namedViewIndex, activeView.MainViewport))
        {
            originalView.Dispose();
            return OperationResponse<ResolvedView>.Fail("PRINT_SCALE_VIEW_NOT_FOUND");
        }

        activeView.Redraw();
        return OperationResponse<ResolvedView>.Ok(new ResolvedView
        {
            View = activeView,
            RestoreViewInfo = originalView
        });
    }

    private static string CreateTemporaryPath(string outputPath)
    {
        string directory = Path.GetDirectoryName(outputPath)!;
        string fileName = Path.GetFileNameWithoutExtension(outputPath);
        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp.png");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private sealed class PendingCapture
    {
        public PrintScaleImageViewSpec View { get; init; } = null!;
        public RhinoCapturedBitmap Bitmap { get; init; } = null!;
        public string TemporaryPath { get; init; } = string.Empty;
    }

    private sealed class ResolvedView : IDisposable
    {
        public RhinoView View { get; init; } = null!;
        public ViewInfo? RestoreViewInfo { get; init; }
        public IReadOnlyList<ObjectEditWarning> Warnings { get; init; } = Array.Empty<ObjectEditWarning>();

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

    private sealed class PrintScaleExportException : Exception
    {
        public PrintScaleExportException(string message)
            : base(message)
        {
        }
    }

    private sealed class RenderBackgroundState
    {
        public BackgroundStyle Style { get; init; }
        public Color Top { get; init; }
        public Color Bottom { get; init; }
    }
}

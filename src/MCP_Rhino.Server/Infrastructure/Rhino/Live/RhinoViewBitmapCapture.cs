extern alias rhinocommon;

using System.Drawing;
using System.Reflection;
using MCP_Rhino.Server.Contracts.Responses;
using RhinoView = rhinocommon::Rhino.Display.RhinoView;
using ViewCapture = rhinocommon::Rhino.Display.ViewCapture;
using ViewCaptureSettings = rhinocommon::Rhino.Display.ViewCaptureSettings;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

internal static class RhinoViewBitmapCapture
{
    private static readonly Lazy<MethodInfo?> CaptureToBitmapMethod = new(ResolveCaptureToBitmapMethod);

    public static OperationResponse<RhinoCapturedBitmap> Capture(
        RhinoView view,
        Size imageSize,
        double dotsPerInch,
        bool drawBackground)
    {
        MethodInfo? captureMethod = CaptureToBitmapMethod.Value;
        if (captureMethod is null)
        {
            return OperationResponse<RhinoCapturedBitmap>.Fail("VIEW_CAPTURE_METHOD_NOT_FOUND");
        }

        try
        {
            using var settings = new ViewCaptureSettings(view, imageSize, dotsPerInch)
            {
                DrawGrid = false,
                DrawAxis = false,
                DrawMargins = false,
                DrawBackground = drawBackground
            };
            settings.MatchViewportAspectRatio();

            object? bitmap = captureMethod.Invoke(null, new object?[] { settings });
            return bitmap is null
                ? OperationResponse<RhinoCapturedBitmap>.Fail($"Image capture failed for view [{view.MainViewport.Name}].")
                : OperationResponse<RhinoCapturedBitmap>.Ok(new RhinoCapturedBitmap(bitmap));
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return OperationResponse<RhinoCapturedBitmap>.Fail(ex.InnerException.Message);
        }
        catch (Exception ex)
        {
            return OperationResponse<RhinoCapturedBitmap>.Fail(ex.Message);
        }
    }

    private static MethodInfo? ResolveCaptureToBitmapMethod()
    {
        return typeof(ViewCapture)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
            {
                if (!string.Equals(method.Name, "CaptureToBitmap", StringComparison.Ordinal))
                {
                    return false;
                }

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 1
                    && parameters[0].ParameterType == typeof(ViewCaptureSettings);
            });
    }
}

internal sealed class RhinoCapturedBitmap : IDisposable
{
    private readonly object _bitmap;

    public RhinoCapturedBitmap(object bitmap)
    {
        _bitmap = bitmap;
    }

    public OperationResponse Save(string outputPath, double dotsPerInch)
    {
        try
        {
            SetResolution(dotsPerInch);
            InvokeSave(outputPath);
            return OperationResponse.Ok();
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return OperationResponse.Fail(ex.InnerException.Message);
        }
        catch (Exception ex)
        {
            return OperationResponse.Fail(ex.Message);
        }
    }

    public void Dispose()
    {
        if (_bitmap is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void SetResolution(double dotsPerInch)
    {
        MethodInfo? setResolution = _bitmap.GetType().GetMethod(
            "SetResolution",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(float), typeof(float) },
            modifiers: null);

        setResolution?.Invoke(_bitmap, new object[] { (float)dotsPerInch, (float)dotsPerInch });
    }

    private void InvokeSave(string outputPath)
    {
        Type bitmapType = _bitmap.GetType();
        Type? imageFormatType = bitmapType.Assembly.GetType("System.Drawing.Imaging.ImageFormat", throwOnError: false);
        object? imageFormat = ResolveImageFormat(outputPath, imageFormatType);
        if (imageFormat is not null && imageFormatType is not null)
        {
            MethodInfo? saveWithFormat = bitmapType.GetMethod(
                "Save",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(string), imageFormatType },
                modifiers: null);

            if (saveWithFormat is not null)
            {
                saveWithFormat.Invoke(_bitmap, new[] { outputPath, imageFormat });
                return;
            }
        }

        MethodInfo? save = bitmapType.GetMethod(
            "Save",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);

        if (save is null)
        {
            throw new MissingMethodException(bitmapType.FullName, "Save(string)");
        }

        save.Invoke(_bitmap, new object[] { outputPath });
    }

    private static object? ResolveImageFormat(string outputPath, Type? imageFormatType)
    {
        if (imageFormatType is null)
        {
            return null;
        }

        string propertyName = Path.GetExtension(outputPath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "Jpeg",
            ".png" => "Png",
            ".bmp" => "Bmp",
            ".tif" or ".tiff" => "Tiff",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(propertyName)
            ? null
            : imageFormatType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
    }
}

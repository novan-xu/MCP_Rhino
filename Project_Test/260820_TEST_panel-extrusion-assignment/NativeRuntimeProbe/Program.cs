using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

if (args.Length is < 1 or > 2)
{
    throw new ArgumentException("Pass the packaged or installed plug-in directory and optionally a PDF schedule.");
}

string pluginRoot = Path.GetFullPath(args[0]);
string skiaManagedPath = Path.Combine(pluginRoot, "SkiaSharp.dll");
string skiaNativePath = Path.Combine(pluginRoot, "libSkiaSharp.dll");
string harfBuzzNativePath = Path.Combine(pluginRoot, "libHarfBuzzSharp.dll");
Require(File.Exists(skiaManagedPath), $"Managed SkiaSharp is missing: {skiaManagedPath}");
Require(File.Exists(skiaNativePath), $"Top-level native SkiaSharp is missing: {skiaNativePath}");
Require(File.Exists(harfBuzzNativePath), $"Top-level native HarfBuzzSharp is missing: {harfBuzzNativePath}");

AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string candidate = Path.Combine(pluginRoot, $"{name.Name}.dll");
    if (File.Exists(candidate))
    {
        return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
    }
    string rhinoCandidate = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Rhino 8",
        "System",
        "netcore",
        $"{name.Name}.dll");
    return File.Exists(rhinoCandidate)
        ? AssemblyLoadContext.Default.LoadFromAssemblyPath(rhinoCandidate)
        : null;
};

Assembly skia = AssemblyLoadContext.Default.LoadFromAssemblyPath(skiaManagedPath);
Type bitmapType = skia.GetType("SkiaSharp.SKBitmap", throwOnError: true) ??
    throw new InvalidOperationException("SkiaSharp.SKBitmap was not found.");
object bitmap = Activator.CreateInstance(bitmapType, 32, 32, false) ??
    throw new InvalidOperationException("SKBitmap construction returned null.");
((IDisposable)bitmap).Dispose();

nint harfBuzz = NativeLibrary.Load(harfBuzzNativePath);
NativeLibrary.Free(harfBuzz);

Console.WriteLine($"[OK] top-level SkiaSharp P/Invoke resolved and constructed SKBitmap from {pluginRoot}");
Console.WriteLine("[OK] top-level HarfBuzzSharp native library loaded successfully.");

if (args.Length == 2)
{
    string rhpPath = Path.Combine(pluginRoot, "PanelCladdingEditor.rhp");
    Assembly plugin = AssemblyLoadContext.Default.LoadFromAssemblyPath(rhpPath);
    Type importerType = plugin.GetType(
        "PanelCladdingEditor.Infrastructure.PanelCladding.PdfFrameExtrusionScheduleImporter",
        throwOnError: true) ?? throw new InvalidOperationException("PDF importer type was not found.");
    Type requestType = plugin.GetType(
        "PanelCladdingEditor.Domain.Models.PanelCladding.PanelFrameExtrusionScheduleImportRequest",
        throwOnError: true) ?? throw new InvalidOperationException("PDF import request type was not found.");
    object importer = Activator.CreateInstance(importerType) ??
        throw new InvalidOperationException("PDF importer could not be constructed.");
    object request = Activator.CreateInstance(requestType) ??
        throw new InvalidOperationException("PDF import request could not be constructed.");
    requestType.GetProperty("PdfPath")?.SetValue(request, Path.GetFullPath(args[1]));
    object response = importerType.GetMethod("Import")?.Invoke(importer, [request]) ??
        throw new InvalidOperationException("PDF import returned null.");
    Type responseType = response.GetType();
    bool success = (bool)(responseType.GetProperty("Success")?.GetValue(response) ?? false);
    string message = responseType.GetProperty("Message")?.GetValue(response)?.ToString() ?? string.Empty;
    Require(success, $"Packaged PDF import failed: {message}");
    object data = responseType.GetProperty("Data")?.GetValue(response) ??
        throw new InvalidOperationException("Packaged PDF import returned no data.");
    object extrusions = data.GetType().GetProperty("Extrusions")?.GetValue(data) ??
        throw new InvalidOperationException("Packaged PDF import returned no extrusion collection.");
    int count = ((System.Collections.IEnumerable)extrusions).Cast<object>().Count();
    Require(count == 38, $"Packaged PDF import returned {count} profiles instead of all 38.");
    Console.WriteLine("[OK] packaged RHP imported all 38 profiles from the six-page project PDF.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

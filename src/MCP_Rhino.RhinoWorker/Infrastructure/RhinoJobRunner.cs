using System.Text.Json;
using System.IO;
using MCP_Rhino.Offline.Contracts;
using Rhino;
using Rhino.FileIO;
using Rhino.Runtime.InProcess;

namespace MCP_Rhino.RhinoWorker.Infrastructure;

internal static class RhinoJobRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static int Run(string requestPath, string resultPath)
    {
        RhinoWorkerResult result;
        try
        {
            var request = JsonSerializer.Deserialize<RhinoWorkerRequest>(File.ReadAllText(requestPath), Json)
                ?? throw new InvalidOperationException("Missing job request.");
            using var core = new RhinoCore(["/NOSPLASH", "/NOTEMPLATE"], WindowStyle.NoWindow);
            using var doc = Import(request);
            int before = PersistedObjectIds(doc).Count;
            var globals = RhinoScriptCompiler.Execute(doc, request);
            var expectedIds = PersistedObjectIds(doc);
            int after = expectedIds.Count;
            if (File.Exists(request.OutputPath)) throw new IOException("Worker output already exists.");
            if (!doc.Write3dmFile(request.OutputPath, new FileWriteOptions { FileVersion = 8, WriteUserData = true, SuppressDialogBoxes = true }))
                throw new InvalidOperationException("Rhino failed to serialize the working document.");
            using var reread = File3dm.Read(request.OutputPath) ?? throw new InvalidOperationException("Rhino output cannot be reopened.");
            if (!expectedIds.SetEquals(reread.Objects.Select(o => o.Id)) || reread.Settings.ModelUnitSystem != doc.ModelUnitSystem)
                throw new InvalidOperationException($"Rhino output identity/units failed readback: expected objects={after}, file={reread.Objects.Count}, units={doc.ModelUnitSystem}/{reread.Settings.ModelUnitSystem}.");
            result = new(true, null, before, after, doc.InstanceDefinitions.Count, doc.Layers.Count,
                doc.ModelUnitSystem.ToString(), RhinoApp.Version.ToString(), globals.Messages,
                JsonSerializer.SerializeToElement(globals.Result, Json));
        }
        catch (Exception error)
        {
            result = new(false, error.ToString(), 0, 0, 0, 0, "", "", [], JsonSerializer.SerializeToElement<object?>(null));
        }
        File.WriteAllText(resultPath, JsonSerializer.Serialize(result, Json));
        return result.Success ? 0 : 1;
    }

    // ObjectTable.Count includes deleted import intermediates. IdefObjects=true is an
    // exclusive filter and does not enumerate definition members in this headless import.
    private static HashSet<Guid> PersistedObjectIds(RhinoDoc doc)
    {
        var settings = new Rhino.DocObjects.ObjectEnumeratorSettings {
            NormalObjects = true, HiddenObjects = true, LockedObjects = true,
            ActiveObjects = true, ReferenceObjects = false, IdefObjects = false,
            DeletedObjects = false, IncludeLights = true
        };
        return doc.Objects.GetObjectList(settings)
            .Concat(doc.InstanceDefinitions.Where(d => !d.IsDeleted && !d.IsReference).SelectMany(d => d.GetObjects()))
            .Select(o => o.Id).ToHashSet();
    }

    private static RhinoDoc Import(RhinoWorkerRequest request)
    {
        string extension = Path.GetExtension(request.InputPath).ToLowerInvariant();
        if (extension == ".3dm")
            return RhinoDoc.OpenHeadless(request.InputPath) ?? throw new InvalidOperationException("Rhino could not open the input 3dm.");
        if (extension is not (".dwg" or ".dxf")) throw new InvalidOperationException("Unsupported Rhino offline input format.");
        var doc = RhinoDoc.CreateHeadless(null) ?? throw new InvalidOperationException("Could not create a headless document.");
        try
        {
            var options = new FileDwgReadOptions { ImportUnreferencedBlocks = true, ImportUnreferencedLayers = true,
                ImportUnreferencedLinetypes = true, ConvertWidePolylinesToSurfaces = false };
            if (!string.IsNullOrWhiteSpace(request.ModelUnits))
            {
                if (!Enum.TryParse<UnitSystem>(request.ModelUnits, true, out var units) || !Enum.IsDefined(units)
                    || units is UnitSystem.None or UnitSystem.Unset or UnitSystem.CustomUnits)
                    throw new InvalidOperationException("modelUnits must be a standard Rhino unit name, such as Inches or Millimeters.");
                doc.ModelUnitSystem = units;
                options.ModelUnits = units;
                options.LayoutUnits = units;
                doc.ModelAbsoluteTolerance = RhinoMath.UnitScale(UnitSystem.Inches, units) * .001;
            }
            if (!FileDwg.Read(request.InputPath, doc, options)) throw new InvalidOperationException("Rhino DWG/DXF import failed.");
            if (doc.Objects.Count == 0) throw new InvalidOperationException("DWG/DXF import produced no objects; no preview retained.");
            return doc;
        }
        catch { doc.Dispose(); throw; }
    }
}

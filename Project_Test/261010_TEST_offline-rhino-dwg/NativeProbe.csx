using Rhino;
using Rhino.Runtime.InProcess;

internal static class Startup
{
    [STAThread]
    private static int Main(string[] args)
    {
        RhinoInside.Resolver.Initialize();
        Console.WriteLine("Resolver initialized: " + RhinoInside.Resolver.RhinoSystemDirectory);
        return Probe.Run(args);
    }
}
internal static class Probe
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static int Run(string[] args)
    {
        try
        {
            Console.WriteLine("Starting native RhinoCore.");
            using var core = new RhinoCore(new[] { "/NOSPLASH", "/NOTEMPLATE" }, WindowStyle.NoWindow);
            Console.WriteLine("Native RhinoCore ready: " + RhinoApp.Version);
            using var doc = RhinoDoc.CreateHeadless(null);
            Console.WriteLine("Headless document ready; units=" + doc.ModelUnitSystem + "; undo=" + doc.UndoRecordingEnabled);
            if (args.Length > 0)
            {
                doc.ModelUnitSystem = UnitSystem.Inches;
                doc.ModelAbsoluteTolerance = .001;
                bool ok = Rhino.FileIO.FileDwg.Read(args[0], doc, new Rhino.FileIO.FileDwgReadOptions {
                    ModelUnits = UnitSystem.Inches, LayoutUnits = UnitSystem.Inches,
                    ImportUnreferencedBlocks = true, ImportUnreferencedLayers = true
                });
                Console.WriteLine("DWG import=" + ok + "; objects=" + doc.Objects.Count + "; definitions=" + doc.InstanceDefinitions.Count);
                if (!ok) return 2;
                foreach (bool idef in new[]{false,true})
                foreach (bool reference in new[]{false,true})
                {
                    var settings = new Rhino.DocObjects.ObjectEnumeratorSettings { NormalObjects=true, HiddenObjects=true, LockedObjects=true, ActiveObjects=true, ReferenceObjects=reference, IdefObjects=idef, DeletedObjects=false, IncludeLights=true };
                    Console.WriteLine($"enumeration idef={idef}, ref={reference}: {doc.Objects.GetObjectList(settings).Count()}");
                }
                var definitionObjects = doc.InstanceDefinitions.Where(d=>!d.IsDeleted && !d.IsReference).SelectMany(d=>d.GetObjects()).ToArray();
                Console.WriteLine("definition objects="+definitionObjects.Length+" distinct="+definitionObjects.Select(o=>o.Id).Distinct().Count());
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

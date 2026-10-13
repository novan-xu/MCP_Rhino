using Rhino.FileIO;

namespace MCP_Rhino.Offline.Infrastructure;

public static class OfflineRuntimeValidation
{
    public static void Validate()
    {
        using var model = new File3dm();
        var bridge = new OfflineModelBridge(model);
        OfflineScriptRunner.Run("model.addPoint([1,2,3]);", bridge, CancellationToken.None);
        _ = bridge.SerializeVerified();
    }

    public static void ValidateRhinoWorkerBundle(string directory)
    {
        foreach (string name in new[] { "MCP_Rhino.RhinoWorker.exe", "MCP_Rhino.RhinoWorker.dll",
            "MCP_Rhino.RhinoWorker.deps.json", "MCP_Rhino.RhinoWorker.runtimeconfig.json", "Rhino.Inside.dll",
            "Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis.CSharp.dll",
            "Microsoft.CodeAnalysis.Scripting.dll", "Microsoft.CodeAnalysis.CSharp.Scripting.dll" })
            if (!File.Exists(Path.Combine(directory, "RhinoWorker", name)))
                throw new InvalidOperationException("Missing native Rhino worker dependency: " + name);
        if (File.Exists(Path.Combine(directory, "RhinoCommon.dll")) || File.Exists(Path.Combine(directory, "RhinoWorker", "Rhino3dm.dll")))
            throw new InvalidOperationException("RhinoCommon/Rhino3dm process isolation was violated.");
        // File validation does not start/licence Rhino; bounded JS still works without Rhino installed.
    }
}

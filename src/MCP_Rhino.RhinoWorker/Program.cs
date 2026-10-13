using MCP_Rhino.RhinoWorker.Infrastructure;

namespace MCP_Rhino.RhinoWorker;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Internal worker: request.json result.json"); return 2; }
        try
        {
            // Resolve installed Rhino assemblies before JIT touches any RhinoCommon type.
            RhinoInside.Resolver.Initialize();
            return RhinoJobRunner.Run(args[0], args[1]);
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

extern alias rhinocommon;

using Result = rhinocommon::Rhino.Commands.Result;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

// _McpDevSmoke:plugin-side plugin-health sanity check.
// Historically wired to online-mutation-refactor-smoke-test (DeveloperCommandHandler
// fallback) but that smoke was authored with CLI-fallback assertions
// (expects LIVE_RHINO_REQUIRED); when invoked here the plugin DI binds the real
// LiveRhinoDocumentAccessor, flipping the expected failure codes and making the
// assertions spurious. Until a proper live-mode smoke is authored, this command
// is reduced to a plugin-loaded / pipe-up heartbeat + directions to the working
// probe command.
public sealed class McpDevSmokeCommand : RhinoCommand
{
    public override string EnglishName => "McpDevSmoke";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (McpRhinoPlugin.Instance is null)
        {
            RhinoApp.WriteLine("MCP_Rhino plugin instance is not available.");
            return Result.Failure;
        }

        RhinoApp.WriteLine("MCP_Rhino plugin heartbeat:");
        RhinoApp.WriteLine($"  ActiveDoc: {(doc is null ? "<none>" : string.IsNullOrWhiteSpace(doc.Path) ? "<unsaved>" : doc.Path)}");
        RhinoApp.WriteLine($"  Named pipe: \\\\.\\pipe\\{McpRhinoPlugin.PipeName}");
        RhinoApp.WriteLine("  Live smoke: not wired (online-mutation-refactor smoke was CLI-only).");
        RhinoApp.WriteLine("  For layer API behavior verification, run _McpLayerBehaviorProbe on a throwaway saved .3dm.");
        return Result.Success;
    }
}

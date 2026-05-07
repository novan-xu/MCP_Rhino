extern alias rhinocommon;

using Result = rhinocommon::Rhino.Commands.Result;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

public sealed class McpDebugBridgeOnlyPluginSmokeCommand : RhinoCommand
{
    public override string EnglishName => "McpDebugBridgeOnlyPluginSmoke";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (McpRhinoPlugin.Instance is null)
        {
            RhinoApp.WriteLine("MCP_Rhino plugin instance is not available.");
            return Result.Failure;
        }

        RhinoApp.WriteLine("Running debug-bridge-only-plugin-smoke-test.");
        bool success = McpRhinoPlugin.Instance.RunDeveloperCommand("debug-bridge-only-plugin-smoke-test");
        return success ? Result.Success : Result.Failure;
    }
}

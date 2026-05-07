extern alias rhinocommon;

using Result = rhinocommon::Rhino.Commands.Result;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

public sealed class McpPanelRuntimePolicySmokeCommand : RhinoCommand
{
    public override string EnglishName => "McpPanelRuntimePolicySmoke";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (McpRhinoPlugin.Instance is null)
        {
            RhinoApp.WriteLine("MCP_Rhino plugin instance is not available.");
            return Result.Failure;
        }

        RhinoApp.WriteLine("Running panel-runtime-policy-smoke-test.");
        bool success = McpRhinoPlugin.Instance.RunDeveloperCommand("panel-runtime-policy-smoke-test");
        return success ? Result.Success : Result.Failure;
    }
}

extern alias rhinocommon;

using Result = rhinocommon::Rhino.Commands.Result;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

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

        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("McpDevSmoke requires a saved active document.");
            return Result.Failure;
        }

        RhinoApp.WriteLine("Running layer-management-smoke-test on the active saved document.");
        bool success = McpRhinoPlugin.Instance.RunDeveloperCommand("layer-management-smoke-test", doc.Path);
        return success ? Result.Success : Result.Failure;
    }
}

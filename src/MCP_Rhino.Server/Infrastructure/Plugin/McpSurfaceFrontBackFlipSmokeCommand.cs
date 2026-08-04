extern alias rhinocommon;

using Result = rhinocommon::Rhino.Commands.Result;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

public sealed class McpSurfaceFrontBackFlipSmokeCommand : RhinoCommand
{
    public override string EnglishName => "McpSurfaceFrontBackFlipSmoke";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (McpRhinoPlugin.Instance is null)
        {
            RhinoApp.WriteLine("MCP_Rhino plugin instance is not available.");
            return Result.Failure;
        }

        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("McpSurfaceFrontBackFlipSmoke requires a saved active document.");
            return Result.Failure;
        }

        RhinoApp.WriteLine("Running surface-front-back-flip-smoke-test on the active saved document.");
        bool success = McpRhinoPlugin.Instance.RunDeveloperCommand("surface-front-back-flip-smoke-test", doc.Path);
        return success ? Result.Success : Result.Failure;
    }
}

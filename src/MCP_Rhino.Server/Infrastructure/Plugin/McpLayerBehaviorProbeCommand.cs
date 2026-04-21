extern alias rhinocommon;

using Result = rhinocommon::Rhino.Commands.Result;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

public sealed class McpLayerBehaviorProbeCommand : RhinoCommand
{
    public override string EnglishName => "McpLayerBehaviorProbe";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (McpRhinoPlugin.Instance is null)
        {
            RhinoApp.WriteLine("MCP_Rhino plugin instance is not available.");
            return Result.Failure;
        }

        RhinoApp.WriteLine("Running layer-behavior-probe. This will create throwaway layers and points on the active doc; use Ctrl+Z afterwards to unwind.");
        bool success = McpRhinoPlugin.Instance.RunDeveloperCommand("layer-behavior-probe");
        return success ? Result.Success : Result.Failure;
    }
}

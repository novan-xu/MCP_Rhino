extern alias rhinocommon;

using Result = rhinocommon::Rhino.Commands.Result;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

public sealed class McpChatCommand : RhinoCommand
{
    public override string EnglishName => "Mcpchat";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (McpRhinoPlugin.Instance is null)
        {
            RhinoApp.WriteLine("MCP_Rhino plugin instance is not available.");
            return Result.Failure;
        }

        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("Mcpchat requires a saved active document.");
            return Result.Failure;
        }

        bool shown = McpRhinoPlugin.Instance.TryShowChatPanel(doc);
        if (!shown)
        {
            RhinoApp.WriteLine("MCP_Rhino chat UI is not available.");
            return Result.Failure;
        }

        RhinoApp.WriteLine("MCP_Rhino chat UI opened.");
        return Result.Success;
    }
}

extern alias rhinocommon;

using MCP_Rhino.Server.Infrastructure.Plugin;
using Result = rhinocommon::Rhino.Commands.Result;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace MCP_Rhino.Server.Infrastructure.CLI;

// Rhino command `_McpLiveDocumentSmoke` — entry point for running the live
// document smoke unit inside a running Rhino instance. Deleting the containing
// temp folder removes this command and the matching CLI handler in one step.
public sealed class McpLiveDocumentSmokeCommand : RhinoCommand
{
    public override string EnglishName => "McpLiveDocumentSmoke";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (McpRhinoPlugin.Instance is null)
        {
            RhinoApp.WriteLine("MCP_Rhino plugin instance is not available.");
            return Result.Failure;
        }

        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("McpLiveDocumentSmoke requires a saved active document — please save the file first.");
            return Result.Failure;
        }

        RhinoApp.WriteLine("Running live-document smoke test. Temporary layers prefixed '__CodexLiveSmoke_' will be created and purged automatically.");
        bool success = McpRhinoPlugin.Instance.RunDeveloperCommand("live-document-smoke-test", doc.Path);
        return success ? Result.Success : Result.Failure;
    }
}

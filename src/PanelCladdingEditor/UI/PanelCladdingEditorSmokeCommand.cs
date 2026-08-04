extern alias rhinocommon;

using System.Runtime.InteropServices;
using Result = rhinocommon::Rhino.Commands.Result;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace PanelCladdingEditor.UI;

[Guid("E5F1E84E-0F8B-4C2D-92D7-33D2651A2AF6")]
public sealed class PanelCladdingEditorSmokeCommand : RhinoCommand
{
    public override string EnglishName => "PanelCladdingEditorSmoke";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var assembly = typeof(PanelCladdingEditorSmokeCommand).Assembly;
        string[] forbidden =
        {
            "MCP_Rhino", "ModelContextProtocol", "Microsoft.Extensions.Hosting",
            "MCP_Rhino.Router", "MCP_Rhino.Transport"
        };
        string[] references = assembly.GetReferencedAssemblies()
            .Select(item => item.Name ?? string.Empty)
            .ToArray();
        string? violation = references.FirstOrDefault(reference =>
            forbidden.Any(token => reference.Contains(token, StringComparison.OrdinalIgnoreCase)));
        if (violation is not null)
        {
            RhinoApp.WriteLine($"[FAIL] Standalone dependency audit found: {violation}");
            return Result.Failure;
        }

        if (assembly.GetType("PanelCladdingEditor.UI.PanelCladdingEditorCommand") is null)
        {
            RhinoApp.WriteLine("[FAIL] PanelCladdingEditor command is missing.");
            return Result.Failure;
        }

        RhinoApp.WriteLine("[OK] PanelCladdingEditor is a standalone Rhino plug-in.");
        RhinoApp.WriteLine("[OK] No MCP_Rhino, Router, Transport, or MCP SDK assembly reference.");
        return Result.Success;
    }
}

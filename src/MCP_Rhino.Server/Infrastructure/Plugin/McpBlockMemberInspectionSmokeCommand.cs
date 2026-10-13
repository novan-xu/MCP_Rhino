extern alias rhinocommon;

using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using Result = rhinocommon::Rhino.Commands.Result;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

public sealed class McpBlockMemberInspectionSmokeCommand : RhinoCommand
{
    public override string EnglishName => "McpBlockMemberInspectionSmoke";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        => McpRhinoPlugin.Instance is not null && !string.IsNullOrWhiteSpace(doc.Path)
            && McpRhinoPlugin.Instance.RunDeveloperCommand("block-member-inspection-smoke-test", doc.Path)
            ? Result.Success : Result.Failure;
}

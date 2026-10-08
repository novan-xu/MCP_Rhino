extern alias rhinocommon;

using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using GetObject = rhinocommon::Rhino.Input.Custom.GetObject;
using GetString = rhinocommon::Rhino.Input.Custom.GetString;
using GetResult = rhinocommon::Rhino.Input.GetResult;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using Result = rhinocommon::Rhino.Commands.Result;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace PanelCladdingEditor.UI;

[System.Runtime.InteropServices.Guid("0B1D8536-AB91-48BE-B2B6-7C6AFEC72199")]
public sealed class PanelCladdingPidCommand : RhinoCommand
{
    public override string EnglishName => "PCpid";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("Save the Rhino document before running PCpid.");
            return Result.Failure;
        }
        string project;
        while (true)
        {
            using var input = new GetString();
            input.SetCommandPrompt("Enter the three-letter project code");
            input.AcceptNothing(false);
            if (input.Get() != GetResult.String) return input.CommandResult();
            project = input.StringResult().Trim().ToUpperInvariant();
            if (PanelCladdingPidPlanningService.IsProjectCode(project)) break;
            RhinoApp.WriteLine("The project code must contain exactly three letters, for example BKT.");
        }
        ILivePanelCladdingPidService service = new LivePanelCladdingPidService();
        var context = service.GetPanelObjectIds(doc.RuntimeSerialNumber);
        if (!context.Success || context.Data is null)
        {
            RhinoApp.WriteLine(context.Message);
            return Result.Failure;
        }
        var contextIds = context.Data.ToHashSet();
        using var panels = new GetObject();
        panels.SetCommandPrompt("Select panel surfaces to update under 01_CW Panels::Surfaces-PNL");
        panels.GeometryFilter = ObjectType.Surface | ObjectType.Brep;
        panels.SubObjectSelect = false;
        panels.GroupSelect = true;
        panels.EnablePreSelect(true, true);
        panels.SetCustomGeometryFilter((obj, _, _) => contextIds.Contains(obj.Id));
        if (panels.GetMultiple(1, 0) != GetResult.Object) return panels.CommandResult();
        var ids = Enumerable.Range(0, panels.ObjectCount).Select(i => panels.Object(i).ObjectId).Distinct().ToArray();
        RhinoApp.WriteLine($"PCpid uses all {contextIds.Count} panels in the panel-layer subtree for numbering; only selected panels will be changed.");
        var northResult = SelectReference(context.Data, "Select a panel in the panel-layer subtree on the north facade", out Guid north);
        if (northResult != Result.Success) return northResult;
        var floorResult = SelectReference(context.Data, "Select a panel in the panel-layer subtree on the first floor (level 01)", out Guid firstFloor);
        if (floorResult != Result.Success) return floorResult;

        var response = service.Assign(doc.RuntimeSerialNumber, new(project, ids, north, firstFloor));
        if (!response.Success || response.Data is null)
        {
            RhinoApp.WriteLine(response.Message);
            return Result.Failure;
        }
        var result = response.Data;
        RhinoApp.WriteLine($"PCpid analyzed {result.Plan.ContextPanelCount} panels; assigned {result.Plan.Panels.Count} selected panel IDs; {result.UpdatedPanelCount} panel(s) changed.");
        RhinoApp.WriteLine($"Standardized point order on {result.ReorderedPanelCount} selected surface(s).");
        foreach (string warning in result.PointOrderWarnings) RhinoApp.WriteLine("Point order skipped: " + warning);
        foreach (var group in result.Plan.Panels.GroupBy(p => p.Elevation))
            RhinoApp.WriteLine($"{group.Key}: {group.Count()} panels, {group.Select(p => p.Level).Distinct().Count()} levels, {group.Select(p => p.Bay).Distinct().Count()} bays.");
        return Result.Success;
    }

    private static Result SelectReference(IReadOnlyList<Guid> ids, string prompt, out Guid id)
    {
        using var getter = new GetObject();
        var selected = ids.ToHashSet();
        getter.SetCommandPrompt(prompt);
        getter.GeometryFilter = ObjectType.Surface | ObjectType.Brep;
        getter.SubObjectSelect = false;
        getter.GroupSelect = false;
        getter.EnablePreSelect(false, true);
        getter.SetCustomGeometryFilter((obj, _, _) => selected.Contains(obj.Id));
        var result = getter.Get();
        id = result == GetResult.Object ? getter.Object(0).ObjectId : Guid.Empty;
        return result == GetResult.Object ? Result.Success : getter.CommandResult();
    }
}

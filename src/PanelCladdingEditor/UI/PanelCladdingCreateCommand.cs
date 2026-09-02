extern alias rhinocommon;

using System;
using System.Collections.Generic;
using System.Linq;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using RhinoBrep = rhinocommon::Rhino.Geometry.Brep;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoCurve = rhinocommon::Rhino.Geometry.Curve;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoGetObject = rhinocommon::Rhino.Input.Custom.GetObject;
using RhinoGetResult = rhinocommon::Rhino.Input.GetResult;
using RhinoObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using RhinoResult = rhinocommon::Rhino.Commands.Result;
using RhinoRunMode = rhinocommon::Rhino.Commands.RunMode;
using RhinoApp = rhinocommon::Rhino.RhinoApp;

namespace PanelCladdingEditor.UI;

[System.Runtime.InteropServices.Guid("9B4D3E0C-7F25-4D71-A7EE-8BDA71580459")]
public sealed class PanelCladdingCreateCommand : RhinoCommand
{
    public override string EnglishName => "PCCreate";

    protected override RhinoResult RunCommand(RhinoDoc doc, RhinoRunMode mode)
    {
        if (doc == null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PCCreate requires the active Rhino document to be saved first.");
            return RhinoResult.Failure;
        }

        var panelIds = SelectPanels();
        if (panelIds == null)
        {
            RhinoApp.WriteLine("PCCreate cancelled before any changes were made.");
            return RhinoResult.Cancel;
        }

        var guideCurveIds = SelectGuideCurves();
        if (guideCurveIds == null)
        {
            RhinoApp.WriteLine("PCCreate cancelled before any changes were made.");
            return RhinoResult.Cancel;
        }

        var keyService = new PanelCladdingKeyService();
        var planningService = new PanelCladdingCreatePlanningService(keyService);
        ILivePanelCladdingCreateService createService = new LivePanelCladdingCreateService(planningService);
        var response = createService.Create(doc.Path, panelIds, guideCurveIds);
        if (!response.Success || response.Data == null)
        {
            RhinoApp.WriteLine(string.IsNullOrWhiteSpace(response.Message)
                ? "PCCreate failed."
                : response.Message);
            return RhinoResult.Failure;
        }

        foreach (var warning in response.Data.Warnings)
        {
            RhinoApp.WriteLine($"PCCreate warning: {warning}");
        }

        RhinoApp.WriteLine(
            $"PCCreate updated {response.Data.UpdatedPanelIds.Count} of {response.Data.SelectedPanelIds.Count} panel(s), " +
            $"created {response.Data.HorizontalOffsetCount} H offset(s), {response.Data.VerticalOffsetCount} V offset(s), " +
            $"and initialized {response.Data.CellCount} cell(s).");
        return RhinoResult.Success;
    }

    private static IReadOnlyList<Guid>? SelectPanels()
    {
        using var getter = new RhinoGetObject();
        getter.SetCommandPrompt("Select panel surfaces to create cladding grid attributes");
        getter.GeometryFilter = RhinoObjectType.Brep | RhinoObjectType.Surface;
        getter.GroupSelect = true;
        getter.SubObjectSelect = false;
        getter.EnablePreSelect(true, true);
        getter.EnablePostSelect(true);
        getter.GetMultiple(1, 0);
        if (getter.CommandResult() != RhinoResult.Success || getter.Result() != RhinoGetResult.Object)
        {
            return null;
        }

        return Enumerable.Range(0, getter.ObjectCount)
            .Select(index => getter.Object(index).Object())
            .Where(rhinoObject => rhinoObject?.Geometry is RhinoBrep)
            .Select(rhinoObject => rhinoObject!.Id)
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyList<Guid>? SelectGuideCurves()
    {
        using var getter = new RhinoGetObject();
        getter.SetCommandPrompt("Select curves to create panel H and V offsets");
        getter.GeometryFilter = RhinoObjectType.Curve;
        getter.GroupSelect = true;
        getter.SubObjectSelect = false;
        getter.EnablePreSelect(true, true);
        getter.EnablePostSelect(true);
        getter.GetMultiple(1, 0);
        if (getter.CommandResult() != RhinoResult.Success || getter.Result() != RhinoGetResult.Object)
        {
            return null;
        }

        return Enumerable.Range(0, getter.ObjectCount)
            .Select(index => getter.Object(index).Object())
            .Where(rhinoObject => rhinoObject?.Geometry is RhinoCurve)
            .Select(rhinoObject => rhinoObject!.Id)
            .Distinct()
            .ToArray();
    }
}

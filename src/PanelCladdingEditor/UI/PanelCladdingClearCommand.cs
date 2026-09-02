extern alias rhinocommon;

using System.Runtime.InteropServices;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using GetObject = rhinocommon::Rhino.Input.Custom.GetObject;
using GetResult = rhinocommon::Rhino.Input.GetResult;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using Result = rhinocommon::Rhino.Commands.Result;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace PanelCladdingEditor.UI;

[Guid("1BE39D1F-FE6E-4E82-8A2C-00E91485B71C")]
public sealed class PanelCladdingClearCommand : RhinoCommand
{
    public override string EnglishName => "PCClear";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PCClear requires a saved active document.");
            return Result.Failure;
        }

        using var getter = new GetObject();
        getter.SetCommandPrompt("Select panel Breps to clear cladding assignments");
        getter.EnablePreSelect(enable: true, ignoreUnacceptablePreselectedObjects: true);
        getter.GeometryFilter = ObjectType.Brep;
        getter.GroupSelect = true;
        getter.SubObjectSelect = false;
        GetResult selection = getter.GetMultiple(minimumNumber: 1, maximumNumber: 0);
        if (selection != GetResult.Object)
        {
            RhinoApp.WriteLine("Panel cladding clear selection cancelled.");
            return selection == GetResult.Cancel ? Result.Cancel : Result.Failure;
        }

        Guid[] objectIds = Enumerable.Range(0, getter.ObjectCount)
            .Select(index => getter.Object(index))
            .Where(reference => reference?.Object()?.Geometry is Brep)
            .Select(reference => reference!.ObjectId)
            .Distinct()
            .ToArray();
        if (objectIds.Length == 0)
        {
            RhinoApp.WriteLine("No panel Breps were selected.");
            return Result.Failure;
        }

        var keys = new PanelCladdingKeyService();
        var planning = new PanelCladdingClearPlanningService(keys);
        ILivePanelCladdingClearService clear = new LivePanelCladdingClearService(planning);
        OperationResponse<PanelCladdingClearResult> response = clear.Clear(doc.Path, objectIds);
        if (!response.Success || response.Data is null)
        {
            RhinoApp.WriteLine(response.Message);
            return Result.Failure;
        }

        RhinoApp.WriteLine(
            $"PCClear removed {response.Data.RemovedKeyCount} cladding key(s) " +
            $"from {response.Data.UpdatedObjectIds.Count} of {response.Data.SelectedObjectIds.Count} selected panel(s).");
        return Result.Success;
    }
}

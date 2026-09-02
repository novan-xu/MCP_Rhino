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
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace PanelCladdingEditor.UI;

[Guid("A8426A74-3F42-49CE-8B52-A3797942D30E")]
public sealed class PanelCladdingSpawnCrvCommand : RhinoCommand
{
    public override string EnglishName => "PCSpawnCrv";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PCSpawnCrv requires a saved active document.");
            return Result.Failure;
        }
        RhinoObject[] selected = doc.Objects.GetSelectedObjects(false, false)
            .Where(item => item.Geometry is Brep).ToArray();
        Guid[] objectIds;
        if (selected.Length > 0)
        {
            objectIds = selected.Select(item => item.Id).Distinct().ToArray();
        }
        else
        {
            using var getter = new GetObject();
            getter.SetCommandPrompt("Select panel Breps to spawn extrusion curves");
            getter.EnablePreSelect(false, true);
            getter.GeometryFilter = ObjectType.Brep;
            getter.GroupSelect = true;
            getter.SubObjectSelect = false;
            GetResult result = getter.GetMultiple(1, 0);
            if (result != GetResult.Object)
            {
                return result == GetResult.Cancel ? Result.Cancel : Result.Failure;
            }
            objectIds = Enumerable.Range(0, getter.ObjectCount)
                .Select(index => getter.Object(index))
                .Where(reference => reference?.Object()?.Geometry is Brep)
                .Select(reference => reference!.ObjectId)
                .Distinct().ToArray();
        }
        if (objectIds.Length == 0)
        {
            RhinoApp.WriteLine("No panel Breps were selected.");
            return Result.Failure;
        }

        var keys = new PanelCladdingKeyService();
        ILivePanelCladdingRepository layouts = new LivePanelCladdingRepository(
            keys, new PanelAxonometricProjectionService());
        ILivePanelCladdingSpawnService spawn = new LivePanelCladdingSpawnService(
            layouts, keys, new PanelCladdingSpawnPlanningService(keys));
        OperationResponse<PanelCladdingSpawnResult> response = spawn.Spawn(
            doc.Path, objectIds, PanelCladdingObjectScope.Curves);
        if (!response.Success || response.Data is null)
        {
            RhinoApp.WriteLine(response.Message);
            return Result.Failure;
        }
        RhinoApp.WriteLine(
            $"PCSpawnCrv created {response.Data.CreatedCurveIds.Count} extrusion curve(s) " +
            $"from {response.Data.SourcePanelIds.Count} panel(s): {string.Join(", ", response.Data.Cids)}");
        return Result.Success;
    }
}

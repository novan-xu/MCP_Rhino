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

[Guid("38EEFE95-EDFC-4071-AB39-F52D9F5EBC82")]
public sealed class PanelCladdingSpawnCommand : RhinoCommand
{
    public override string EnglishName => "PanelCladdingSpawn";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PanelCladdingSpawn requires a saved active document.");
            return Result.Failure;
        }

        TargetSelection selection = ResolveTarget(doc);
        if (!selection.Success)
        {
            RhinoApp.WriteLine(selection.Message);
            return selection.Cancelled ? Result.Cancel : Result.Failure;
        }

        var keys = new PanelCladdingKeyService();
        var projection = new PanelAxonometricProjectionService();
        ILivePanelCladdingRepository layoutRepository = new LivePanelCladdingRepository(keys, projection);
        var planner = new PanelCladdingSpawnPlanningService(keys);
        ILivePanelCladdingSpawnService spawn = new LivePanelCladdingSpawnService(
            layoutRepository,
            keys,
            planner);
        OperationResponse<PanelCladdingSpawnResult> response = spawn.Spawn(doc.Path, selection.ObjectIds);
        if (!response.Success || response.Data is null)
        {
            RhinoApp.WriteLine(response.Message);
            return Result.Failure;
        }

        RhinoApp.WriteLine(
            $"PanelCladdingSpawn created {response.Data.CreatedObjectIds.Count} material surface(s) " +
            $"from {response.Data.SourcePanelIds.Count} panel(s): " +
            string.Join(", ", response.Data.Cids));
        return Result.Success;
    }

    private static TargetSelection ResolveTarget(RhinoDoc doc)
    {
        RhinoObject[] selected = doc.Objects.GetSelectedObjects(includeLights: false, includeGrips: false)
            .Where(item => item.Geometry is Brep)
            .ToArray();
        if (selected.Length > 0)
        {
            return TargetSelection.Ok(selected.Select(item => item.Id));
        }

        using var getter = new GetObject();
        getter.SetCommandPrompt("Select panel Breps to spawn cladding surfaces");
        getter.EnablePreSelect(enable: false, ignoreUnacceptablePreselectedObjects: true);
        getter.GeometryFilter = ObjectType.Brep;
        getter.GroupSelect = true;
        getter.SubObjectSelect = false;
        GetResult result = getter.GetMultiple(minimumNumber: 1, maximumNumber: 0);
        if (result != GetResult.Object)
        {
            return new TargetSelection(false, result == GetResult.Cancel, Array.Empty<Guid>(), "Panel selection cancelled.");
        }
        Guid[] objectIds = Enumerable.Range(0, getter.ObjectCount)
            .Select(index => getter.Object(index))
            .Where(reference => reference?.Object()?.Geometry is Brep)
            .Select(reference => reference!.ObjectId)
            .Distinct()
            .ToArray();
        if (objectIds.Length == 0)
        {
            return new TargetSelection(false, false, Array.Empty<Guid>(), "No Brep panels were selected.");
        }
        return TargetSelection.Ok(objectIds);
    }

    private readonly record struct TargetSelection(
        bool Success,
        bool Cancelled,
        IReadOnlyList<Guid> ObjectIds,
        string Message)
    {
        public static TargetSelection Ok(IEnumerable<Guid> objectIds) =>
            new(true, false, objectIds.Distinct().ToArray(), string.Empty);
    }
}

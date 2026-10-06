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

[Guid("F2C41D75-2F88-4CB4-9D1C-CF6BD1D6208A")]
public sealed class PanelCladdingUpdateCommand : RhinoCommand
{
    public override string EnglishName => "PCUpdate";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PCUpdate requires a saved active document.");
            return Result.Failure;
        }

        TargetSelection selection = ResolveTarget(doc);
        if (!selection.Success)
        {
            RhinoApp.WriteLine(selection.Message);
            return selection.Cancelled ? Result.Cancel : Result.Failure;
        }

        OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>> materialColors =
            PanelCladdingSpawnSrfCommand.ResolveMaterialColors(doc);
        if (!materialColors.Success || materialColors.Data is null)
        {
            RhinoApp.WriteLine(materialColors.Message);
            return Result.Failure;
        }

        var keys = new PanelCladdingKeyService();
        var projection = new PanelAxonometricProjectionService();
        ILivePanelCladdingRepository layouts = new LivePanelCladdingRepository(keys, projection);
        var spawnPlanning = new PanelCladdingSpawnPlanningService(keys, materialColors.Data);
        ILivePanelCladdingUpdateService update = new LivePanelCladdingUpdateService(
            layouts,
            keys,
            spawnPlanning,
            new PanelCladdingDependencyReconciliationService());
        OperationResponse<PanelCladdingUpdateResult> response = update.Update(
            doc.Path,
            selection.ObjectIds);
        if (!response.Success || response.Data is null)
        {
            RhinoApp.WriteLine(response.Message);
            return Result.Failure;
        }

        PanelCladdingUpdateResult result = response.Data;
        RhinoApp.WriteLine(
            $"PCUpdate processed {result.SourcePanelIds.Count} panel(s): " +
            $"created {result.CreatedSurfaceIds.Count} surface(s) and {result.CreatedCurveIds.Count} curve(s), " +
            $"updated {result.UpdatedSurfaceIds.Count} surface(s) and {result.UpdatedCurveIds.Count} curve(s), " +
            $"deleted {result.DeletedObjectIds.Count} obsolete or duplicate object(s).");
        if (result.DuplicateCidGroups.Count > 0)
        {
            int skippedCount = result.DuplicateCidGroups.Sum(group => group.PanelObjectIds.Count);
            RhinoApp.WriteLine($"Skipped {skippedCount} panel(s) with duplicate CIDs; these panels remain selected for inspection.");
            foreach (PanelCladdingDuplicateCidGroup group in result.DuplicateCidGroups)
            {
                RhinoApp.WriteLine($"Duplicate CID {group.Cid}: {group.PanelObjectIds.Count} panels.");
            }
        }
        if (result.PreservedAmbiguousDependencyIds.Count > 0)
        {
            RhinoApp.WriteLine($"Left {result.PreservedAmbiguousDependencyIds.Count} legacy dependencies unchanged because their CID does not identify a single selected panel.");
        }
        return Result.Success;
    }

    private static TargetSelection ResolveTarget(RhinoDoc document)
    {
        RhinoObject[] selected = document.Objects.GetSelectedObjects(
                includeLights: false,
                includeGrips: false)
            .Where(item => item.Geometry is Brep)
            .ToArray();
        if (selected.Length > 0)
        {
            return TargetSelection.Ok(selected.Select(item => item.Id));
        }

        using var getter = new GetObject();
        getter.SetCommandPrompt("Select panel Breps to update all managed dependencies");
        getter.EnablePreSelect(enable: false, ignoreUnacceptablePreselectedObjects: true);
        getter.GeometryFilter = ObjectType.Brep;
        getter.GroupSelect = true;
        getter.SubObjectSelect = false;
        GetResult selection = getter.GetMultiple(minimumNumber: 1, maximumNumber: 0);
        if (selection != GetResult.Object)
        {
            return new TargetSelection(
                false,
                selection == GetResult.Cancel,
                Array.Empty<Guid>(),
                "Panel update selection cancelled.");
        }

        Guid[] objectIds = Enumerable.Range(0, getter.ObjectCount)
            .Select(index => getter.Object(index))
            .Where(reference => reference?.Object()?.Geometry is Brep)
            .Select(reference => reference!.ObjectId)
            .Distinct()
            .ToArray();
        return objectIds.Length == 0
            ? new TargetSelection(false, false, Array.Empty<Guid>(), "No Brep panels were selected.")
            : TargetSelection.Ok(objectIds);
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

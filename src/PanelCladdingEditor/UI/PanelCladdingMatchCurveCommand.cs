extern alias rhinocommon;

using System.Runtime.InteropServices;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live;
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

[Guid("4A0E749F-021B-41E5-9A43-D0D5A0F043A7")]
public sealed class PanelCladdingMatchCurveCommand : RhinoCommand
{
    public override string EnglishName => "PCMatchCrv";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PCMatchCrv requires a saved active document.");
            return Result.Failure;
        }

        TargetSelection targets = SelectTargets();
        if (!targets.Success)
        {
            RhinoApp.WriteLine(targets.Message);
            return targets.Cancelled ? Result.Cancel : Result.Failure;
        }
        SourceSelection source = SelectSource(targets.ObjectIds);
        if (!source.Success)
        {
            RhinoApp.WriteLine(source.Message);
            return source.Cancelled ? Result.Cancel : Result.Failure;
        }

        var keys = new PanelCladdingKeyService();
        var projection = new PanelAxonometricProjectionService();
        ILivePanelCladdingRepository repository = new LivePanelCladdingRepository(keys, projection);
        IPanelCladdingMatchPlanningService planner = new PanelCladdingCurveMatchPlanningService(keys);
        ILivePanelCladdingMatchService match = new LivePanelCladdingMatchService(repository, planner);
        OperationResponse<PanelCladdingMatchResult> response = match.Match(
            doc.Path,
            source.ObjectId,
            targets.ObjectIds);
        if (!response.Success || response.Data is null)
        {
            RhinoApp.WriteLine(response.Message);
            return Result.Failure;
        }

        RhinoApp.WriteLine(
            $"PCMatchCrv copied the segment, merge, and hide masks from {response.Data.SourceObjectId:D} " +
            $"to {response.Data.UpdatedTargetIds.Count} panel(s).");
        return Result.Success;
    }

    private static TargetSelection SelectTargets()
    {
        using var getter = new GetObject();
        getter.SetCommandPrompt("Select target panel Breps for curve topology matching");
        getter.EnablePreSelect(enable: true, ignoreUnacceptablePreselectedObjects: true);
        getter.GeometryFilter = ObjectType.Brep;
        getter.GroupSelect = true;
        getter.SubObjectSelect = false;
        GetResult result = getter.GetMultiple(minimumNumber: 1, maximumNumber: 0);
        if (result != GetResult.Object)
        {
            return new TargetSelection(
                false,
                result == GetResult.Cancel,
                Array.Empty<Guid>(),
                "Target panel selection cancelled.");
        }

        Guid[] objectIds = Enumerable.Range(0, getter.ObjectCount)
            .Select(index => getter.Object(index))
            .Where(reference => reference?.Object()?.Geometry is Brep)
            .Select(reference => reference!.ObjectId)
            .Distinct()
            .ToArray();
        return objectIds.Length == 0
            ? new TargetSelection(false, false, objectIds, "No target Brep panels were selected.")
            : new TargetSelection(true, false, objectIds, string.Empty);
    }

    private static SourceSelection SelectSource(IReadOnlyList<Guid> targetObjectIds)
    {
        var targetIds = targetObjectIds.ToHashSet();
        using var getter = new GetObject();
        getter.SetCommandPrompt("Select one source panel Brep for curve topology matching");
        getter.EnablePreSelect(enable: false, ignoreUnacceptablePreselectedObjects: true);
        getter.GeometryFilter = ObjectType.Brep;
        getter.SubObjectSelect = false;
        GetResult result = getter.Get();
        if (result != GetResult.Object)
        {
            return new SourceSelection(
                false,
                result == GetResult.Cancel,
                Guid.Empty,
                "Source panel selection cancelled.");
        }

        var reference = getter.Object(0);
        return reference?.Object()?.Geometry is Brep && !targetIds.Contains(reference.ObjectId)
            ? new SourceSelection(true, false, reference.ObjectId, string.Empty)
            : new SourceSelection(
                false,
                false,
                Guid.Empty,
                "Select one source Brep that is not a target panel.");
    }

    private readonly record struct TargetSelection(
        bool Success,
        bool Cancelled,
        IReadOnlyList<Guid> ObjectIds,
        string Message);

    private readonly record struct SourceSelection(
        bool Success,
        bool Cancelled,
        Guid ObjectId,
        string Message);
}

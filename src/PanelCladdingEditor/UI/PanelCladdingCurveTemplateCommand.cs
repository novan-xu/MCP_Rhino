extern alias rhinocommon;

using System.Runtime.InteropServices;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using GetObject = rhinocommon::Rhino.Input.Custom.GetObject;
using GetOption = rhinocommon::Rhino.Input.Custom.GetOption;
using GetResult = rhinocommon::Rhino.Input.GetResult;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using Result = rhinocommon::Rhino.Commands.Result;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace PanelCladdingEditor.UI;

[Guid("6D026D22-A99A-4EF4-83C0-6140427248D2")]
public sealed class PanelCladdingCurveTemplateCommand : RhinoCommand
{
    public override string EnglishName => "PCCrvTemplate";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PCCrvTemplate requires a saved active document.");
            return Result.Failure;
        }

        IReadOnlyList<Guid>? panelIds = SelectPanels();
        if (panelIds is null)
        {
            RhinoApp.WriteLine("PCCrvTemplate panel selection cancelled before any changes were made.");
            return Result.Cancel;
        }

        PanelCladdingCurveTemplatePriority? priority = SelectPriority();
        if (priority is null)
        {
            RhinoApp.WriteLine("PCCrvTemplate priority selection cancelled before any changes were made.");
            return Result.Cancel;
        }

        var keys = new PanelCladdingKeyService();
        var projection = new PanelAxonometricProjectionService();
        var repository = new LivePanelCladdingRepository(keys, projection);
        var planning = new PanelCladdingCurveTemplatePlanningService(keys);
        ILivePanelCladdingCurveTemplateService service =
            new LivePanelCladdingCurveTemplateService(repository, planning);
        OperationResponse<PanelCladdingCurveTemplateResult> response =
            service.Apply(doc.Path, panelIds, priority.Value);
        if (!response.Success || response.Data is null)
        {
            RhinoApp.WriteLine(string.IsNullOrWhiteSpace(response.Message)
                ? "PCCrvTemplate failed."
                : response.Message);
            return Result.Failure;
        }

        string label = response.Data.Priority == PanelCladdingCurveTemplatePriority.Horizontal
            ? "H priority"
            : "V priority";
        RhinoApp.WriteLine(
            $"PCCrvTemplate applied {label} to {response.Data.UpdatedPanelIds.Count} of " +
            $"{response.Data.SelectedPanelIds.Count} selected panel(s), producing " +
            $"{response.Data.MergeRunCount} merge run(s).");
        if (response.Data.SkippedPanelIds.Count > 0)
        {
            RhinoApp.WriteLine(
                $"Skipped {response.Data.SkippedPanelIds.Count} already-configured panel(s).");
        }
        return Result.Success;
    }

    private static IReadOnlyList<Guid>? SelectPanels()
    {
        using var getter = new GetObject();
        getter.SetCommandPrompt("Select panel Breps for curve template");
        getter.EnablePreSelect(enable: true, ignoreUnacceptablePreselectedObjects: true);
        getter.EnablePostSelect(true);
        getter.GeometryFilter = ObjectType.Brep | ObjectType.Surface;
        getter.GroupSelect = true;
        getter.SubObjectSelect = false;
        GetResult selection = getter.GetMultiple(minimumNumber: 1, maximumNumber: 0);
        if (selection != GetResult.Object)
        {
            return null;
        }

        Guid[] objectIds = Enumerable.Range(0, getter.ObjectCount)
            .Select(index => getter.Object(index))
            .Where(reference => reference?.Object()?.Geometry is Brep)
            .Select(reference => reference!.ObjectId)
            .Distinct()
            .ToArray();
        return objectIds.Length == 0 ? null : objectIds;
    }

    private static PanelCladdingCurveTemplatePriority? SelectPriority()
    {
        using var getter = new GetOption();
        getter.SetCommandPrompt("Choose curve template priority");
        int horizontal = getter.AddOption("HPriority");
        int vertical = getter.AddOption("VPriority");
        GetResult result = getter.Get();
        if (result != GetResult.Option)
        {
            return null;
        }
        if (getter.OptionIndex() == horizontal)
        {
            return PanelCladdingCurveTemplatePriority.Horizontal;
        }
        return getter.OptionIndex() == vertical
            ? PanelCladdingCurveTemplatePriority.Vertical
            : null;
    }
}

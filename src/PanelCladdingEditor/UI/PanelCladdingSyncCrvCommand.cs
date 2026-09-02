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

[Guid("A91D32DD-66DF-45E4-8503-87C56D19D727")]
public sealed class PanelCladdingSyncCrvCommand : RhinoCommand
{
    public override string EnglishName => "PCSyncCrv";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PCSyncCrv requires a saved active document.");
            return Result.Failure;
        }

        using var getter = new GetObject();
        getter.SetCommandPrompt("Select panel Breps to synchronize from associated extrusion curves");
        getter.EnablePreSelect(enable: true, ignoreUnacceptablePreselectedObjects: true);
        getter.GeometryFilter = ObjectType.Brep;
        getter.GroupSelect = true;
        getter.SubObjectSelect = false;
        GetResult selection = getter.GetMultiple(minimumNumber: 1, maximumNumber: 0);
        if (selection != GetResult.Object)
        {
            RhinoApp.WriteLine("Panel cladding curve synchronization selection cancelled.");
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

        string storedWorkbookPath = doc.Strings.GetValue(
            PanelCladdingKeyService.WorkbookPathDocumentKey) ?? string.Empty;
        var keys = new PanelCladdingKeyService();
        var projection = new PanelAxonometricProjectionService();
        ILivePanelCladdingRepository layouts = new LivePanelCladdingRepository(keys, projection);
        ILivePanelCladdingSurfaceSyncRepository live =
            new LivePanelCladdingSurfaceSyncRepository(layouts, keys, projection);
        var planning = new PanelCladdingSurfaceSyncPlanningService(keys);
        var signature = new PanelCladdingTypeSignatureService(keys);
        IPanelCladdingSurfaceSyncService sync = new PanelCladdingSurfaceSyncService(
            live,
            signature,
            planning);
        OperationResponse<PanelCladdingSurfaceSyncResult> response = sync.Sync(
            doc.Path,
            objectIds,
            storedWorkbookPath,
            allowCreateWorkbook: false,
            PanelCladdingObjectScope.Curves);
        if (!response.Success || response.Data is null)
        {
            RhinoApp.WriteLine(response.Message);
            return Result.Failure;
        }

        foreach (PanelCladdingSurfaceSyncIssue issue in response.Data.Issues)
        {
            RhinoApp.WriteLine(issue.Message);
        }
        int processedPanels = response.Data.SelectedPanelIds.Count - response.Data.SkippedPanelIds.Count;
        RhinoApp.WriteLine(
            $"PCSyncCrv processed {processedPanels} panel(s), skipped " +
            $"{response.Data.SkippedPanelIds.Count} panel(s), matched {response.Data.MatchedCurveCount} curve(s), " +
            $"refreshed {response.Data.RefreshedCurveIds.Count} curve(s), " +
            $"and wrote {response.Data.ChangedPanelIds.Count} panel layout key/value set(s).");
        if (response.Data.SkippedPanelIds.Count > 0)
        {
            RhinoApp.WriteLine("Skipped panels are selected for inspection.");
        }
        return Result.Success;
    }
}

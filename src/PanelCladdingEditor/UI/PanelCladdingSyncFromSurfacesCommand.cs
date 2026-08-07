extern alias rhinocommon;

using System.Runtime.InteropServices;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
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

[Guid("15F873A5-5B00-4232-89DB-78C234E2DBBB")]
public sealed class PanelCladdingSyncFromSurfacesCommand : RhinoCommand
{
    public override string EnglishName => "PanelCladdingSyncFromSurfaces";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PanelCladdingSyncFromSurfaces requires a saved active document.");
            return Result.Failure;
        }

        using var getter = new GetObject();
        getter.SetCommandPrompt("Select panel Breps to synchronize from cladding surface layers");
        getter.EnablePreSelect(enable: true, ignoreUnacceptablePreselectedObjects: true);
        getter.GeometryFilter = ObjectType.Brep;
        getter.GroupSelect = true;
        getter.SubObjectSelect = false;
        GetResult selection = getter.GetMultiple(minimumNumber: 1, maximumNumber: 0);
        if (selection != GetResult.Object)
        {
            RhinoApp.WriteLine("Panel cladding surface synchronization selection cancelled.");
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

        using var pathGetter = new GetString();
        pathGetter.SetCommandPrompt("Type the full path to the cladding typology .xlsx workbook");
        string storedWorkbookPath = doc.Strings.GetValue(
            PanelCladdingKeyService.WorkbookPathDocumentKey) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(storedWorkbookPath))
        {
            pathGetter.SetDefaultString(storedWorkbookPath);
        }
        GetResult pathResult = pathGetter.GetLiteralString();
        if (pathResult != GetResult.String)
        {
            RhinoApp.WriteLine("Typology workbook selection cancelled.");
            return pathResult == GetResult.Cancel ? Result.Cancel : Result.Failure;
        }

        string workbookPath = pathGetter.StringResult().Trim();
        if (string.IsNullOrWhiteSpace(workbookPath))
        {
            RhinoApp.WriteLine("A typology workbook path is required.");
            return Result.Failure;
        }
        bool allowCreate = !File.Exists(workbookPath);
        var keys = new PanelCladdingKeyService();
        var projection = new PanelAxonometricProjectionService();
        ILivePanelCladdingRepository layouts = new LivePanelCladdingRepository(keys, projection);
        ILivePanelCladdingSurfaceSyncRepository live =
            new LivePanelCladdingSurfaceSyncRepository(layouts);
        var planning = new PanelCladdingSurfaceSyncPlanningService(keys);
        var signature = new PanelCladdingTypeSignatureService(keys);
        var renderer = new PanelPreviewRenderer();
        IPanelCladdingWorkbookRepository workbook = new OpenXmlPanelCladdingWorkbookRepository();
        IPanelCladdingSurfaceSyncService sync = new PanelCladdingSurfaceSyncService(
            live,
            workbook,
            renderer,
            signature,
            planning);
        OperationResponse<PanelCladdingSurfaceSyncResult> response = sync.Sync(
            doc.Path,
            objectIds,
            workbookPath,
            allowCreate);
        if (!response.Success || response.Data is null)
        {
            RhinoApp.WriteLine(response.Message);
            return Result.Failure;
        }

        int reusedTypes = response.Data.Types.Count(type => type.ReusedExistingType);
        RhinoApp.WriteLine(
            $"PanelCladdingSyncFromSurfaces matched {response.Data.MatchedSurfaceCount} surface(s), " +
            $"refreshed {response.Data.RefreshedSurfaceIds.Count} surface Cladding value(s), " +
            $"updated {response.Data.ChangedPanelIds.Count} panel(s), and exported " +
            $"{response.Data.Types.Count} type assignment(s) ({reusedTypes} reused).");
        return Result.Success;
    }
}

extern alias rhinocommon;

using System.Runtime.InteropServices;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using GetObject = rhinocommon::Rhino.Input.Custom.GetObject;
using GetResult = rhinocommon::Rhino.Input.GetResult;
using Result = rhinocommon::Rhino.Commands.Result;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace PanelCladdingEditor.UI;

[Guid("A5C10BE1-0E0C-4CBF-BF99-9C389F9A16B4")]
public sealed class PanelCladdingEditorCommand : RhinoCommand
{
    private static PanelCladdingEditorWindow? _window;

    public override string EnglishName => "PanelCladdingEditor";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PanelCladdingEditor requires a saved active document.");
            return Result.Failure;
        }

        TargetSelection selection = ResolveTarget(doc);
        if (!selection.Success)
        {
            RhinoApp.WriteLine(selection.Message);
            return selection.Cancelled ? Result.Cancel : Result.Failure;
        }

        if (_window is null)
        {
            var keys = new PanelCladdingKeyService();
            var projection = new PanelAxonometricProjectionService();
            var liveRepository = new LivePanelCladdingRepository(keys, projection);
            var renderer = new PanelPreviewRenderer();
            var workbook = new OpenXmlPanelCladdingWorkbookRepository();
            var signature = new PanelCladdingTypeSignatureService(keys);
            var save = new PanelCladdingSaveService(liveRepository, workbook, renderer, signature);
            var controller = new PanelCladdingEditorController(liveRepository, save, renderer);
            _window = new PanelCladdingEditorWindow(controller);
            _window.EditorClosed += (_, _) => _window = null;
            _window.Show();
        }

        OperationResponse loaded = _window.LoadPanel(doc.Path, selection.ObjectId);
        if (!loaded.Success)
        {
            RhinoApp.WriteLine(loaded.Message);
            return Result.Failure;
        }
        _window.BringToFront();
        _window.Focus();
        return Result.Success;
    }

    private static TargetSelection ResolveTarget(RhinoDoc doc)
    {
        RhinoObject[] selected = doc.Objects.GetSelectedObjects(includeLights: false, includeGrips: false)
            .Where(item => item.Geometry is Brep)
            .ToArray();
        if (selected.Length == 1)
        {
            return TargetSelection.Ok(selected[0].Id);
        }

        using var getter = new GetObject();
        getter.SetCommandPrompt("Select one panel Brep to edit");
        getter.EnablePreSelect(enable: false, ignoreUnacceptablePreselectedObjects: true);
        getter.SubObjectSelect = false;
        GetResult result = getter.Get();
        if (result != GetResult.Object)
        {
            return new TargetSelection(false, result == GetResult.Cancel, Guid.Empty, "Panel selection cancelled.");
        }
        var objectReference = getter.Object(0);
        if (objectReference?.Object()?.Geometry is not Brep)
        {
            return new TargetSelection(false, false, Guid.Empty, "Selected object is not a Brep panel.");
        }
        return TargetSelection.Ok(objectReference.ObjectId);
    }

    private readonly record struct TargetSelection(bool Success, bool Cancelled, Guid ObjectId, string Message)
    {
        public static TargetSelection Ok(Guid objectId) => new(true, false, objectId, string.Empty);
    }
}

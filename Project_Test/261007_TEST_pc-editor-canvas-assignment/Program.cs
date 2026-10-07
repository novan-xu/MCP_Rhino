using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace PanelCladdingCanvasAssignmentSmoke;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main()
    {
        var metadata = new Dictionary<string, string>
        {
            ["CW_1.01_PID"] = "PID_N1_03_07",
            ["cw_1.02_cid"] = " CID_N1_03_07-C ",
            ["CW_1.06_UNIT_TYPE"] = "corner_child"
        };
        var repository = new Repository(BuildLayout(metadata));
        var keys = new PanelCladdingKeyService();
        var signature = new PanelCladdingTypeSignatureService(keys);
        var window = new PanelCladdingEditorWindow(new PanelCladdingEditorController(
            repository, new PanelCladdingSaveService(repository, signature), new PanelPreviewRenderer(), signature));
        Require(window.LoadPanel(repository.Layout.DocumentPath, repository.Layout.ObjectId).Success, "Fixture load failed.");
        FrameworkElement root = (FrameworkElement)window.Content;
        Layout(root, 1440, 900);
        Grid floating = Named<Grid>(window, "ExtrusionAssignmentSection");
        Border dropTarget = (Border)floating.Children[0];
        var canvas = Named<PanelCladdingGridCanvas>(window, "PanelGridCanvas");
        Require(ReferenceEquals(floating.Parent, window.FindName("CanvasWorkspace")), "Assignment is not on the canvas.");
        Require(floating.Visibility == Visibility.Collapsed, "Assignment is visible in Cladding view.");
        Require(Named<TextBlock>(window, "PanelIdText").Text == "N1_03_07-C", "Heading did not prefer the shortened stored CID.");
        Require((string)Named<TextBlock>(window, "PanelIdText").ToolTip == "CID_N1_03_07-C", "Full CID tooltip missing.");
        CultureInfo priorCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Invoke(window, "PopulatePanelMetadata");
            Require(Named<TextBlock>(window, "OverallWidthText").Text == "Width  90.12346 in", "Width rounding/precision is incorrect.");
            Require(Named<TextBlock>(window, "OverallHeightText").Text == "Height 180.00000 in", "Height precision is incorrect.");
        }
        finally { CultureInfo.CurrentCulture = priorCulture; }
        TextBlock width = Named<TextBlock>(window, "OverallWidthText");
        TextBlock height = Named<TextBlock>(window, "OverallHeightText");
        Require(height.TranslatePoint(default, root).Y >= width.TranslatePoint(default, root).Y + width.ActualHeight,
            "Width and height overlap instead of occupying separate lines.");

        Invoke(window, "SwitchView", PanelEditorView.Extrusion, false);
        Require(floating.Visibility == Visibility.Collapsed, "Empty extrusion selection shows assignment.");
        Select(canvas, "FRM_0");
        Require(floating.Visibility == Visibility.Visible, "Click selection did not reveal assignment.");
        Require(Named<TextBlock>(window, "SelectedExtrusionNamesText").Text == "FRM_0", "Selected curve name missing.");
        Render(root, 1440, 900, "empty-selection-1440x900.png");

        ObservableCollection<PanelFrameExtrusion> profiles = Field<ObservableCollection<PanelFrameExtrusion>>(window, "_frameExtrusions");
        for (int i = 0; i < 7; i++)
        {
            profiles.Add(new PanelFrameExtrusion
            {
                Code = $"1D-T{i:000}", BaseCode = $"T{i:000}", SourceCode = $"T{i:000}",
                Dimension = PanelFrameProfileDimension.OneDimensional, CalculationValue = 1,
                Description = "Synthetic smoke profile", Thumbnail = Thumbnail(i)
            });
        }
        Invoke(window, "RefreshAvailableExtrusions", new object?[] { null });
        var cards = Named<ItemsControl>(window, "SelectedExtrusionAssignments");
        var catalogue = Named<ItemsControl>(window, "ExtrusionLegend");
        DragEventArgs drag = DragArgs(dropTarget, "1D-T000", DragDrop.PreviewDragOverEvent);
        dropTarget.RaiseEvent(drag);
        Require(drag.Handled && drag.Effects == DragDropEffects.Copy, "Valid profile drag rejected.");
        dropTarget.RaiseEvent(DragArgs(dropTarget, "1D-T000", DragDrop.PreviewDropEvent));
        Require(cards.Items.Count == 1 && ((PanelExtrusionAssignmentCard)cards.Items[0]).Code == "1D-T000", "Profile drop failed.");
        Require(catalogue.Items.Count == 6, "Assigned profile remained in the catalogue.");
        Render(root, 1440, 900, "assigned-1440x900.png");

        TextBox modifier = Descendants<TextBox>(cards).Single();
        modifier.Text = "+4";
        Invoke(window, "ApplyCurveModifier", modifier, false);
        Require(Field<Dictionary<string, double>>(window, "_frameCurveModifiers")["FRM_0"] == 4, "Modifier failed.");
        Layout(root, 1440, 900);
        Descendants<Button>(cards).Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(cards.Items.Count == 0 && catalogue.Items.Count == 7, "Remove card failed.");
        Invoke(window, "UndoLastChange");
        Require(cards.Items.Count == 1, "Undo removal failed.");
        Named<Button>(window, "ClearExtrusionAssignmentsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(cards.Items.Count == 0 && floating.Visibility == Visibility.Visible, "Clear all lost the selection drop target.");
        Invoke(window, "UndoLastChange");
        Require(cards.Items.Count == 1, "Undo clear failed.");

        for (int i = 1; i < 7; i++)
        {
            dropTarget.RaiseEvent(DragArgs(dropTarget, $"1D-T{i:000}", DragDrop.PreviewDropEvent));
            if (i == 2) Render(root, 1440, 900, "two-column-three-profiles-1440x900.png");
        }
        foreach ((int w, int h) in new[] { (1440, 900), (980, 680) })
        {
            Render(root, w, h, $"scrolling-{w}x{h}.png");
            var scroll = Named<ScrollViewer>(window, "ExtrusionAssignmentScroll");
            Require(scroll.ScrollableHeight > 0 && scroll.ViewportHeight > 60, "Long profile list does not scroll.");
            Require(dropTarget.ActualHeight <= floating.ActualHeight + 1, "Assignment exceeds its canvas slot.");
            Point origin = dropTarget.TranslatePoint(default, root);
            Require(origin.Y > height.TranslatePoint(default, root).Y + height.ActualHeight, "Overlay covers size readout.");
            Require(origin.Y + dropTarget.ActualHeight <= h - 70, "Overlay covers bottom actions.");
            TextBox input = Descendants<TextBox>(cards).First();
            Require(input.ActualWidth >= 40, "Modifier input clipped at small size.");
            scroll.ScrollToEnd();
            Layout(root, w, h);
            Require(scroll.VerticalOffset > 0, "Cannot reach the end of assignments.");
            scroll.ScrollToHome();
        }
        Select(canvas, "FRM_1");
        Require(cards.Items.Count == 0 && catalogue.Items.Count == 7, "Changing curves retained stale assignments.");
        Select(canvas, "FRM_0", "FRM_1");
        Require(cards.Items.Count == 7 && Named<TextBlock>(window, "SelectedExtrusionNamesText").Text == "FRM_0, FRM_1",
            "Multi-selection summary or assignments failed.");
        Named<Button>(window, "ClearExtrusionAssignmentsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(cards.Items.Count == 0, "Multi-selection clear failed.");
        // Use the actual topology ID rather than coupling to its string encoding.
        var intermediate = ((IReadOnlyList<PanelExtrusionSegment>)Invoke(window, "CurrentSegments")!).First(s => !s.IsFrame);
        Select(canvas, intermediate.Id);
        Require(floating.Visibility == Visibility.Visible && Named<TextBlock>(window, "SelectedExtrusionNamesText").Text == intermediate.Code,
            "Intermediate curve selection failed.");
        Select(canvas);
        Require(floating.Visibility == Visibility.Collapsed, "Blank selection did not hide assignment.");
        drag = DragArgs(dropTarget, "1D-T000", DragDrop.PreviewDragOverEvent);
        dropTarget.RaiseEvent(drag);
        Require(drag.Effects == DragDropEffects.None, "Drag without a selection was accepted.");
        Select(canvas, "FRM_0");
        Invoke(window, "SwitchView", PanelEditorView.Cladding, false);
        Require(floating.Visibility == Visibility.Collapsed, "View switch did not hide assignment.");
        Invoke(window, "SwitchView", PanelEditorView.Extrusion, false);
        Require(floating.Visibility == Visibility.Visible, "View switch lost valid selection.");
        var escape = new KeyEventArgs(Keyboard.PrimaryDevice, new TestPresentationSource(root), 0, Key.Escape)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        Invoke(window, "OnWindowPreviewKeyDown", window, escape);
        Require(floating.Visibility == Visibility.Collapsed && escape.Handled, "Escape did not dismiss assignment.");
        Require(repository.CommitCount == 0, "Preview edits unexpectedly wrote Rhino attributes.");

        foreach ((string? cid, string? pid, string? role, string expected) in new[]
        {
            ("CID_BKT_N1_03_07-P", "PID_WRONG", "flat", "N1_03_07-P"),
            ((string?)null, "PID_N1_03_07", "corner_parent", "N1_03_07-P"),
            (null, "PID_N1_03_07", "flat", "N1_03_07"),
            (null, null, null, "—")
        })
        {
            metadata.Clear();
            if (cid is not null) metadata["CW_1.02_CID"] = cid;
            if (pid is not null) metadata["CW_1.01_PID"] = pid;
            if (role is not null) metadata["CW_1.06_UNIT_TYPE"] = role;
            Invoke(window, "PopulatePanelMetadata");
            Require(Named<TextBlock>(window, "PanelIdText").Text == expected, $"CID fallback failed: {expected}.");
        }
        Invoke(window, "SetDirty", false);
        Require(window.LoadPanel(repository.Layout.DocumentPath, repository.Layout.ObjectId).Success, "Panel reload failed.");
        Require(floating.Visibility == Visibility.Collapsed, "Reload retained an old floating selection.");
        Console.WriteLine("PASS: precise dimensions, stored/legacy/missing CID, selection/view/reload visibility, routed drag/drop, modifier/remove/clear/Undo, multi-selection, scrolling at 1440x900 and 980x680; no document commits.");
    }

    private static void Select(PanelCladdingGridCanvas canvas, params string[] ids) =>
        Invoke(canvas, "ApplySelection", ids, ModifierKeys.None);

    private static DragEventArgs DragArgs(DependencyObject target, string code, RoutedEvent routedEvent)
    {
        var data = new DataObject("PanelCladdingEditor.FrameExtrusionCode", code);
        var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), Private, null,
            [data, DragDropKeyStates.None, DragDropEffects.Copy, target, new Point(10, 10)], CultureInfo.InvariantCulture)!;
        args.RoutedEvent = routedEvent;
        return args;
    }

    private static PanelCladdingLayout BuildLayout(IReadOnlyDictionary<string, string> metadata) => new()
    {
        ObjectId = Guid.Parse("A0427000-0000-0000-0000-000000000107"),
        DocumentPath = "C:/fixtures/canvas-assignment.3dm", DocumentRuntimeSerialNumber = 107,
        ObjectName = "N1_03_07", SystemCode = "WT04", GeometryFingerprint = "canvas-assignment",
        GeometryClass = PanelGeometryClass.Planar, Width = 90.123456, Height = 180.000001,
        ModelTolerance = 0.001, ModelUnitScaleToMillimeters = 25.4,
        HorizontalOffsets = [102.85714], VerticalOffsets = [45], SourceUserText = metadata,
        Cells = Enumerable.Range(0, 2).SelectMany(column => Enumerable.Range(0, 2).Select(row => new PanelCladdingCell
        {
            Column = column, Row = row, RowLabel = row == 0 ? "A" : "B",
            ShortLabel = $"{column}{(row == 0 ? "A" : "B")}",
            UserTextKey = PanelCladdingKeyService.GetCellKey(column, row == 0 ? "A" : "B"), Value = "MPL-001"
        })).ToArray()
    };

    private static ImageSource Thumbnail(int i)
    {
        var drawing = new DrawingGroup();
        using (DrawingContext dc = drawing.Open())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 140, 75));
            dc.DrawRectangle(null, new Pen(Brushes.DimGray, 2), new Rect(25, 12, 80, 48));
            dc.DrawRectangle(null, new Pen(Brushes.Gray, 1), new Rect(30, 17, 70, 38));
            dc.DrawLine(new Pen(Brushes.Gray, 1), new Point(45 + i * 5, 17), new Point(45 + i * 5, 55));
        }
        return new DrawingImage(drawing);
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class => (T)root.FindName(name);
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static object? Invoke(object target, string name, params object?[] args) => target.GetType().GetMethod(name, Private)!.Invoke(target, args);
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T item) yield return item;
            foreach (T descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void Layout(FrameworkElement root, int width, int height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }
    private static void Render(FrameworkElement root, int width, int height, string name)
    {
        Layout(root, width, height);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream file = File.Create(Path.Combine("Project_Test", "261007_TEST_pc-editor-canvas-assignment", name));
        encoder.Save(file);
    }
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }

    private sealed class Repository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public PanelCladdingLayout Layout { get; } = layout;
        public int CommitCount { get; private set; }
        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) => OperationResponse<PanelCladdingLayout>.Ok(Layout);
        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) => OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not needed.");
        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(PanelAttributeCommitRequest request, Func<OperationResponse> finalizeExternalCommit)
        {
            CommitCount++;
            return OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult { ObjectId = request.ObjectId, Mutated = true });
        }
        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) => OperationResponse<string>.Ok(workbookPath);
    }

    private sealed class TestPresentationSource(Visual root) : PresentationSource
    {
        public override Visual RootVisual { get; set; } = root;
        public override bool IsDisposed => false;
        protected override CompositionTarget? GetCompositionTargetCore() => null;
    }
}

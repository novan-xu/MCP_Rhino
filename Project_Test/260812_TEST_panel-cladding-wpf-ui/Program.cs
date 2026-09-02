using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace PanelCladdingWpfUiSmoke;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        PanelCladdingLayout layout = BuildLayout();
        var repository = new StubRepository(layout);
        var keys = new PanelCladdingKeyService();
        var renderer = new PanelPreviewRenderer();
        var signature = new PanelCladdingTypeSignatureService(keys);
        var save = new PanelCladdingSaveService(repository, new StubWorkbookRepository(), renderer, signature);
        var controller = new PanelCladdingEditorController(repository, save, renderer, signature);
        var window = new PanelCladdingEditorWindow(controller)
        {
            Width = 1440d,
            Height = 900d,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };

        OperationResponse loaded = window.LoadPanel(layout.DocumentPath, layout.ObjectId);
        Require(loaded.Success, $"Sample layout failed to load: {loaded.Message}");

        FrameworkElement root = (FrameworkElement)window.Content;
        root.Measure(new Size(1440d, 900d));
        root.Arrange(new Rect(0d, 0d, 1440d, 900d));
        root.UpdateLayout();

        List<Button> buttons = Descendants<Button>(root).ToList();
        string[] requiredActions =
        [
            "Clear assignment", "Extrusion view", "Cladding view", "Material", "Parent cell",
            "Hide", "Save Extrusions", "Save Cladding", "Save Both"
        ];
        foreach (string action in requiredActions)
        {
            Require(buttons.Any(button => string.Equals(button.Content?.ToString(), action, StringComparison.Ordinal)),
                $"Designed action is missing: {action}");
        }
        Require(Descendants<PanelCladdingGridCanvas>(root).Count() == 1,
            "The window must contain exactly one custom WPF panel-grid canvas.");
        Require(!typeof(PanelCladdingEditorWindow).Assembly.GetReferencedAssemblies()
                .Any(reference => string.Equals(reference.Name, "Eto", StringComparison.OrdinalIgnoreCase)),
            "The WPF editor assembly must not reference Eto.");
        Require(typeof(PanelCladdingEditorWindow).BaseType == typeof(Window),
            "PanelCladdingEditorWindow must be a WPF Window.");

        string outputPath = OutputPath("panel-cladding-wpf-ui-1440x900.png");
        Render(root, 1440, 900, outputPath);
        string compactOutputPath = OutputPath("panel-cladding-wpf-ui-1024x768.png");
        Render(root, 1024, 768, compactOutputPath);

        var materialDialog = new MaterialSetupDialog(
        [
            PanelCladdingMaterialPalette.FromCode("MET-01"),
            PanelCladdingMaterialPalette.FromCode("GLS-02"),
            PanelCladdingMaterialPalette.FromCode("STN-01"),
            PanelCladdingMaterialPalette.FromCode("TER-01")
        ]);
        FrameworkElement materialRoot = (FrameworkElement)materialDialog.Content;
        string materialOutputPath = OutputPath("panel-cladding-material-setup-680x690.png");
        Render(materialRoot, 680, 690, materialOutputPath);

        Console.WriteLine("[OK] WPF window loads a real 4 x 3 panel layout.");
        Console.WriteLine("[OK] Design-critical canvas, direct assignment, and footer actions are present.");
        Console.WriteLine("[OK] Production UI assembly references WPF and does not reference Eto.");
        Console.WriteLine($"[OK] 1440 x 900 off-screen render: {outputPath}");
        Console.WriteLine($"[OK] 1024 x 768 compact render: {compactOutputPath}");
        Console.WriteLine($"[OK] 680 x 690 material setup render: {materialOutputPath}");
    }

    private static string OutputPath(string fileName) => Path.GetFullPath(Path.Combine(
        "Project_Test", "260812_TEST_panel-cladding-wpf-ui", fileName));

    private static void Render(FrameworkElement root, int width, int height, string outputPath)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0d, 0d, width, height));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(outputPath);
        encoder.Save(stream);
        Require(new FileInfo(outputPath).Length > 15_000L, $"Off-screen UI snapshot is unexpectedly empty: {outputPath}");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
            {
                yield return typed;
            }
            foreach (T descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static PanelCladdingLayout BuildLayout()
    {
        string[] rowLabels = ["A", "B", "C"];
        string?[,] values =
        {
            { "TER-01", "0A", "MET-01", null },
            { "MET-01", "GLS-02", "GLS-02", "STN-01" },
            { "MET-01", "0C", "GLS-02", "STN-01" }
        };
        var cells = new List<PanelCladdingCell>();
        for (int row = 0; row < rowLabels.Length; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                string label = $"{column}{rowLabels[row]}";
                cells.Add(new PanelCladdingCell
                {
                    Column = column,
                    Row = row,
                    RowLabel = rowLabels[row],
                    ShortLabel = label,
                    UserTextKey = PanelCladdingKeyService.GetCellKey(column, rowLabels[row]),
                    Value = values[row, column] ?? string.Empty
                });
            }
        }
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("A0427000-0000-0000-0000-000000000001"),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/design/panel-cladding.3dm",
            ObjectName = "PID-A0427",
            LayerFullPath = "CLDG::L03",
            SystemCode = "CW01",
            GeometryFingerprint = "design-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar Brep is projectable.",
            Width = 181.1d,
            Height = 112.6d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = [29.9d, 76.4d],
            VerticalOffsets = [49.2d, 82.7d, 142.5d],
            Cells = cells,
            WorkbookPath = "C:/design/panel-cladding-types.xlsx"
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class StubRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used by WPF UI smoke.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit) =>
            OperationResponse<PanelAttributeCommitResult>.Fail("Not used by WPF UI smoke.");

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Fail("Not used by WPF UI smoke.");
    }

    private sealed class StubWorkbookRepository : IPanelCladdingWorkbookRepository
    {
        public OperationResponse<IPreparedPanelCladdingWorkbookUpdate> PrepareUpsert(PanelCladdingWorkbookUpsert request) =>
            OperationResponse<IPreparedPanelCladdingWorkbookUpdate>.Fail("Not used by WPF UI smoke.");

        public OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate> PrepareBatchUpsert(
            PanelCladdingWorkbookBatchUpsert request) =>
            OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate>.Fail("Not used by WPF UI smoke.");
    }
}

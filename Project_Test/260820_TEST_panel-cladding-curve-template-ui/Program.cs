using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;
using Rhino.Commands;

namespace PanelCladdingCurveTemplateUiSmoke;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        VerifyCurveTemplatePlanning();
        VerifyCurveTemplateBatches();
        VerifyCommandAndMutationContracts();

        Console.WriteLine("[OK] H/V curve-template masks preserve topology and round-trip canonically.");
        Console.WriteLine("[OK] Mixed/all-configured batches skip existing masks; duplicates, no-runs, and invalid inputs are handled.");
        Console.WriteLine("[OK] PCCrvTemplate command, one-record Undo, and rollback contracts passed.");
        if (args.Contains("--curve-template-only", StringComparer.Ordinal))
        {
            return;
        }
        VerifyResponsiveMaterialCatalogue();
        VerifyFooterLabelsFit();
        Console.WriteLine("[OK] Material catalogue uses longest-code tile width and responsive wrapping.");
        Console.WriteLine("[OK] Save Extrusions, Save Cladding, and Save Both fit at constrained width.");
    }

    private static void VerifyCurveTemplatePlanning()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingCurveTemplatePlanningService(keys);
        Guid panelId = Guid.Parse("A8200000-0000-0000-0000-000000000001");
        var original = new PanelCladdingTopologyState
        {
            MissingSegments =
            [
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 2)
            ],
            HiddenSegments =
            [
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 1, 0),
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 1, 1)
            ],
            MergeRuns =
            [
                new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Vertical, 0, 0, 2)
            ]
        };
        var snapshot = new PanelCladdingCurveTemplatePanelSnapshot
        {
            ObjectId = panelId,
            HorizontalTrackCount = 2,
            VerticalTrackCount = 3,
            Topology = original
        };

        PanelCladdingCurveTemplatePanelPlan horizontal = RequirePlan(
            planner.CreatePlan([snapshot], PanelCladdingCurveTemplatePriority.Horizontal));
        PanelCladdingMergeRun[] expectedHorizontal =
        [
            new(PanelCladdingTopologyAxis.Horizontal, 0, 0, 1),
            new(PanelCladdingTopologyAxis.Horizontal, 1, 0, 1),
            new(PanelCladdingTopologyAxis.Horizontal, 1, 2, 3)
        ];
        Require(horizontal.MergeRuns.SequenceEqual(expectedHorizontal),
            $"Unexpected H-priority runs: {FormatRuns(horizontal.MergeRuns)}");
        OperationResponse<PanelCladdingTopologyState> combined = keys.DecodeTopology(
            new Dictionary<string, string> { [PanelCladdingKeyService.FrameConfigKey] = horizontal.FrameConfig }, 2, 3);
        Require(combined.Success && combined.Data is not null &&
                combined.Data.MergeRuns.SequenceEqual(expectedHorizontal) &&
                combined.Data.MissingSegments.ToHashSet().SetEquals(original.MissingSegments) &&
                combined.Data.HiddenSegments.ToHashSet().SetEquals(original.HiddenSegments),
            "Template combined configuration must retain delete/hide state with the new merge runs.");
        Require(horizontal.MergeRuns.All(run => run.Axis == PanelCladdingTopologyAxis.Horizontal),
            "H priority retained a non-horizontal merge run.");
        Require(horizontal.UserTextDeletes.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(
                [PanelCladdingKeyService.SignatureKey, PanelCladdingKeyService.LegacySignatureKey]),
            "A changed merge mask does not invalidate both stored topology signatures.");

        PanelCladdingTopologyState decodedHorizontal = DecodeWithReplacement(
            keys,
            original,
            horizontal.MergeMask,
            horizontalTrackCount: 2,
            verticalTrackCount: 3);
        Require(decodedHorizontal.MissingSegments.SequenceEqual(original.MissingSegments) &&
                decodedHorizontal.HiddenSegments.SequenceEqual(original.HiddenSegments) &&
                decodedHorizontal.MergeRuns.SequenceEqual(expectedHorizontal),
            "H-priority payload did not preserve segment/hide evidence and expected runs.");

        OperationResponse<PanelCladdingCurveTemplatePlan> alreadyConfigured = planner.CreatePlan(
        [
            new PanelCladdingCurveTemplatePanelSnapshot
            {
                ObjectId = panelId,
                HorizontalTrackCount = 2,
                VerticalTrackCount = 3,
                HasMergeMask = true,
                Topology = decodedHorizontal
            }
        ], PanelCladdingCurveTemplatePriority.Horizontal);
        Require(alreadyConfigured.Success && alreadyConfigured.Data is not null &&
                alreadyConfigured.Data.Panels.Count == 0 &&
                alreadyConfigured.Data.SkippedPanelIds.SequenceEqual([panelId]),
            "PCCrvTemplate did not skip a panel with an existing merge code.");

        PanelCladdingCurveTemplatePanelPlan vertical = RequirePlan(
            planner.CreatePlan([snapshot], PanelCladdingCurveTemplatePriority.Vertical));
        PanelCladdingMergeRun[] expectedVertical =
        [
            new(PanelCladdingTopologyAxis.Vertical, 0, 0, 2),
            new(PanelCladdingTopologyAxis.Vertical, 1, 0, 2),
            new(PanelCladdingTopologyAxis.Vertical, 2, 0, 2)
        ];
        Require(vertical.MergeRuns.SequenceEqual(expectedVertical),
            $"Unexpected V-priority runs: {FormatRuns(vertical.MergeRuns)}");
        PanelCladdingTopologyState decodedVertical = DecodeWithReplacement(
            keys,
            original,
            vertical.MergeMask,
            horizontalTrackCount: 2,
            verticalTrackCount: 3);
        Require(decodedVertical.MergeRuns.SequenceEqual(expectedVertical),
            "V-priority payload did not replace the prior merge topology.");

        OperationResponse<PanelCladdingCurveTemplatePlan> empty = planner.CreatePlan(
            Array.Empty<PanelCladdingCurveTemplatePanelSnapshot>(),
            PanelCladdingCurveTemplatePriority.Horizontal);
        Require(!empty.Success && empty.Message.Contains("SELECTION_REQUIRED", StringComparison.Ordinal),
            "An empty curve-template plan did not fail before mutation.");
    }

    private static void VerifyCurveTemplateBatches()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingCurveTemplatePlanningService(keys);
        var configured = new PanelCladdingCurveTemplatePanelSnapshot
        {
            ObjectId = Guid.Parse("A9160000-0000-0000-0000-000000000001"),
            HorizontalTrackCount = 2,
            VerticalTrackCount = 3,
            HasMergeMask = true,
            Topology = new PanelCladdingTopologyState
            {
                MergeRuns = [new(PanelCladdingTopologyAxis.Vertical, 0, 0, 1)]
            }
        };
        var first = new PanelCladdingCurveTemplatePanelSnapshot
        {
            ObjectId = Guid.Parse("A9160000-0000-0000-0000-000000000002"),
            HorizontalTrackCount = 2,
            VerticalTrackCount = 3
        };
        var second = new PanelCladdingCurveTemplatePanelSnapshot
        {
            ObjectId = Guid.Parse("A9160000-0000-0000-0000-000000000003"),
            HorizontalTrackCount = 1,
            VerticalTrackCount = 2
        };
        var noRuns = new PanelCladdingCurveTemplatePanelSnapshot
        {
            ObjectId = Guid.Parse("A9160000-0000-0000-0000-000000000004")
        };
        PanelCladdingMergeRun[] originalRuns = configured.Topology.MergeRuns.ToArray();
        foreach (PanelCladdingCurveTemplatePriority priority in Enum.GetValues<PanelCladdingCurveTemplatePriority>())
        {
            PanelCladdingCurveTemplatePanelPlan expectedFirst = RequirePlan(planner.CreatePlan([first], priority));
            PanelCladdingCurveTemplatePanelPlan expectedSecond = RequirePlan(planner.CreatePlan([second], priority));
            PanelCladdingCurveTemplatePanelSnapshot[][] selections =
            [
                [configured, first, second],
                [first, configured, second],
                [first, second, configured],
                [configured, first, configured, second, first]
            ];
            foreach (PanelCladdingCurveTemplatePanelSnapshot[] selection in selections)
            {
                OperationResponse<PanelCladdingCurveTemplatePlan> mixed = planner.CreatePlan(selection, priority);
                Require(mixed.Success && mixed.Data is not null,
                    $"{priority} mixed configured/unconfigured batch failed: {mixed.Message}");
                Require(mixed.Data!.Priority == priority &&
                        mixed.Data.Panels.Select(panel => panel.ObjectId).SequenceEqual([first.ObjectId, second.ObjectId]),
                    "Mixed batch included configured panels or lost/duplicated eligible panels.");
                Require(mixed.Data.SkippedPanelIds.SequenceEqual([configured.ObjectId]),
                    "Configured skips were lost or counted more than once.");
                Require(mixed.Data.Panels[0].MergeMask == expectedFirst.MergeMask &&
                        mixed.Data.Panels[1].MergeMask == expectedSecond.MergeMask,
                    "Skipping a configured panel changed another panel's planned template.");
            }

            OperationResponse<PanelCladdingCurveTemplatePlan> allConfigured = planner.CreatePlan(
                [configured, configured], priority);
            Require(allConfigured.Success && allConfigured.Data is not null &&
                    allConfigured.Data.Panels.Count == 0 &&
                    allConfigured.Data.SkippedPanelIds.SequenceEqual([configured.ObjectId]),
                "An all-configured selection must succeed without any mutation plans.");

            OperationResponse<PanelCladdingCurveTemplatePlan> withNoRuns = planner.CreatePlan(
                [configured, noRuns, first], priority);
            Require(withNoRuns.Success && withNoRuns.Data is not null &&
                    withNoRuns.Data.Panels.Count == 2 &&
                    withNoRuns.Data.Panels[0].ObjectId == noRuns.ObjectId &&
                    withNoRuns.Data.Panels[0].MergeMask.Length == 0 &&
                    withNoRuns.Data.Panels[0].MergeRuns.Count == 0 &&
                    withNoRuns.Data.Panels[0].UserTextDeletes.Count == 0 &&
                    withNoRuns.Data.SkippedPanelIds.SequenceEqual([configured.ObjectId]),
                "An eligible no-run panel must remain mask-free without signature invalidation.");
        }
        Require(configured.Topology.MergeRuns.SequenceEqual(originalRuns),
            "Planning mutated an existing configured panel's topology.");

        var invalid = new PanelCladdingCurveTemplatePanelSnapshot
        {
            ObjectId = Guid.Parse("A9160000-0000-0000-0000-000000000005"),
            HorizontalTrackCount = -1,
            VerticalTrackCount = 2
        };
        OperationResponse<PanelCladdingCurveTemplatePlan> invalidBatch = planner.CreatePlan(
            [first, configured, invalid], PanelCladdingCurveTemplatePriority.Horizontal);
        Require(!invalidBatch.Success && invalidBatch.Data is null &&
                invalidBatch.Message.Contains("TRACK_COUNT_INVALID", StringComparison.Ordinal),
            "An invalid unconfigured panel must fail the batch without exposing a partial plan.");
        Require(!planner.CreatePlan([configured], (PanelCladdingCurveTemplatePriority)999).Success,
            "An all-configured selection bypassed invalid-priority validation.");
        Require(!planner.CreatePlan([new PanelCladdingCurveTemplatePanelSnapshot()],
                PanelCladdingCurveTemplatePriority.Horizontal).Success,
            "An empty-ID selection bypassed required-selection validation.");
    }

    private static PanelCladdingTopologyState DecodeWithReplacement(
        PanelCladdingKeyService keys,
        PanelCladdingTopologyState original,
        string mergeMask,
        int horizontalTrackCount,
        int verticalTrackCount)
    {
        OperationResponse<PanelCladdingTopologyPayloads> originalPayloads = keys.EncodeTopology(
            new PanelCladdingTopologyState
            {
                MissingSegments = original.MissingSegments,
                HiddenSegments = original.HiddenSegments
            },
            horizontalTrackCount,
            verticalTrackCount);
        Require(originalPayloads.Success && originalPayloads.Data is not null,
            $"Original topology did not encode: {originalPayloads.Message}");
        OperationResponse<PanelCladdingTopologyState> decoded = keys.DecodeTopology(
            new Dictionary<string, string>
            {
                [PanelCladdingKeyService.SegmentMaskKey] = originalPayloads.Data!.SegmentMask,
                [PanelCladdingKeyService.MergeMaskKey] = mergeMask,
                [PanelCladdingKeyService.HideMaskKey] = originalPayloads.Data.HideMask
            },
            horizontalTrackCount,
            verticalTrackCount);
        Require(decoded.Success && decoded.Data is not null,
            $"Replacement topology did not decode: {decoded.Message}");
        return decoded.Data!;
    }

    private static PanelCladdingCurveTemplatePanelPlan RequirePlan(
        OperationResponse<PanelCladdingCurveTemplatePlan> response)
    {
        Require(response.Success && response.Data is not null,
            $"Curve-template plan failed: {response.Message}");
        Require(response.Data!.Panels.Count == 1, "Expected exactly one focused panel plan.");
        return response.Data.Panels[0];
    }

    private static void VerifyCommandAndMutationContracts()
    {
        Assembly assembly = typeof(PanelCladdingKeyService).Assembly;
        Type command = assembly.GetType("PanelCladdingEditor.UI.PanelCladdingCurveTemplateCommand") ??
            throw new InvalidOperationException("PCCrvTemplate command type is missing.");
        Require(typeof(Command).IsAssignableFrom(command) && command.GUID != Guid.Empty,
            "PCCrvTemplate is not a Rhino Command with an explicit non-empty GUID.");
        Type[] commands = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Command).IsAssignableFrom(type))
            .ToArray();
        Require(commands.Select(type => type.GUID).Distinct().Count() == commands.Length,
            "PCCrvTemplate introduced a duplicate command GUID.");

        string repositoryRoot = FindRepositoryRoot();
        string commandSource = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "PanelCladdingEditor", "UI", "PanelCladdingCurveTemplateCommand.cs"));
        Require(Regex.IsMatch(commandSource,
                "EnglishName\\s*=>\\s*\\\"PCCrvTemplate\\\"",
                RegexOptions.CultureInvariant),
            "The command does not register the exact PCCrvTemplate name.");
        Require(commandSource.IndexOf("SelectPanels()", StringComparison.Ordinal) <
                commandSource.IndexOf("SelectPriority()", StringComparison.Ordinal),
            "PCCrvTemplate does not prompt for panels before priority.");
        Require(commandSource.Contains("HPriority", StringComparison.Ordinal) &&
                commandSource.Contains("VPriority", StringComparison.Ordinal),
            "PCCrvTemplate does not expose both required priority options.");

        string liveSource = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live",
            "PanelCladding", "LivePanelCladdingCurveTemplateService.cs"));
        Require(Regex.Matches(liveSource, "BeginUndoRecord\\(", RegexOptions.CultureInvariant).Count == 1 &&
                liveSource.Contains("RestoreCommitted", StringComparison.Ordinal) &&
                liveSource.IndexOf("_planning.CreatePlan", StringComparison.Ordinal) <
                liveSource.IndexOf("BeginUndoRecord", StringComparison.Ordinal),
            "Curve-template mutation is not planned first, batched into one Undo record, and rollback-capable.");
        Require(liveSource.Contains("PanelCladdingKeyService.FrameConfigKey", StringComparison.Ordinal) &&
                liveSource.Contains("HasNonblankMergeMask", StringComparison.Ordinal) &&
                liveSource.Contains("IsNullOrWhiteSpace(panelPlan.MergeMask)", StringComparison.Ordinal) &&
                !liveSource.Contains("SegmentMaskKey", StringComparison.Ordinal) &&
                !liveSource.Contains("HideMaskKey", StringComparison.Ordinal),
            "The live mutation must guard configured/no-op panels and write combined configuration.");
        Require(liveSource.Contains("panelPlan.UserTextDeletes", StringComparison.Ordinal),
            "The live mutation does not apply planned signature invalidation after a mask change.");
    }

    private static void VerifyResponsiveMaterialCatalogue()
    {
        string[] codes =
        [
            "GL-1", "MET-02", "STN-003", "TERR-04", "WOOD-005",
            "CERAMIC-06", "FIBRE-007", "THERMALLY-BROKEN", "GFRC-09", "ZINC-10"
        ];
        PanelCladdingMaterial[] materials = codes
            .Select((code, index) => new PanelCladdingMaterial
            {
                Code = code,
                Name = $"Material {index + 1}",
                Category = "Composite",
                Color = Color.FromRgb((byte)(80 + index * 8), (byte)(110 + index * 5), (byte)(130 + index * 4))
            })
            .ToArray();
        var dialog = new MaterialSetupDialog(materials)
        {
            Width = 720,
            Height = 740,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        FrameworkElement root = (FrameworkElement)dialog.Content;
        Layout(root, 720, 740);

        ListBox configured = Named<ListBox>(dialog, "ConfiguredList");
        Require(configured.MaxHeight >= 220d,
            "The catalogue retains the old shallow browsing cap.");
        WrapPanel panel = Descendants<WrapPanel>(configured).Single();
        Require(!Descendants<UniformGrid>(configured).Any(),
            "The catalogue is still constrained to a fixed UniformGrid column count.");
        ListBoxItem[] tiles = Enumerable.Range(0, configured.Items.Count)
            .Select(index => configured.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem ??
                throw new InvalidOperationException($"Catalogue tile {index} was not generated."))
            .ToArray();
        Require(tiles.All(tile => Math.Abs(tile.ActualWidth - dialog.CatalogueItemWidth) < 0.01d),
            "Catalogue tiles do not share the longest-code-governed width.");
        int firstRowCount = tiles.Count(tile =>
            Math.Abs(tile.TranslatePoint(new Point(0, 0), panel).Y -
                     tiles[0].TranslatePoint(new Point(0, 0), panel).Y) < 0.01d);
        Require(firstRowCount >= 4,
            $"Responsive catalogue fitted only {firstRowCount} columns at 720 DIPs.");

        ListBoxItem longestTile = tiles[Array.IndexOf(codes, "THERMALLY-BROKEN")];
        TextBlock codeText = Descendants<TextBlock>(longestTile).Single();
        double requiredTextWidth = MeasureText(codeText.Text, codeText).WidthIncludingTrailingWhitespace;
        Require(codeText.ActualWidth + 0.5d >= requiredTextWidth,
            $"Longest material code is clipped ({codeText.ActualWidth:0.##}/{requiredTextWidth:0.##}).");
        Require(dialog.CatalogueItemWidth < configured.ActualWidth / 3d,
            "A long material code still forces the catalogue into only three equal columns.");

        Render(root, 720, 740, OutputPath("material-catalogue-responsive-720x740.png"));
    }

    private static void VerifyFooterLabelsFit()
    {
        PanelCladdingLayout layout = BuildLayout();
        var repository = new StubRepository(layout);
        var keys = new PanelCladdingKeyService();
        var renderer = new PanelPreviewRenderer();
        var signature = new PanelCladdingTypeSignatureService(keys);
        var save = new PanelCladdingSaveService(repository, signature);
        var controller = new PanelCladdingEditorController(repository, save, renderer, signature);
        var window = new PanelCladdingEditorWindow(controller)
        {
            Width = 980,
            Height = 700,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        OperationResponse loaded = window.LoadPanel(layout.DocumentPath, layout.ObjectId);
        Require(loaded.Success, $"Footer fixture did not load: {loaded.Message}");
        FrameworkElement root = (FrameworkElement)window.Content;
        Layout(root, 980, 700);

        foreach (string name in new[] { "SaveExtrusionsButton", "SaveCladdingButton", "SaveButton" })
        {
            Button button = Named<Button>(window, name);
            string label = button.Content?.ToString() ?? string.Empty;
            double required = MeasureText(label, button).WidthIncludingTrailingWhitespace +
                button.Padding.Left + button.Padding.Right + button.BorderThickness.Left +
                button.BorderThickness.Right;
            Require(button.ActualWidth + 0.5d >= required,
                $"{label} is clipped at constrained width ({button.ActualWidth:0.##}/{required:0.##}).");
        }

        Render(root, 980, 700, OutputPath("panel-cladding-footer-980x700.png"));
    }

    private static FormattedText MeasureText(string text, Control control) => MeasureText(
        text,
        control.FontFamily,
        control.FontStyle,
        control.FontWeight,
        control.FontStretch,
        control.FontSize);

    private static FormattedText MeasureText(string text, TextBlock textBlock) => MeasureText(
        text,
        textBlock.FontFamily,
        textBlock.FontStyle,
        textBlock.FontWeight,
        textBlock.FontStretch,
        textBlock.FontSize);

    private static FormattedText MeasureText(
        string text,
        FontFamily fontFamily,
        FontStyle fontStyle,
        FontWeight fontWeight,
        FontStretch fontStretch,
        double fontSize) => new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, fontStyle, fontWeight, fontStretch),
            fontSize,
            Brushes.Black,
            1d);

    private static PanelCladdingLayout BuildLayout()
    {
        string[] rowLabels = ["A", "B"];
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
                    Value = column % 2 == 0 ? "MET-01" : "GLS-02"
                });
            }
        }
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("A8200000-0000-0000-0000-000000000002"),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/design/panel-cladding.3dm",
            ObjectName = "PID-TEMPLATE-UI",
            LayerFullPath = "CLDG::CW01",
            SystemCode = "CW01",
            GeometryFingerprint = "curve-template-ui-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar test fixture.",
            Width = 180d,
            Height = 100d,
            ModelTolerance = 0.001d,
            HorizontalOffsets = [50d],
            VerticalOffsets = [45d, 90d, 135d],
            Cells = cells
        };
    }

    private static string FormatRuns(IEnumerable<PanelCladdingMergeRun> runs) => string.Join(
        ", ", runs.Select(run => $"{run.Axis}:{run.Track}:{run.StartBay}-{run.EndBay}"));

    private static T Named<T>(FrameworkElement owner, string name) where T : FrameworkElement =>
        owner.FindName(name) as T ?? throw new InvalidOperationException($"Missing named element {name}.");

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

    private static void Layout(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static void Render(FrameworkElement element, int width, int height, string path)
    {
        Layout(element, width, height);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        Require(new FileInfo(path).Length > 10_000, $"Render is unexpectedly empty: {path}");
    }

    private static string OutputPath(string fileName) => Path.GetFullPath(Path.Combine(
        "Project_Test", "260820_TEST_panel-cladding-curve-template-ui", fileName));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "PanelCladdingEditor")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the MCP_Rhino repository root.");
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
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used by focused UI smoke.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit) =>
            OperationResponse<PanelAttributeCommitResult>.Fail("Not used by focused UI smoke.");

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Fail("Not used by focused UI smoke.");
    }
}

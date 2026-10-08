using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace PanelExtrusionAssignmentSmoke;

internal static class Program
{
    private const string ExpectedProfile = "1D-H0651";

    [STAThread]
    private static int Main(string[] args)
    {
        bool clearQa = args.FirstOrDefault() == "--clear-qa";
        string pdfPath = clearQa ? string.Empty : args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("PANEL_EXTRUSION_SCHEDULE_PDF") ?? string.Empty;
        if (clearQa)
        {
            VerifyCatalogueClear(Path.GetFullPath(args[1]));
        }
        VerifyAssignmentSerializationAndFrameConfiguration();
        VerifyBakedCurveAssignments();
        VerifyConfiguredProfileFormulas();
        VerifySourceContracts();
        if (!string.IsNullOrWhiteSpace(pdfPath))
        {
            VerifyRealScheduleAndWorkbook(pdfPath);
        }
        Console.WriteLine("[OK] additive 1D/0D assignments serialize deterministically and validate merge runs.");
        Console.WriteLine("[OK] LL modifiers, quantities, fixed counts, and spacing formulas are correct.");
        Console.WriteLine("[OK] frame config stores topology; frame type stores assigned profile definitions.");
        Console.WriteLine("[OK] exact Rhino keys and baked-curve take-off key are present in production sources.");
        if (!string.IsNullOrWhiteSpace(pdfPath))
        {
            Console.WriteLine("[OK] real all-page schedule import and Extrusions worksheet round-trip passed.");
        }
        return 0;
    }

    private static void VerifyCatalogueClear(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var drawing = new DrawingVisual();
        using (DrawingContext context = drawing.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 120, 70));
            context.DrawRectangle(null, new Pen(Brushes.Black, 2), new Rect(15, 15, 90, 40));
            context.DrawRectangle(null, new Pen(Brushes.Black, 1), new Rect(20, 20, 80, 30));
        }
        var bitmap = new RenderTargetBitmap(120, 70, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var image = new MemoryStream();
        encoder.Save(image);
        PanelFrameExtrusionCatalogItem Raw(string code, string pdfPath) => new()
        {
            BaseCode = code, SourceCode = $"ALU-{code}", Category = "FRAMING",
            SourcePageNumber = 1, SourcePdfPath = pdfPath, ThumbnailPng = image.ToArray()
        };
        const string originalPdf = @"C:\Temp\schedule-before.pdf";
        PanelFrameExtrusionCatalogItem[] initial =
        [
            Raw("H0703", originalPdf),
            Configured(Raw("H0651", originalPdf), PanelFrameProfileDimension.OneDimensional,
                PanelFrameProfileCalculation.Length, 2d),
            Configured(Raw("H0652", originalPdf), PanelFrameProfileDimension.ZeroDimensional,
                PanelFrameProfileCalculation.Spacing, 24d, ExpectedProfile)
        ];
        PanelFrameExtrusion[] caller = initial.Select(PanelFrameExtrusion.FromCatalogItem).ToArray();
        string workbookPath = Path.Combine(Path.GetTempPath(), $"extrusion-clear-{Guid.NewGuid():N}.xlsx");
        var workbook = new OpenXmlPanelCladdingWorkbookRepository();
        var live = new StubLiveRepository(BuildLayout());
        var signature = new PanelCladdingTypeSignatureService(new PanelCladdingKeyService());
        var importer = new StubScheduleImporter(path => Raw("H0651", path));
        var controller = new PanelCladdingEditorController(live, new PanelCladdingSaveService(live, signature),
            new PanelPreviewRenderer(), signature, workbook, importer);
        void Save(IEnumerable<PanelFrameExtrusionCatalogItem> items) => Required(
            controller.SaveFrameExtrusionCatalog(new PanelFrameExtrusionCatalogSaveRequest
            {
                WorkbookPath = workbookPath, AllowCreate = !File.Exists(workbookPath), Extrusions = items.ToArray()
            }), "save extrusion catalogue");
        try
        {
            Save(initial);
            byte[] originalWorkbook = File.ReadAllBytes(workbookPath);
            var prompt = new StubClearPrompt(false, true, true);
            var dialog = new ExtrusionSetupDialog(controller, workbookPath, caller, prompt);
            T Named<T>(string name) where T : FrameworkElement =>
                (T)(dialog.FindName(name) ?? throw new InvalidOperationException($"Missing {name}"));
            ListBox pool = Named<ListBox>("PoolList");
            ListBox ready = Named<ListBox>("ReadyList");
            Button clear = Named<Button>("ClearAllButton");
            TextBox pdf = Named<TextBox>("PdfPathText");
            var selected = ready.Items.Cast<PanelFrameExtrusion>().Single(item => item.Code == "0D-H0652");
            ready.SelectedItem = selected;
            Named<TextBox>("QuantityText").Text = "7";
            FrameworkElement root = (FrameworkElement)dialog.Content;
            // The detached content render needs the background normally supplied by its Window.
            ((System.Windows.Controls.Panel)root).Background = dialog.Background;
            foreach ((int width, int height) in new[] { (1220, 820), (1040, 700) })
            {
                Render(root, width, height, Path.Combine(outputDirectory, $"extrusion-clear-{width}x{height}.png"));
                Point buttonOrigin = clear.TranslatePoint(new Point(0, 0), root);
                Point cancelOrigin = Named<Button>("CancelButton").TranslatePoint(new Point(0, 0), root);
                Require(buttonOrigin.X >= 0 && buttonOrigin.Y + clear.ActualHeight <= height &&
                        buttonOrigin.X + clear.ActualWidth < cancelOrigin.X,
                    "Clear all is clipped or overlaps the footer at a supported dialog size.");
            }

            clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(prompt.CallCount == 1 && prompt.ProfileCount == 3 && ReferenceEquals(prompt.Owner, dialog) &&
                    pool.Items.Count == 1 && ready.Items.Count == 2 && ReferenceEquals(ready.SelectedItem, selected) &&
                    pdf.Text == originalPdf && Named<TextBox>("QuantityText").Text == "7" &&
                    Named<TextBox>("ZeroValueText").Text == "24",
                "Declining confirmation changed the catalogue, path, selection or editor values.");
            clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(prompt.CallCount == 2 && dialog.Extrusions.Count == 0 && pool.Items.Count == 0 &&
                    ready.Items.Count == 0 && pool.SelectedItem is null && ready.SelectedItem is null && pdf.Text.Length == 0,
                "Accepted clear did not remove both configured/unconfigured lists and the PDF path.");
            Require(Named<Image>("EditorPreview").Source is null &&
                    Named<TextBlock>("EditorCodeText").Text == "Select a profile" &&
                    Named<TextBox>("QuantityText").Text.Length == 0 && Named<TextBox>("ZeroValueText").Text.Length == 0 &&
                    Named<TabControl>("DimensionTabs").SelectedIndex == 0 && Named<RadioButton>("FixedRadio").IsChecked == true &&
                    Named<ComboBox>("ParentCodeCombo").Items.Count == 1 &&
                    !Named<Button>("ConfigureButton").IsEnabled && !Named<Button>("ReturnButton").IsEnabled,
                "Clear left a selected profile, preview, configuration value or parent behind.");
            Require(Named<TextBlock>("PoolCountText").Text.StartsWith("0 ", StringComparison.Ordinal) &&
                    Named<TextBlock>("ReadyCountText").Text.StartsWith("0 ", StringComparison.Ordinal) &&
                    Named<TextBox>("WorkbookPathText").Text == workbookPath && dialog.WorkbookPath == workbookPath &&
                    caller.Length == 3 && caller.Count(item => item.IsConfigured) == 2 &&
                    File.ReadAllBytes(workbookPath).SequenceEqual(originalWorkbook),
                "Clear changed the save destination, caller or workbook, or failed to refresh counts.");
            var cancelled = new ExtrusionSetupDialog(controller, workbookPath, caller, new StubClearPrompt());
            Require(cancelled.Extrusions.Count == 3, "Discarding pending clear did not retain the saved catalogue.");
            Save(dialog.Extrusions.Select(item => item.ToCatalogItem()));
            var reopened = new ExtrusionSetupDialog(controller, workbookPath, caller, new StubClearPrompt());
            Require(reopened.Extrusions.Count == 0 && ((TextBox)reopened.FindName("PdfPathText")).Text.Length == 0,
                "Saved empty catalogue reopened with old profiles or inherited PDF path.");
            Render(root, 1220, 820, Path.Combine(outputDirectory, "extrusion-clear-empty.png"));

            pdf.Text = @"C:\Temp\schedule-next.pdf";
            clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(prompt.ProfileCount == 0 && pdf.Text.Length == 0,
                "Clear did not reset a PDF path when the catalogue was already empty.");
            pdf.Text = @"C:\Temp\schedule-next.pdf";
            Named<Button>("ExtractButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(importer.LastPath == pdf.Text && pool.Items.Count == 1 && ready.Items.Count == 0 &&
                    dialog.Extrusions.Single().SourceCode == "ALU-H0651" && !dialog.Extrusions.Single().IsConfigured &&
                    Named<Button>("ExtractButton").IsEnabled,
                "Extraction after clear failed or carried over old configuration for the same source code.");
        }
        finally
        {
            File.Delete(workbookPath);
        }
        Console.WriteLine("[OK] Clear confirmation decline/accept, complete reset, working-copy isolation, empty save/reopen and re-extraction passed.");
    }

    private static void VerifyAssignmentSerializationAndFrameConfiguration()
    {
        var keys = new PanelCladdingKeyService();
        var assignments = new PanelFrameAssignmentService();
        PanelCladdingLayout layout = BuildLayout();
        var state = new PanelFrameAssignmentState
        {
            FrameAssignments = new Dictionary<string, IReadOnlyList<string>>
            {
                ["FRM_0"] = [ExpectedProfile, "1D-H0677"]
            },
            SegmentAssignments =
            [
                new(new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 0), [ExpectedProfile]),
                new(new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 1), [ExpectedProfile]),
                new(new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, 0, 0), ["1D-H0677"])
            ],
            Definitions = Definitions(ExpectedProfile, "1D-H0677")
        };
        string payload = Required(assignments.Encode(state, 1, 1, layout.Topology), "encode assignments");
        PanelFrameAssignmentState decoded = Required(assignments.Decode(payload, 1, 1, layout.Topology), "decode assignments");
        Require(decoded.FrameAssignments["FRM_0"].Count == 2, "Multiple frame codes were not retained.");
        Require(Required(assignments.Encode(decoded, 1, 1, layout.Topology), "re-encode assignments") == payload,
            "Assignment payload was not deterministic.");

        string config = Required(keys.EncodeFrameConfiguration(layout.Topology, 1, 1), "encode configuration");
        foreach (PanelCladdingTopologyState changed in new[]
                 {
                     new PanelCladdingTopologyState(),
                     new PanelCladdingTopologyState
                     {
                         MergeRuns = layout.Topology.MergeRuns,
                         HiddenSegments = [new(PanelCladdingTopologyAxis.Vertical, 0, 0)]
                     },
                     new PanelCladdingTopologyState
                     {
                         MergeRuns = layout.Topology.MergeRuns,
                         MissingSegments = [new(PanelCladdingTopologyAxis.Vertical, 0, 1)]
                     }
                 })
        {
            Require(Required(keys.EncodeFrameConfiguration(changed, 1, 1), "changed config") != config,
                "Changing merge, hide or delete state did not change frame config.");
        }
        VerifyFrameAttributeMigration(keys, layout.Topology, payload);
        var invalidMerge = new PanelFrameAssignmentState
        {
            SegmentAssignments =
            [
                new(new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 0), [ExpectedProfile]),
                new(new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 1), ["1D-H0677"])
            ],
            Definitions = Definitions(ExpectedProfile, "1D-H0677")
        };
        Require(!assignments.Normalize(invalidMerge, 1, 1, layout.Topology).Success,
            "A merge run accepted inconsistent atomic assignment sets.");
        VerifySavePersistence(layout, state);
    }

    private static void VerifyFrameAttributeMigration(
        PanelCladdingKeyService keys, PanelCladdingTopologyState topology, string typePayload)
    {
        var assignments = new PanelFrameAssignmentService();
        PanelCladdingTopologyPayloads masks = Required(keys.EncodeTopology(topology, 1, 1), "legacy masks");
        string config = Required(keys.EncodeFrameConfiguration(topology, 1, 1), "combined masks");
        var legacy = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PanelCladdingKeyService.SegmentMaskKey.ToLowerInvariant()] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey.ToLowerInvariant()] = masks.MergeMask,
            [PanelCladdingKeyService.HideMaskKey.ToLowerInvariant()] = masks.HideMask,
            [PanelCladdingKeyService.LegacyFrameAssignmentsKey.ToLowerInvariant()] = typePayload,
            [PanelCladdingKeyService.LegacyFrameTypologyKey.ToLowerInvariant()] = "RETIRED",
            [PanelCladdingKeyService.FrameConfigKey] = " ",
            [PanelCladdingKeyService.FrameTypeKey] = " "
        };
        PanelCladdingTopologyState loaded = Required(keys.DecodeTopology(legacy, 1, 1), "legacy fallback config");
        Require(Required(keys.EncodeFrameConfiguration(loaded, 1, 1), "re-encode legacy") == config &&
                Required(assignments.Encode(Required(assignments.Decode(legacy, 1, 1, loaded), "legacy type"),
                    1, 1, loaded), "re-encode legacy type") == typePayload,
            "PCpid blank placeholders must not hide legacy masks or assignments.");
        var canonical = new Dictionary<string, string>(legacy)
        {
            [PanelCladdingKeyService.FrameConfigKey] = config,
            [PanelCladdingKeyService.FrameTypeKey] = typePayload,
            [PanelCladdingKeyService.MergeMaskKey.ToLowerInvariant()] = "obsolete-invalid",
            [PanelCladdingKeyService.LegacyFrameAssignmentsKey.ToLowerInvariant()] = "obsolete-invalid"
        };
        Require(keys.DecodeTopology(canonical, 1, 1).Success && assignments.Decode(canonical, 1, 1, topology).Success,
            "Canonical frame values must take precedence over old keys.");
        var invalidConfig = new Dictionary<string, string>(legacy) { [PanelCladdingKeyService.FrameConfigKey] = "not-json" };
        var invalidType = new Dictionary<string, string>(legacy) { [PanelCladdingKeyService.FrameTypeKey] = "not-json" };
        Require(!keys.DecodeTopology(invalidConfig, 1, 1).Success &&
                !assignments.Decode(invalidType, 1, 1, topology).Success,
            "Malformed canonical data must fail instead of restoring legacy values.");
        var conflictConfig = new Dictionary<string, string>(canonical) { [PanelCladdingKeyService.FrameConfigKey.ToLowerInvariant()] = "different" };
        var conflictType = new Dictionary<string, string>(canonical) { [PanelCladdingKeyService.FrameTypeKey.ToLowerInvariant()] = "different" };
        Require(!keys.DecodeTopology(conflictConfig, 1, 1).Success && !assignments.Decode(conflictType, 1, 1, topology).Success,
            "Conflicting case variants must fail.");
        Require(!PanelCladdingKeyService.ValidateFrameAttributeNames(conflictConfig).Success &&
                !PanelCladdingKeyService.ValidateFrameAttributeNames(conflictType).Success &&
                PanelCladdingKeyService.ValidateFrameAttributeNames(canonical).Success,
            "Live snapshots must reject conflicting names before case-insensitive dictionary normalization.");
        Require(!keys.DecodeTopology(new Dictionary<string, string> { [PanelCladdingKeyService.FrameConfigKey] = config }, 2, 1).Success,
            "Combined configuration must still validate grid dimensions.");
        foreach (string malformed in new[] { "{\"v\":2}", "{\"v\":1}", config.Replace("\"v\":1", "\"v\":1,\"v\":1") })
        {
            Require(!keys.DecodeTopology(new Dictionary<string, string> { [PanelCladdingKeyService.FrameConfigKey] = malformed }, 1, 1).Success,
                "Invalid configuration schema was accepted.");
        }
        PanelCladdingLayout layout = BuildLayout(topology, sourceUserText: legacy);
        var repository = new CapturingLiveRepository(layout);
        var service = new PanelCladdingSaveService(repository, new PanelCladdingTypeSignatureService(keys));
        Required(service.Save(new PanelCladdingSaveRequest
        {
            FilePath = layout.DocumentPath, ObjectId = layout.ObjectId,
            ExpectedGeometryFingerprint = layout.GeometryFingerprint,
            HorizontalOffsets = layout.HorizontalOffsets, VerticalOffsets = layout.VerticalOffsets,
            Topology = topology, FrameAssignments = Required(assignments.Decode(typePayload, 1, 1, topology), "legacy state"),
            Scope = PanelCladdingSaveScope.Extrusions
        }), "migrate legacy extrusion save");
        PanelAttributeCommitRequest commit = repository.LastCommit!;
        Require(legacy.Keys.Where(PanelCladdingKeyService.IsRetiredFrameKey)
                    .All(key => commit.UserTextDeletes.Contains(key, StringComparer.OrdinalIgnoreCase)) &&
                commit.UserTextWrites[PanelCladdingKeyService.FrameConfigKey] == config &&
                commit.UserTextWrites[PanelCladdingKeyService.FrameTypeKey] == typePayload,
            "Extrusion save failed to migrate legacy data and delete retired attributes.");
        Console.WriteLine("[OK] New frame keys round-trip; legacy/blank fallback, precedence, malformed/conflict rejection and save migration pass.");
    }

    private static void VerifySavePersistence(PanelCladdingLayout layout, PanelFrameAssignmentState state)
    {
        var repository = new CapturingLiveRepository(layout);
        var keys = new PanelCladdingKeyService();
        var service = new PanelCladdingSaveService(repository, new PanelCladdingTypeSignatureService(keys));
        OperationResponse<PanelCladdingSaveResult> saved = service.Save(new PanelCladdingSaveRequest
        {
            FilePath = layout.DocumentPath,
            ObjectId = layout.ObjectId,
            ExpectedGeometryFingerprint = layout.GeometryFingerprint,
            SystemCode = layout.SystemCode,
            HorizontalOffsets = layout.HorizontalOffsets,
            VerticalOffsets = layout.VerticalOffsets,
            Topology = layout.Topology,
            FrameAssignments = state,
            CellValues = layout.Cells.ToDictionary(
                cell => cell.UserTextKey,
                cell => cell.Value,
                StringComparer.OrdinalIgnoreCase),
            Scope = PanelCladdingSaveScope.Extrusions
        });
        Require(saved.Success && saved.Data is not null, $"Assigned extrusion save failed: {saved.Message}");
        PanelCladdingSaveResult saveResult = saved.Data ??
            throw new InvalidOperationException("Assigned extrusion save returned no result.");
        PanelAttributeCommitRequest commit = repository.LastCommit ??
            throw new InvalidOperationException("Assigned extrusion save did not commit panel attributes.");
        Require(commit.UserTextWrites.ContainsKey(PanelCladdingKeyService.FrameTypeKey),
            "Assigned extrusion save omitted CW_1.09_FRAME_TYPE.");
        Require(commit.UserTextWrites.TryGetValue(PanelCladdingKeyService.FrameConfigKey, out string? storedConfig) &&
                storedConfig == saveResult.FrameConfig &&
                keys.DecodeTopology(commit.UserTextWrites, 1, 1).Success &&
                !commit.UserTextWrites.Keys.Any(PanelCladdingKeyService.IsRetiredFrameKey),
            "Assigned extrusion save must persist config and type without retired keys.");
    }

    private static void VerifyRealScheduleAndWorkbook(string pdfPath)
    {
        OperationResponse<PanelFrameExtrusionScheduleImportResult> imported =
            new PdfFrameExtrusionScheduleImporter().Import(new PanelFrameExtrusionScheduleImportRequest
            {
                PdfPath = pdfPath
            });
        Require(imported.Success && imported.Data is not null, $"Schedule import failed: {imported.Message}");
        PanelFrameExtrusionCatalogItem profile = imported.Data!.Extrusions.Single(item => item.SourceCode == "ALU-H0651");
        Require(profile.Code.Length == 0 && profile.BaseCode == "H0651",
            "ALU-H0651 was not imported as the unconfigured H0651 pool profile.");
        Require(profile.Category == "FRAMING", "Page-one category was not retained as FRAMING.");
        Require(profile.SourcePageNumber == 1 && profile.ThumbnailPng.Length > 1000,
            "ALU-H0651 did not yield a non-empty page-one profile crop.");
        Require(imported.Data.ImportedPageNumbers.SequenceEqual([1, 2, 3, 4, 5, 6]),
            $"Expected all six schedule pages, found {string.Join(',', imported.Data.ImportedPageNumbers)}.");
        Require(imported.Data.Extrusions.Count > 10,
            $"All-page import unexpectedly found only {imported.Data.Extrusions.Count} profiles.");
        Require(new[] { "FRAMING", "HOLLOW", "HOLLOW (SMALL SIZE)", "SOLID", "SOLID (SMALL SIZE)", "ASSEMBLIES" }
                .All(category => imported.Data.Extrusions.Any(item => item.Category == category)),
            "One or more PDF page categories were not retained.");

        string outputDirectory = Path.Combine(AppContext.BaseDirectory, "qa");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(Path.Combine(outputDirectory, "alu-h0651.png"), profile.ThumbnailPng);
        string workbookPath = Path.Combine(outputDirectory, "extrusion-catalog.xlsx");
        IReadOnlyList<PanelFrameExtrusionCatalogItem> configuredCatalog = ConfigureCatalog(imported.Data.Extrusions);
        var workbook = new OpenXmlPanelCladdingWorkbookRepository();
        OperationResponse<IPreparedPanelFrameExtrusionCatalogUpdate> prepared =
            workbook.PrepareFrameExtrusionCatalog(new PanelFrameExtrusionCatalogSaveRequest
            {
                WorkbookPath = workbookPath,
                AllowCreate = true,
                Extrusions = configuredCatalog
            });
        Require(prepared.Success && prepared.Data is not null, $"Workbook prepare failed: {prepared.Message}");
        using IPreparedPanelFrameExtrusionCatalogUpdate update = prepared.Data!;
        Require(update.Commit().Success, "Workbook extrusion catalogue commit failed.");
        PanelFrameExtrusionCatalog catalog = Required(workbook.ReadFrameExtrusionCatalog(workbookPath),
            "read extrusion catalogue");
        Require(catalog.Extrusions.Count == imported.Data.Extrusions.Count &&
                catalog.Extrusions.Single(item => item.SourceCode == profile.SourceCode)
                    .ThumbnailPng.SequenceEqual(profile.ThumbnailPng) &&
                catalog.Extrusions.Count(item => item.Dimension is null) == imported.Data.Extrusions.Count - 4,
            "Extrusions worksheet did not round-trip pool/ready state and thumbnail bytes.");
        Require(catalog.Extrusions.Single(item => item.Code == "1D-H0579").CalculationValue == 2d &&
                catalog.Extrusions.Single(item => item.Code == "1D-H0579").ParentCode == "1D-H0651" &&
                catalog.Extrusions.Single(item => item.Code == "0D-H0652").Calculation == PanelFrameProfileCalculation.FixedQuantity &&
                catalog.Extrusions.Single(item => item.Code == "0D-H0655").Calculation == PanelFrameProfileCalculation.Spacing,
            "Configured 1D/0D catalogue fields did not round-trip.");
        VerifyUi(imported.Data, workbookPath, outputDirectory);
    }

    private static void VerifyBakedCurveAssignments()
    {
        PanelCladdingLayout layout = BuildLayout();
        var state = new PanelFrameAssignmentState
        {
            FrameAssignments = new Dictionary<string, IReadOnlyList<string>>
            {
                ["FRM_0"] = [ExpectedProfile, "1D-H0677"]
            },
            SegmentAssignments =
            [
                new(new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 0), [ExpectedProfile]),
                new(new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 1), [ExpectedProfile])
            ],
            Definitions = Definitions(ExpectedProfile, "1D-H0677")
        };
        var keySet = new PanelCladdingKeySet
        {
            HorizontalOffsets = layout.HorizontalOffsets,
            VerticalOffsets = layout.VerticalOffsets,
            Cells = layout.Cells,
            Topology = layout.Topology,
            FrameAssignments = state
        };
        IReadOnlyList<PanelCladdingExtrusionCurvePlan> curves = Required(
            new PanelCladdingExtrusionPlanningService().CreatePlan(
                "PID_FRAME_01", "CID_FRAME_01", layout.Width, layout.Height, keySet,
                "01_CW Panels::Surfaces-PNL::WT-01"),
            "plan assigned extrusion curves");
        PanelCladdingExtrusionCurvePlan frame = curves.Single(curve => curve.Code == "FRM_0");
        Require(frame.UserTextWrites[PanelCladdingExtrusionPlanningService.AssignedExtrusionsUserTextKey] ==
                "1D-H0651;1D-H0677",
            "Baked frame curve did not receive sorted additive take-off codes.");
        PanelCladdingExtrusionCurvePlan merged = curves.Single(curve =>
            curve.Kind == PanelCladdingExtrusionCurveKind.Merged &&
            curve.Axis == PanelCladdingTopologyAxis.Horizontal);
        Require(merged.AssignedExtrusionCodes.SequenceEqual([ExpectedProfile]) &&
                merged.UserTextWrites[PanelCladdingExtrusionPlanningService.AssignedExtrusionsUserTextKey] == ExpectedProfile,
            "Baked merged curve did not receive its atomic assignment set.");
    }

    private static void VerifyConfiguredProfileFormulas()
    {
        PanelCladdingLayout layout = BuildLayout();
        string[] codes = ["1D-H0579", "1D-H0651", "0D-H0652", "0D-H0655"];
        var definitions = new Dictionary<string, PanelFrameProfileDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [codes[0]] = Definition(codes[0], PanelFrameProfileDimension.OneDimensional,
                PanelFrameProfileCalculation.Length, 2d, "1D-H0651"),
            [codes[1]] = Definition(codes[1], PanelFrameProfileDimension.OneDimensional,
                PanelFrameProfileCalculation.Length, 1d),
            [codes[2]] = Definition(codes[2], PanelFrameProfileDimension.ZeroDimensional,
                PanelFrameProfileCalculation.FixedQuantity, 2d),
            [codes[3]] = Definition(codes[3], PanelFrameProfileDimension.ZeroDimensional,
                PanelFrameProfileCalculation.Spacing, 24d)
        };
        var state = new PanelFrameAssignmentState
        {
            FrameAssignments = new Dictionary<string, IReadOnlyList<string>> { ["FRM_3"] = codes },
            Definitions = definitions,
            CurveModifiers = new Dictionary<string, double> { ["FRM_3"] = 4d }
        };
        var assignments = new PanelFrameAssignmentService();
        string payload = Required(assignments.Encode(state, 1, 1, layout.Topology), "encode configured profiles");
        Require(payload.Contains("\"v\":2", StringComparison.Ordinal), "Configured assignments did not use schema v2.");
        PanelFrameAssignmentState decoded = Required(assignments.Decode(payload, 1, 1, layout.Topology),
            "decode configured profiles");
        var keySet = new PanelCladdingKeySet
        {
            HorizontalOffsets = layout.HorizontalOffsets,
            VerticalOffsets = layout.VerticalOffsets,
            Cells = layout.Cells,
            Topology = layout.Topology,
            FrameAssignments = decoded
        };
        PanelCladdingExtrusionCurvePlan curve = Required(
                new PanelCladdingExtrusionPlanningService().CreatePlan(
                    "PID_FORMULA", "CID_FORMULA", layout.Width, layout.Height, keySet,
                    "01_CW Panels::Surfaces-PNL::WT-01"),
                "plan configured formulas")
            .Single(item => item.Code == "FRM_3");
        Require(curve.UserTextWrites["1D-H0579"] == "(LL+4)*2", "1D quantity formula is incorrect.");
        Require(curve.UserTextWrites["1D-H0651"] == "LL+4", "1D default formula is incorrect.");
        Require(curve.UserTextWrites["0D-H0652"] == "2", "0D fixed formula is incorrect.");
        Require(curve.UserTextWrites["0D-H0655"] == "(LL+4)/24", "0D spacing formula is incorrect.");
    }

    private static void VerifyUi(
        PanelFrameExtrusionScheduleImportResult imported,
        string workbookPath,
        string outputDirectory)
    {
        PanelCladdingLayout layout = BuildLayout(workbookPath: workbookPath);
        var live = new StubLiveRepository(layout);
        var workbook = new OpenXmlPanelCladdingWorkbookRepository();
        var keys = new PanelCladdingKeyService();
        var renderer = new PanelPreviewRenderer();
        var signature = new PanelCladdingTypeSignatureService(keys);
        var save = new PanelCladdingSaveService(live, workbook, renderer, signature);
        var controller = new PanelCladdingEditorController(
            live,
            save,
            renderer,
            signature,
            workbook,
            new PdfFrameExtrusionScheduleImporter());
        var window = new PanelCladdingEditorWindow(controller)
        {
            Width = 1440,
            Height = 900,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        OperationResponse loaded = window.LoadPanel(layout.DocumentPath, layout.ObjectId);
        Require(loaded.Success, $"Extrusion-view UI fixture failed to load: {loaded.Message}");
        FrameworkElement root = (FrameworkElement)window.Content;
        Render(root, 1440, 900, Path.Combine(outputDirectory, "extrusion-assignment-main.png"));
        Button extrusionView = Descendants<Button>(root).Single(button =>
            string.Equals(button.Content?.ToString(), "Extrusion view", StringComparison.Ordinal));
        extrusionView.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Render(root, 1440, 900, Path.Combine(outputDirectory, "extrusion-assignment-main-extrusion-view.png"));
        Require(Descendants<Image>(root).Count(image => image.Source is not null) >= 4,
            "Extrusion view did not render the four configured ready profiles.");
        SelectExtrusions(window, "FRM_0");
        Render(root, 1440, 900, Path.Combine(outputDirectory, "extrusion-assignment-selected.png"));
        var assignedItems = (ItemsControl)window.FindName("SelectedExtrusionAssignments");
        var availableItems = (ItemsControl)window.FindName("ExtrusionLegend");
        PanelExtrusionAssignmentCard assigned = assignedItems.Items
            .Cast<PanelExtrusionAssignmentCard>()
            .Single();
        Require(assigned.Code == ExpectedProfile && assigned.Thumbnail is not null,
            "The assigned-profile card did not retain the configured profile preview.");
        Require(!availableItems.Items.Cast<PanelFrameExtrusion>().Any(item => item.Code == ExpectedProfile),
            "An assigned profile remained visible in Drag Extrusions.");

        InvokePrivate(window, "AssignExtrusionCode", "1D-H0579");
        Require(assignedItems.Items.Cast<PanelExtrusionAssignmentCard>().Any(item => item.Code == "1D-H0579") &&
                !availableItems.Items.Cast<PanelFrameExtrusion>().Any(item => item.Code == "1D-H0579"),
            "A newly assigned profile was not moved from the available catalogue to assignment cards.");
        InvokePrivate(window, "RemoveExtrusionCodeFromSelection", "1D-H0579");
        Require(availableItems.Items.Cast<PanelFrameExtrusion>().Any(item => item.Code == "1D-H0579"),
            "A removed profile did not return to Drag Extrusions.");

        var modifierInput = new TextBox
        {
            DataContext = assignedItems.Items.Cast<PanelExtrusionAssignmentCard>().Single(),
            Text = "+4"
        };
        InvokePrivate(window, "ApplyCurveModifier", modifierInput, false);
        var modifiers = (Dictionary<string, double>)RequiredPrivateField(window, "_frameCurveModifiers");
        Require(modifiers.TryGetValue("FRM_0", out double modifier) && modifier == 4d,
            "The inline assigned-card modifier did not update the selected curve modifier.");

        var setup = new ExtrusionSetupDialog(
            controller,
            workbookPath,
            imported.Extrusions.Select(PanelFrameExtrusion.FromCatalogItem));
        FrameworkElement setupRoot = (FrameworkElement)setup.Content;
        Render(setupRoot, 1220, 820, Path.Combine(outputDirectory, "extrusion-assignment-setup.png"));
        Require(Descendants<Image>(setupRoot).Count(image => image.Source is not null) == imported.Extrusions.Count,
            "Extrusion setup did not render every imported profile image.");
    }

    private static void Render(FrameworkElement root, int width, int height, string path)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        Require(new FileInfo(path).Length > 12_000, $"UI render is unexpectedly empty: {path}");
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

    private static void SelectExtrusions(PanelCladdingEditorWindow window, params string[] ids)
    {
        var selected = (HashSet<string>)RequiredPrivateField(window, "_selectedExtrusions");
        selected.Clear();
        selected.UnionWith(ids);
        InvokePrivate(window, "UpdateExtrusionSelectionUi");
    }

    private static object RequiredPrivateField(object target, string name) =>
        target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(target)
        ?? throw new InvalidOperationException($"Private field {name} was not found.");

    private static object? InvokePrivate(object target, string name, params object[] arguments)
    {
        System.Reflection.MethodInfo method = target.GetType().GetMethod(
                name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Private method {name} was not found.");
        return method.Invoke(target, arguments);
    }

    private static void VerifySourceContracts()
    {
        string root = FindRepositoryRoot();
        string keySource = File.ReadAllText(Path.Combine(root, "src", "PanelCladdingEditor", "Application", "Services", "PanelCladdingKeyService.cs"));
        string plannerSource = File.ReadAllText(Path.Combine(root, "src", "PanelCladdingEditor", "Application", "Services", "PanelCladding", "PanelCladdingExtrusionPlanningService.cs"));
        string syncSource = File.ReadAllText(Path.Combine(root, "src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live", "PanelCladding", "LivePanelCladdingSurfaceSyncRepository.cs"));
        Require(keySource.Contains("CW_1.08_FRAME_CONFIG", StringComparison.Ordinal),
            "Exact frame configuration Rhino key is absent.");
        Require(keySource.Contains("CW_1.09_FRAME_TYPE", StringComparison.Ordinal),
            "Frame assignment persistence key is absent.");
        Require(plannerSource.Contains("AssignedExtrusionsUserTextKey = \"Extrusions\"", StringComparison.Ordinal),
            "Baked curves do not expose the extrusion take-off user-text key.");
        Require(syncSource.Contains("curveWrite.DesiredAssignedExtrusions", StringComparison.Ordinal),
            "PCSyncCrv does not propagate updated extrusion take-off codes.");
    }

    private static PanelCladdingLayout BuildLayout(
        PanelCladdingTopologyState? topology = null,
        string workbookPath = "",
        IReadOnlyDictionary<string, string>? sourceUserText = null) => new()
    {
        SourceUserText = sourceUserText ?? new Dictionary<string, string>(),
        ObjectId = Guid.Parse("A8200000-0000-0000-0000-000000000901"),
        DocumentPath = "C:\\Temp\\frame-typology.3dm",
        GeometryFingerprint = "FRAME-TYPOLOGY-SMOKE",
        GeometryClass = PanelGeometryClass.Planar,
        Width = 2000,
        Height = 1600,
        ModelTolerance = 0.01,
        SystemCode = "CW01",
        HorizontalOffsets = [800],
        VerticalOffsets = [1000],
        Cells =
        [
            new PanelCladdingCell { Column = 0, Row = 0, RowLabel = "A", ShortLabel = "0A", UserTextKey = "CW_1.09_CLAD MAT_A0" },
            new PanelCladdingCell { Column = 1, Row = 0, RowLabel = "A", ShortLabel = "1A", UserTextKey = "CW_1.09_CLAD MAT_A1" },
            new PanelCladdingCell { Column = 0, Row = 1, RowLabel = "B", ShortLabel = "0B", UserTextKey = "CW_1.09_CLAD MAT_B0" },
            new PanelCladdingCell { Column = 1, Row = 1, RowLabel = "B", ShortLabel = "1B", UserTextKey = "CW_1.09_CLAD MAT_B1" }
        ],
        Topology = topology ?? new PanelCladdingTopologyState
        {
            MergeRuns =
            [
                new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Horizontal, 0, 0, 1)
            ]
        },
        FrameAssignments = new PanelFrameAssignmentState
        {
            FrameAssignments = new Dictionary<string, IReadOnlyList<string>>
            {
                ["FRM_0"] = [ExpectedProfile]
            }
        },
        WorkbookPath = workbookPath
    };

    private static IReadOnlyDictionary<string, PanelFrameProfileDefinition> Definitions(params string[] codes) =>
        codes.ToDictionary(
            code => code,
            code => Definition(code, PanelFrameProfileDimension.OneDimensional,
                PanelFrameProfileCalculation.Length, 1d),
            StringComparer.OrdinalIgnoreCase);

    private static PanelFrameProfileDefinition Definition(
        string code,
        PanelFrameProfileDimension dimension,
        PanelFrameProfileCalculation calculation,
        double value,
        string parentCode = "") => new()
    {
        Code = code,
        BaseCode = code[3..],
        SourceCode = $"ALU-{code[3..]}",
        Category = "FRAMING",
        Dimension = dimension,
        Calculation = calculation,
        CalculationValue = value,
        ParentCode = parentCode
    };

    private static IReadOnlyList<PanelFrameExtrusionCatalogItem> ConfigureCatalog(
        IReadOnlyList<PanelFrameExtrusionCatalogItem> imported) => imported.Select(item => item.BaseCode switch
    {
        "H0579" => Configured(item, PanelFrameProfileDimension.OneDimensional,
            PanelFrameProfileCalculation.Length, 2d, "1D-H0651"),
        "H0651" => Configured(item, PanelFrameProfileDimension.OneDimensional,
            PanelFrameProfileCalculation.Length, 1d),
        "H0652" => Configured(item, PanelFrameProfileDimension.ZeroDimensional,
            PanelFrameProfileCalculation.FixedQuantity, 2d),
        "H0655" => Configured(item, PanelFrameProfileDimension.ZeroDimensional,
            PanelFrameProfileCalculation.Spacing, 24d),
        _ => item
    }).ToArray();

    private static PanelFrameExtrusionCatalogItem Configured(
        PanelFrameExtrusionCatalogItem item,
        PanelFrameProfileDimension dimension,
        PanelFrameProfileCalculation calculation,
        double value,
        string parentCode = "") => new()
    {
        Code = $"{(dimension == PanelFrameProfileDimension.OneDimensional ? "1D" : "0D")}-{item.BaseCode}",
        BaseCode = item.BaseCode,
        SourceCode = item.SourceCode,
        Description = item.Description,
        Category = item.Category,
        Dimension = dimension,
        Calculation = calculation,
        CalculationValue = value,
        ParentCode = parentCode,
        SourcePdfPath = item.SourcePdfPath,
        SourcePageNumber = item.SourcePageNumber,
        ThumbnailPng = item.ThumbnailPng
    };

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static T Required<T>(OperationResponse<T> response, string action) where T : class =>
        response.Success && response.Data is not null
            ? response.Data
            : throw new InvalidOperationException($"{action} failed: {response.Message}");

    private static string Required(OperationResponse<string> response, string action) =>
        response.Success && response.Data is not null
            ? response.Data
            : throw new InvalidOperationException($"{action} failed: {response.Message}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class StubLiveRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used by extrusion assignment smoke.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit) =>
            OperationResponse<PanelAttributeCommitResult>.Fail("Not used by extrusion assignment smoke.");

        public OperationResponse<string> SetWorkbookPath(string filePath, string path) =>
            OperationResponse<string>.Ok(path);
    }

    private sealed class StubClearPrompt(params bool[] responses) : IExtrusionCatalogueClearPrompt
    {
        private readonly Queue<bool> _responses = new(responses);
        public int CallCount { get; private set; }
        public int ProfileCount { get; private set; }
        public Window? Owner { get; private set; }
        public bool ConfirmClear(Window owner, int profileCount)
        {
            CallCount++;
            Owner = owner;
            ProfileCount = profileCount;
            return _responses.Dequeue();
        }
    }

    private sealed class StubScheduleImporter(Func<string, PanelFrameExtrusionCatalogItem> profileFactory)
        : IPanelFrameExtrusionScheduleImporter
    {
        public string? LastPath { get; private set; }
        public OperationResponse<PanelFrameExtrusionScheduleImportResult> Import(PanelFrameExtrusionScheduleImportRequest request)
        {
            LastPath = request.PdfPath;
            return OperationResponse<PanelFrameExtrusionScheduleImportResult>.Ok(new()
            {
                PdfPath = request.PdfPath, ImportedPageNumbers = [1], Extrusions = [profileFactory(request.PdfPath)]
            });
        }
    }

    private sealed class CapturingLiveRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public PanelAttributeCommitRequest? LastCommit { get; private set; }

        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used by extrusion assignment smoke.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit)
        {
            LastCommit = request;
            OperationResponse finalized = finalizeExternalCommit();
            return finalized.Success
                ? OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
                {
                    ObjectId = request.ObjectId,
                    Mutated = true
                })
                : OperationResponse<PanelAttributeCommitResult>.Fail(finalized.Message);
        }

        public OperationResponse<string> SetWorkbookPath(string filePath, string path) =>
            OperationResponse<string>.Ok(path);
    }
}

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
        string pdfPath = args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("PANEL_EXTRUSION_SCHEDULE_PDF") ?? string.Empty;
        VerifyAssignmentSerializationAndFrameTypology();
        VerifyBakedCurveAssignments();
        VerifyConfiguredProfileFormulas();
        VerifySourceContracts();
        if (!string.IsNullOrWhiteSpace(pdfPath))
        {
            VerifyRealScheduleAndWorkbook(pdfPath);
        }
        Console.WriteLine("[OK] additive 1D/0D assignments serialize deterministically and validate merge runs.");
        Console.WriteLine("[OK] LL modifiers, quantities, fixed counts, and spacing formulas are correct.");
        Console.WriteLine("[OK] frame typology responds to topology masks and assigned extrusion codes.");
        Console.WriteLine("[OK] exact Rhino keys and baked-curve take-off key are present in production sources.");
        if (!string.IsNullOrWhiteSpace(pdfPath))
        {
            Console.WriteLine("[OK] real all-page schedule import and Extrusions worksheet round-trip passed.");
        }
        return 0;
    }

    private static void VerifyAssignmentSerializationAndFrameTypology()
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

        var typology = new PanelFrameTypologyService(keys, assignments);
        PanelFrameTypologyIdentity first = Required(typology.Create(layout, state, "CW01"), "create frame typology");
        Require(first.TypologyCode.StartsWith("CW01-2X2-", StringComparison.Ordinal),
            $"Unexpected frame typology format: {first.TypologyCode}");
        var changedAssignments = new PanelFrameAssignmentState
        {
            FrameAssignments = state.FrameAssignments,
            SegmentAssignments = state.SegmentAssignments.Select((item, index) => index == 2
                ? item with { Codes = ["1D-H0999"] }
                : item).ToArray(),
            Definitions = Definitions(ExpectedProfile, "1D-H0677", "1D-H0999")
        };
        PanelFrameTypologyIdentity second = Required(typology.Create(layout, changedAssignments, "CW01"),
            "create changed frame typology");
        Require(first.TypologyCode != second.TypologyCode,
            "Changing a main-frame extrusion code did not change frame typology.");

        PanelCladdingLayout hiddenLayout = BuildLayout(new PanelCladdingTopologyState
        {
            MergeRuns = layout.Topology.MergeRuns,
            HiddenSegments =
            [
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, 0, 0)
            ]
        });
        PanelFrameTypologyIdentity hidden = Required(typology.Create(hiddenLayout, state, "CW01"),
            "create hidden frame typology");
        Require(first.TypologyCode != hidden.TypologyCode,
            "Changing a hide mask did not change frame typology.");
        PanelFrameTypologyIdentity unmerged = Required(typology.Create(
                BuildLayout(new PanelCladdingTopologyState()),
                state,
                "CW01"),
            "create unmerged frame typology");
        Require(first.TypologyCode != unmerged.TypologyCode,
            "Changing a merge mask did not change frame typology.");
        PanelFrameTypologyIdentity segmented = Required(typology.Create(
                BuildLayout(new PanelCladdingTopologyState
                {
                    MergeRuns = layout.Topology.MergeRuns,
                    MissingSegments =
                    [
                        new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, 0, 1)
                    ]
                }),
                state,
                "CW01"),
            "create segmented frame typology");
        Require(first.TypologyCode != segmented.TypologyCode,
            "Changing a segment mask did not change frame typology.");

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
        Require(commit.UserTextWrites.ContainsKey(PanelCladdingKeyService.FrameAssignmentsKey),
            "Assigned extrusion save omitted CW_2.09_FRAME_ASSIGNMENTS.");
        Require(commit.UserTextWrites.TryGetValue(PanelCladdingKeyService.FrameTypologyKey, out string? storedTypology) &&
                storedTypology == saveResult.FrameTypology &&
                storedTypology.StartsWith("CW01-2X2-", StringComparison.Ordinal),
            "Assigned extrusion save omitted or mismatched CW_1.5D_FRAME TYPOLOGY.");
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
                "PID_FRAME_01", "CID_FRAME_01", layout.Width, layout.Height, keySet),
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
                    "PID_FORMULA", "CID_FORMULA", layout.Width, layout.Height, keySet),
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
        Require(keySource.Contains("CW_1.5D_FRAME TYPOLOGY", StringComparison.Ordinal),
            "Exact frame typology Rhino key is absent.");
        Require(keySource.Contains("CW_2.09_FRAME_ASSIGNMENTS", StringComparison.Ordinal),
            "Frame assignment persistence key is absent.");
        Require(plannerSource.Contains("AssignedExtrusionsUserTextKey = \"Extrusions\"", StringComparison.Ordinal),
            "Baked curves do not expose the extrusion take-off user-text key.");
        Require(syncSource.Contains("curveWrite.DesiredAssignedExtrusions", StringComparison.Ordinal),
            "PCSyncCrv does not propagate updated extrusion take-off codes.");
    }

    private static PanelCladdingLayout BuildLayout(
        PanelCladdingTopologyState? topology = null,
        string workbookPath = "") => new()
    {
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

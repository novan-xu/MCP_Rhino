using System.Reflection;
using DocumentFormat.OpenXml.Packaging;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace PanelCladdingSurfaceSyncSmoke;

internal static class Program
{
    private static readonly Guid PanelOneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PanelTwoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly string[] FinalMaterials = { "GL01", "GL02", "STN02", "TER01" };

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingSurfaceSyncPlanningService(keys);
        PanelCladdingSurfaceSyncSnapshot snapshot = BuildTwoPanelSnapshot();
        PanelCladdingSurfaceSyncPlan plan = RequireData(
            planner.CreatePlan(snapshot),
            "Create two-panel surface sync plan");
        Require(plan.Panels.Count == 2 && plan.Surfaces.Count == 8,
            "Two selected 2x2 panels must map to eight surfaces.");
        Require(plan.Panels.All(panel => panel.CladdingChanged),
            "Both panel configurations should be changed by their surface layers.");
        Require(plan.Surfaces.Count(surface => surface.CladdingKeyChanged) == 2,
            "Exactly two stale surface Cladding values should be refreshed.");
        foreach (PanelCladdingSurfaceSyncPanelPlan panel in plan.Panels)
        {
            string[] values = panel.Layout.Cells
                .OrderBy(cell => cell.Column)
                .ThenBy(cell => cell.Row)
                .Select(cell => panel.CellValues[cell.UserTextKey])
                .ToArray();
            Require(values.SequenceEqual(FinalMaterials),
                $"Layer-derived panel material mapping failed for {panel.PanelId}.");
        }
        Require(plan.Surfaces.All(surface =>
                surface.DesiredCid == PanelCladdingSpawnPlanningService.BuildSurfaceCid(
                    surface.PanelId,
                    CellLabelForKey(plan, surface.CellKey))),
            "Every mapped surface CID must use the shared PID-to-CID derivation plus cell label.");
        Require(plan.Surfaces.Any(surface => surface.DesiredCid.StartsWith("CID_", StringComparison.Ordinal)),
            "Prefixed production PIDs must map to CID_-prefixed surface identifiers.");
        Console.WriteLine("[OK] exact PID/CID mapping, layer authority, surface refresh, and panel change detection");

        VerifySurfaceOnlyRefresh(planner);
        Console.WriteLine("[OK] stale surface key refresh without unnecessary panel type regeneration");

        VerifyIsolatedMappings(planner, snapshot);
        Console.WriteLine("[OK] mapping failures are isolated to their owning panels");

        VerifyPartialBatchPlanning(planner, snapshot);
        Console.WriteLine("[OK] valid panels continue when another selected panel has a missing CID");

        VerifyWorkflow(keys, snapshot);
        Console.WriteLine("[OK] end-to-end orchestration, writes, workbook commit, and no-change model pruning");

        VerifyBatchWorkbook(keys, plan);
        Console.WriteLine("[OK] workbook reuse plus managed-type pruning with unrelated-sheet preservation");

        VerifyAssemblyContract();
        Console.WriteLine("[OK] standalone sync command/services, batch contract, and unique GUID");
    }

    private static void VerifySurfaceOnlyRefresh(PanelCladdingSurfaceSyncPlanningService planner)
    {
        PanelCladdingLayout layout = BuildLayout(PanelOneId, FinalMaterials);
        PanelCladdingSurfaceSyncSurfaceSnapshot[] surfaces = BuildSurfaces("PANEL-01", FinalMaterials)
            .Select((surface, index) => index == 0
                ? Surface(surface.ObjectId, surface.PanelId, surface.Cid, surface.LayerPath, "gl01")
                : surface)
            .ToArray();
        PanelCladdingSurfaceSyncPlan plan = RequireData(
            planner.CreatePlan(Snapshot(
                new[] { Panel(PanelOneId, "PANEL-01", layout) },
                surfaces)),
            "Create surface-only refresh plan");
        Require(!plan.Panels[0].CladdingChanged,
            "Normalized-equal panel cells must not regenerate a type.");
        Require(plan.Surfaces.Count(surface => surface.CladdingKeyChanged) == 1,
            "Noncanonical surface Cladding text should be refreshed from its layer.");
    }

    private static void VerifyIsolatedMappings(
        PanelCladdingSurfaceSyncPlanningService planner,
        PanelCladdingSurfaceSyncSnapshot source)
    {
        PanelCladdingSurfaceSyncPanelSnapshot panel = source.Panels[0];
        PanelCladdingSurfaceSyncSurfaceSnapshot[] own = source.Surfaces
            .Where(surface => surface.PanelId == panel.PanelId)
            .ToArray();

        RequireIsolatedIssue(
            planner.CreatePlan(Snapshot(new[] { panel }, own.Skip(1).ToArray())),
            "SURFACE_MISSING");
        RequireIsolatedIssue(
            planner.CreatePlan(Snapshot(new[] { panel }, own.Concat(new[]
            {
                Surface(Guid.NewGuid(), own[0].PanelId, own[0].Cid, own[0].LayerPath, own[0].CladdingValue)
            }).ToArray())),
            "DUPLICATE_CID");
        RequireIsolatedIssue(
            planner.CreatePlan(Snapshot(new[] { panel }, own.Concat(new[]
            {
                Surface(Guid.NewGuid(), panel.PanelId,
                    PanelCladdingSpawnPlanningService.BuildSurfaceCid(panel.PanelId, "9Z"),
                    MaterialLayer("GL01"), "GL01")
            }).ToArray())),
            "UNEXPECTED_CID");

        PanelCladdingSurfaceSyncSurfaceSnapshot wrongPid = Surface(
            own[0].ObjectId,
            "OTHER-PANEL",
            own[0].Cid,
            own[0].LayerPath,
            own[0].CladdingValue);
        RequireIsolatedIssue(
            planner.CreatePlan(Snapshot(new[] { panel }, new[] { wrongPid }.Concat(own.Skip(1)).ToArray())),
            "SURFACE_PID_MISMATCH");

        PanelCladdingSurfaceSyncSurfaceSnapshot wrongRoot = Surface(
            own[0].ObjectId,
            own[0].PanelId,
            own[0].Cid,
            "Manual::Surfaces-Glass::GL01",
            own[0].CladdingValue);
        RequireIsolatedIssue(
            planner.CreatePlan(Snapshot(new[] { panel }, new[] { wrongRoot }.Concat(own.Skip(1)).ToArray())),
            "LAYER_INVALID");

        PanelCladdingSurfaceSyncSurfaceSnapshot wrongFamily = Surface(
            own[0].ObjectId,
            own[0].PanelId,
            own[0].Cid,
            "03_Material Surfaces (STEP)::Surfaces-Metal::GL01",
            own[0].CladdingValue);
        RequireIsolatedIssue(
            planner.CreatePlan(Snapshot(new[] { panel }, new[] { wrongFamily }.Concat(own.Skip(1)).ToArray())),
            "MATERIAL_FAMILY_MISMATCH");

        RequireIsolatedIssue(
            planner.CreatePlan(Snapshot(
                new[]
                {
                    panel,
                    Panel(PanelTwoId, panel.PanelId, BuildLayout(PanelTwoId, FinalMaterials))
                },
                own)),
            "DUPLICATE_SELECTED_PID",
            expectedIssueCount: 2);

        PanelCladdingSurfaceSyncPlan readFailurePlan = RequireData(
            planner.CreatePlan(new PanelCladdingSurfaceSyncSnapshot
            {
                DocumentPath = source.DocumentPath,
                SelectedPanelIds = new[] { PanelOneId },
                Issues = new[]
                {
                    new PanelCladdingSurfaceSyncIssue
                    {
                        PanelObjectId = PanelOneId,
                        Message = $"PANEL_CLADDING_SURFACE_SYNC_PANEL_READ_FAILED: {PanelOneId:D}"
                    }
                }
            }),
            "Create all-skipped read-failure plan");
        Require(readFailurePlan.Panels.Count == 0 && readFailurePlan.Surfaces.Count == 0 &&
                readFailurePlan.Issues.Count == 1,
            "An all-skipped selection must complete with no mutation plans and one issue.");
    }

    private static void VerifyPartialBatchPlanning(
        PanelCladdingSurfaceSyncPlanningService planner,
        PanelCladdingSurfaceSyncSnapshot source)
    {
        PanelCladdingSurfaceSyncPanelSnapshot badPanel = source.Panels[0];
        PanelCladdingSurfaceSyncPanelSnapshot goodPanel = source.Panels[1];
        string missingCid = PanelCladdingSpawnPlanningService.BuildSurfaceCid(
            badPanel.PanelId,
            badPanel.Layout.Cells[0].ShortLabel);
        PanelCladdingSurfaceSyncPlan plan = RequireData(
            planner.CreatePlan(Snapshot(
                source.Panels,
                source.Surfaces.Where(surface => !string.Equals(
                    surface.Cid,
                    missingCid,
                    StringComparison.OrdinalIgnoreCase)).ToArray())),
            "Create partial-batch plan");
        Require(plan.Panels.Count == 1 && plan.Panels[0].ObjectId == goodPanel.ObjectId,
            "Only the fully mapped panel should receive a panel plan.");
        Require(plan.Surfaces.Count == goodPanel.Layout.Cells.Count &&
                plan.Surfaces.All(surface => surface.PanelObjectId == goodPanel.ObjectId),
            "A skipped panel must not contribute partial surface writes.");
        Require(plan.Issues.Count == 1 && plan.Issues[0].PanelObjectId == badPanel.ObjectId &&
                plan.Issues[0].Message.Contains("SURFACE_MISSING", StringComparison.Ordinal),
            "The missing-CID panel must be returned as the sole skipped issue.");
    }

    private static void VerifyBatchWorkbook(
        PanelCladdingKeyService keys,
        PanelCladdingSurfaceSyncPlan plan)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"PanelCladdingSurfaceSyncSmoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string workbookPath = Path.Combine(directory, "Typology.xlsx");
        try
        {
            var renderer = new PanelPreviewRenderer();
            var signatures = new PanelCladdingTypeSignatureService(keys);
            var items = new List<PanelCladdingWorkbookUpsert>();
            foreach (PanelCladdingSurfaceSyncPanelPlan panel in plan.Panels)
            {
                PanelCladdingTypeIdentity identity = RequireData(
                    signatures.Create(panel.Layout, panel.CellValues, "WT01"),
                    "Create sync identity");
                byte[] preview = RequireData(renderer.RenderPng(panel.Layout, 480, 320), "Render preview");
                items.Add(new PanelCladdingWorkbookUpsert
                {
                    WorkbookPath = workbookPath,
                    Layout = panel.Layout,
                    Identity = identity,
                    PreviewPng = preview,
                    AllowCreate = true
                });
            }

            var workbook = new OpenXmlPanelCladdingWorkbookRepository();
            PanelCladdingWorkbookCommitResult retainedResult;
            using (IPreparedPanelCladdingWorkbookBatchUpdate prepared = RequireData(
                workbook.PrepareBatchUpsert(new PanelCladdingWorkbookBatchUpsert
                {
                    WorkbookPath = workbookPath,
                    AllowCreate = true,
                    Items = items
                }),
                "Prepare workbook batch"))
            {
                Require(prepared.Results.Count == 2, "Workbook batch must return one result per panel.");
                Require(prepared.Results[0].Result.Identity.TypeCode ==
                        prepared.Results[1].Result.Identity.TypeCode &&
                        prepared.Results[0].Result.SheetName == prepared.Results[1].Result.SheetName,
                    "Equal signatures in one batch must share the same type and sheet.");
                Require(!prepared.Results[0].Result.ReusedExistingType &&
                        prepared.Results[1].Result.ReusedExistingType,
                    "The second equal signature must reuse the first in-batch type.");
                retainedResult = prepared.Results[0].Result;
                Require(prepared.Commit().Success, "Workbook batch commit failed.");
            }
            Require(File.Exists(workbookPath) && new FileInfo(workbookPath).Length > 0,
                "Committed typology workbook is missing or empty.");

            PanelCladdingWorkbookUpsert reopenItem = items[0];
            using IPreparedPanelCladdingWorkbookBatchUpdate reopened = RequireData(
                workbook.PrepareBatchUpsert(new PanelCladdingWorkbookBatchUpsert
                {
                    WorkbookPath = workbookPath,
                    AllowCreate = false,
                    Items = new[] { reopenItem }
                }),
                "Reopen workbook batch");
            Require(reopened.Results[0].Result.ReusedExistingType,
                "Existing workbook signature must be reused on a later batch.");
            Require(reopened.Commit().Success, "No-op workbook reuse commit failed.");

            PanelCladdingSurfaceSyncPanelPlan sourcePanel = plan.Panels[0];
            var unusedValues = new Dictionary<string, string>(
                sourcePanel.CellValues,
                StringComparer.OrdinalIgnoreCase)
            {
                [sourcePanel.Layout.Cells[0].UserTextKey] = "GL99"
            };
            PanelCladdingTypeIdentity unusedIdentity = RequireData(
                signatures.Create(sourcePanel.Layout, unusedValues, "WT01"),
                "Create unused workbook identity");
            PanelCladdingWorkbookCommitResult unusedResult;
            using (IPreparedPanelCladdingWorkbookBatchUpdate addUnused = RequireData(
                workbook.PrepareBatchUpsert(new PanelCladdingWorkbookBatchUpsert
                {
                    WorkbookPath = workbookPath,
                    AllowCreate = false,
                    Items = new[]
                    {
                        new PanelCladdingWorkbookUpsert
                        {
                            WorkbookPath = workbookPath,
                            Layout = sourcePanel.Layout,
                            Identity = unusedIdentity,
                            PreviewPng = items[0].PreviewPng,
                            AllowCreate = false
                        }
                    }
                }),
                "Add unused workbook type"))
            {
                unusedResult = addUnused.Results[0].Result;
                Require(addUnused.Commit().Success, "Unused workbook type commit failed.");
            }
            AddUnrelatedWorksheet(workbookPath, "Project Notes");
            using (IPreparedPanelCladdingWorkbookBatchUpdate prune = RequireData(
                workbook.PrepareBatchUpsert(new PanelCladdingWorkbookBatchUpsert
                {
                    WorkbookPath = workbookPath,
                    AllowCreate = false,
                    Items = Array.Empty<PanelCladdingWorkbookUpsert>(),
                    PruneUnusedTypes = true,
                    RetainedTypes = new[]
                    {
                        new PanelCladdingWorkbookTypeReference
                        {
                            TypeCode = retainedResult.Identity.TypeCode,
                            StoredSignature = retainedResult.Identity.StoredSignature
                        }
                    }
                }),
                "Prepare unused workbook type pruning"))
            {
                Require(prune.Results.Count == 0,
                    "A prune-only batch must not invent upsert results.");
                Require(prune.RemovedTypeCodes.SequenceEqual(new[] { unusedResult.Identity.TypeCode }),
                    "Prune-only batch did not report the unused managed type.");
                Require(prune.Commit().Success, "Unused workbook type pruning commit failed.");
            }
            VerifyPrunedWorkbook(
                workbookPath,
                retainedResult.SheetName,
                unusedResult.SheetName,
                retainedResult.Identity.TypeCode);

            RequireFailure(
                workbook.PrepareBatchUpsert(new PanelCladdingWorkbookBatchUpsert
                {
                    WorkbookPath = workbookPath,
                    AllowCreate = false,
                    Items = new[] { reopenItem, reopenItem }
                }),
                "BATCH_OBJECT_IDS_INVALID");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void VerifyWorkflow(
        PanelCladdingKeyService keys,
        PanelCladdingSurfaceSyncSnapshot snapshot)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"PanelCladdingSurfaceSyncWorkflow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string workbookPath = Path.Combine(directory, "Typology.xlsx");
        try
        {
            var live = new CapturingLiveRepository(snapshot);
            IPanelCladdingSurfaceSyncService service = new PanelCladdingSurfaceSyncService(
                live,
                new OpenXmlPanelCladdingWorkbookRepository(),
                new PanelPreviewRenderer(),
                new PanelCladdingTypeSignatureService(keys),
                new PanelCladdingSurfaceSyncPlanningService(keys));
            PanelCladdingSurfaceSyncResult result = RequireData(
                service.Sync(
                    snapshot.DocumentPath,
                    snapshot.Panels.Select(panel => panel.ObjectId).ToArray(),
                    workbookPath,
                    allowCreateWorkbook: true),
                "Run surface sync workflow");
            PanelCladdingSurfaceSyncCommitRequest request = live.LastCommitRequest ??
                throw new InvalidOperationException("Live commit was not invoked.");
            Require(request.PanelWrites.Count == 2 && request.SurfaceWrites.Count == 2,
                "Workflow must commit two changed panels and two stale surface keys.");
            Require(request.PanelWrites.All(write =>
                    write.CellValues.Values.SequenceEqual(FinalMaterials) &&
                    write.HorizontalOffsets.SequenceEqual(snapshot.Panels[0].Layout.HorizontalOffsets) &&
                    write.VerticalOffsets.SequenceEqual(snapshot.Panels[0].Layout.VerticalOffsets) &&
                    !string.IsNullOrWhiteSpace(write.TypeCode) &&
                    write.StoredSignature.StartsWith("v3:sha256:", StringComparison.Ordinal)),
                "Final panel writes must carry normalized cells, type codes, and signatures.");
            Require(result.MatchedSurfaceCount == 8 && result.ChangedPanelIds.Count == 2 &&
                    result.RefreshedSurfaceIds.Count == 2 && result.Types.Count == 2,
                "Workflow result counts are incorrect.");
            Require(result.Types.Select(type => type.TypeCode).Distinct().Count() == 1,
                "Equal final configurations must reuse one type in the workflow batch.");
            Require(File.Exists(workbookPath), "Workflow did not commit the typology workbook.");

            PanelCladdingSurfaceSyncTypeResult usedType = result.Types[0];
            PanelCladdingLayout unusedLayout = BuildLayout(PanelOneId, FinalMaterials);
            var unusedValues = unusedLayout.Cells.ToDictionary(
                cell => cell.UserTextKey,
                cell => cell.Value,
                StringComparer.OrdinalIgnoreCase);
            unusedValues[unusedLayout.Cells[0].UserTextKey] = "GL99";
            PanelCladdingTypeIdentity unusedIdentity = RequireData(
                new PanelCladdingTypeSignatureService(keys).Create(unusedLayout, unusedValues, "WT01"),
                "Create workflow unused identity");
            string unusedTypeCode;
            var workbookRepository = new OpenXmlPanelCladdingWorkbookRepository();
            using (IPreparedPanelCladdingWorkbookBatchUpdate addUnused = RequireData(
                workbookRepository.PrepareBatchUpsert(new PanelCladdingWorkbookBatchUpsert
                {
                    WorkbookPath = workbookPath,
                    AllowCreate = false,
                    Items = new[]
                    {
                        new PanelCladdingWorkbookUpsert
                        {
                            WorkbookPath = workbookPath,
                            Layout = unusedLayout,
                            Identity = unusedIdentity,
                            PreviewPng = RequireData(
                                new PanelPreviewRenderer().RenderPng(unusedLayout, 480, 320),
                                "Render workflow unused preview"),
                            AllowCreate = false
                        }
                    }
                }),
                "Add workflow unused type"))
            {
                unusedTypeCode = addUnused.Results[0].Result.Identity.TypeCode;
                Require(addUnused.Commit().Success, "Workflow unused type commit failed.");
            }

            PanelCladdingSurfaceSyncPanelSnapshot[] unchangedPanels =
            {
                Panel(PanelOneId, "PID_PANEL_01", BuildLayout(PanelOneId, FinalMaterials)),
                Panel(PanelTwoId, "PID_PANEL_02", BuildLayout(PanelTwoId, FinalMaterials))
            };
            PanelCladdingSurfaceSyncSnapshot unchangedSnapshot = Snapshot(
                unchangedPanels,
                BuildSurfaces("PID_PANEL_01", FinalMaterials)
                    .Concat(BuildSurfaces("PID_PANEL_02", FinalMaterials))
                    .ToArray(),
                unchangedPanels.Select(panel => new PanelCladdingWorkbookTypeReference
                {
                    ObjectId = panel.ObjectId,
                    TypeCode = usedType.TypeCode,
                    StoredSignature = usedType.StoredSignature
                }).ToArray());
            var pruneLive = new CapturingLiveRepository(unchangedSnapshot);
            IPanelCladdingSurfaceSyncService pruneService = new PanelCladdingSurfaceSyncService(
                pruneLive,
                workbookRepository,
                new PanelPreviewRenderer(),
                new PanelCladdingTypeSignatureService(keys),
                new PanelCladdingSurfaceSyncPlanningService(keys));
            PanelCladdingSurfaceSyncResult pruneResult = RequireData(
                pruneService.Sync(
                    unchangedSnapshot.DocumentPath,
                    unchangedSnapshot.SelectedPanelIds,
                    workbookPath,
                    allowCreateWorkbook: false),
                "Run no-change workbook-pruning workflow");
            PanelCladdingSurfaceSyncCommitRequest pruneRequest = pruneLive.LastCommitRequest ??
                throw new InvalidOperationException("No-change pruning did not invoke live commit.");
            Require(pruneRequest.PanelWrites.Count == 0 && pruneRequest.SurfaceWrites.Count == 0,
                "No-change pruning must not invent Rhino attribute writes.");
            Require(pruneResult.RemovedWorkbookTypeCodes.SequenceEqual(new[] { unusedTypeCode }),
                "No-change sync did not remove the workbook type unused by the Rhino model.");

            PanelCladdingSurfaceSyncPanelSnapshot badPanel = snapshot.Panels[0];
            string missingCid = PanelCladdingSpawnPlanningService.BuildSurfaceCid(
                badPanel.PanelId,
                badPanel.Layout.Cells[0].ShortLabel);
            PanelCladdingSurfaceSyncSnapshot partialSnapshot = Snapshot(
                snapshot.Panels,
                snapshot.Surfaces.Where(surface => !string.Equals(
                    surface.Cid,
                    missingCid,
                    StringComparison.OrdinalIgnoreCase)).ToArray());
            var partialLive = new CapturingLiveRepository(partialSnapshot);
            IPanelCladdingSurfaceSyncService partialService = new PanelCladdingSurfaceSyncService(
                partialLive,
                new OpenXmlPanelCladdingWorkbookRepository(),
                new PanelPreviewRenderer(),
                new PanelCladdingTypeSignatureService(keys),
                new PanelCladdingSurfaceSyncPlanningService(keys));
            string partialWorkbookPath = Path.Combine(directory, "Typology-partial.xlsx");
            PanelCladdingSurfaceSyncResult partialResult = RequireData(
                partialService.Sync(
                    partialSnapshot.DocumentPath,
                    partialSnapshot.SelectedPanelIds,
                    partialWorkbookPath,
                    allowCreateWorkbook: true),
                "Run partial surface sync workflow");
            PanelCladdingSurfaceSyncCommitRequest partialRequest = partialLive.LastCommitRequest ??
                throw new InvalidOperationException("Partial live commit was not invoked.");
            Require(partialRequest.PanelWrites.Count == 1 && partialRequest.SurfaceWrites.Count == 1,
                "The valid panel must still commit its panel and stale-surface writes.");
            Require(partialRequest.SkippedPanelIds.SequenceEqual(new[] { badPanel.ObjectId }) &&
                    partialRequest.Issues.Count == 1,
                "The skipped panel and issue must reach the live commit request.");
            Require(partialResult.SkippedPanelIds.SequenceEqual(new[] { badPanel.ObjectId }) &&
                    partialResult.Issues.Count == 1 && partialResult.MatchedSurfaceCount == 4,
                "The partial workflow result must expose skipped-panel details and valid matches.");
            Require(File.Exists(partialWorkbookPath),
                "The valid panel's typology workbook was not committed in the partial workflow.");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void AddUnrelatedWorksheet(string workbookPath, string sheetName)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(workbookPath, true);
        WorkbookPart workbookPart = document.WorkbookPart ??
            throw new InvalidOperationException("WorkbookPart is missing.");
        S.Workbook workbook = workbookPart.Workbook ??
            throw new InvalidOperationException("Workbook is missing.");
        S.Sheets sheets = workbook.GetFirstChild<S.Sheets>() ??
            workbook.AppendChild(new S.Sheets());
        WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        worksheetPart.Worksheet = new S.Worksheet(new S.SheetData(new S.Row(
            new S.Cell
            {
                CellReference = "A1",
                DataType = S.CellValues.InlineString,
                InlineString = new S.InlineString(new S.Text("Preserve this project worksheet"))
            }) { RowIndex = 1U }));
        worksheetPart.Worksheet.Save();
        uint sheetId = sheets.Elements<S.Sheet>()
            .Select(sheet => sheet.SheetId?.Value ?? 0U)
            .DefaultIfEmpty()
            .Max() + 1U;
        sheets.Append(new S.Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = sheetId,
            Name = sheetName
        });
        workbook.Save();
    }

    private static void VerifyPrunedWorkbook(
        string workbookPath,
        string retainedSheetName,
        string removedSheetName,
        string retainedTypeCode)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(workbookPath, false);
        WorkbookPart workbookPart = document.WorkbookPart ??
            throw new InvalidOperationException("WorkbookPart is missing.");
        S.Workbook workbook = workbookPart.Workbook ??
            throw new InvalidOperationException("Workbook is missing.");
        S.Sheets sheets = workbook.GetFirstChild<S.Sheets>() ??
            throw new InvalidOperationException("Workbook sheets are missing.");
        string[] sheetNames = sheets.Elements<S.Sheet>()
            .Select(sheet => sheet.Name?.Value ?? string.Empty)
            .ToArray();
        Require(sheetNames.Contains(retainedSheetName, StringComparer.OrdinalIgnoreCase),
            "The model-used managed type worksheet was deleted.");
        Require(!sheetNames.Contains(removedSheetName, StringComparer.OrdinalIgnoreCase),
            "The unused managed type worksheet was not deleted.");
        Require(sheetNames.Contains("Project Notes", StringComparer.OrdinalIgnoreCase),
            "An unrelated project worksheet was deleted during pruning.");
        S.Sheet indexSheet = sheets.Elements<S.Sheet>().Single(sheet =>
            string.Equals(sheet.Name?.Value, "_CLADDING_INDEX", StringComparison.OrdinalIgnoreCase));
        WorksheetPart indexPart = (WorksheetPart)workbookPart.GetPartById(indexSheet.Id!);
        S.Worksheet indexWorksheet = indexPart.Worksheet ??
            throw new InvalidOperationException("Index worksheet is missing.");
        string[] indexedTypeCodes = (indexWorksheet.GetFirstChild<S.SheetData>()?.Elements<S.Row>() ??
                Enumerable.Empty<S.Row>())
            .Skip(1)
            .Select(row => row.Elements<S.Cell>().FirstOrDefault()?.InnerText ?? string.Empty)
            .Where(value => value.Length > 0)
            .ToArray();
        Require(indexedTypeCodes.SequenceEqual(new[] { retainedTypeCode }, StringComparer.OrdinalIgnoreCase),
            "The hidden cladding index does not match the remaining managed type worksheet.");
    }

    private static void VerifyAssemblyContract()
    {
        Assembly assembly = typeof(PanelCladdingSurfaceSyncPlanningService).Assembly;
        Type syncCommand = RequireType(
            assembly,
            "PanelCladdingEditor.UI.PanelCladdingSyncFromSurfacesCommand");
        Type editorCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingEditorCommand");
        Type spawnCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingSpawnCommand");
        Type matchCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingMatchCommand");
        Type clearCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingClearCommand");
        Type smokeCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingEditorSmokeCommand");
        Require(syncCommand.GUID != Guid.Empty, "Sync command GUID must be explicit and non-empty.");
        Require(new[]
            {
                syncCommand.GUID, editorCommand.GUID, spawnCommand.GUID,
                matchCommand.GUID, clearCommand.GUID, smokeCommand.GUID
            }.Distinct().Count() == 6,
            "All PanelCladdingEditor Rhino command GUIDs must be unique.");
        Type liveRepository = RequireType(
            assembly,
            "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingSurfaceSyncRepository");
        MethodInfo enumeratorFactory = liveRepository.GetMethod(
                "CreateSurfaceEnumeratorSettings",
                BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException("Hidden-surface enumerator settings factory is missing.");
        object enumeratorSettings = enumeratorFactory.Invoke(null, null) ??
            throw new InvalidOperationException("Hidden-surface enumerator settings factory returned null.");
        foreach (string enabledProperty in new[]
        {
            "NormalObjects", "LockedObjects", "HiddenObjects", "ActiveObjects"
        })
        {
            bool enabled = (bool)(enumeratorSettings.GetType().GetProperty(enabledProperty)?.GetValue(enumeratorSettings) ?? false);
            Require(enabled, $"Surface enumeration must enable {enabledProperty}.");
        }
        bool referenceObjects = (bool)(enumeratorSettings.GetType().GetProperty("ReferenceObjects")?.GetValue(enumeratorSettings) ?? true);
        Require(!referenceObjects, "Surface enumeration must continue excluding reference objects.");
        Require(typeof(IPanelCladdingWorkbookRepository).GetMethod(
                nameof(IPanelCladdingWorkbookRepository.PrepareBatchUpsert)) is not null,
            "Batch workbook preparation contract is missing.");
        Require(typeof(IPreparedPanelCladdingWorkbookBatchUpdate).GetProperty(
                nameof(IPreparedPanelCladdingWorkbookBatchUpdate.RemovedTypeCodes)) is not null,
            "Prepared workbook pruning results are missing.");
        MethodInfo sync = typeof(IPanelCladdingSurfaceSyncService).GetMethod(
                nameof(IPanelCladdingSurfaceSyncService.Sync)) ??
            throw new InvalidOperationException("Surface sync service contract is missing.");
        Require(sync.GetParameters().Length == 4,
            "Surface sync service must accept document, panel ids, workbook, and create policy.");

        string[] forbidden = { "MCP_Rhino", "ModelContextProtocol", "Microsoft.Extensions.Hosting" };
        Require(!assembly.GetReferencedAssemblies().Any(reference => forbidden.Any(token =>
                (reference.Name ?? string.Empty).Contains(token, StringComparison.OrdinalIgnoreCase))),
            "Surface sync introduced an MCP_Rhino or MCP SDK dependency.");
    }

    private static PanelCladdingSurfaceSyncSnapshot BuildTwoPanelSnapshot()
    {
        string[] originalsOne = { "GL01", "GL02", "STN01", "TER01" };
        string[] originalsTwo = { "GL03", "GL03", "STN01", "TER02" };
        PanelCladdingSurfaceSyncSurfaceSnapshot[] surfacesOne =
            BuildSurfaces("PID_PANEL_01", FinalMaterials);
        surfacesOne[2] = Surface(
            surfacesOne[2].ObjectId,
            surfacesOne[2].PanelId,
            surfacesOne[2].Cid,
            surfacesOne[2].LayerPath,
            "STN01");
        PanelCladdingSurfaceSyncSurfaceSnapshot[] surfacesTwo =
            BuildSurfaces("PID_PANEL_02", FinalMaterials);
        surfacesTwo[0] = Surface(
            surfacesTwo[0].ObjectId,
            surfacesTwo[0].PanelId,
            surfacesTwo[0].Cid,
            surfacesTwo[0].LayerPath,
            "OLD");
        return Snapshot(
            new[]
            {
                Panel(PanelOneId, "PID_PANEL_01", BuildLayout(PanelOneId, originalsOne)),
                Panel(PanelTwoId, "PID_PANEL_02", BuildLayout(PanelTwoId, originalsTwo))
            },
            surfacesOne.Concat(surfacesTwo).ToArray());
    }

    private static PanelCladdingLayout BuildLayout(
        Guid objectId,
        IReadOnlyList<string> materials,
        double horizontalOffset = 50d,
        double verticalOffset = 50d)
    {
        var cells = new List<PanelCladdingCell>();
        int materialIndex = 0;
        for (int column = 0; column < 2; column++)
        {
            for (int row = 0; row < 2; row++)
            {
                string rowLabel = PanelCladdingKeyService.GetRowLabel(row);
                cells.Add(new PanelCladdingCell
                {
                    Column = column,
                    Row = row,
                    RowLabel = rowLabel,
                    ShortLabel = $"{column}{rowLabel}",
                    UserTextKey = PanelCladdingKeyService.GetCellKey(column, rowLabel),
                    Value = materials[materialIndex++]
                });
            }
        }

        return new PanelCladdingLayout
        {
            ObjectId = objectId,
            DocumentPath = "C:\\tests\\surface-sync.3dm",
            SystemCode = "WT01",
            GeometryFingerprint = $"fingerprint-{objectId:D}",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 100d,
            Height = 100d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 1d,
            HorizontalOffsets = new[] { horizontalOffset },
            VerticalOffsets = new[] { verticalOffset },
            Cells = cells,
            Preview = BuildPreview(cells)
        };
    }

    private static PanelPreviewGeometry BuildPreview(IReadOnlyList<PanelCladdingCell> cells)
    {
        return new PanelPreviewGeometry
        {
            Vertices = new[]
            {
                new PanelPoint3(0d, 0d, 0d), new PanelPoint3(100d, 0d, 0d),
                new PanelPoint3(100d, 100d, 0d), new PanelPoint3(0d, 100d, 0d)
            },
            Triangles = new[] { new PanelTriangle(0, 1, 2), new PanelTriangle(0, 2, 3) },
            GridPolylines = new[]
            {
                (IReadOnlyList<PanelPoint3>)new[]
                {
                    new PanelPoint3(0d, 50d, 0d), new PanelPoint3(100d, 50d, 0d)
                },
                new[] { new PanelPoint3(50d, 0d, 0d), new PanelPoint3(50d, 100d, 0d) }
            },
            Cells = cells.Select(cell => new PanelPreviewCell
            {
                UserTextKey = cell.UserTextKey,
                Center = new PanelPoint3(25d + cell.Column * 50d, 25d + cell.Row * 50d, 0d),
                Boundary = Array.Empty<PanelPoint3>()
            }).ToArray(),
            DepthSamples = Enumerable.Repeat(0d, 25).ToArray()
        };
    }

    private static PanelCladdingSurfaceSyncSurfaceSnapshot[] BuildSurfaces(
        string pid,
        IReadOnlyList<string> materials)
    {
        var result = new List<PanelCladdingSurfaceSyncSurfaceSnapshot>();
        int materialIndex = 0;
        for (int column = 0; column < 2; column++)
        {
            for (int row = 0; row < 2; row++)
            {
                string label = $"{column}{PanelCladdingKeyService.GetRowLabel(row)}";
                string material = materials[materialIndex++];
                result.Add(Surface(
                    Guid.NewGuid(),
                    pid,
                    PanelCladdingSpawnPlanningService.BuildSurfaceCid(pid, label),
                    MaterialLayer(material),
                    material));
            }
        }
        return result.ToArray();
    }

    private static string MaterialLayer(string material)
    {
        return $"{PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer}::" +
            $"{PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(material)}::{material}";
    }

    private static PanelCladdingSurfaceSyncPanelSnapshot Panel(
        Guid objectId,
        string pid,
        PanelCladdingLayout layout)
    {
        return new PanelCladdingSurfaceSyncPanelSnapshot
        {
            ObjectId = objectId,
            PanelId = pid,
            Layout = layout
        };
    }

    private static PanelCladdingSurfaceSyncSurfaceSnapshot Surface(
        Guid objectId,
        string pid,
        string cid,
        string layerPath,
        string claddingValue)
    {
        return new PanelCladdingSurfaceSyncSurfaceSnapshot
        {
            ObjectId = objectId,
            PanelId = pid,
            Cid = cid,
            LayerPath = layerPath,
            CladdingValue = claddingValue
        };
    }

    private static PanelCladdingSurfaceSyncSnapshot Snapshot(
        IReadOnlyList<PanelCladdingSurfaceSyncPanelSnapshot> panels,
        IReadOnlyList<PanelCladdingSurfaceSyncSurfaceSnapshot> surfaces,
        IReadOnlyList<PanelCladdingWorkbookTypeReference>? modelTypeAssignments = null)
    {
        return new PanelCladdingSurfaceSyncSnapshot
        {
            DocumentPath = "C:\\tests\\surface-sync.3dm",
            SelectedPanelIds = panels.Select(panel => panel.ObjectId).ToArray(),
            Panels = panels,
            Surfaces = surfaces,
            ModelTypeAssignments = modelTypeAssignments ?? Array.Empty<PanelCladdingWorkbookTypeReference>()
        };
    }

    private static string CellLabelForKey(
        PanelCladdingSurfaceSyncPlan plan,
        string cellKey)
    {
        return plan.Panels
            .SelectMany(panel => panel.Layout.Cells)
            .First(cell => string.Equals(cell.UserTextKey, cellKey, StringComparison.OrdinalIgnoreCase))
            .ShortLabel;
    }

    private static Type RequireType(Assembly assembly, string name)
    {
        return assembly.GetType(name) ?? throw new InvalidOperationException($"Type is missing: {name}");
    }

    private static T RequireData<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static void RequireFailure<T>(OperationResponse<T> response, string token)
    {
        Require(!response.Success && response.Message.Contains(token, StringComparison.Ordinal),
            $"Expected failure containing {token}, got: {response.Message}");
    }

    private static void RequireIsolatedIssue(
        OperationResponse<PanelCladdingSurfaceSyncPlan> response,
        string token,
        int expectedIssueCount = 1)
    {
        PanelCladdingSurfaceSyncPlan plan = RequireData(response, $"Create isolated {token} plan");
        Require(plan.Panels.Count == 0 && plan.Surfaces.Count == 0,
            $"A panel with {token} must not contribute panel or surface writes.");
        Require(plan.Issues.Count == expectedIssueCount && plan.Issues.Any(issue =>
                issue.Message.Contains(token, StringComparison.Ordinal)),
            $"Expected {expectedIssueCount} isolated issue(s) containing {token}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturingLiveRepository : ILivePanelCladdingSurfaceSyncRepository
    {
        private readonly PanelCladdingSurfaceSyncSnapshot _snapshot;

        public CapturingLiveRepository(PanelCladdingSurfaceSyncSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public PanelCladdingSurfaceSyncCommitRequest? LastCommitRequest { get; private set; }

        public OperationResponse<PanelCladdingSurfaceSyncSnapshot> Read(
            string filePath,
            IReadOnlyList<Guid> panelObjectIds)
        {
            return OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Ok(_snapshot);
        }

        public OperationResponse<PanelCladdingSurfaceSyncResult> Commit(
            PanelCladdingSurfaceSyncCommitRequest request,
            Func<OperationResponse> finalizeWorkbook,
            IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> types,
            int matchedSurfaceCount)
        {
            LastCommitRequest = request;
            OperationResponse finalized = finalizeWorkbook();
            if (!finalized.Success)
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(finalized.Message);
            }
            return OperationResponse<PanelCladdingSurfaceSyncResult>.Ok(
                new PanelCladdingSurfaceSyncResult
                {
                    SelectedPanelIds = request.SelectedPanelIds,
                    SkippedPanelIds = request.SkippedPanelIds,
                    ChangedPanelIds = request.PanelWrites.Select(write => write.ObjectId).ToArray(),
                    RefreshedSurfaceIds = request.SurfaceWrites.Select(write => write.ObjectId).ToArray(),
                    MatchedSurfaceCount = matchedSurfaceCount,
                    WorkbookPath = request.WorkbookPath,
                    Types = types,
                    Issues = request.Issues,
                    RemovedWorkbookTypeCodes = request.RemovedWorkbookTypeCodes
                });
        }
    }
}

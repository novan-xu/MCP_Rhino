using System.Reflection;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;

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
                surface.Cid == PanelCladdingSpawnPlanningService.BuildSurfaceCid(
                    surface.PanelId,
                    CellLabelForKey(plan, surface.CellKey))),
            "Every mapped surface CID must use the shared PID-to-CID derivation plus cell label.");
        Require(plan.Surfaces.Any(surface => surface.Cid.StartsWith("CID_", StringComparison.Ordinal)),
            "Prefixed production PIDs must map to CID_-prefixed surface identifiers.");
        Console.WriteLine("[OK] exact PID/CID mapping, layer authority, surface refresh, and panel change detection");

        VerifySurfaceOnlyRefresh(planner);
        Console.WriteLine("[OK] stale surface key refresh without unnecessary panel type regeneration");

        VerifyFailClosedMappings(planner, snapshot);
        Console.WriteLine("[OK] missing, duplicate, unexpected, wrong-PID, layer, family, and selected-PID failures");

        VerifyWorkflow(keys, snapshot);
        Console.WriteLine("[OK] end-to-end orchestration, final panel writes, surface writes, and workbook commit");

        VerifyBatchWorkbook(keys, plan);
        Console.WriteLine("[OK] one prepared workbook batch, in-batch signature reuse, commit, and reopen reuse");

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

    private static void VerifyFailClosedMappings(
        PanelCladdingSurfaceSyncPlanningService planner,
        PanelCladdingSurfaceSyncSnapshot source)
    {
        PanelCladdingSurfaceSyncPanelSnapshot panel = source.Panels[0];
        PanelCladdingSurfaceSyncSurfaceSnapshot[] own = source.Surfaces
            .Where(surface => surface.PanelId == panel.PanelId)
            .ToArray();

        RequireFailure(
            planner.CreatePlan(Snapshot(new[] { panel }, own.Skip(1).ToArray())),
            "SURFACE_MISSING");
        RequireFailure(
            planner.CreatePlan(Snapshot(new[] { panel }, own.Concat(new[]
            {
                Surface(Guid.NewGuid(), own[0].PanelId, own[0].Cid, own[0].LayerPath, own[0].CladdingValue)
            }).ToArray())),
            "DUPLICATE_CID");
        RequireFailure(
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
        RequireFailure(
            planner.CreatePlan(Snapshot(new[] { panel }, new[] { wrongPid }.Concat(own.Skip(1)).ToArray())),
            "SURFACE_PID_MISMATCH");

        PanelCladdingSurfaceSyncSurfaceSnapshot wrongRoot = Surface(
            own[0].ObjectId,
            own[0].PanelId,
            own[0].Cid,
            "Manual::Surfaces-Glass::GL01",
            own[0].CladdingValue);
        RequireFailure(
            planner.CreatePlan(Snapshot(new[] { panel }, new[] { wrongRoot }.Concat(own.Skip(1)).ToArray())),
            "LAYER_INVALID");

        PanelCladdingSurfaceSyncSurfaceSnapshot wrongFamily = Surface(
            own[0].ObjectId,
            own[0].PanelId,
            own[0].Cid,
            "02_Material Surfaces::Surfaces-Metal::GL01",
            own[0].CladdingValue);
        RequireFailure(
            planner.CreatePlan(Snapshot(new[] { panel }, new[] { wrongFamily }.Concat(own.Skip(1)).ToArray())),
            "MATERIAL_FAMILY_MISMATCH");

        RequireFailure(
            planner.CreatePlan(Snapshot(
                new[]
                {
                    panel,
                    Panel(PanelTwoId, panel.PanelId, BuildLayout(PanelTwoId, FinalMaterials))
                },
                own)),
            "DUPLICATE_SELECTED_PID");
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
                    !string.IsNullOrWhiteSpace(write.TypeCode) &&
                    write.StoredSignature.StartsWith("v1:sha256:", StringComparison.Ordinal)),
                "Final panel writes must carry normalized cells, type codes, and signatures.");
            Require(result.MatchedSurfaceCount == 8 && result.ChangedPanelIds.Count == 2 &&
                    result.RefreshedSurfaceIds.Count == 2 && result.Types.Count == 2,
                "Workflow result counts are incorrect.");
            Require(result.Types.Select(type => type.TypeCode).Distinct().Count() == 1,
                "Equal final configurations must reuse one type in the workflow batch.");
            Require(File.Exists(workbookPath), "Workflow did not commit the typology workbook.");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
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
        Require(assembly.GetType(
                "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingSurfaceSyncRepository") is not null,
            "Live surface sync repository is missing.");
        Require(typeof(IPanelCladdingWorkbookRepository).GetMethod(
                nameof(IPanelCladdingWorkbookRepository.PrepareBatchUpsert)) is not null,
            "Batch workbook preparation contract is missing.");
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

    private static PanelCladdingLayout BuildLayout(Guid objectId, IReadOnlyList<string> materials)
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
            HorizontalOffsets = new[] { 50d },
            VerticalOffsets = new[] { 50d },
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
        IReadOnlyList<PanelCladdingSurfaceSyncSurfaceSnapshot> surfaces)
    {
        return new PanelCladdingSurfaceSyncSnapshot
        {
            DocumentPath = "C:\\tests\\surface-sync.3dm",
            Panels = panels,
            Surfaces = surfaces
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
                    ChangedPanelIds = request.PanelWrites.Select(write => write.ObjectId).ToArray(),
                    RefreshedSurfaceIds = request.SurfaceWrites.Select(write => write.ObjectId).ToArray(),
                    MatchedSurfaceCount = matchedSurfaceCount,
                    WorkbookPath = request.WorkbookPath,
                    Types = types
                });
        }
    }
}

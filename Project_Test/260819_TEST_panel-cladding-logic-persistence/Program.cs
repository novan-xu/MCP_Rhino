using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingLogicPersistenceSmoke;

internal static class Program
{
    private static readonly Guid PanelId =
        Guid.Parse("68000000-0000-0000-0000-000000000001");
    private const string Pid = "PID_CLADDING_LOGIC_01";
    private const string Material = "MPL-001";

    private static void Main()
    {
        EncodingContainsOnlyCellLogic();
        SurfacePlanningCombinesSavedLogicAndGeometry();
        LegacyAndMalformedLogicBehaveSafely();
        SyncBackfillsAndVerifiesLogic();
        OtherCladdingMutationsMaintainLogic();

        Console.WriteLine("[OK] CW_2.13_CLADDING_LOGIC stores deterministic material-free cell logic.");
        Console.WriteLine("[OK] PCSyncSrf preserves valid saved owners and uses geometry for edited splits/merges.");
        Console.WriteLine("[OK] missing logic is backward-compatible and malformed logic fails before mutation.");
        Console.WriteLine("[OK] Save, PCCreate, PCMatchSrf, PCSyncSrf, and PCClear maintain the logic attribute.");
    }

    private static void EncodingContainsOnlyCellLogic()
    {
        var keys = new PanelCladdingKeyService();
        var logic = new PanelCladdingLogicService();
        PanelCladdingLayout layout = BuildLayout(keys, 2, new Dictionary<string, string>
        {
            [CellKey(0)] = Material,
            [CellKey(1)] = "0A"
        });
        string encoded = Required(logic.Encode(
            layout.Cells,
            layout.Cells.ToDictionary(cell => cell.UserTextKey, cell => cell.Value)),
            "Encode logic");
        Require(encoded == "{\"0A\":\"\",\"1A\":\"0A\"}",
            $"Unexpected canonical logic payload: {encoded}");
        Require(!encoded.Contains(Material, StringComparison.OrdinalIgnoreCase),
            "The logic payload leaked a material code.");

        IReadOnlyDictionary<string, string> decoded = Required(logic.Decode(encoded), "Decode logic");
        Require(Required(logic.ResolveOwnerLabel(new[] { "0A", "1A" }, decoded),
                "Resolve saved owner") == "0A",
            "The decoded graph did not resolve the saved owner.");
        Require(!logic.Decode("{\"0A\":\"1A\",\"1A\":\"0A\"}").Success,
            "A cyclic logic graph was accepted.");
        Require(!logic.Decode("{\"0A\":\"9Z\",\"1A\":\"\"}").Success,
            "An unknown parent-cell reference was accepted.");
    }

    private static void SurfacePlanningCombinesSavedLogicAndGeometry()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingSurfaceSyncPlanningService(keys);

        PanelCladdingLayout preserved = BuildLayout(keys, 3, new Dictionary<string, string>
        {
            [CellKey(0)] = "1A",
            [CellKey(1)] = Material,
            [CellKey(2)] = "MPL-002"
        }, "{\"0A\":\"1A\",\"1A\":\"\",\"2A\":\"\"}");
        PanelCladdingSurfaceSyncPlan preservedPlan = Required(planner.CreatePlan(Snapshot(
            preserved,
            Surface("68000000-0000-0000-0000-000000000010", new[] { "0A", "1A" }, Material),
            Surface("68000000-0000-0000-0000-000000000011", new[] { "2A" }, "MPL-002"))),
            "Plan saved owner preservation");
        PanelCladdingSurfaceSyncPanelPlan preservedPanel = preservedPlan.Panels.Single();
        Require(preservedPanel.CellValues[CellKey(1)] == Material &&
                preservedPanel.CellValues[CellKey(0)] == "1A",
            "An unchanged region did not preserve its saved non-default owner.");
        Require(preservedPlan.Surfaces.Single(surface =>
                surface.CoveredCellLabels.Count == 2).DesiredCid.EndsWith("-1A", StringComparison.Ordinal),
            "The preserved owner was not used for the surface CID.");

        PanelCladdingLayout merged = BuildLayout(keys, 3, new Dictionary<string, string>
        {
            [CellKey(0)] = Material,
            [CellKey(1)] = Material,
            [CellKey(2)] = Material
        }, "{\"0A\":\"\",\"1A\":\"\",\"2A\":\"\"}");
        PanelCladdingSurfaceSyncPlan mergedPlan = Required(planner.CreatePlan(Snapshot(
            merged,
            Surface("68000000-0000-0000-0000-000000000012", new[] { "0A", "1A", "2A" }, Material))),
            "Plan merged geometry");
        Require(mergedPlan.Panels.Single().CellValues[CellKey(0)] == Material &&
                mergedPlan.Panels.Single().CellValues[CellKey(1)] == "0A" &&
                mergedPlan.Panels.Single().CellValues[CellKey(2)] == "0A",
            "A merged surface did not fall back to the geometry-derived owner.");

        PanelCladdingLayout split = BuildLayout(keys, 3, new Dictionary<string, string>
        {
            [CellKey(0)] = "1A",
            [CellKey(1)] = Material,
            [CellKey(2)] = "1A"
        }, "{\"0A\":\"1A\",\"1A\":\"\",\"2A\":\"1A\"}");
        PanelCladdingSurfaceSyncPlan splitPlan = Required(planner.CreatePlan(Snapshot(
            split,
            Surface("68000000-0000-0000-0000-000000000013", new[] { "0A" }, "MPL-002"),
            Surface("68000000-0000-0000-0000-000000000014", new[] { "1A", "2A" }, Material))),
            "Plan split geometry");
        IReadOnlyDictionary<string, string> splitValues = splitPlan.Panels.Single().CellValues;
        Require(splitValues[CellKey(0)] == "MPL-002",
            "The split region outside its saved owner did not use its geometry owner.");
        Require(splitValues[CellKey(1)] == Material && splitValues[CellKey(2)] == "1A",
            "The split region retaining its saved owner did not preserve that owner.");
    }

    private static void LegacyAndMalformedLogicBehaveSafely()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingSurfaceSyncPlanningService(keys);
        PanelCladdingLayout legacy = BuildLayout(keys, 2, new Dictionary<string, string>
        {
            [CellKey(0)] = Material,
            [CellKey(1)] = "0A"
        });
        PanelCladdingSurfaceSyncPlan legacyPlan = Required(planner.CreatePlan(Snapshot(
            legacy,
            Surface("68000000-0000-0000-0000-000000000020", new[] { "0A", "1A" }, Material))),
            "Plan legacy geometry-only sync");
        PanelCladdingSurfaceSyncPanelPlan panel = legacyPlan.Panels.Single();
        Require(panel.CladdingChanged &&
                panel.CladdingLogic == "{\"0A\":\"\",\"1A\":\"0A\"}",
            "A legacy panel did not use geometry inference and schedule a logic backfill.");

        PanelCladdingLayout malformed = BuildLayout(keys, 2, new Dictionary<string, string>
        {
            [CellKey(0)] = Material,
            [CellKey(1)] = "0A"
        }, "{\"0A\":\"9Z\",\"1A\":\"\"}");
        PanelCladdingSurfaceSyncPlan malformedPlan = Required(planner.CreatePlan(Snapshot(
            malformed,
            Surface("68000000-0000-0000-0000-000000000021", new[] { "0A", "1A" }, Material))),
            "Plan malformed logic rejection");
        Require(malformedPlan.Panels.Count == 0 && malformedPlan.Issues.Count == 1 &&
                malformedPlan.Issues[0].Message.Contains("LOGIC_INVALID", StringComparison.Ordinal),
            "Malformed nonblank logic did not skip the panel before mutation.");
    }

    private static void SyncBackfillsAndVerifiesLogic()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingLayout legacy = BuildLayout(keys, 2, new Dictionary<string, string>
        {
            [CellKey(0)] = Material,
            [CellKey(1)] = "0A"
        });
        PanelCladdingSurfaceSyncSnapshot snapshot = Snapshot(
            legacy,
            Surface("68000000-0000-0000-0000-000000000030", new[] { "0A", "1A" }, Material));
        var live = new CapturingSurfaceSyncRepository(snapshot);
        var service = new PanelCladdingSurfaceSyncService(
            live,
            new PanelCladdingTypeSignatureService(keys),
            new PanelCladdingSurfaceSyncPlanningService(keys));
        Required(service.Sync(
            snapshot.DocumentPath,
            new[] { PanelId },
            string.Empty,
            allowCreateWorkbook: false,
            PanelCladdingObjectScope.Surfaces),
            "Run logic backfill sync");
        PanelCladdingSurfaceSyncPanelWrite write = live.LastCommit?.PanelWrites.Single() ??
            throw new InvalidOperationException("PCSyncSrf did not schedule one panel write.");
        Require(write.CladdingLogic == "{\"0A\":\"\",\"1A\":\"0A\"}",
            "The PCSyncSrf panel write omitted the derived logic.");

        var applied = new Dictionary<string, string>(write.CellValues, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.CladdingLogicKey] = write.CladdingLogic
        };
        Require(PanelCladdingSurfaceSyncService.ValidateAppliedPanelCellValues(applied, write).Success,
            "The committed logic postcondition rejected the exact write.");
        applied[PanelCladdingKeyService.CladdingLogicKey] = "{}";
        Require(!PanelCladdingSurfaceSyncService.ValidateAppliedPanelCellValues(applied, write).Success,
            "The committed logic postcondition accepted a changed payload.");
    }

    private static void OtherCladdingMutationsMaintainLogic()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingLayout layout = BuildLayout(keys, 2, new Dictionary<string, string>
        {
            [CellKey(0)] = Material,
            [CellKey(1)] = "0A"
        });

        var saveRepository = new CapturingPanelRepository(layout);
        var save = new PanelCladdingSaveService(
            saveRepository,
            new PanelCladdingTypeSignatureService(keys));
        Required(save.Save(new PanelCladdingSaveRequest
        {
            FilePath = layout.DocumentPath,
            ObjectId = layout.ObjectId,
            ExpectedGeometryFingerprint = layout.GeometryFingerprint,
            SystemCode = layout.SystemCode,
            CellValues = layout.Cells.ToDictionary(cell => cell.UserTextKey, cell => cell.Value),
            Scope = PanelCladdingSaveScope.Cladding
        }), "Save cladding logic");
        string savedLogic = saveRepository.LastCommit?.UserTextWrites.GetValueOrDefault(
            PanelCladdingKeyService.CladdingLogicKey) ?? string.Empty;
        Require(savedLogic == "{\"0A\":\"\",\"1A\":\"0A\"}" &&
                !savedLogic.Contains(Material, StringComparison.OrdinalIgnoreCase),
            "PCEditor cladding Save omitted or polluted the logic attribute.");

        PanelCladdingCreatePlan create = Required(new PanelCladdingCreatePlanningService(keys).CreatePlan(new[]
        {
            new PanelCladdingCreatePanelSnapshot
            {
                ObjectId = Guid.Parse("68000000-0000-0000-0000-000000000040"),
                XMinimum = 0d,
                XMaximum = 100d,
                YMinimum = 0d,
                YMaximum = 80d,
                ZMinimum = -1d,
                ZMaximum = 1d,
                Tolerance = 0.001d,
                Guides = new[]
                {
                    new PanelCladdingCreateGuideSnapshot
                    {
                        ObjectId = Guid.Parse("68000000-0000-0000-0000-000000000041"),
                        Samples = new[]
                        {
                            new PanelPoint3(-5d, 40d, 0d),
                            new PanelPoint3(50d, 40d, 0d),
                            new PanelPoint3(105d, 40d, 0d)
                        }
                    }
                }
            }
        }), "Create blank panel logic");
        string createdLogic = create.Panels.Single().UserTextWrites[PanelCladdingKeyService.CladdingLogicKey];
        Require(createdLogic == "{\"0A\":\"\",\"0B\":\"\"}",
            $"PCCreate did not initialize blank logic: {createdLogic}");

        PanelCladdingMatchGeometryDescriptor geometry = new()
        {
            GeometryClass = PanelGeometryClass.Planar,
            Width = layout.Width,
            Height = layout.Height,
            ModelTolerance = layout.ModelTolerance
        };
        PanelCladdingMatchPlan match = Required(new PanelCladdingMatchPlanningService(keys).CreatePlan(
            new PanelCladdingMatchPanelSnapshot
            {
                ObjectId = Guid.Parse("68000000-0000-0000-0000-000000000050"),
                Geometry = geometry,
                UserText = layout.SourceUserText
            },
            new[]
            {
                new PanelCladdingMatchPanelSnapshot
                {
                    ObjectId = Guid.Parse("68000000-0000-0000-0000-000000000051"),
                    Geometry = geometry,
                    UserText = new Dictionary<string, string>(layout.SourceUserText)
                    {
                        [PanelCladdingKeyService.CladdingLogicKey] = "{\"0A\":\"\",\"1A\":\"\"}"
                    }
                }
            }), "Match cladding logic");
        PanelCladdingMatchTargetPlan matchTarget = match.Targets.Single();
        Require(matchTarget.UserTextWrites[PanelCladdingKeyService.CladdingLogicKey] ==
                "{\"0A\":\"\",\"1A\":\"0A\"}" &&
                matchTarget.UserTextDeletes.Contains(
                    PanelCladdingKeyService.CladdingLogicKey,
                    StringComparer.OrdinalIgnoreCase),
            "PCMatchSrf did not replace the target logic with the source graph.");

        PanelCladdingClearPlan clear = Required(new PanelCladdingClearPlanningService(keys).CreatePlan(new[]
        {
            new PanelCladdingClearPanelSnapshot
            {
                ObjectId = PanelId,
                UserText = new Dictionary<string, string>
                {
                    [PanelCladdingKeyService.CladdingLogicKey] = savedLogic,
                    [CellKey(0)] = Material
                }
            }
        }), "Clear cladding logic");
        Require(clear.Panels.Single().UserTextDeletes.Contains(
                PanelCladdingKeyService.CladdingLogicKey,
                StringComparer.OrdinalIgnoreCase),
            "PCClear did not remove the derived cladding logic.");
    }

    private static PanelCladdingLayout BuildLayout(
        PanelCladdingKeyService keys,
        int columnCount,
        IReadOnlyDictionary<string, string> values,
        string? claddingLogic = null)
    {
        double[] verticalOffsets = Enumerable.Range(1, columnCount - 1)
            .Select(index => index * 30d)
            .ToArray();
        PanelCladdingKeySet keySet = Required(keys.CreateKeySet(
            Array.Empty<double>(),
            verticalOffsets,
            values,
            columnCount * 30d,
            180d,
            0.001d), "Create layout");
        var userText = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < verticalOffsets.Length; index++)
        {
            userText[PanelCladdingKeyService.GetVerticalOffsetKey(index)] =
                PanelCladdingKeyService.FormatOffset(verticalOffsets[index]);
        }
        if (claddingLogic is not null)
        {
            userText[PanelCladdingKeyService.CladdingLogicKey] = claddingLogic;
        }
        return new PanelCladdingLayout
        {
            ObjectId = PanelId,
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:\\tests\\panel-cladding-logic.3dm",
            ObjectName = Pid,
            LayerFullPath = "01_CW Panels::Surfaces-PNL::WT-01",
            SystemCode = "WT01",
            GeometryFingerprint = "cladding-logic-fingerprint",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar test fixture.",
            Width = columnCount * 30d,
            Height = 180d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 1d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = keySet.Topology,
            SourceUserText = userText
        };
    }

    private static PanelCladdingSurfaceSyncSnapshot Snapshot(
        PanelCladdingLayout layout,
        params PanelCladdingSurfaceSyncSurfaceSnapshot[] surfaces) =>
        new()
        {
            Scope = PanelCladdingObjectScope.Surfaces,
            DocumentPath = layout.DocumentPath,
            SelectedPanelIds = new[] { PanelId },
            Panels = new[]
            {
                new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = PanelId,
                    PanelId = Pid,
                    PanelCid = "CID_CLADDING_LOGIC_01",
                    Layout = layout
                }
            },
            Surfaces = surfaces
        };

    private static PanelCladdingSurfaceSyncSurfaceSnapshot Surface(
        string objectId,
        IReadOnlyList<string> coverage,
        string material) =>
        new()
        {
            ObjectId = Guid.Parse(objectId),
            PanelObjectId = PanelId,
            PanelId = Pid,
            Cid = $"CID_CLADDING_LOGIC_01-{coverage[0]}",
            LayerPath = $"{PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer}::" +
                $"{PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(material)}::{material}",
            CladdingValue = material,
            CoveredCellLabels = coverage
        };

    private static string CellKey(int column) =>
        PanelCladdingKeyService.GetCellKey(column, "A");

    private static T Required<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturingPanelRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public PanelAttributeCommitRequest? LastCommit { get; private set; }

        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used.");

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

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Fail("Not used.");
    }

    private sealed class CapturingSurfaceSyncRepository(
        PanelCladdingSurfaceSyncSnapshot snapshot) : ILivePanelCladdingSurfaceSyncRepository
    {
        public PanelCladdingSurfaceSyncCommitRequest? LastCommit { get; private set; }

        public OperationResponse<PanelCladdingSurfaceSyncSnapshot> Read(
            string filePath,
            IReadOnlyList<Guid> panelObjectIds,
            PanelCladdingObjectScope scope) =>
            OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Ok(snapshot);

        public OperationResponse<PanelCladdingSurfaceSyncResult> Commit(
            PanelCladdingSurfaceSyncCommitRequest request,
            Func<OperationResponse> finalizeWorkbook,
            IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> types,
            int matchedSurfaceCount,
            int matchedCurveCount)
        {
            LastCommit = request;
            OperationResponse finalized = finalizeWorkbook();
            return finalized.Success
                ? OperationResponse<PanelCladdingSurfaceSyncResult>.Ok(new PanelCladdingSurfaceSyncResult
                {
                    Scope = request.Scope,
                    SelectedPanelIds = request.SelectedPanelIds,
                    ChangedPanelIds = request.PanelWrites.Select(write => write.ObjectId).ToArray(),
                    Types = types,
                    MatchedSurfaceCount = matchedSurfaceCount,
                    MatchedCurveCount = matchedCurveCount
                })
                : OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(finalized.Message);
        }
    }
}

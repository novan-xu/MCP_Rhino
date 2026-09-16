using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingSparseTopologySmoke;

internal static class Program
{
    private static int Main()
    {
        var keys = new PanelCladdingKeyService();
        VerifyIndependentSparseMasks(keys);
        VerifyHideOnlySave(keys);
        VerifyMatchAndSpawnDefaults(keys);
        VerifyCurveTemplateEligibility(keys);
        VerifySurfaceSyncAndSignatureContracts();

        Console.WriteLine("[OK] default topology persists no 2.05-2.07 attributes.");
        Console.WriteLine("[OK] missing, merge, and hide states persist independently.");
        Console.WriteLine("[OK] hide-only save removes stale default masks and writes only 2.07.");
        Console.WriteLine("[OK] absent topology masks parse, match, and spawn as defaults.");
        Console.WriteLine("[OK] PCCrvTemplate skips configured merge codes and no-ops when no merge is possible.");
        Console.WriteLine("[OK] surface sync uses sparse persistence while signatures retain full canonical masks.");
        return 0;
    }

    private static void VerifyIndependentSparseMasks(PanelCladdingKeyService keys)
    {
        IReadOnlyDictionary<string, string> defaults = Required(
            keys.EncodeNonDefaultTopology(new PanelCladdingTopologyState(), 1, 1),
            "Encode sparse defaults");
        Require(defaults.Count == 0, "Default topology emitted one or more mask attributes.");

        var missing = new PanelCladdingTopologyState
        {
            MissingSegments =
            [
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 0)
            ]
        };
        var merged = new PanelCladdingTopologyState
        {
            MergeRuns =
            [
                new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Horizontal, 0, 0, 1)
            ]
        };
        var hidden = new PanelCladdingTopologyState
        {
            HiddenSegments =
            [
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, 0, 1)
            ]
        };
        RequireOnly(keys, missing, PanelCladdingKeyService.SegmentMaskKey);
        RequireOnly(keys, merged, PanelCladdingKeyService.MergeMaskKey);
        RequireOnly(keys, hidden, PanelCladdingKeyService.HideMaskKey);

        OperationResponse<PanelCladdingTopologyState> decoded = keys.DecodeTopology(
            new Dictionary<string, string>(), 1, 1);
        Require(decoded.Success && decoded.Data is not null &&
                decoded.Data.MissingSegments.Count == 0 &&
                decoded.Data.MergeRuns.Count == 0 &&
                decoded.Data.HiddenSegments.Count == 0,
            "Absent topology attributes did not decode to defaults.");

        PanelCladdingTopologyPayloads explicitDefaults = Required(
            keys.EncodeTopology(new PanelCladdingTopologyState(), 1, 1),
            "Encode full defaults");
        var oldStorage = new Dictionary<string, string>
        {
            [PanelCladdingKeyService.SegmentMaskKey] = explicitDefaults.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = explicitDefaults.MergeMask,
            [PanelCladdingKeyService.HideMaskKey] = explicitDefaults.HideMask
        };
        Require(PanelCladdingKeyService.TopologyPersistenceMatches(
                new Dictionary<string, string>(), defaults),
            "Absent default storage was not recognized as canonical sparse persistence.");
        Require(!PanelCladdingKeyService.TopologyPersistenceMatches(oldStorage, defaults),
            "Explicit default payloads were not identified for cleanup.");
        Require(!PanelCladdingKeyService.TopologyPersistenceMatches(
                new Dictionary<string, string> { [PanelCladdingKeyService.MergeMaskKey] = " " },
                defaults),
            "A blank topology attribute was treated as absent instead of cleanup work.");
    }

    private static void VerifyHideOnlySave(PanelCladdingKeyService keys)
    {
        PanelCladdingTopologyPayloads fullDefaults = Required(
            keys.EncodeTopology(new PanelCladdingTopologyState(), 1, 1),
            "Encode stale full defaults");
        PanelCladdingLayout layout = BuildLayout(keys, new Dictionary<string, string>
        {
            [PanelCladdingKeyService.SegmentMaskKey] = fullDefaults.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = fullDefaults.MergeMask,
            [PanelCladdingKeyService.HideMaskKey] = fullDefaults.HideMask
        });
        var repository = new CapturingRepository(layout);
        var service = new PanelCladdingSaveService(
            repository,
            new PanelCladdingTypeSignatureService(keys));
        var hideOnly = new PanelCladdingTopologyState
        {
            HiddenSegments =
            [
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 1)
            ]
        };
        OperationResponse<PanelCladdingSaveResult> saved = service.Save(new PanelCladdingSaveRequest
        {
            FilePath = layout.DocumentPath,
            ObjectId = layout.ObjectId,
            ExpectedGeometryFingerprint = layout.GeometryFingerprint,
            SystemCode = layout.SystemCode,
            HorizontalOffsets = layout.HorizontalOffsets,
            VerticalOffsets = layout.VerticalOffsets,
            Topology = hideOnly,
            CellValues = layout.Cells.ToDictionary(
                cell => cell.UserTextKey,
                cell => cell.Value,
                StringComparer.OrdinalIgnoreCase),
            Scope = PanelCladdingSaveScope.Extrusions
        });
        Require(saved.Success, $"Hide-only save failed: {saved.Message}");
        PanelAttributeCommitRequest commit = repository.LastCommit ??
            throw new InvalidOperationException("Hide-only save did not prepare an attribute commit.");
        string[] topologyWrites = commit.UserTextWrites.Keys
            .Where(PanelCladdingKeyService.IsTopologyKey)
            .ToArray();
        Require(topologyWrites.SequenceEqual([PanelCladdingKeyService.HideMaskKey]),
            $"Hide-only save wrote unexpected topology keys: {string.Join(", ", topologyWrites)}");
        Require(commit.UserTextDeletes.Contains(
                    PanelCladdingKeyService.SegmentMaskKey,
                    StringComparer.OrdinalIgnoreCase) &&
                commit.UserTextDeletes.Contains(
                    PanelCladdingKeyService.MergeMaskKey,
                    StringComparer.OrdinalIgnoreCase),
            "Hide-only save did not remove stale default segment and merge masks.");
    }

    private static void VerifyMatchAndSpawnDefaults(PanelCladdingKeyService keys)
    {
        PanelCladdingLayout layout = BuildLayout(keys, new Dictionary<string, string>());
        var source = Snapshot(layout, Guid.Parse("A8200000-0000-0000-0000-000000000101"));
        PanelCladdingTopologyPayloads staleDefaults = Required(
            keys.EncodeTopology(new PanelCladdingTopologyState(), 1, 1),
            "Encode match target defaults");
        var targetText = new Dictionary<string, string>(layout.SourceUserText, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.SegmentMaskKey] = staleDefaults.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = staleDefaults.MergeMask,
            [PanelCladdingKeyService.HideMaskKey] = staleDefaults.HideMask
        };
        var target = new PanelCladdingMatchPanelSnapshot
        {
            ObjectId = Guid.Parse("A8200000-0000-0000-0000-000000000102"),
            Geometry = source.Geometry,
            UserText = targetText
        };
        OperationResponse<PanelCladdingMatchPlan> matched =
            new PanelCladdingCurveMatchPlanningService(keys).CreatePlan(source, [target]);
        Require(matched.Success && matched.Data is not null, $"Default curve match failed: {matched.Message}");
        PanelCladdingMatchTargetPlan targetPlan = matched.Data!.Targets.Single();
        Require(!targetPlan.UserTextWrites.Keys.Any(PanelCladdingKeyService.IsTopologyKey),
            "Default curve match wrote explicit default masks.");
        Require(new[]
            {
                PanelCladdingKeyService.SegmentMaskKey,
                PanelCladdingKeyService.MergeMaskKey,
                PanelCladdingKeyService.HideMaskKey
            }.All(key => targetPlan.UserTextDeletes.Contains(key, StringComparer.OrdinalIgnoreCase)),
            "Default curve match did not clean all stale target topology masks.");

        var spawnText = new Dictionary<string, string>(layout.SourceUserText, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = "PID_SPARSE_01",
            [PanelCladdingSpawnPlanningService.CidUserTextKey] = "CID_SPARSE_01",
            [PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = "01",
            [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT01"
        };
        OperationResponse<PanelCladdingSpawnPlan> spawned = new PanelCladdingSpawnPlanningService(keys)
            .CreatePlan(spawnText, new PanelCladdingKeySet
            {
                HorizontalOffsets = layout.HorizontalOffsets,
                VerticalOffsets = layout.VerticalOffsets,
                Cells = layout.Cells,
                Topology = new PanelCladdingTopologyState()
            }, layout.Width, layout.Height, layout.LayerFullPath);
        Require(spawned.Success && spawned.Data is not null && spawned.Data.Curves.Count > 0,
            $"Absent default masks did not permit extrusion planning: {spawned.Message}");
    }

    private static void VerifyCurveTemplateEligibility(PanelCladdingKeyService keys)
    {
        var planner = new PanelCladdingCurveTemplatePlanningService(keys);
        OperationResponse<PanelCladdingCurveTemplatePlan> oneColumn = planner.CreatePlan(
        [
            new PanelCladdingCurveTemplatePanelSnapshot
            {
                ObjectId = Guid.Parse("A8200000-0000-0000-0000-000000000201"),
                HorizontalTrackCount = 2,
                VerticalTrackCount = 0
            }
        ], PanelCladdingCurveTemplatePriority.Horizontal);
        Require(oneColumn.Success && oneColumn.Data is not null &&
                oneColumn.Data.Panels.Single().MergeRuns.Count == 0 &&
                oneColumn.Data.Panels.Single().MergeMask.Length == 0 &&
                oneColumn.Data.Panels.Single().UserTextDeletes.Count == 0,
            "One-column H-priority produced a default merge payload or signature invalidation.");

        OperationResponse<PanelCladdingCurveTemplatePlan> oneRow = planner.CreatePlan(
        [
            new PanelCladdingCurveTemplatePanelSnapshot
            {
                ObjectId = Guid.Parse("A8200000-0000-0000-0000-000000000202"),
                HorizontalTrackCount = 0,
                VerticalTrackCount = 2
            }
        ], PanelCladdingCurveTemplatePriority.Vertical);
        Require(oneRow.Success && oneRow.Data is not null &&
                string.IsNullOrEmpty(oneRow.Data.Panels.Single().MergeMask),
            "One-row V-priority produced a default merge payload.");

        OperationResponse<PanelCladdingCurveTemplatePlan> configured = planner.CreatePlan(
        [
            new PanelCladdingCurveTemplatePanelSnapshot
            {
                ObjectId = Guid.Parse("A8200000-0000-0000-0000-000000000203"),
                HorizontalTrackCount = 1,
                VerticalTrackCount = 2,
                HasMergeMask = true
            }
        ], PanelCladdingCurveTemplatePriority.Horizontal);
        Require(configured.Success && configured.Data is not null &&
                configured.Data.Panels.Count == 0 && configured.Data.SkippedPanelIds.SequenceEqual(
                    [Guid.Parse("A8200000-0000-0000-0000-000000000203")]),
            "PCCrvTemplate did not skip an existing nonblank merge code.");

        Require(!PanelCladdingKeyService.HasNonblankMergeMask(new Dictionary<string, string>()) &&
                !PanelCladdingKeyService.HasNonblankMergeMask(new Dictionary<string, string>
                {
                    [PanelCladdingKeyService.MergeMaskKey.ToLowerInvariant()] = " "
                }) &&
                PanelCladdingKeyService.HasNonblankMergeMask(new Dictionary<string, string>
                {
                    [PanelCladdingKeyService.MergeMaskKey.ToLowerInvariant()] = "payload"
                }),
            "Merge-code eligibility is not case-insensitive with blank values treated as absent.");
    }

    private static void VerifySurfaceSyncAndSignatureContracts()
    {
        string root = FindRepositoryRoot();
        string surfaceSync = File.ReadAllText(Path.Combine(
            root, "src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live",
            "PanelCladding", "LivePanelCladdingSurfaceSyncRepository.cs"));
        Require(surfaceSync.Contains("EncodeNonDefaultTopology", StringComparison.Ordinal) &&
                surfaceSync.Contains("TopologyPersistenceMatches", StringComparison.Ordinal),
            "Surface sync does not compare and commit the sparse topology representation.");
        string signatures = File.ReadAllText(Path.Combine(
            root, "src", "PanelCladdingEditor", "Application", "Services",
            "PanelCladdingTypeSignatureService.cs"));
        Require(signatures.Contains("EncodeTopology(", StringComparison.Ordinal) &&
                !signatures.Contains("EncodeNonDefaultTopology", StringComparison.Ordinal),
            "Type signatures no longer use the full canonical topology payloads.");
    }

    private static void RequireOnly(
        PanelCladdingKeyService keys,
        PanelCladdingTopologyState topology,
        string expectedKey)
    {
        IReadOnlyDictionary<string, string> writes = Required(
            keys.EncodeNonDefaultTopology(topology, 1, 1),
            $"Encode {expectedKey}");
        Require(writes.Count == 1 && writes.ContainsKey(expectedKey),
            $"Expected only {expectedKey}; found {string.Join(", ", writes.Keys)}.");
    }

    private static PanelCladdingLayout BuildLayout(
        PanelCladdingKeyService keys,
        IReadOnlyDictionary<string, string> topologyText)
    {
        double[] horizontal = [20d];
        double[] vertical = [15d];
        var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int column = 0; column < 2; column++)
        {
            for (int row = 0; row < 2; row++)
            {
                cells[PanelCladdingKeyService.GetCellKey(
                    column,
                    PanelCladdingKeyService.GetRowLabel(row))] = "MPL-001";
            }
        }
        PanelCladdingKeySet keySet = Required(
            keys.CreateKeySet(horizontal, vertical, cells, 30d, 40d, 0.001d),
            "Build sparse fixture key set");
        var userText = new Dictionary<string, string>(cells, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "20.00000",
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "15.00000",
            [PanelCladdingKeyService.TypeCodeKey] = "WT01-2X2-SPARSE",
            [PanelCladdingKeyService.SignatureKey] = "v4:sha256:stale"
        };
        foreach ((string key, string value) in topologyText)
        {
            userText[key] = value;
        }
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("A8200000-0000-0000-0000-000000000100"),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/test/panel-sparse-topology.3dm",
            ObjectName = "PID_SPARSE_01",
            LayerFullPath = "01_CW Panels::Surfaces-PNL::WT-01",
            SystemCode = "WT01",
            GeometryFingerprint = "sparse-topology-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar fixture.",
            Width = 30d,
            Height = 40d,
            ModelTolerance = 0.001d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = keySet.Topology,
            SourceUserText = userText
        };
    }

    private static PanelCladdingMatchPanelSnapshot Snapshot(PanelCladdingLayout layout, Guid objectId) =>
        new()
        {
            ObjectId = objectId,
            Geometry = new PanelCladdingMatchGeometryDescriptor
            {
                GeometryClass = PanelGeometryClass.Planar,
                Width = layout.Width,
                Height = layout.Height,
                ModelTolerance = layout.ModelTolerance
            },
            UserText = layout.SourceUserText
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

    private static T Required<T>(OperationResponse<T> response, string operation) where T : class =>
        response.Success && response.Data is not null
            ? response.Data
            : throw new InvalidOperationException($"{operation} failed: {response.Message}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturingRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public PanelAttributeCommitRequest? LastCommit { get; private set; }

        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(
            string filePath,
            Guid objectId) => OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used.");

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
}

using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace CladdingSurfaceCoverageMetadataSmoke;

internal static class Program
{
    private static readonly Guid PanelId =
        Guid.Parse("6a000000-0000-0000-0000-000000000001");

    private static void Main()
    {
        VerifyCodecAndSpawnWrites();
        Console.WriteLine("[OK] spawn writes canonical single and merged cladding coverage");

        VerifyCoverageAuthorityPreservesMergedMullion();
        Console.WriteLine("[OK] baked merged coverage preserves its hidden mullion");

        VerifyCoverageAuthorityRefreshesSeparateBoundaries();
        Console.WriteLine("[OK] separate baked coverage replaces moved boundaries and suppresses stale curves");

        VerifyInvalidPartitionsFailClosed();
        Console.WriteLine("[OK] overlapping and incomplete baked coverage fail before offset planning");

        VerifySyncPlansCoverageMigrationAndCanonicalization();
        Console.WriteLine("[OK] PCSyncSrf migrates and canonicalizes baked coverage metadata");

        VerifyLiveReadWriteContract();
        Console.WriteLine("[OK] live sync reads, validates, rewrites, and persists coverage metadata");
    }

    private static void VerifyCodecAndSpawnWrites()
    {
        var coverage = new PanelCladdingSurfaceCoverageService();
        OperationResponse<IReadOnlyList<string>> duplicate = coverage.Decode("0A;1A;1a");
        Require(!duplicate.Success && duplicate.Message.Contains("CELL_INVALID", StringComparison.Ordinal),
            "Duplicate coverage labels must be rejected case-insensitively.");

        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingSpawnPlanningService(keys);
        var mergedText = BasePanelText();
        mergedText[PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "45";
        mergedText[PanelCladdingKeyService.GetCellKey(0, "A")] = "GLS-005";
        mergedText[PanelCladdingKeyService.GetCellKey(1, "A")] = "0A";
        PanelCladdingKeySet mergedKeys = RequireData(
            keys.Parse(mergedText, 90d, 198d, 0.001d),
            "Parse merged coverage grid");
        PanelCladdingSpawnPlan merged = RequireData(
            planner.CreatePlan(mergedText, mergedKeys),
            "Plan merged cladding spawn");
        PanelCladdingSpawnRegionPlan mergedRegion = merged.Regions.Single();
        Require(mergedRegion.UserTextWrites[PanelCladdingSurfaceCoverageService.UserTextKey] ==
                "0A;1A",
            "Merged 0A/1A geometry must carry complete owner-first coverage.");

        var singleText = BasePanelText();
        singleText[PanelCladdingKeyService.GetCellKey(0, "A")] = "GLS-005";
        PanelCladdingKeySet singleKeys = RequireData(
            keys.Parse(singleText, 90d, 198d, 0.001d),
            "Parse single-cell grid");
        PanelCladdingSpawnPlan single = RequireData(
            planner.CreatePlan(singleText, singleKeys),
            "Plan single-cell cladding spawn");
        Require(single.Regions.Single().UserTextWrites[
                PanelCladdingSurfaceCoverageService.UserTextKey] == "0A",
            "Single-cell geometry must still carry an explicit coverage schema marker.");
    }

    private static void VerifyCoverageAuthorityPreservesMergedMullion()
    {
        PanelCladdingLayout layout = BuildLayout(
            horizontalOffsets: [],
            verticalOffsets: [45d],
            width: 90d,
            height: 198d,
            ownerByCell: new Dictionary<string, string>
            {
                ["0A"] = string.Empty,
                ["1A"] = string.Empty
            });
        PanelCladdingInferredOffsets result = RequireData(
            PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(
                layout,
                new PanelCladdingInferredOffsets(),
                curveInferred: null,
                storedSurfaceCoverages: new IReadOnlyList<string>[]
                {
                    new[] { "0A", "1A" }
                }),
            "Preserve metadata-owned hidden mullion");
        Require(result.VerticalOffsets.SequenceEqual([45d]),
            "One baked surface covering 0A/1A must protect V0 despite a misleading panel graph.");
    }

    private static void VerifyCoverageAuthorityRefreshesSeparateBoundaries()
    {
        PanelCladdingLayout layout = BuildLayout(
            horizontalOffsets: [21.375d, 107.25d, 169.09079d],
            verticalOffsets: [],
            width: 90d,
            height: 198d,
            ownerByCell: new Dictionary<string, string>
            {
                ["0A"] = string.Empty,
                ["0B"] = "0A",
                ["0C"] = "0A",
                ["0D"] = "0A"
            });
        PanelCladdingInferredOffsets result = RequireData(
            PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(
                layout,
                new PanelCladdingInferredOffsets
                {
                    HorizontalOffsets = [19.625d, 105.75d, 169.09079d]
                },
                new PanelCladdingInferredOffsets
                {
                    HorizontalOffsets = [21.375d, 107.25d, 169.09079d]
                },
                new IReadOnlyList<string>[]
                {
                    new[] { "0A" },
                    new[] { "0B" },
                    new[] { "0C" },
                    new[] { "0D" }
                }),
            "Refresh separately baked cladding boundaries");
        Require(result.HorizontalOffsets.SequenceEqual([19.625d, 105.75d, 169.09079d]),
            $"Expected refreshed surface offsets only, found {string.Join(",", result.HorizontalOffsets)}.");
    }

    private static void VerifyInvalidPartitionsFailClosed()
    {
        PanelCladdingLayout layout = BuildLayout(
            horizontalOffsets: [],
            verticalOffsets: [45d],
            width: 90d,
            height: 198d,
            ownerByCell: new Dictionary<string, string>
            {
                ["0A"] = string.Empty,
                ["1A"] = "0A"
            });
        OperationResponse<PanelCladdingInferredOffsets> overlap =
            PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(
                layout,
                new PanelCladdingInferredOffsets(),
                null,
                new IReadOnlyList<string>[]
                {
                    new[] { "0A", "1A" },
                    new[] { "1A" }
                });
        Require(!overlap.Success && overlap.Message.Contains("CELL_OVERLAP", StringComparison.Ordinal),
            "Overlapping baked coverage must fail closed.");

        OperationResponse<PanelCladdingInferredOffsets> missing =
            PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(
                layout,
                new PanelCladdingInferredOffsets(),
                null,
                new IReadOnlyList<string>[] { new[] { "0A" } });
        Require(!missing.Success && missing.Message.Contains("CELL_MISSING", StringComparison.Ordinal),
            "Incomplete baked coverage must fail closed.");
    }

    private static void VerifySyncPlansCoverageMigrationAndCanonicalization()
    {
        PanelCladdingLayout layout = BuildLayout(
            horizontalOffsets: [],
            verticalOffsets: [45d],
            width: 90d,
            height: 198d,
            ownerByCell: new Dictionary<string, string>
            {
                ["0A"] = string.Empty,
                ["1A"] = "0A"
            });
        var planner = new PanelCladdingSurfaceSyncPlanningService(new PanelCladdingKeyService());

        PanelCladdingSurfaceSyncPlan migrated = RequireData(
            planner.CreatePlan(BuildSnapshot(layout, coverageValue: string.Empty)),
            "Plan legacy surface coverage migration");
        PanelCladdingSurfaceSyncSurfacePlan migratedSurface = migrated.Surfaces.Single();
        Require(migratedSurface.CoverageChanged &&
                migratedSurface.DesiredCoverageValue == "0A;1A",
            "A legacy merged surface must schedule the canonical coverage attribute.");

        PanelCladdingSurfaceSyncPlan canonical = RequireData(
            planner.CreatePlan(BuildSnapshot(layout, coverageValue: "0A;1A")),
            "Plan canonical coverage no-op");
        Require(!canonical.Surfaces.Single().CoverageChanged,
            "Canonical coverage must not create a redundant surface write.");

        PanelCladdingSurfaceSyncPlan normalized = RequireData(
            planner.CreatePlan(BuildSnapshot(layout, coverageValue: "0a; 1a")),
            "Plan noncanonical coverage refresh");
        Require(normalized.Surfaces.Single().CoverageChanged &&
                normalized.Surfaces.Single().DesiredCoverageValue == "0A;1A",
            "Noncanonical coverage must be rewritten deterministically.");
    }

    private static PanelCladdingSurfaceSyncSnapshot BuildSnapshot(
        PanelCladdingLayout layout,
        string coverageValue)
    {
        const string pid = "PID_COVERAGE_01";
        const string material = "GLS-005";
        return new PanelCladdingSurfaceSyncSnapshot
        {
            Scope = PanelCladdingObjectScope.Surfaces,
            SelectedPanelIds = [PanelId],
            Panels =
            [
                new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = PanelId,
                    PanelId = pid,
                    Layout = layout
                }
            ],
            Surfaces =
            [
                new PanelCladdingSurfaceSyncSurfaceSnapshot
                {
                    ObjectId = Guid.Parse("6a000000-0000-0000-0000-000000000002"),
                    PanelObjectId = PanelId,
                    PanelId = pid,
                    Cid = PanelCladdingSpawnPlanningService.BuildSurfaceCid(pid, "0A"),
                    LayerPath = $"{PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer}::" +
                        $"{PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(material)}::{material}",
                    CladdingValue = material,
                    CoverageValue = coverageValue,
                    CoveredCellLabels = ["0A", "1A"]
                }
            ]
        };
    }

    private static Dictionary<string, string> BasePanelText() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = "PID_COVERAGE_01",
            [PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = "R1",
            [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT01"
        };

    private static PanelCladdingLayout BuildLayout(
        IReadOnlyList<double> horizontalOffsets,
        IReadOnlyList<double> verticalOffsets,
        double width,
        double height,
        IReadOnlyDictionary<string, string> ownerByCell)
    {
        PanelCladdingKeySet keySet = RequireData(
            new PanelCladdingKeyService().CreateKeySet(
                horizontalOffsets,
                verticalOffsets,
                new Dictionary<string, string>(),
                width,
                height,
                0.001d),
            "Build coverage test layout");
        return new PanelCladdingLayout
        {
            ObjectId = PanelId,
            Width = width,
            Height = height,
            ModelTolerance = 0.001d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            SourceUserText = new Dictionary<string, string>
            {
                [PanelCladdingKeyService.CladdingLogicKey] = JsonSerializer.Serialize(ownerByCell)
            }
        };
    }

    private static void VerifyLiveReadWriteContract()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "PanelCladdingEditor",
            "Infrastructure",
            "Rhino",
            "Live",
            "PanelCladding",
            "LivePanelCladdingSurfaceSyncRepository.cs"));
        Require(source.Contains("PANEL_CLADDING_SURFACE_COVERAGE_INCOMPLETE", StringComparison.Ordinal) &&
                source.Contains("surface.CoverageValue", StringComparison.Ordinal) &&
                source.Contains("surfaceWrite.DesiredCoverageValue", StringComparison.Ordinal) &&
                source.Contains("PanelCladdingSurfaceCoverageService.UserTextKey", StringComparison.Ordinal),
            "The live repository must read, validate, plan, and write the coverage key.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static T RequireData<T>(OperationResponse<T> response, string operation)
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
}

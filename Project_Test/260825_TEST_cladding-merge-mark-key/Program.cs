using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace CladdingMergeMarkKeySmoke;

internal static class Program
{
    private static readonly Guid PanelId =
        Guid.Parse("6b000000-0000-0000-0000-000000000001");

    private static void Main()
    {
        VerifyCanonicalKeyAndStoredValueResolution();
        Console.WriteLine("[OK] Merge_Mark is canonical and conflicting legacy values fail closed");

        VerifySpawnWritesOnlyMergeMark();
        Console.WriteLine("[OK] PCSpawnSrf planning writes Merge_Mark and never the legacy key");

        VerifySyncSchedulesLegacyCleanup();
        Console.WriteLine("[OK] PCSyncSrf planning schedules legacy-key cleanup");

        VerifyLiveRepositoryMigrationContract();
        Console.WriteLine("[OK] live sync resolves and deletes the legacy key before writing Merge_Mark");
    }

    private static void VerifyCanonicalKeyAndStoredValueResolution()
    {
        Require(PanelCladdingSurfaceCoverageService.UserTextKey == "Merge_Mark",
            "The canonical baked coverage key must be exactly Merge_Mark.");
        Require(PanelCladdingSurfaceCoverageService.LegacyUserTextKey ==
                "CW_1.03_CLADDING_CELLS",
            "The former key must remain identified only as a migration alias.");

        var coverage = new PanelCladdingSurfaceCoverageService();
        RequireData(coverage.ResolveStoredValue("0A;1A", string.Empty),
            "Resolve canonical-only coverage");
        RequireData(coverage.ResolveStoredValue(string.Empty, "0A;1A"),
            "Resolve legacy-only coverage");
        RequireData(coverage.ResolveStoredValue("0a; 1a", "0A;1A"),
            "Resolve semantically equal dual keys");

        OperationResponse<string> conflict = coverage.ResolveStoredValue("0A;1A", "0A");
        Require(!conflict.Success &&
                conflict.Message.Contains("KEY_CONFLICT", StringComparison.Ordinal),
            "Different canonical and legacy values must fail before mutation.");
    }

    private static void VerifySpawnWritesOnlyMergeMark()
    {
        var keys = new PanelCladdingKeyService();
        var userText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = "PID_MERGE_MARK_01",
            [PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = "R1",
            [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT01",
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "45",
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "GLS-005",
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "0A"
        };
        PanelCladdingKeySet keySet = RequireData(
            keys.Parse(userText, 90d, 198d, 0.001d),
            "Parse merged spawn grid");
        PanelCladdingSpawnPlan plan = RequireData(
            new PanelCladdingSpawnPlanningService(keys).CreatePlan(userText, keySet),
            "Plan merged surface spawn");
        IReadOnlyDictionary<string, string> writes = plan.Regions.Single().UserTextWrites;

        Require(writes.TryGetValue("Merge_Mark", out string? value) && value == "0A;1A",
            "Merged spawn geometry must carry Merge_Mark=0A;1A.");
        Require(!writes.ContainsKey(PanelCladdingSurfaceCoverageService.LegacyUserTextKey),
            "PCSpawnSrf must not write the former CW_1.03 key.");
    }

    private static void VerifySyncSchedulesLegacyCleanup()
    {
        PanelCladdingLayout layout = BuildLayout();
        var planner = new PanelCladdingSurfaceSyncPlanningService(new PanelCladdingKeyService());

        PanelCladdingSurfaceSyncSurfacePlan canonical = RequireData(
            planner.CreatePlan(BuildSnapshot(layout, "0A;1A", string.Empty)),
            "Plan canonical Merge_Mark surface").Surfaces.Single();
        Require(!canonical.CoverageChanged,
            "Canonical Merge_Mark coverage must not schedule a redundant write.");

        PanelCladdingSurfaceSyncSurfacePlan legacy = RequireData(
            planner.CreatePlan(BuildSnapshot(layout, "0A;1A", "0A;1A")),
            "Plan legacy-key cleanup").Surfaces.Single();
        Require(legacy.CoverageChanged && legacy.DesiredCoverageValue == "0A;1A",
            "A legacy value must schedule cleanup and canonical persistence.");
    }

    private static PanelCladdingLayout BuildLayout()
    {
        PanelCladdingKeySet keySet = RequireData(
            new PanelCladdingKeyService().CreateKeySet(
                Array.Empty<double>(),
                new[] { 45d },
                new Dictionary<string, string>(),
                90d,
                198d,
                0.001d),
            "Build merge-mark test layout");
        return new PanelCladdingLayout
        {
            ObjectId = PanelId,
            Width = 90d,
            Height = 198d,
            ModelTolerance = 0.001d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            SourceUserText = new Dictionary<string, string>
            {
                [PanelCladdingKeyService.CladdingLogicKey] = JsonSerializer.Serialize(
                    new Dictionary<string, string>
                    {
                        ["0A"] = string.Empty,
                        ["1A"] = "0A"
                    })
            }
        };
    }

    private static PanelCladdingSurfaceSyncSnapshot BuildSnapshot(
        PanelCladdingLayout layout,
        string coverageValue,
        string legacyCoverageValue)
    {
        const string panelPid = "PID_MERGE_MARK_01";
        const string material = "GLS-005";
        return new PanelCladdingSurfaceSyncSnapshot
        {
            Scope = PanelCladdingObjectScope.Surfaces,
            SelectedPanelIds = new[] { PanelId },
            Panels = new[]
            {
                new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = PanelId,
                    PanelId = panelPid,
                    Layout = layout
                }
            },
            Surfaces = new[]
            {
                new PanelCladdingSurfaceSyncSurfaceSnapshot
                {
                    ObjectId = Guid.Parse("6b000000-0000-0000-0000-000000000002"),
                    PanelObjectId = PanelId,
                    PanelId = panelPid,
                    Cid = PanelCladdingSpawnPlanningService.BuildSurfaceCid(panelPid, "0A"),
                    LayerPath = $"{PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer}::" +
                        $"{PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(material)}::{material}",
                    CladdingValue = material,
                    CoverageValue = coverageValue,
                    LegacyCoverageValue = legacyCoverageValue,
                    CoveredCellLabels = new[] { "0A", "1A" }
                }
            }
        };
    }

    private static void VerifyLiveRepositoryMigrationContract()
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
        Require(source.Contains("ResolveStoredValue(", StringComparison.Ordinal) &&
                source.Contains("PanelCladdingSurfaceCoverageService.LegacyUserTextKey",
                    StringComparison.Ordinal) &&
                source.Contains("PanelCladdingSurfaceCoverageService.UserTextKey",
                    StringComparison.Ordinal),
            "Live sync must resolve dual keys, delete the legacy alias, and write the canonical key.");
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

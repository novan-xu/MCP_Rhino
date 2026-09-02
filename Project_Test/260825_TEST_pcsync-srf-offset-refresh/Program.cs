using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PCSyncSrfOffsetRefreshSmoke;

internal static class Program
{
    private static void Main()
    {
        VerifyReportedMovedHeightsReplaceOldOffsets();
        Console.WriteLine("[OK] moved Level 5 H offsets replace old indexed values");

        VerifyNonSplittingTrackRemainsWithoutGeometryEvidence();
        Console.WriteLine("[OK] a proven non-splitting structural track remains without geometry evidence");

        VerifyNonSplittingTrackFollowsCurrentCurve();
        Console.WriteLine("[OK] a proven structural track follows its current associated curve");

        VerifyUnmatchedCurveAddsNewStructuralTrack();
        Console.WriteLine("[OK] a genuinely unmatched curve adds a new structural track");

        VerifyLiveRepositoryUsesRoleAwareLayoutOverload();
        Console.WriteLine("[OK] live PCSyncSrf uses role-aware offset refresh planning");
    }

    private static void VerifyReportedMovedHeightsReplaceOldOffsets()
    {
        PanelCladdingLayout layout = BuildLayout(
            horizontalOffsets: [21.375d, 107.25d, 169.09079d],
            verticalOffsets: [],
            width: 90d,
            height: 198d,
            ownerByCell: new Dictionary<string, string>
            {
                ["0A"] = string.Empty,
                ["0B"] = string.Empty,
                ["0C"] = string.Empty,
                ["0D"] = string.Empty
            });
        PanelCladdingInferredOffsets result = RequireData(
            PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(
                layout,
                new PanelCladdingInferredOffsets
                {
                    HorizontalOffsets = [19.625d, 105.75d, 169.09079d],
                    VerticalOffsets = []
                },
                new PanelCladdingInferredOffsets
                {
                    HorizontalOffsets = [21.375d, 107.25d, 169.09079d],
                    VerticalOffsets = []
                }),
            "Refresh reported Level 5 heights");

        Require(result.HorizontalOffsets.SequenceEqual([19.625d, 105.75d, 169.09079d]),
            $"Expected refreshed H offsets only, found {string.Join(",", result.HorizontalOffsets)}.");
        Require(!result.HorizontalOffsets.Contains(21.375d) &&
                !result.HorizontalOffsets.Contains(107.25d),
            "Stale H values survived under new offset indexes.");
    }

    private static void VerifyNonSplittingTrackRemainsWithoutGeometryEvidence()
    {
        PanelCladdingLayout layout = BuildLayout(
            horizontalOffsets: [],
            verticalOffsets: [45d],
            width: 90d,
            height: 180d,
            ownerByCell: new Dictionary<string, string>
            {
                ["0A"] = string.Empty,
                ["1A"] = "0A"
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
            "Preserve non-splitting V0");

        Require(result.HorizontalOffsets.Count == 0 &&
                result.VerticalOffsets.SequenceEqual([45d]),
            "A full non-splitting V0 must survive surface sync.");
    }

    private static void VerifyNonSplittingTrackFollowsCurrentCurve()
    {
        PanelCladdingLayout layout = BuildLayout(
            horizontalOffsets: [],
            verticalOffsets: [45d],
            width: 90d,
            height: 180d,
            ownerByCell: new Dictionary<string, string>
            {
                ["0A"] = string.Empty,
                ["1A"] = "0A"
            });
        PanelCladdingInferredOffsets result = RequireData(
            PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(
                layout,
                new PanelCladdingInferredOffsets(),
                new PanelCladdingInferredOffsets { VerticalOffsets = [47d] },
                new IReadOnlyList<string>[]
                {
                    new[] { "0A", "1A" }
                }),
            "Refresh structural V0 from curve");

        Require(result.VerticalOffsets.SequenceEqual([47d]),
            "A moved structural curve must refresh its stored track position.");
    }

    private static void VerifyUnmatchedCurveAddsNewStructuralTrack()
    {
        PanelCladdingLayout layout = BuildLayout(
            horizontalOffsets: [10d],
            verticalOffsets: [],
            width: 50d,
            height: 100d,
            ownerByCell: new Dictionary<string, string>
            {
                ["0A"] = string.Empty,
                ["0B"] = string.Empty
            });
        PanelCladdingInferredOffsets result = RequireData(
            PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(
                layout,
                new PanelCladdingInferredOffsets { HorizontalOffsets = [12d] },
                new PanelCladdingInferredOffsets { HorizontalOffsets = [90d] }),
            "Add unmatched structural curve");

        Require(result.HorizontalOffsets.SequenceEqual([12d, 90d]),
            "The refreshed surface track and genuinely unmatched curve track must both remain.");
    }

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
            "Build test layout");
        return new PanelCladdingLayout
        {
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

    private static void VerifyLiveRepositoryUsesRoleAwareLayoutOverload()
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
        Require(source.Contains(
                "ResolveSurfaceScopeOffsets(\n                    panel.Layout,",
                StringComparison.Ordinal) ||
            source.Contains(
                "ResolveSurfaceScopeOffsets(\r\n                    panel.Layout,",
                StringComparison.Ordinal),
            "The live repository must supply the existing layout for track-role classification.");
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

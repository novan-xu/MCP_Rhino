using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PCSyncSrfUnmarkedOffsetClearingSmoke;

internal static class Program
{
    private static void Main()
    {
        VerifyUnmarkedParentGraphCannotPreserveMovedOffsets();
        Console.WriteLine("[OK] unmarked parent relationships cannot preserve moved offsets");

        VerifyUnmarkedHiddenTrackClearsWithoutGeometryEvidence();
        Console.WriteLine("[OK] an unmarked geometry-missing stored track is cleared");

        VerifyMarkedMergedTrackRemainsProtected();
        Console.WriteLine("[OK] complete merged coverage still protects its hidden track");

        VerifyCoverageBlindOverloadClearsStoredTracks();
        Console.WriteLine("[OK] coverage-blind planning never preserves stored tracks");
    }

    private static void VerifyUnmarkedParentGraphCannotPreserveMovedOffsets()
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
                storedSurfaceCoverages: null),
            "Clear unmarked moved offsets");

        Require(result.HorizontalOffsets.SequenceEqual([19.625d, 105.75d, 169.09079d]),
            $"Expected current surface offsets only, found {string.Join(",", result.HorizontalOffsets)}.");
        Require(!result.HorizontalOffsets.Contains(21.375d) &&
                !result.HorizontalOffsets.Contains(107.25d),
            "The unmarked PCEditor parent graph preserved stale offsets.");
    }

    private static void VerifyUnmarkedHiddenTrackClearsWithoutGeometryEvidence()
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

        PanelCladdingInferredOffsets result = RequireData(
            PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(
                layout,
                new PanelCladdingInferredOffsets(),
                curveInferred: null,
                storedSurfaceCoverages: null),
            "Clear unmarked hidden track");

        Require(result.VerticalOffsets.Count == 0,
            "A geometry-missing stored track without baked coverage must be cleared.");
    }

    private static void VerifyMarkedMergedTrackRemainsProtected()
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
            "Preserve explicitly marked hidden track");

        Require(result.VerticalOffsets.SequenceEqual([45d]),
            "Complete baked merged coverage must remain the preservation authority.");
    }

    private static void VerifyCoverageBlindOverloadClearsStoredTracks()
    {
        PanelCladdingInferredOffsets surfaceScopeResult = RequireData(
            PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(
                existingHorizontalOffsets: [21.375d],
                existingVerticalOffsets: [],
                surfaceInferred: new PanelCladdingInferredOffsets
                {
                    HorizontalOffsets = [19.625d]
                },
                curveInferred: new PanelCladdingInferredOffsets
                {
                    HorizontalOffsets = [21.375d]
                },
                panelWidth: 90d,
                panelHeight: 198d,
                modelTolerance: 0.001d),
            "Clear stored tracks without coverage authority");

        Require(surfaceScopeResult.HorizontalOffsets.SequenceEqual([19.625d]),
            "Coverage-blind planning must not preserve the old surface-boundary offset.");

        PanelCladdingInferredOffsets effectiveResult = RequireData(
            PanelCladdingSurfaceSyncPlanningService.ResolveEffectiveOffsets(
                PanelCladdingObjectScope.Surfaces,
                existingHorizontalOffsets: [21.375d],
                existingVerticalOffsets: [],
                inferred: new PanelCladdingInferredOffsets
                {
                    HorizontalOffsets = [19.625d]
                },
                panelWidth: 90d,
                panelHeight: 198d,
                modelTolerance: 0.001d),
            "Clear stored tracks through coverage-blind effective planning");

        Require(effectiveResult.HorizontalOffsets.SequenceEqual([19.625d]),
            "The general surface planner must not retain stored offsets without coverage authority.");
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
            "Build unmarked-offset layout");
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

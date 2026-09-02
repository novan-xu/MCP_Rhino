using System.Text.RegularExpressions;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed partial class PanelCladdingMatchPlanningService : IPanelCladdingMatchPlanningService
{
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelCladdingLogicService _claddingLogic;
    private readonly PanelCladdingLogicalCellService _logicalCells;
    private readonly PanelCladdingRegionService _regions;
    private readonly PanelCladdingMatchFeasibilityService _feasibility;

    public PanelCladdingMatchPlanningService(PanelCladdingKeyService keys)
    {
        _keys = keys;
        _claddingLogic = new PanelCladdingLogicService();
        _logicalCells = new PanelCladdingLogicalCellService();
        _regions = new PanelCladdingRegionService(keys);
        _feasibility = new PanelCladdingMatchFeasibilityService(keys);
    }

    public OperationResponse<PanelCladdingMatchPlan> CreatePlan(
        PanelCladdingMatchPanelSnapshot source,
        IReadOnlyList<PanelCladdingMatchPanelSnapshot> targets)
    {
        if (source.ObjectId == Guid.Empty)
        {
            return OperationResponse<PanelCladdingMatchPlan>.Fail(
                "PANEL_CLADDING_MATCH_SOURCE_REQUIRED");
        }

        PanelCladdingMatchPanelSnapshot[] distinctTargets = (targets ??
                Array.Empty<PanelCladdingMatchPanelSnapshot>())
            .Where(target => target.ObjectId != Guid.Empty)
            .GroupBy(target => target.ObjectId)
            .Select(group => group.First())
            .ToArray();
        if (distinctTargets.Length == 0)
        {
            return OperationResponse<PanelCladdingMatchPlan>.Fail(
                "PANEL_CLADDING_MATCH_TARGET_REQUIRED");
        }
        if (distinctTargets.Any(target => target.ObjectId == source.ObjectId))
        {
            return OperationResponse<PanelCladdingMatchPlan>.Fail(
                "PANEL_CLADDING_MATCH_SOURCE_IS_TARGET");
        }

        OperationResponse<SourceCladdingConfiguration> configuration =
            BuildSourceConfiguration(source);
        if (!configuration.Success || configuration.Data is null)
        {
            return OperationResponse<PanelCladdingMatchPlan>.Fail(configuration.Message);
        }

        var targetPlans = new List<PanelCladdingMatchTargetPlan>(distinctTargets.Length);
        foreach (PanelCladdingMatchPanelSnapshot target in distinctTargets)
        {
            OperationResponse<TargetCladdingConfiguration> targetConfiguration =
                BuildTargetConfiguration(
                configuration.Data,
                target);
            if (!targetConfiguration.Success || targetConfiguration.Data is null)
            {
                return OperationResponse<PanelCladdingMatchPlan>.Fail(
                    $"PANEL_CLADDING_MATCH_GEOMETRY_MISMATCH: {target.ObjectId:D}: " +
                    targetConfiguration.Message);
            }

            var deletes = target.UserText.Keys
                .Where(IsTransferConfigurationKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            deletes.Add(PanelCladdingKeyService.SignatureKey);
            deletes.Add(PanelCladdingKeyService.LegacySignatureKey);
            targetPlans.Add(new PanelCladdingMatchTargetPlan
            {
                ObjectId = target.ObjectId,
                UserTextDeletes = deletes.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray(),
                UserTextWrites = new Dictionary<string, string>(
                    targetConfiguration.Data.Writes,
                    StringComparer.OrdinalIgnoreCase)
            });
        }

        return OperationResponse<PanelCladdingMatchPlan>.Ok(new PanelCladdingMatchPlan
        {
            SourceObjectId = source.ObjectId,
            Targets = targetPlans
        });
    }

    public static IReadOnlyDictionary<string, string> ApplyUserTextPlan(
        IReadOnlyDictionary<string, string> current,
        PanelCladdingMatchTargetPlan plan)
    {
        var result = new Dictionary<string, string>(
            current ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
        foreach (string key in plan.UserTextDeletes ?? Array.Empty<string>())
        {
            result.Remove(key);
        }
        foreach ((string key, string value) in plan.UserTextWrites ??
            new Dictionary<string, string>())
        {
            result[key] = value;
        }
        return result;
    }

    public static OperationResponse ValidateAppliedUserTextPlan(
        IReadOnlyDictionary<string, string> current,
        PanelCladdingMatchTargetPlan plan)
    {
        var actual = new Dictionary<string, string>(
            current ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string> writes = plan.UserTextWrites ??
            new Dictionary<string, string>();
        foreach ((string key, string expected) in writes)
        {
            if (!actual.TryGetValue(key, out string? value) ||
                !string.Equals(value, expected, StringComparison.Ordinal))
            {
                return OperationResponse.Fail(
                    $"PANEL_CLADDING_MATCH_WRITE_MISMATCH: {key}: expected '{expected}', actual '{value ?? "<missing>"}'");
            }
        }
        foreach (string key in plan.UserTextDeletes ?? Array.Empty<string>())
        {
            if (!writes.ContainsKey(key) && actual.ContainsKey(key))
            {
                return OperationResponse.Fail(
                    $"PANEL_CLADDING_MATCH_DELETE_MISMATCH: {key}");
            }
        }
        return OperationResponse.Ok();
    }

    private OperationResponse<SourceCladdingConfiguration> BuildSourceConfiguration(
        PanelCladdingMatchPanelSnapshot source)
    {
        if (source.Geometry.GeometryClass == PanelGeometryClass.UnsupportedProjection)
        {
            return SourceNotConfigured(source.ObjectId, "unsupported panel projection");
        }

        OperationResponse<PanelCladdingKeySet> parsed = _keys.Parse(
            source.UserText,
            source.Geometry.Width,
            source.Geometry.Height,
            source.Geometry.ModelTolerance);
        if (!parsed.Success || parsed.Data is null)
        {
            return SourceNotConfigured(source.ObjectId, parsed.Message);
        }

        OperationResponse<PanelCladdingRegionSet> regions = _regions.Resolve(
            parsed.Data.Cells,
            requirePopulatedCells: false);
        if (!regions.Success || regions.Data is null)
        {
            return SourceNotConfigured(source.ObjectId, regions.Message);
        }

        var physicalValues = parsed.Data.Cells.ToDictionary(
            cell => cell.UserTextKey,
            cell => _keys.NormalizeCladdingValue(cell.Value),
            StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string> logicalValues = _logicalCells.Collapse(
            parsed.Data.Cells,
            parsed.Data.Topology,
            physicalValues);

        var writes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string value) in logicalValues)
        {
            // PCMatchSrf transfers the source assignment graph, not merely an equivalent
            // canonical material region. Collapse removes physical cells hidden by the source's
            // merge topology and remaps parent references to their surviving representatives.
            writes[key] = PanelCladdingKeyService.EncodeCellValueForStorage(value);
        }
        var explicitSourceCellKeys = source.UserText.Keys
            .Where(_keys.IsCladdingCellKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in parsed.Data.Cells.Where(cell =>
            explicitSourceCellKeys.Contains(cell.UserTextKey)))
        {
            // PCMatchSrf is cell-only: an explicitly persisted source cell is configuration
            // truth even when extrusion topology would otherwise hide that physical cell.
            // This keeps parent tokens such as 1A=0A from being silently discarded while
            // still omitting topology-hidden cells that are genuinely absent from source text.
            writes[cell.UserTextKey] = PanelCladdingKeyService.EncodeCellValueForStorage(
                physicalValues[cell.UserTextKey]);
        }
        OperationResponse<string> claddingLogic = _claddingLogic.Encode(
            parsed.Data.Cells,
            physicalValues);
        if (!claddingLogic.Success || claddingLogic.Data is null)
        {
            return SourceNotConfigured(source.ObjectId, claddingLogic.Message);
        }
        writes[PanelCladdingKeyService.CladdingLogicKey] = claddingLogic.Data;

        return OperationResponse<SourceCladdingConfiguration>.Ok(new SourceCladdingConfiguration
        {
            Writes = writes,
            KeySet = parsed.Data
        });
    }

    private OperationResponse<TargetCladdingConfiguration> BuildTargetConfiguration(
        SourceCladdingConfiguration configuration,
        PanelCladdingMatchPanelSnapshot target)
    {
        if (target.Geometry.GeometryClass == PanelGeometryClass.UnsupportedProjection)
        {
            return OperationResponse<TargetCladdingConfiguration>.Fail(
                "unsupported panel projection");
        }

        OperationResponse<PanelCladdingKeySet> targetLayout = _keys.Parse(
            target.UserText,
            target.Geometry.Width,
            target.Geometry.Height,
            target.Geometry.ModelTolerance);
        if (!targetLayout.Success || targetLayout.Data is null)
        {
            return OperationResponse<TargetCladdingConfiguration>.Fail(targetLayout.Message);
        }

        OperationResponse<PanelCladdingMatchMapping> mapping = _feasibility.CreateMapping(
            configuration.KeySet,
            targetLayout.Data);
        if (!mapping.Success || mapping.Data is null)
        {
            return OperationResponse<TargetCladdingConfiguration>.Fail(mapping.Message);
        }

        bool samePhysicalGrid = configuration.KeySet.HorizontalOffsets.Count ==
                targetLayout.Data.HorizontalOffsets.Count &&
            configuration.KeySet.VerticalOffsets.Count ==
                targetLayout.Data.VerticalOffsets.Count &&
            configuration.KeySet.Topology.MissingSegments.ToHashSet().SetEquals(
                targetLayout.Data.Topology.MissingSegments);
        if (samePhysicalGrid)
        {
            return OperationResponse<TargetCladdingConfiguration>.Ok(
                new TargetCladdingConfiguration
                {
                    Writes = configuration.Writes
                });
        }

        var writes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string value) in mapping.Data.TargetCellValues)
        {
            writes[key] = PanelCladdingKeyService.EncodeCellValueForStorage(value);
        }
        writes[PanelCladdingKeyService.CladdingLogicKey] = mapping.Data.CladdingLogic;
        return OperationResponse<TargetCladdingConfiguration>.Ok(
            new TargetCladdingConfiguration
            {
                Writes = writes
            });
    }

    private static bool IsTransferConfigurationKey(string key)
    {
        return CladdingCellRegex().IsMatch(key) || string.Equals(
            key,
            PanelCladdingKeyService.CladdingLogicKey,
            StringComparison.OrdinalIgnoreCase);
    }

    private static OperationResponse<SourceCladdingConfiguration> SourceNotConfigured(
        Guid sourceObjectId,
        string detail)
    {
        return OperationResponse<SourceCladdingConfiguration>.Fail(
            $"PANEL_CLADDING_MATCH_SOURCE_NOT_CONFIGURED: {sourceObjectId:D}: {detail}");
    }

    [GeneratedRegex(@"^CW_4\.\d{2}_CLADDING_\d+[A-Z]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CladdingCellRegex();

    private sealed class SourceCladdingConfiguration
    {
        public IReadOnlyDictionary<string, string> Writes { get; init; } =
            new Dictionary<string, string>();
        public PanelCladdingKeySet KeySet { get; init; } = new();
    }

    private sealed class TargetCladdingConfiguration
    {
        public IReadOnlyDictionary<string, string> Writes { get; init; } =
            new Dictionary<string, string>();
    }
}

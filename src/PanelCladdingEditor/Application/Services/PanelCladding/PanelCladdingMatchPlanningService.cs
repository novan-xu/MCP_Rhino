using System.Globalization;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed partial class PanelCladdingMatchPlanningService
{
    private readonly PanelCladdingKeyService _keys;

    public PanelCladdingMatchPlanningService(PanelCladdingKeyService keys)
    {
        _keys = keys;
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
            if (IsConfiguredTarget(target.UserText))
            {
                return OperationResponse<PanelCladdingMatchPlan>.Fail(
                    $"PANEL_CLADDING_MATCH_TARGET_ALREADY_CONFIGURED: {target.ObjectId:D}");
            }

            OperationResponse compatible = ValidateTargetCompatibility(
                configuration.Data,
                target);
            if (!compatible.Success)
            {
                return OperationResponse<PanelCladdingMatchPlan>.Fail(
                    $"PANEL_CLADDING_MATCH_GEOMETRY_MISMATCH: {target.ObjectId:D}: {compatible.Message}");
            }

            var deletes = target.UserText.Keys
                .Where(IsTransferConfigurationKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            deletes.Add(PanelCladdingKeyService.TypeCodeKey);
            deletes.Add(PanelCladdingKeyService.LegacyTypeCodeKey);
            deletes.Add(PanelCladdingKeyService.SignatureKey);
            deletes.Add(PanelCladdingKeyService.LegacySignatureKey);
            targetPlans.Add(new PanelCladdingMatchTargetPlan
            {
                ObjectId = target.ObjectId,
                UserTextDeletes = deletes.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray(),
                UserTextWrites = new Dictionary<string, string>(configuration.Data.Writes, StringComparer.OrdinalIgnoreCase)
            });
        }

        return OperationResponse<PanelCladdingMatchPlan>.Ok(new PanelCladdingMatchPlan
        {
            SourceObjectId = source.ObjectId,
            Targets = targetPlans
        });
    }

    private OperationResponse<SourceCladdingConfiguration> BuildSourceConfiguration(
        PanelCladdingMatchPanelSnapshot source)
    {
        if (source.Geometry.GeometryClass == PanelGeometryClass.UnsupportedProjection)
        {
            return SourceNotConfigured(source.ObjectId, "unsupported panel projection");
        }

        if (!TryGetNonblank(source.UserText, PanelCladdingKeyService.TypeCodeKey, out string typeCode) ||
            !TryGetNonblank(source.UserText, PanelCladdingKeyService.SignatureKey, out string signature))
        {
            return SourceNotConfigured(
                source.ObjectId,
                $"expected {PanelCladdingKeyService.TypeCodeKey} and {PanelCladdingKeyService.SignatureKey}");
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

        var writes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.TypeCodeKey] = typeCode,
            [PanelCladdingKeyService.SignatureKey] = signature
        };
        foreach (PanelCladdingCell cell in parsed.Data.Cells)
        {
            string material = _keys.NormalizeCladdingValue(cell.Value);
            if (material.Length == 0)
            {
                return SourceNotConfigured(
                    source.ObjectId,
                    $"blank cladding cell {cell.ShortLabel} ({cell.UserTextKey})");
            }
            writes[cell.UserTextKey] = material;
        }

        return OperationResponse<SourceCladdingConfiguration>.Ok(new SourceCladdingConfiguration
        {
            Writes = writes,
            CellLabels = parsed.Data.Cells.Select(cell => cell.ShortLabel).ToArray()
        });
    }

    private OperationResponse ValidateTargetCompatibility(
        SourceCladdingConfiguration configuration,
        PanelCladdingMatchPanelSnapshot target)
    {
        if (target.Geometry.GeometryClass == PanelGeometryClass.UnsupportedProjection)
        {
            return OperationResponse.Fail("unsupported panel projection");
        }

        OperationResponse<PanelCladdingKeySet> targetLayout = _keys.Parse(
            target.UserText,
            target.Geometry.Width,
            target.Geometry.Height,
            target.Geometry.ModelTolerance);
        if (!targetLayout.Success || targetLayout.Data is null)
        {
            return OperationResponse.Fail(targetLayout.Message);
        }

        string[] targetCellLabels = targetLayout.Data.Cells
            .Select(cell => cell.ShortLabel)
            .ToArray();
        if (!configuration.CellLabels.SequenceEqual(
            targetCellLabels,
            StringComparer.OrdinalIgnoreCase))
        {
            return OperationResponse.Fail(
                $"cladding cell topology {string.Join(',', targetCellLabels)} does not match " +
                string.Join(',', configuration.CellLabels));
        }

        return OperationResponse.Ok();
    }

    private static bool IsConfiguredTarget(IReadOnlyDictionary<string, string> userText)
    {
        foreach ((string key, string value) in userText)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }
            if (CladdingCellRegex().IsMatch(key) ||
                string.Equals(key, PanelCladdingKeyService.TypeCodeKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, PanelCladdingKeyService.LegacyTypeCodeKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, PanelCladdingKeyService.SignatureKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, PanelCladdingKeyService.LegacySignatureKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsTransferConfigurationKey(string key)
    {
        return CladdingCellRegex().IsMatch(key) ||
            string.Equals(key, PanelCladdingKeyService.TypeCodeKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, PanelCladdingKeyService.LegacyTypeCodeKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, PanelCladdingKeyService.SignatureKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, PanelCladdingKeyService.LegacySignatureKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetNonblank(
        IReadOnlyDictionary<string, string> userText,
        string key,
        out string value)
    {
        foreach ((string existingKey, string existingValue) in userText)
        {
            if (string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(existingValue))
            {
                value = existingValue.Trim();
                return true;
            }
        }
        value = string.Empty;
        return false;
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
        public IReadOnlyList<string> CellLabels { get; init; } = Array.Empty<string>();
    }
}

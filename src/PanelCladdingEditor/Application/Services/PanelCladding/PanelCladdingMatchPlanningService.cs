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

        OperationResponse<IReadOnlyDictionary<string, string>> configuration =
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

            OperationResponse compatible = ValidateGeometryCompatibility(
                source.Geometry,
                target.Geometry);
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
                UserTextWrites = new Dictionary<string, string>(configuration.Data, StringComparer.OrdinalIgnoreCase)
            });
        }

        return OperationResponse<PanelCladdingMatchPlan>.Ok(new PanelCladdingMatchPlan
        {
            SourceObjectId = source.ObjectId,
            Targets = targetPlans
        });
    }

    private OperationResponse<IReadOnlyDictionary<string, string>> BuildSourceConfiguration(
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
        for (int index = 0; index < parsed.Data.HorizontalOffsets.Count; index++)
        {
            writes[$"CW_2.03_OFFSET_H{index}"] = FormatDistance(parsed.Data.HorizontalOffsets[index]);
        }
        for (int index = 0; index < parsed.Data.VerticalOffsets.Count; index++)
        {
            writes[$"CW_2.04_OFFSET_V{index}"] = FormatDistance(parsed.Data.VerticalOffsets[index]);
        }
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

        return OperationResponse<IReadOnlyDictionary<string, string>>.Ok(writes);
    }

    private static OperationResponse ValidateGeometryCompatibility(
        PanelCladdingMatchGeometryDescriptor source,
        PanelCladdingMatchGeometryDescriptor target)
    {
        if (source.GeometryClass == PanelGeometryClass.UnsupportedProjection ||
            target.GeometryClass == PanelGeometryClass.UnsupportedProjection)
        {
            return OperationResponse.Fail("unsupported panel projection");
        }
        if (source.GeometryClass != target.GeometryClass)
        {
            return OperationResponse.Fail(
                $"geometry class {target.GeometryClass} does not match {source.GeometryClass}");
        }

        double tolerance = Math.Max(source.ModelTolerance, target.ModelTolerance);
        if (!double.IsFinite(tolerance) || tolerance <= 0d)
        {
            tolerance = 1e-6d;
        }
        if (Math.Abs(source.Width - target.Width) > tolerance ||
            Math.Abs(source.Height - target.Height) > tolerance)
        {
            return OperationResponse.Fail(
                $"local extents {target.Width:G17}x{target.Height:G17} do not match " +
                $"{source.Width:G17}x{source.Height:G17}");
        }

        if (source.GeometryClass == PanelGeometryClass.Curved)
        {
            if (source.DepthSamples.Count == 0 ||
                source.DepthSamples.Count != target.DepthSamples.Count)
            {
                return OperationResponse.Fail("curved depth sample counts do not match");
            }
            double depthTolerance = Math.Max(
                tolerance,
                Math.Max(source.Width, source.Height) * 1e-6d);
            for (int index = 0; index < source.DepthSamples.Count; index++)
            {
                if (!double.IsFinite(source.DepthSamples[index]) ||
                    !double.IsFinite(target.DepthSamples[index]) ||
                    Math.Abs(source.DepthSamples[index] - target.DepthSamples[index]) > depthTolerance)
                {
                    return OperationResponse.Fail($"curved depth profile differs at sample {index}");
                }
            }
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
        return HorizontalOffsetRegex().IsMatch(key) ||
            VerticalOffsetRegex().IsMatch(key) ||
            CladdingCellRegex().IsMatch(key) ||
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

    private static OperationResponse<IReadOnlyDictionary<string, string>> SourceNotConfigured(
        Guid sourceObjectId,
        string detail)
    {
        return OperationResponse<IReadOnlyDictionary<string, string>>.Fail(
            $"PANEL_CLADDING_MATCH_SOURCE_NOT_CONFIGURED: {sourceObjectId:D}: {detail}");
    }

    private static string FormatDistance(double value)
    {
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^CW_2\.03_OFFSET_H\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalOffsetRegex();

    [GeneratedRegex(@"^CW_2\.04_OFFSET_V\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VerticalOffsetRegex();

    [GeneratedRegex(@"^CW_4\.\d{2}_CLADDING_\d+[A-Z]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CladdingCellRegex();
}

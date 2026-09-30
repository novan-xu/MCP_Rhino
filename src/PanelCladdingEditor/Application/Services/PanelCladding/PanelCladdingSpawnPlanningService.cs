using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingSpawnPlanningService
{
    public const string MaterialSurfaceRootLayer = "04_STEP Surfaces";
    public const string LegacyMaterialSurfaceRootLayer = "03_Material Surfaces (STEP)";
    public const string ExtrusionRootLayer = "02_CW Extrusions";
    public const string PanelIdUserTextKey = "CW_1.01_PID";
    public const string CidUserTextKey = "CW_1.02_CID";
    public const string ReleaseUserTextKey = "CW_1.05_RELEASE";
    public const string WallTypeUserTextKey = "CW_1.07_WALL_TYPE";

    private readonly PanelCladdingRegionService _regions;
    private readonly IReadOnlyDictionary<string, PanelColorRgb> _configuredMaterialColors;
    private readonly PanelCladdingLogicalCellService _logicalCells = new();
    private readonly PanelCladdingExtrusionPlanningService _extrusions = new();
    private readonly PanelCladdingSurfaceCoverageService _surfaceCoverage = new();

    public PanelCladdingSpawnPlanningService(
        PanelCladdingKeyService keys,
        IReadOnlyDictionary<string, PanelColorRgb>? configuredMaterialColors = null)
    {
        _regions = new PanelCladdingRegionService(keys);
        var colors = new Dictionary<string, PanelColorRgb>(StringComparer.OrdinalIgnoreCase);
        foreach ((string code, PanelColorRgb color) in configuredMaterialColors ??
            new Dictionary<string, PanelColorRgb>())
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                colors[code.Trim()] = color;
            }
        }
        _configuredMaterialColors = colors;
    }

    public OperationResponse<PanelCladdingSpawnPlan> CreatePlan(
        IReadOnlyDictionary<string, string> panelUserText,
        PanelCladdingKeySet keySet)
    {
        return CreatePlanCore(panelUserText, keySet, null, null, null, allowEmptyRegions: false);
    }

    public OperationResponse<PanelCladdingSpawnPlan> CreateUpdatePlan(
        IReadOnlyDictionary<string, string> panelUserText,
        PanelCladdingKeySet keySet)
    {
        return CreatePlanCore(panelUserText, keySet, null, null, null, allowEmptyRegions: true);
    }

    public OperationResponse<PanelCladdingSpawnPlan> CreatePlan(
        IReadOnlyDictionary<string, string> panelUserText,
        PanelCladdingKeySet keySet,
        double panelWidth,
        double panelHeight,
        string sourcePanelLayerPath)
    {
        return CreatePlanCore(
            panelUserText,
            keySet,
            panelWidth,
            panelHeight,
            sourcePanelLayerPath,
            allowEmptyRegions: false);
    }

    private OperationResponse<PanelCladdingSpawnPlan> CreatePlanCore(
        IReadOnlyDictionary<string, string> panelUserText,
        PanelCladdingKeySet keySet,
        double? panelWidth,
        double? panelHeight,
        string? sourcePanelLayerPath,
        bool allowEmptyRegions)
    {
        OperationResponse<KeyValuePair<string, string>> panelId = FindRequiredMetadata(
            panelUserText,
            PanelIdUserTextKey,
            "PID");
        if (!panelId.Success)
        {
            return OperationResponse<PanelCladdingSpawnPlan>.Fail(panelId.Message);
        }

        OperationResponse<KeyValuePair<string, string>> release = FindRequiredMetadata(
            panelUserText,
            ReleaseUserTextKey,
            "RELEASE_NUMBER");
        if (!release.Success)
        {
            return OperationResponse<PanelCladdingSpawnPlan>.Fail(release.Message);
        }

        OperationResponse<KeyValuePair<string, string>> wallType = FindRequiredMetadata(
            panelUserText,
            WallTypeUserTextKey,
            "WALL_TYPE");
        if (!wallType.Success)
        {
            return OperationResponse<PanelCladdingSpawnPlan>.Fail(wallType.Message);
        }

        string pid = panelId.Data.Value.Trim();
        var inherited = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [panelId.Data.Key] = pid,
            [release.Data.Key] = release.Data.Value.Trim(),
            [wallType.Data.Key] = wallType.Data.Value.Trim()
        };
        IReadOnlyDictionary<string, string> expandedValues = _logicalCells.Expand(
            keySet.Cells,
            keySet.Topology,
            keySet.Cells.ToDictionary(
                cell => cell.UserTextKey,
                cell => cell.Value,
                StringComparer.OrdinalIgnoreCase));
        OperationResponse<PanelCladdingRegionSet> regionResponse = _regions.Resolve(
            keySet.Cells,
            expandedValues);
        if (!regionResponse.Success || regionResponse.Data is null)
        {
            return OperationResponse<PanelCladdingSpawnPlan>.Fail(regionResponse.Message);
        }

        var regions = new List<PanelCladdingSpawnRegionPlan>();
        foreach (PanelCladdingRegion region in regionResponse.Data.Regions)
        {
            string material = region.MaterialCode;
            if (!IsValidLayerSegment(material))
            {
                return OperationResponse<PanelCladdingSpawnPlan>.Fail(
                    $"PANEL_CLADDING_MATERIAL_LAYER_NAME_INVALID: {region.OwnerCellLabel} material '{material}' cannot be used as a Rhino layer name.");
            }

            string cid = PanelCladdingCidService.SurfaceCid(pid, region.OwnerCellLabel, panelUserText);
            OperationResponse<string> coverage = _surfaceCoverage.Encode(
                region.OwnerCellLabel,
                region.Cells);
            if (!coverage.Success || coverage.Data is null)
            {
                return OperationResponse<PanelCladdingSpawnPlan>.Fail(coverage.Message);
            }
            var writes = new Dictionary<string, string>(inherited, StringComparer.Ordinal)
            {
                [CidUserTextKey] = cid,
                [PanelCladdingSurfaceCoverageService.UserTextKey] = coverage.Data,
                ["Cladding"] = material
            };
            regions.Add(new PanelCladdingSpawnRegionPlan
            {
                OwnerCellLabel = region.OwnerCellLabel,
                Cells = region.Cells,
                CladdingCode = material,
                Cid = cid,
                LayerPath = $"{MaterialSurfaceRootLayer}::{ResolveMaterialFamilyLayer(material)}::{material}",
                LayerColor = _configuredMaterialColors.TryGetValue(material, out PanelColorRgb configuredColor)
                    ? configuredColor
                    : ResolveMaterialLayerColor(material),
                UserTextWrites = writes
            });
        }

        if (regions.Count == 0 && !allowEmptyRegions)
        {
            return OperationResponse<PanelCladdingSpawnPlan>.Fail(
                "PANEL_CLADDING_NO_POPULATED_CELLS: no cladding surfaces were planned.");
        }
        if (regions.Select(region => region.Cid).Distinct(StringComparer.OrdinalIgnoreCase).Count() != regions.Count)
        {
            return OperationResponse<PanelCladdingSpawnPlan>.Fail("PANEL_CLADDING_DUPLICATE_PLANNED_CID");
        }

        IReadOnlyList<PanelCladdingExtrusionCurvePlan> curves =
            Array.Empty<PanelCladdingExtrusionCurvePlan>();
        if (panelWidth.HasValue && panelHeight.HasValue)
        {
            OperationResponse<KeyValuePair<string, string>> panelCid = FindRequiredMetadata(
                panelUserText,
                CidUserTextKey,
                "CID");
            if (!panelCid.Success && PanelCladdingCidService.PanelCidWrite(panelUserText) is null)
            {
                return OperationResponse<PanelCladdingSpawnPlan>.Fail(panelCid.Message);
            }
            OperationResponse<IReadOnlyList<PanelCladdingExtrusionCurvePlan>> extrusionPlan =
                _extrusions.CreatePlan(
                    pid,
                    PanelCladdingCidService.ResolvePanelCid(pid, panelUserText),
                    panelWidth.Value,
                    panelHeight.Value,
                    keySet,
                    sourcePanelLayerPath ?? string.Empty,
                    release.Data.Value);
            if (!extrusionPlan.Success || extrusionPlan.Data is null)
            {
                return OperationResponse<PanelCladdingSpawnPlan>.Fail(extrusionPlan.Message);
            }
            curves = extrusionPlan.Data;
        }

        return OperationResponse<PanelCladdingSpawnPlan>.Ok(new PanelCladdingSpawnPlan
        {
            PanelId = pid,
            Regions = regions,
            Curves = curves
        });
    }

    public static string ResolveMaterialFamilyLayer(string materialCode)
    {
        string code = new string((materialCode ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .TakeWhile(char.IsLetter)
            .ToArray());
        if (code.StartsWith("GL", StringComparison.Ordinal))
        {
            return "Surfaces-Glass";
        }
        if (code is "AL" or "ALU" or "ACM" or "ACP" or "MPL" or "MTL" or "SS" or "STL")
        {
            return "Surfaces-Metal";
        }
        if (code is "STN" or "STONE" or "GR" or "GRANITE" or "MAR" or "MARBLE")
        {
            return "Surfaces-Stone";
        }
        if (code is "TER" or "TC" or "TERRA" or "TERRACOTTA")
        {
            return "Surfaces-Terracotta";
        }
        if (code is "CON" or "CONC" or "GFRC" or "GRC")
        {
            return "Surfaces-Concrete";
        }
        if (code is "WD" or "WOOD" or "TIM" or "TIMBER")
        {
            return "Surfaces-Wood";
        }
        return "Surfaces-Other";
    }

    public static bool IsSupportedMaterialSurfaceRoot(string layerName)
    {
        string normalized = layerName?.Trim() ?? string.Empty;
        return string.Equals(normalized, MaterialSurfaceRootLayer, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, LegacyMaterialSurfaceRootLayer, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsManagedSurfaceLayerPath(string layerPath) =>
        IsAtOrBelowLayerRoot(layerPath, MaterialSurfaceRootLayer);

    public static bool IsManagedExtrusionLayerPath(string layerPath) =>
        IsAtOrBelowLayerRoot(layerPath, ExtrusionRootLayer);

    private static bool IsAtOrBelowLayerRoot(string layerPath, string rootLayer)
    {
        string normalized = layerPath?.Trim() ?? string.Empty;
        return string.Equals(normalized, rootLayer, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(rootLayer + "::", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildSurfaceCid(string panelId, string cellLabel)
    {
        string cidBase = PanelCladdingCidService.FromPanelId(panelId);
        return $"{cidBase}-{(cellLabel ?? string.Empty).Trim()}";
    }

    public static PanelColorRgb ResolveMaterialLayerColor(string materialCode)
    {
        const int variationCount = 5;
        int variantIndex = ResolveMaterialVariantIndex(materialCode, variationCount);
        string family = ResolveMaterialFamilyLayer(materialCode);
        if (family == "Surfaces-Glass")
        {
            PanelColorRgb[] glassPalette =
            {
                new(89, 131, 144),
                new(70, 104, 115),
                new(113, 160, 174),
                new(52, 76, 84),
                new(80, 118, 129)
            };
            return glassPalette[variantIndex];
        }

        PanelColorRgb baseColor = family switch
        {
            "Surfaces-Metal" => new PanelColorRgb(110, 119, 128),
            "Surfaces-Stone" => new PanelColorRgb(150, 132, 109),
            "Surfaces-Terracotta" => new PanelColorRgb(158, 87, 56),
            "Surfaces-Concrete" => new PanelColorRgb(128, 133, 130),
            "Surfaces-Wood" => new PanelColorRgb(139, 91, 52),
            _ => new PanelColorRgb(120, 105, 143)
        };
        double[] lightnessOffsets = { 0.07d, -0.07d, 0.18d, -0.18d, 0d };
        return AdjustHslLightness(baseColor, lightnessOffsets[variantIndex]);
    }

    private static int ResolveMaterialVariantIndex(string materialCode, int variationCount)
    {
        string normalized = (materialCode ?? string.Empty).Trim().ToUpperInvariant();
        int digitStart = normalized.Length;
        while (digitStart > 0 && char.IsDigit(normalized[digitStart - 1]))
        {
            digitStart--;
        }
        if (digitStart < normalized.Length &&
            int.TryParse(normalized.AsSpan(digitStart), out int numericSuffix) && numericSuffix > 0)
        {
            return (numericSuffix - 1) % variationCount;
        }

        uint hash = 2166136261U;
        foreach (char character in normalized)
        {
            hash = (hash ^ character) * 16777619U;
        }
        return (int)(hash % (uint)variationCount);
    }

    private static PanelColorRgb AdjustHslLightness(PanelColorRgb color, double lightnessOffset)
    {
        double red = color.Red / 255d;
        double green = color.Green / 255d;
        double blue = color.Blue / 255d;
        double max = Math.Max(red, Math.Max(green, blue));
        double min = Math.Min(red, Math.Min(green, blue));
        double lightness = 0.5d * (max + min);
        double hue = 0d;
        double saturation = 0d;
        double delta = max - min;
        if (delta > 1e-12d)
        {
            saturation = lightness > 0.5d
                ? delta / (2d - max - min)
                : delta / (max + min);
            if (Math.Abs(max - red) < 1e-12d)
            {
                hue = (green - blue) / delta + (green < blue ? 6d : 0d);
            }
            else if (Math.Abs(max - green) < 1e-12d)
            {
                hue = (blue - red) / delta + 2d;
            }
            else
            {
                hue = (red - green) / delta + 4d;
            }
            hue /= 6d;
        }

        lightness = Math.Clamp(lightness + lightnessOffset, 0.2d, 0.85d);
        if (saturation <= 1e-12d)
        {
            byte gray = ToColorByte(lightness);
            return new PanelColorRgb(gray, gray, gray);
        }

        double q = lightness < 0.5d
            ? lightness * (1d + saturation)
            : lightness + saturation - lightness * saturation;
        double p = 2d * lightness - q;
        return new PanelColorRgb(
            ToColorByte(HueToRgb(p, q, hue + 1d / 3d)),
            ToColorByte(HueToRgb(p, q, hue)),
            ToColorByte(HueToRgb(p, q, hue - 1d / 3d)));
    }

    private static double HueToRgb(double p, double q, double hue)
    {
        if (hue < 0d) hue += 1d;
        if (hue > 1d) hue -= 1d;
        if (hue < 1d / 6d) return p + (q - p) * 6d * hue;
        if (hue < 0.5d) return q;
        if (hue < 2d / 3d) return p + (q - p) * (2d / 3d - hue) * 6d;
        return p;
    }

    private static byte ToColorByte(double channel)
    {
        return (byte)Math.Clamp(
            (int)Math.Round(channel * 255d, MidpointRounding.AwayFromZero),
            0,
            255);
    }

    private static OperationResponse<KeyValuePair<string, string>> FindRequiredMetadata(
        IReadOnlyDictionary<string, string> panelUserText,
        string canonicalKey,
        string semanticName)
    {
        foreach (KeyValuePair<string, string> item in panelUserText)
        {
            if (string.Equals(item.Key, canonicalKey, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(item.Value))
            {
                return OperationResponse<KeyValuePair<string, string>>.Ok(item);
            }
        }
        return OperationResponse<KeyValuePair<string, string>>.Fail(
            $"PANEL_CLADDING_{semanticName}_REQUIRED: expected {canonicalKey}.");
    }

    private static bool IsValidLayerSegment(string value)
    {
        return value.Length > 0 &&
            !value.Contains("::", StringComparison.Ordinal) &&
            value.IndexOfAny(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }) < 0;
    }
}

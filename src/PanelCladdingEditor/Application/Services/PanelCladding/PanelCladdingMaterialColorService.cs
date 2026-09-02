using System.Globalization;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingMaterialColorService
{
    public OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>> CreateColorMap(
        PanelCladdingMaterialCatalog catalog)
    {
        if (catalog is null)
        {
            return OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>>.Fail(
                "PANEL_CLADDING_MATERIAL_CATALOG_REQUIRED");
        }

        var colors = new Dictionary<string, PanelColorRgb>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingMaterialCatalogItem material in catalog.Materials)
        {
            string code = material.Code.Trim();
            if (code.Length == 0)
            {
                return OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>>.Fail(
                    "PANEL_CLADDING_MATERIAL_COLOR_CODE_REQUIRED");
            }
            OperationResponse<PanelColorRgb> parsed = ParseHex(material.ColorHex);
            if (!parsed.Success)
            {
                return OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>>.Fail(
                    $"PANEL_CLADDING_MATERIAL_COLOR_INVALID: {code}: {parsed.Message}");
            }
            if (!colors.TryAdd(code, parsed.Data))
            {
                return OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>>.Fail(
                    $"PANEL_CLADDING_MATERIAL_COLOR_DUPLICATE: {code}");
            }
        }
        return OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>>.Ok(colors);
    }

    public OperationResponse<PanelColorRgb> ParseHex(string colorHex)
    {
        string normalized = (colorHex ?? string.Empty).Trim();
        if (normalized.Length != 7 || normalized[0] != '#' ||
            !byte.TryParse(normalized.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte red) ||
            !byte.TryParse(normalized.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte green) ||
            !byte.TryParse(normalized.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte blue))
        {
            return OperationResponse<PanelColorRgb>.Fail(
                $"expected #RRGGBB, received '{normalized}'.");
        }
        return OperationResponse<PanelColorRgb>.Ok(new PanelColorRgb(red, green, blue));
    }
}

using System.Globalization;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelFrameProfileFormulaService
{
    public OperationResponse<string> CreateFormula(PanelFrameProfileDefinition definition, double modifier)
    {
        if (!double.IsFinite(modifier))
        {
            return OperationResponse<string>.Fail("PANEL_FRAME_PROFILE_MODIFIER_INVALID");
        }
        string length = LengthExpression(modifier);
        if (definition.Dimension == PanelFrameProfileDimension.OneDimensional)
        {
            int quantity = (int)Math.Round(definition.CalculationValue ?? 1d);
            return quantity <= 0
                ? OperationResponse<string>.Fail($"PANEL_FRAME_PROFILE_QUANTITY_INVALID: {definition.Code}")
                : OperationResponse<string>.Ok(quantity == 1 ? length : $"{Wrap(length)}*{quantity}");
        }

        if (definition.CalculationValue is not double value || !double.IsFinite(value) || value <= 0d)
        {
            return OperationResponse<string>.Fail($"PANEL_FRAME_PROFILE_VALUE_INVALID: {definition.Code}");
        }
        return definition.Calculation switch
        {
            PanelFrameProfileCalculation.FixedQuantity =>
                OperationResponse<string>.Ok(Format(value)),
            PanelFrameProfileCalculation.Spacing =>
                OperationResponse<string>.Ok($"{Wrap(length)}/{Format(value)}"),
            _ => OperationResponse<string>.Fail($"PANEL_FRAME_PROFILE_MODE_INVALID: {definition.Code}")
        };
    }

    private static string LengthExpression(double modifier)
    {
        if (Math.Abs(modifier) < 1e-9d)
        {
            return "LL";
        }
        string sign = modifier > 0d ? "+" : "-";
        return $"LL{sign}{Format(Math.Abs(modifier))}";
    }

    private static string Wrap(string expression) => expression == "LL" ? expression : $"({expression})";

    private static string Format(double value) => value.ToString("0.#####", CultureInfo.InvariantCulture);
}

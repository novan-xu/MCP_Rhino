using System.Windows.Media;

namespace PanelCladdingEditor.UI;

using PanelCladdingEditor.Domain.Models.PanelCladding;

public sealed class PanelCladdingMaterial
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required Color Color { get; init; }

    public SolidColorBrush Brush
    {
        get
        {
            var brush = new SolidColorBrush(Color);
            brush.Freeze();
            return brush;
        }
    }

    public string DisplayName => $"{Code} · {Name}";

    public PanelCladdingMaterial Copy() => new()
    {
        Code = Code,
        Name = Name,
        Category = Category,
        Color = Color
    };

    public PanelCladdingMaterialCatalogItem ToCatalogItem() => new()
    {
        Code = Code,
        Description = Name,
        Category = Category,
        ColorHex = PanelCladdingMaterialPalette.ToHex(Color)
    };

    public static PanelCladdingMaterial FromCatalogItem(PanelCladdingMaterialCatalogItem item) => new()
    {
        Code = item.Code.Trim().ToUpperInvariant(),
        Name = string.IsNullOrWhiteSpace(item.Description) ? "Panel finish" : item.Description.Trim(),
        Category = string.IsNullOrWhiteSpace(item.Category) ? "Composite" : item.Category.Trim(),
        Color = PanelCladdingMaterialPalette.ParseColor(item.ColorHex)
    };
}

public static class PanelCladdingMaterialPalette
{
    private static readonly IReadOnlyDictionary<string, string[]> Palettes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Metal"] = ["#91857A", "#756D66", "#A79B90", "#5E6265", "#B0A69D"],
            ["Glass"] = ["#91D0D8", "#70B8C3", "#AEDFE4", "#5D9FAA", "#B9E7EB"],
            ["Stone"] = ["#C4B9A9", "#AFA292", "#D4CCBF", "#978B7D", "#E0D9CF"],
            ["Terracotta"] = ["#C87247", "#A95C38", "#D88A62", "#934A2D", "#E0A080"],
            ["Composite"] = ["#7E8994", "#68747F", "#98A2AB", "#57616B", "#ACB4BB"]
        };

    public static IReadOnlyList<string> Categories => Palettes.Keys.ToArray();

    public static IReadOnlyList<Color> ColorsFor(string category) =>
        Palettes.TryGetValue(category, out string[]? colors)
            ? colors.Select(ParseColor).ToArray()
            : Palettes["Composite"].Select(ParseColor).ToArray();

    public static PanelCladdingMaterial FromCode(string code, int variation = 0)
    {
        string normalized = code.Trim().ToUpperInvariant();
        string category = CategoryFor(normalized);
        IReadOnlyList<Color> colors = ColorsFor(category);
        Color color = colors[Math.Max(0, variation) % colors.Count];
        return new PanelCladdingMaterial
        {
            Code = normalized,
            Name = DescriptionFor(category),
            Category = category,
            Color = color
        };
    }

    public static Color ParseColor(string value) =>
        (Color)ColorConverter.ConvertFromString(value)!;

    public static string ToHex(Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string CategoryFor(string code)
    {
        if (code.StartsWith("GL", StringComparison.OrdinalIgnoreCase))
        {
            return "Glass";
        }
        if (code.StartsWith("ST", StringComparison.OrdinalIgnoreCase))
        {
            return "Stone";
        }
        if (code.StartsWith("TER", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("BR", StringComparison.OrdinalIgnoreCase))
        {
            return "Terracotta";
        }
        if (code.StartsWith("MET", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("AL", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("SS", StringComparison.OrdinalIgnoreCase))
        {
            return "Metal";
        }
        return "Composite";
    }

    private static string DescriptionFor(string category) => category switch
    {
        "Glass" => "Glazing panel",
        "Stone" => "Stone finish",
        "Terracotta" => "Terracotta finish",
        "Metal" => "Metal panel",
        _ => "Panel finish"
    };
}

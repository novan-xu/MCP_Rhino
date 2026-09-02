using System.Windows.Media;
using System.Windows.Media.Imaging;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.UI;

public sealed class PanelFrameExtrusion
{
    public string Code { get; init; } = string.Empty;
    public string BaseCode { get; init; } = string.Empty;
    public string SourceCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public PanelFrameProfileDimension? Dimension { get; init; }
    public PanelFrameProfileCalculation Calculation { get; init; } = PanelFrameProfileCalculation.Length;
    public double? CalculationValue { get; init; }
    public string ParentCode { get; init; } = string.Empty;
    public string SourcePdfPath { get; init; } = string.Empty;
    public int SourcePageNumber { get; init; }
    public byte[] ThumbnailPng { get; init; } = Array.Empty<byte>();
    public ImageSource? Thumbnail { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(Description)
        ? (IsConfigured ? Code : BaseCode)
        : $"{(IsConfigured ? Code : BaseCode)} - {Description}";
    public bool IsConfigured => Dimension is not null;
    public string PrimaryCode => IsConfigured ? Code : BaseCode;
    public string DimensionLabel => Dimension switch
    {
        PanelFrameProfileDimension.OneDimensional => "1D",
        PanelFrameProfileDimension.ZeroDimensional => "0D",
        _ => "Unconfigured"
    };
    public string ConfigurationSummary => Dimension switch
    {
        PanelFrameProfileDimension.OneDimensional =>
            CalculationValue is > 1d ? $"LL × {CalculationValue:0}" : "LL",
        PanelFrameProfileDimension.ZeroDimensional when Calculation == PanelFrameProfileCalculation.FixedQuantity =>
            $"Fixed {CalculationValue:0.#####}",
        PanelFrameProfileDimension.ZeroDimensional when Calculation == PanelFrameProfileCalculation.Spacing =>
            $"Every {CalculationValue:0.#####} in",
        _ => "Choose 1D or 0D"
    };

    public PanelFrameExtrusion Copy() => FromCatalogItem(ToCatalogItem());

    public PanelFrameExtrusionCatalogItem ToCatalogItem() => new()
    {
        Code = Code,
        BaseCode = BaseCode,
        SourceCode = SourceCode,
        Description = Description,
        Category = Category,
        Dimension = Dimension,
        Calculation = Calculation,
        CalculationValue = CalculationValue,
        ParentCode = ParentCode,
        SourcePdfPath = SourcePdfPath,
        SourcePageNumber = SourcePageNumber,
        ThumbnailPng = ThumbnailPng.ToArray()
    };

    public static PanelFrameExtrusion FromCatalogItem(PanelFrameExtrusionCatalogItem item) => new()
    {
        Code = item.Code.Trim().ToUpperInvariant(),
        BaseCode = item.BaseCode.Trim().ToUpperInvariant(),
        SourceCode = item.SourceCode.Trim().ToUpperInvariant(),
        Description = item.Description.Trim(),
        Category = item.Category.Trim(),
        Dimension = item.Dimension,
        Calculation = item.Calculation,
        CalculationValue = item.CalculationValue,
        ParentCode = item.ParentCode.Trim().ToUpperInvariant(),
        SourcePdfPath = item.SourcePdfPath.Trim(),
        SourcePageNumber = item.SourcePageNumber,
        ThumbnailPng = item.ThumbnailPng.ToArray(),
        Thumbnail = LoadImage(item.ThumbnailPng)
    };

    public PanelFrameProfileDefinition ToDefinition() => new()
    {
        Code = Code,
        BaseCode = BaseCode,
        SourceCode = SourceCode,
        Category = Category,
        Dimension = Dimension ?? PanelFrameProfileDimension.OneDimensional,
        Calculation = Calculation,
        CalculationValue = CalculationValue,
        ParentCode = ParentCode
    };

    private static ImageSource? LoadImage(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }
        using var stream = new MemoryStream(bytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}

public sealed record PanelExtrusionAssignmentCard(
    string Code,
    string DisplayName,
    ImageSource? Thumbnail,
    string ModifierText,
    string ModifierToolTip);

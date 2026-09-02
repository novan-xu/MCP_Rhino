using System.Text.RegularExpressions;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Rendering.Skia;

namespace PanelCladdingEditor.Infrastructure.PanelCladding;

public sealed partial class PdfFrameExtrusionScheduleImporter : IPanelFrameExtrusionScheduleImporter
{
    private const float TargetRenderedPageWidth = 1500f;
    private const int MaximumThumbnailBytes = 24_000;

    public OperationResponse<PanelFrameExtrusionScheduleImportResult> Import(
        PanelFrameExtrusionScheduleImportRequest request)
    {
        string rawPath = request.PdfPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return OperationResponse<PanelFrameExtrusionScheduleImportResult>.Fail(
                "PANEL_FRAME_EXTRUSION_PDF_REQUIRED");
        }

        string path;
        try
        {
            path = Path.GetFullPath(rawPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return OperationResponse<PanelFrameExtrusionScheduleImportResult>.Fail(
                $"PANEL_FRAME_EXTRUSION_PDF_PATH_INVALID: {exception.Message}");
        }
        if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResponse<PanelFrameExtrusionScheduleImportResult>.Fail(
                "PANEL_FRAME_EXTRUSION_PDF_NOT_FOUND");
        }

        try
        {
            using PdfDocument document = PdfDocument.Open(path, SkiaRenderingParsingOptions.Instance);
            document.AddSkiaPageFactory();
            var pages = new List<SchedulePage>();
            for (int pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
            {
                Page page = document.GetPage(pageNumber);
                Word[] words = page.GetWords().ToArray();
                HeaderCandidate[] candidates = FindHeaderCandidates(page, words);
                if (candidates.Length == 0)
                {
                    continue;
                }
                string pageText = page.Text ?? string.Empty;
                pages.Add(new SchedulePage(
                    pageNumber,
                    page.Width,
                    page.Height,
                    ExtractCategory(pageText, pageNumber),
                    BuildEntries(page, words, candidates)));
            }

            SchedulePage[] selectedPages = pages.ToArray();
            if (selectedPages.Length == 0)
            {
                return OperationResponse<PanelFrameExtrusionScheduleImportResult>.Fail(
                    "PANEL_FRAME_EXTRUSION_CODES_NOT_FOUND: no ALU die numbers were found in the PDF.");
            }

            var items = new Dictionary<string, PanelFrameExtrusionCatalogItem>(StringComparer.OrdinalIgnoreCase);
            foreach (SchedulePage page in selectedPages)
            {
                float scale = Math.Clamp(TargetRenderedPageWidth / (float)page.Width, 0.2f, 0.8f);
                using SKBitmap bitmap = document.GetPageAsSKBitmap(page.PageNumber, scale, SKColors.White);
                foreach (ScheduleEntry entry in page.Entries)
                {
                    if (items.ContainsKey(entry.SourceCode))
                    {
                        continue;
                    }
                    OperationResponse<byte[]> thumbnail = CropThumbnail(bitmap, page, entry);
                    if (!thumbnail.Success || thumbnail.Data is null)
                    {
                        return OperationResponse<PanelFrameExtrusionScheduleImportResult>.Fail(
                            $"{thumbnail.Message}: {entry.SourceCode} on page {page.PageNumber}.");
                    }
                    items[entry.SourceCode] = new PanelFrameExtrusionCatalogItem
                    {
                        Code = string.Empty,
                        BaseCode = ShortenSourceCode(entry.SourceCode),
                        SourceCode = entry.SourceCode,
                        Description = entry.Description,
                        Category = page.Category,
                        SourcePdfPath = path,
                        SourcePageNumber = page.PageNumber,
                        ThumbnailPng = thumbnail.Data
                    };
                }
            }

            PanelFrameExtrusionCatalogItem[] ordered = items.Values
                .OrderBy(item => item.SourcePageNumber)
                .ThenBy(item => item.BaseCode, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return ordered.Length == 0
                ? OperationResponse<PanelFrameExtrusionScheduleImportResult>.Fail(
                    "PANEL_FRAME_EXTRUSION_CODES_NOT_FOUND")
                : OperationResponse<PanelFrameExtrusionScheduleImportResult>.Ok(
                    new PanelFrameExtrusionScheduleImportResult
                    {
                        PdfPath = path,
                        Extrusions = ordered,
                        ImportedPageNumbers = selectedPages.Select(page => page.PageNumber).ToArray(),
                        UsedFramingPageFilter = false
                    },
                    $"Imported {ordered.Length} profiles from all {selectedPages.Length} schedule page(s).");
        }
        catch (Exception exception)
        {
            return OperationResponse<PanelFrameExtrusionScheduleImportResult>.Fail(
                $"PANEL_FRAME_EXTRUSION_PDF_IMPORT_FAILED: {exception.Message}");
        }
    }

    private static HeaderCandidate[] FindHeaderCandidates(Page page, IReadOnlyList<Word> words)
    {
        var result = new List<HeaderCandidate>();
        double lineTolerance = Math.Max(8d, page.Height * 0.005d);
        foreach (Word word in words)
        {
            Match match = SourceCodeRegex().Match(word.Text ?? string.Empty);
            if (!match.Success)
            {
                continue;
            }
            bool containsNumber = word.Text?.Contains("NUMBER", StringComparison.OrdinalIgnoreCase) == true;
            bool hasNearbyNumber = words.Any(other =>
                !ReferenceEquals(other, word) &&
                other.Text?.Contains("NUMBER", StringComparison.OrdinalIgnoreCase) == true &&
                Math.Abs(CenterY(other) - CenterY(word)) <= lineTolerance &&
                other.BoundingBox.Right <= word.BoundingBox.Right &&
                word.BoundingBox.Left - other.BoundingBox.Right <= page.Width * 0.08d);
            if (!containsNumber && !hasNearbyNumber)
            {
                continue;
            }
            result.Add(new HeaderCandidate(
                match.Value.ToUpperInvariant(),
                CenterX(word),
                CenterY(word)));
        }
        return result
            .GroupBy(item => item.SourceCode, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group
                .OrderByDescending(item => item.Y)
                .Take(1))
            .OrderByDescending(item => item.Y)
            .ThenBy(item => item.X)
            .ToArray();
    }

    private static IReadOnlyList<ScheduleEntry> BuildEntries(
        Page page,
        IReadOnlyList<Word> words,
        IReadOnlyList<HeaderCandidate> candidates)
    {
        double rowTolerance = Math.Max(12d, page.Height * 0.008d);
        var rows = new List<List<HeaderCandidate>>();
        foreach (HeaderCandidate candidate in candidates.OrderByDescending(item => item.Y).ThenBy(item => item.X))
        {
            List<HeaderCandidate>? row = rows.FirstOrDefault(items =>
                Math.Abs(items.Average(item => item.Y) - candidate.Y) <= rowTolerance);
            if (row is null)
            {
                row = [];
                rows.Add(row);
            }
            row.Add(candidate);
        }
        rows = rows.OrderByDescending(row => row.Average(item => item.Y)).ToList();
        double[] rowCenters = rows.Select(row => row.Average(item => item.Y)).ToArray();
        double rowHeight = MedianPositiveDifference(rowCenters) ?? page.Height * 0.19d;
        rowHeight = Math.Clamp(rowHeight, page.Height * 0.12d, page.Height * 0.28d);

        var result = new List<ScheduleEntry>();
        foreach (List<HeaderCandidate> row in rows)
        {
            HeaderCandidate[] ordered = row.OrderBy(item => item.X).ToArray();
            double[] centers = ordered.Select(item => item.X).ToArray();
            double cellWidth = MedianPositiveDifference(centers) ?? page.Width * 0.19d;
            cellWidth = Math.Clamp(cellWidth, page.Width * 0.12d, page.Width * 0.28d);
            foreach (HeaderCandidate candidate in ordered)
            {
                double cellLeft = Math.Clamp(candidate.X - cellWidth * 0.69d, 0d, page.Width - cellWidth);
                double cellTop = Math.Min(page.Height, candidate.Y + rowHeight * 0.05d);
                double cellBottom = Math.Max(0d, cellTop - rowHeight);
                string description = FindDescription(
                    words,
                    candidate,
                    cellLeft,
                    cellLeft + cellWidth,
                    rowTolerance);
                result.Add(new ScheduleEntry(
                    candidate.SourceCode,
                    description,
                    cellLeft,
                    cellLeft + cellWidth,
                    cellBottom,
                    cellTop));
            }
        }
        return result;
    }

    private static string FindDescription(
        IReadOnlyList<Word> words,
        HeaderCandidate candidate,
        double cellLeft,
        double cellRight,
        double lineTolerance)
    {
        string line = string.Join(' ', words
            .Where(word =>
                CenterX(word) >= cellLeft &&
                CenterX(word) <= cellRight &&
                Math.Abs(CenterY(word) - candidate.Y) <= lineTolerance)
            .OrderBy(word => word.BoundingBox.Left)
            .Select(word => word.Text));
        Match match = PartNameRegex().Match(line);
        return match.Success
            ? Regex.Replace(match.Groups["name"].Value.Trim(), @"\s+", " ")
            : string.Empty;
    }

    private static OperationResponse<byte[]> CropThumbnail(
        SKBitmap pageBitmap,
        SchedulePage page,
        ScheduleEntry entry)
    {
        double cellWidth = entry.Right - entry.Left;
        double cellHeight = entry.Top - entry.Bottom;
        double cropLeft = entry.Left + cellWidth * 0.04d;
        double cropRight = entry.Right - cellWidth * 0.06d;
        double cropTop = entry.Top - cellHeight * 0.17d;
        double cropBottom = entry.Bottom + cellHeight * 0.17d;
        int left = (int)Math.Floor(cropLeft / page.Width * pageBitmap.Width);
        int right = (int)Math.Ceiling(cropRight / page.Width * pageBitmap.Width);
        int top = (int)Math.Floor((page.Height - cropTop) / page.Height * pageBitmap.Height);
        int bottom = (int)Math.Ceiling((page.Height - cropBottom) / page.Height * pageBitmap.Height);
        var rectangle = new SKRectI(
            Math.Clamp(left, 0, pageBitmap.Width - 1),
            Math.Clamp(top, 0, pageBitmap.Height - 1),
            Math.Clamp(right, 1, pageBitmap.Width),
            Math.Clamp(bottom, 1, pageBitmap.Height));
        if (rectangle.Width < 20 || rectangle.Height < 20)
        {
            return OperationResponse<byte[]>.Fail("PANEL_FRAME_EXTRUSION_THUMBNAIL_BOUNDS_INVALID");
        }
        using var subset = new SKBitmap();
        if (!pageBitmap.ExtractSubset(subset, rectangle))
        {
            return OperationResponse<byte[]>.Fail("PANEL_FRAME_EXTRUSION_THUMBNAIL_CROP_FAILED");
        }
        SKRectI inkBounds = FindInkBounds(subset);
        using var presentation = new SKBitmap();
        SKBitmap thumbnail = subset;
        if (inkBounds.Width >= 20 && inkBounds.Height >= 20 && subset.ExtractSubset(presentation, inkBounds))
        {
            thumbnail = presentation;
        }
        using SKImage image = SKImage.FromBitmap(thumbnail);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        byte[] bytes = data.ToArray();
        if (bytes.Length == 0 || bytes.Length > MaximumThumbnailBytes)
        {
            return OperationResponse<byte[]>.Fail(
                $"PANEL_FRAME_EXTRUSION_THUMBNAIL_SIZE_INVALID ({bytes.Length} bytes)");
        }
        return OperationResponse<byte[]>.Ok(bytes);
    }

    private static SKRectI FindInkBounds(SKBitmap bitmap)
    {
        int minimumX = bitmap.Width;
        int minimumY = bitmap.Height;
        int maximumX = -1;
        int maximumY = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor color = bitmap.GetPixel(x, y);
                if (color.Red > 235 && color.Green > 235 && color.Blue > 235)
                {
                    continue;
                }
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
            }
        }
        if (maximumX < minimumX || maximumY < minimumY)
        {
            return new SKRectI(0, 0, bitmap.Width, bitmap.Height);
        }
        int margin = Math.Max(5, Math.Min(bitmap.Width, bitmap.Height) / 24);
        return new SKRectI(
            Math.Max(0, minimumX - margin),
            Math.Max(0, minimumY - margin),
            Math.Min(bitmap.Width, maximumX + margin + 1),
            Math.Min(bitmap.Height, maximumY + margin + 1));
    }

    private static double? MedianPositiveDifference(IReadOnlyList<double> values)
    {
        double[] differences = values
            .Zip(values.Skip(1), (first, second) => Math.Abs(first - second))
            .Where(value => value > 1e-6d)
            .OrderBy(value => value)
            .ToArray();
        return differences.Length == 0 ? null : differences[differences.Length / 2];
    }

    private static double CenterX(Word word) =>
        (word.BoundingBox.Left + word.BoundingBox.Right) / 2d;

    private static double CenterY(Word word) =>
        (word.BoundingBox.Bottom + word.BoundingBox.Top) / 2d;

    private static string ShortenSourceCode(string sourceCode)
    {
        string value = sourceCode.Trim().ToUpperInvariant();
        return value.StartsWith("ALU-", StringComparison.Ordinal) ? value[4..] : value;
    }

    private static string ExtractCategory(string pageText, int pageNumber)
    {
        string text = Regex.Replace(pageText ?? string.Empty, @"\s+", " ");
        if (text.Contains("ASSEMBLIES SCHEDULE", StringComparison.OrdinalIgnoreCase))
        {
            return "ASSEMBLIES";
        }
        if (text.Contains("HOLLOW (SMALL SIZE)", StringComparison.OrdinalIgnoreCase))
        {
            return "HOLLOW (SMALL SIZE)";
        }
        if (text.Contains("SOLID (SMALL SIZE)", StringComparison.OrdinalIgnoreCase))
        {
            return "SOLID (SMALL SIZE)";
        }
        foreach (string category in new[] { "FRAMING", "HOLLOW", "SOLID" })
        {
            if (text.Contains($"SCHEDULE- {category}", StringComparison.OrdinalIgnoreCase) ||
                text.Contains($"SCHEDULE - {category}", StringComparison.OrdinalIgnoreCase))
            {
                return category;
            }
        }
        return $"PAGE {pageNumber}";
    }

    [GeneratedRegex(@"ALU-[A-Z0-9][A-Z0-9-]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceCodeRegex();

    [GeneratedRegex(@"PART\s*NAME\s*:\s*(?<name>.*?)\s*ISLAND\s+DIE\s+NUMBER", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartNameRegex();

    private sealed record HeaderCandidate(string SourceCode, double X, double Y);

    private sealed record ScheduleEntry(
        string SourceCode,
        string Description,
        double Left,
        double Right,
        double Bottom,
        double Top);

    private sealed record SchedulePage(
        int PageNumber,
        double Width,
        double Height,
        string Category,
        IReadOnlyList<ScheduleEntry> Entries);
}

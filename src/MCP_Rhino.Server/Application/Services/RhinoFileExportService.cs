using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoFileExportService
{
    private const double DefaultImageDpi = 96d;
    private const double DefaultPdfDpi = 300d;
    private static readonly FileExportImageSize DefaultImageSize = new() { Width = 1920, Height = 1080 };
    private static readonly FileExportPageSize DefaultPdfPageSize = new() { WidthMm = 420d, HeightMm = 297d };

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILiveFileExporter _exporter;

    public RhinoFileExportService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILiveFileExporter exporter)
    {
        _documentAccessor = documentAccessor;
        _exporter = exporter;
    }

    public OperationResponse<FileExportResponse> ExportToDwg(ExportToDwgRequest request)
    {
        return Export(request.FilePath, CreateWriteFileSpec(request.OutputPath, FileExportFormat.Dwg, request.SelectedObjectIds, request.OverwriteExisting, request.FormatOptions));
    }

    public OperationResponse<FileExportResponse> ExportToDxf(ExportToDxfRequest request)
    {
        return Export(request.FilePath, CreateWriteFileSpec(request.OutputPath, FileExportFormat.Dxf, request.SelectedObjectIds, request.OverwriteExisting, request.FormatOptions));
    }

    public OperationResponse<FileExportResponse> ExportToIfc(ExportToIfcRequest request)
    {
        return Export(request.FilePath, CreateWriteFileSpec(request.OutputPath, FileExportFormat.Ifc, request.SelectedObjectIds, request.OverwriteExisting, request.FormatOptions));
    }

    public OperationResponse<FileExportResponse> ExportToStl(ExportToStlRequest request)
    {
        return Export(request.FilePath, CreateWriteFileSpec(request.OutputPath, FileExportFormat.Stl, request.SelectedObjectIds, request.OverwriteExisting, request.FormatOptions));
    }

    public OperationResponse<FileExportResponse> ExportToImage(ExportToImageRequest request)
    {
        OperationResponse<FileExportFormat> format = ResolveImageFormat(request.OutputPath);
        if (!format.Success)
        {
            return OperationResponse<FileExportResponse>.Fail(format.Message);
        }

        return Export(request.FilePath, new FileExportSpec
        {
            OutputPath = request.OutputPath ?? string.Empty,
            Format = format.Data,
            OverwriteExisting = request.OverwriteExisting,
            ViewName = NormalizeOptionalValue(request.ViewName),
            ImageSizePx = request.ImageSizePx is null
                ? new FileExportImageSize { Width = DefaultImageSize.Width, Height = DefaultImageSize.Height }
                : new FileExportImageSize
                {
                    Width = request.ImageSizePx.Width,
                    Height = request.ImageSizePx.Height
                },
            DotsPerInch = request.DotsPerInch ?? DefaultImageDpi,
            BackgroundTransparent = request.BackgroundTransparent
        });
    }

    public OperationResponse<FileExportResponse> ExportToPdf(ExportToPdfRequest request)
    {
        return Export(request.FilePath, new FileExportSpec
        {
            OutputPath = request.OutputPath ?? string.Empty,
            Format = FileExportFormat.Pdf,
            OverwriteExisting = request.OverwriteExisting,
            ViewName = NormalizeOptionalValue(request.ViewName),
            PageSizeMm = request.PageSizeMm is null
                ? new FileExportPageSize { WidthMm = DefaultPdfPageSize.WidthMm, HeightMm = DefaultPdfPageSize.HeightMm }
                : new FileExportPageSize
                {
                    WidthMm = request.PageSizeMm.WidthMm,
                    HeightMm = request.PageSizeMm.HeightMm
                },
            DotsPerInch = request.DotsPerInch ?? DefaultPdfDpi
        });
    }

    private OperationResponse<FileExportResponse> Export(string filePath, FileExportSpec spec)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResponse<FileExportResponse>.Fail("FilePath is required.");
        }

        OperationResponse<FileExportSpec> validation = ValidateSpec(spec);
        if (!validation.Success || validation.Data is null)
        {
            return OperationResponse<FileExportResponse>.Fail(validation.Message);
        }

        FileExportSpec validatedSpec = validation.Data;
        return _documentAccessor.Execute(filePath, document =>
        {
            if (PathsEqual(document.Path, validatedSpec.OutputPath))
            {
                return OperationResponse<FileExportResponse>.Fail("EXPORT_OUTPUT_OVERWRITE_BLOCKED");
            }

            OperationResponse<FileExportExecutionResult> exported = _exporter.Export(document, validatedSpec);
            if (!exported.Success || exported.Data is null)
            {
                return OperationResponse<FileExportResponse>.Fail(exported.Message);
            }

            var response = new FileExportResponse
            {
                FilePath = filePath,
                OutputPath = validatedSpec.OutputPath,
                Format = validatedSpec.Format,
                ExportedObjectCount = exported.Data.ExportedObjectCount,
                OutputFileSizeBytes = exported.Data.OutputFileSizeBytes,
                DurationMs = exported.Data.DurationMs,
                Warnings = exported.Data.Warnings
            };

            return OperationResponse<FileExportResponse>.Ok(response, "File export completed.");
        });
    }

    private static FileExportSpec CreateWriteFileSpec(
        string outputPath,
        FileExportFormat format,
        IEnumerable<Guid>? selectedObjectIds,
        bool overwriteExisting,
        IDictionary<string, string>? formatOptions)
    {
        return new FileExportSpec
        {
            OutputPath = outputPath ?? string.Empty,
            Format = format,
            OverwriteExisting = overwriteExisting,
            SelectedObjectIds = NormalizeObjectIds(selectedObjectIds),
            FormatOptions = NormalizeFormatOptions(formatOptions)
        };
    }

    private static OperationResponse<FileExportSpec> ValidateSpec(FileExportSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.OutputPath))
        {
            return OperationResponse<FileExportSpec>.Fail("OutputPath is required.");
        }

        if (!Path.IsPathFullyQualified(spec.OutputPath))
        {
            return OperationResponse<FileExportSpec>.Fail("OutputPath must be an absolute path.");
        }

        string normalizedOutputPath = Path.GetFullPath(spec.OutputPath);
        string? parentDirectory = Path.GetDirectoryName(normalizedOutputPath);
        if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
        {
            return OperationResponse<FileExportSpec>.Fail("EXPORT_OUTPUT_PARENT_NOT_FOUND");
        }

        if (File.Exists(normalizedOutputPath) && !spec.OverwriteExisting)
        {
            return OperationResponse<FileExportSpec>.Fail("EXPORT_OUTPUT_OVERWRITE_BLOCKED");
        }

        if (!IsExpectedExtension(normalizedOutputPath, spec.Format))
        {
            return OperationResponse<FileExportSpec>.Fail($"OutputPath extension does not match requested export format [{spec.Format}].");
        }

        if (spec.ImageSizePx is not null && (spec.ImageSizePx.Width <= 0 || spec.ImageSizePx.Height <= 0))
        {
            return OperationResponse<FileExportSpec>.Fail("ImageSizePx must have positive Width and Height.");
        }

        if (spec.PageSizeMm is not null && (spec.PageSizeMm.WidthMm <= 0d || spec.PageSizeMm.HeightMm <= 0d))
        {
            return OperationResponse<FileExportSpec>.Fail("PageSizeMm must have positive WidthMm and HeightMm.");
        }

        if (spec.DotsPerInch.HasValue && spec.DotsPerInch.Value <= 0d)
        {
            return OperationResponse<FileExportSpec>.Fail("DotsPerInch must be greater than zero.");
        }

        spec.OutputPath = normalizedOutputPath;
        spec.ViewName = NormalizeOptionalValue(spec.ViewName);
        spec.SelectedObjectIds = NormalizeObjectIds(spec.SelectedObjectIds);
        spec.FormatOptions = NormalizeFormatOptions(spec.FormatOptions);
        return OperationResponse<FileExportSpec>.Ok(spec);
    }

    private static OperationResponse<FileExportFormat> ResolveImageFormat(string outputPath)
    {
        string extension = Path.GetExtension(outputPath ?? string.Empty).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => OperationResponse<FileExportFormat>.Ok(FileExportFormat.Jpg),
            ".png" => OperationResponse<FileExportFormat>.Ok(FileExportFormat.Png),
            ".bmp" => OperationResponse<FileExportFormat>.Ok(FileExportFormat.Bmp),
            ".tif" or ".tiff" => OperationResponse<FileExportFormat>.Ok(FileExportFormat.Tiff),
            _ => OperationResponse<FileExportFormat>.Fail("ExportToImage supports only .jpg, .png, .bmp, .tif, or .tiff output paths.")
        };
    }

    private static IReadOnlyList<Guid> NormalizeObjectIds(IEnumerable<Guid>? objectIds)
    {
        return objectIds is null
            ? new List<Guid>()
            : objectIds
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static IReadOnlyDictionary<string, string> NormalizeFormatOptions(IEnumerable<KeyValuePair<string, string>>? formatOptions)
    {
        if (formatOptions is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<string, string> normalized = formatOptions
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        return normalized;
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsExpectedExtension(string outputPath, FileExportFormat format)
    {
        string extension = Path.GetExtension(outputPath).ToLowerInvariant();
        return format switch
        {
            FileExportFormat.Dwg => extension == ".dwg",
            FileExportFormat.Dxf => extension == ".dxf",
            FileExportFormat.Ifc => extension == ".ifc",
            FileExportFormat.Stl => extension == ".stl",
            FileExportFormat.Pdf => extension == ".pdf",
            FileExportFormat.Jpg => extension is ".jpg" or ".jpeg",
            FileExportFormat.Png => extension == ".png",
            FileExportFormat.Bmp => extension == ".bmp",
            FileExportFormat.Tiff => extension is ".tif" or ".tiff",
            _ => false
        };
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}

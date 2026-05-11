using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class ProceduralTextureImageService
{
    public OperationResponse<ProceduralTextureImageResponse> Generate(ProceduralTextureImageSpec spec)
    {
        OperationResponse validation = Validate(spec);
        if (!validation.Success)
        {
            return OperationResponse<ProceduralTextureImageResponse>.Fail(validation.Message);
        }

        if (!OperatingSystem.IsWindows())
        {
            return OperationResponse<ProceduralTextureImageResponse>.Fail(
                "Procedural texture image generation currently requires Windows because it uses System.Drawing.");
        }

        string outputPath = Path.GetFullPath(spec.OutputPath);
        bool existed = File.Exists(outputPath);
        if (existed && !spec.Overwrite)
        {
            return OperationResponse<ProceduralTextureImageResponse>.Fail(
                $"OutputPath already exists and Overwrite is false: {outputPath}");
        }

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        WritePng(outputPath, spec);
        var fileInfo = new FileInfo(outputPath);
        return OperationResponse<ProceduralTextureImageResponse>.Ok(
            new ProceduralTextureImageResponse
            {
                OutputPath = outputPath,
                WidthPx = spec.WidthPx,
                HeightPx = spec.HeightPx,
                PatternKind = spec.PatternKind,
                OverwroteExisting = existed,
                BytesWritten = fileInfo.Length,
                Message = "Procedural texture image generated."
            },
            "Procedural texture image generated.");
    }

    [SupportedOSPlatform("windows")]
    private static void WritePng(string outputPath, ProceduralTextureImageSpec spec)
    {
        using var bitmap = new Bitmap(spec.WidthPx, spec.HeightPx);
        for (int y = 0; y < spec.HeightPx; y++)
        {
            for (int x = 0; x < spec.WidthPx; x++)
            {
                bitmap.SetPixel(x, y, RenderPixel(spec, x, y));
            }
        }

        bitmap.Save(outputPath, ImageFormat.Png);
    }

    private static OperationResponse Validate(ProceduralTextureImageSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.OutputPath))
        {
            return OperationResponse.Fail("OutputPath is required.");
        }

        if (spec.WidthPx is < 16 or > 2048 || spec.HeightPx is < 16 or > 2048)
        {
            return OperationResponse.Fail("WidthPx and HeightPx must be between 16 and 2048.");
        }

        if (spec.ThreadSpacingPx is < 2 or > 512)
        {
            return OperationResponse.Fail("ThreadSpacingPx must be between 2 and 512.");
        }

        if (spec.ThreadThicknessPx < 1 || spec.ThreadThicknessPx > spec.ThreadSpacingPx)
        {
            return OperationResponse.Fail("ThreadThicknessPx must be at least 1 and no greater than ThreadSpacingPx.");
        }

        if (double.IsNaN(spec.NoiseAmount) || double.IsInfinity(spec.NoiseAmount) || spec.NoiseAmount is < 0d or > 1d)
        {
            return OperationResponse.Fail("NoiseAmount must be between 0 and 1.");
        }

        return OperationResponse.Ok();
    }

    private static Color RenderPixel(ProceduralTextureImageSpec spec, int x, int y)
    {
        return spec.PatternKind switch
        {
            ProceduralTexturePatternKind.FineNoise => RenderFineNoise(spec, x, y),
            ProceduralTexturePatternKind.LinearGrain => RenderLinearGrain(spec, x, y),
            ProceduralTexturePatternKind.CheckerStripes => RenderCheckerStripes(spec, x, y),
            _ => RenderWovenFabric(spec, x, y)
        };
    }

    private static Color RenderWovenFabric(ProceduralTextureImageSpec spec, int x, int y)
    {
        int spacing = spec.ThreadSpacingPx;
        int thickness = spec.ThreadThicknessPx;
        bool warp = x % spacing < thickness;
        bool weft = y % spacing < thickness;
        bool warpOver = ((x / spacing) + (y / spacing)) % 2 == 0;

        Color color = ToColor(spec.BaseColor);
        if (warp)
        {
            color = Blend(color, ToColor(spec.WarpColor), warpOver ? 0.72d : 0.42d);
        }

        if (weft)
        {
            color = Blend(color, ToColor(spec.WeftColor), warpOver ? 0.42d : 0.72d);
        }

        double ribShade = (Math.Sin((x / (double)spacing) * Math.PI) + Math.Cos((y / (double)spacing) * Math.PI)) * 0.035d;
        return AddNoise(Adjust(color, ribShade), spec, x, y);
    }

    private static Color RenderFineNoise(ProceduralTextureImageSpec spec, int x, int y)
    {
        return AddNoise(ToColor(spec.BaseColor), spec, x, y);
    }

    private static Color RenderLinearGrain(ProceduralTextureImageSpec spec, int x, int y)
    {
        double stripe = Math.Sin((x + HashNoise(y, x, spec.Seed) * 12d) / Math.Max(1d, spec.ThreadSpacingPx) * Math.PI);
        Color grain = Blend(ToColor(spec.BaseColor), ToColor(spec.WarpColor), 0.35d + (stripe * 0.18d));
        return AddNoise(grain, spec, x, y);
    }

    private static Color RenderCheckerStripes(ProceduralTextureImageSpec spec, int x, int y)
    {
        int cell = Math.Max(2, spec.ThreadSpacingPx);
        bool even = ((x / cell) + (y / cell)) % 2 == 0;
        Color baseColor = even
            ? Blend(ToColor(spec.BaseColor), ToColor(spec.WarpColor), 0.55d)
            : Blend(ToColor(spec.BaseColor), ToColor(spec.WeftColor), 0.55d);
        return AddNoise(baseColor, spec, x, y);
    }

    private static Color AddNoise(Color color, ProceduralTextureImageSpec spec, int x, int y)
    {
        if (spec.NoiseAmount <= 0d)
        {
            return color;
        }

        double amount = (HashNoise(x, y, spec.Seed) - 0.5d) * spec.NoiseAmount;
        return Adjust(color, amount);
    }

    private static double HashNoise(int x, int y, int seed)
    {
        unchecked
        {
            int value = seed;
            value = (value * 397) ^ x;
            value = (value * 397) ^ y;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return (value & 0x7fffffff) / (double)int.MaxValue;
        }
    }

    private static Color Blend(Color left, Color right, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            BlendChannel(left.R, right.R, amount),
            BlendChannel(left.G, right.G, amount),
            BlendChannel(left.B, right.B, amount));
    }

    private static int BlendChannel(int left, int right, double amount)
    {
        return ClampToByte((int)Math.Round(left + ((right - left) * amount)));
    }

    private static Color Adjust(Color color, double amount)
    {
        return Color.FromArgb(
            ClampToByte((int)Math.Round(color.R + (255d * amount))),
            ClampToByte((int)Math.Round(color.G + (255d * amount))),
            ClampToByte((int)Math.Round(color.B + (255d * amount))));
    }

    private static int ClampToByte(int value)
    {
        return Math.Clamp(value, 0, 255);
    }

    private static Color ToColor(RhinoDisplayColor color)
    {
        return Color.FromArgb(
            ClampToByte(color.R),
            ClampToByte(color.G),
            ClampToByte(color.B));
    }
}

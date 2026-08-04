using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Materials;

[McpServerToolType]
public sealed class GenerateProceduralTextureImageTool
{
    private readonly ProceduralTextureImageService _service;

    public GenerateProceduralTextureImageTool(ProceduralTextureImageService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Generate a bounded deterministic PNG texture image such as woven fabric, fine noise, linear grain, or checker stripes at an explicit output path. This writes an external file and never scans directories or calls image-generation AI.")]
    public OperationResponse<ProceduralTextureImageResponse> GenerateProceduralTextureImage(
        GenerateProceduralTextureImageRequest request)
    {
        return _service.Generate(new ProceduralTextureImageSpec
        {
            PatternKind = request.PatternKind,
            OutputPath = request.OutputPath,
            WidthPx = request.WidthPx,
            HeightPx = request.HeightPx,
            BaseColor = ToDisplayColor(request.BaseColor),
            WarpColor = ToDisplayColor(request.WarpColor),
            WeftColor = ToDisplayColor(request.WeftColor),
            ThreadSpacingPx = request.ThreadSpacingPx,
            ThreadThicknessPx = request.ThreadThicknessPx,
            NoiseAmount = request.NoiseAmount,
            Tileable = request.Tileable,
            Overwrite = request.Overwrite,
            Seed = request.Seed
        });
    }

    private static RhinoDisplayColor ToDisplayColor(ObjectColorRequest color)
    {
        return new RhinoDisplayColor
        {
            R = color.R,
            G = color.G,
            B = color.B
        };
    }
}

using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Drawing;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Drawing;

[McpServerToolType]
public sealed class LotVisualizationExportTool
{
    private readonly LotVisualizationExportSkill _skill;

    public LotVisualizationExportTool(LotVisualizationExportSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview a live Rhino lot-visualization export: resolve visible geometry by lot-number user text, report deterministic lot colors/groups, and list four isometric PNG outputs. Does not mutate Rhino or write files.")]
    public OperationResponse<LotVisualizationPreviewResponse> PreviewLotVisualizationExport(
        string filePath,
        IReadOnlyList<Guid>? objectIds = null,
        IReadOnlyList<string>? lotNumberKeys = null,
        IReadOnlyList<string>? ineffectiveLotValues = null,
        string? outputDirectory = null,
        string? outputFilePrefix = null)
    {
        return _skill.Preview(new PreviewLotVisualizationExportRequest
        {
            FilePath = filePath,
            ObjectIds = objectIds ?? Array.Empty<Guid>(),
            LotNumberKeys = lotNumberKeys ?? Array.Empty<string>(),
            IneffectiveLotValues = ineffectiveLotValues ?? Array.Empty<string>(),
            OutputDirectory = outputDirectory,
            OutputFilePrefix = outputFilePrefix
        });
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Execute the previewed live Rhino lot-visualization workflow: group and distinctly color effective lots, keep ineffective lots white, create four fitted isometric named views, and export four PNGs at one Rhino Print scale. White is applied only as a temporary solid document render background and exact render background state is restored after success or failure; Rhino application appearance settings are never changed.")]
    public OperationResponse<LotVisualizationExportResponse> ExportLotVisualization(
        string filePath,
        IReadOnlyList<Guid>? objectIds = null,
        IReadOnlyList<string>? lotNumberKeys = null,
        IReadOnlyList<string>? ineffectiveLotValues = null,
        string? outputDirectory = null,
        string? outputFilePrefix = null,
        ImageSizePxRequest? imageSizePx = null,
        double? dotsPerInch = null,
        double? fitScaleMultiplier = null,
        double? marginMm = null,
        double? viewFitMarginPercent = null,
        bool overwriteExisting = false)
    {
        return _skill.Export(new ExportLotVisualizationRequest
        {
            FilePath = filePath,
            ObjectIds = objectIds ?? Array.Empty<Guid>(),
            LotNumberKeys = lotNumberKeys ?? Array.Empty<string>(),
            IneffectiveLotValues = ineffectiveLotValues ?? Array.Empty<string>(),
            OutputDirectory = outputDirectory,
            OutputFilePrefix = outputFilePrefix,
            ImageSizePx = imageSizePx,
            DotsPerInch = dotsPerInch,
            FitScaleMultiplier = fitScaleMultiplier,
            MarginMm = marginMm,
            ViewFitMarginPercent = viewFitMarginPercent,
            OverwriteExisting = overwriteExisting
        });
    }
}

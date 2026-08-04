using System.ComponentModel;
using MCP_Rhino.Server.Agents.Modeling;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Modeling;

[McpServerToolType]
public sealed class RunReferenceImageObjectModelingAgentTool
{
    private const string ImageBriefRequiredMessage =
        "Raw image inference is not available in this route. Provide structured BriefRequest or use an image-brief provider.";

    private readonly ReferenceImageObjectModelingAgent _agent;

    public RunReferenceImageObjectModelingAgentTool(ReferenceImageObjectModelingAgent agent)
    {
        _agent = agent;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Run the structured ReferenceImageObjectModelingAgent in the current live Rhino document. Use for full reference-image object modeling only when a structured briefRequest is supplied; raw image-only requests return IMAGE_BRIEF_REQUIRED because this tool does not inspect pixels. Material, texture, highlight, and shadow cues must be routed as material/render context, not geometry.")]
    public OperationResponse<ReferenceImageObjectModelingAgentResponse> RunReferenceImageObjectModelingAgent(
        string filePath,
        BuildReferenceImageModelBriefRequest? briefRequest = null,
        string targetLayerFullPath = "",
        double scale = 1d,
        bool createTargetLayer = true,
        bool runVisualQa = true,
        bool applyMaterials = true,
        bool executeSimpleRaisedStripDetails = true,
        int maxMassingCorrectionLoops = 1,
        int maxDetailMaterialCorrectionLoops = 1,
        ImageSizePxRequest? qaImageSizePx = null,
        List<string>? qaFindings = null)
    {
        BuildReferenceImageModelBriefRequest brief = briefRequest ?? new BuildReferenceImageModelBriefRequest();
        if (IsRawImageOnly(brief))
        {
            return OperationResponse<ReferenceImageObjectModelingAgentResponse>.Ok(
                BuildImageBriefRequiredResponse(filePath, brief, targetLayerFullPath),
                ImageBriefRequiredMessage);
        }

        return _agent.Run(new ReferenceImageObjectModelingAgentRequest
        {
            FilePath = filePath,
            BriefRequest = brief,
            TargetLayerFullPath = targetLayerFullPath,
            Scale = scale,
            CreateTargetLayer = createTargetLayer,
            RunVisualQa = runVisualQa,
            ApplyMaterials = applyMaterials,
            ExecuteSimpleRaisedStripDetails = executeSimpleRaisedStripDetails,
            MaxMassingCorrectionLoops = maxMassingCorrectionLoops,
            MaxDetailMaterialCorrectionLoops = maxDetailMaterialCorrectionLoops,
            QaImageSizePx = qaImageSizePx,
            QaFindings = qaFindings ?? new List<string>()
        });
    }

    private static bool IsRawImageOnly(BuildReferenceImageModelBriefRequest brief)
    {
        return !string.IsNullOrWhiteSpace(brief.ReferenceImagePath)
            && string.IsNullOrWhiteSpace(brief.ObjectType)
            && brief.Parts.Count == 0;
    }

    private static ReferenceImageObjectModelingAgentResponse BuildImageBriefRequiredResponse(
        string filePath,
        BuildReferenceImageModelBriefRequest brief,
        string targetLayerFullPath)
    {
        return new ReferenceImageObjectModelingAgentResponse
        {
            FilePath = filePath,
            TargetLayerFullPath = targetLayerFullPath,
            Status = ReferenceImageObjectModelingStatus.CapabilityGap,
            StatusReason = ImageBriefRequiredMessage,
            Plan = new ReferenceImageObjectModelingPlan
            {
                Brief = new ReferenceImageModelBrief
                {
                    ReferenceImagePath = brief.ReferenceImagePath,
                    ReferenceImageLabel = brief.ReferenceImageLabel
                }
            },
            CapabilityGaps = new List<ReferenceImageCapabilityGapResponse>
            {
                new()
                {
                    Layer = ReferenceImageCapabilityGapLayer.ExternalConnector,
                    Code = "IMAGE_BRIEF_REQUIRED",
                    Message = ImageBriefRequiredMessage
                }
            }
        };
    }
}

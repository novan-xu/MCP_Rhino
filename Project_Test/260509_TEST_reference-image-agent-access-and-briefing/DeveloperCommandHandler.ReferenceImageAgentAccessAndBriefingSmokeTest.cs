using System.ComponentModel;
using System.Reflection;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Tools.Modeling;
using MCP_Rhino.Server.Tools.Reference;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string ReferenceImageAgentAccessAndBriefingSlug = "reference-image-agent-access-and-briefing-smoke-test";

    partial void RegisterReferenceImageAgentAccessAndBriefingHandlers()
    {
        _extensionHandlers[ReferenceImageAgentAccessAndBriefingSlug] = HandleReferenceImageAgentAccessAndBriefingSmokeTest;
    }

    private bool HandleReferenceImageAgentAccessAndBriefingSmokeTest(string[] args)
    {
        try
        {
            RunReferenceImageAgentAccessAndBriefingSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Reference image agent access and briefing smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunReferenceImageAgentAccessAndBriefingSmoke()
    {
        MethodInfo runMethod = typeof(RunReferenceImageObjectModelingAgentTool)
            .GetMethod(nameof(RunReferenceImageObjectModelingAgentTool.RunReferenceImageObjectModelingAgent))
            ?? throw new InvalidOperationException("RunReferenceImageObjectModelingAgent method was not found.");
        RequireReferenceImageAccess(
            typeof(RunReferenceImageObjectModelingAgentTool).GetCustomAttribute<McpServerToolTypeAttribute>() is not null,
            "RunReferenceImageObjectModelingAgentTool must be an MCP tool type.");
        RequireReferenceImageAccess(
            runMethod.GetCustomAttribute<McpServerToolAttribute>() is { ReadOnly: false, Destructive: false, OpenWorld: false },
            "RunReferenceImageObjectModelingAgent must have explicit non-destructive live-document safety metadata.");
        RequireReferenceImageAccess(
            runMethod.GetCustomAttribute<DescriptionAttribute>()?.Description.Contains("structured briefRequest", StringComparison.OrdinalIgnoreCase) == true,
            "RunReferenceImageObjectModelingAgent description must mention the structured brief request boundary.");

        var schemaTool = new GetReferenceImageBriefSchemaTool();
        ReferenceImageBriefSchemaResponse schema = schemaTool.GetReferenceImageBriefSchema();
        RequireReferenceImageAccess(
            schema.Markdown.Contains("IMAGE_BRIEF_REQUIRED", StringComparison.Ordinal)
                && schema.JsonExample.Contains("\"objectType\"", StringComparison.Ordinal),
            "Reference image brief schema must describe the raw-image gap and include an objectType example.");

        var agentTool = new RunReferenceImageObjectModelingAgentTool(_referenceImageObjectModelingAgent);
        OperationResponse<ReferenceImageObjectModelingAgentResponse> imageOnly = agentTool.RunReferenceImageObjectModelingAgent(
            "C:/mcp-rhino/reference-image-agent-access-smoke.3dm",
            new BuildReferenceImageModelBriefRequest
            {
                ReferenceImagePath = "C:/reference/sofa.png",
                ReferenceImageLabel = "image-only-smoke"
            });
        RequireReferenceImageAccess(imageOnly.Success && imageOnly.Data is not null, "Image-only agent wrapper response should be a structured capability gap.");
        ReferenceImageObjectModelingAgentResponse imageOnlyData = imageOnly.Data
            ?? throw new InvalidOperationException("Image-only data unexpectedly null.");
        RequireReferenceImageAccess(
            imageOnlyData.Status == ReferenceImageObjectModelingStatus.CapabilityGap
                && imageOnlyData.CapabilityGaps.Any(gap => gap.Code == "IMAGE_BRIEF_REQUIRED"),
            "Image-only request must return IMAGE_BRIEF_REQUIRED.");

        OperationResponse<ReferenceImageObjectModelingAgentResponse> structured = agentTool.RunReferenceImageObjectModelingAgent(
            "C:/mcp-rhino/reference-image-agent-access-smoke.3dm",
            ReferenceImageAccessBrief(),
            targetLayerFullPath: "MCP::REFERENCE_IMAGE_AGENT_ACCESS_SMOKE",
            scale: 2d,
            runVisualQa: false,
            applyMaterials: false,
            executeSimpleRaisedStripDetails: false);
        RequireReferenceImageAccess(structured.Success && structured.Data is not null, "Structured agent wrapper response should be returned.");
        ReferenceImageObjectModelingAgentResponse structuredData = structured.Data
            ?? throw new InvalidOperationException("Structured data unexpectedly null.");
        RequireReferenceImageAccess(
            structuredData.Plan.Brief.ObjectType == "sofa"
                && structuredData.CapabilityGaps.Any(gap => gap.Message.Contains("LIVE_RHINO_REQUIRED", StringComparison.Ordinal)),
            "Structured request should delegate into the agent and stop at the CLI live-Rhino gap.");

        Console.WriteLine("[OK] reference-image agent wrapper is MCP-exposed and delegates structured requests.");
        Console.WriteLine("[OK] raw image-only requests return IMAGE_BRIEF_REQUIRED and the brief schema is discoverable.");
    }

    private static BuildReferenceImageModelBriefRequest ReferenceImageAccessBrief()
    {
        return new BuildReferenceImageModelBriefRequest
        {
            ReferenceImageLabel = "agent-access-sofa",
            ObjectType = "sofa",
            ObjectTypeConfidence = 0.9d,
            PrimaryTargetObject = "sofa",
            WidthRatio = 3,
            DepthRatio = 1.1,
            HeightRatio = 1,
            OverallEdgeCharacter = ReferenceImageObjectEdgeCharacter.Soft,
            Parts = new List<ReferenceImageBriefPartRequest>
            {
                new()
                {
                    Name = "seat cushion",
                    Role = ReferenceImagePartRole.PrimaryMass,
                    RelativeWidth = 3,
                    RelativeDepth = 1,
                    RelativeHeight = 0.35,
                    RelativeCenterZ = 0.4,
                    MaterialKey = "fabric"
                }
            },
            DetailCues = new List<ReferenceImageVisibleDetailCueRequest>
            {
                new()
                {
                    PartName = "seat cushion",
                    Kind = "seam",
                    Description = "front horizontal raised seam",
                    Role = ReferenceImagePartRole.AdditiveDetail
                }
            },
            MaterialCues = new List<ReferenceImageMaterialCueRequest>
            {
                new()
                {
                    Key = "fabric",
                    PartName = "seat cushion",
                    Description = "warm brown woven fabric",
                    BaseColor = new ObjectColorRequest { R = 142, G = 92, B = 73 },
                    Intent = ReferenceImageMaterialIntent.Fabric,
                    Roughness = 0.9,
                    TextureLikely = true
                }
            }
        };
    }

    private static void RequireReferenceImageAccess(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

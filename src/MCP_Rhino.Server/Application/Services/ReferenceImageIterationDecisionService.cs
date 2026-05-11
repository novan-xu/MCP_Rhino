using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class ReferenceImageIterationDecisionService
{
    public OperationResponse<ReferenceImageIterationDecisionResponse> Decide(
        DecideReferenceImageIterationRequest request)
    {
        if (request.MaxIterations < 1)
        {
            return OperationResponse<ReferenceImageIterationDecisionResponse>.Fail("MaxIterations must be at least 1.");
        }

        ReferenceImageModelingTrace trace = request.Trace;
        var reasons = new List<string>();

        if (trace.IterationIndex >= request.MaxIterations)
        {
            reasons.Add("Maximum iteration count reached.");
            return Ok(ReferenceImageIterationDecisionKind.StopAndReportGap, 0.9d, "stop", reasons, "MAX_ITERATIONS_REACHED");
        }

        if (request.VisualQa is not null && request.VisualQa.TargetObjectCount == 0)
        {
            reasons.Add("Visual QA found no target objects.");
            return Ok(ReferenceImageIterationDecisionKind.ReviseMassing, 0.95d, "massing", reasons);
        }

        if (trace.MassingObjectCount == 0)
        {
            reasons.Add("No massing objects are recorded in the modeling trace.");
            return Ok(ReferenceImageIterationDecisionKind.ReviseMassing, 0.95d, "massing", reasons);
        }

        string joinedFindings = string.Join(" ", request.QaFindings).ToLowerInvariant();
        if (ContainsAny(joinedFindings, "silhouette", "proportion", "scale", "massing", "wrong shape", "too wide", "too tall", "too deep"))
        {
            reasons.Add("QA findings indicate massing or proportion issues.");
            return Ok(ReferenceImageIterationDecisionKind.ReviseMassing, 0.85d, "massing", reasons);
        }

        if (ContainsAny(joinedFindings, "missing detail", "seam", "groove", "hole", "handle", "rim", "leg", "support", "label"))
        {
            reasons.Add("QA findings indicate missing or inaccurate detail.");
            return Ok(ReferenceImageIterationDecisionKind.ReviseDetail, 0.8d, "detail", reasons);
        }

        if (ContainsAny(joinedFindings, "material", "color", "texture", "roughness", "gloss", "fabric", "transparent"))
        {
            reasons.Add("QA findings indicate material mismatch.");
            return Ok(ReferenceImageIterationDecisionKind.ReviseMaterial, 0.78d, "material", reasons);
        }

        if (trace.MaterialAssignmentCount == 0)
        {
            reasons.Add("Massing exists but no material assignments are recorded.");
            return Ok(ReferenceImageIterationDecisionKind.ReviseMaterial, 0.7d, "material", reasons);
        }

        if (trace.KnownGaps.Count > 0)
        {
            reasons.Add("Known modeling gaps remain: " + string.Join("; ", trace.KnownGaps.Take(3)));
            return Ok(ReferenceImageIterationDecisionKind.StopAndReportGap, 0.7d, "stop", reasons, "KNOWN_GAPS_REMAIN");
        }

        reasons.Add("No blocking QA findings or trace gaps remain.");
        return Ok(ReferenceImageIterationDecisionKind.Accept, 0.75d, "accept", reasons);
    }

    private static OperationResponse<ReferenceImageIterationDecisionResponse> Ok(
        ReferenceImageIterationDecisionKind kind,
        double confidence,
        string nextStage,
        List<string> reasons,
        string stopReason = "")
    {
        return OperationResponse<ReferenceImageIterationDecisionResponse>.Ok(
            new ReferenceImageIterationDecisionResponse
            {
                Decision = new ReferenceImageIterationDecision
                {
                    Kind = kind,
                    Confidence = confidence,
                    NextStage = nextStage,
                    StopReason = stopReason,
                    Reasons = reasons
                }
            },
            "Reference image iteration decision completed.");
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        return tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}

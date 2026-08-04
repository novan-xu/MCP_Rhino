using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Domain.Rules;

namespace MCP_Rhino.Server.Application.Services;

public sealed class ReferenceImageDetailRefinementPlanningService
{
    public OperationResponse<ReferenceImageDetailRefinementPlanResponse> Plan(
        PlanReferenceImageDetailRefinementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Brief.ObjectType))
        {
            return OperationResponse<ReferenceImageDetailRefinementPlanResponse>.Fail("Brief.ObjectType is required.");
        }

        var plan = new ReferenceImageRefinementPlan
        {
            ReferenceImageLabel = request.Brief.ReferenceImageLabel,
            ObjectType = request.Brief.ObjectType
        };

        foreach (ReferenceImageVisibleDetailCue cue in request.Brief.DetailCues)
        {
            ReferenceImageRefinementAction action = CreateAction(cue);
            if (action.Kind == ReferenceImageRefinementActionKind.UseMaterialInstead)
            {
                plan.SuppressedTextureLikeDetails.Add(
                    $"{cue.PartName}: {cue.Description}");
            }
            else
            {
                plan.Actions.Add(action);
            }
        }

        foreach (ReferenceImagePrimitivePart part in request.Decomposition.Parts)
        {
            if (part.EdgeCharacter == ReferenceImageObjectEdgeCharacter.Soft
                && part.Role is ReferenceImagePartRole.PrimaryMass or ReferenceImagePartRole.StructuralMass
                && part.PreferredPrimitive != ReferenceImagePrimitiveVocabularyKind.RoundedBox
                && part.PreferredPrimitive != ReferenceImagePrimitiveVocabularyKind.Ellipsoid
                && part.PreferredPrimitive != ReferenceImagePrimitiveVocabularyKind.Capsule)
            {
                plan.Actions.Add(new ReferenceImageRefinementAction
                {
                    Kind = ReferenceImageRefinementActionKind.SoftenEdge,
                    PartName = part.PartName,
                    Description = "Preserve visibly soft edge character during refinement.",
                    SuggestedPrimitive = ReferenceImagePrimitiveVocabularyKind.RoundedBox,
                    ToolFamily = ReferenceImageToolFamily.GeometryEdit,
                    Priority = 0.55d,
                    Rationale = "The part is marked soft but its primitive is not inherently soft."
                });
            }
        }

        AddQaFindings(request.QaFindings, plan);
        if (request.VisualQa is not null && request.VisualQa.TargetObjectCount == 0)
        {
            plan.Warnings.Add("Visual QA reported zero target objects; detail refinement should wait until massing exists.");
        }

        return OperationResponse<ReferenceImageDetailRefinementPlanResponse>.Ok(
            new ReferenceImageDetailRefinementPlanResponse
            {
                Plan = plan,
                Warnings = plan.Warnings
                    .Select(message => new ObjectEditWarning { Code = "REFINEMENT_WARNING", Message = message })
                    .ToList()
            },
            "Reference image detail refinement plan completed.");
    }

    private static ReferenceImageRefinementAction CreateAction(ReferenceImageVisibleDetailCue cue)
    {
        string text = $"{cue.Kind} {cue.Description}".ToLowerInvariant();
        if (ReferenceImageVisualCueGeometryPolicy.IsNonGeometricVisualCue(cue))
        {
            return new ReferenceImageRefinementAction
            {
                Kind = ReferenceImageRefinementActionKind.UseMaterialInstead,
                PartName = cue.PartName,
                Description = cue.Description,
                SuggestedPrimitive = ReferenceImagePrimitiveVocabularyKind.MaterialOnly,
                ToolFamily = ReferenceImageToolFamily.Material,
                Priority = 0.7d,
                Rationale = $"{ReferenceImageVisualCueGeometryPolicy.NonGeometryRationale} Use material, texture mapping, or render context instead."
            };
        }

        if (cue.PreferredRepresentation != ReferenceImagePrimitiveVocabularyKind.Unknown)
        {
            return FromPreferredRepresentation(cue);
        }

        if (ContainsAny(text, "hole", "slot", "cut", "opening", "negative"))
        {
            return new ReferenceImageRefinementAction
            {
                Kind = ReferenceImageRefinementActionKind.AddCutout,
                PartName = cue.PartName,
                Description = cue.Description,
                SuggestedPrimitive = ReferenceImagePrimitiveVocabularyKind.BooleanCutter,
                ToolFamily = ReferenceImageToolFamily.Boolean,
                Priority = 0.85d,
                Rationale = "The cue describes subtractive geometry."
            };
        }

        if (ContainsAny(text, "seam", "rib", "trim", "border", "bezel", "raised", "strip"))
        {
            return new ReferenceImageRefinementAction
            {
                Kind = ReferenceImageRefinementActionKind.AddRaisedStrip,
                PartName = cue.PartName,
                Description = cue.Description,
                SuggestedPrimitive = ReferenceImagePrimitiveVocabularyKind.RaisedStrip,
                ToolFamily = ReferenceImageToolFamily.CurveOps,
                Priority = 0.8d,
                Rationale = "Raised linear detail improves recognizability without replacing the massing object."
            };
        }

        if (ContainsAny(text, "label", "logo", "decal", "graphic"))
        {
            return new ReferenceImageRefinementAction
            {
                Kind = ReferenceImageRefinementActionKind.AddDecalOrLabel,
                PartName = cue.PartName,
                Description = cue.Description,
                SuggestedPrimitive = ReferenceImagePrimitiveVocabularyKind.DecalPlane,
                ToolFamily = ReferenceImageToolFamily.Viewport,
                Priority = 0.5d,
                Rationale = "The cue is a surface graphic rather than shape mass."
            };
        }

        return new ReferenceImageRefinementAction
        {
            Kind = ReferenceImageRefinementActionKind.AddHandleOrSupport,
            PartName = cue.PartName,
            Description = cue.Description,
            SuggestedPrimitive = ReferenceImagePrimitiveVocabularyKind.Capsule,
            ToolFamily = ReferenceImageToolFamily.GeneralPrimitive,
            Priority = 0.55d,
            Rationale = "Generic protruding detail fallback."
        };
    }

    private static ReferenceImageRefinementAction FromPreferredRepresentation(ReferenceImageVisibleDetailCue cue)
    {
        return new ReferenceImageRefinementAction
        {
            Kind = cue.PreferredRepresentation switch
            {
                ReferenceImagePrimitiveVocabularyKind.BooleanCutter => ReferenceImageRefinementActionKind.AddCutout,
                ReferenceImagePrimitiveVocabularyKind.RaisedStrip => ReferenceImageRefinementActionKind.AddRaisedStrip,
                ReferenceImagePrimitiveVocabularyKind.DecalPlane => ReferenceImageRefinementActionKind.AddDecalOrLabel,
                ReferenceImagePrimitiveVocabularyKind.MaterialOnly => ReferenceImageRefinementActionKind.UseMaterialInstead,
                _ => ReferenceImageRefinementActionKind.AddHandleOrSupport
            },
            PartName = cue.PartName,
            Description = cue.Description,
            SuggestedPrimitive = cue.PreferredRepresentation,
            ToolFamily = cue.PreferredRepresentation switch
            {
                ReferenceImagePrimitiveVocabularyKind.BooleanCutter => ReferenceImageToolFamily.Boolean,
                ReferenceImagePrimitiveVocabularyKind.RaisedStrip => ReferenceImageToolFamily.CurveOps,
                ReferenceImagePrimitiveVocabularyKind.DecalPlane => ReferenceImageToolFamily.Viewport,
                ReferenceImagePrimitiveVocabularyKind.MaterialOnly => ReferenceImageToolFamily.Material,
                _ => ReferenceImageToolFamily.GeneralPrimitive
            },
            Priority = 0.75d,
            Rationale = "The structured cue supplied a preferred representation."
        };
    }

    private static void AddQaFindings(IEnumerable<string> findings, ReferenceImageRefinementPlan plan)
    {
        foreach (string finding in findings.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            string text = finding.ToLowerInvariant();
            if (ReferenceImageVisualCueGeometryPolicy.IsNonGeometricVisualCue(text))
            {
                plan.Warnings.Add($"QA finding treated as material or lighting context, not detail geometry: {finding.Trim()}");
                continue;
            }

            if (ContainsAny(text, "missing seam", "missing rib", "missing trim", "not enough detail"))
            {
                plan.Actions.Add(new ReferenceImageRefinementAction
                {
                    Kind = ReferenceImageRefinementActionKind.AddRaisedStrip,
                    Description = finding.Trim(),
                    SuggestedPrimitive = ReferenceImagePrimitiveVocabularyKind.RaisedStrip,
                    ToolFamily = ReferenceImageToolFamily.CurveOps,
                    Priority = 0.9d,
                    Rationale = "Visual QA feedback requested additional readable detail."
                });
            }
        }
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        return tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}

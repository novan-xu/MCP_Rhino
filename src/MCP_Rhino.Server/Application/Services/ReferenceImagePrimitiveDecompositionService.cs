using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class ReferenceImagePrimitiveDecompositionService
{
    public OperationResponse<ReferenceImagePrimitiveDecompositionResponse> Decompose(
        DecomposeReferenceImagePrimitivesRequest request)
    {
        ReferenceImageModelBrief brief = request.Brief;
        if (string.IsNullOrWhiteSpace(brief.ObjectType))
        {
            return OperationResponse<ReferenceImagePrimitiveDecompositionResponse>.Fail(
                "Brief.ObjectType is required for primitive decomposition.");
        }

        if (brief.Parts.Count == 0)
        {
            return OperationResponse<ReferenceImagePrimitiveDecompositionResponse>.Fail(
                "Brief.Parts must contain at least one part.");
        }

        double scale = request.Scale > 0d && !double.IsNaN(request.Scale) && !double.IsInfinity(request.Scale)
            ? request.Scale
            : 1d;
        var decomposition = new ReferenceImagePrimitiveDecomposition
        {
            ReferenceImageLabel = brief.ReferenceImageLabel,
            ObjectType = brief.ObjectType,
            Scale = scale
        };

        foreach (ReferenceImageBriefPart part in brief.Parts)
        {
            ReferenceImagePrimitiveVocabularyKind preferred = ResolvePreferredPrimitive(brief, part);
            decomposition.Parts.Add(new ReferenceImagePrimitivePart
            {
                PartName = part.Name,
                ParentPartName = part.ParentName,
                Role = part.Role,
                EdgeCharacter = ResolveEdgeCharacter(brief, part),
                PreferredPrimitive = preferred,
                FallbackPrimitive = ResolveFallbackPrimitive(preferred),
                RequiredToolFamily = ResolveToolFamily(preferred),
                CenterX = part.RelativeCenterX * scale,
                CenterY = part.RelativeCenterY * scale,
                CenterZ = part.RelativeCenterZ * scale,
                SizeX = Math.Max(part.RelativeWidth * scale, 0.01d),
                SizeY = Math.Max(part.RelativeDepth * scale, 0.01d),
                SizeZ = Math.Max(part.RelativeHeight * scale, 0.01d),
                Radius = ResolveRadius(part, scale),
                HasLocalFrame = part.HasLocalFrame,
                NormalX = part.NormalX,
                NormalY = part.NormalY,
                NormalZ = part.NormalZ,
                XAxisX = part.XAxisX,
                XAxisY = part.XAxisY,
                XAxisZ = part.XAxisZ,
                HasTaper = part.HasTaper,
                StartWidth = ResolveSectionSize(part.StartWidth, part.RelativeWidth, scale),
                StartDepth = ResolveSectionSize(part.StartDepth, part.RelativeDepth, scale),
                EndWidth = ResolveSectionSize(part.EndWidth, part.RelativeWidth, scale),
                EndDepth = ResolveSectionSize(part.EndDepth, part.RelativeDepth, scale),
                MaterialKey = string.IsNullOrWhiteSpace(part.MaterialKey) ? "default" : part.MaterialKey,
                Anchors = part.Anchors.Select(anchor => new ReferenceImageBriefAnchor
                {
                    Name = anchor.Name,
                    X = anchor.X * scale,
                    Y = anchor.Y * scale,
                    Z = anchor.Z * scale
                }).ToList(),
                ProfilePoints = part.ProfilePoints.Select(point => new ReferenceImageBriefProfilePoint
                {
                    X = point.X * scale,
                    Y = point.Y * scale,
                    Z = point.Z * scale
                }).ToList(),
                Candidates = BuildCandidates(brief, part, preferred),
                Notes = new List<string>(part.Notes)
            });
        }

        AddMissingToolWarnings(decomposition);
        return OperationResponse<ReferenceImagePrimitiveDecompositionResponse>.Ok(
            new ReferenceImagePrimitiveDecompositionResponse
            {
                Decomposition = decomposition,
                Warnings = decomposition.Warnings
                    .Select(message => new ObjectEditWarning { Code = "DECOMPOSITION_WARNING", Message = message })
                    .ToList()
            },
            "Reference image primitive decomposition completed.");
    }

    private static ReferenceImagePrimitiveVocabularyKind ResolvePreferredPrimitive(
        ReferenceImageModelBrief brief,
        ReferenceImageBriefPart part)
    {
        if (part.PreferredPrimitiveHint != ReferenceImagePrimitiveVocabularyKind.Unknown)
        {
            return part.PreferredPrimitiveHint;
        }

        string text = BuildSearchText(brief.ObjectType, part.Name, part.Notes);

        if (part.Role == ReferenceImagePartRole.MaterialOnly)
        {
            return ReferenceImagePrimitiveVocabularyKind.MaterialOnly;
        }

        if (part.HasTaper)
        {
            return ReferenceImagePrimitiveVocabularyKind.TaperedBox;
        }

        if (part.Role == ReferenceImagePartRole.SubtractiveDetail)
        {
            return ReferenceImagePrimitiveVocabularyKind.BooleanCutter;
        }

        if (part.Role is ReferenceImagePartRole.AdditiveDetail or ReferenceImagePartRole.SurfaceDetail)
        {
            if (ContainsAny(text, "label", "logo", "decal", "graphic"))
            {
                return ReferenceImagePrimitiveVocabularyKind.DecalPlane;
            }

            return ContainsAny(text, "seam", "rib", "trim", "border", "bezel", "strip")
                ? ReferenceImagePrimitiveVocabularyKind.RaisedStrip
                : ReferenceImagePrimitiveVocabularyKind.RoundedBox;
        }

        if (ContainsAny(text, "ring", "rim", "loop", "wheel", "gasket"))
        {
            return ReferenceImagePrimitiveVocabularyKind.Torus;
        }

        if (ContainsAny(text, "taper", "wedge", "sloped support", "angled support", "splayed leg", "a-frame"))
        {
            return ReferenceImagePrimitiveVocabularyKind.TaperedBox;
        }

        if (ContainsAny(text, "subd", "subdivision", "crowned cushion", "bulged cushion", "organic upholstery"))
        {
            return ReferenceImagePrimitiveVocabularyKind.SubD;
        }

        if (ContainsAny(text, "leg", "rod", "handle", "bar", "rail", "roll", "bolster"))
        {
            return ReferenceImagePrimitiveVocabularyKind.Capsule;
        }

        if (ContainsAny(text, "bottle", "can", "cup", "tube", "cylinder", "post"))
        {
            return ReferenceImagePrimitiveVocabularyKind.Cylinder;
        }

        if (ContainsAny(text, "cone", "taper", "spout", "tip"))
        {
            return ReferenceImagePrimitiveVocabularyKind.Cone;
        }

        if (ContainsAny(text, "sphere", "ball", "dome", "organic", "pillow"))
        {
            return ReferenceImagePrimitiveVocabularyKind.Ellipsoid;
        }

        ReferenceImageObjectEdgeCharacter edge = ResolveEdgeCharacter(brief, part);
        if (edge == ReferenceImageObjectEdgeCharacter.Soft)
        {
            return ReferenceImagePrimitiveVocabularyKind.RoundedBox;
        }

        return ReferenceImagePrimitiveVocabularyKind.RoundedBox;
    }

    private static ReferenceImageObjectEdgeCharacter ResolveEdgeCharacter(
        ReferenceImageModelBrief brief,
        ReferenceImageBriefPart part)
    {
        return part.EdgeCharacter == ReferenceImageObjectEdgeCharacter.Unknown
            ? brief.OverallEdgeCharacter
            : part.EdgeCharacter;
    }

    private static ReferenceImagePrimitiveVocabularyKind ResolveFallbackPrimitive(
        ReferenceImagePrimitiveVocabularyKind preferred)
    {
        return preferred switch
        {
            ReferenceImagePrimitiveVocabularyKind.Box => ReferenceImagePrimitiveVocabularyKind.RoundedBox,
            ReferenceImagePrimitiveVocabularyKind.Pipe => ReferenceImagePrimitiveVocabularyKind.Capsule,
            ReferenceImagePrimitiveVocabularyKind.Sphere => ReferenceImagePrimitiveVocabularyKind.Ellipsoid,
            ReferenceImagePrimitiveVocabularyKind.PlanarPanel => ReferenceImagePrimitiveVocabularyKind.RoundedBox,
            ReferenceImagePrimitiveVocabularyKind.ProfileExtrusion => ReferenceImagePrimitiveVocabularyKind.RoundedBox,
            ReferenceImagePrimitiveVocabularyKind.Sweep => ReferenceImagePrimitiveVocabularyKind.Capsule,
            ReferenceImagePrimitiveVocabularyKind.Loft => ReferenceImagePrimitiveVocabularyKind.Ellipsoid,
            ReferenceImagePrimitiveVocabularyKind.TaperedBox => ReferenceImagePrimitiveVocabularyKind.RoundedBox,
            ReferenceImagePrimitiveVocabularyKind.SubD => ReferenceImagePrimitiveVocabularyKind.RoundedBox,
            ReferenceImagePrimitiveVocabularyKind.BooleanCutter => ReferenceImagePrimitiveVocabularyKind.RoundedBox,
            ReferenceImagePrimitiveVocabularyKind.DecalPlane => ReferenceImagePrimitiveVocabularyKind.MaterialOnly,
            _ => preferred
        };
    }

    private static ReferenceImageToolFamily ResolveToolFamily(ReferenceImagePrimitiveVocabularyKind primitive)
    {
        return primitive switch
        {
            ReferenceImagePrimitiveVocabularyKind.RaisedStrip => ReferenceImageToolFamily.CurveOps,
            ReferenceImagePrimitiveVocabularyKind.BooleanCutter => ReferenceImageToolFamily.Boolean,
            ReferenceImagePrimitiveVocabularyKind.DecalPlane => ReferenceImageToolFamily.Viewport,
            ReferenceImagePrimitiveVocabularyKind.MaterialOnly => ReferenceImageToolFamily.Material,
            ReferenceImagePrimitiveVocabularyKind.SubD => ReferenceImageToolFamily.SubD,
            ReferenceImagePrimitiveVocabularyKind.TaperedBox => ReferenceImageToolFamily.GeneralPrimitive,
            ReferenceImagePrimitiveVocabularyKind.ProfileExtrusion
                or ReferenceImagePrimitiveVocabularyKind.Sweep
                or ReferenceImagePrimitiveVocabularyKind.Loft
                or ReferenceImagePrimitiveVocabularyKind.Pipe => ReferenceImageToolFamily.CurveOps,
            _ => ReferenceImageToolFamily.GeneralPrimitive
        };
    }

    private static List<ReferenceImagePrimitiveCandidate> BuildCandidates(
        ReferenceImageModelBrief brief,
        ReferenceImageBriefPart part,
        ReferenceImagePrimitiveVocabularyKind preferred)
    {
        var candidates = new List<ReferenceImagePrimitiveCandidate>
        {
            new()
            {
                Kind = preferred,
                Score = 0.9d,
                Reason = "Best fit from part role, edge character, proportions, and semantic cues."
            }
        };

        ReferenceImagePrimitiveVocabularyKind fallback = ResolveFallbackPrimitive(preferred);
        if (fallback != preferred)
        {
            candidates.Add(new ReferenceImagePrimitiveCandidate
            {
                Kind = fallback,
                Score = 0.65d,
                Reason = "Fallback when the preferred primitive or tool family is unavailable."
            });
        }

        ReferenceImageObjectEdgeCharacter edge = ResolveEdgeCharacter(brief, part);
        if (edge is ReferenceImageObjectEdgeCharacter.Soft or ReferenceImageObjectEdgeCharacter.Mixed
            && preferred != ReferenceImagePrimitiveVocabularyKind.RoundedBox)
        {
            candidates.Add(new ReferenceImagePrimitiveCandidate
            {
                Kind = ReferenceImagePrimitiveVocabularyKind.RoundedBox,
                Score = 0.55d,
                Reason = "General soft massing fallback."
            });
        }

        return candidates;
    }

    private static double ResolveRadius(ReferenceImageBriefPart part, double scale)
    {
        double smallest = Math.Min(part.RelativeWidth, Math.Min(part.RelativeDepth, part.RelativeHeight)) * scale;
        return Math.Max(smallest * 0.12d, 0.01d);
    }

    private static double ResolveSectionSize(double requestedSize, double fallbackRelativeSize, double scale)
    {
        double fallback = Math.Max(fallbackRelativeSize * scale, 0.01d);
        return requestedSize > 0d && !double.IsNaN(requestedSize) && !double.IsInfinity(requestedSize)
            ? requestedSize * scale
            : fallback;
    }

    private static void AddMissingToolWarnings(ReferenceImagePrimitiveDecomposition decomposition)
    {
        foreach (ReferenceImagePrimitivePart part in decomposition.Parts)
        {
            if (part.PreferredPrimitive is ReferenceImagePrimitiveVocabularyKind.BooleanCutter
                or ReferenceImagePrimitiveVocabularyKind.DecalPlane
                or ReferenceImagePrimitiveVocabularyKind.ProfileExtrusion
                or ReferenceImagePrimitiveVocabularyKind.Sweep
                or ReferenceImagePrimitiveVocabularyKind.Loft
                or ReferenceImagePrimitiveVocabularyKind.SubD)
            {
                decomposition.Warnings.Add(
                    $"{part.PartName} prefers {part.PreferredPrimitive}, which may require a later specialized tool slice.");
            }
        }
    }

    private static string BuildSearchText(string objectType, string partName, IEnumerable<string> notes)
    {
        return string.Join(" ", new[] { objectType, partName }.Concat(notes)).ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        return tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}

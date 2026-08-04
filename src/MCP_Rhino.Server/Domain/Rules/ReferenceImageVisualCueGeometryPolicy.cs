using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Domain.Rules;

public static class ReferenceImageVisualCueGeometryPolicy
{
    public const string NonGeometryRationale =
        "Material, texture, lighting, and shadow cues are not object geometry.";

    private static readonly string[] ShadowOrLightingTokens =
    {
        "shadow",
        "cast shadow",
        "contact shadow",
        "drop shadow",
        "ambient occlusion",
        "occlusion",
        "lighting",
        "studio light",
        "light falloff",
        "highlight",
        "specular",
        "reflection",
        "reflected light",
        "shading"
    };

    private static readonly string[] MaterialAppearanceTokens =
    {
        "texture",
        "woven",
        "weave",
        "warp",
        "weft",
        "fabric grain",
        "wood grain",
        "grain",
        "speckle",
        "color variation",
        "colour variation",
        "pattern",
        "roughness",
        "gloss",
        "sheen",
        "finish",
        "material",
        "bump",
        "normal map"
    };

    private static readonly string[] StrongPhysicalDetailTokens =
    {
        "button",
        "cutout",
        "foot",
        "groove",
        "handle",
        "hole",
        "knob",
        "leg",
        "opening",
        "piping",
        "rib",
        "rim",
        "ring",
        "seam",
        "slot",
        "stitch",
        "support",
        "tuft",
        "welt"
    };

    public static bool IsNonGeometricVisualCue(ReferenceImageVisibleDetailCue cue)
    {
        if (cue.Role == ReferenceImagePartRole.MaterialOnly
            || cue.PreferredRepresentation == ReferenceImagePrimitiveVocabularyKind.MaterialOnly)
        {
            return true;
        }

        return IsNonGeometricVisualCue($"{cue.Kind} {cue.Description}");
    }

    public static bool IsNonGeometricVisualCue(ReferenceImageRefinementAction action)
    {
        if (action.Kind == ReferenceImageRefinementActionKind.UseMaterialInstead
            || action.SuggestedPrimitive == ReferenceImagePrimitiveVocabularyKind.MaterialOnly
            || action.ToolFamily == ReferenceImageToolFamily.Material)
        {
            return true;
        }

        return IsNonGeometricVisualCue($"{action.PartName} {action.Description} {action.Rationale}");
    }

    public static bool IsNonGeometricVisualCue(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized = text.ToLowerInvariant();
        if (ContainsAny(normalized, ShadowOrLightingTokens))
        {
            return true;
        }

        if (!ContainsAny(normalized, MaterialAppearanceTokens))
        {
            return false;
        }

        return !ContainsAny(normalized, StrongPhysicalDetailTokens);
    }

    private static bool ContainsAny(string text, IEnumerable<string> tokens)
    {
        return tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}

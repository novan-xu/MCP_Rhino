using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Reference;

[McpServerToolType]
public sealed class GetReferenceImageBriefSchemaTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Get the structured BriefRequest schema required before running the reference-image object modeling agent. Reference-only; does not inspect images or mutate Rhino.")]
    public ReferenceImageBriefSchemaResponse GetReferenceImageBriefSchema()
    {
        return new ReferenceImageBriefSchemaResponse
        {
            RequiredConcepts = new[]
            {
                "objectType",
                "primaryTargetObject",
                "relative width/depth/height",
                "parts with roles, relative dimensions, edge character, and material keys",
                "detailCues separated into physical geometry, material-only cues, and reference-only shadows/lighting",
                "materialCues with base color, roughness, transparency, and textureLikely"
            },
            Markdown = """
            # Reference Image Brief Schema

            `RunReferenceImageObjectModelingAgent` does not inspect raw pixels. A vision-capable caller must convert the visible reference image into `briefRequest` first.

            Required modeling concepts:

            - `objectType`: plain object category, such as sofa, bottle, speaker, chair, appliance, fixture, or product.
            - `primaryTargetObject`: the object to model if the image contains multiple visible objects.
            - `widthRatio`, `depthRatio`, `heightRatio`: approximate proportions, not exact dimensions.
            - `parts`: major visible physical parts. Use `PrimaryMass` or `StructuralMass` for large volumes, `AdditiveDetail` for real protruding pieces, `SubtractiveDetail` for holes/cutouts, `SurfaceDetail` for seams/grooves, `MaterialOnly` for texture/material cues, and `Reference` for shadows/lighting/context.
            - `detailCues`: physical details only become geometry when they represent real object parts such as seams, rims, legs, handles, holes, supports, buttons, or grooves.
            - `materialCues`: fabric, grain, weave, color variation, roughness, gloss, and texture intent belong here.
            - Shadows, highlights, contact shadows, cast shadows, ambient occlusion, and studio lighting must be marked as `Reference` or omitted; they are not object geometry.

            If only `referenceImagePath` is supplied, the modeling agent returns `IMAGE_BRIEF_REQUIRED`.
            """,
            JsonExample = """
            {
              "referenceImagePath": "<USER_HOME>/Desktop/reference.png",
              "referenceImageLabel": "sofa-reference",
              "objectType": "sofa",
              "objectTypeConfidence": 0.9,
              "primaryTargetObject": "sofa",
              "widthRatio": 3.0,
              "depthRatio": 1.1,
              "heightRatio": 1.0,
              "overallEdgeCharacter": "Soft",
              "parts": [
                {
                  "name": "seat cushion",
                  "role": "PrimaryMass",
                  "edgeCharacter": "Soft",
                  "preferredPrimitiveHint": "RoundedBox",
                  "relativeWidth": 3.0,
                  "relativeDepth": 1.0,
                  "relativeHeight": 0.35,
                  "relativeCenterZ": 0.4,
                  "materialKey": "fabric"
                }
              ],
              "detailCues": [
                {
                  "partName": "seat cushion",
                  "kind": "seam",
                  "description": "front horizontal raised seam",
                  "role": "AdditiveDetail",
                  "preferredRepresentation": "RaisedStrip"
                },
                {
                  "partName": "seat cushion",
                  "kind": "texture",
                  "description": "woven fabric surface",
                  "role": "MaterialOnly",
                  "preferredRepresentation": "MaterialOnly"
                },
                {
                  "partName": "floor",
                  "kind": "shadow",
                  "description": "dark cast shadow under the sofa",
                  "role": "Reference"
                }
              ],
              "materialCues": [
                {
                  "key": "fabric",
                  "partName": "seat cushion",
                  "description": "warm brown woven fabric",
                  "baseColor": { "r": 142, "g": 92, "b": 73 },
                  "intent": "Fabric",
                  "roughness": 0.9,
                  "textureLikely": true
                }
              ]
            }
            """
        };
    }
}

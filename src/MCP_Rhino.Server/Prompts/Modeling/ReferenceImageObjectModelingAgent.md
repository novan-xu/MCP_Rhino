# Reference Image Object Modeling Agent

Build an approximate Rhino model from structured reference-image context.

Workflow:

1. Validate that the request includes structured image observations and a live Rhino target.
2. Build a model brief.
3. Decompose visible parts into buildable primitives.
4. Create initial massing before adding detail.
5. Run visual QA immediately after massing.
6. Plan detail refinement and execute only supported simple detail operations.
7. Plan and apply materials by part.
8. Run a final visual QA checkpoint.
9. Decide whether to accept, iterate, or stop with a capability gap.

Do not infer raw bitmap contents inside the Rhino server. Raw image recognition must come from an LLM or future multimodal connector that supplies structured observations.

Do not keep decorating a model when the massing QA indicates the coarse silhouette or proportions are wrong.

Material, texture, lighting, and shadow cues are not object geometry. Do not model woven fabric, grain, printed patterns, color variation, roughness, gloss, highlights, cast shadows, contact shadows, or ambient occlusion as strips, blobs, spheres, slabs, or other physical parts. Route material appearance to the material plan and stop with a capability gap when the current material tools cannot create or apply the needed texture.

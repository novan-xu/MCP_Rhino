# 261010_PLAN_offline-copy-edit

## Background
The user explicitly authorized restoring offline editing on 2026-10-10, with a mandatory duplicate and clear suffix for every offline change. The May live-only migration removed the former disk adapters. Existing live tools and their Undo behavior remain live-only.

## Goals
- Make explicit offline inspection, preview, and batch editing available through the existing Router with Rhino closed.
- Never open an offline source for writing, never overwrite any existing output, and never offer an overwrite escape hatch.
- Return a new sibling `<stem>_offline-edit_<UTC timestamp>_<unique id>.3dm` for each successful apply, including edits to previous copies.

## Architecture ownership
- A separate `MCP_Rhino.Offline` library owns Contracts, Application, Infrastructure, Tools, and assembly-scanned tool registration. Rhino3dm must never load inside the Rhino plug-in (McNeel explicitly excludes plug-in use).
- Router composes this explicit local tool surface with existing live proxy tools; it contains no geometry/file-edit business logic. No additional client transport or daemon.
- Server keeps RhinoCommon/live adapters and existing method signatures. Transport remains BCL-only.

## Key design
- Offline tools are explicitly named `rhino_offline_*`; no automatic fallback after a live error. Live selection never silently retargets an offline request.
- Inspect saved objects, layers, and document metadata with bounded/paged object output. Preview exact-id object attribute/user-text edits, affine geometry transforms, object deletion, layer creation/editing, document user text, and point/line/polyline creation in memory.
- Apply requires the exact preview fingerprint binding the absolute source path, SHA-256 of source bytes, and structured edit request. Reject stale source/changed request/unknown IDs/unsupported operation before creating output.
- Read source under a read-only handle with write/delete sharing denied. Retain the source handle through publication. Read bounded bytes and use Rhino3dm in memory. Serialize the complete model with user data enabled, re-read and verify before publication.
- Stage a unique temporary file and atomically move without replacement to an automatically generated suffixed sibling; collisions fail closed. Source and previous outputs are never write targets. No caller-supplied output path.
- Offline tools explicitly read saved disk state; reject known open documents when Router discovery identifies the same path, and explain that offline inspection cannot see unsaved changes in undiscovered Rhino sessions.
- Input/output version is checked; reject unsupported newer archives instead of silently downgrading. Reject block-definition geometry mutations that could break references.
- Cancellation and validation failures publish no output. Apply is open-world, non-read-only, non-destructive to existing files; preview is read-only.

## Files involved
`src/MCP_Rhino.Offline/`, Router runtime/startup/project, solution, exact Server test-glob exclusion, existing Router/safety regression tests, package staging validation, README, runtime workflow and architecture guides, and matching TEST/EXET artifacts.

## Usage
Inspect a closed `.3dm` with `rhino_offline_inspect_document`; pass an explicit batch to `rhino_offline_preview_edits`, then apply that same batch plus its fingerprint with `rhino_offline_apply_edits`. Use the returned output path for subsequent work. Open the duplicate in Rhino for full modeling/Undo/live-only features.

## Acceptance criteria
- Real Rhino3dm fixture round-trip without Rhino hosting verifies geometry, attributes, layer edits and metadata in the duplicate while source SHA-256 and last-write time remain unchanged.
- Invalid edits, stale previews, path tricks, existing outputs, concurrent applies, and repeated edits cannot overwrite the source or another file.
- Offline MCP tools list and execute with zero Rhino endpoints; live routing conflicts and endpoint failures retain existing behavior.
- Debug and Release solution builds, offline integration/safety tests, existing Router regression, and server MCP safety smoke pass. Published Router contains managed/native offline dependencies.

## Risks and rollback
Rhino3dm is not full RhinoCommon: advanced modeling, viewport, Grasshopper and plug-in execution remain live-only. Unknown plug-in data fidelity needs real-project review, so originals are always retained. File size and batch limits bound resource use. Rollback removes the offline registration/library and reverts the matching documentation; created copies remain ordinary user files. Build/stage only; do not activate or replace installed RHPs without the repository's independent host registry attestation.

## Future extensions
Add further offline operations only with round-trip tests and the same mandatory copy writer. No offline in-place save capability is planned.

## Revision (2026-10-10)
Before implementation the user requested script-based editing instead of operation-specific APIs, with whatever language works best. Replace structured batch tools with `rhino_offline_inspect_document`, `rhino_offline_preview_script`, and `rhino_offline_apply_script`. Use bounded JavaScript (Jint) in the C# host, exposing only a JSON-only bridge to an in-memory Rhino3dm model. Do not expose CLR objects, reflection, modules, filesystem, processes, arbitrary imports, network, or raw File3dm.Write. The script can loop, branch, filter and combine supported geometry/metadata edits. The fingerprint binds source path, source bytes and exact script. Preview retains a bounded, expiring in-memory result; apply publishes those exact validated bytes without rerunning a potentially nondeterministic script. Reject missing/expired previews and changed sources. No API-per-edit MCP surface. Regression tests cover forbidden host access, bounded execution and exact preview/apply output in addition to copy safety. This is a constrained modeling script environment, not arbitrary Python/C# execution or an OS security sandbox.

# 261010_PLAN_offline-rhino-dwg

## Background

The user explicitly authorized constructing the missing copy-only DWG conversion/headless Rhino script runner, for repeated DWG workflows via Rhino. The requested acceptance case is `ROW 1_LINTEL STONE.dwg` in the project's lintel study folder, running the current linked StoneSurfaceBake implementation at baseline Z=0 inches and surface height=4.46645 inches.

## Goals

Provide reusable explicit offline Rhino processing for .dwg/.dxf/.3dm, including conversion-only and trusted C# script processing; preserve every original and prior output. Complete the user's lintel bake on a suffixed .3dm copy and validate actual surface heights and repeated-run duplicate suppression.

## Architecture ownership

- Router remains the sole MCP transport. Add headless preview/apply tools to the offline library, with generated method metadata and existing open-source-path exclusion.
- A separate Windows executable owns Rhino.Inside/RhinoCommon import, C# compilation/execution, document inspection and serialization. Never load RhinoCommon into the Rhino3dm Router process or start another MCP transport.
- Offline Application owns jobs/preview lifetime; Infrastructure owns read-only source leases, worker processes and non-replacing publication. Contracts contain JSON-only requests/results. Trusted native C# execution is explicit and OpenWorld; it is distinct from bounded JavaScript and is not advertised as a sandbox.
- Extend the linked stone script only where needed for an explicit headless RhinoDoc entry point; preserve live behavior and prior user settings/wires.

## Key design

1. Probe Rhino.Inside startup and native DWG import with the installed Rhino 8 before finalizing the runner. Use STA and no UI; surface startup/license/import failures, never automate Windows UI.
2. Hold the source open read-only denying write/delete sharing, snapshot to an owned job directory, execute only against a headless working document. No caller-supplied final path, overwrite or source-save option.
3. Worker input supports an inline trusted C# script using an explicit `doc` and JSON parameters; optional named-class extraction from a trusted linked C# library allows reuse of the GH script's geometry class without executing its UI entry point. Empty script converts/inspects only. Record script/library hashes.
4. Preserve imported layers, blocks, units and source object identities. Use the native DWG importer; report import limitations and counts. Explicit unit override is available for ambiguous DWG units; this test uses inches.
5. Preview creates only owned temporary artifacts, times out/kills only its own process, reopens/validates the generated 3dm, retains exact output bytes for a one-use expiring token. Apply checks source path/hash again, publishes a unique suffixed sibling using create-new staging and non-replacing move, and never reruns scripts.
6. Build/package worker separately from Router/RHP. No production RHP installation, registry activation or previous-version retirement. Use the staged Router for this test if the installed Router is older.

## Files and usage

New worker project, offline service/tool/contracts/process infrastructure, Router registration/packaging/runtime guidance, focused TEST artifacts and EXET. Call headless preview with a DWG path and optional script, review summary, then apply the returned token in the same Router session. Subsequent edits use the returned .3dm path and create another copy.

## Acceptance

Debug/Release solution/plugin builds and MCP safety checks. Source hash/timestamp preservation; source write-lock rejection; source/path/token freshness; failed script/timeout yields no published file; generated 3dm reopens; worker process isolation and no UI. Real DWG conversion, execute current lintel class with 0/4.46645-inch inputs, measure baked surface height, retain source blocks/metadata, repeat bake has no added outputs. Distinguish actual native execution from managed tests. Complete PLAN/TEST/EXET and package validation.

## Risks and rollback

Installed licensed Rhino is required. Native startup, DWG xrefs/proxies and source units may need environment-specific handling; do not claim success without native evidence. Full-trust C# can execute OS code; source locks enforce the named input's preservation but this is not an untrusted-code sandbox. Worker never receives a final output path. Roll back code via scoped changes and restore the backed-up linked script if adapted. No originals or existing outputs are deleted.

## Future work

Additional native Rhino import/export formats can follow the same worker contract. DWG export is separate from this first .3dm copy workflow.

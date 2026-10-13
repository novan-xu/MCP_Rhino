# Stone Wireframe Bake Test Results

Execution date: 2026-09-01

## Static And Build Validation

- Python reusable script compiled successfully.
- Debug CLI fallback smoke passed.
- Release CLI fallback smoke passed.
- MCP safety smoke passed in Debug and Release for 171 tools.
- MCP overlap/inventory smoke passed with 16 tools in the Blocks family.
- Debug solution build passed with 0 warnings and 0 errors using `-m:1`.
- Release solution build passed with 0 warnings and 0 errors using `-m:1`.
- MCP_Rhino 1.3.1 package build passed.
- Startup discovery smoke passed, including idle reconciliation assertions.
- Production RHP contains one plug-in identity and only `McpRhinoStatus` as a production Rhino command.
- Isolated runtime contains no Rhino plug-in or command identity.

The first parallel Debug solution attempt encountered the existing PanelCladdingEditor reference-output race.
The required sequential solution build passed cleanly; MCP_Rhino.Server itself had already compiled cleanly in
both configurations.

## Live Target

Historical user-owned live stone classification model (project path omitted).

Installed MCP_Rhino version: 1.3.1, registry-only, startup-loaded.

## Preview

- assembly definitions: 22
- assembly instances: 570
- stone definitions: 36
- stone occurrences: 1,143
- output definitions to create: 36
- output instances to create: 1,143
- failures: 0
- `_EL` / `_V` assemblies included: 0

Definition-level preview returned the selected source object id, left-to-right line endpoints, base point,
length, orientation, and occurrence counts for all 36 individual stones.

## Apply

- created `WF_...` definitions: 36
- created attributed instances: 1,143
- failures: 0
- undo records: one (`MCP: ApplyStoneWireframeBake`)
- source objects deleted, hidden, exploded, or replaced: 0
- returned object ids: first 200, with the expected bounded-response truncation warning

Every created instance was written with `Assembly_ID` equal to its parent assembly definition name.

## Idempotency And Attribute Verification

A second independent Router preview reported:

- reused definitions: 36
- definitions to create: 0
- existing transform-and-`Assembly_ID` matched instances: 1,143
- instances to create: 0
- failures: 0

The one-to-one existing-placement match consumes instances by output definition, full transform, and exact
`Assembly_ID`, so these counts verify definition compatibility, complete placement coverage, and the required
attribute values without adding duplicates.

## Startup Race Regression

Live validation exposed a route that could remain `ACTIVE_DOC_UNSAVED` when the production plug-in loaded
against Rhino's temporary startup document before the command-line model became active. Version 1.3.1 adds
bounded `RhinoApp.Idle` reconciliation only when the active path differs or a saved document lacks a listener.
After reinstall, the exact target route reached `READY`, remained `READY` after multiple Router client
disconnects, and Rhino stayed open through apply and verification.

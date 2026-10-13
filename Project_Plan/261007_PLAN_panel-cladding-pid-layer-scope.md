# PCpid complete panel-layer scope PLAN

## Background and authorization

On 2026-10-07 the user requested that PCpid detect all panels under
`01_CW Panels::Surfaces-PNL`, including nested sublayers, but change only selected
panels. This explicit change authorizes the construction PLAN/EXET/TEST chain.

## Goals

Number selected panels using complete panel-layer geometry. Check PID uniqueness
throughout that subtree after projecting the selected writes onto existing IDs.
Keep every unselected object's attributes and geometry unchanged.

## Architecture ownership

Extend the existing Application planner, Domain plan, live Rhino adapter/interface
and thin PCpid command. Reuse the existing panel-layer root constant. No MCP tool,
transport or shared registration changes. Update product documentation/version.

## Key design

Exact root or root plus `::` prefix, case-insensitive, includes any nesting depth
without including similarly named siblings. Enumerate all source surface/Brep
objects, including hidden, locked and reference panels as read-only context.
Only editable selected objects in this scope may be targets. Curves/annotations
are not panels. Unsupported panel Breps fail preflight rather than silently
changing the numbering context.

Read all context geometry to infer directions, planes, global rows and per-plane
bays. North/first-floor references may be unselected context panels. Separate
context IDs from selected write IDs throughout; return assignments only for selected
targets and a context count. Before mutation, audit effective PID values over all
context panels (selected proposed IDs plus unselected stored IDs), ignoring blanks.
Reject any duplicate, including an unselected/unselected pair. A selected write
that resolves an old duplicate is permitted. Preserve existing external PID/CID
collision and dependency guards, canonical identity writes, naming and Undo rules.

## Files involved

Existing PanelCladdingPid models/planner/interface/live service/command;
Packaging/PanelCladdingEditor README and manifest; new scoped TEST folder and exact
Server test exclusion; update the prior native test fixture to create source layers.

## Usage

Run PCpid, enter the project code, select only panels needing updates, and choose
north and first-floor references anywhere in the panel subtree. PID/CID/elevation/
level/name remain synchronized only on selected panels. Other panels supply context.

## Acceptance criteria

- Every singleton and partial selection from the 20-panel tutorial reproduces the
  corresponding full-context address; write plans contain no unselected IDs.
- Root and nested layers included; ancestors, similarly named siblings and unrelated
  branches excluded; hidden/locked panels remain context-only.
- Unselected duplicate stored PIDs are detected; selected repairs are allowed;
  collisions with unselected IDs are rejected without writes.
- Existing PCpid regression, scoped tests and Debug/Release solution builds pass.
- Package GUID/hash/command gates pass; stage while Rhino is running. Install only
  with Rhino closed and independently attested host registry access.

## Risks and rollback

Invalid geometry or duplicate PIDs elsewhere in the scope now prevents writes until
corrected. Missing unselected IDs stay blank. No automatic changes to unselected
panels or generated dependencies. Existing native-host startup limitation remains;
document any unexecuted native checks. Preserve the installed RHP until safe activation.

## Future extensions

Separate reporting/highlighting of multiple conflicts or explicit irregular-layout
overrides can be added if requested.

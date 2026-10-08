# PanelCladdingEditor package

This plug-in is independent of MCP_Rhino. Its product identity is
`PanelCladdingEditor`; build it with `Build-PanelCladdingEditorPackage.ps1`, then install or validate
it with `Install-PanelCladdingEditor.ps1`.

The Rhino command surface uses the `PC` prefix: `_PCEditor`, `_PCCreate`, `_PCClear`,
`_PCCrvTemplate`, `_PCMatchSrf`, `_PCMatchCrv`, `_PCSpawnSrf`, `_PCSpawnCrv`, `_PCSyncSrf`,
`_PCSyncCrv`, `_PCUpdate`, and `_PCpid`. `PCUpdate` treats the selected panels' saved attributes as
authoritative and reconciles all CID-bearing managed surfaces and curves: matching objects are
rebuilt, missing objects are created, and stale or duplicate objects are deleted. `PCCrvTemplate`
assigns a full-run horizontal or vertical merge mask only to selected
panel Breps that do not already have a merge code. Configured panels in a mixed selection are
skipped and reported while the remaining eligible panels are updated. Selecting only configured
panels succeeds without changes. A one-row/one-column case with no applicable
run remains mask-free. Surface commands operate only on cladding Breps; curve commands operate only
on extrusion curves.

Since 1.0.96, `PCMatchCrv` copies extrusion assignments together with the segment, merge and hide
masks. Select target panel Breps, then the source; horizontal/vertical grid counts
must match. Profiles, quantities, formulas, parent links and length modifiers are
replaced by the source assignments, while target dimensions, offsets and cladding
remain intact. Configuration is stored in `CW_1.08_FRAME_CONFIG`, and profile
assignments in `CW_1.09_FRAME_TYPE`. An unassigned source
clears target assignments. Run `PCUpdate` to rebuild existing managed curves from
the matched panel settings, or `PCSpawnCrv` to generate curves.

Since 1.0.95, Extrusion Setup includes Clear all beside Cancel. After a Yes/No
confirmation it clears configured and unconfigured profiles, editor values and
the schedule PDF path. Confirm saves the cleared catalogue; Cancel discards it.
The project workbook remains selected so a new schedule can be extracted.

Since 1.0.94, Material Setup includes a trash button beside the project material
catalogue. Select a material to remove it from the working catalogue, then choose
Confirm to save or Cancel to discard the change. Existing panel assignments are
preserved.

Since 1.0.93, PCpid measures the outer boundary on Rhino's verified face plane.
It no longer treats bounding-box thickness as nonplanarity; actual nonplanar
surfaces are still rejected at the document tolerance.

`PCpid` (1.0.89) is the first setup step, before PCCreate. In a saved document it
prompts for a three-letter project code, the panels to update,
a north-facade panel, and a first-floor panel. It writes
`CW_1.03_ELEVATION`, `CW_1.04_LEVEL`, `CW_1.01_PID` and `CW_1.02_CID`, for example
`PID_BKT_W3_05_11` and `CID_BKT_W3_05_11`. CID keeps the existing unit-type role
suffix policy, and the panel name follows the existing shortened-CID convention.
Bay is the last PID/CID segment; PCpid does not introduce another user-text key.

Since 1.0.90, numbering considers every surface/Brep on
`01_CW Panels::Surfaces-PNL` and every nested sublayer, including hidden, locked
and reference panels as read-only context. Only selected editable panels in this
subtree receive PID/CID/elevation/level/name changes. North and first-floor
references may be unselected panels in the subtree. Unrelated layers and similarly
named sibling layers are excluded from numbering.

Since 1.0.91, PCpid also ensures all 30 panel setup keys requested for CW_1.00–1.11,
CW_2.00–2.02, the four CW_2.05 anchor fields, CW_2.06 penetration, the four CW_5.00
definitions, CW_6.00 wind loads, CW_6.01 PSF, CW_6.02 weight and CW_7.00 blinds
exist on selected panels. Existing non-generated values are retained; unassigned
fields use a stored space so Rhino retains the key. Width and height come from
each panel's tight bounds in its own facade plane, in model units with five decimal
places. Unit dimension is `widthxheight`. These three dimensions are refreshed
alongside the IDs on every run. Conflicting case variants fail before any writes.

Since 1.0.92, both PCpid and PCUpdate also standardize selected source surfaces
using the MCP standard four-point skill's convention: lower-left anchor viewed
from the oriented front, clockwise corners in a gravity-aligned local frame,
four-point rebuild, front/back flip, then SwapUV. The original front direction,
footprint, object ID and metadata are retained. Already-standardized surfaces are
not replaced again. Geometry changes share the owning command's Undo record.
PCUpdate generates dependencies after this preparation and continues to exclude
duplicate-CID sources. PCpid's unselected context surfaces are never reordered.

Point ordering supports planar, straight, convex four-corner surfaces without
holes. Unsupported or ambiguous surfaces are reported as skipped for point order;
their geometry is retained and the command's other supported operations continue.
Horizontal faces have no gravity-defined lower-left and are also reported as skipped
by the ordering step (PCpid still requires vertical facade geometry overall).

The panels must be vertical planar single-face surfaces/Breps with outward normals.
The north reference defines project north, including for rotated buildings; world
Z defines up. Same-facing panels on different planes receive separate elevation
numbers, ordered left-to-right then bottom-to-top when viewed from outside.
Aligned bottom edges identify levels across the full panel subtree; the picked floor is 01
and observed rows increment above it (00, -01, etc. below it). Aligned left edges
identify bays, restarting at 01 for every elevation. All alignment uses document
tolerance. Unequal panel heights/widths are supported when their starting edges
align. Missing floors/bays are not inferred from gaps, and staggered layouts may
require preparation. Temporary IDs, existing elevation text, and object names do
not drive numbering. Updating a single panel retains the same inferred address as
updating the complete panel subtree with the same references.

All geometry and document-wide identity checks run before any attribute write.
Duplicate addresses and collisions with unselected objects fail without changes.
The PID audit checks the resulting state of all panels in the subtree: selected
proposed IDs plus unselected existing IDs. Even duplicate PIDs on two unselected
panels are reported and block writes. Blank unselected IDs stay blank; selected
updates that resolve old duplicates are allowed.
Renumbering a panel that already has generated dependencies is blocked until those
dependencies are cleared. An identical rerun makes no changes. Attribute changes
share one Rhino Undo entry; unrelated metadata and geometry are preserved. Escape
at any of the four prompts exits before attributes are written.

`PCCreate` initializes the selected panels' grid from selected guide curves. Since
version 1.0.80 it also writes `CW_2.00_UNIT_DIMENSION`, `CW_2.01_UNIT_WIDTH`, and
`CW_2.02_UNIT_HEIGHT` from each panel's local extents, using invariant five-decimal
values (`widthxheight` for the combined dimension). Existing dimension values are
refreshed. Rerunning PCCreate still resets generated grid and cell assignments.
Since version 1.0.81, its intermediate tracks start segmented even when a guide
spans multiple cells. Guide coverage still determines missing segments; guide
continuity does not create a merge mask. Merge explicitly in PCEditor or apply
PCCrvTemplate when desired. Perimeter frame behavior is unchanged.

Since version 1.0.82, changing H/V values or row/column dimensions in PCEditor
preserves the current delete, hide, and merge masks by track/segment index.
Cladding materials, parent-cell links, and extrusion profile assignments retain
their layout while coordinates move. Save/reload persists the same masks; adding
or removing tracks remains an explicit topology edit.

Since version 1.0.84, cladding-view dashed boundaries follow the resolved cladding
region across actual shared edges, including indirect parent references and
spanning/nonrectangular logical cells. Cell label numbering and material code
equality do not determine line style: separate owners stay solid even with the
same material, and deleted logical-cell interiors remain absent.

Use `PCSyncSrf` after editing associated cladding surfaces or their material layers
to reconstruct the selected panels' cladding layout, material assignments, and
cell ownership. Use `PCSyncCrv` after editing associated extrusion curves to
reconstruct the selected panels' H/V grid and curve topology. Both read existing
geometry back into panel attributes and refresh dependency metadata and panel
dimensions; select the source panel Breps in a saved document.

`PCUpdate` participates in Rhino's command-owned Undo record when invoked from the command line and
opens its own record only when called without an active command record. A successful batch therefore
appears as one normal Rhino Undo entry without attempting an unsupported nested record.
Since version 1.0.83, PCUpdate normalizes parent/child CIDs before checking selected
panel identities. Parent and child panels sharing a PID can update together.
Every panel in a duplicate-CID group is skipped; unique-CID panels still update,
and skipped panels remain selected with their duplicate CIDs reported. An
all-duplicate selection succeeds without dependency changes. Legacy unsuffixed
dependencies whose ownership is ambiguous between selected panels are preserved
and reported; dependencies belonging to skipped panels are excluded from updates.
Existing managed dependencies can remain object-locked, object-hidden, or on locked/hidden managed
layers: update and stale/duplicate deletion bypass those mutation modes without unlocking/showing
the objects or changing layer state, and retained object-level mode is preserved.
Managed extrusion curves use object colors by planned type: main-frame curves are Blue
(`RGB 0,0,255`), horizontal intermediate curves are Purple (`RGB 128,0,128`), and vertical
intermediate curves are DarkGreen (`RGB 0,100,0`). `PCSpawnCrv` applies the colors to new curves,
while `PCUpdate` and `PCSyncCrv` correct existing curves within the selected-panel scope.

Panel cladding saves persist material-independent cell ownership under
`CW_2.13_CLADDING_LOGIC`. This key stores ownership JSON, separate from the binary
hidden-segment mask inside `CW_1.08_FRAME_CONFIG`. `PCSyncSrf` combines that saved owner graph with current Rhino surface
coverage and material layers, while edited splits and merges remain geometry-authoritative.

Since version 1.0.85, panel roles come exclusively from `CW_1.06_UNIT_TYPE`:
`flat` uses a PID-derived CID without a role suffix, `corner_parent` uses `-P`,
and `corner_child` uses `-C`. Keys and values ignore case and surrounding value
whitespace. The old `parent` and `child` flags are ignored. Material surfaces and
extrusion curves inherit the suffix before their cell/curve code, for example
`CID_BKT_W1_03_01-P-0A` and `CID_BKT_W1_03_01-P-INT_B1`.
Existing PC save/create/match/spawn/sync/update paths apply the rule. Changing to
`flat` clears a stale panel CID role suffix. A missing or unsupported unit type
does not rewrite an existing custom panel CID or fall back to legacy flags.

Since version 1.0.86, panel and managed dependency object names use a shortened
CID: `CID_BKT_S1_06_14` becomes `S1_06_14`, and
`CID_BKT_S1_06_14-P-INT_B1` becomes `S1_06_14-P-INT_B1`. The leading `CID_BKT_`
is removed without changing the full CID user text; other `CID_` identifiers lose
only `CID_`, and custom identifiers are preserved. Prefix matching ignores case.
Create, save, match, spawn, sync, and update normalize source-panel names from the
current CID. Spawn/update/sync apply the same rule to surfaces and extrusion curves.
Run PCUpdate on existing configured panels to refresh their dependency names.
Both sync commands detect name-only changes; missing CID leaves the panel name alone.

Since version 1.0.87, PCEditor displays total width and height on separate lines
with five decimal places and shows the shortened panel CID in Current Panel.
In Extrusion view, selecting frame or intermediate curves opens the floating
profile-assignment panel on the canvas. Drag profiles from the sidebar into it;
clear the selection or press Escape to hide it.
Version 1.0.88 fits assigned profiles into two columns within the same floating
panel width, with each profile code above its length modifier and remove button.

Baked cladding surfaces and extrusion curves inherit the panel's `CW_1.05_LOT`
value, including leading zeros. `PCUpdate`, `PCSyncSrf`, and `PCSyncCrv` use this
canonical lot key for inherited metadata. An absent panel lot leaves synced
dependencies without that key; surface spawning requires a populated lot.
Version 1.0.79 replaces `CW_1.05_RELEASE` with `CW_1.05_LOT` without an old-key
fallback. Existing Rhino data must already use the new name.

Every baked cladding Brep persists its complete logical coverage under
`Merge_Mark`, including the owner: a single-cell surface stores `0A`, while a merged
surface stores values such as `0A;1A`. A complete baked coverage partition lets `PCSyncSrf`
distinguish a hidden mullion inside one merged surface from an obsolete boundary between separate
surfaces. Unmarked surface sets do not preserve geometry-missing offsets from the PCEditor parent
graph; unmatched stored values are cleared and the geometry-derived canonical coverage is then
written. Mixed or invalid coverage fails before mutation rather than guessing. The former
`CW_1.03_CLADDING_CELLS` name is accepted only for migration: sync removes it and writes
`Merge_Mark`; conflicting dual values fail before mutation.

The plug-in no longer writes the panel user-text keys `Signature` or
`CW_4.00_CLADDING_SIGNATURE`. Touched panels remove either legacy value; layout compatibility and
reconciliation use the authoritative `CW_2.10`-`CW_2.13` topology/logic plus `CW_4.xx` material and
parent assignments directly.

Combined cladding types are suspended. Editor saves and surface/curve sync no longer
calculate or persist `CW_2.14_CLADDING_TYPE`, and the editor no longer shows a cladding
type preview. Every save scope and changed-panel sync removes that attribute and its
previous names (`CW_2.13_CLADDING_TYPE`, `CW_1.10_CLADDING_TYPE`, `CW_4.00_CLADDING_TYPE`).
PCClear also recognizes these retired keys. Material, owner-graph, and topology
validation remain active. The former frame-typology identifier is also retired.
The old identity/workbook utilities remain dormant for a future design decision.

Since 1.0.96, frame configuration is stored as versioned JSON in `CW_1.08_FRAME_CONFIG`:
`{"v":1,"delete":"<mask>","merge":"<mask>","hide":"<mask>"}`.
Each field retains the existing mask encoding and grid dimensions, including the
delete mask's original polarity. A saved default grid also gets a complete config.
No dimensions, profile definitions or generated typology hash are included in this value.

`CW_1.09_FRAME_TYPE` stores the existing version-2 assignment JSON unchanged: `f`
contains perimeter assignments, `s` segment assignments, `d` profile definitions
and calculation settings, and `x` length modifiers. No assigned profiles means
this key is removed. Generated curve attributes (`Extrusions` and per-code formulas)
keep their existing format.

Existing `CW_2.12_DELETE_MASK`, `CW_2.10_MERGE_MASK`, `CW_2.11_HIDE_MASK` and
`CW_2.09_FRAME_ASSIGNMENTS` remain readable for migration. Nonblank new attributes
take precedence; blank PCpid placeholders allow legacy fallback. Malformed new
payloads fail validation. Saving extrusions/both or matching curve settings writes
the new keys and removes the old masks, assignment key and `CW_1.5D_FRAME TYPOLOGY`.
PCCreate resets frame settings under the new keys; changed template/sync operations
also use the new format. Cladding-only saves preserve frame settings.

Version 1.0.78 renames the former `CW_2.05_SEGMENT_MASK`, `CW_2.06_MERGE_MASK`, `CW_2.07_HIDE_MASK`,
and `CW_2.08_CLADDING_LOGIC` keys to the 2.12/2.10/2.11/2.13 names and suspends cladding type metadata.
Documents predating that rename still require that earlier mask/logic migration.

The extrusion view maintains an image-backed project catalogue of framing profiles in the workbook
`Extrusions` sheet. PDF schedule die numbers such as `ALU-H0651` import as `1D-ALU-H0651`; profile
codes are additive per `FRM`/`INT` curve and baked curves expose the sorted codes in `Extrusions`
user text for length take-off. Panel assignment state is stored in `CW_1.09_FRAME_TYPE`.

The package keeps the Windows x64 Skia/HarfBuzz native binaries both in their `.deps.json` runtime
paths and beside the RHP. Rhino's plug-in load context requires the top-level copies for native
P/Invoke resolution during PDF profile extraction.

When Rhino is open, the production installer stages the bundle under LocalAppData instead of
overwriting a loaded plug-in. Close Rhino and run the installer inside the reported staged bundle to
activate it.

The installed RHP and its private dependencies live at
`%LOCALAPPDATA%\PanelCladdingEditor\plugin\<version>`. The installer writes the complete Rhino 8
current-user registration directly: root product/startup metadata, `PlugIn\FileName`, and the exact
eleven-command `CommandList`. It does not rely on Rhino consuming a shorthand root `FileName` during
first startup. Installer validation rejects shorthand-only, partial, stale-command, or mixed
registrations. It removes the legacy `PanelCladdingEditor` and `BayHealthPanelCladdingEditor` roots
from Rhino Package Manager discovery. Do not place the RHP under
`%APPDATA%\McNeel\Rhinoceros\packages`; doing so gives Rhino two loaders for one plug-in identity
and produces `ID already in use`.

Only `PanelCladdingEditor.rhp` is shipped for the plug-in assembly. Do not also ship the identical
`.dll`; Rhino interprets both files as plug-ins with the same ID.

The installer writes `%LOCALAPPDATA%\PanelCladdingEditor\install-manifest.json` for hash validation
and ownership-aware uninstall. Run `Uninstall-PanelCladdingEditor.ps1` only while Rhino is closed.

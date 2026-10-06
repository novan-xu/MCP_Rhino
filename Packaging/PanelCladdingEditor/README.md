# PanelCladdingEditor package

This plug-in is independent of MCP_Rhino. Its product identity is
`PanelCladdingEditor`; build it with `Build-PanelCladdingEditorPackage.ps1`, then install or validate
it with `Install-PanelCladdingEditor.ps1`.

The Rhino command surface uses the `PC` prefix: `_PCEditor`, `_PCCreate`, `_PCClear`,
`_PCCrvTemplate`, `_PCMatchSrf`, `_PCMatchCrv`, `_PCSpawnSrf`, `_PCSpawnCrv`, `_PCSyncSrf`,
`_PCSyncCrv`, and `_PCUpdate`. `PCUpdate` treats the selected panels' saved attributes as
authoritative and reconciles all CID-bearing managed surfaces and curves: matching objects are
rebuilt, missing objects are created, and stale or duplicate objects are deleted. `PCCrvTemplate`
assigns a full-run horizontal or vertical merge mask only to selected
panel Breps that do not already have a merge code. Configured panels in a mixed selection are
skipped and reported while the remaining eligible panels are updated. Selecting only configured
panels succeeds without changes. A one-row/one-column case with no applicable
run remains mask-free. Surface commands operate only on cladding Breps; curve commands operate only
on extrusion curves.

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
hidden-segment mask in `CW_2.11_HIDE_MASK`. `PCSyncSrf` combines that saved owner graph with current Rhino surface
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
validation remain active. Frame typology is independent and remains enabled.
The old identity/workbook utilities remain dormant for a future design decision.

Topology masks are sparse and independent: `CW_2.12_DELETE_MASK` is stored only for missing segments,
`CW_2.10` only for merges, and `CW_2.11` only for hidden segments. Missing mask attributes decode
as the all-present, all-segmented, all-visible defaults.
The delete mask retains the former segment mask's binary encoding and polarity.

Version 1.0.78 renames the former `CW_2.05_SEGMENT_MASK`, `CW_2.06_MERGE_MASK`, `CW_2.07_HIDE_MASK`,
and `CW_2.08_CLADDING_LOGIC` keys to the canonical names above and suspends cladding type metadata.
Existing documents need value-preserving mask/logic migration before using this version.

The extrusion view maintains an image-backed project catalogue of framing profiles in the workbook
`Extrusions` sheet. PDF schedule die numbers such as `ALU-H0651` import as `1D-ALU-H0651`; profile
codes are additive per `FRM`/`INT` curve and baked curves expose the sorted codes in `Extrusions`
user text for length take-off. Panel assignment state is stored in `CW_2.09_FRAME_ASSIGNMENTS`, and
the deterministic typology governed by segment, merge, hide, and assignment state is stored in
`CW_1.5D_FRAME TYPOLOGY` only when at least one extrusion code is assigned.

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

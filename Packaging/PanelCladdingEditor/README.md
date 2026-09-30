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

`PCUpdate` participates in Rhino's command-owned Undo record when invoked from the command line and
opens its own record only when called without an active command record. A successful batch therefore
appears as one normal Rhino Undo entry without attempting an unsupported nested record.
Existing managed dependencies can remain object-locked, object-hidden, or on locked/hidden managed
layers: update and stale/duplicate deletion bypass those mutation modes without unlocking/showing
the objects or changing layer state, and retained object-level mode is preserved.
Managed extrusion curves use object colors by planned type: main-frame curves are Blue
(`RGB 0,0,255`), horizontal intermediate curves are Purple (`RGB 128,0,128`), and vertical
intermediate curves are DarkGreen (`RGB 0,100,0`). `PCSpawnCrv` applies the colors to new curves,
while `PCUpdate` and `PCSyncCrv` correct existing curves within the selected-panel scope.

Panel cladding saves persist material-independent cell ownership under
`CW_2.08_CLADDING_LOGIC`. `PCSyncSrf` combines that saved owner graph with current Rhino surface
coverage and material layers, while edited splits and merges remain geometry-authoritative.

Panels with user text `parent=1` or `child=1` use a PID-derived CID ending in `-P`
or `-C`. Material surfaces and extrusion curves inherit that role suffix before
their cell/curve code, for example `CID_BKT_W1_03_01-P-0A` and
`CID_BKT_W1_03_01-P-INT_B1`. Existing PC save/create/match/spawn/sync/update paths
apply the rule. Parent takes precedence if both flags are `1`.

Baked extrusion curves also inherit the panel's `CW_1.05_RELEASE` value, including
leading zeros. `PCUpdate` and `PCSyncCrv` refresh existing curve release metadata
from the panel; an absent panel release leaves the curve without that key.

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
reconciliation use the authoritative `CW_2.05`-`CW_2.08` topology/logic plus `CW_4.xx` material and
parent assignments directly. `CW_1.10_CLADDING_TYPE` remains supported.

Topology masks are sparse and independent: `CW_2.05` is stored only for missing segments,
`CW_2.06` only for merges, and `CW_2.07` only for hidden segments. Missing mask attributes decode
as the all-present, all-segmented, all-visible defaults.

Generated cladding type codes use `<system>-<columns>X<rows>-<digest>`, for example
`WT01-4X3-A1B2C3D4`. The cladding marker is implicit in `CW_1.10_CLADDING_TYPE` and is not repeated
inside the code.

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

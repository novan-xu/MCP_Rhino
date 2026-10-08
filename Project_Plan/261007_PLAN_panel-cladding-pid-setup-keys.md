# PCpid setup keys and dimensions PLAN

## Background and authorization

On 2026-10-07 the user explicitly requested that PCpid ensure the listed 30
setup keys exist, including blank fields, and generate CW_2.00 through CW_2.02.
This authorizes construction under the existing selected-write/full-context scope.

## Goals

Every selected panel receives all requested canonical keys. Preserve existing
non-generated field values and unrelated attributes. Recompute PID/CID/elevation/
level and unit dimensions from current geometry. Unselected context stays untouched.

## Architecture ownership and design

Add a pure Application setup service combining the existing PID assignment with
the source snapshot. It owns the required-key list and measures width/height along
the panel's horizontal and in-plane vertical axes from its tight plane bounds.
Use existing invariant five-decimal dimension formatting and widthxheight syntax
in model units. Missing/empty values use the existing Rhino-storable blank space.
Canonicalize case variants without discarding nonblank values; reject conflicting
case-variant values before mutation rather than choosing data to discard.

The live adapter prepares this complete key set for selected panels inside its
existing preflight and single Undo/rollback path. All geometry/context numbering,
PID uniqueness, generated-dependency checks and prompts remain in force.

## Files

- Application/Services/PanelCladding/PanelCladdingPidSetupService.cs
- Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingPidService.cs
- Scoped TEST folder, original PCpid native harness, and exact Server test exclusion
- Packaging manifest/README and matching EXET/TEST records

## Usage

Run PCpid with the existing project, target, north and first-floor prompts.
No additional input is required for metadata initialization or dimensions.

## Acceptance

Test exact key coverage, blank persistence representation, preserved existing and
unrelated values, generated-field replacement, case normalization/conflict handling,
cardinal/rotated/translated panels, model-unit dimensions and repeat stability.
Retain full-context numbering and selected-only writes. Compile native assertions
for actual key persistence and Undo; distinguish native host limitations from passes.
Build the standalone plug-in in Debug/Release and run existing PID/scope regressions.
Verify assembly identity and bundle hashes before staging 1.0.91. Installation follows
the existing authorization only if Rhino is closed and host persistence is attested;
mandatory Validate and independent readback remain required.

## Risks and rollback

Empty strings may remove Rhino keys, so store a space. Do not introduce default
business values for PNL, unit type or other unspecified fields. Tight bounds report
overall panel extents for nonrectangular panels. Reuse dimension precision semantics.
Attribute rollback and Rhino Undo restore original metadata; installer preserves
the prior reachable RHP. Revert only this follow-up for source rollback.

## Future extensions

Additional business defaults or nonvertical panel support require separate requests.

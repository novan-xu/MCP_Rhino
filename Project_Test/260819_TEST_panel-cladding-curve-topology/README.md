# Panel Cladding Curve Topology TEST

Run:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Release
```

The smoke covers the screenshot-shaped PCCreate layout, endpoint-only and cascading dangling guides,
segmented initialization for continuous and separate guides, logical blank-cell persistence, sparse `PCMatchCrv` mask
transfer, different numeric target offsets, incompatible grid rejection, and public command identity.

## Verified result

- Debug: exit 0
- Release: exit 0
- Full continuous-guide fixture: `2H/2V`, no merge mask, nine cells, and twelve individual intermediate extrusion curves plus four perimeter frames.
- Screenshot fixture: `3H/1V`, three missing horizontal atoms, no automatic merge runs, and five surviving logical cells.
- Endpoint-only and cascading dangling fixtures: rejected with no layout plan.
- PCMatchCrv: combined source configuration and complete extrusion assignments are written;
  target offsets, cells, retired cladding type metadata, and unrelated text are preserved.
  `CW_1.08_FRAME_CONFIG` contains the masks; `CW_1.09_FRAME_TYPE` contains assignments.
  The retired frame-typology key is removed.
- Assignment matching covers perimeter/merged/vertical/hidden segments, additive codes,
  1D quantity, 0D fixed/spacing formulas, parents, modifiers, replacement of old assignments,
  empty-source clearing, legacy payloads, idempotence, malformed data and all-target preflight.
  Generated extrusion plans use target dimensions and copied profile formulas.

Assignment matching follow-up evidence is in
`Project_Test/261007_TEST_pcmatchcrv-extrusion-assignments/`.
The later attribute migration supersedes the original separate-mask/typology
persistence; evidence is in `Project_Test/261007_TEST_frame-config-type-attributes/`.

The segmented default supersedes the original guide-continuity merge inference
under the user's 2026-10-06 correction. Follow-up evidence is in
`Project_Test/261006_TEST_pccreate-segmented-default/`.

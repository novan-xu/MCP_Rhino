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
- PCMatchCrv: only nondefault source masks are written; target offsets, cells, type metadata, and
  unrelated text are preserved.

The segmented default supersedes the original guide-continuity merge inference
under the user's 2026-10-06 correction. Follow-up evidence is in
`Project_Test/261006_TEST_pccreate-segmented-default/`.

# PCSpawnSrf Boundary And Parent Order Smoke

This focused smoke validates the 2026-08-20 boundary-first region construction contract and the
parent-reference dropdown order.

## Automated commands

```powershell
dotnet run --project .\Project_Test\260820_TEST_pcspawnsrf-boundary-parent-order\PCSpawnSrfBoundaryParentOrderSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260820_TEST_pcspawnsrf-boundary-parent-order\PCSpawnSrfBoundaryParentOrderSmoke.csproj -c Release
```

The pure checks must report:

- the shared `0A`/`1A` atomic edge is absent from the configured region boundary;
- only boundaries between the region and other panel cells are planned as internal split curves;
- a complete region has no internal split curves;
- parent choices are `0A, 0B, 0C, 0D, 1A, 1B, 1C, 1D`;
- `XX=#123456` in Material Setup produces the exact spawn-layer RGB `(18, 52, 86)`, while a
  missing catalog entry keeps the deterministic fallback.

The same executable attempts a RhinoCommon geometry probe. A standalone process may report a
documented `[SKIP]` because Rhino's native geometry runtime is initialized by the Rhino host.

## Rhino live verification

1. Open a saved test copy containing a single-face panel with at least cells `0A` and `1A`.
2. Set `0A=XX` and `1A=0A`; keep another row or column outside that region when possible.
3. Run `PCSpawnSrf`.
4. Confirm the `-0A` result is one Brep with one face and has no internal seam at the former
   `0A`/`1A` divider.
5. Run `PCSyncSrf` and confirm the panel still reconstructs `0A=XX` and `1A=0A`.
6. Open `PCEditor`, select a cell, and confirm the parent dropdown is column-first.
7. In Material Setup, give `XX` a conspicuous test color, rerun `PCSpawnSrf`, and confirm the
   matching `04_STEP Surfaces::<family>::XX` layer uses that exact color.

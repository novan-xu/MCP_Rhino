# PCSpawnCrv Curve Simplification Smoke

This smoke verifies that:

- `01_CW Panels::Surfaces-PNL::WT-04` maps to
  `02_CW Extrusions::Curves-PNL::WT-04`;
- nested panel-type suffixes are preserved and invalid source roots fail closed;
- every planned extrusion curve carries the derived destination layer; and
- the live Rhino simplification helper reduces a deliberately dense curve while sampled deviation
  stays within the supplied fit tolerance.

Run from the repository root:

```powershell
$env:PATH = 'C:\Program Files\Rhino 8\System;' + $env:PATH
dotnet run --project .\Project_Test\260903_TEST_pcspawncrv-curve-simplification\PCSpawnCrvCurveSimplificationSmoke.csproj -c Debug
```

The default CLI run validates the application-layer routing and reports the native Rhino geometry
case as skipped. In a supported initialized Rhino test host, append `-- --rhino-geometry` to run the
control-point/deviation assertion.

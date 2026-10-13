# Native offline Rhino / DWG validation

Requires Windows, .NET 8 Desktop Runtime/SDK, installed licensed Rhino 8, and the repository dependencies. No UI automation or open Rhino document is used. `RhinoCommon` lives only in the worker process; Router uses Rhino3dm to verify the serialized result.

## Reproduce

```powershell
& Project_Test/261010_TEST_offline-rhino-dwg/Run-Validation.ps1
```

This builds the solution in Debug/Release, runs the existing bounded JavaScript and Router regressions, executes native C# against a real headless RhinoDoc, converts an ASCII DXF with an empty script, verifies original write protection, copy publication, replay/path/freshness rejection, script failure and worker timeout, then runs MCP safety and Router bundle checks. Test models and outputs stay in ignored `Runtime_Test/local/offline-rhino-dwg/`.

To test the staged package with the same native integration suite:

```powershell
dotnet run --project Project_Test/261010_TEST_offline-rhino-dwg/OfflineRhinoSmoke.csproj -c Release -- Release Packaging/MCP_Rhino/artifacts/MCP_Rhino-1.3.4/bin/MCP_Rhino.Router.exe
```

For the project-specific lintel acceptance, provide the explicitly requested original DWG and current linked C# library:

```powershell
dotnet run --project Project_Test/261010_TEST_offline-rhino-dwg/DwgBakeAcceptance.csproj -c Release -- Release $dwgPath $linkedScriptPath apply
```

Omit `apply` to preview only. The test uses the actual Router MCP stdio transport and the named `StoneSurfaceBake` class, not a rewritten geometry implementation. The library needs the explicit `RunHeadless(RhinoDoc, Settings, bool)` entry point. It checks all imported persistent objects' geometry CRC and serialized attributes before/after baking, every generated lintel height, and a second bake with no added objects/definitions. After apply, it independently opens the saved 3dm with Rhino3dm, checks block member geometry and IDs, measures transformed surface occurrences, and reopens the saved copy in a fresh native worker to check duplicate suppression again. `BakedIds` includes existing skipped assembly IDs, so it is not an added-object count.

## Results — 2026-10-10 local / 2026-10-11 UTC

- Debug and Release solution/plugin/CLI builds: passed, zero warnings/errors. Safety checks: 171 existing Server tools plus explicit offline annotation audit; Router surfaces five offline tools.
- Native tests: passed in both configurations, including DXF conversion and real stdio round-trip; packaged Release Router passed the same suite.
- Original DWG import: 38,830 persistent objects and 906 definitions. `ObjectTable.Count` was 71,585 because it includes deleted import intermediates; enumeration of top-level plus active definition members matches serialization.
- Lintel bake: 26 assembly instances, 26 lintel surface occurrences, six new definitions, 38,862 persistent output objects, 912 definitions, 63 layers, inches.
- All 38,830 imported objects preserved; source file hash/timestamp unchanged. Baseline Z = 0 inches; measured height = 4.46645 inches (saved transformed maximum 4.466450000000001, within 1e-7 tolerance). STN_ID and Assembly_ID preserved.
- Both same-session and reopened-copy bake added zero objects/definitions.
- Final output size: 17,412,983 bytes. SHA-256: `65BB9146A0C094F5CECFF26131E18E2E9C6636D58B7C1755CD269721143ACD29`.
- Source SHA-256: `2CA3FAA9614B539C76AA59261AC93D693EF21A16D278DEDC1765CA1FA26B1654`.
- Native runtime: Rhino 8.35.26251.13001. Detailed private paths/results are in ignored `Runtime_Test/local/offline-rhino-dwg/dwg-bake-result.json`; build/native/package logs are in ignored `.validation/offline-rhino-dwg/`.
- Package 1.3.4: all 181 manifest file hashes matched; packaged Router `--validate-install` and direct compiled RHP GUID checks passed. Package is staged, not production-installed.

DWG fidelity is governed by Rhino's importer. These assertions cover imported geometry in this drawing; they do not claim support for every AutoCAD proxy entity, missing font or external reference. Relative external references may need separate handling because import uses a temporary snapshot. C# is trusted full-trust code, not an OS sandbox. No interactive Grasshopper solve or live document mutation was performed.

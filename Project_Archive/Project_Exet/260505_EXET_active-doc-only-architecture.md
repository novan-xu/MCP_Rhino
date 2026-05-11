# 260505_EXET_active-doc-only-architecture

## 对应计划

- Plan: `Project_Plan/260505_PLAN_active-doc-only-architecture.md`
- Execute date: 2026-05-05

## 关联产物

- Test folder: `Project_Test/260505_TEST_active-doc-only-architecture/`
- Commit / PR: not created in this working session.

## 执行结果 / 实际落地范围

- Removed the disk-backed repository path and deleted:
  - `Application/Interfaces/IRhinoDocumentRepository.cs`
  - `Application/Interfaces/IGeometryBuilder.cs`
  - `Application/Interfaces/IGeometryValidator.cs`
  - `Application/Interfaces/IObjectEditSpecValidator.cs`
  - `Infrastructure/Rhino/RhinoDocumentRepository.cs`
  - `Infrastructure/Rhino/RhinoGeometryBuilder.cs`
  - `Infrastructure/Rhino/RhinoGeometryValidator.cs`
  - `Infrastructure/Rhino/RhinoObjectEditValidator.cs`
- Removed `AddOfflineRhinoAdapters()` and all calls to it.
- Moved pure evaluator / formatter registrations into `AddRhinoApplication()`.
- Expanded live validators so services depend only on live-facing validation interfaces.
- Routed former public offline service methods (`Get`, `Read`, `Preview`, `Filter`, `ResolveByObjectIds`, `FindLayerCandidates`) to live implementations.
- Removed offline-only CLI command routing from `DeveloperCommandHandler`.
- Reworked legacy CLI fallback smoke tests so they assert `LIVE_RHINO_REQUIRED` instead of reading fixture files.
- Removed the `Rhino3dm` package reference and the native resolver that existed only for disk-file access.
- Rewrote `Project_Guides/MCP_Rhino Architecture.md` to Live Only execution mode.

## 与计划的偏差

- Added `Project_Test/260505_TEST_active-doc-only-architecture/README.md` even though the plan did not request a new smoke slug. This keeps the required PLAN -> TEST -> EXET artifact chain without registering a new command.
- Live Rhino smoke commands were not executed in this shell because Rhino UI/plugin hosting is not available here. They remain required manual regression checks.
- `Rhino.FileIO` remains in `LiveRhinoFileExporter` through RhinoCommon aliases (`FilePdf`, `FileWriteOptions`). This is live export support, not disk-backed model read/write.

## 施工中发现并修复的问题

- CLI fallback geometry creation initially attempted RhinoCommon geometry validation before `NullLiveRhinoDocumentAccessor` could return `LIVE_RHINO_REQUIRED`, causing `rhcommon_c` load failure outside Rhino. Fixed by moving geometry creation validation behind the live accessor boundary.
- Legacy smoke tests still imported unaliased Rhino3dm types and read fixture files. Reworked them to active-doc-only CLI fallback checks.

## 测试记录

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --nologo
```

- Exit code: 0
- Result: build succeeded, 0 warnings, 0 errors.
- Artifact verified: `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp` exists.

```powershell
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release --nologo
```

- Exit code: 0
- Result: build succeeded, 0 warnings, 0 errors.
- Artifact verified: `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe` exists.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- geometry-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

- Exit code: 0
- Key output:
  - `CreatePoints returned LIVE_RHINO_REQUIRED`
  - `TransformObjects returned LIVE_RHINO_REQUIRED`
  - `ReplaceGeometry returned LIVE_RHINO_REQUIRED`
  - `DeleteObjects returned LIVE_RHINO_REQUIRED`

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- online-mutation-refactor-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

- Exit code: 0
- Key output:
  - `GetDocumentUserStrings rejected in CLI fallback`
  - `PreviewObjectUserTextWrites rejected in CLI fallback`
  - `SetDocumentUserStrings rejected in CLI fallback`
  - `CreatePoints rejected in CLI fallback`
  - `ApplyObjectUserTextWrites rejected in CLI fallback`

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- layer-management-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

- Exit code: 0
- Key output:
  - `GetLayers rejected in CLI fallback`
  - `CreateLayers rejected in CLI fallback`
  - `ModifyLayers rejected in CLI fallback`
  - `DeleteLayers rejected in CLI fallback`
  - `PurgeLayers rejected in CLI fallback`
  - `PreviewModifyLayers rejected in CLI fallback`
  - `PreviewDeleteLayers rejected in CLI fallback`
  - `PreviewPurgeLayers rejected in CLI fallback`

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- find-layer-candidates Runtime_Test\MCP_rhino_test.3dm Wall
```

- Exit code: 1
- Output: `CLI mode only supports explicit developer commands. Load the Rhino plugin and use MCP_Rhino.Bridge for MCP transport.`

```powershell
rg -n "File3dm\.Read|File3dm\.Write|IRhinoDocumentRepository|RhinoDocumentRepository|AddOfflineRhinoAdapters" src Project_Guides README.md Project_Test
```

- Exit code: 1
- Meaning: no matches.

```powershell
rg -n "Offline Allowed|File3dm\.Read|--force-offline|Rhino/Offline|offline|Offline" "Project_Guides\MCP_Rhino Architecture.md"
```

- Exit code: 1
- Meaning: no matches.

## 验收判据对齐

- Build and static checks passed.
- Deleted command validation passed.
- CLI fallback smoke checks passed and now assert live-only `LIVE_RHINO_REQUIRED`.
- Architecture guide keyword checks passed.
- Historical PLAN / EXET documents were not rewritten.

## 回退验证

- No runtime rollback command was executed.
- Structural rollback remains straightforward through git because removed offline files and DI calls are isolated in this change set.

## 当前遗留项

- Rhino-hosted live smoke commands still need to be run manually in Rhino 8:
  - `_McpGeometryAnalysisSmoke`
  - `_McpFileImportExportSmoke`
  - `_McpGeometryEditMolecularFoundationSmoke`
  - `_McpGeometryEditCurveCompositeSmoke`
  - `_McpGeometryEditDerivedRoutingSmoke`
  - `_McpGeometryEditSurfaceCompositeSmoke`
  - `_McpSurfacePointOrderRebuildSmoke`
  - `_McpLayerBehaviorProbe`
- Historical plan/execution docs still contain pre-260505 offline descriptions by design.

## 结论

The repository is now active-doc-only for business capabilities. Disk-backed `.3dm` read/write infrastructure has been removed, service entry points are live-routed, CLI mode is limited to fallback smoke behavior, and the architecture guide matches the new execution model.

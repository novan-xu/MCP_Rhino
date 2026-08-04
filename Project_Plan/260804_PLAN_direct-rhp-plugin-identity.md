# Direct RHP Plug-in Identity Plan

## 背景

Rhino 8 currently discovers `MCP_Rhino.Server.rhp`, while the installed package also contains an identical `MCP_Rhino.Server.dll`. Runtime inspection confirmed that Rhino has loaded both files from the same package version. Because both assemblies declare the same Rhino plug-in GUID, the second load fails with `ID already in use`.

The current build creates the `.rhp` by copying the server `.dll`, and the package copies the complete publish output. This produces two discoverable managed assemblies with one Rhino plug-in identity.

## 目标

- Build `MCP_Rhino.Server` directly as a `.rhp` for both Debug and Release.
- Package exactly one server assembly identity: `MCP_Rhino.Server.rhp`.
- Preserve `Program.cs` as a development-only smoke fallback without adding a second production transport.
- Keep `MCP_Rhino.Router.exe` as the only external client transport and leave route behavior unchanged.
- Reject future MCP_Rhino packages that contain a sibling `MCP_Rhino.Server.dll` or inconsistent dependency metadata.
- Provide a dedicated regression smoke for the direct-RHP identity contract.

## 架构归属

- `src/MCP_Rhino.Server/`: server output shape and development CLI build mode.
- `Packaging/MCP_Rhino/`: Release staging and package validation.
- `Project_Test/260804_TEST_direct-rhp-plugin-identity/`: build/package identity regression tests and the capability-owned smoke registration.
- `src/MCP_Rhino.Server/Infrastructure/Plugin/`: capability-owned Rhino smoke command only; no MCP tool or transport changes.

## 关键设计

1. Remove the post-build DLL-to-RHP copy. MSBuild will emit the default plug-in artifact directly with `.rhp` as its target extension.
2. First verify the executable/direct-RHP combination in a minimal test project. If `Program.cs` cannot be executed reliably in that shape, introduce an explicit development-only CLI build property that emits `.dll`; production Debug/Release builds remain direct `.rhp`.
3. The package builder will use publish output for dependencies, then take the server assembly and dependency metadata from the direct Release build. It will exclude the published self `.dll`, apphost, and stale self metadata.
4. The packaged `MCP_Rhino.Server.deps.json` must identify `MCP_Rhino.Server.rhp`, not `MCP_Rhino.Server.dll`.
5. Package and installer validation will fail closed if both server `.rhp` and `.dll` exist, the `.rhp` is absent, or dependency metadata points at the self `.dll`.
6. Router files remain under the existing `bin` layout. No bridge, companion, fixed debug pipe, embedded chat, or alternate transport is added.
7. A unique `direct-rhp-plugin-identity-smoke-test` CLI slug and dedicated Rhino command will verify the loaded assembly extension, sibling-file exclusion, dependency metadata, and plug-in GUID identity.
8. Installation into the live user package is staged when Rhino is running; the loaded package is never overwritten in place.

## 涉及文件

- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpDirectRhpPluginIdentitySmokeCommand.cs`
- `Packaging/MCP_Rhino/Build-McpRhinoPackage.ps1`
- `Packaging/MCP_Rhino/Install-McpRhino.ps1`
- `Packaging/MCP_Rhino/package-manifest.json`
- `Project_Test/260804_TEST_direct-rhp-plugin-identity/`
- `Project_Exet/260804_EXET_direct-rhp-plugin-identity.md`

## 使用方式

- Production plug-in builds use the ordinary solution commands; no special build property is required.
- External MCP clients continue launching the installed `MCP_Rhino.Router.exe` only.
- The development smoke fallback uses the documented CLI command recorded by the TEST artifact if a conditional CLI output is required.
- In Rhino, `_McpDirectRhpPluginIdentitySmoke` runs the capability-owned live identity check after the corrected package is loaded.

## 验收标准

- `dotnet build .\MCP_Rhino.sln -c Debug` succeeds.
- `dotnet build .\MCP_Rhino.sln -c Release` succeeds.
- Default Debug and Release server build outputs contain `MCP_Rhino.Server.rhp` and no sibling `MCP_Rhino.Server.dll`.
- The packaged plug-in directory contains one server assembly identity and its dependency metadata names `.rhp` as the self runtime asset.
- The package contains `MCP_Rhino.Router.exe` in the existing Router layout and no retired connection component.
- The development CLI smoke fallback still executes.
- Existing Router-only smoke and MCP tool-safety checks pass in Debug and Release where applicable.
- The package installs or stages successfully without overwriting a package currently loaded by Rhino.
- After Rhino restarts into the corrected package, `_McpDirectRhpPluginIdentitySmoke` passes and the duplicate plug-in-ID load error is absent.

## 风险与回退方案

- Direct `.rhp` output can differ from `dotnet publish` dependency metadata. The builder therefore combines published dependencies with the direct build's self artifact and validates the result before installation.
- A development CLI output can pollute the normal `obj`/`bin` graph. Any conditional CLI shape will use an explicit build contract and the package build will perform a clean direct rebuild before staging.
- If the corrected version cannot load, the installer rollback/backup mechanism and versioned Rhino package layout preserve the prior package for recovery. The live loaded package will not be modified while Rhino is running.
- No plug-in GUID change is planned; identity remains stable and the duplicate physical assembly is removed.

## 后续扩展方向

- Add the direct-RHP package audit to release CI.
- Make package ownership manifests record the single self-assembly invariant explicitly.
- Add an automated post-restart Rhino harness when a supported non-UI Rhino integration runner becomes available.

## Rhino 8.29 live-acceptance correction

The 1.1.1 live launch proved that the direct library RHP starts successfully, publishes its Router route,
and is the only MCP_Rhino assembly in the package tree. Rhino 8.29 then runs its package-install pass
against the same already loaded `AtStartup` RHP and reports `ID already in use`. McNeel tracks this
package-update ordering defect as RH-94972 and corrected it in Rhino 8.32 RC1. The installed Rhino is
8.29, so the package must also work around the older loader.

This correction supersedes the plan's production Rhino-command and live-command-smoke requirements:

1. The installer owns one canonical Rhino registration at
   `HKCU\Software\McNeel\Rhinoceros\8.0\Plug-Ins\7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`.
2. Installation and repair set `LoadMode=1` and the `PlugIn\FileName` value to the active versioned RHP
   while Rhino is closed, before the next startup.
3. Validation requires the canonical registry path to match the installer-owned active RHP.
4. Any owned legacy `CommandList` and `Panels` subkeys under the MCP_Rhino plug-in registration are
   removed. No unrelated Rhino registration is touched.
5. Production RHP builds exclude all `Mcp*SmokeCommand` and other developer Rhino command entry types.
   Developer smoke coverage remains available through the conditional CLI host and static/package tests.
6. The installer records and tests registry ownership through an injectable registry root so isolated
   regression does not modify the production Rhino registration.
7. A live restart is accepted only when Rhino loads the canonical RHP once, no package-install error is
   displayed, the Router route is READY, and the process contains the RHP plus the GUID-free Runtime DLL.

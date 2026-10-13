# GA- assembly naming regression

## Automated checks

`Run-Validation.ps1` builds Debug/Release CLI hosts and runs the existing
`stone-wireframe-bake-smoke-test`, MCP safety, tool inventory, and Router smokes.
The original capability retains ownership of its CLI slug; this folder extends
its naming/service regression coverage without registering an alias.

`Verify-StagedInstaller.ps1 -BundleRoot <bundle>` exercises only an isolated
filesystem/registry fixture. It verifies initial installation, validation after
Rhino-style removal of root FileName, rejection of stale PlugIn paths, rejection
of extra commands, and repair from the Rhino-hydrated product name.

## Results (2026-10-09)

- Debug and Release solution builds: passed, zero warnings/errors (`-m:1`).
- Debug and Release capability, safety, inventory, and Router checks: passed.
- MCP safety/inventory: 171 tools; 16 in Blocks.
- Current GA- names accepted case-insensitively; legacy GA, `_EL`, `_V`, doubled
  separators, missing suffixes, and space-suffixed names rejected.
- Explicit scopes reach the live adapter with exact spelling and deduplication.
- Normal, hidden, and locked local instances included; deleted, definition, and
  external-reference objects excluded.
- Release package 1.3.2 built and all bundle hashes verified.
- RHP PE metadata declares `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`, matching manifest
  and plugin class. The isolated runtime declares no plugin identity.
- Installer fixture checks passed. They do not establish host persistence.

Local logs: `build-debug.log`, `build-release.log`, `smoke.log`, `package.log`,
`identity.log`, `installer-test.log`. Generated outputs are Git-ignored.

## Pending user-host activation and live bake

Save and close Rhino and Router processes. In ordinary user PowerShell run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\01_Projects\MCP_Rhino\Project_Test\261009_TEST_stone-assembly-ga-hyphen\Install-InHostPowerShell.ps1
```

The script verifies staged hashes, invokes the product installer, and starts a
separate host process for `-Mode Validate`, exact PlugIn path/CommandList readback,
and all three registration timestamps. Its `Preflight` mode is read-only and has
passed; its production `Install`/`Attest` modes have not been executed by the agent.

After Rhino reopens, verify the exact RHP loaded and compare root/CommandList
timestamps against `.validation/ga-hyphen-host/after.json`; PlugIn must remain
unchanged. Then select the user's live document through the installed Router,
preview the GA- scope, review selected edges/base points, apply, verify exact
Assembly_ID values and a second preview with zero proposed creations. No live
geometry changes or successful production installation are claimed yet.

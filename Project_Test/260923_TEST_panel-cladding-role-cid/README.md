# Panel parent/child CID regression

Run from the repository root:

```powershell
dotnet run --project Project_Test\260923_TEST_panel-cladding-role-cid\PanelCladdingRoleCidSmoke.csproj -c Debug
dotnet run --project Project_Test\260923_TEST_panel-cladding-role-cid\PanelCladdingRoleCidSmoke.csproj -c Release
```

The executable uses application services and in-memory snapshots. It does not open
or modify a Rhino document. The fixture's document name is an identifier only.

Coverage:

- Exact parent and child panel/surface/curve CIDs, including `-0A` and `-INT_B1`.
- Unchanged PID inheritance and unchanged ordinary panel behavior.
- Spawn and update surface plans, all three editor save scopes, and PCCreate writes.
- Both sync scopes, with legacy and already suffixed surface CIDs and no coverage payload.
- Panel CID-only changes included in the sync commit path.
- Missing stored CID on a flagged panel, repeated execution, and switching role flags.
- Case-insensitive flag keys, trimmed values, exact `1` activation, and parent precedence.
- Unflagged custom CIDs preserved, including CIDs ending in `-P` or `-C`.
- Existing dependency scope includes the selected role and legacy unsuffixed output,
  and excludes the other role's already suffixed output.

## Results (2026-09-23)

Debug and Release focused runs: exit 0. Output in each configuration:

```text
[OK] parent: spawn, update planning, all save scopes, create, and both sync scopes
[OK] child: spawn, update planning, all save scopes, create, and both sync scopes
[OK] ordinary: spawn, update planning, all save scopes, create, and both sync scopes
[OK] flag values, casing, precedence, repeat/role changes, custom IDs, and sibling update isolation
```

Build commands, each exit 0 with 0 warnings and 0 errors:

```powershell
dotnet build src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo -v minimal
dotnet build src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo -v minimal
dotnet build .\MCP_Rhino.sln -c Debug --nologo -v minimal
dotnet build .\MCP_Rhino.sln -c Release --nologo -v minimal
```

Existing regressions run using `dotnet run --project <path> -c Debug`, each final run exit 0:

- `Project_Test/260805_TEST_panel-cladding-spawn/PanelCladdingSpawnSmoke.csproj`
- `Project_Test/260807_TEST_panel-cladding-surface-sync/PanelCladdingSurfaceSyncSmoke.csproj`
- `Project_Test/260818_TEST_panel-cladding-extrusion-sync/PanelCladdingExtrusionSyncSmoke.csproj`
- `Project_Test/260818_TEST_panel-cladding-create-command/PanelCladdingCreateSmoke.csproj`
- `Project_Test/260903_TEST_panel-cladding-managed-cid-scope/PanelCladdingManagedCidScopeSmoke.csproj`
- `Project_Test/260903_TEST_panel-cladding-update-command/PanelCladdingUpdateCommandSmoke.csproj`
- `Project_Test/260903_TEST_pcupdate-command-undo/PCUpdateCommandUndoSmoke.csproj`
- `Project_Test/260903_TEST_pcupdate-locked-managed-objects/PCUpdateLockedManagedObjectsSmoke.csproj`

The Undo smoke checks the source contract in this run; its optional native ambient
Undo branch was explicitly skipped because no Rhino test runtime was initialized.
Live geometry mutation, failure rollback, and Rhino Undo were not exercised.

Initial test corrections: the new fixture needed two horizontal tracks to produce
the example `INT_B1`. The old PCUpdate source assertion was updated to the lookup
that also checks role ownership. Three old regression suites required exactly
package 1.0.73 despite the current manifest already being 1.0.75; they now require
1.0.73 or later, retaining their registration and behavioral assertions.

`git -c core.safecrlf=false diff --check`: exit 0.

Direct compiled assembly inspection: exit 0. Both Debug and Release RHPs declare
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the plug-in class and manifest.

```powershell
dotnet run --project Project_Test\260805_TEST_rhino-plugin-assembly-identity\PluginAssemblyIdentityProbe\PluginAssemblyIdentityProbe.csproj -- src\PanelCladdingEditor\bin\Debug\net8.0-windows\PanelCladdingEditor.rhp src\PanelCladdingEditor\bin\Release\net8.0-windows\PanelCladdingEditor.rhp
```

## Deployment validation (2026-09-23)

Package 1.0.76 publish and direct Release rebuild passed. Pre/post-package GUID checks
passed against both product manifests and confirmed distinct non-empty plug-in IDs.
The product installer and `-Mode Validate` ran successfully in an independently
verified, same-user Windows host context. A second host process after installer exit
confirmed the new existing RHP path, package-matching SHA-256, exact eleven-command
registration, advancement of all three registry key timestamps, and another passing
`-Mode Validate`. The local verifier exited 0. See the EXET deployment follow-up.

Post-start Rhino loaded-module/timestamp verification remains pending; no runtime
behavior acceptance is claimed by the installation gates.

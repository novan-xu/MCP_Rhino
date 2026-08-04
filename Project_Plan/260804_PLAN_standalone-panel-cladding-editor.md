# Standalone panel cladding editor plan

## Background

The first implementation placed the panel cladding editor inside `MCP_Rhino.Server`. The user has
clarified that the editor must instead be a standalone Rhino 8 plug-in with no MCP_Rhino runtime,
project, package, Router, Transport, MCP SDK, or dependency-injection dependency.

## Goal

- Create an independent `PanelCladdingEditor` Rhino plug-in project and package.
- Preserve the completed panel workflow: H/V key parsing, curved/projectable axonometric preview,
  cell editing, deterministic type identity, Rhino attribute commit, and visual `.xlsx` catalog.
- Rename the user commands to `_PanelCladdingEditor` and `_PanelCladdingEditorSmoke`.
- Remove the editor implementation, commands, dependencies, registrations, and active smoke hook
  from `MCP_Rhino.Server`.
- Ensure the standalone binary contains no MCP_Rhino, Router, Transport, ModelContextProtocol,
  Companion, Bridge, or chat dependency.
- Build and validate standalone Debug/Release outputs and an independent current-user installer.

## Architecture ownership

- `src/PanelCladdingEditor/Domain`: geometry/layout/type models.
- `src/PanelCladdingEditor/Application`: key parsing, projection, signature, and save coordination.
- `src/PanelCladdingEditor/Infrastructure`: Open XML workbook, PNG renderer, and live Rhino adapter.
- `src/PanelCladdingEditor/UI`: Eto command, modeless editor, and preview canvas.
- `src/PanelCladdingEditor/PanelCladdingEditorPlugin.cs`: minimal Rhino plug-in entry point only.
- `Packaging/PanelCladdingEditor/`: independent build/install/validate scripts and manifest.
- `Project_Test/260804_TEST_standalone-panel-cladding-editor/`: standalone smoke executable and
  package/source audits.

The standalone project may depend only on Rhino 8 SDK assemblies, Eto supplied by Rhino,
`DocumentFormat.OpenXml`, `System.Drawing.Common`, and the .NET runtime. It must not reference any
MCP_Rhino project or assembly.

## Key design

- Move the existing panel-specific domain/application/file/render code into the standalone assembly
  and rename namespaces to `PanelCladdingEditor.*`.
- Replace MCP_Rhino's `OperationResponse` with a small plug-in-local result contract.
- Replace `ILiveRhinoDocumentAccessor` and Microsoft DI with direct, UI-thread RhinoDoc access and
  explicit composition in the command.
- Give the plug-in a new GUID and package name so Rhino treats it as a distinct product.
- After Rhino persisted a collision against the original development identity, issue the clean
  product identity `BayHealthPanelCladdingEditor` with GUID
  `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35`; command names and document schema remain unchanged.
- Keep the command modeless and document/object pinned, with stale-fingerprint and Undo protection.
- Keep workbook preparation before Rhino mutation and rollback Rhino state when external commit
  fails.
- Exclude the superseded historical MCP-hosted test partial from active Server compilation while
  preserving its PLAN/TEST/EXET record.
- Independently package the plug-in under Rhino's current-user package directory; do not modify MCP
  client configuration or MCP_Rhino installation ownership.

## Involved files

- New `src/PanelCladdingEditor/**`
- New `Packaging/PanelCladdingEditor/**`
- New `Project_Test/260804_TEST_standalone-panel-cladding-editor/**`
- New `Project_Exet/260804_EXET_standalone-panel-cladding-editor.md`
- `MCP_Rhino.sln`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- Removal of panel-specific production sources from `src/MCP_Rhino.Server`.

## Usage

1. Install the independent package and restart Rhino.
2. Preselect one panel Brep carrying the H/V and cladding keys.
3. Run `_PanelCladdingEditor`.
4. Enter cladding values, choose an `.xlsx`, and save the type.

The plug-in uses Rhino's standard `WhenNeeded` policy: Rhino registers its commands from the package
and loads the `.rhp` on the first command invocation. It does not require an always-running service.

## Acceptance criteria

- `PanelCladdingEditor.csproj` has no ProjectReference and no MCP/hosting package.
- Standalone Debug and Release builds pass with zero warnings/errors.
- Deterministic key/projection/signature/workbook smoke passes independently of MCP_Rhino.Server.
- Standalone package install/validate passes in isolated roots.
- Binary dependency audit finds no MCP_Rhino, Router, Transport, ModelContextProtocol, Companion,
  Bridge, or chat reference.
- MCP_Rhino.Server builds without Open XML/Eto/Rhino.UI dependencies introduced by the editor and
  contains neither panel editor command.
- The shared Grasshopper script remains beside the Rhino document and is unchanged by this split.

## Risks and rollback

- Moving source may expose hidden coupling to Server contracts; compilation and binary-reference
  audits must catch it.
- Rhino cannot hot-replace a loaded plug-in, so production installation must stage while Rhino is
  open and complete after Rhino exits.
- Rollback is removal of the standalone package; MCP_Rhino remains independently installed.

## Future extensions

- Add a toolbar/button package.
- Add configurable system-code rules and cladding-code libraries.
- Add explicit workbook schema migrations and catalog browsing.

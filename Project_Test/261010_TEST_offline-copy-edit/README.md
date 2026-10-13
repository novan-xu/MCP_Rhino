# Offline script editing validation

Run from repository root:

```powershell
Project_Test/261010_TEST_offline-copy-edit/Run-Validation.ps1
```

This runs Debug and Release solution builds, the standalone `offline-copy-edit-smoke-test`, existing Router protocol smoke, and Server MCP tool safety smoke. The offline executable also verifies the three offline tools' runtime metadata and real stdio preview/apply calls. It requires .NET 8; offline execution itself does not require Rhino to be running. Full solution compilation still references the installed Rhino SDK.

Generated anonymous fixtures and output copies stay in ignored `Runtime_Test/local/offline-copy-edit/<run-id>/`. No user model is used. The standalone test source and its generated files are excluded precisely from the RHP's historical test-source glob; this project independently compiles them.

Assertions cover geometry/metadata round-trip, unchanged source hash/timestamp, repeated/concurrent duplicate creation, nondeterministic scripts applied exactly once from cached bytes, missing/stale/expired/wrong-source tokens, bounded preview cache, read-only/locked sources, cancellation, invalid UUIDs/numbers/attributes, caught model errors, forbidden host/filesystem/CLR/process access, disabled eval, loop limits, no-endpoint tool listing, live failure without offline fallback, and actual MCP stdio execution.

## Workflow

Use `rhino_offline_inspect_document` for IDs and script help. Submit JavaScript text to `rhino_offline_preview_script`, review the counts and log, and call `rhino_offline_apply_script` with the exact source path and returned token in the same Router session. Tokens expire after ten minutes and are consumed on successful apply. New outputs use `<stem>_YYYYMMDD_HHMMSS.3dm` with host local date and 24-hour time. Same-second collisions wait for an available second with cancellation and a ten-second limit. No output-path or overwrite option exists. Subsequent scripts start from the returned copy and create another copy without accumulating suffixes.

Example script:

```javascript
const layer = model.addLayer('Offline review', '#4488CC');
for (let offset = 0; ; offset += 200) {
    const page = model.objects(offset, 200);
    for (const obj of page.items) {
        if (!obj.isDefinitionGeometry && obj.name.startsWith('Panel')) {
            model.setAttributes([obj.id], {
                layerId: layer, userText: { Reviewed: 'Yes' }
            });
        }
    }
    if (offset + page.items.length >= page.total) break;
}
model.setDocumentUserText('Review status', 'Offline copy');
model.log('Review changes prepared');
```

Collect IDs before deleting/adding objects when iterating pages, since those operations change pagination. Transforming an object preserves its UUID but can change table order; collect IDs before transforms too. Supported operations and limits come from the inspect tool's `scripting` field. The interpreter only exposes JSON values and a restricted modeling bridge, not raw Rhino3dm/CLR objects. It is not a general Python/C# runtime or an OS sandbox for hostile code. Rhino-specific plug-in data and unsupported geometry work should be reviewed in Rhino; the original is always retained.

## Release staging

Build a package with `Packaging/MCP_Rhino/Build-McpRhinoPackage.ps1 -OutputRoot Project_Test/261010_TEST_offline-copy-edit/artifacts`. The bundle must include `MCP_Rhino.Offline.dll`, `Rhino3dm.dll`, `Jint.dll` and native Rhino3dm runtime assets in `bin`, and no Rhino3dm dependency in `Plugin`. The published Router's `--validate-install` exercises an in-memory script and model serialization. Staging does not install or modify the production Rhino registration.

Primary dependency references: [Rhino3dm standalone usage](https://developer.rhino3d.com/en/guides/opennurbs/what-is-rhino3dmio/), [Jint interpreter and constraints](https://github.com/sebastienros/jint).

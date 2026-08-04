# Rhino Reference Resources EXET

## Corresponding Plan

- Plan: `Project_Plan/260507_PLAN_rhino-reference-resources.md`
- Execution date: 2026-05-08

## Related Artifacts

- Test folder: `Project_Test/260507_TEST_rhino-reference-resources/`
- Smoke test source: `Project_Test/260507_TEST_rhino-reference-resources/DeveloperCommandHandler.RhinoReferenceResourcesSmokeTest.cs`
- Existing safety smoke updated: `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`
- Commit / PR: none in this workspace execution

## Execution Result / Actual Scope

Implemented a reference-only MCP surface for Rhino and MCP_Rhino tool discovery.

Production additions:

- `src/MCP_Rhino.Server/Contracts/Responses/RhinoReferenceResponses.cs`
- `src/MCP_Rhino.Server/Infrastructure/Reference/RhinoReferenceIndex.cs`
- `src/MCP_Rhino.Server/Infrastructure/Reference/McpRhinoToolHelpIndex.cs`
- `src/MCP_Rhino.Server/Resources/RhinoReferenceResource.cs`
- `src/MCP_Rhino.Server/Resources/McpRhinoToolHelpResource.cs`
- `src/MCP_Rhino.Server/Tools/Reference/ListRhinoReferenceModulesTool.cs`
- `src/MCP_Rhino.Server/Tools/Reference/SearchRhinoReferenceTool.cs`
- `src/MCP_Rhino.Server/Tools/Reference/GetRhinoReferenceFunctionTool.cs`
- `src/MCP_Rhino.Server/Tools/Reference/GetMcpRhinoToolHelpTool.cs`

Resource URIs implemented:

- `rhino-reference://modules`
- `rhino-reference://module/{moduleName}`
- `rhino-reference://function/{functionName}`
- `mcp-rhino-tools://families`
- `mcp-rhino-tools://tool/{toolName}`

Fallback MCP tools implemented:

- `ListRhinoReferenceModules`
- `SearchRhinoReference`
- `GetRhinoReferenceFunction`
- `GetMcpRhinoToolHelp`

All fallback tools are annotated as `ReadOnly = true`, `Destructive = false`, `OpenWorld = false`.

The reference source is a small curated internal index. It does not copy external Rhino documentation text and does not execute RhinoScript, C#, macros, Rhino commands, or arbitrary shell commands.

## Deviations From Plan

- Implemented both MCP resources and read-only fallback tools. The fallback tools were added because MCP resource visibility can vary by client, while the tool surface is already validated by existing inventory and safety smokes.
- Used a curated internal RhinoCommon-oriented reference snapshot instead of generating or copying a full external documentation corpus. This avoids licensing ambiguity and keeps responses bounded.
- Generated MCP_Rhino tool help from reflected `[McpServerTool]` metadata instead of maintaining a separate static tool catalog.

## Issues Found And Fixed

- No compile or smoke-test failures were encountered during final validation.
- The existing safety expectation smoke was updated to include the four new `Reference` tools, keeping the explicit annotation contract enforced.

## Test Record

Debug build:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

Result: passed with 0 warnings and 0 errors.

Debug reference smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- rhino-reference-resources-smoke-test
```

Result:

- `[OK] rhino-reference-resources smoke verified curated modules, function lookup, missing-function handling, generated tool help, and MCP resource members.`
- `[OK] Reference modules=6; resourceMembers=5; toolFamilies=17.`

Debug safety smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result:

- `[OK] MCP safety annotations verified for 132 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`

Debug surface governance smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-surface-structure-governance-smoke-test
```

Result:

- `[OK] MCP tool inventory discovered 132 tools.`
- `[INFO] Tool family Reference: 4`
- `[OK] MCP resource inventory discovered 5 resources.`

Release build:

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

Result: passed with 0 warnings and 0 errors.

Release reference smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-reference-resources-smoke-test
```

Result:

- `[OK] rhino-reference-resources smoke verified curated modules, function lookup, missing-function handling, generated tool help, and MCP resource members.`
- `[OK] Reference modules=6; resourceMembers=5; toolFamilies=17.`

Release safety smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result:

- `[OK] MCP safety annotations verified for 132 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`

Release surface governance smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-surface-structure-governance-smoke-test
```

Result:

- `[OK] MCP tool inventory discovered 132 tools.`
- `[INFO] Tool family Reference: 4`
- `[OK] MCP resource inventory discovered 5 resources.`

Repository check:

```powershell
git diff --check
```

Result: passed with only existing line-ending normalization warnings.

## Acceptance Criteria Alignment

- Reference capability is read-only and does not access or mutate live Rhino documents: met.
- MCP resources use the structure-governance `Resources/` layer and `ResourceRegistration`: met.
- Tool fallback has explicit read-only safety annotations: met.
- Reference lookup responses are bounded by `limit` parameters and curated detail payloads: met.
- Licensing/source of reference data is documented: met as curated internal reference, no external documentation copied.
- Debug and Release builds pass: met.
- Debug and Release MCP safety smokes pass: met.
- Smoke demonstrates module listing, function lookup, missing-function handling, generated tool help, MCP resource members, and no live Rhino dependency: met.

## Rollback Verification

Rollback can remove the new `Infrastructure/Reference`, `Resources/*Reference*`, `Tools/Reference`, and response DTO files added by this execution, then remove the reference smoke hook from `DeveloperCommandHandler` and the four tools from the safety expectation smoke.

No live Rhino document behavior, Undo behavior, external file export path, or arbitrary execution capability was introduced.

## Current Remaining Items

- Validate actual MCP resource visibility in each target MCP client path: bridge client, Claude/Codex client, and panel-bound client. The CLI reflection smoke verifies server registration but does not prove every client renders resources equally.
- Expand the curated reference set over time only with explicit source/licensing review.

## Conclusion

`260507_PLAN_rhino-reference-resources.md` is executed. MCP_Rhino now exposes safe reference-only resources plus four read-only fallback tools for Rhino reference search and MCP_Rhino tool help, with Debug and Release validation passing.

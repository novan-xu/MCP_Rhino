# Rhino Reference Resources Plan

## Background

The `rhinomcp` project exposes RhinoScript documentation through both MCP tools and MCP resources. This helps agents avoid guessing function names and signatures before writing script code. MCP_Rhino currently does not expose MCP resources and does not expose arbitrary script execution tools.

For MCP_Rhino, the safe lesson is to provide reference information without adding a general "execute arbitrary RhinoScript or C# code" capability by default. Static reference resources and search tools can help users and agents understand Rhino APIs, but arbitrary execution would bypass the repository's atomic tool model and needs a separate explicit policy decision.

This plan depends on `260507_PLAN_mcp-surface-structure-governance` if a new `Resources/` layer is accepted. That structure plan owns `ResourceRegistration`, `Resources/` directory rules, and resource-vs-tool workflow rules. This plan owns only the reference content/providers or read-only tool fallback.

## Goal

Add safe Rhino reference discovery without adding arbitrary code execution.

Target capabilities:

- browse RhinoScript/RhinoCommon reference modules through MCP resources if supported
- search reference functions by keyword
- get focused function documentation by name
- optionally expose repository tool help generated from MCP tool metadata
- keep all reference capabilities read-only and non-mutating

## Architecture Ownership

- `Resources/`: target home for MCP resource providers only after the structure governance plan has created and documented that layer.
- `Tools/Reference`: optional home for searchable reference tools if MCP resource UX is insufficient across clients.
- `Infrastructure/Reference`: loaders/indexers for static reference data.
- `Contracts/Responses`: response DTOs for search results and function details.
- `Project_Guides/MCP_Rhino Architecture.md`: not owned by this plan unless this plan is executed together with the structure governance plan.
- `Runtime_Workflow/MCP_Rhino Workflow.md`: not owned by this plan unless this plan is executed together with the structure governance plan.

## Key Design

### 1. Reference Only

The first implementation must not execute RhinoScript, C#, macros, or arbitrary commands. It only exposes reference documentation.

### 2. Resource URIs

If the .NET MCP SDK supports resources cleanly, provide URIs such as:

- `rhino-reference://modules`
- `rhino-reference://module/{moduleName}`
- `rhino-reference://function/{functionName}`
- `mcp-rhino-tools://families`
- `mcp-rhino-tools://tool/{toolName}`

If resource support is not reliable for target clients, implement searchable read-only tools instead and record the SDK/client limitation in EXET.

### 3. Static Data Source

Prefer a generated local reference snapshot committed under a clearly named path only if licensing permits it. Otherwise, use small curated internal docs that describe supported MCP_Rhino tools rather than copying large external docs.

Do not bulk-copy external copyrighted documentation into the repository without license review.

### 4. Search Tools

Optional tools:

- `SearchRhinoReferenceTool`
- `GetRhinoReferenceFunctionTool`
- `ListRhinoReferenceModulesTool`
- `GetMcpRhinoToolHelpTool`

Safety:

- `ReadOnly = true`
- `Destructive = false`
- `OpenWorld = false`

### 5. Arbitrary Execution Is Explicitly Out Of Scope

Do not add:

- `ExecuteRhinoScriptPythonCodeTool`
- `ExecuteRhinoCommonCSharpCodeTool`
- macro execution
- command-line execution inside Rhino

Those would require a separate plan with explicit safety UX, approval behavior, sandboxing discussion, and likely `OpenWorld`/destructive treatment.

## Involved Files

Precondition if resources are accepted:

- `260507_PLAN_mcp-surface-structure-governance` has executed the `ResourceRegistration` and rule-layer work.

Likely production files after that precondition:

- `src/MCP_Rhino.Server/Resources/RhinoReferenceResource.cs`
- `src/MCP_Rhino.Server/Resources/McpRhinoToolHelpResource.cs`
- `src/MCP_Rhino.Server/Infrastructure/Reference/RhinoReferenceIndex.cs`
- `src/MCP_Rhino.Server/Infrastructure/Reference/RhinoReferenceLoader.cs`

Likely production files if tool fallback is chosen:

- `src/MCP_Rhino.Server/Tools/Reference/SearchRhinoReferenceTool.cs`
- `src/MCP_Rhino.Server/Tools/Reference/GetRhinoReferenceFunctionTool.cs`
- `src/MCP_Rhino.Server/Tools/Reference/ListRhinoReferenceModulesTool.cs`
- `src/MCP_Rhino.Server/Tools/Reference/GetMcpRhinoToolHelpTool.cs`

Required test artifacts if executed:

- `Project_Test/260507_TEST_rhino-reference-resources/`

Required safety update if tools are added:

- `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`

## Usage

Expected runtime behavior:

- Use reference resources/tools when a user asks what Rhino operations are available.
- Use MCP_Rhino generated tool help when choosing among existing MCP tools.
- When a user asks to run arbitrary script code, report the capability gap unless a future execution capability has been explicitly built.

## Acceptance Criteria

- Reference capability is read-only and does not access or mutate live Rhino documents.
- If resources are used, the structure governance plan has already documented resource ownership and registration.
- Tool fallback, if added, has explicit read-only safety annotations.
- Reference lookup responses are bounded and do not dump large documentation blobs.
- Licensing/source of reference data is documented.
- `dotnet build .\MCP_Rhino.sln -c Debug` passes.
- `dotnet build .\MCP_Rhino.sln -c Release` passes.
- If tool fallback is added, `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test` passes.
- If tool fallback is added, `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test` passes.
- Resource/tool smoke demonstrates module listing, function lookup, missing-function handling, and no live Rhino dependency.

## Risks And Rollback

- Risk: MCP resources may not be consistently visible in all target clients.
  - Mitigation: validate with bridge, Claude Code, and panel-bound client paths; fall back to read-only tools if needed.
- Risk: reference docs can drift from actual Rhino versions.
  - Mitigation: document Rhino version source and expose version metadata.
- Risk: generated tool help duplicates descriptions.
  - Mitigation: generate from reflection rather than manually maintaining separate help text.

Rollback removes reference resources, reference loaders, and reference tools. Resource registration and rule-layer changes are rolled back only through the structure governance capability if they were implemented there. Runtime Rhino tools remain unaffected.

## Future Extension

- Add versioned reference snapshots for Rhino 7 and Rhino 8 if licensing permits.
- Add examples tailored to MCP_Rhino tool usage instead of raw RhinoScript execution.
- Consider an arbitrary script execution capability only after explicit user approval and a separate safety plan.

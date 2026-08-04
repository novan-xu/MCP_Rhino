# Runtime Text Normalization Plan

## Background

The follow-up review found Chinese runtime-facing strings that still affect MCP argument construction, tool responses, failure recovery, and CLI smoke diagnostics:

- request DTO `[Description]` attributes for object user text and document user string entries
- `PassThroughEditResultFormatter` labels returned through MCP preview/apply/create formatting paths
- `LiveRhinoGeometryValidator` failure and warning messages returned by geometry creation, transform, replace, and control-point edit flows
- legacy `DeveloperCommandHandler` and parsing errors used by CLI smoke/fallback commands

The previous review-finding fix normalized top-level tool descriptions and some stale workflow text, but did not cover these deeper runtime text surfaces.

## Goals

- Convert targeted runtime-facing C# strings to clear English.
- Keep behavior, contracts, tool names, and response shapes unchanged.
- Add static smoke coverage that prevents CJK text from returning to the targeted runtime surfaces.
- Avoid changing prompt files, README documentation, architecture guides, plan logs, or other intentionally bilingual content.

## Architecture Ownership

- Request DTO descriptions stay in `Contracts/Requests`.
- Formatter output stays in `Infrastructure/Rhino/PassThroughEditResultFormatter.cs`.
- Rhino geometry validation messages stay in `Infrastructure/Rhino/Live/LiveRhinoGeometryValidator.cs`.
- CLI user-facing fallback messages stay in `Infrastructure/CLI`.
- Regression coverage belongs in `Project_Test/260508_TEST_runtime-text-normalization/`.

## Key Design

- Apply direct English replacements only in the reported runtime-facing files.
- Add a smoke command `runtime-text-normalization-smoke-test` that checks:
  - no CJK characters in active MCP tool method descriptions
  - no CJK characters in `[Description]` attributes under `Contracts/Requests`
  - no CJK characters in targeted formatter, validator, CLI handler, and CLI parser source files
  - no CJK characters in reflected `OperationResponse.Fail(...)` messages is approximated by targeted source scanning for the validator file
- Keep documentation and prompt files out of this smoke to avoid blocking intentional bilingual project guidance.

## Involved Files

- `src/MCP_Rhino.Server/Contracts/Requests/ObjectScopedUserTextKeyRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ObjectScopedUserTextEntryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/DocumentUserStringEntryRequest.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/PassThroughEditResultFormatter.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryValidator.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.Parsing.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `Project_Test/260508_TEST_runtime-text-normalization/`

## Usage

No runtime API change is expected. The new developer smoke is:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- runtime-text-normalization-smoke-test
```

## Acceptance Criteria

- Debug and Release solution builds pass.
- New runtime text normalization smoke passes in Debug and Release.
- Existing review-finding, MCP safety, overlap cleanup, and surface governance smokes continue to pass.
- Targeted runtime-facing C# files no longer contain CJK text.

## Risks And Rollback

- Translation changes can alter exact user-facing text expected by informal workflows. The smoke focuses on source content, not message wording, so behavior remains stable.
- If an intentional CJK runtime string is needed later, add a small explicit allowlist with a code comment explaining why.
- Rollback is a normal git revert of this plan's touched files and artifacts.

## Future Extensions

- Move user-facing strings into a central localization/resource mechanism if multi-language runtime output becomes a real product requirement.
- Add live MCP response snapshots for the formatter and validator once stable fixtures exist.

# Project_Test

Active standalone validation assets. Create tests when they provide useful coverage; a change does
not automatically need its own test folder. Prefer extending an existing suite when appropriate.

## Ownership and evidence

- Folder: `YYMMDD_TEST_<capability-name>/`, matching the owning work item's EXET and PLAN when present.
- Link commands, results, and limitations from EXET. Do not create placeholder folders or duplicate
  reports to satisfy a three-artifact checklist.
- Follow the [change-record guide](../Project_Guides/MCP_Rhino%20Plan%20Log.md) for record levels,
  archive eligibility, and the detailed live smoke CLI registration contract.
- Keep local `.3dm` fixtures under ignored `Runtime_Test/local/`; use anonymous relative paths in records.
- Mutation smoke tests must restore their disposable fixture baseline and verify that restoration.
  Read-only tests should verify relevant state remains unchanged.

## Build and execution

The [Server project](../src/MCP_Rhino.Server/MCP_Rhino.Server.csproj) includes supported smoke code
from both `Project_Test/**/*.cs` and `Project_Archive/Project_Test/**/*.cs`. Exact exclusions keep
retired suites and standalone projects out of the Server build. Generated `bin`/`obj` code is excluded.
Archival must preserve these semantics and repair project references or script roots when needed.

Server CLI smoke extensions use `MCP_Rhino.Server.Infrastructure.CLI` and the existing partial
`DeveloperCommandHandler` registration hooks. Each new live smoke owns a unique CLI slug and
Rhino command; only the main handler implements `RegisterExtensionHandlers()`. A documentation
change does not need a CLI slug or Rhino command.

For live smoke tests, keep the fixture path as the main input with clear usage on missing inputs.
Report useful checkpoints and exit `0` only when all required assertions pass; otherwise exit
nonzero. Cover the successful path and relevant invalid-input/failure cases, reuse assertion
helpers, and verify state preservation or restoration. Test instructions must identify the
required host and fixture; static CLI checks must not be presented as live acceptance.

Record actual commands and exit codes. State whether a result proves static/build behavior or
live Rhino behavior; an unrun host acceptance test is not a pass. Follow the Architecture guide
for required Debug/Release, safety, identity, and installation validation.

## Finding historical tests

Completed eligible suites live under `Project_Archive/Project_Test/`. Search both test roots and
read the owning EXET for scope, commands, and limits. The current archive manifest links moved
work items and explains which older sets remain active.

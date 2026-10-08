# PCMatchCrv extrusion assignments EXET

## Corresponding plan

[PLAN](../Project_Plan/261007_PLAN_pcmatchcrv-extrusion-assignments.md).
Execution date: 2026-10-07.

## Related artifacts

[TEST and runner](../Project_Test/261007_TEST_pcmatchcrv-extrusion-assignments/README.md).
The existing curve-topology smoke owns the new assertions. No commit or PR was requested.

## Execution result and scope

The standalone Application curve-match planner now transfers normalized extrusion
assignments with the existing sparse segment/merge/hide masks. It carries complete
profile definitions, formulas, quantities, parent references and curve modifiers.
Empty source assignments remove target assignment/typology keys. Nonempty source
assignments replace target data and regenerate frame typology from each target's
dimensions, offsets and layer system code.

The Domain match snapshot now carries SystemCode. The live repository derives it
from the layer leaf using the existing layout-read convention. PCMatchCrv prompts
and result text describe the expanded behavior; package usage documentation was updated.
The existing live preflight, commit validation, rollback and Undo path was retained.
Target cladding, dimensions, offsets and unrelated metadata remain outside the transfer.

## Deviations from plan

None. Verification reuses the existing smoke projects and records their output in
the matching TEST folder. No new MCP endpoint, developer smoke command or server
dependency was needed. Deployment remains separate.

## Problems found and fixed

The old planner copied masks only, leaving extrusion assignments attached to the
targets' previous configuration. Copying those assignments also requires replacing
the old frame typology; directly copying the source's typology would be wrong when
target geometry or system differs. Both are now planned together before writes.

## Test record

Executed `Verify-Regression.ps1` in the linked TEST folder, exit 0:

| Check | Debug | Release |
| --- | --- | --- |
| Curve topology and new assignment matching smoke | Pass | Pass |
| Hide-mask smoke | Pass | Pass |
| Surface-match smoke | Pass | Pass |
| Extrusion assignment/formula smoke | Pass | Pass |
| Standalone PanelCladdingEditor build | 0 warnings/errors | 0 warnings/errors |

Each smoke runs with `dotnet run --project <existing smoke project> -c <configuration>`;
builds use `dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c <configuration> --nologo`.
The runner contains exact project paths and stores all ten logs beside its README.
`git diff --check` passed. Full MCP solution builds were unnecessary because the
change is confined to the independent editor product and does not change the MCP
server, plugin host, Router, tool registration or routing lifecycle.

## Acceptance alignment

The regression covers two targets with different dimensions/system codes, complete
assignment round-trip, assigned hidden/merged segments, additive codes, parent
metadata and curve formulas generated against target dimensions. It checks old
assignment replacement, empty-source clearing, legacy payload normalization,
case-insensitive key handling, unrelated metadata preservation, source immutability
and idempotence. Malformed payloads and invalid merged assignments fail preflight;
a bad later target produces no partial plan. Existing equal-grid guards still pass.

## Rollback verification

Production documents and installed binaries were not modified. Runtime attribute
commit/rollback code is unchanged. The planner produces no partial result on
failure, verified by the multi-target invalid-payload fixture. Reverting this
task's planner, snapshot, repository and command edits restores the prior behavior
without a schema migration. Live Rhino Undo was not exercised.

## Remaining items

Installation and live Rhino command/PCEditor/Undo acceptance have not been performed
for this change. Existing managed curves are updated through PCUpdate, or new curves
through PCSpawnCrv, after matching. No workbook synchronization was introduced.

## Conclusion

PCMatchCrv now matches saved extrusion assignments along with curve topology.
Debug/Release regressions and builds passed; the change is ready for packaging.

## Follow-up installation (2026-10-07)

Installed in version 1.0.96 together with the user's subsequent frame-attribute
redesign. The final schema uses CW_1.08_FRAME_CONFIG and CW_1.09_FRAME_TYPE and
retires the old frame-typology identifier. Installation and independent validation
evidence is recorded in
[the frame-attribute EXET](261007_EXET_frame-config-type-attributes.md).

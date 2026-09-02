# PCMatchSrf Repair Existing Targets PLAN

## Background

After the first incomplete match, a target panel retains nonblank owner-cell assignments but is missing parent references such as `1A=0A`. `PCMatchSrf` currently rejects every target containing any nonblank cladding cell before it builds a repair plan. Consequently, rerunning the corrected matcher cannot repair the partially configured target.

The command also reports success immediately after `ModifyAttributes` without reading the committed attributes back. A successful command should guarantee that every planned parent/material value exists exactly and that obsolete target-only cell keys are gone.

## Goal

- Allow `PCMatchSrf` to overwrite the cladding-cell assignment set of an explicitly selected compatible target.
- Preserve all target non-cell attributes, including H/V offsets, segment/merge masks, type/signature, PID/CID, and unrelated user text.
- Verify committed target cell attributes before reporting success.
- Roll back the complete batch if any planned parent/material write is missing or incorrect after commit.

## Architecture ownership

- Overwrite planning and pure postcondition validation belong to `PanelCladdingMatchPlanningService` in Application.
- Rhino attribute read-back, batch rollback, Undo, and redraw remain in `LivePanelCladdingMatchService` in Infrastructure.
- Regression coverage belongs in `Project_Test/260819_TEST_pcmatch-srf-repair-existing-targets/`.

## Key design

1. Remove the populated-target eligibility rejection. Target selection is explicit and the operation remains one Rhino Undo record.
2. Continue deleting only target cladding-cell keys and writing only the source-projected cladding-cell set.
3. Keep the existing cell-code compatibility check; an incompatible row/column grid still fails before mutation.
4. Add a pure validator that requires every planned write to be present with the exact value and every delete not superseded by a write to be absent.
5. After each Rhino `ModifyAttributes`, re-read the object attributes and run that validator.
6. If verification fails, restore the current object and every prior target from their duplicated original attributes before returning failure.

## Files involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingMatchService.cs`
- `Project_Test/260805_TEST_panel-cladding-match/Program.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Project_Test/260819_TEST_pcmatch-srf-repair-existing-targets/`
- `Packaging/PanelCladdingEditor/package-manifest.json`

## Usage

Run `PCMatchSrf`, select one or more compatible targets—including partially or fully assigned targets—and then select the source. The command replaces only target cladding-cell assignments and leaves every non-cell attribute untouched.

## Acceptance criteria

- A target containing owner values but missing `1A=0A` can be matched again and receives the parent value.
- Existing target cladding values not present in the source projection are removed.
- Target offsets, masks, type/signature, identity, and unrelated metadata remain unchanged.
- Postcondition validation fails when a planned parent value is missing or changed.
- A verification failure rolls back the complete batch and returns failure rather than a success message.
- Existing parent-fidelity, logical-cell cleanup, topology-mask, and base-match regressions pass.
- Debug/Release solution builds, package creation, identity validation, and staged/install validation pass.

## Risks and rollback

- PCMatchSrf becomes an overwrite operation for cladding cells. This is explicit in the target/source selection flow and remains recoverable through one Rhino Undo entry.
- Roll back by restoring the populated-target guard and removing post-commit verification, then reinstalling package `1.0.52`.

## Future extensions

- Add a preview count of target cell keys to be overwritten before source selection completes.
- Show the exact repaired parent/material cell count in the Rhino command result.

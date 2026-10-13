# Nested stone outlines: execution

## Corresponding plan

[PLAN](../Project_Plan/261010_PLAN_stone-bake-nested-outlines.md). Executed 2026-10-10 after the user explicitly authorized extending the script and finishing row 6.

## Related artifacts

[TEST and reproduction](../Project_Test/261010_TEST_stone-bake-nested-outlines/README.md), `NativeChecks.csx`, `RunNativeChecks.csx`, and `ValidateBake.csx`. No commit/PR requested or created.

## Result and delivered scope

Updated the authoritative linked script at `V:/01 Project Folders/P00012 Met Tang/03-Design-Eng/03-BIM/05-Wireframe/01_grasshopper/05_STN/StoneSurfaceBake.cs`.

Outline collection now follows nested block definitions and composes transforms into the parent stone's coordinate system. Curves are duplicated before transforming and segment classification, so a rotated child can contribute an edge that is horizontal in stone coordinates. The existing highest-local-Y, longest-edge selection and ambiguity rejection remain. The recursion stack is scoped to each path, permitting reused shared definitions. Missing definitions, singular/invalid transforms, cycles, and depth of 24 or greater fail before bake writes.

Missing-only assembly filtering still occurs before traversal. No existing output definition is rewritten. Inputs and public Grasshopper/headless interfaces are unchanged.

Finished the failed row 6 drawing through the existing Router native offline preview/apply tools. Published one new copy and moved only that verified new file to the requested processed folder:

`V:/01 Project Folders/P00012 Met Tang/03-Design-Eng/03-BIM/05-Wireframe/09_reference/STN_srf/processed/FP-102_ROW 6_FIN STONE_20261010_235137.3dm`

The result has 130 fins and 115 flats in 115 new assembly instances. Existing family/visibility rules left 40 source assemblies without eligible stones unbaked; their geometry was preserved. All 18 batch drawings are now processed. The final saved totals are 796 fins, 761 flats, and 266 lintels, in 1,021 assembly instances.

## Deviations from plan

None in delivery scope. Native cycle rejection was tested by seeding the active traversal stack; the depth guard was tested with a real nested-definition chain. This does not claim construction of a corrupt cyclic Rhino definition. No MCP implementation was changed, so standalone Debug/Release script validation was used rather than rebuilding the solution or RHPs.

## Problems found and resolved

The row 6 flat stone STN-MET-1050S-002 contained its outline in FS-2X1ft 1in_Plan, leaving the old direct-curve collector empty. Recursive transformed collection resolves it. During test setup, two combined test snippets used the same local variable name; the test fixture variable was renamed before native execution passed. No model output was published by the failed test compilation.

## Test record

See TEST README for reproducible commands. Both managed Debug and Release runs exited 0. The native preview passed all 12 fixture assertions and the actual drawing bake/preservation/idempotence checks. Production resume command:

```powershell
dotnet run --project '.validation/stn-srf-batch/Batch.csproj' -c Release -- 'FP-102_ROW 6_FIN STONE.dwg'
dotnet run --project '.validation/stn-srf-batch/Verify.csproj' -c Release
```

Both exited 0. Production row 6 persisted 28,497 total file objects versus 28,299 imported objects; 831 definitions total, 86 layers, inch units. All 28,299 source objects retained geometry CRCs and full attributes. The repeated bake added nothing. The saved row 6 SHA256 is `CD2E9958F3031C1E36482A39A98F1BF24E66664E598910AFE58FA30CF4938B18`.

Final verification reopened all 18 saved files using Rhino3dm and checked units, source occurrence identity, STN_ID/Assembly_ID, direct Brep members, family counts, and workbook baseline/height values within 1e-6 inches. All raw DWG hashes, workbook hash, and prior 17 output hashes remained unchanged. The workbook mapping independently verified 44 arithmetic formulas against cached values.

Workbook SHA256: `D1D403F577A6C2D4F027B3431824E862672847FDCCEB7FCC2246C7CC1BB2A501`.

## Acceptance alignment

Nested outline support is delivered and tested; row 6 completed; all 18 outputs are verified in processed with short local date/time suffixes. Original drawings and spreadsheet were not overwritten. No original source definition or completed result was replaced by the bake.

## Rollback verification

A create-only sibling backup `StoneSurfaceBake_20261010_235130.cs` was hash-verified before replacing the linked script. Its SHA256 is `9EB14EFD7BF35EE7B9A4B2E1942FCBBFB00E8E4B5AE0ACB4373ED04862988E94`. The deployed script matches tested SHA256 `E412BE542D1899DE472B93A14B6E08EBDAF4777D18D187FFD3AE57A7B237FB20`. Restoring that backup reverts the extension; rollback was not executed. All output models are new independent copies.

## Remaining items

No outstanding work for this batch. Live Grasshopper solve/Undo was not executed. The Runtime activity-log tool was absent from the Router tool inventory, so no activity record was written through a substitute path. No plugin installation, activation, registry write, or package version change was attempted.

## Conclusion

The linked script handles nested stone outlines, and row 6 is complete. All 18 processed model copies pass saved-file verification with original inputs preserved.

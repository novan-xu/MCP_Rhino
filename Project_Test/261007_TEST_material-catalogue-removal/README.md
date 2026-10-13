# Material catalogue removal verification

Run from the repository root:

```powershell
./Project_Test/261007_TEST_material-catalogue-removal/Verify-Regression.ps1
```

The script runs the existing material catalogue editing smoke with its new removal
checks in Debug and Release, then builds the standalone editor RHP in both
configurations. An optional output directory argument keeps rendered images in
this TEST folder instead of overwriting earlier evidence.

Coverage includes selection/edit/add/category regressions, enabled state when
selection changes, selected-only removal, stale field clearing, repeated removal
without selection, tile width recalculation, source-copy isolation, and reduced
and empty workbook round trips. The workbook uses a uniquely named temporary file
and is deleted afterward. Persistence checks explicitly invoke the existing
repository with the same material payload used by Confirm; they do not automate
the modal Confirm/Cancel window.

The WPF test measures and renders controls in process without showing a window or
automating Windows/Rhino. The 720x740 and 620x640 images show a 15-material fixture
including a long code to exercise wrapping and catalogue scrolling.

Results: all checks passed in Debug and Release; both RHP builds completed with
zero warnings and errors. Both removal renders were visually inspected. Logs and
PNGs are alongside this README. Live Rhino interaction was not performed.

## Authorized installation

The user's follow-up requested installation. Package 1.0.94 was built, its direct
assembly GUID and all 27 bundle hashes verified, and the supported installer run
from a hidden host process after independent registry nonce attestation. The first
ordinary child-process probe was virtualized; `activation-isolated-probe.log`
preserves that evidence and no installation was attempted in that context.

`Verify-Activation.ps1` checks the saved host before/install/post-exit Validate
records, live Windows registry provider agreement, all three advanced registry
timestamps, exact commands, the installed RHP hash, and the prior RHP rollback hash.
It passed with Rhino closed. See `activation-summary.json`, `host-persistence.json`
and `package-summary.json`. The existing
`Project_Test/260930_TEST_panel-cladding-type-suspension/Host-PanelCladdingActivation.ps1`
was used with the explicit new BundleRoot for Probe, Install and Validate.

Post-start loaded-module and registry ownership checks remain pending the next
Rhino launch. This verification script is specific to this completed activation;
later activations or Rhino loads may legitimately change the compared timestamps.

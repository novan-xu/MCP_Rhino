# Extrusion catalogue clear verification

Run from the repository root:

```powershell
./Project_Test/261007_TEST_extrusion-catalogue-clear/Verify-Regression.ps1
```

The script runs the existing extrusion assignment smoke with `--clear-qa` in
Debug and Release, then builds the standalone editor RHP in each configuration.
This focused mode does not read a real PDF or depend on the optional schedule
environment variable. It uses synthetic profile PNGs, a three-profile fixture
(one unconfigured, two configured with a parent dependency), a temporary workbook,
an injected confirmation prompt and a fake schedule importer.

Coverage:

- Declining the prompt retains both lists, selection, editor values and PDF path.
- Accepting removes all entries, resets editor controls, preview, parent options,
  counts and PDF path, and retains the selected workbook.
- The caller's source and saved workbook are unchanged before explicit save.
- An empty catalogue persists through the controller/repository, and reopening it
  clears even a stale PDF path supplied by the caller.
- Clear also works when only a PDF path remains; another extraction starts fresh
  even when the imported source code was configured before clearing.
- Existing assignment, formula, typology and source-contract regressions pass.
- Footer bounds are checked at 1220x820 and 1040x700.

All checks passed in both configurations; both standalone RHP builds completed
with zero warnings/errors. Logs and three offscreen renders are in this folder.
The renders were visually inspected. The detached content uses the dialog's
normal Window background; a Debug rerun regenerated images after this QA fix.

No Windows UI automation or Rhino document changes occurred. The production
confirmation is an owner-bound WPF Yes/No MessageBox with No as its default;
tests inject the response instead of showing a modal window. Main Confirm/Cancel
is not automated; persistence is exercised through its existing controller save
path and working-copy isolation. Real PDF import and installed Rhino interaction
were not rerun for this UI change.

## Authorized installation

The user's subsequent installation request was completed as 1.0.95. Package
identity and 27 file hashes passed; installer Install/Validate and a separate
post-exit host Validate passed with Rhino closed.

`Verify-Activation.ps1` verifies the saved host records, installed RHP/hash, exact
12 command values, three advanced registry timestamps, independent Windows
registry provider agreement and retained 1.0.94 rollback RHP hash. It exited 0.
See `package-summary.json`, `host-persistence.json` and `activation-summary.json`.

The hidden host mechanism was previously write-attested in this chat's material
removal installation and freshly checked against the host/provider before this
activation. No new probe key or cleanup retry was needed. The existing
`Project_Test/260930_TEST_panel-cladding-type-suspension/Host-PanelCladdingActivation.ps1`
was used with the explicit 1.0.95 BundleRoot for Snapshot, Install and Validate.
Post-start loaded-module/registry ownership checks await the next Rhino launch.

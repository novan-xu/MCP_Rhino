# EXET — Panel extrusion configuration

Date: 2026-08-20

Plan: `Project_Plan/260820_PLAN_panel-extrusion-configuration.md`

## Execution

- Removed the framing-only PDF filter. The importer now processes every schedule page, shortens
  `ALU-H0579` to the base code `H0579`, and retains page-derived categories such as `FRAMING`,
  `HOLLOW`, `HOLLOW (SMALL SIZE)`, `SOLID`, `SOLID (SMALL SIZE)`, and `ASSEMBLIES`.
- Reworked the setup window into category-grouped unconfigured-pool and ready-catalogue panes.
  Profiles move to ready only after selecting 1D or 0D and completing their calculation settings.
- Extended the workbook `Extrusions` sheet to retain pool/ready state, base code, category,
  dimension, calculation mode/value, optional parent, source metadata, and thumbnail bytes. Old
  six-column catalogues migrate to shortened 1D codes when read.
- Added 1D quantity, 0D fixed-quantity, and 0D spacing configuration. Added optional parent
  dependencies which are expanded only when the child is newly assigned; a manually removed
  dependency is not reintroduced while the child remains assigned.
- Added per-curve length modifiers and version-2 assignment persistence. The schema stores the
  assigned profile definitions and stable curve modifiers while decoding version-1 payloads.
- Added direct take-off attributes on baked curves. Examples include `(LL+4)*2`, `LL+4`, `2`, and
  `(LL+4)/24`. The compatibility `Extrusions` summary remains present.
- Updated `PCSyncCrv` planning and commit behavior to compare, replace, and remove direct `0D-*`
  and `1D-*` attributes along with the compatibility summary.
- Bumped the package definition from `1.0.62` to `1.0.63`.

## Verification

- The real six-page BayHealth schedule imported 38 profiles from pages 1–6 across all six expected
  categories.
- Workbook round-trip retained 34 unconfigured profiles and four configured 1D/0D profiles,
  including quantity, parent, fixed-count, and spacing settings.
- Formula, schema-v2, typology, baked-curve, sync-planning, sparse-topology, curve-topology,
  full-track-collapse, scoped-save, spawn/sync, and WPF rendering checks passed.
- Off-screen 1220×820 setup and 1440×900 main-editor renders were visually inspected.

## Packaging outcome

- Built `PanelCladdingEditor` version `1.0.63` with zero warnings and errors.
- Verified the compiled RHP's assembly-level plug-in GUID directly as
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`.
- Registry-only installation/validation/tamper-rejection/uninstall smoke passed.
- The packaged native-runtime probe constructed Skia, loaded HarfBuzz, and imported all 38
  profiles from the supplied six-page PDF.
- Four active Rhino processes prevented live replacement. The current bundle was staged at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.63-20260820214413563` for activation
  after every Rhino process exits.
- After Rhino exited, `1.0.63` was activated registry-only at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.63`. Installed-path validation and
  the native/PDF probe passed, including all 38 profiles from the six-page schedule.

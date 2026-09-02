# Panel Cladding Scoped Save Test Results

Date: 2026-08-20

## Focused smoke

- Debug: PASS
- Release: PASS

Validated:

- absent masks decode as the all-present, all-segmented, all-visible default;
- extrusion planning accepts that sparse default state;
- cladding-only save does not write/delete masks or offsets;
- extrusion-only save writes offsets without redundant default masks or cladding changes;
- Save Both includes both scopes while retaining sparse default topology;
- the editor footer contains `Save Extrusions`, `Save Cladding`, and `Save Both`, has no `Exit`, and
  keeps default masks absent;
- an extrusion-only save refreshes the live fingerprint so a subsequent cladding-only save succeeds.

This focused smoke passed in Debug and Release as part of the sparse-topology construction run.

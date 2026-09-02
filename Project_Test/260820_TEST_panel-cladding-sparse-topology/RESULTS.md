# Panel Cladding Sparse Topology Results

Date: 2026-08-20

- Debug: PASS
- Release: PASS

Validated:

- default topology emits no `2.05`-`2.07` writes;
- missing-only, merge-only, and hide-only states emit exactly one corresponding mask;
- hide-only Save Extrusions deletes stale explicit segment/merge defaults and writes only `2.07`;
- absent masks decode, curve-match, and spawn as defaults;
- default curve match deletes stale target masks and writes none;
- one-column H priority and one-row V priority produce no merge mask or signature invalidation;
- `PCCrvTemplate` rejects an existing nonblank merge code;
- surface sync uses sparse persistence while type signatures retain full canonical payloads.

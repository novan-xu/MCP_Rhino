# Panel Cladding Offset Sync Smoke

This folder owns focused regression coverage for geometry-derived H/V offset replacement and the
`04_STEP Surfaces` spawn root plus exact legacy-root read compatibility.

Offsets are canonicalized to five decimal places before grid validation. The smoke verifies
rounding, compact `0.#####` serialization, and fail-closed rejection when two boundaries collapse
to the same five-decimal coordinate.

The standalone smoke must cover canonical key replacement, changed-grid sync planning, STEP layer
acceptance, and rejection of both legacy material roots. A live Rhino fixture should additionally
verify naked-edge extraction and geometry coverage on planar and curved panels.

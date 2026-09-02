# Panel cladding blank-cell persistence plan

## Background

PCEditor Save builds a write entry for each surviving logical cladding cell, including unassigned cells whose value is an empty string. Rhino treats an empty user-string value as removal, so those cladding keys disappear from the panel attributes. The H/V offsets and masks may remain, but the cell structure is no longer directly visible in the panel key/value set.

## Goal

Persist every surviving logical cladding-cell key when PCEditor saves. Assigned cells retain their material or parent-cell value; unassigned cells retain an empty-looking value that Rhino stores instead of deleting.

## Architecture ownership

- `PanelCladdingKeyService` owns the storage representation for an unassigned cladding value.
- `PanelCladdingSaveService` prepares the complete logical-cell write set and applies that representation only at the Rhino persistence boundary.
- Existing readers continue to normalize whitespace-only stored values to the domain value `string.Empty`.
- `ILivePanelCladdingRepository` remains a generic attribute transaction boundary and does not acquire cladding-specific sentinel logic.

## Key design

1. Keep `string.Empty` as the application/domain representation of an unassigned cell.
2. Encode an unassigned cell as one regular space only when assembling Rhino user-text writes. Rhino retains this non-empty string, while the Attributes UI renders it as blank and all current cladding readers trim it back to empty.
3. Write every representative key returned by logical-cell collapse, including blank representatives.
4. Continue deleting only obsolete grid keys outside the current normalized logical layout. Hidden physical members of a merged logical cell remain obsolete and are not recreated.
5. Do not change signature semantics: blank cells remain empty in the canonical identity payload.

## Involved files

- `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260819_TEST_panel-cladding-blank-cell-persistence/`
- `Project_Exet/260819_EXET_panel-cladding-blank-cell-persistence.md`

## Usage

Open a panel in PCEditor, create or edit its extrusion layout, leave any or all cladding cells unassigned, and click Save. Rhino panel attributes retain a `CW_4.xx_CLADDING_*` key for every surviving logical cell; unassigned values appear blank.

## Acceptance criteria

- Saving an unassigned 2x3 layout writes all six logical cell keys.
- Each unassigned cell write uses the Rhino-retained blank representation and reads back as an unassigned value after normalization.
- Mixed assigned, parent, and unassigned cells preserve their respective values.
- Obsolete cells removed by track collapse and hidden physical cells absorbed by segment merging remain in `UserTextDeletes` and are absent from `UserTextWrites`.
- Blank cells remain blank in the type signature rather than becoming a material or parent token.
- Focused Debug/Release tests and the existing logical-cell/full-track/topology regressions pass.
- The packaged plug-in retains its required assembly/plugin GUID identity.

## Risks and rollback

- Risk: a whitespace value could be interpreted as assigned by a reader that does not normalize input. Mitigation: verify all PanelCladdingEditor material, parent, spawn, sync, and signature paths normalize with `Trim`/`IsNullOrWhiteSpace`, and cover read normalization in the focused test.
- Risk: generic attribute tooling will expose a one-character value. Mitigation: use an ordinary space so Rhino displays an empty-looking field without introducing an opaque protocol token.
- Rollback: remove the storage encoder and restore direct logical-cell writes; assigned-cell and topology behavior is otherwise unchanged.

## Future extensions

- If Rhino later supports retaining a true zero-length user string, replace the storage encoder without changing domain semantics.
- Centralize user-text decoding if future integrations need to consume the stored blank representation without using the existing key parser.

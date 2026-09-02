import json
import re
import sys
from pathlib import Path

import rhino3dm


def main() -> int:
    if len(sys.argv) != 3:
        raise SystemExit("usage: extract_bkt_materials.py <model.3dm> <output.json>")
    model_path = Path(sys.argv[1])
    output_path = Path(sys.argv[2])
    model = rhino3dm.File3dm.Read(str(model_path))
    if model is None:
        raise RuntimeError(f"Unable to read {model_path}")

    cell_key = re.compile(r"^CW_\d+\.\d{2}_CLADDING_\d+[A-Z]+$", re.IGNORECASE)
    cell_reference = re.compile(r"^\d+[A-Z]+$", re.IGNORECASE)
    material_code = re.compile(r"^[A-Z]{2,4}-\d{3}$", re.IGNORECASE)
    used_codes: set[str] = set()
    for item in model.Objects:
        for key, value in item.Attributes.GetUserStrings():
            if cell_key.fullmatch(key or ""):
                normalized = (value or "").strip().upper()
                if normalized and not cell_reference.fullmatch(normalized) and material_code.fullmatch(normalized):
                    used_codes.add(normalized)

    material_layers: dict[str, dict] = {}
    roots = ("03_MATERIAL SURFACES (STEP)::", "03_MATERIAL SURFACES (STP)::")
    for layer in model.Layers:
        full_path = (layer.FullPath or layer.Name or "").strip()
        if not full_path.upper().startswith(roots):
            continue
        code = (layer.Name or "").strip().upper()
        if code not in used_codes:
            continue
        color = layer.Color
        red, green, blue = color[:3]
        material_layers[code] = {
            "code": code,
            "colorHex": f"#{red:02X}{green:02X}{blue:02X}",
            "layerPath": full_path,
        }

    missing = sorted(used_codes.difference(material_layers), key=str.casefold)
    result = {
        "modelPath": str(model_path.resolve()),
        "objectCount": len(model.Objects),
        "layerCount": len(model.Layers),
        "usedCodes": sorted(used_codes, key=str.casefold),
        "materials": [material_layers[code] for code in sorted(material_layers, key=str.casefold)],
        "missingColorCodes": missing,
    }
    output_path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(
        f"BKT_MATERIALS={len(material_layers)}|USED_CODES={len(used_codes)}|"
        f"MISSING_COLORS={len(missing)}"
    )
    for material in result["materials"]:
        print(f"{material['code']}|{material['colorHex']}|{material['layerPath']}")
    return 0 if not missing else 1


if __name__ == "__main__":
    raise SystemExit(main())

"""Preview/apply via installed Router only; preserve raw mutation evidence before validation.

No .3dm files are opened from disk. Default mode is read-only preview.
"""
import argparse
import json
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "Scripts"))
from stone_wireframe_bake import DEFAULT_ROUTER, RouterClient, unwrap_tool_result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--document", required=True)
    parser.add_argument("--evidence", required=True, type=Path)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    args.evidence.mkdir(parents=True, exist_ok=True)

    def save(name, value):
        (args.evidence / (name + ".json")).write_text(json.dumps(value, indent=2), encoding="utf-8")

    arguments = {"filePath": args.document, "outputMode": "WorldZSurface", "surfaceHeightInches": 5}
    with RouterClient(DEFAULT_ROUTER, args.document, 180) as client:
        catalog = client.request("tools/list", {})
        bake = next(t for t in catalog["result"]["tools"] if t["name"] == "apply_stone_wireframe_bake")
        assert "outputMode" in bake["inputSchema"]["properties"], "The loaded plugin has no surface output support."
        preview = client.call_tool("preview_stone_wireframe_bake", arguments)
        save("preview", preview)
        assert preview["failedCount"] == 0, preview["failures"]
        assert preview["outputMode"] == "WorldZSurface"
        assert preview["surfaceHeightInches"] == 5 and preview["surfaceHeightModelUnits"] > 0
        assert len(preview["placements"]) == preview["stoneOccurrenceCount"], "Verification requires all placement evidence."
        print(json.dumps({k: preview[k] for k in ("assemblyInstanceCount", "stoneDefinitionCount", "stoneOccurrenceCount",
              "wouldCreateDefinitionCount", "wouldCreateInstanceCount", "existingInstanceCount", "failedCount")}))
        if not args.apply:
            return

        mutation_file = args.evidence / "apply-raw.json"
        assert not mutation_file.exists(), "Prior mutation evidence exists. Inspect it before any repeat apply."
        source_defs = client.call_tool("list_block_definitions", {"filePath": args.document})
        parent_ids = sorted({p["assemblyInstanceId"] for p in preview["placements"]})
        parents = {i: client.call_tool("get_block_instance_details", {"filePath": args.document, "objectId": i})
                   for i in parent_ids}
        save("source-definitions-before", source_defs)
        save("parent-instances-before", parents)

        raw = client.request("tools/call", {"name": "apply_stone_wireframe_bake", "arguments": arguments})
        save("apply-raw", raw)
        applied = unwrap_tool_result(raw, "apply_stone_wireframe_bake")
        save("apply", applied)
        assert applied["failedCount"] == 0
        assert applied["createdDefinitionCount"] == preview["wouldCreateDefinitionCount"]
        assert applied["createdInstanceCount"] == preview["wouldCreateInstanceCount"]
        post = client.call_tool("preview_stone_wireframe_bake", arguments)
        save("post-preview", post)
        assert post["failedCount"] == 0 and post["wouldCreateInstanceCount"] == 0
        assert post["wouldCreateDefinitionCount"] == 0 and post["existingInstanceCount"] == preview["stoneOccurrenceCount"]

        # The post-preview checks actual shared definition contents and full instance matrices.
        # Independently read each resulting block and verify its world-space corners and metadata.
        length_by_name = {d["outputDefinitionName"]: d["length"] for d in post["definitions"]}
        height = post["surfaceHeightModelUnits"]
        outputs = []

        def xyz(point):
            return [point["x"], point["y"], point["z"]]

        def transform(m, p):
            return [sum(m[r * 4 + c] * p[c] for c in range(3)) + m[r * 4 + 3] for r in range(3)]

        for p in post["placements"]:
            detail = client.call_tool("get_block_instance_details", {"filePath": args.document, "objectId": p["outputObjectId"]})
            instance = detail["instance"]
            assert instance["definitionName"] == p["outputDefinitionName"]
            assert instance["userText"]["Assembly_ID"] == p["assemblyDefinitionName"]
            assert instance["name"] == p["outputDefinitionName"]
            length = length_by_name[p["outputDefinitionName"]]
            expected = [xyz(p[k]) for k in ("worldStartPoint", "worldEndPoint", "worldTopEndPoint", "worldTopStartPoint")]
            actual = [transform(instance["transform"]["matrix"], q)
                      for q in ([0, 0, 0], [length, 0, 0], [length, 0, height], [0, 0, height])]
            assert all(math.dist(a, b) < 1e-7 for a, b in zip(actual, expected)), "Corner placement mismatch."
            box = instance["boundingBox"]
            for axis, key in enumerate(("X", "Y", "Z")):
                assert abs(box["min" + key] - min(q[axis] for q in expected)) < 0.001
                assert abs(box["max" + key] - max(q[axis] for q in expected)) < 0.001
            outputs.append(detail)
        save("verified-instances", outputs)

        defs_after = client.call_tool("list_block_definitions", {"filePath": args.document})
        save("definitions-after", defs_after)
        after_by_id = {d["definitionId"]: d for d in defs_after["definitions"]}
        for source in source_defs["definitions"]:
            assert after_by_id[source["definitionId"]] == source, "An existing source definition changed."
        for i, original in parents.items():
            current = client.call_tool("get_block_instance_details", {"filePath": args.document, "objectId": i})
            assert current == original, "A source assembly instance changed."
        result = {"verifiedInstances": len(outputs), "verifiedSourceAssemblies": len(parents),
                  "surfaceDefinitions": len(length_by_name), "heightInches": 5, "direction": "World +Z",
                  "sourceDefinitionsUnchanged": True, "repeatPreviewCreatesNothing": True}
        save("verification", result)
        print(json.dumps(result))


if __name__ == "__main__":
    main()

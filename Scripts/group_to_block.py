"""Convert Rhino groups to blocks named from each group ID.

Usage:
1. Preselect one or more grouped objects, then run this script.
2. If nothing is preselected, the script prompts you to select grouped objects.
3. Set PROCESS_ALL_GROUPS to True to convert every active group in the file.

Each converted group becomes:
- one block definition named with the Rhino group component GUID
- one block instance inserted back at the group's bounding-box center

The original grouped objects are deleted only after the block instance is
created successfully.
"""

import System
import Rhino
import rhinoscriptsyntax as rs
import scriptcontext as sc


PROCESS_ALL_GROUPS = False
DELETE_SOURCE_OBJECTS = True
DELETE_EMPTY_GROUPS = True

# "BoundingBoxCenter" keeps each block insertion point near its source geometry.
# Change to "WorldOrigin" if you want all converted blocks inserted at 0,0,0.
BASE_POINT_MODE = "BoundingBoxCenter"


def write_line(message):
    Rhino.RhinoApp.WriteLine(str(message))


def active_group_indices():
    indices = []
    for index in range(sc.doc.Groups.Count):
        if sc.doc.Groups.IsDeleted(index):
            continue
        if sc.doc.Groups.GroupObjectCount(index) <= 0:
            continue
        indices.append(index)
    return indices


def group_indices_from_objects(objects):
    indices = set()
    for rhino_object in objects:
        if rhino_object is None or rhino_object.IsDeleted:
            continue

        group_list = rhino_object.Attributes.GetGroupList()
        if not group_list:
            continue

        for group_index in group_list:
            if group_index < 0:
                continue
            if sc.doc.Groups.IsDeleted(group_index):
                continue
            indices.add(int(group_index))

    return sorted(indices)


def selected_group_indices():
    selected = list(sc.doc.Objects.GetSelectedObjects(False, False))
    return group_indices_from_objects(selected)


def picked_group_indices():
    get_object = Rhino.Input.Custom.GetObject()
    get_object.SetCommandPrompt("Select grouped objects to convert to blocks")
    get_object.GroupSelect = True
    get_object.SubObjectSelect = False
    get_object.ReferenceObjectSelect = False
    get_object.GetMultiple(1, 0)

    if get_object.CommandResult() != Rhino.Commands.Result.Success:
        return None

    objects = []
    for index in range(get_object.ObjectCount):
        rhino_object = get_object.Object(index).Object()
        if rhino_object is not None:
            objects.append(rhino_object)

    return group_indices_from_objects(objects)


def get_group_component(group_index):
    try:
        return sc.doc.Groups.FindIndex(group_index)
    except Exception:
        pass

    try:
        return sc.doc.Groups[group_index]
    except Exception:
        return None


def block_name_for_group(group_index):
    group = get_group_component(group_index)
    if group is not None:
        try:
            group_id = group.Id
            if group_id != System.Guid.Empty:
                return str(group_id)
        except Exception:
            pass

    group_name = sc.doc.Groups.GroupName(group_index)
    if group_name and group_name.strip():
        return group_name.strip()

    return "Group_{0}".format(group_index)


def group_members(group_index):
    members = sc.doc.Groups.GroupMembers(group_index)
    if not members:
        return []

    return [rhino_object for rhino_object in members
            if rhino_object is not None and not rhino_object.IsDeleted]


def group_base_point(members):
    if BASE_POINT_MODE == "WorldOrigin":
        return Rhino.Geometry.Point3d.Origin

    bounding_box = None
    for rhino_object in members:
        if rhino_object.Geometry is None:
            continue

        object_box = rhino_object.Geometry.GetBoundingBox(True)
        if not object_box.IsValid:
            continue

        if bounding_box is None:
            bounding_box = object_box
        else:
            bounding_box.Union(object_box)

    if bounding_box is None or not bounding_box.IsValid:
        return None

    return bounding_box.Center


def common_layer_index(members):
    if not members:
        return None

    layer_index = members[0].Attributes.LayerIndex
    for rhino_object in members[1:]:
        if rhino_object.Attributes.LayerIndex != layer_index:
            return None

    return layer_index


def set_block_instance_attributes(instance_id, block_name, layer_index):
    instance_object = sc.doc.Objects.FindId(instance_id)
    if instance_object is None:
        return

    attributes = instance_object.Attributes.Duplicate()
    attributes.Name = block_name
    if layer_index is not None:
        attributes.LayerIndex = layer_index

    sc.doc.Objects.ModifyAttributes(instance_id, attributes, True)


def validate_members(group_index, block_name, members):
    if rs.IsBlock(block_name):
        return "Block definition already exists: {0}".format(block_name)

    if not members:
        return "Group has no active members."

    for rhino_object in members:
        if rhino_object.IsReference:
            return "Reference objects cannot be converted: {0}".format(rhino_object.Id)

        if rhino_object.IsLocked:
            return "Locked objects cannot be converted: {0}".format(rhino_object.Id)

        if rhino_object.Geometry is None:
            return "Object has no convertible geometry: {0}".format(rhino_object.Id)

        if rhino_object.ObjectType in (
                Rhino.DocObjects.ObjectType.Light,
                Rhino.DocObjects.ObjectType.Grip,
                Rhino.DocObjects.ObjectType.Phantom):
            return "Group contains an unsupported object type: {0}".format(
                rhino_object.Id)

    if group_base_point(members) is None:
        return "Could not compute a valid group base point."

    return None


def collect_group_data(group_indices):
    data = {}
    object_to_groups = {}

    for group_index in group_indices:
        members = group_members(group_index)
        data[group_index] = members

        for rhino_object in members:
            key = str(rhino_object.Id)
            if key not in object_to_groups:
                object_to_groups[key] = []
            object_to_groups[key].append(group_index)

    overlapping_groups = set()
    for indices in object_to_groups.values():
        if len(indices) > 1:
            for group_index in indices:
                overlapping_groups.add(group_index)

    return data, overlapping_groups


def delete_sources(members):
    deleted_count = 0
    for rhino_object in members:
        if sc.doc.Objects.Delete(rhino_object.Id, True):
            deleted_count += 1
    return deleted_count


def convert_group(group_index, members, overlapping_groups):
    block_name = block_name_for_group(group_index)

    if group_index in overlapping_groups:
        return {
            "status": "skipped",
            "group_index": group_index,
            "block_name": block_name,
            "message": "Group shares source objects with another selected group."
        }

    validation_error = validate_members(group_index, block_name, members)
    if validation_error:
        return {
            "status": "skipped",
            "group_index": group_index,
            "block_name": block_name,
            "message": validation_error
        }

    base_point = group_base_point(members)
    layer_index = common_layer_index(members)
    object_ids = [rhino_object.Id for rhino_object in members]

    created_name = rs.AddBlock(object_ids, base_point, block_name, False)
    if not created_name:
        return {
            "status": "failed",
            "group_index": group_index,
            "block_name": block_name,
            "message": "Failed to create block definition."
        }

    instance_id = rs.InsertBlock(created_name, base_point)
    if not instance_id:
        try:
            rs.DeleteBlock(created_name)
        except Exception:
            pass

        return {
            "status": "failed",
            "group_index": group_index,
            "block_name": block_name,
            "message": "Created definition but failed to insert block instance."
        }

    set_block_instance_attributes(instance_id, created_name, layer_index)

    deleted_count = 0
    if DELETE_SOURCE_OBJECTS:
        deleted_count = delete_sources(members)
        if DELETE_EMPTY_GROUPS:
            try:
                sc.doc.Groups.Delete(group_index)
            except Exception:
                pass

    return {
        "status": "converted",
        "group_index": group_index,
        "block_name": created_name,
        "instance_id": instance_id,
        "source_count": len(members),
        "deleted_count": deleted_count,
        "message": "Converted {0} source object(s).".format(len(members))
    }


def groups_to_convert():
    if PROCESS_ALL_GROUPS:
        return active_group_indices()

    indices = selected_group_indices()
    if indices:
        return indices

    return picked_group_indices()


def main():
    group_indices = groups_to_convert()
    if group_indices is None:
        return Rhino.Commands.Result.Cancel

    if not group_indices:
        write_line("No grouped objects were selected.")
        return Rhino.Commands.Result.Nothing

    data, overlapping_groups = collect_group_data(group_indices)
    results = []

    rs.EnableRedraw(False)
    try:
        for group_index in group_indices:
            results.append(convert_group(
                group_index,
                data.get(group_index, []),
                overlapping_groups))
    finally:
        rs.EnableRedraw(True)
        sc.doc.Views.Redraw()

    converted = [result for result in results if result["status"] == "converted"]
    skipped = [result for result in results if result["status"] == "skipped"]
    failed = [result for result in results if result["status"] == "failed"]

    write_line("Group-to-block batch complete. Converted: {0}. Skipped: {1}. Failed: {2}.".format(
        len(converted), len(skipped), len(failed)))

    for result in results:
        write_line("[{0}] group {1} -> {2}: {3}".format(
            result["status"],
            result["group_index"],
            result["block_name"],
            result["message"]))

    if failed:
        return Rhino.Commands.Result.Failure

    if converted:
        return Rhino.Commands.Result.Success

    return Rhino.Commands.Result.Nothing


if __name__ == "__main__":
    main()

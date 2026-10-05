"""Remove Blender's baked, static object tracks from the book statue FBX.

Run with Blender's Python after exporting the source model. The only animation
left in the FBX belongs to the Page_00..Page_12 bones; the statue root and all
static meshes remain free to use their scene transforms in Unity.
"""

import os
import shutil
from pathlib import Path

from io_scene_fbx import encode_bin, parse_fbx


PROJECT = Path(__file__).resolve().parents[1]
FBX = PROJECT / "Assets/FINAL_ASSETS/MAPS/REMADE/Book_Statue.fbx"
BACKUP = PROJECT / "Library/Book_Statue.before_animation_fix.fbx"
TEMP = PROJECT / "Library/Book_Statue.pages_only.fbx"


def child(element, name):
    return next(item for item in element.elems if item.id == name)


root, version = parse_fbx.parse(str(FBX))
objects = child(root, b"Objects")
connections = child(root, b"Connections")
models = {item.props[0]: item.props[1].split(b"\x00", 1)[0]
          for item in objects.elems if item.id == b"Model"}
page_bones = {model_id for model_id, name in models.items()
              if name.startswith(b"Page_") and name[5:].isdigit()}
assert len(page_bones) == 13, f"Expected 13 page bones, got {len(page_bones)}"

all_nodes = {item.props[0] for item in objects.elems
             if item.id == b"AnimationCurveNode"}
all_curves = {item.props[0] for item in objects.elems
              if item.id == b"AnimationCurve"}

# An OP connection attaches each curve node to a model's local transform.
page_nodes = {link.props[1] for link in connections.elems
              if link.id == b"C" and len(link.props) >= 3
              and link.props[0] == b"OP"
              and link.props[1] in all_nodes and link.props[2] in page_bones}
# The curve's OP connection names its X/Y/Z channel on that node.
page_curves = {link.props[1] for link in connections.elems
               if link.id == b"C" and len(link.props) >= 3
               and link.props[0] == b"OP"
               and link.props[1] in all_curves and link.props[2] in page_nodes}
assert len(page_nodes) == 39 and len(page_curves) == 117, (
    f"Unexpected page animation: {len(page_nodes)} nodes, {len(page_curves)} curves"
)

removed = (all_nodes - page_nodes) | (all_curves - page_curves)
objects.elems[:] = [item for item in objects.elems
                    if not (item.id in {b"AnimationCurveNode", b"AnimationCurve"}
                            and item.props[0] in removed)]
connections.elems[:] = [link for link in connections.elems
                        if not (link.id == b"C" and any(
                            isinstance(prop, int) and prop in removed
                            for prop in link.props[1:3]))]

definitions = child(root, b"Definitions")
child(definitions, b"Count").props[0] = len(objects.elems) + 1
for kind in (b"AnimationCurveNode", b"AnimationCurve"):
    object_type = next(item for item in definitions.elems
                       if item.id == b"ObjectType" and item.props[0] == kind)
    child(object_type, b"Count").props[0] = sum(
        item.id == kind for item in objects.elems)


def encode(parsed):
    element = encode_bin.FBXElem(parsed.id)
    adders = {
        "C": element.add_char,
        "D": element.add_float64,
        "I": element.add_int32,
        "L": element.add_int64,
        "R": element.add_bytes,
        "S": element.add_string,
        "d": element.add_float64_array,
        "f": element.add_float32_array,
        "i": element.add_int32_array,
        "l": element.add_int64_array,
    }
    for value, kind in zip(parsed.props, parsed.props_type):
        adders[chr(kind)](value)
    element.elems = [encode(item) for item in parsed.elems]
    return element


BACKUP.parent.mkdir(parents=True, exist_ok=True)
if not BACKUP.exists():
    shutil.copy2(FBX, BACKUP)
encode_bin.write(str(TEMP), encode(root), version)
check, _ = parse_fbx.parse(str(TEMP))
check_objects = child(check, b"Objects")
assert sum(item.id == b"AnimationCurveNode" for item in check_objects.elems) == 39
assert sum(item.id == b"AnimationCurve" for item in check_objects.elems) == 117
os.replace(TEMP, FBX)
print(f"Kept 117 page-bone curves; removed {len(removed)} static/root tracks from {FBX}")

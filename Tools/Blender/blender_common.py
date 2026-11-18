"""
What the generators' Blender scripts share (Tools/Mannequin/generate_mannequin.py, Tools/Fox/generate_fox.py, and their
animation modules): rotations and easing for posing, materials, selecting, the skinning check, reading the command line,
and the FBX export, written the way Unity imports them. Each script puts this folder on sys.path before importing it.
A script's log lines start with its tag ("[mannequin]", "[fox]"), and "[tag] exported" once the file is written, which
Unity's BlenderRunner waits for.
"""
import math
import os
import sys

import bpy
from mathutils import Quaternion


# ---- Posing ----

def rx(deg):
    return Quaternion((1.0, 0.0, 0.0), math.radians(deg))


def ry(deg):
    return Quaternion((0.0, 1.0, 0.0), math.radians(deg))


def rz(deg):
    return Quaternion((0.0, 0.0, 1.0), math.radians(deg))


def rotate_x(point, deg):
    """Rotates a (y, z) point about X, the same way rx() does."""
    a = math.radians(deg)
    y, z = point
    return (y * math.cos(a) - z * math.sin(a), y * math.sin(a) + z * math.cos(a))


def smoothstep(edge0, edge1, x):
    """0 at edge0, 1 at edge1, easing in and out between (clamped outside)."""
    t = min(max((x - edge0) / (edge1 - edge0), 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


# ---- Building ----

def make_material(name, color, metallic=0.0, roughness=0.6):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = color
    return mat


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def validate(body, rig, tag):
    """Fails loudly if any vertex isn't skinned, so a bad heat-weighting pass never reaches Unity."""
    bone_names = set(rig.data.bones.keys())
    group_names = {g.index: g.name for g in body.vertex_groups}
    unweighted = 0
    for v in body.data.vertices:
        total = sum(g.weight for g in v.groups if group_names.get(g.group) in bone_names)
        if total <= 1e-4:
            unweighted += 1
    used = {group_names[g.group] for v in body.data.vertices for g in v.groups if g.weight > 1e-4}
    unused = sorted(bone_names - used)
    dims = body.dimensions
    print(f"[{tag}] vertices={len(body.data.vertices)} faces={len(body.data.polygons)} "
          f"bones={len(bone_names)} size=({dims.x:.2f}, {dims.y:.2f}, {dims.z:.2f})")
    print(f"[{tag}] unweighted vertices={unweighted}; bones with no influence={unused}")
    if unweighted:
        raise RuntimeError(f"{unweighted} vertices have no bone weights; heat weighting failed.")


# ---- Running and exporting ----

def parse_args():
    """The command line after "--": the output FBX (absolute), and the --preview directory, or None."""
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        raise SystemExit("Usage: ... -- <output.fbx> [--preview <dir>]")
    output = os.path.abspath(argv[0])
    preview_dir = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    return output, preview_dir


def export_fbx(path, objects, tag):
    """Exports the objects (a mesh and its armature) and their actions to an FBX, as Unity imports it, then logs it."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,  # Bake the axis conversion so objects import into Unity without a -90° X rotation.
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        use_armature_deform_only=True,
        mesh_smooth_type="FACE",
        bake_anim=True,
        bake_anim_use_all_actions=True,  # One FBX take per action: each animation clip.
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,  # Keep every baked key; simplification can break loop seams.
    )
    print(f"[{tag}] exported {path}")

"""
Generates a rigged, skinned, animated humanoid mannequin and exports it as an FBX for Unity.

The body is built with Blender's Skin modifier around a stick skeleton, then smoothed, rigged with an armature
using Unity's standard humanoid bone names (so Unity's Humanoid avatar maps it automatically), and skinned with
automatic (heat) weights. Animation clips come from mannequin_animations.py. The model is built in a T-pose, facing -Y in Blender (which is +Z, Unity's forward).

Usually run from Unity with Tools > Character Showcase > Generate Mannequin, which then sets up its animations.
By hand (Blender 4.x, from the repo root):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
        -P Tools/Mannequin/generate_mannequin.py -- Assets/Models/CharacterModels/Mannequin/Mannequin.fbx [--preview <dir>]

All measurements are in meters. Total height is ~1.8 m, matching the 2 m character capsule.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mannequin_animations  # noqa: E402  (needs the script directory on sys.path first)

# ---------------------------------------------------------------------------------------------------------------
# Proportions. +X is the character's left, -Y is forward, +Z is up.
# ---------------------------------------------------------------------------------------------------------------

SHOULDER_Z = 1.42
HIP_X = 0.10

# Skin-modifier skeleton: name -> (position, (radius_x, radius_y)). The subdivision pass shrinks the skin by
# roughly a third, so these radii are larger than the final body. Mid-limb joints exist only to shape muscles.
SKIN_JOINTS = {
    "hips":       ((0.0, 0.0, 0.98), (0.175, 0.15)),
    "spine":      ((0.0, 0.0, 1.12), (0.155, 0.145)),
    "chest":      ((0.0, -0.01, 1.28), (0.185, 0.17)),
    "upperchest": ((0.0, 0.0, 1.40), (0.205, 0.155)),
    "neck":       ((0.0, 0.0, 1.50), (0.07, 0.07)),
    "headbase":   ((0.0, 0.0, 1.56), (0.085, 0.085)),
    "headmid":    ((0.0, -0.01, 1.66), (0.115, 0.13)),
    "headtop":    ((0.0, 0.0, 1.77), (0.075, 0.075)),
}
for side, sx in (("L", 1.0), ("R", -1.0)):
    SKIN_JOINTS.update({
        f"shoulder{side}": ((0.19 * sx, 0.0, SHOULDER_Z), (0.08, 0.08)),
        f"bicep{side}":    ((0.32 * sx, 0.0, SHOULDER_Z), (0.062, 0.062)),
        f"elbow{side}":    ((0.46 * sx, 0.0, SHOULDER_Z), (0.05, 0.05)),
        f"forearm{side}":  ((0.57 * sx, 0.0, SHOULDER_Z), (0.052, 0.052)),
        f"wrist{side}":    ((0.71 * sx, 0.0, SHOULDER_Z), (0.04, 0.035)),
        f"hand{side}":     ((0.80 * sx, 0.0, SHOULDER_Z), (0.058, 0.028)),
        f"handend{side}":  ((0.88 * sx, 0.0, SHOULDER_Z), (0.042, 0.022)),
        f"hip{side}":      ((HIP_X * sx, 0.0, 0.93), (0.115, 0.115)),
        f"thigh{side}":    ((HIP_X * sx, 0.0, 0.74), (0.098, 0.098)),
        f"knee{side}":     ((HIP_X * sx, 0.0, 0.52), (0.072, 0.072)),
        f"calf{side}":     ((HIP_X * sx, 0.012, 0.36), (0.075, 0.075)),
        f"ankle{side}":    ((HIP_X * sx, 0.0, 0.09), (0.052, 0.052)),
        f"toe{side}":      ((HIP_X * sx, -0.13, 0.05), (0.058, 0.042)),
        f"toeend{side}":   ((HIP_X * sx, -0.20, 0.04), (0.048, 0.036)),
    })

SKIN_EDGES = [
    ("hips", "spine"), ("spine", "chest"), ("chest", "upperchest"), ("upperchest", "neck"),
    ("neck", "headbase"), ("headbase", "headmid"), ("headmid", "headtop"),
]
for side in ("L", "R"):
    SKIN_EDGES += [
        ("upperchest", f"shoulder{side}"), (f"shoulder{side}", f"bicep{side}"), (f"bicep{side}", f"elbow{side}"),
        (f"elbow{side}", f"forearm{side}"), (f"forearm{side}", f"wrist{side}"),
        (f"wrist{side}", f"hand{side}"), (f"hand{side}", f"handend{side}"),
        ("hips", f"hip{side}"), (f"hip{side}", f"thigh{side}"), (f"thigh{side}", f"knee{side}"),
        (f"knee{side}", f"calf{side}"), (f"calf{side}", f"ankle{side}"),
        (f"ankle{side}", f"toe{side}"), (f"toe{side}", f"toeend{side}"),
    ]

# Armature: name -> (head, tail, parent). Names follow Unity's humanoid conventions for automatic avatar mapping.
BONES = {
    "Hips":       ((0.0, 0.0, 0.95), (0.0, 0.0, 1.10), None),
    "Spine":      ((0.0, 0.0, 1.10), (0.0, 0.0, 1.26), "Hips"),
    "Chest":      ((0.0, 0.0, 1.26), (0.0, 0.0, 1.40), "Spine"),
    "UpperChest": ((0.0, 0.0, 1.40), (0.0, 0.0, 1.48), "Chest"),
    "Neck":       ((0.0, 0.0, 1.48), (0.0, 0.0, 1.56), "UpperChest"),
    "Head":       ((0.0, 0.0, 1.56), (0.0, 0.0, 1.80), "Neck"),
}
for side, name, sx in (("L", "Left", 1.0), ("R", "Right", -1.0)):
    BONES.update({
        f"{name}Shoulder": ((0.03 * sx, 0.0, SHOULDER_Z), (0.17 * sx, 0.0, SHOULDER_Z), "UpperChest"),
        f"{name}UpperArm": ((0.17 * sx, 0.0, SHOULDER_Z), (0.46 * sx, 0.0, SHOULDER_Z), f"{name}Shoulder"),
        f"{name}LowerArm": ((0.46 * sx, 0.0, SHOULDER_Z), (0.71 * sx, 0.0, SHOULDER_Z), f"{name}UpperArm"),
        f"{name}Hand":     ((0.71 * sx, 0.0, SHOULDER_Z), (0.88 * sx, 0.0, SHOULDER_Z), f"{name}LowerArm"),
        f"{name}UpperLeg": ((HIP_X * sx, 0.0, 0.93), (HIP_X * sx, 0.0, 0.52), "Hips"),
        f"{name}LowerLeg": ((HIP_X * sx, 0.0, 0.52), (HIP_X * sx, 0.0, 0.09), f"{name}UpperLeg"),
        f"{name}Foot":     ((HIP_X * sx, 0.0, 0.09), (HIP_X * sx, -0.13, 0.045), f"{name}LowerLeg"),
        f"{name}Toes":     ((HIP_X * sx, -0.13, 0.045), (HIP_X * sx, -0.21, 0.035), f"{name}Foot"),
    })

BODY_COLOR = (0.72, 0.73, 0.76, 1.0)
VISOR_COLOR = (0.04, 0.09, 0.18, 1.0)


# ---------------------------------------------------------------------------------------------------------------

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


def smoothstep(edge0, edge1, x):
    t = min(max((x - edge0) / (edge1 - edge0), 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def deepen_torso(mesh, front=0.45, back=0.2, bottom=1.02, top=1.52, half_width=0.17, blend=0.08):
    """
    The Skin modifier largely ignores the depth radius along the vertical spine chain, leaving a slab-like torso.
    Scale the chest's front-to-back depth directly, fading smoothly to nothing at the waist, neck and shoulders.
    """
    for v in mesh.vertices:
        x, y, z = v.co
        vertical = math.sin(math.pi * min(max((z - bottom) / (top - bottom), 0.0), 1.0))
        lateral = 1.0 - smoothstep(half_width, half_width + blend, abs(x))
        amount = front if y < 0.0 else back  # -Y is forward: the chest pushes out more than the back.
        v.co.y = y * (1.0 + amount * vertical * lateral)


def build_body():
    names = list(SKIN_JOINTS)
    index = {n: i for i, n in enumerate(names)}
    mesh = bpy.data.meshes.new("Body")
    mesh.from_pydata([SKIN_JOINTS[n][0] for n in names], [(index[a], index[b]) for a, b in SKIN_EDGES], [])
    body = bpy.data.objects.new("Body", mesh)
    bpy.context.collection.objects.link(body)

    skin = body.modifiers.new("Skin", "SKIN")
    skin.branch_smoothing = 0.6
    skin.use_smooth_shade = True
    for i, n in enumerate(names):
        vertex = mesh.skin_vertices[0].data[i]
        vertex.radius = SKIN_JOINTS[n][1]
        vertex.use_root = (n == "hips")

    subsurf = body.modifiers.new("Subdivision", "SUBSURF")
    subsurf.levels = 2
    subsurf.render_levels = 2

    activate(body)
    bpy.ops.object.modifier_apply(modifier="Skin")
    bpy.ops.object.modifier_apply(modifier="Subdivision")
    deepen_torso(body.data)
    bpy.ops.object.shade_smooth()
    body.data.materials.append(make_material("Body", BODY_COLOR))
    return body


def build_visor():
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1.0, location=(0.0, -0.105, 1.675))
    visor = bpy.context.active_object
    visor.name = "Visor"
    visor.scale = (0.075, 0.035, 0.032)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.shade_smooth()
    visor.data.materials.append(make_material("Visor", VISOR_COLOR, metallic=0.3, roughness=0.25))
    return visor


def build_armature():
    data = bpy.data.armatures.new("Armature")
    rig = bpy.data.objects.new("Armature", data)
    bpy.context.collection.objects.link(rig)
    activate(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (head, tail, _) in BONES.items():
        bone = data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        # Point every bone's roll axis forward/up consistently so twist axes are predictable in Unity.
        bone.align_roll(Vector((0.0, -1.0, 0.0)) if abs(tail[2] - head[2]) > 1e-4 else Vector((0.0, 0.0, 1.0)))
    for name, (_, _, parent) in BONES.items():
        if parent:
            data.edit_bones[name].parent = data.edit_bones[parent]
            data.edit_bones[name].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def skin_to_armature(body, visor, rig):
    # Automatic (heat) weights for the body.
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")

    # Heat weights change sharply across joints, which creases knees and elbows when they bend. Blur them a little,
    # then renormalise so each vertex's weights still sum to 1.
    activate(body)
    bpy.ops.object.mode_set(mode="WEIGHT_PAINT")
    bpy.ops.object.vertex_group_smooth(group_select_mode="ALL", factor=0.5, repeat=4, expand=0.0)
    # Unity skins with at most 4 bones per vertex by default; limit here so nothing is silently dropped on import.
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    bpy.ops.object.mode_set(mode="OBJECT")

    # The visor is rigid: fully weighted to the head. Joining keeps vertex groups by name.
    group = visor.vertex_groups.new(name="Head")
    group.add(list(range(len(visor.data.vertices))), 1.0, "REPLACE")
    bpy.ops.object.select_all(action="DESELECT")
    visor.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    return body


def validate(body, rig):
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
    print(f"[mannequin] vertices={len(body.data.vertices)} faces={len(body.data.polygons)} "
          f"bones={len(bone_names)} size=({dims.x:.2f}, {dims.y:.2f}, {dims.z:.2f})")
    print(f"[mannequin] unweighted vertices={unweighted}; bones with no influence={unused}")
    if unweighted:
        raise RuntimeError(f"{unweighted} vertices have no bone weights; heat weighting failed.")


def render_previews(directory):
    os.makedirs(directory, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x = 600
    scene.render.resolution_y = 800
    world = bpy.data.worlds.new("World")
    scene.world = world

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2.2
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam

    views = {
        "front": ((0.0, -5.0, 0.9), (math.radians(90), 0.0, 0.0)),
        "side": ((5.0, 0.0, 0.9), (math.radians(90), 0.0, math.radians(90))),
        "three_quarter": ((3.5, -3.5, 1.5), (math.radians(82), 0.0, math.radians(45))),
    }
    for name, (location, rotation) in views.items():
        cam.location = location
        cam.rotation_euler = rotation
        scene.render.filepath = os.path.join(directory, f"mannequin_{name}.png")
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        raise SystemExit("Usage: ... -- <output.fbx> [--preview <dir>]")
    output = os.path.abspath(argv[0])
    preview_dir = argv[argv.index("--preview") + 1] if "--preview" in argv else None

    bpy.ops.wm.read_factory_settings(use_empty=True)

    body = build_body()
    visor = build_visor()
    rig = build_armature()
    body = skin_to_armature(body, visor, rig)
    validate(body, rig)
    clips = mannequin_animations.build_actions(rig)
    print(f"[mannequin] animation clips={clips}")

    if preview_dir:
        render_previews(preview_dir)

    os.makedirs(os.path.dirname(output), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    rig.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=output,
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
        bake_anim_use_all_actions=True,  # One FBX take per action: Idle, Walk, Run, Rise, Fall.
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,  # Keep every baked key; simplification can break loop seams.
    )
    print(f"[mannequin] exported {output}")


if __name__ == "__main__":
    main()

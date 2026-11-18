"""
Generates a rigged, skinned, animated low-poly fox (wolf-sized) and exports it as an FBX for Unity.
The body is built with Blender's Skin modifier around a four-legged stick skeleton, subdivided once and flat shaded
(low poly), coloured in flat regions (orange coat, white chest, muzzle and tail tip, dark legs, ear tips and nose),
rigged with a Generic armature (Hips, Spine1-2, Neck, Head, Muzzle, Ears, Tail1-4, and each leg's bones), and
skinned with automatic (heat) weights. Animation clips come from fox_animations.py. Built standing on all fours,
facing -Y in Blender (which is +Z, Unity's forward), its paws on the ground at the origin.
Usually run from Unity with Tools > Character Showcase > Generate Fox, which then sets it up.
By hand (Blender 4.x, from the repo root):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
        -P Tools/Fox/generate_fox.py -- Assets/Models/Creatures/Fox/Fox.fbx [--preview <dir>]
All measurements are in meters: about 0.7 m at the shoulder, 1.3 m from nose to rump, and a 0.6 m tail.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "Blender"))
import fox_animations  # noqa: E402  (needs the script directory on sys.path first)
from blender_common import activate, export_fbx, make_material, parse_args, validate  # noqa: E402

# ---------------------------------------------------------------------------------------------------------------
# Proportions. +X is the fox's left, -Y is forward, +Z is up.
# ---------------------------------------------------------------------------------------------------------------
LEG_X = 0.09

# Skin-modifier skeleton: name -> (position, (radius_x, radius_y)).
SKIN_JOINTS = {
    "rump":    ((0.0, 0.42, 0.66), (0.15, 0.16)),
    "loin":    ((0.0, 0.20, 0.68), (0.14, 0.15)),
    "belly":   ((0.0, 0.00, 0.66), (0.16, 0.18)),
    "chest":   ((0.0, -0.22, 0.64), (0.17, 0.21)),
    "withers": ((0.0, -0.34, 0.70), (0.15, 0.17)),
    "neck":    ((0.0, -0.46, 0.84), (0.095, 0.105)),
    "head":    ((0.0, -0.56, 0.93), (0.12, 0.11)),
    "muzzle":  ((0.0, -0.69, 0.89), (0.06, 0.05)),
    "nose":    ((0.0, -0.79, 0.87), (0.025, 0.022)),
    "tail1":   ((0.0, 0.52, 0.66), (0.05, 0.05)),
    "tail2":   ((0.0, 0.66, 0.60), (0.09, 0.09)),
    "tail3":   ((0.0, 0.82, 0.53), (0.105, 0.105)),
    "tail4":   ((0.0, 0.96, 0.48), (0.075, 0.075)),
    "tailtip": ((0.0, 1.06, 0.46), (0.02, 0.02)),
}
for side, sx in (("L", 1.0), ("R", -1.0)):
    SKIN_JOINTS.update({
        f"ear{side}":       ((0.06 * sx, -0.55, 1.02), (0.045, 0.025)),
        f"eartip{side}":    ((0.072 * sx, -0.54, 1.13), (0.01, 0.01)),
        f"fshoulder{side}": ((LEG_X * sx, -0.30, 0.56), (0.08, 0.09)),
        f"felbow{side}":    ((LEG_X * sx, -0.27, 0.36), (0.052, 0.052)),
        f"fwrist{side}":    ((LEG_X * sx, -0.29, 0.10), (0.036, 0.036)),
        f"fpaw{side}":      ((LEG_X * sx, -0.33, 0.035), (0.042, 0.035)),
        f"ftoe{side}":      ((LEG_X * sx, -0.38, 0.025), (0.025, 0.02)),
        f"hhip{side}":      ((LEG_X * sx, 0.38, 0.56), (0.10, 0.11)),
        f"hknee{side}":     ((LEG_X * sx, 0.30, 0.36), (0.062, 0.068)),
        f"hhock{side}":     ((LEG_X * sx, 0.42, 0.16), (0.038, 0.038)),
        f"hpaw{side}":      ((LEG_X * sx, 0.39, 0.035), (0.042, 0.035)),
        f"htoe{side}":      ((LEG_X * sx, 0.34, 0.025), (0.025, 0.02)),
    })

SKIN_EDGES = [
    ("rump", "loin"), ("loin", "belly"), ("belly", "chest"), ("chest", "withers"), ("withers", "neck"),
    ("neck", "head"), ("head", "muzzle"), ("muzzle", "nose"),
    ("rump", "tail1"), ("tail1", "tail2"), ("tail2", "tail3"), ("tail3", "tail4"), ("tail4", "tailtip"),
]
for side in ("L", "R"):
    SKIN_EDGES += [
        ("head", f"ear{side}"), (f"ear{side}", f"eartip{side}"),
        ("chest", f"fshoulder{side}"), (f"fshoulder{side}", f"felbow{side}"), (f"felbow{side}", f"fwrist{side}"),
        (f"fwrist{side}", f"fpaw{side}"), (f"fpaw{side}", f"ftoe{side}"),
        ("rump", f"hhip{side}"), (f"hhip{side}", f"hknee{side}"), (f"hknee{side}", f"hhock{side}"),
        (f"hhock{side}", f"hpaw{side}"), (f"hpaw{side}", f"htoe{side}"),
    ]

# Armature: name -> (head, tail, parent). Generic names (not Unity humanoid): the fox is imported as a Generic rig.
BONES = {
    "Hips":   ((0.0, 0.42, 0.66), (0.0, 0.18, 0.68), None),
    "Spine1": ((0.0, 0.18, 0.68), (0.0, -0.06, 0.66), "Hips"),
    "Spine2": ((0.0, -0.06, 0.66), (0.0, -0.30, 0.68), "Spine1"),
    "Neck":   ((0.0, -0.32, 0.72), (0.0, -0.50, 0.88), "Spine2"),
    "Head":   ((0.0, -0.50, 0.88), (0.0, -0.62, 0.96), "Neck"),
    "Muzzle": ((0.0, -0.62, 0.92), (0.0, -0.80, 0.87), "Head"),
    "Tail1":  ((0.0, 0.48, 0.66), (0.0, 0.64, 0.61), "Hips"),
    "Tail2":  ((0.0, 0.64, 0.61), (0.0, 0.80, 0.54), "Tail1"),
    "Tail3":  ((0.0, 0.80, 0.54), (0.0, 0.95, 0.49), "Tail2"),
    "Tail4":  ((0.0, 0.95, 0.49), (0.0, 1.07, 0.46), "Tail3"),
}
for side, sx in (("L", 1.0), ("R", -1.0)):
    x = LEG_X * sx
    BONES.update({
        f"Ear_{side}":           ((0.055 * sx, -0.55, 1.00), (0.065 * sx, -0.54, 1.11), "Head"),
        f"FrontUpperLeg_{side}": ((x, -0.30, 0.58), (x, -0.27, 0.36), "Spine2"),
        f"FrontLowerLeg_{side}": ((x, -0.27, 0.36), (x, -0.29, 0.10), f"FrontUpperLeg_{side}"),
        f"FrontPaw_{side}":      ((x, -0.29, 0.10), (x, -0.38, 0.03), f"FrontLowerLeg_{side}"),
        f"HindUpperLeg_{side}":  ((x, 0.38, 0.58), (x, 0.30, 0.36), "Hips"),
        f"HindLowerLeg_{side}":  ((x, 0.30, 0.36), (x, 0.42, 0.16), f"HindUpperLeg_{side}"),
        f"HindFoot_{side}":      ((x, 0.42, 0.16), (x, 0.39, 0.04), f"HindLowerLeg_{side}"),
        f"HindPaw_{side}":       ((x, 0.39, 0.04), (x, 0.33, 0.025), f"HindFoot_{side}"),
    })

ORANGE = (0.80, 0.33, 0.08, 1.0)
WHITE = (0.93, 0.91, 0.86, 1.0)
DARK = (0.10, 0.075, 0.065, 1.0)
NOSE = (0.03, 0.03, 0.03, 1.0)


# ---------------------------------------------------------------------------------------------------------------

def region(center):
    """Which colour a face is, by where it is (its centre): flat regions, as a low-poly fox's coat."""
    x, y, z = center
    if y < -0.765:
        return "Nose"
    if z > 1.06 and abs(x) > 0.02:
        return "Dark"  # Ear tips
    if z < 0.24:
        return "Dark"  # Lower legs and paws
    if y > 0.99:
        return "White"  # Tail tip
    if y < -0.62 and z < 0.885:
        return "White"  # Under the muzzle
    if -0.50 < y < -0.12 and 0.38 < z < 0.62 and abs(x) < 0.065:
        return "White"  # Chest and the front of the belly, between the front legs
    if -0.64 < y < -0.42 and z < 0.86 and abs(x) < 0.08:
        return "White"  # Throat
    return "Orange"


def build_body():
    names = list(SKIN_JOINTS)
    index = {n: i for i, n in enumerate(names)}
    mesh = bpy.data.meshes.new("Body")
    mesh.from_pydata([SKIN_JOINTS[n][0] for n in names], [(index[a], index[b]) for a, b in SKIN_EDGES], [])
    body = bpy.data.objects.new("Body", mesh)
    bpy.context.collection.objects.link(body)

    skin = body.modifiers.new("Skin", "SKIN")
    skin.branch_smoothing = 0.5
    for i, n in enumerate(names):
        vertex = mesh.skin_vertices[0].data[i]
        vertex.radius = SKIN_JOINTS[n][1]
        vertex.use_root = (n == "rump")

    # One subdivision, flat shaded: rounded enough to read as a fox, faceted enough to be low poly.
    subsurf = body.modifiers.new("Subdivision", "SUBSURF")
    subsurf.levels = 1
    subsurf.render_levels = 1

    activate(body)
    bpy.ops.object.modifier_apply(modifier="Skin")
    # The Skin modifier leaves gaps where several branches meet closely (the tail and both hind legs at the rump):
    # merge what's doubled, drop loose edges, and close the holes, before smoothing.
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.002)
    bpy.ops.mesh.delete_loose(use_verts=True, use_edges=True, use_faces=False)
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.fill_holes(sides=0)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.modifier_apply(modifier="Subdivision")
    bpy.ops.object.shade_flat()

    # Paws on the ground: the lowest point at the origin's height.
    lowest = min(v.co.z for v in body.data.vertices)
    for v in body.data.vertices:
        v.co.z -= lowest

    materials = {name: make_material(name, color, roughness=0.8) for name, color in
                 (("Orange", ORANGE), ("White", WHITE), ("Dark", DARK), ("Nose", NOSE))}
    order = list(materials)
    for mat in materials.values():
        body.data.materials.append(mat)
    for poly in body.data.polygons:
        poly.material_index = order.index(region(poly.center))
    return body


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
        # A consistent roll (forward for mostly-vertical bones, up for the rest) so the bones' axes are predictable.
        vertical = abs(tail[2] - head[2]) > abs(tail[1] - head[1])
        bone.align_roll(Vector((0.0, -1.0, 0.0)) if vertical else Vector((0.0, 0.0, 1.0)))
    for name, (_, _, parent) in BONES.items():
        if parent:
            data.edit_bones[name].parent = data.edit_bones[parent]
            data.edit_bones[name].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def skin_to_armature(body, rig):
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")

    activate(body)
    bpy.ops.object.mode_set(mode="WEIGHT_PAINT")
    bpy.ops.object.vertex_group_smooth(group_select_mode="ALL", factor=0.5, repeat=3, expand=0.0)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return body


def render_previews(directory, rig):
    """Renders the fox from the side, front and three-quarters at rest, and from the side through each clip."""
    os.makedirs(directory, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x = 700
    scene.render.resolution_y = 500
    scene.world = bpy.data.worlds.new("World")

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2.2
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam

    def shoot(name, location, rotation):
        cam.location = location
        cam.rotation_euler = rotation
        scene.render.filepath = os.path.join(directory, f"fox_{name}.png")
        bpy.ops.render.render(write_still=True)

    side = ((4.0, 0.15, 0.55), (math.radians(90), 0.0, math.radians(90)))
    shoot("side", *side)
    shoot("front", (0.0, -4.0, 0.55), (math.radians(90), 0.0, 0.0))
    shoot("three_quarter", (3.0, -3.0, 1.4), (math.radians(78), 0.0, math.radians(45)))
    shoot("rear_quarter", (-2.6, 3.4, 1.6), (math.radians(72), 0.0, math.radians(-142)))
    for clip, (fn, _) in fox_animations.CLIPS.items():
        for k, phase in enumerate((0.0, 0.25, 0.5, 0.75)):
            fox_animations.apply_pose(rig, fn(phase))
            bpy.context.view_layer.update()
            shoot(f"{clip}_{k}", *side)
    fox_animations.reset_pose(rig)
    bpy.data.objects.remove(cam)


def main():
    output, preview_dir = parse_args()

    bpy.ops.wm.read_factory_settings(use_empty=True)

    body = build_body()
    rig = build_armature()
    body = skin_to_armature(body, rig)
    validate(body, rig, "fox")
    if preview_dir:
        render_previews(preview_dir, rig)
    clips = fox_animations.build_actions(rig)
    print(f"[fox] animation clips={clips}")

    export_fbx(output, [body, rig], "fox")


if __name__ == "__main__":
    main()

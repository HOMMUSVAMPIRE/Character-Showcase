"""Extracts Quaternius's male mannequin from an animation library FBX into a model file of its own.

The Universal Animation Libraries (Assets/Animation/Quaternius) are the libraries' mannequin with every animation on it;
the pack ships the female mannequin as a model file of its own, but not this one. This keeps the armature and its mesh,
drops the animations, and writes an FBX like the female one's (the same objects, rig and orientation), for
Assets/Models/Humanoid/Quaternius, where Prepare Humanoid Models sets it up as a character model. The file is
read back and checked: every bone where it rests in the library, the mesh the same, and no animations.

Run with Blender (4.x):
    Blender -b --factory-startup --python Tools/Animation/extract_mannequin.py -- <library.fbx> <out.fbx> [mesh name]
"""
import bpy, sys

args = sys.argv[sys.argv.index('--') + 1:]
source, output = args[0], args[1]
mesh_name = args[2] if len(args) > 2 else "Mannequin_M"
TOLERANCE = 1e-4  # metres: what a round trip through FBX may move a bone's rest position


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    armature = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if len(meshes) != 1:
        raise SystemExit(f"[extract] expected one mesh in {path}, found {[m.name for m in meshes]}")
    return armature, meshes[0]


def rest(armature):
    """Each bone's rest head and tail, in world space."""
    world = armature.matrix_world
    return {b.name: (world @ b.head_local, world @ b.tail_local) for b in armature.data.bones}


armature, mesh = load(source)
expected_bones = rest(armature)
expected_vertices = len(mesh.data.vertices)
expected_dimensions = tuple(mesh.dimensions)

# Drop the animations: the takes, and what's bound to them.
for obj in bpy.data.objects:
    if obj.animation_data:
        obj.animation_data_clear()
for action in list(bpy.data.actions):
    bpy.data.actions.remove(action)
mesh.name = mesh_name
mesh.data.name = mesh_name

bpy.ops.object.select_all(action='DESELECT')
armature.select_set(True)
mesh.select_set(True)
bpy.context.view_layer.objects.active = armature
bpy.ops.export_scene.fbx(
    filepath=output,
    use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False,  # The rig has its own leaf bones already (ball_leaf_l, ...).
    bake_anim=False,
    mesh_smooth_type='FACE',
    # The axes and units Quaternius's own files are in (Z up, -Y forward, centimetres), as the female mannequin's is:
    # written Y up in metres instead, Unity imported it turned round (Prepare Humanoid Models then turned it back).
    axis_forward='-Y',
    axis_up='Z',
    apply_scale_options='FBX_SCALE_UNITS',
)

# Read it back and check it.
armature, mesh = load(output)
got_bones = rest(armature)
problems = []
if set(got_bones) != set(expected_bones):
    problems.append(f"bones differ: {sorted(set(got_bones) ^ set(expected_bones))}")
else:
    worst = max(max((got_bones[n][0] - expected_bones[n][0]).length, (got_bones[n][1] - expected_bones[n][1]).length)
                for n in expected_bones)
    if worst > TOLERANCE:
        problems.append(f"a bone's rest moved by {worst:.6f} m")
if len(mesh.data.vertices) != expected_vertices:
    problems.append(f"the mesh has {len(mesh.data.vertices)} vertices, not {expected_vertices}")
if max(abs(a - b) for a, b in zip(mesh.dimensions, expected_dimensions)) > TOLERANCE:
    problems.append(f"the mesh measures {tuple(mesh.dimensions)}, not {expected_dimensions}")
if bpy.data.actions:
    problems.append(f"{len(bpy.data.actions)} animations left")
if problems:
    raise SystemExit("[extract] " + "; ".join(problems))
print(f"[extract] wrote {output}: {len(got_bones)} bones, {expected_vertices} vertices, "
      f"{mesh.dimensions.z:.3f} m tall, no animations")

"""Makes the T-pose in the first frame of each motion-capture clip (Assets/Animation/CMU) its skeleton's rest pose.

The clips' first frame is a T-pose (arms out, legs straight down, spine up), but the skeleton stored in each file rests in
another pose. A Humanoid avatar takes its reference pose from that stored skeleton, and every clip plays relative to it,
so this makes the T-pose the rest pose, and re-keys the motion against it so every bone is where it was in every frame.
Each exported file is read back and checked, and a line per clip is printed with the largest error.

Run with Blender (4.x):
    Blender -b --factory-startup --python Tools/Animation/tpose_rest.py -- <out folder, or - to overwrite> <clip.fbx>...
"""
import bpy, sys, os, json

args = sys.argv[sys.argv.index('--') + 1:]
out_dir = args[0]


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, anim_offset=0.0)  # Frames numbered as in the file.
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    action = arm.animation_data.action
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = int(round(action.frame_range[0])), int(round(action.frame_range[1]))
    return arm, action


def poses(arm):
    """Each bone's pose (armature space) at every frame."""
    scene = bpy.context.scene
    result = {}
    for f in range(scene.frame_start, scene.frame_end + 1):
        scene.frame_set(f)
        result[f] = {b.name: b.matrix.copy() for b in arm.pose.bones}
    return result


def positions(arm):
    scene = bpy.context.scene
    result = {}
    for f in range(scene.frame_start, scene.frame_end + 1):
        scene.frame_set(f)
        result[f] = {b.name: (b.head.copy(), b.tail.copy()) for b in arm.pose.bones}
    return result


def parents_first(arm):
    order, seen = [], set()
    def visit(bone):
        if bone.name in seen: return
        if bone.parent: visit(bone.parent)
        seen.add(bone.name); order.append(arm.pose.bones[bone.name])
    for bone in arm.data.bones: visit(bone)
    return order


for path in args[1:]:
    arm, action = load(path)
    scene = bpy.context.scene
    first = scene.frame_start
    recorded = poses(arm)
    expected = positions(arm)

    # The first frame's pose becomes the rest pose.
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    scene.frame_set(first)
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode='OBJECT')

    # Re-key every frame against the new rest pose, from the recorded poses.
    name = action.name
    arm.animation_data.action = None
    bpy.data.actions.remove(action)
    action = bpy.data.actions.new(name)
    arm.animation_data.action = action
    order = parents_first(arm)
    for b in order: b.rotation_mode = 'QUATERNION'
    for f in range(first, scene.frame_end + 1):
        scene.frame_set(f)
        for b in order:
            b.matrix = recorded[f][b.name]
            bpy.context.view_layer.update()
        for b in order:
            b.keyframe_insert('location', frame=f)
            b.keyframe_insert('rotation_quaternion', frame=f)
            if b.parent is None: b.keyframe_insert('scale', frame=f)
    for fc in action.fcurves:
        for k in fc.keyframe_points: k.interpolation = 'LINEAR'

    # Exported with the current frame on the T-pose, so the file's stored skeleton is the T-pose either way.
    scene.frame_set(first)
    temp = (path if out_dir == '-' else os.path.join(out_dir, os.path.basename(path))) + '.tmp.fbx'
    bpy.ops.export_scene.fbx(filepath=temp, object_types={'ARMATURE'}, add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.01)

    # Check the file: the same motion, and a T-pose at rest.
    arm2, _ = load(temp)
    got = positions(arm2)
    error = max(max((g[0] - e[0]).length, (g[1] - e[1]).length)
                for f in expected for n, e in expected[f].items() for g in [got[f][n]])
    rest = {b.name: b for b in arm2.data.bones}
    def direction(n): b = rest[n]; return (b.tail_local - b.head_local).normalized()
    arms_out = min(abs(direction('lShldr').x), abs(direction('rShldr').x))
    legs_down = min(-direction('lThigh').z, -direction('rThigh').z)
    target = path if out_dir == '-' else os.path.join(out_dir, os.path.basename(path))
    ok = error < 0.01 and arms_out > 0.95 and legs_down > 0.95
    if ok: os.replace(temp, target)
    else: os.remove(temp)
    print("RESULT", json.dumps(dict(file=os.path.basename(path), error=round(error, 5), arms_out=round(arms_out, 3),
                                    legs_down=round(legs_down, 3), written=ok)), flush=True)

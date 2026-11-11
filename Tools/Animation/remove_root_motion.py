"""Removes root motion from motion-capture clips (Assets/Animation/CMU), in place or into another folder.

Each clip's hips stop travelling across the ground: at each frame, a straight line fitted to the hips' path over the
window around it is taken out, so steady travel goes while the sway within each step stays, and the hips keep their
height, so jumps still rise and fall. Every other bone is left as captured. Each exported file is read back and checked
against the edit, and a line per clip is printed: how far its hips travelled before and after, and the largest error.

Run with Blender (4.x):
    Blender -b --factory-startup --python Tools/Animation/remove_root_motion.py -- <out folder, or - to overwrite> <window seconds> <clip.fbx>...
For example, to redo every clip in place with a one-second window (clips that stop or change speed suddenly may need
half a second):
    find Assets/Animation/CMU -name "*.fbx" -print0 | xargs -0 Blender -b --factory-startup --python Tools/Animation/remove_root_motion.py -- - 1.0
"""
import bpy, sys, os, json
from mathutils import Vector
args = sys.argv[sys.argv.index('--') + 1:]
out_dir, window = args[0], float(args[1])
report = []

def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Without Blender's default one-frame shift, so frames are numbered as in the file, and exported back unshifted.
    bpy.ops.import_scene.fbx(filepath=path, anim_offset=0.0)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    action = arm.animation_data.action
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = int(round(action.frame_range[0])), int(round(action.frame_range[1]))
    return arm, action

def bone_positions(arm, frames):
    """Every bone's head, in armature space, at each frame."""
    scene = bpy.context.scene
    result = []
    for f in frames:
        scene.frame_set(f)
        result.append({b.name: arm.matrix_world.inverted() @ (arm.matrix_world @ b.head) for b in arm.pose.bones})
    return result

for path in args[2:]:
    arm, action = load(path)
    hip = arm.pose.bones['hip']
    rest = hip.bone.matrix_local
    curves = {fc.array_index: fc for fc in action.fcurves if fc.data_path == 'pose.bones["hip"].location'}
    frames = sorted({int(round(k.co[0])) for k in curves[0].keyframe_points})
    fps = bpy.context.scene.render.fps
    before = bone_positions(arm, frames)
    # The hips in armature space (Z up), and their path across the ground: at each frame, a straight line fitted to the
    # hips over the window around it, which follows steady travel exactly (to the ends of the clip, where a plain average
    # would lag) while the sway within each step averages out. A window wider than the clip is a line over all of it.
    heads = [rest @ Vector([curves[i].evaluate(f) for i in range(3)]) for f in frames]
    half = max(1, int(round(window * fps / 2)))
    n = len(frames)
    path_xy = [Vector((0.0, 0.0))]
    # Fitted to the motion only, from the second frame.
    heads_all, heads = heads, heads[1:]
    n -= 1
    for i in range(n):
        a, b = i - half, i + half + 1
        if a < 0: a, b = 0, min(n, 2 * half + 1)
        if b > n: a, b = max(0, n - 2 * half - 1), n
        ts = list(range(a, b)); tm = sum(ts) / len(ts)
        fit = []
        for axis in (0, 1):
            vs = [heads[t][axis] for t in ts]; vm = sum(vs) / len(vs)
            var = sum((t - tm) ** 2 for t in ts)
            slope = sum((t - tm) * (v - vm) for t, v in zip(ts, vs)) / var if var > 0 else 0.0
            fit.append(vm + slope * (i - tm))
        path_xy.append(Vector(fit))
    heads = heads_all
    inverse = rest.inverted()
    # The first frame is the T-pose the clips are set up from (see tpose_rest.py), not part of the motion: left alone.
    for i, f in enumerate(frames):
        if i == 0: continue
        moved = heads[i] - Vector((path_xy[i].x, path_xy[i].y, 0.0))
        loc = inverse @ moved
        for axis in range(3):
            key = next(k for k in curves[axis].keyframe_points if int(round(k.co[0])) == f)
            key.co[1] = loc[axis]
            key.handle_left[1] = key.handle_right[1] = loc[axis]
    for fc in curves.values(): fc.update()
    stem = os.path.splitext(os.path.basename(path))[0]
    action.name = stem
    after = bone_positions(arm, frames)
    travel_before = (Vector((before[-1]['hip'].x, before[-1]['hip'].y)) - Vector((before[1]['hip'].x, before[0]['hip'].y))).length
    travel_after = (Vector((after[-1]['hip'].x, after[-1]['hip'].y)) - Vector((after[1]['hip'].x, after[0]['hip'].y))).length
    reach = max(Vector((p['hip'].x, p['hip'].y)).length for p in after[1:])
    target = path if out_dir == '-' else os.path.join(out_dir, os.path.basename(path))
    temp = target + '.tmp.fbx'
    # Exported on the first frame, the T-pose, so the skeleton the file stores is the T-pose (see tpose_rest.py).
    bpy.context.scene.frame_set(frames[0])
    bpy.ops.export_scene.fbx(filepath=temp, object_types={'ARMATURE'}, add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.01)
    # Check the exported file: every bone where it is in the edited scene, relative to the hips.
    expected = after
    arm2, _ = load(temp)
    got = bone_positions(arm2, frames)
    worst = max((g[n] - g['hip'] - (e[n] - e['hip'])).length for g, e in zip(got, expected) for n in e if n in g)
    hip_err = max((g['hip'] - e['hip']).length for g, e in zip(got, expected))
    missing = [n for n in expected[0] if n not in got[0]]
    os.replace(temp, target)
    report.append(dict(file=stem, frames=len(frames), fps=fps, travel_before=round(travel_before, 3),
                       travel_after=round(travel_after, 3), max_offset=round(reach, 3), bone_error=round(worst, 5),
                       hip_error=round(hip_err, 5), missing=missing, height=round(min(p['hip'].z for p in after[1:]), 3)))
    print("RESULT", json.dumps(report[-1]), flush=True)

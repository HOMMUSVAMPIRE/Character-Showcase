"""
Procedurally authored animation clips for the mannequin: Idle, Walk, Run, Rise and Fall.

Poses are described as *world-space* (armature-space) rotations per bone, which are far easier to reason about than
each bone's local axes, and converted to Blender's bone-local pose rotations here. All clips loop, stay in place
(no root travel), and start with the left foot planted forward so blend trees keep the feet in phase.

World axes (Blender, before export): +X = the character's left, -Y = forward, +Z = up.
Sign conventions for the rotation helpers used below:
  - Legs/arms hanging down: a NEGATIVE rotation about X swings them FORWARD.
  - Knees: a POSITIVE rotation about X (relative to the thigh) bends the shin backward.
  - Elbows: a NEGATIVE rotation about X (relative to the upper arm) bends the forearm forward.
  - Feet: a POSITIVE rotation about X points the toes down.
  - Spine: a POSITIVE rotation about X leans forward. A POSITIVE rotation about Z turns the left side backward.
  - Arms: a POSITIVE rotation about Y lowers the left arm from the T-pose; NEGATIVE lowers the right arm.
"""

import math

from mathutils import Matrix, Quaternion, Vector

FPS = 60
TAU = math.pi * 2.0


def rx(deg):
    return Quaternion((1.0, 0.0, 0.0), math.radians(deg))


def ry(deg):
    return Quaternion((0.0, 1.0, 0.0), math.radians(deg))


def rz(deg):
    return Quaternion((0.0, 0.0, 1.0), math.radians(deg))


def positive(x):
    return max(0.0, x)


class Pose:
    """World-space rotation (about each bone's head) per bone, plus a world-space offset for the hips."""

    def __init__(self):
        self.rotations = {}
        self.hips_offset = Vector((0.0, 0.0, 0.0))
        self.leg_angles = {}  # side -> (thigh, knee) in degrees, recorded by legs() for grounding.

    def set(self, bone, rotation):
        self.rotations[bone] = rotation


def legs(pose, side, thigh, knee, foot):
    """Thigh swing, knee bend and foot pitch in degrees. Each child includes its parent's rotation."""
    thigh_q = rx(thigh)
    shin_q = rx(thigh + knee)
    foot_q = rx(foot)  # Feet are kept in world orientation so they stay roughly level, plus pitch.
    pose.set(f"{side}UpperLeg", thigh_q)
    pose.set(f"{side}LowerLeg", shin_q)
    pose.set(f"{side}Foot", foot_q)
    pose.set(f"{side}Toes", foot_q)
    pose.leg_angles[side] = (thigh, knee)


# Leg segment lengths, matching the armature in generate_mannequin.py (hip 0.93 -> knee 0.52 -> ankle 0.09).
THIGH_LENGTH = 0.41
SHIN_LENGTH = 0.43


def ground(pose, lift=0.0):
    """
    Drops the hips so the lower ankle sits exactly where it does in the rest pose, so the planted foot stays on
    the ground however the knees bend. The vertical bob of walking and running falls out of this naturally.
    """
    extents = [THIGH_LENGTH * math.cos(math.radians(t)) + SHIN_LENGTH * math.cos(math.radians(t + k))
               for t, k in pose.leg_angles.values()]
    drop = (THIGH_LENGTH + SHIN_LENGTH) - max(extents)
    pose.hips_offset = Vector((0.0, 0.0, lift - drop))


def arms(pose, side, lower, swing, elbow, shoulder_lift=0.0):
    """Lower the arm from the T-pose by `lower` degrees, swing it about X, and bend the elbow forward."""
    down = ry(lower if side == "Left" else -lower)
    lift = ry(-shoulder_lift if side == "Left" else shoulder_lift)
    pose.set(f"{side}Shoulder", lift)
    pose.set(f"{side}UpperArm", rx(swing) @ down)
    pose.set(f"{side}LowerArm", rx(swing - elbow) @ down)
    pose.set(f"{side}Hand", rx(swing - elbow) @ down)


def torso(pose, hips_twist=0.0, hips_lean=0.0, spine_lean=0.0, chest_twist=0.0, chest_lean=0.0, head_lean=0.0,
          head_turn=0.0, roll=0.0):
    pose.set("Hips", rz(hips_twist) @ rx(hips_lean))
    pose.set("Spine", rz(hips_twist * 0.5) @ rx(spine_lean) @ ry(roll))
    pose.set("Chest", rz(chest_twist) @ rx(spine_lean + chest_lean) @ ry(roll))
    pose.set("UpperChest", rz(chest_twist) @ rx(spine_lean + chest_lean) @ ry(roll))
    pose.set("Neck", rz(chest_twist * 0.5 + head_turn * 0.5) @ rx((spine_lean + chest_lean) * 0.5 + head_lean * 0.5))
    pose.set("Head", rz(head_turn) @ rx(head_lean))


# ---------------------------------------------------------------------------------------------------------------
# Clips. Each takes a phase in [0, 1) and returns a Pose. Phase 0 = left foot planted forward.
# ---------------------------------------------------------------------------------------------------------------

def idle(p):
    pose = Pose()
    breath = math.sin(TAU * p)
    sway = math.sin(TAU * p + 0.6)
    torso(pose, spine_lean=1.0 + 0.8 * breath, chest_lean=-1.2 * breath, head_lean=-1.0 - 0.5 * breath,
          head_turn=2.0 * math.sin(TAU * p * 1.0 + 1.3), roll=0.8 * sway)
    legs(pose, "Left", thigh=-3.0, knee=5.0, foot=-2.0)
    legs(pose, "Right", thigh=-1.0, knee=3.0, foot=-2.0)
    arms(pose, "Left", lower=78.0 + 1.5 * breath, swing=-4.0, elbow=10.0)
    arms(pose, "Right", lower=78.0 + 1.5 * breath, swing=-4.0, elbow=10.0)
    ground(pose, lift=-0.004 * breath)
    return pose


def walk(p):
    pose = Pose()
    c = math.cos(TAU * p)
    torso(pose, hips_twist=-6.0 * c, spine_lean=3.0, chest_twist=5.0 * c, head_turn=-1.0 * c)
    for side, offset in (("Left", 0.0), ("Right", 0.5)):
        q = (p + offset) % 1.0
        thigh = -27.0 * math.cos(TAU * q)
        # Swing phase is the second half of each leg's cycle: bend the knee to clear the ground.
        knee = 6.0 + 55.0 * positive(math.sin(TAU * (q - 0.5))) + 8.0 * positive(math.sin(TAU * q))
        foot = 22.0 * positive(math.sin(TAU * (q - 0.35))) - 6.0 * positive(math.cos(TAU * q))
        legs(pose, side, thigh, knee, foot)
    arms(pose, "Left", lower=74.0, swing=20.0 * c, elbow=12.0 + 6.0 * positive(-c))
    arms(pose, "Right", lower=74.0, swing=-20.0 * c, elbow=12.0 + 6.0 * positive(c))
    ground(pose)
    return pose


def run(p):
    pose = Pose()
    c = math.cos(TAU * p)
    torso(pose, hips_twist=-10.0 * c, spine_lean=13.0, chest_twist=9.0 * c, head_lean=-9.0, head_turn=-2.0 * c)
    for side, offset in (("Left", 0.0), ("Right", 0.5)):
        q = (p + offset) % 1.0
        thigh = -45.0 * math.cos(TAU * q) - 8.0
        knee = 18.0 + 95.0 * positive(math.sin(TAU * (q - 0.45))) + 14.0 * positive(math.sin(TAU * q))
        foot = 32.0 * positive(math.sin(TAU * (q - 0.3))) - 8.0 * positive(math.cos(TAU * q))
        legs(pose, side, thigh, knee, foot)
    arms(pose, "Left", lower=70.0, swing=42.0 * c, elbow=80.0 + 12.0 * positive(-c))
    arms(pose, "Right", lower=70.0, swing=-42.0 * c, elbow=80.0 + 12.0 * positive(c))
    # Running has a flight phase between steps: lift the body a little after each push-off.
    ground(pose, lift=0.035 * positive(math.sin(2.0 * TAU * (p - 0.1))))
    return pose


def rise(p):
    pose = Pose()
    w = math.sin(TAU * p)
    torso(pose, spine_lean=6.0, chest_lean=-4.0, head_lean=-6.0, chest_twist=3.0)
    legs(pose, "Left", thigh=-62.0 + 3.0 * w, knee=95.0, foot=20.0)
    legs(pose, "Right", thigh=8.0 - 3.0 * w, knee=38.0, foot=30.0)
    arms(pose, "Left", lower=45.0, swing=25.0, elbow=45.0)
    arms(pose, "Right", lower=40.0 + 3.0 * w, swing=-45.0, elbow=55.0)
    return pose


def fall(p):
    pose = Pose()
    w = math.sin(TAU * p)
    torso(pose, spine_lean=-4.0, chest_lean=2.0, head_lean=4.0)
    legs(pose, "Left", thigh=-24.0 + 4.0 * w, knee=32.0, foot=8.0)
    legs(pose, "Right", thigh=-10.0 - 4.0 * w, knee=22.0, foot=12.0)
    arms(pose, "Left", lower=22.0 - 6.0 * w, swing=-12.0, elbow=25.0, shoulder_lift=6.0)
    arms(pose, "Right", lower=22.0 + 6.0 * w, swing=-12.0, elbow=25.0, shoulder_lift=6.0)
    return pose


# name -> (pose function, duration in seconds). Durations are chosen so stride length matches the speeds the
# Animator blend tree uses (Walk at ~1.6 m/s, Run at ~10 m/s).
CLIPS = {
    "Idle": (idle, 3.0),
    "Walk": (walk, 1.1),
    "Run": (run, 0.45),
    "Rise": (rise, 1.0),
    "Fall": (fall, 0.6),
}

SAMPLES_PER_SECOND = 30  # Keys per second of animation; FBX export bakes the in-between frames.


# ---------------------------------------------------------------------------------------------------------------

def apply_pose(rig, pose):
    """
    Converts world-space bone rotations to Blender pose-bone (local) rotations.

    For a bone with rest orientation R (armature space), parent world rotation Cp and desired world rotation C,
    the local pose rotation is  B = R^-1 · Cp^-1 · C · R.  Bones without an entry inherit their parent's rotation.
    """
    world = {}
    for bone in rig.data.bones:  # Parents always precede children in this list.
        parent_world = world.get(bone.parent.name, Quaternion()) if bone.parent else Quaternion()
        c = pose.rotations.get(bone.name, parent_world)
        world[bone.name] = c
        rest = bone.matrix_local.to_quaternion()
        local = rest.inverted() @ parent_world.inverted() @ c @ rest
        pb = rig.pose.bones[bone.name]
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = local
        pb.location = (0.0, 0.0, 0.0)

    hips = rig.data.bones["Hips"]
    rig.pose.bones["Hips"].location = hips.matrix_local.to_3x3().inverted() @ pose.hips_offset


def build_actions(rig):
    """Creates one Blender action per clip on the rig. Returns the action names."""
    import bpy

    bpy.context.scene.render.fps = FPS
    rig.animation_data_create()
    names = []
    for name, (fn, duration) in CLIPS.items():
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        rig.animation_data.action = action
        frames = max(2, round(duration * FPS))
        keys = max(4, round(duration * SAMPLES_PER_SECOND))
        for k in range(keys + 1):
            phase = (k / keys) % 1.0  # The last key repeats the first, so the loop is seamless.
            frame = 1 + frames * k / keys
            apply_pose(rig, fn(phase))
            for pb in rig.pose.bones:
                pb.keyframe_insert("rotation_quaternion", frame=frame, group=pb.name)
                if pb.name == "Hips":
                    pb.keyframe_insert("location", frame=frame, group=pb.name)
        action.frame_range = (1, 1 + frames)
        names.append(name)

    # Leave the rig in its rest pose with no active action, so the exported bind pose is the T-pose.
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = (0.0, 0.0, 0.0)
    return names

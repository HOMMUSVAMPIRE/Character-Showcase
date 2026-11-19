"""
Procedurally authored animation clips for the fox: Idle, Walk, Trot, Gallop, Jump Rise, Jump Fall, Crouch Idle, Crouch
Walk, Turn Step, Swim and Swim Idle.

As for the mannequin (see Tools/Mannequin/mannequin_animations.py), poses are world-space (armature-space) rotations
per bone, converted to Blender's bone-local pose rotations here. All clips loop and stay in place (no root travel).

World axes (Blender, before export): +X = the fox's left, -Y = forward, +Z = up.
Sign conventions for rotations about X (rx):
  - Legs hanging down: NEGATIVE swings them forward, POSITIVE back.
  - The body and head: POSITIVE pitches the nose down, NEGATIVE up.
  - The tail (pointing back): POSITIVE raises it.

The legs are posed by forward kinematics: each leg's world angles per segment (the upper leg's swing, then each
segment's bend added to its parent's). A gait gives each leg a phase; while its paw is planted the leg sweeps from
forward to back by the angle that carries the paw back the stance's distance at the clip's ground speed, and while it
swings it comes forward again, folded to lift the paw. The body is lowered so the planted paws stay on the ground.
"""

import math

from mathutils import Quaternion, Vector

# Shared with the other generators (Tools/Blender, which the generator puts on sys.path before importing this).
from blender_common import rotate_x, rx, rz, smoothstep  # noqa: E402

# Keys per second. The FBX bakes a key a frame, and a galloping leg landing straight flexes fast as the body comes down
# over it: between keys far apart, the joints' interpolated turns don't keep the paw off the floor, so the clips are
# keyed at twice the usual 60.
FPS = 120
SAMPLES_PER_SECOND = FPS
TAU = math.pi * 2.0
# A one-frame take holding the rest pose, named to sort first, so it's the FBX's current take (see the mannequin's).
BIND_POSE_TAKE = "!BindPose"


class Pose:
    """World-space rotation (about each bone's head) per bone, plus a world-space offset for the hips (the root)."""

    def __init__(self):
        self.rotations = {}
        self.hips_offset = Vector((0.0, 0.0, 0.0))

    def set(self, bone, rotation):
        self.rotations[bone] = rotation


# Each leg's segments at rest, (y, z) from its root (+y back, +z up), matching the armature in generate_fox.py.
FRONT_SEGMENTS = [(0.03, -0.22), (-0.02, -0.26), (-0.09, -0.07)]
HIND_SEGMENTS = [(-0.08, -0.22), (0.12, -0.20), (-0.03, -0.12), (-0.06, -0.015)]
FRONT_BONES = ["FrontUpperLeg", "FrontLowerLeg", "FrontPaw"]
HIND_BONES = ["HindUpperLeg", "HindLowerLeg", "HindFoot", "HindPaw"]


def leg_depth(segments, angles):
    """How far below the leg's root its lowest point is, for these world angles per segment."""
    y = z = 0.0
    lowest = 0.0
    for segment, angle in zip(segments, angles):
        dy, dz = rotate_x(segment, angle)
        y, z = y + dy, z + dz
        lowest = min(lowest, z)
    return -lowest


# The flex reach() scans (degrees): in steps of REACH_SCAN_STEP, up to REACH_MAX_FLEX.
REACH_SCAN_STEP = 1.0
REACH_MAX_FLEX = 120.0

# How far (radians) each tail bone's sway lags the one before it, so the sway ripples down the tail.
TAIL_LAG = 0.6

# A stride's swing lifts the front paw by these shares of its lift (lift_front, degrees): the upper leg swung back, the
# elbow flexing the forearm forward, the wrist folding the paw back.
FRONT_LIFT_SHOULDER = 0.5
FRONT_LIFT_ELBOW = 1.5
FRONT_LIFT_WRIST = 2.5

# How far (m) a stepping paw's mesh reaches below the end of its bones as the paw tilts: kept that much clear.
PAW_TILT_MARGIN = 0.015

FRONT_LENGTH = leg_depth(FRONT_SEGMENTS, [0.0] * 3)
HIND_LENGTH = leg_depth(HIND_SEGMENTS, [0.0] * 4)


def front_angles(swing, bend=0.0, paw=0.0):
    """World angles of a front leg's segments: the upper leg's swing, the lower's bend on it, and the paw's own."""
    return [swing, swing + bend, swing + bend + paw]


def hind_angles(swing, bend=0.0, foot=0.0, paw=0.0):
    """World angles of a hind leg's segments: thigh swing, shin bend on it, foot (hock to paw) bend, paw's own."""
    return [swing, swing + bend, swing + bend + foot, swing + bend + foot + paw]


def set_leg(pose, bones, side, angles):
    for bone, angle in zip(bones, angles):
        pose.set(f"{bone}_{side}", rx(angle))


class Leg:
    """A leg in a gait: front or hind, its side, its phase offset, and its base angles (bent while crouched)."""

    def __init__(self, front, side, offset):
        self.front = front
        self.side = side
        self.offset = offset


def sweep_angle(travel, length):
    """The swing (degrees) either side of straight down that carries the paw `travel` back while planted."""
    return math.degrees(math.asin(min(0.95, 0.5 * travel / length)))


class Gait:
    """
    A stepping cycle: each leg planted for `stance` of it, from its phase offset. Over the clip's duration the fox
    covers speed * duration; a planted paw is carried back the stance's share of that.
    """

    def __init__(self, speed, duration, stance, legs, lift_front=60.0, lift_hind=40.0, crouch=False, spine_flex=0.0,
                 bob=0.01, head_bob=2.0, tail_sway=10.0, neck=0.0, head=0.0, ears=0.0):
        self.speed = speed
        self.duration = duration
        self.stance = stance
        self.legs = legs
        self.lift_front = lift_front
        self.lift_hind = lift_hind
        self.crouch = crouch
        self.spine_flex = spine_flex
        self.bob = bob
        self.head_bob = head_bob
        # The tail's sway side to side, once a stride (degrees, its amplitude: see body), counterbalancing the legs; the
        # neck lowered forward, the head's nod (positive nose down) and the ears turned (negative back), held through it.
        self.tail_sway = tail_sway
        self.neck = neck
        self.head = head
        self.ears = ears
        travel = speed * duration * stance
        self.front_sweep = sweep_angle(travel, FRONT_LENGTH)
        self.hind_sweep = sweep_angle(travel, HIND_LENGTH)
        # The body's height (below standing) for the gait: low enough that a leg reaches the ground straight at the ends
        # of its stride (touching down and pushing off). Between them a planted leg flexes to stay on the ground (see
        # reach), rather than the body bobbing as legs land and lift.
        front_base = CROUCH_FRONT if crouch else (0.0, 0.0, 0.0)
        hind_base = CROUCH_HIND if crouch else (0.0, 0.0, 0.0, 0.0)
        self.drop = max(
            FRONT_LENGTH - leg_depth(FRONT_SEGMENTS, front_angles(front_base[0] - self.front_sweep, *front_base[1:])),
            HIND_LENGTH - leg_depth(HIND_SEGMENTS, hind_angles(hind_base[0] - self.hind_sweep, *hind_base[1:])))

    def leg_angles(self, leg, q):
        """The leg's world angles at its own phase q (0 at touch-down, in front)."""
        sweep = self.front_sweep if leg.front else self.hind_sweep
        if q < self.stance:
            swing = -sweep + 2.0 * sweep * (q / self.stance)
            lift = 0.0
        else:
            t = (q - self.stance) / (1.0 - self.stance)
            swing = sweep - 2.0 * sweep * smoothstep(0.0, 1.0, t)
            lift = math.sin(math.pi * t)
        if leg.front:
            base = CROUCH_FRONT if self.crouch else (0.0, 0.0, 0.0)
            # A fox's front leg folds as its elbow points: back. Lifting, the upper leg swings back, the elbow flexes the
            # forearm forward and up, and the wrist folds the paw back under it (never the forearm swung back, as a knee).
            f = self.lift_front * lift
            return front_angles(base[0] + swing + FRONT_LIFT_SHOULDER * f, base[1] - FRONT_LIFT_ELBOW * f,
                                base[2] + FRONT_LIFT_WRIST * f)
        base = CROUCH_HIND if self.crouch else (0.0, 0.0, 0.0, 0.0)
        return hind_angles(base[0] + swing - 0.3 * self.lift_hind * lift, base[1] + self.lift_hind * lift,
                           base[2] - 1.2 * self.lift_hind * lift, base[3] + 0.5 * self.lift_hind * lift)

    def __call__(self, p):
        pose = Pose()
        # The body at its gait's height, bobbing smoothly; the legs reaching the ground from there.
        height = -self.drop + self.bob * math.cos(2.0 * TAU * p)
        for leg in self.legs:
            q = (p + leg.offset) % 1.0
            angles = reach(leg.front, self.leg_angles(leg, q), (FRONT_LENGTH if leg.front else HIND_LENGTH) + height - PAW_TILT_MARGIN,
                           planted=q < self.stance)
            set_leg(pose, FRONT_BONES if leg.front else HIND_BONES, leg.side, angles)
        flex = self.spine_flex * math.cos(TAU * p)
        body(pose, crouch=self.crouch, spine=flex, head=self.head + self.head_bob * math.sin(2.0 * TAU * p), neck=self.neck,
             tail_sway=self.tail_sway, tail_phase=TAU * p, tail_lift=4.0 if not self.crouch else -8.0, ears=self.ears)
        pose.hips_offset = Vector((0.0, 0.0, height))
        return pose


def leg_end(segments, angles):
    """Where the end of the leg is, (y, z) from its root, for these world angles per segment."""
    y = z = 0.0
    for segment, angle in zip(segments, angles):
        dy, dz = rotate_x(segment, angle)
        y, z = y + dy, z + dz
    return y, z


def flexed(front, angles, flex):
    """The leg's angles flexed by `flex` degrees more at its middle joint (the front leg's elbow, flexing it forward with a
    negative flex; the hind leg's stifle, back with a positive one,
    its foot turned back as much, so it stays as it was), then the whole leg turned so its end is in the same direction
    from its root as before: flexing only draws the paw in toward the shoulder or hip, never swings it about. (Flexing
    the middle joint alone, a leg swung forward first reaches deeper as its lower half swings toward straight down, so
    the least flex that clears the ground jumps from none to most of a right angle: the joint snapped between keys.)"""
    bent = list(angles)
    bent[1] += flex
    if not front:
        bent[2] -= flex
    segments = FRONT_SEGMENTS if front else HIND_SEGMENTS
    by, bz = leg_end(segments, angles)
    ay, az = leg_end(segments, bent)
    turn = (math.degrees(math.atan2(bz, by) - math.atan2(az, ay)) + 180.0) % 360.0 - 180.0
    return [angle + turn for angle in bent]


def reach(front, angles, height, planted):
    """
    The leg's angles flexed (see flexed) so its paw is no lower than the ground, its root `height` above it: a leg that
    would go through the ground is flexed until it doesn't (a planted one so its paw is on it); one short enough, swinging
    or straight at the end of its stride, is left as it is.
    """
    segments = FRONT_SEGMENTS if front else HIND_SEGMENTS
    if leg_depth(segments, angles) <= height:
        return angles  # Short enough already (a planted leg straight at the end of its stride just reaches).
    # Flexed the way the joint folds: a front leg's elbow points back (the forearm flexes forward), a hind leg's stifle
    # forward (the shin flexes back). Always that way, never chosen key by key (by whichever way shortens it more, say):
    # near straight, that flips from one key to the next, and the joint snaps across between them, sweeping the paw
    # through the floor; and flexed the other way, a front leg bends like a knee.
    sign = -1.0 if front else 1.0
    # The least flex that clears the ground: scanned up from none (a leg's depth needn't fall steadily as it flexes, so
    # a bisection over the whole range can land past a dip), then narrowed between the last step short of it and the first
    # past it. The least is continuous from one key to the next wherever the leg's depth is, so the joint never snaps.
    low = high = None
    previous = 0.0
    flex = REACH_SCAN_STEP
    while flex <= REACH_MAX_FLEX:
        if leg_depth(segments, flexed(front, angles, sign * flex)) <= height:
            low, high = previous, flex
            break
        previous = flex
        flex += REACH_SCAN_STEP
    if high is None:
        high = REACH_MAX_FLEX  # It can't reach: as near as it gets.
        low = high
    for _ in range(16):
        mid = 0.5 * (low + high)
        if leg_depth(segments, flexed(front, angles, sign * mid)) > height:
            low = mid
        else:
            high = mid
    return flexed(front, angles, sign * high)


def body(pose, crouch=False, spine=0.0, pitch=0.0, head=0.0, head_turn=0.0, tail_sway=0.0, tail_lift=0.0, ears=0.0,
         neck=0.0, tail_phase=None):
    """
    The spine, neck, head and tail: the spine flexing (positive arches it), the head nodding (positive nose down), the
    neck lowered forward (positive), the ears turned (negative folds them back), the tail swaying side to side. With a
    tail_phase (radians), tail_sway is the sway's amplitude, as a wave down the tail: each bone a little behind the one
    before it (TAIL_LAG), so it ripples rather than swinging stiffly; without, each bone is turned by its share of it.
    """
    pose.set("Hips", rx(pitch + spine))
    pose.set("Spine1", rx(pitch))
    pose.set("Spine2", rx(pitch - spine))
    neck_angle = (22.0 if crouch else 0.0) + pitch + neck
    pose.set("Neck", rz(head_turn * 0.5) @ rx(neck_angle + head * 0.5))
    pose.set("Head", rz(head_turn) @ rx((-12.0 if crouch else 0.0) + pitch + head))
    for i, bone in enumerate(("Tail1", "Tail2", "Tail3", "Tail4")):
        share = 0.5 + 0.4 * i
        sway = tail_sway * share * (math.sin(tail_phase - TAIL_LAG * i) if tail_phase is not None else 1.0)
        pose.set(bone, rz(sway) @ rx(tail_lift * (1.0 - 0.15 * i)))
    for side, sx in (("L", 1.0), ("R", -1.0)):
        pose.set(f"Ear_{side}", rx(ears))


# Crouched (stalking): front legs bent back at the shoulder and forward at the elbow, hind legs folded, the body low.
CROUCH_FRONT = (45.0, -95.0, 50.0)
CROUCH_HIND = (-38.0, 72.0, -54.0, 20.0)
CROUCH_DROP = max(FRONT_LENGTH - leg_depth(FRONT_SEGMENTS, front_angles(*CROUCH_FRONT)),
                  HIND_LENGTH - leg_depth(HIND_SEGMENTS, hind_angles(*CROUCH_HIND)))


def stand(pose, crouch=False, height=0.0):
    """All four legs planted under the body (crouched or not), reaching the ground from `height` (below standing)."""
    for side in ("L", "R"):
        set_leg(pose, FRONT_BONES, side, reach(True, front_angles(*(CROUCH_FRONT if crouch else (0.0, 0.0, 0.0))),
                                               FRONT_LENGTH + height, planted=True))
        set_leg(pose, HIND_BONES, side, reach(False, hind_angles(*(CROUCH_HIND if crouch else (0.0, 0.0, 0.0, 0.0))),
                                              HIND_LENGTH + height, planted=True))


# ---------------------------------------------------------------------------------------------------------------
# Clips. Each takes a phase in [0, 1) and returns a Pose.
# ---------------------------------------------------------------------------------------------------------------

def idle(p):
    pose = Pose()
    breath = math.sin(TAU * p)
    stand(pose)
    body(pose, spine=0.8 * breath, head=-1.5 * breath, head_turn=4.0 * math.sin(TAU * p + 1.0),
         tail_sway=10.0 * math.sin(TAU * p + 0.5), tail_lift=2.0, ears=3.0 * math.sin(2.0 * TAU * p))
    pose.hips_offset = Vector((0.0, 0.0, 0.004 * breath))
    return pose


def crouch_idle(p):
    pose = Pose()
    breath = math.sin(TAU * p)
    stand(pose, crouch=True, height=-CROUCH_DROP)
    body(pose, crouch=True, spine=0.5 * breath, head=-1.0 * breath, tail_sway=4.0 * math.sin(TAU * p), tail_lift=-8.0)
    pose.hips_offset = Vector((0.0, 0.0, -CROUCH_DROP + 0.003 * breath))
    return pose


def jump_rise(p):
    """Leaving the ground: nose up, the front legs tucked (elbows back, forearms forward, paws folded back), the hind legs
    stretched back from the push."""
    pose = Pose()
    wave = math.sin(TAU * p)
    for side in ("L", "R"):
        set_leg(pose, FRONT_BONES, side, front_angles(35.0, -115.0, 140.0))
        set_leg(pose, HIND_BONES, side, hind_angles(45.0, 10.0, 0.0, 30.0))
    body(pose, pitch=-12.0, spine=-4.0, tail_lift=12.0 + 2.0 * wave, ears=-10.0)
    return pose


def jump_fall(p):
    """Coming down: nose a little down, the front legs reaching forward and down for the landing, the hind tucked."""
    pose = Pose()
    wave = math.sin(TAU * p)
    for side in ("L", "R"):
        set_leg(pose, FRONT_BONES, side, front_angles(-25.0, -5.0, -10.0))
        set_leg(pose, HIND_BONES, side, hind_angles(-25.0, 35.0, -30.0, 10.0))
    body(pose, pitch=6.0, spine=3.0, tail_lift=8.0 + 2.0 * wave, ears=-6.0)
    return pose


class Paddle:
    """
    Swimming: a dog paddle at the surface, the legs in diagonal pairs (as a trot). Each front leg reaches forward and
    pulls down and back through the water, then folds up (the elbow flexing the forearm forward, the paw folded back
    under it) to reach forward again; each hind leg kicks back straight and draws up bent. The body is level and a
    little nose up, the neck raised and the head looking ahead, the tail floating out behind. The fox's feet (its
    origin) are the swim depth below the surface (Swimming), so the body is drawn at its standing height above them.
    `effort` scales the strokes: treading water, they're small.
    """

    def __init__(self, duration, effort):
        self.duration = duration
        self.effort = effort

    def __call__(self, p):
        pose = Pose()
        e = self.effort
        for leg, offset in ((Leg(True, "L", 0.0), 0.0), (Leg(True, "R", 0.5), 0.5), (Leg(False, "L", 0.5), 0.5), (Leg(False, "R", 0.0), 0.0)):
            phi = TAU * ((p + offset) % 1.0)
            # Pulling back while cos(phi) > 0 (the leg swinging back), recovering forward folded while it's < 0.
            fold = max(0.0, -math.cos(phi))
            if leg.front:
                angles = front_angles(-5.0 + 40.0 * e * math.sin(phi), -80.0 * e * fold, 100.0 * e * fold + 15.0)
            else:
                angles = hind_angles(10.0 + 30.0 * e * math.sin(phi + math.pi), 60.0 * e * fold + 10.0, -45.0 * e * fold - 10.0,
                                     25.0 * e * fold + 20.0)
            set_leg(pose, FRONT_BONES if leg.front else HIND_BONES, leg.side, angles)
        body(pose, pitch=-5.0, spine=1.5 * e * math.sin(2.0 * TAU * p), tail_sway=6.0 * math.sin(TAU * p), tail_lift=12.0,
             ears=-4.0)
        # The neck raised, the head looking ahead (nose a little up), clear of the water.
        pose.set("Neck", rx(-30.0))
        pose.set("Head", rx(-8.0 + 2.0 * e * math.sin(2.0 * TAU * p)))
        pose.hips_offset = Vector((0.0, 0.0, 0.006 * e * math.cos(2.0 * TAU * p)))
        return pose


SWIM = Paddle(duration=0.6, effort=1.0)
SWIM_IDLE = Paddle(duration=1.0, effort=0.45)
# The speed (m/s) the swimming paddle is authored for (the fox's Swim Speed): FoxGenerator's Swim blend matches it.
SWIM_SPEED = 2.5


def legs(*offsets):
    """Left front, right front, left hind, right hind, with these phase offsets."""
    lf, rf, lh, rh = offsets
    return [Leg(True, "L", lf), Leg(True, "R", rf), Leg(False, "L", lh), Leg(False, "R", rh)]


# A walk: four beats, each hind leg followed by the front on its side (lateral sequence).
WALK = Gait(speed=1.3, duration=0.7, stance=0.65, legs=legs(0.75, 0.25, 0.0, 0.5), bob=0.008, head_bob=2.0, tail_sway=14.0)
# A trot: diagonal pairs together.
TROT = Gait(speed=3.5, duration=0.36, stance=0.45, legs=legs(0.0, 0.5, 0.5, 0.0), bob=0.015, head_bob=1.5, tail_sway=11.0)
# A gallop (rotary): the hind legs, then the front, each pair a little apart; the spine flexing with the stride. Running
# flat out: the neck lowered and stretched forward, the head looking ahead, the ears folded back flat, the tail streaming
# out behind, barely swaying.
GALLOP = Gait(speed=10.0, duration=0.3, stance=0.2, legs=legs(0.5, 0.6, 0.1, 0.0), lift_front=75.0, lift_hind=55.0,
              spine_flex=7.0, bob=0.03, head_bob=4.0, tail_sway=2.0, neck=16.0, head=5.0, ears=-55.0)
# Stalking: the walk, crouched.
CROUCH_WALK = Gait(speed=1.0, duration=0.8, stance=0.65, legs=legs(0.75, 0.25, 0.0, 0.5), lift_front=35.0, lift_hind=25.0,
                   crouch=True, bob=0.004, head_bob=0.5, tail_sway=8.0)

# Stepping in place, to turn on the spot: the trot's diagonal pairs, slower, with small lifts and no stride. Played as the
# fox turns standing (by its turn speed), its paws lift and are put down again where the turning body has them, so it
# steps round (LegPlacementIK locks each where it's put down).
TURN_STEP = Gait(speed=0.0, duration=0.5, stance=0.5, legs=legs(0.0, 0.5, 0.5, 0.0), lift_front=35.0, lift_hind=25.0,
                 bob=0.006, head_bob=1.0, tail_sway=8.0)

# Clip name -> (pose function of phase, duration in seconds). Speeds (m/s) the gaits are authored for are in SPEEDS.
CLIPS = {
    "Idle": (idle, 2.4),
    "Walk": (WALK, WALK.duration),
    "Trot": (TROT, TROT.duration),
    "Gallop": (GALLOP, GALLOP.duration),
    "Jump Rise": (jump_rise, 1.0),
    "Jump Fall": (jump_fall, 1.0),
    "Crouch Idle": (crouch_idle, 2.4),
    "Crouch Walk": (CROUCH_WALK, CROUCH_WALK.duration),
    "Turn Step": (TURN_STEP, TURN_STEP.duration),
    "Swim": (SWIM, SWIM.duration),
    "Swim Idle": (SWIM_IDLE, SWIM_IDLE.duration),
}
SPEEDS = {"Walk": WALK.speed, "Trot": TROT.speed, "Gallop": GALLOP.speed, "Crouch Walk": CROUCH_WALK.speed, "Swim": SWIM_SPEED}


# ---------------------------------------------------------------------------------------------------------------

def apply_pose(rig, pose):
    """
    Converts world-space bone rotations to Blender pose-bone (local) rotations: for a bone with rest orientation R,
    parent world rotation Cp and desired world rotation C, the local rotation is R^-1 · Cp^-1 · C · R. Bones without
    an entry inherit their parent's rotation.
    """
    world = {}
    for bone in rig.data.bones:  # Parents always precede children in this list.
        parent_world = world.get(bone.parent.name, Quaternion()) if bone.parent else Quaternion()
        c = pose.rotations.get(bone.name, parent_world)
        world[bone.name] = c
        rest = bone.matrix_local.to_quaternion()
        pb = rig.pose.bones[bone.name]
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = rest.inverted() @ parent_world.inverted() @ c @ rest
        pb.location = (0.0, 0.0, 0.0)
    hips = rig.data.bones["Hips"]
    rig.pose.bones["Hips"].location = hips.matrix_local.to_3x3().inverted() @ pose.hips_offset


def reset_pose(rig):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = Quaternion()
        pb.location = (0.0, 0.0, 0.0)


def build_actions(rig):
    """Creates one Blender action per clip on the rig (and the bind pose take). Returns the clips' names."""
    import bpy

    bpy.context.scene.render.fps = FPS
    rig.animation_data_create()

    bind_pose = bpy.data.actions.new(BIND_POSE_TAKE)
    bind_pose.use_fake_user = True
    rig.animation_data.action = bind_pose
    reset_pose(rig)
    for pb in rig.pose.bones:
        for frame in (1, 2):
            pb.keyframe_insert("rotation_quaternion", frame=frame, group=pb.name)
            pb.keyframe_insert("location", frame=frame, group=pb.name)
    bind_pose.frame_range = (1, 2)
    if sorted(a.name for a in bpy.data.actions)[0] != BIND_POSE_TAKE:
        raise RuntimeError(f"'{BIND_POSE_TAKE}' must be the first action by name, so it's the FBX's current take.")

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

    rig.animation_data.action = None
    reset_pose(rig)
    return names

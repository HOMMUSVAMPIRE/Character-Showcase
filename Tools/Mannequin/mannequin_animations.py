"""
Procedurally authored animation clips for the mannequin: Idle, Walk, Run, Sprint, Rise, Fall, Dash, Crouch, Hang and Wall Slide.

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
    pose.leg_angles[side] = (thigh, knee, foot)


# Leg geometry, matching the armature in generate_mannequin.py (hips bone head 0.95, hip joints 0.93 -> knee 0.52 ->
# ankle 0.09). Points in the leg's plane are (y, z): +y is backward, +z is up.
HIPS_HEAD = Vector((0.0, 0.0, 0.95))
HIP_JOINT = {"Left": Vector((0.10, 0.0, 0.93)), "Right": Vector((-0.10, 0.0, 0.93))}
THIGH_LENGTH = 0.41
SHIN_LENGTH = 0.43
ANKLE_HEIGHT = 0.09
# Where the sole meets the ground, relative to the ankle: under the heel, and under the ball of the foot.
HEEL = (0.03, -ANKLE_HEIGHT)
BALL = (-0.13, -ANKLE_HEIGHT)


def rotate_x(point, deg):
    """Rotates a (y, z) point about X, the same way rx() does."""
    a = math.radians(deg)
    y, z = point
    return (y * math.cos(a) - z * math.sin(a), y * math.sin(a) + z * math.cos(a))


def sole_depth(thigh, knee, foot):
    """How far below the hip joint the lowest point of the sole is, for the given leg angles."""
    ankle_z = -(THIGH_LENGTH * math.cos(math.radians(thigh)) + SHIN_LENGTH * math.cos(math.radians(thigh + knee)))
    return -(ankle_z + min(rotate_x(HEEL, foot)[1], rotate_x(BALL, foot)[1]))


REST_SOLE_DEPTH = sole_depth(0.0, 0.0, 0.0)


def ground(pose, lift=0.0):
    """
    Drops the hips so the lowest point of the lower foot sits on the ground, as it does in the rest pose, so the
    planted foot stays on the ground however the knees bend and the feet pitch. For poses without stepping (idle).
    """
    depth = max(sole_depth(*angles) for angles in pose.leg_angles.values())
    pose.hips_offset = Vector((0.0, 0.0, lift + depth - REST_SOLE_DEPTH))


def hip_joint(pose, side):
    """The hip joint's (y, z) position, after the hips have been offset and rotated."""
    hips = pose.rotations.get("Hips", Quaternion())
    joint = HIPS_HEAD + pose.hips_offset + hips @ (HIP_JOINT[side] - HIPS_HEAD)
    return joint.y, joint.z


def solve_leg(hip, ankle):
    """Two-bone IK in the leg's plane: the thigh and knee angles (degrees) that put the ankle at `ankle`."""
    dy, dz = ankle[0] - hip[0], ankle[1] - hip[1]
    reach = THIGH_LENGTH + SHIN_LENGTH
    d = min(math.hypot(dy, dz), reach - 1e-4)

    def angle(adjacent_a, adjacent_b, opposite):
        cos = (adjacent_a ** 2 + adjacent_b ** 2 - opposite ** 2) / (2.0 * adjacent_a * adjacent_b)
        return math.degrees(math.acos(max(-1.0, min(1.0, cos))))

    knee = 180.0 - angle(THIGH_LENGTH, SHIN_LENGTH, d)
    # The knee bends forward of the hip-to-ankle line, so the thigh swings forward (negative) of it.
    thigh = math.degrees(math.atan2(dy, -dz)) - angle(THIGH_LENGTH, d, SHIN_LENGTH)
    return thigh, knee


def curve(keys, x, periodic=True):
    """
    Smooth (C1 cubic Hermite) interpolation through (x, value) keys sorted by x. Tangents come from each key's
    neighbours. Periodic curves wrap at 1, so the first key also follows the last.
    """
    if periodic:
        keys = [(keys[-1][0] - 1.0, keys[-1][1])] + list(keys) + [(keys[0][0] + 1.0, keys[0][1]),
                                                                   (keys[1][0] + 1.0, keys[1][1])]
        x %= 1.0
        if x < keys[1][0]:
            x += 1.0
    i = max(0, min(len(keys) - 2, next((k for k in range(len(keys) - 1) if x < keys[k + 1][0]), len(keys) - 2)))

    def tangent(k):
        a, b = keys[max(0, k - 1)], keys[min(len(keys) - 1, k + 1)]
        return (b[1] - a[1]) / (b[0] - a[0])

    (x0, v0), (x1, v1) = keys[i], keys[i + 1]
    h = x1 - x0
    t = (x - x0) / h
    t2, t3 = t * t, t * t * t
    return ((2 * t3 - 3 * t2 + 1) * v0 + (t3 - 2 * t2 + t) * h * tangent(i)
            + (-2 * t3 + 3 * t2) * v1 + (t3 - t2) * h * tangent(i + 1))


class Gait:
    """
    A stepping cycle (walk or run) whose planted feet never slide. Each leg's phase q runs from 0 (the foot touches
    down in front) to 1. While the foot is planted (q < stance), the point it pivots on moves straight back at the
    clip's ground speed, and the leg reaches it by IK, rolling heel-to-toe as the foot pitches. While the foot
    swings, the leg follows hand-authored keys, joined smoothly onto the stance at touch-down and push-off.

    The clip's duration follows from the speed: the foot's travel while planted divided by the time it is planted.
    """

    def __init__(self, speed, stance, contact_y, push_off_y, foot_keys, swing_keys, hips_height, torso, arms):
        self.speed = speed
        self.stance = stance  # Fraction of each leg's cycle with the foot planted
        self.contact_y = contact_y  # Where the flat foot's ankle would be (y) at touch-down and push-off
        self.push_off_y = push_off_y
        self.foot_keys = foot_keys  # (q, foot pitch) over the whole cycle
        self.swing_keys = swing_keys  # (q, thigh, knee) while swinging
        self.hips_height = hips_height  # p -> hips offset in meters
        self.torso = torso  # (pose, p) -> None
        self.arms = arms  # (pose, p) -> None
        self.duration = (push_off_y - contact_y) / (stance * speed)

    def body(self, p):
        """The pose without the legs: torso, arms and hips height, which the legs are solved against."""
        pose = Pose()
        self.torso(pose, p)
        self.arms(pose, p)
        pose.hips_offset = Vector((0.0, 0.0, self.hips_height(p % 1.0)))
        return pose

    def planted_leg(self, side, p, q):
        """Thigh and knee for a planted foot, by IK onto where the foot pivots on the ground."""
        s = q / self.stance
        pitch = curve(self.foot_keys, q)
        flat_ankle = (self.contact_y + (self.push_off_y - self.contact_y) * s, ANKLE_HEIGHT)
        # Roll on the heel while the toes are up, and on the ball of the foot once the heel lifts. Both pivots are
        # on the ground, so the ankle rises around whichever one is down.
        pivot = HEEL if pitch < 0.0 else BALL
        turned = rotate_x(pivot, pitch)
        ankle = (flat_ankle[0] + pivot[0] - turned[0], flat_ankle[1] + pivot[1] - turned[1])
        return solve_leg(hip_joint(self.body(p), side), ankle)

    def leg(self, side, p, q):
        if q < self.stance:
            return self.planted_leg(side, p, q)
        # Join the swing keys onto the planted leg at push-off and touch-down, with neighbouring planted samples
        # so the joins are smooth rather than just continuous.
        edge = 0.02
        start, end = p - q + self.stance, p - q + 1.0  # This leg's push-off and next touch-down, in clip phase
        before = [(self.stance - edge, self.planted_leg(side, start - edge, self.stance - edge)),
                  (self.stance, self.planted_leg(side, start, self.stance))]
        after = [(1.0, self.planted_leg(side, end, 0.0)), (1.0 + edge, self.planted_leg(side, end + edge, edge))]
        keys = before + [(k[0], (k[1], k[2])) for k in self.swing_keys] + after
        thigh = curve([(x, v[0]) for x, v in keys], q, periodic=False)
        knee = curve([(x, v[1]) for x, v in keys], q, periodic=False)
        return thigh, knee

    def __call__(self, p):
        pose = self.body(p)
        for side, offset in (("Left", 0.0), ("Right", 0.5)):
            q = (p + offset) % 1.0
            thigh, knee = self.leg(side, p, q)
            legs(pose, side, thigh, knee, curve(self.foot_keys, q))
        return pose


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


def swinging_arms(lower, swing, forward_bias, elbow, elbow_forward, phase=0.0):
    """Arms swinging opposite the legs: the left arm is furthest back at p = phase (by default while the left foot is
    planted in front, p = 0), as the left thigh is furthest forward. The elbow bends further as each arm comes forward."""
    def apply(pose, p):
        c = math.cos(TAU * (p - phase))
        arms(pose, "Left", lower=lower, swing=swing * c - forward_bias, elbow=elbow + elbow_forward * positive(-c))
        arms(pose, "Right", lower=lower, swing=-swing * c - forward_bias, elbow=elbow + elbow_forward * positive(c))
    return apply


def walking(speed, stance, contact_y, push_off_y, arm_swing, hips_bob, hips_low, swing_knee, reach):
    """
    A walk at the given ground speed, in the style of the walk reference: upright, a long reaching step landing on the
    heel, the trailing foot rolling up onto its toes to push off, the swinging knee bending about 50 degrees as it
    passes, and the arms swinging from the shoulders opposite the legs with the elbows soft. Faster walks take longer
    steps and swing the arms further.
    """
    return Gait(
        speed=speed, stance=stance, contact_y=contact_y, push_off_y=push_off_y,
        # Heel strike with the toes up, flat through the step, heel lifting into push-off, toes clearing the ground,
        # then the foot coming level and the toes lifting to reach for the next heel strike.
        foot_keys=[(0.0, -16.0), (0.08, 0.0), (stance - 0.22, 0.0), (stance - 0.08, 12.0), (stance, 32.0),
                   (stance + 0.08, 26.0), (stance + 0.22, 2.0), (0.92, -12.0)],
        # (q, thigh, knee): the knee bends most just after push-off, then the leg swings through and straightens. (The
        # lower the hips ride, the more the knee must bend for the swinging foot to clear the ground.)
        swing_keys=[(stance + 0.08, -2.0, swing_knee), (stance + 0.2, -22.0, swing_knee - 4.0), (0.9, *reach)],
        # The hips ride lower in faster walks, for the leg to reach further.
        hips_height=lambda p: -hips_low + hips_bob * math.cos(2.0 * TAU * (p - stance / 2.0)),
        torso=lambda pose, p: torso(pose, hips_twist=-6.0 * math.cos(TAU * p), spine_lean=3.0,
                                    chest_twist=6.0 * math.cos(TAU * p), head_lean=-2.0),
        # Each arm furthest forward as the opposite foot lands, the elbow bending a little more as it comes forward.
        arms=swinging_arms(lower=87.0, swing=arm_swing, forward_bias=0.0, elbow=12.0, elbow_forward=12.0),
    )


# Walking, at an everyday pace (about two steps a second), and briskly, for the character's default speed: longer
# steps and a quicker pace, with the arms swinging further. Each foot is down a little over half the time, so both
# are down together briefly after each step; the hips are highest as they pass over the planted foot.
walk = walking(speed=1.3, stance=0.62, contact_y=-0.33, push_off_y=0.44, arm_swing=15.0, hips_bob=0.016, hips_low=0.045,
               swing_knee=54.0, reach=(-28.0, 14.0))
brisk_walk = walking(speed=2.2, stance=0.58, contact_y=-0.38, push_off_y=0.52, arm_swing=22.0, hips_bob=0.018, hips_low=0.07,
                     swing_knee=66.0, reach=(-35.0, 36.0))

# Running, in the style of the run reference, for a steady run (about three steps a second): upright with a slight
# lean, each foot down for a little over a quarter of the cycle with flight between steps, landing under the knee,
# the heel kicking up behind until the shin is level, the knee driving forward to about 50 degrees, and the arms
# swinging fully front and back with the elbows at about a right angle. The hips dip as each foot takes the landing.
RUN_STANCE = 0.27
run = Gait(
    speed=3.9, stance=RUN_STANCE, contact_y=-0.27, push_off_y=0.44,
    foot_keys=[(0.0, 2.0), (0.08, 4.0), (0.18, 22.0), (0.27, 40.0), (0.38, 55.0), (0.55, 38.0), (0.75, 6.0),
               (0.9, -6.0)],
    # (q, thigh, knee): the heel kicks up behind, the knee drives forward, then the shin reaches out to land.
    swing_keys=[(0.36, 20.0, 60.0), (0.5, 0.0, 112.0), (0.65, -32.0, 104.0), (0.78, -48.0, 64.0), (0.9, -34.0, 26.0)],
    hips_height=lambda p: -0.045 - 0.03 * math.cos(2.0 * TAU * (p - RUN_STANCE / 2.0)),
    torso=lambda pose, p: torso(pose, hips_lean=6.0, hips_twist=-8.0 * math.cos(TAU * p), spine_lean=18.0,
                                chest_twist=10.0 * math.cos(TAU * p), head_lean=-20.0),
    arms=swinging_arms(lower=86.0, swing=38.0, forward_bias=4.0, elbow=84.0, elbow_forward=14.0, phase=-0.1),
)

# Sprinting, in the style of the sprint reference, for the character's sprint (its run speed), about 3.6 steps a
# second: driving forward, the trunk leaning about 25 degrees with the head up, the hips lower, each foot down about
# a fifth of the cycle on the forefoot, landing under the body, the trailing leg straightening right out behind at
# push-off, the heel folding up under the hips and the knee driving up to about 70 degrees in front, and the arms
# pumping hard from the shoulders, elbows near a right angle in front and opening behind.
SPRINT_STANCE = 0.22
sprint = Gait(
    # (Its strides measure about 6% slower in Unity, on the toes rather than the pivots planted here: 5.84 here makes
    # them 5.5 m/s there.)
    speed=5.84, stance=SPRINT_STANCE, contact_y=-0.22, push_off_y=0.48,
    # Landing on the forefoot nearly level, rolling up onto the toes to push off, the foot trailing toes-down as the
    # heel folds up, then coming through level and reaching forward toes-up.
    foot_keys=[(0.0, 4.0), (0.06, 8.0), (0.14, 26.0), (0.22, 48.0), (0.32, 62.0), (0.5, 42.0), (0.7, 8.0),
               (0.86, -8.0), (0.95, 0.0)],
    # (q, thigh, knee): leaving the ground stretched out behind, the heel folds up under the hips, the knee drives
    # forward and high, and the shin reaches out under the knee to land.
    swing_keys=[(0.3, 26.0, 40.0), (0.42, 6.0, 104.0), (0.55, -32.0, 124.0), (0.68, -64.0, 100.0), (0.8, -68.0, 58.0),
                (0.9, -46.0, 26.0)],
    hips_height=lambda p: -0.06 - 0.03 * math.cos(2.0 * TAU * (p - SPRINT_STANCE / 2.0)),
    torso=lambda pose, p: torso(pose, hips_lean=14.0, hips_twist=-9.0 * math.cos(TAU * p), spine_lean=28.0,
                                chest_twist=12.0 * math.cos(TAU * p), head_lean=-38.0),
    # Each arm furthest back just before its foot lands, as its thigh is furthest forward (as the motion capture does).
    arms=swinging_arms(lower=86.0, swing=56.0, forward_bias=4.0, elbow=72.0, elbow_forward=26.0, phase=-0.1),
)


# Jumping, after the jump reference: rising, the body stretched out from the take-off with the arms flung up; at the
# top, tucked, the knees pulled up and the fists up by the head; falling, stretched out again, the arms raised and
# the legs reaching down for the ground. The Animator blends them by vertical speed. Each holds still but for a
# little drift, so it doesn't freeze.

def rise(p):
    pose = Pose()
    w = math.sin(TAU * p)
    # Leaning into the jump, the legs trailing straight behind from the push-off, toes pointed.
    torso(pose, hips_lean=10.0, spine_lean=8.0, chest_lean=-2.0, head_lean=-6.0)
    legs(pose, "Left", thigh=18.0 + 2.0 * w, knee=10.0, foot=48.0)
    legs(pose, "Right", thigh=30.0 - 2.0 * w, knee=22.0, foot=52.0)
    # Arms flung up overhead, a little bent, one ahead of the other.
    arms(pose, "Left", lower=80.0, swing=-150.0 + 3.0 * w, elbow=30.0)
    arms(pose, "Right", lower=80.0, swing=-162.0 - 3.0 * w, elbow=24.0)
    return pose


def apex(p):
    pose = Pose()
    w = math.sin(TAU * p)
    torso(pose, hips_lean=18.0, spine_lean=16.0, chest_lean=6.0, head_lean=-20.0)
    legs(pose, "Left", thigh=-88.0 + 2.0 * w, knee=128.0, foot=30.0)
    legs(pose, "Right", thigh=-76.0 - 2.0 * w, knee=120.0, foot=36.0)
    # Fists up by the head, elbows bent hard.
    arms(pose, "Left", lower=72.0, swing=-112.0 + 3.0 * w, elbow=110.0)
    arms(pose, "Right", lower=72.0, swing=-100.0 - 3.0 * w, elbow=104.0)
    return pose


def fall(p):
    pose = Pose()
    w = math.sin(TAU * p)
    torso(pose, hips_lean=-6.0, spine_lean=-6.0, chest_lean=-2.0, head_lean=10.0)
    # Legs reaching forward and down for the ground, one ahead of the other, knees soft, toes pointing down.
    legs(pose, "Left", thigh=-24.0 + 3.0 * w, knee=12.0, foot=18.0)
    legs(pose, "Right", thigh=-6.0 - 3.0 * w, knee=24.0, foot=24.0)
    # Arms flung up and open above the head, as if caught by the fall.
    arms(pose, "Left", lower=30.0 - 4.0 * w, swing=-172.0, elbow=18.0)
    arms(pose, "Right", lower=46.0 + 4.0 * w, swing=-158.0, elbow=26.0)
    return pose


def dash(p):
    """A low, forward-pitched slide in the dash direction, with the legs trailing behind and the arms swept back."""
    pose = Pose()
    w = math.sin(TAU * p)
    torso(pose, hips_lean=30.0, spine_lean=35.0, chest_lean=15.0, head_lean=5.0)
    legs(pose, "Left", thigh=45.0 + 4.0 * w, knee=30.0, foot=50.0)
    legs(pose, "Right", thigh=60.0 - 4.0 * w, knee=45.0, foot=55.0)
    arms(pose, "Left", lower=75.0, swing=60.0 + 3.0 * w, elbow=10.0)
    arms(pose, "Right", lower=75.0, swing=60.0 - 3.0 * w, elbow=10.0)
    pose.hips_offset = Vector((0.0, 0.0, -0.3))  # Drop low into the slide.
    return pose


def crouch(p):
    """Crouched low on bent knees, leaning forward over them, the arms held loosely in front: crouching, and the
    fallback for crouch-walking where the Quaternius libraries (with their own crouch) aren't installed."""
    pose = Pose()
    breath = math.sin(TAU * p)
    torso(pose, hips_lean=24.0, spine_lean=12.0 + 0.8 * breath, chest_lean=-1.0 * breath, head_lean=-26.0)
    # The left foot a little ahead, the right knee lower, both feet flat.
    legs(pose, "Left", thigh=-82.0, knee=118.0, foot=0.0)
    legs(pose, "Right", thigh=-62.0, knee=122.0, foot=0.0)
    arms(pose, "Left", lower=72.0, swing=-34.0 + 1.5 * breath, elbow=48.0)
    arms(pose, "Right", lower=72.0, swing=-28.0 - 1.5 * breath, elbow=52.0)
    ground(pose, lift=-0.004 * breath)
    return pose


def hang(p):
    """Hanging from a ledge by both hands: the arms straight up above the head and a little forward (onto the ledge),
    the shoulders shrugged up with the weight, looking up, the knees bent and the feet brought forward to rest their
    toes on the wall, swaying a little. (As the Quaternius library's climbs hold the body against a wall: its feet a
    little in front of its hips. LedgeHandIK spreads the hands along the ledge.)"""
    pose = Pose()
    w = math.sin(TAU * p)
    torso(pose, hips_lean=-2.0 + 1.5 * w, spine_lean=-3.0, chest_lean=-2.0, head_lean=-14.0)
    legs(pose, "Left", thigh=-45.0 + 3.0 * w, knee=70.0, foot=30.0)
    legs(pose, "Right", thigh=-30.0 - 3.0 * w, knee=55.0, foot=34.0)
    arms(pose, "Left", lower=82.0, swing=-166.0 + 1.0 * w, elbow=16.0, shoulder_lift=14.0)
    arms(pose, "Right", lower=82.0, swing=-166.0 - 1.0 * w, elbow=16.0, shoulder_lift=14.0)
    return pose


def wall_slide(p):
    """Sliding down a wall it faces: the hands spread wide against the wall at head height, elbows bent out to take
    the weight, looking up, one knee raised against the wall and the other leg hanging, swaying a little as it slides.
    (Hand height and spread as the Quaternius library's climbs hold them against a wall: about a metre apart, at head
    height.)"""
    pose = Pose()
    w = math.sin(TAU * p)
    torso(pose, hips_lean=-4.0, spine_lean=-3.0 + 1.0 * w, chest_lean=-2.0, head_lean=-12.0)
    legs(pose, "Left", thigh=-58.0 + 2.0 * w, knee=78.0, foot=18.0)
    legs(pose, "Right", thigh=-12.0 - 2.0 * w, knee=32.0, foot=36.0)
    arms(pose, "Left", lower=55.0, swing=-97.0 + 1.5 * w, elbow=50.0, shoulder_lift=6.0)
    arms(pose, "Right", lower=55.0, swing=-93.0 - 1.5 * w, elbow=50.0, shoulder_lift=6.0)
    return pose


# name -> (pose function, duration in seconds). Walk, Run and Sprint take their durations from their ground speeds, so
# their planted feet don't slide at those speeds. Of them, the Animator's blend tree uses only Sprint, at the speed its
# strides measure in Unity (MannequinGenerator); the walk and run there are the motion capture's.
CLIPS = {
    "Idle": (idle, 3.0),
    "Walk": (walk, walk.duration),
    "Brisk Walk": (brisk_walk, brisk_walk.duration),
    "Run": (run, run.duration),
    "Sprint": (sprint, sprint.duration),
    "Rise": (rise, 1.0),
    "Apex": (apex, 1.0),
    "Fall": (fall, 0.6),
    "Dash": (dash, 0.4),
    "Crouch": (crouch, 3.0),
    "Hang": (hang, 2.0),
    "Wall Slide": (wall_slide, 1.5),
}

# Keys per second of animation. One per frame: a running foot is planted for only a few frames, and keys any
# sparser would let it slide between them.
SAMPLES_PER_SECOND = FPS

# The take holding the rest pose; "!" sorts before any letter, so it's always exported first. See build_actions.
BIND_POSE_TAKE = "!BindPose"


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

    # Blender's FBX export makes the first exported action the file's "current take", and Unity takes the model's
    # default pose from it, which becomes the Humanoid avatar's reference pose. Exported in name order, that was
    # Dash, so every clip was stored relative to the dash's forward lean and other models played them leaning back.
    # A one-frame take holding the rest pose, named to sort first, makes the default pose the rest pose. It isn't
    # imported as a clip (see MannequinGenerator).
    bind_pose = bpy.data.actions.new(BIND_POSE_TAKE)
    bind_pose.use_fake_user = True
    rig.animation_data.action = bind_pose
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = Quaternion()
        pb.location = (0.0, 0.0, 0.0)
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

    # Leave the rig in its rest pose with no active action, so the exported bind pose is the T-pose.
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = (0.0, 0.0, 0.0)
    return names

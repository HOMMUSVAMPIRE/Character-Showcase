using System;
using Level;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// Swimming (see Level.Water): in water deep enough, the character swims at the surface, its feet hanging the swim
    /// depth below it, the way the movement input points (faster with Sprint held). Falling or jumping in, it plunges and
    /// floats back up. Crouch dives, to swim underwater (steered by the camera, or up and down with Jump and Crouch held),
    /// back to the surface by drifting or swimming up. Jump jumps out; swimming into something low sticking out of the water vaults over or onto it, and
    /// into an edge within reach of its hands climbs out onto it (handed to Vaulting and Ledges); and the character walks
    /// out where the floor comes up to find its feet. Only at the surface: it
    /// doesn't dive.
    /// </summary>
    [Serializable]
    public class Swimming : MovementComponent
    {
        [Header("Floating")]
        [Tooltip("How deep water must be over the character's feet for it to swim, as a share of its standing height; " +
                 "swimming, its feet hang this far below the surface. 0 turns swimming off")]
        [Min(0f)] [SerializeField] private float swimDepth = 0.75f;
        [Tooltip("The fastest (m/s) the character rises back to the surface after plunging in, at its authored size")]
        [Min(0.1f)] [SerializeField] private float swimSurfacingSpeed = 3f;
        [Tooltip("How strongly the water slows the character's plunge when it falls in: the share of its speed it loses " +
                 "for each metre it plunges, at its authored size. Higher stops it sooner, nearer the surface")]
        [Min(0f)] [SerializeField] private float swimPlungeDrag = 1.5f;

        [Header("Swimming")]
        [Tooltip("How fast (m/s) the character swims at full movement input, at its authored size")]
        [Min(0f)] [SerializeField] private float swimSpeed = 3f;
        [Tooltip("How fast (m/s) the character swims while the Sprint button is held, at its authored size. At or below " +
                 "Swim Speed, sprinting does nothing")]
        [Min(0f)] [SerializeField] private float swimSprintSpeed = 5f;
        [Tooltip("How quickly (m/s²) the character's swimming speed changes, at its authored size: speeding up, turning, " +
                 "and slowing to a stop with no movement input (it also takes away the speed it falls in with)")]
        [Min(0.1f)] [SerializeField] private float swimAcceleration = 8f;
        [Tooltip("How high (m) jumping out of the water lifts the character's feet above the surface, at its authored " +
                 "size: what it can climb out onto. 0 means it can't jump out")]
        [Min(0f)] [SerializeField] private float swimJumpHeight = 1.2f;
        [Tooltip("How far above the water's surface the top of something the character swims into can be for it to " +
                 "vault over it or onto it (a pool's edge, a low divider), as a share of its standing height: taller, it " +
                 "climbs out onto it if its hands reach the top (see Ledges). 0 means it never vaults from the water")]
        [Range(0f, 1f)] [SerializeField] private float swimVaultHeight = 0.3f;

        [Header("Diving")]
        [Tooltip("How fast (m/s) pressing Crouch at the surface pushes the character down under it, at its authored size. " +
                 "0 turns diving off")]
        [Min(0f)] [SerializeField] private float swimDiveSpeed = 4f;
        [Tooltip("How fast (m/s) holding Jump or Crouch underwater rises or sinks, at its authored size, whichever way the " +
                 "camera looks (otherwise the character swims the way the camera looks, up or down with it)")]
        [Min(0f)] [SerializeField] private float swimVerticalSpeed = 2.5f;
        [Tooltip("How fast (m/s) the character drifts up underwater without any input, at its authored size: reaching its " +
                 "swimming depth, it's back at the surface. 0 stays where it is")]
        [Min(0f)] [SerializeField] private float swimBuoyancy = 0.5f;

        // Seconds over which the character closes in on its swimming depth (rising back up, or settling down onto it):
        // quickly, but not in one step.
        private const float SettleTime = 0.25f;
        // How quickly (a rate, per settle time) whatever's left of the character's vertical speed beyond closing in on its
        // depth fades: 4 makes the settling critically damped, the quickest it can be without bobbing past the depth.
        private const float SettleDamping = 4f;
        // Swimming, the character stops (finding its feet) once standing on a floor this share of the swim depth under
        // the surface: a little shallower than where it started swimming, so wading at the edge doesn't flicker between
        // the two.
        private const float FindFeetShare = 0.85f;
        // How far (of full) the movement input must push into an edge to climb out onto it.
        private const float ClimbOutInput = 0.5f;

        /// <summary>How deep water must be over the feet to swim, as a share of standing height. 0 turns swimming off.</summary>
        public float SwimDepth { get => swimDepth; set => swimDepth = Mathf.Max(0f, value); }
        /// <summary>How fast (m/s) the character swims at full movement input, at its authored size.</summary>
        public float SwimSpeed { get => swimSpeed; set => swimSpeed = Mathf.Max(0f, value); }

        public override bool IsActive(MovementController controller) => controller.swim.IsSwimming;

        /// <summary>
        /// Underwater, the character's collider is lowered to the crouch height, as a body swimming along is far less tall
        /// than one standing, so it fits through what it swims through. (Its collider is still an upright capsule: a
        /// flat one, for a body lying along the way it swims, waits on movement not assuming that shape.)
        /// </summary>
        public override bool KeepsLow(MovementController controller) => controller.swim.IsUnderwater;

        public override void Interrupt(ref MovementData movementData, MovementController controller)
        {
            if (controller.swim.IsSwimming) { StopSwimming(controller); }
        }

        /// <summary>
        /// Starts swimming where the water is deep enough over the character's feet (not while it's rising out of it
        /// faster than it would float up: a jump or a launch carries on), ending whatever else had the character; or
        /// carries on: at the surface, the feet the swim depth below it, moving the way the input points. It stops where
        /// it stands on a floor shallow enough, or leaves the water; Jump jumps out. Getting into the water gives back the
        /// character's jumps and air dashes, as touching the ground does.
        /// </summary>
        public override bool TakeOver(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            SwimState swim = controller.swim;
            Vector3 feet = movementData.initialPosition;
            float depth = swimDepth * controller.StandingHeight;
            Water water = depth > 0f ? Water.At(feet) : null;
            float under = water != null ? water.SurfaceHeight - feet.y : float.NegativeInfinity;
            float surfacingSpeed = swimSurfacingSpeed * controller.SpeedScale;

            if (!swim.IsSwimming)
            {
                if (under < depth || movementData.initialVelocity.y > surfacingSpeed) { return false; }
                StartSwimming(ref movementData, controller, water);
            }
            else if (water == null || (movementData.initialGrounded && under < depth * FindFeetShare))
            {
                // Out of the water (off its edge), or finding its feet in the shallows: on as it was going.
                StopSwimming(controller);
                return false;
            }
            swim.water = water;

            float dt = movementData.deltaTime;
            Vector3 velocity = movementData.initialVelocity;
            // Its own vertical speed, not the step's: standing on something (a beach, the pool's floor after a plunge),
            // the step's is the push that keeps a walking character on the ground (see FallAndAirHandling), which held
            // the swimmer down there. Blocked by what's below or above, it stops.
            float verticalSpeed = swim.verticalSpeed;
            if ((movementData.previousCollisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f) { verticalSpeed = 0f; }
            if ((movementData.previousCollisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f) { verticalSpeed = 0f; }

            if (swim.underwater) { return SwimUnderwater(ref movementData, inputs, controller, water, depth, verticalSpeed); }

            if (swimJumpHeight > 0f && Mode.IsNewJumpPress(inputs, controller))
            {
                // Out of the water: up fast enough that the feet rise the jump height above the surface, from where they are.
                StopSwimming(controller);
                float rise = under + swimJumpHeight * controller.HeightScale;
                float up = MovementHelpers.LaunchSpeedFor(rise, Mode.GravityAcceleration(controller), dt);
                MovementHelpers.JumpOff(ref movementData, controller, new Vector3(velocity.x, up, velocity.z), MovementEvents.Jumped);
                return true;
            }

            // Crouch dives: pushed down under the surface, and swimming underwater from this step.
            if (swimDiveSpeed > 0f && Mode.ConsumeCrouchPress(inputs, controller))
            {
                swim.underwater = true;
                return SwimUnderwater(ref movementData, inputs, controller, water, depth, Mathf.Min(verticalSpeed, -swimDiveSpeed * controller.SpeedScale));
            }

            // Swimming into an edge within reach of the hands (a pool's side), up onto it, as climbing over a ledge's top.
            Vector3 input = MovementHelpers.InputDirection(movementData, inputs);
            if (TryClimbOut(ref movementData, inputs, controller, input, under)) { return true; }

            // Across the water: speeding up toward the input's way and speed (relative to the camera), or slowing to a
            // stop, facing the way it swims.
            float topSpeed = SwimSpeedFor(inputs) * controller.SpeedScale * controller.movementSpeedMultiplier;
            Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), input * topSpeed,
                swimAcceleration * controller.AccelerationScale * dt);
            if (input.sqrMagnitude > 1e-4f && topSpeed > 0f) { movementData.targetRotation = Quaternion.LookRotation(input.normalized, Vector3.up); }

            // Up or down to the swimming depth: closing in on it over the settle time, rising no faster than the surfacing
            // speed. Whatever's left of the speed it had beyond that (a plunge) fades, quickly while it's fast (the
            // water's drag goes with the square of the speed, so a plunge from high goes only a little deeper than one from
            // low), and smoothly into the settling. Solved for the end of the step, so it's steady however strong the drag.
            float target = water.SurfaceHeight - depth;
            float settleTime = SettleTime * controller.DurationScale;
            float settle = Mathf.Clamp((target - feet.y) / settleTime, -surfacingSpeed, surfacingSpeed);
            float excess = verticalSpeed - settle;
            excess /= 1f + (swimPlungeDrag / controller.HeightScale * Mathf.Abs(excess) + SettleDamping / settleTime) * dt;
            float vertical = settle + excess;
            swim.verticalSpeed = vertical;

            movementData.velocityOverride = horizontal + Vector3.up * vertical;
            movementData.directMovementVector = Vector3.zero;
            movementData.state |= CharacterState.Swimming;
            if (horizontal.sqrMagnitude > 1e-4f) { movementData.state |= CharacterState.Moving; }
            return true;
        }

        /// <summary>
        /// Swimming underwater: the way the movement input points relative to the camera, up and down with it as it looks
        /// (looking down and pushing forward swims down), at the swim speed; holding Jump rises and Crouch sinks, at the
        /// swim vertical speed, whichever way it looks. Without any input, it drifts up at the swim buoyancy; back up at its
        /// swimming depth, rising, it's at the surface again. A press of Jump only rises: it isn't kept to jump out with
        /// once the character's at the surface. Speeds change at the swim acceleration; it faces the way it swims across.
        /// </summary>
        private bool SwimUnderwater(ref MovementData movementData, Inputs inputs, MovementController controller, Water water, float depth,
            float verticalSpeed)
        {
            SwimState swim = controller.swim;
            float dt = movementData.deltaTime;
            if (Mode.IsNewJumpPress(inputs, controller)) { controller.jump.press.Use(); }

            // movementVector is the stick in camera space, flat: turned by the whole camera, pitch and all.
            Vector3 steer = Vector3.ClampMagnitude(movementData.cameraLook * inputs.movementVector, 1f);
            float upOrDown = (Held(inputs.jump) ? 1f : 0f) - (Held(inputs.crouch) ? 1f : 0f);
            float topSpeed = SwimSpeedFor(inputs) * controller.SpeedScale * controller.movementSpeedMultiplier;
            Vector3 target = steer * topSpeed;
            if (upOrDown != 0f) { target.y = upOrDown * swimVerticalSpeed * controller.SpeedScale; }
            else if (steer.sqrMagnitude < 1e-4f) { target.y = swimBuoyancy * controller.SpeedScale; }

            Vector3 velocity = movementData.initialVelocity;
            velocity = Vector3.MoveTowards(new Vector3(velocity.x, verticalSpeed, velocity.z), target, swimAcceleration * controller.AccelerationScale * dt);
            Vector3 across = new Vector3(steer.x, 0f, steer.z);
            if (across.sqrMagnitude > 1e-4f && topSpeed > 0f) { movementData.targetRotation = Quaternion.LookRotation(across.normalized, Vector3.up); }

            // Back up to its swimming depth, rising: at the surface again, arriving at it rather than past it.
            float surface = water.SurfaceHeight - depth, feet = movementData.initialPosition.y;
            if (velocity.y > 0f && feet + velocity.y * dt >= surface)
            {
                velocity.y = Mathf.Max(0f, (surface - feet) / dt);
                swim.underwater = false;
            }
            swim.verticalSpeed = velocity.y;

            movementData.velocityOverride = velocity;
            movementData.directMovementVector = Vector3.zero;
            movementData.state |= CharacterState.Swimming;
            if (swim.underwater) { movementData.state |= CharacterState.Diving; }
            if (velocity.sqrMagnitude > 1e-4f) { movementData.state |= CharacterState.Moving; }
            return true;
        }

        private static bool Held(InputButton button) => button != null && button.value;

        /// <summary>
        /// The speed (m/s) to swim at for these inputs, at full movement input and the character's authored size: the
        /// sprint speed while Sprint is held (if it's the faster), otherwise the swim speed.
        /// </summary>
        public float SwimSpeedFor(Inputs inputs) =>
            inputs.sprint != null && inputs.sprint.value && swimSprintSpeed > swimSpeed ? swimSprintSpeed : swimSpeed;

        /// <summary>
        /// Starts swimming in water: it counts as touching the ground (the character's jumps and air dashes come back), it
        /// ends whatever else had the character (a dash, a roll, a slide), and it's done standing.
        /// </summary>
        private void StartSwimming(ref MovementData movementData, MovementController controller, Water water)
        {
            controller.swim.water = water;
            // Falling in, the plunge carries on; walking in, the step's vertical speed is only the ground's push.
            controller.swim.verticalSpeed = movementData.initialGrounded ? 0f : movementData.initialVelocity.y;
            MovementHelpers.GiveBackAirMoves(controller);
            Mode.InterruptOthers(ref movementData, controller, this);
            controller.crouch.IsCrouched = false;
            controller.crouch.wantsToStand = false;
        }

        private static void StopSwimming(MovementController controller)
        {
            controller.swim.water = null;
            controller.swim.underwater = false;
        }

        /// <summary>
        /// Gets out of the water over or onto something the character is swimming into (steering into a wall it touches).
        /// Low, its top no more than Swim Vault Height above the surface, it vaults it, as it would on land (handed to
        /// Vaulting: over a low divider, back into the water beyond; onto a pool's edge). Taller, with a walkable top
        /// within reach of its hands and something to stand on, it climbs out onto it (handed to Ledges' climb over a
        /// top). Without those components, there's no getting out but by jumping. Returns whether it did.
        /// </summary>
        private bool TryClimbOut(ref MovementData movementData, Inputs inputs, MovementController controller, Vector3 input, float under)
        {
            if (input.sqrMagnitude < ClimbOutInput * ClimbOutInput || (movementData.previousCollisions & CollisionFlags.Sides) == 0) { return false; }
            Vaulting vaulting = Mode.Get<Vaulting>();
            float vaultTop = under + swimVaultHeight * controller.StandingHeight;
            bool gotOut = (swimVaultHeight > 0f && vaulting != null && vaulting.TryVaultOutOfWater(ref movementData, inputs, controller, input, under, vaultTop))
                          || (Mode.Get<Ledges>() is { } ledges && ledges.TryClimbOverTop(ref movementData, controller, input.normalized, withinReach: false, requireFloor: true));
            if (gotOut) { StopSwimming(controller); }
            return gotOut;
        }
    }

    /// <summary>The water the character is swimming in, if it's swimming.</summary>
    public class SwimState
    {
        /// <summary>The body of water the character is swimming in (null when it isn't swimming).</summary>
        public Level.Water water;
        /// <summary>How fast (m/s) the character is rising (positive) or sinking in the water.</summary>
        public float verticalSpeed;
        /// <summary>Whether the character is swimming underwater (dived), rather than at the surface.</summary>
        public bool underwater;
        /// <summary>Whether the character is swimming underwater.</summary>
        public bool IsUnderwater => water != null && underwater;

        /// <summary>Whether the character is swimming.</summary>
        public bool IsSwimming => water != null;
    }
}

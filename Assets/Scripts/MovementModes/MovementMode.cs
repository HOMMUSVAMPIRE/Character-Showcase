using UnityEngine;
using static Character.CharacterUtilities;

namespace Character
{
    // The base class for scriptable objects which control the ways characters move.
    public abstract class MovementMode : ScriptableObject
    {
        public virtual void ProcessMovementData(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            return;
        }

        /// <summary>
        /// Called once the step's movement has been resolved against the world (movementData.finalMovementVector is
        /// where the character will go), before it's applied. For abilities that move something else along with the
        /// character, such as an object being pushed or pulled, by however far the character actually got to move.
        /// </summary>
        public virtual void AfterMovementResolved(MovementData movementData, MovementController controller)
        {
        }
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle movement related functionality
    /// </summary>
    public interface WalkRunMode
    {
        public abstract float walkSpeed { get; set; }
        public abstract float runSpeed { get; set; }

        /// <summary>
        /// The speed to move at for these inputs, at full movement input and the character's authored size: the run
        /// speed while Sprint is held (if there is one, faster than walking), otherwise the walk speed.
        /// </summary>
        public float MoveSpeed(Inputs inputs) =>
            inputs.sprint != null && inputs.sprint.value && runSpeed > walkSpeed ? runSpeed : walkSpeed;

        public virtual void ProcessMovementDataForWalk(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            if (inputs.movementVector.sqrMagnitude > 0.001f)
            {
                movementData.directMovementVector = movementData.cameraLookFlat * inputs.movementVector * MoveSpeed(inputs) * Time.fixedDeltaTime;
                movementData.targetRotation = Quaternion.LookRotation(movementData.directMovementVector.sqrMagnitude > 0 ? movementData.directMovementVector : movementData.initialRotation * Vector3.forward, Vector3.up);
            }
            movementData.state = movementData.state | CharacterState.Moving;
        }
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle jumping related functionality
    /// </summary>
    public interface JumpMode
    {
        public abstract int maxJumps { get; set; }
        /// <summary>How high (m) the shortest jump (a tap) rises, at the character's authored size.</summary>
        public abstract float minJumpHeight { get; set; }
        /// <summary>How high (m) a jump held to its top rises, at the character's authored size.</summary>
        public abstract float maxJumpHeight { get; set; }

        public abstract bool CanJump(MovementData data, MovementController controller);

        /// <summary>A plain jump, for modes without their own: launched to the max jump height under Unity's gravity.</summary>
        public virtual void ProcessMovementDataForJump(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            if (CanJump(movementData, controller) && inputs.jump.value)
            {
                movementData.velocityChange += Vector3.up * Mathf.Sqrt(2f * Physics.gravity.magnitude * Mathf.Max(0f, maxJumpHeight));
                movementData.state = movementData.state | CharacterState.Jumping;
            }
        }
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle dashing related functionality
    /// </summary>
    public interface DashMode
    {
        public abstract float dashSpeed { get; set; }
        public abstract float dashDuration { get; set; }
        public abstract int maxAirDashes { get; set; }

        public abstract bool CanDash(MovementData data, Inputs inputs, MovementController controller);

        public virtual void ProcessMovementDataForDash(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            if (CanDash(movementData, inputs, controller))
            {
                movementData.velocityOverride = movementData.initialRotation * Vector3.forward * dashSpeed;
                movementData.state = movementData.state | CharacterState.Dashing;
            }
        }
    }

    // The abilities below each take over the character's movement while they last. Their per-character state lives on
    // the MovementController (CrouchState, SlideState, ...), since movement modes are shared assets. Each
    // ProcessMovementDataFor... method carries on the ability if it's under way, or starts it if its inputs and
    // surroundings call for it, and returns whether the ability has the character this step.

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle crouching: a lower, slower stance
    /// with a shorter collider, which stands back up only where there's room.
    /// </summary>
    public interface CrouchMode
    {
        /// <summary>How fast (m/s) the character moves while crouched, at its authored size.</summary>
        public abstract float crouchSpeed { get; set; }
        /// <summary>The character's collider height while crouched (and sliding and rolling), as a share of its standing height.</summary>
        public abstract float crouchHeight { get; set; }

        /// <summary>Crouches or stands up as the Crouch button asks, standing only where there's room.</summary>
        public abstract void ProcessMovementDataForCrouch(ref MovementData movementData, Inputs inputs, MovementController controller);
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle sliding: crouching while sprinting
    /// slides along the ground on the sprint's momentum, slowing down until it ends in a crouch.
    /// </summary>
    public interface SlideMode
    {
        /// <summary>How fast (m/s) a slide starts, at least, at the character's authored size.</summary>
        public abstract float slideStartSpeed { get; set; }
        /// <summary>The longest a slide lasts, in seconds. 0 turns sliding off.</summary>
        public abstract float slideMaxDuration { get; set; }

        public abstract bool ProcessMovementDataForSlide(ref MovementData movementData, Inputs inputs, MovementController controller);
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle rolling: a quick, low, committed
    /// roll along the ground in the input direction.
    /// </summary>
    public interface RollMode
    {
        public abstract float rollSpeed { get; set; }
        /// <summary>How long a roll lasts, in seconds. 0 turns rolling off.</summary>
        public abstract float rollDuration { get; set; }

        public abstract bool ProcessMovementDataForRoll(ref MovementData movementData, Inputs inputs, MovementController controller);
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle wall-running: running along a wall
    /// in mid-air, falling slowly, and jumping off it.
    /// </summary>
    public interface WallRunningMode
    {
        public abstract float wallRunSpeed { get; set; }
        /// <summary>The longest a wall run lasts, in seconds. 0 turns wall-running off.</summary>
        public abstract float wallRunMaxDuration { get; set; }

        public abstract bool ProcessMovementDataForWallRun(ref MovementData movementData, Inputs inputs, MovementController controller);
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle sliding down walls: running into a
    /// wall head-on in mid-air slides slowly down it, and jumping kicks off it, upward.
    /// </summary>
    public interface WallSlideMode
    {
        /// <summary>The fastest (m/s) the character slides down a wall, at its authored size.</summary>
        public abstract float wallSlideSpeed { get; set; }
        /// <summary>The longest a slide down a wall lasts, in seconds. 0 turns wall sliding off.</summary>
        public abstract float wallSlideMaxDuration { get; set; }

        public abstract bool ProcessMovementDataForWallSlide(ref MovementData movementData, Inputs inputs, MovementController controller);
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle grabbing ledges in mid-air, hanging
    /// from them, and climbing up onto them.
    /// </summary>
    public interface LedgeMode
    {
        /// <summary>Seconds a climb from hanging up onto the ledge takes. 0 turns ledge grabbing off.</summary>
        public abstract float ledgeClimbDuration { get; set; }

        public abstract bool ProcessMovementDataForLedge(ref MovementData movementData, Inputs inputs, MovementController controller);
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle vaulting: heading into something
    /// whose top is too low to grab as a ledge but too high to step onto (in mid-air, or sprinting at it), the character
    /// puts a hand on the top and swings itself over; onto something low and deep, it steps up instead.
    /// </summary>
    public interface VaultMode
    {
        /// <summary>Seconds a vault spends getting a hand onto the top and rising to it, at the character's authored size. 0 turns vaulting off.</summary>
        public abstract float vaultRiseDuration { get; set; }

        public abstract bool ProcessMovementDataForVault(ref MovementData movementData, Inputs inputs, MovementController controller);
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle climbing: running or jumping into a
    /// climbable surface (see Level.ClimbableSurface) takes hold of it, and movement input then moves the character over
    /// it, up, down and sideways, round corners onto connected climbable surfaces, but never off the climbable area.
    /// </summary>
    public interface WallClimbMode
    {
        /// <summary>How fast (m/s) the character climbs at full input, at its authored size. 0 turns climbing off.</summary>
        public abstract float wallClimbSpeed { get; set; }

        public abstract bool ProcessMovementDataForWallClimb(ref MovementData movementData, Inputs inputs, MovementController controller);
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle grabbing large objects (see
    /// Level.PushableObject) and pushing and pulling them along the ground.
    /// </summary>
    public interface PushPullMode
    {
        /// <summary>How fast (m/s) the character pushes or pulls an object, before the object's own speed multiplier.</summary>
        public abstract float pushPullSpeed { get; set; }

        public abstract bool ProcessMovementDataForPushPull(ref MovementData movementData, Inputs inputs, MovementController controller);
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle ropes (see Level.Rope): reaching one
    /// in mid-air catches hold of it, and the character swings on it as a pendulum, pumping the swing with movement
    /// input, until it jumps or lets go.
    /// </summary>
    public interface RopeSwingMode
    {
        /// <summary>How far (m) from the character's hands a rope can be to catch hold of it, at its authored size. 0 turns rope swinging off.</summary>
        public abstract float ropeGrabReach { get; set; }

        public abstract bool ProcessMovementDataForRopeSwing(ref MovementData movementData, Inputs inputs, MovementController controller);
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle ziplines (see Level.Zipline):
    /// reaching a zipline's cable in mid-air catches hold of it, and the character rides along it, downhill, to its end.
    /// </summary>
    public interface ZiplineMode
    {
        /// <summary>How far (m) from the character's hands a cable can be to catch hold of it, at its authored size. 0 turns ziplines off.</summary>
        public abstract float ziplineGrabReach { get; set; }

        public abstract bool ProcessMovementDataForZipline(ref MovementData movementData, Inputs inputs, MovementController controller);
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle bounce pads (see Level.BouncePad):
    /// standing on one launches the character into the air, to the height the pad is set to.
    /// </summary>
    public interface BounceMode
    {
        /// <summary>What every bounce pad's launch height is multiplied by for this character. 0 turns bounce pads off.</summary>
        public abstract float bounceHeightMultiplier { get; set; }

        public abstract bool ProcessMovementDataForBounce(ref MovementData movementData, Inputs inputs, MovementController controller);
    }
}

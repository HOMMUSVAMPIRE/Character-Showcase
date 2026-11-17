using System;
using UnityEngine;
using static Character.MovementHelpers;

namespace Character
{
    /// <summary>
    /// Vaulting: heading into something whose top is too low to grab as a ledge (see Ledges) but too high to step onto,
    /// in mid-air or sprinting at it on the ground, the character puts a hand on the top and swings itself over. Onto
    /// the top of something low and deep (a platform rather than a wall), it steps up instead, at once, as it does onto
    /// stairs.
    /// </summary>
    [Serializable]
    public class Vaulting : MovementComponent
    {
        [Tooltip("Seconds a vault spends getting a hand onto the top and lifting the body up to it, at the character's " +
                 "authored size, before it swings over (speeding up to the speed it leaves with). 0 turns vaulting off")]
        [SerializeField] private float vaultRiseDuration = 0.25f;
        [Tooltip("The share of the speed the character came in at that it leaves a vault with")]
        [Range(0f, 1f)] [SerializeField] private float vaultSpeedRetention = 0.75f;
        [Tooltip("The share of the speed it comes in at that a vault's rise slows to by the time it's up at the top: the " +
                 "vault takes off far enough from the wall for that (further, the faster the character's going), slowing " +
                 "evenly through the rise, rather than dropping almost to a standstill as it begins. 0 takes off as close as " +
                 "the vault is found (just short of the wall), slowing at once to whatever closes that gap over the rise")]
        [Range(0f, 1f)] [SerializeField] private float vaultTakeOffSpeed = 0.5f;
        [Tooltip("The slowest (m/s) a vault leaves the character, at its authored size: a slower approach is sped up to " +
                 "this, so a vault is always brisk")]
        [SerializeField] private float vaultMinSpeed = 3f;
        [Tooltip("Whether sprinting into something low on the ground vaults it, as running into it in mid-air does (or " +
                 "walking into it, while wading: see Wading)")]
        [SerializeField] private bool vaultFromGround = true;
        [Tooltip("How far (degrees) from head-on a sprint into something low on the ground can be and still vault it, so " +
                 "running along a railing doesn't")]
        [Range(0f, 60f)] [SerializeField] private float vaultGroundMaxAngle = 25f;
        [Tooltip("From the ground, something is only vaulted over if there's somewhere to land beyond it no further than " +
                 "this below the character's feet, as a share of its standing height: a railing at the edge of a drop " +
                 "isn't vaulted over")]
        [SerializeField] private float vaultMaxDrop = 1f;
        [Tooltip("The top of something deep enough to stand on (not a wall), up to this high above the character's feet " +
                 "(a share of its standing height), is stepped up onto at once, keeping the character's speed, rather " +
                 "than vaulted onto")]
        [SerializeField] private float vaultStepUpHeight = 0.5f;

        // How fast (m/s, at the authored size) a step up onto something lifts the character, at most, and the longest it
        // takes (seconds), in case something stops it getting up.
        private const float StepUpRiseSpeed = 10f;
        private const float StepUpTimeout = 0.3f;

        /// <summary>Seconds a vault spends getting a hand onto the top and rising to it, at the character's authored size. 0 turns vaulting off.</summary>
        public float VaultRiseDuration { get => vaultRiseDuration; set => vaultRiseDuration = value; }

        public override bool IsActive(MovementController controller) => controller.vault.IsActive;

        public override void Interrupt(ref MovementData movementData, MovementController controller)
        {
            if (controller.vault.IsActive) { EndVault(ref movementData, controller); }
        }

        /// <summary>
        /// Starts a vault (or a step up) when the character heads into a wall just ahead whose top is above the step
        /// height but below the lowest ledge it could grab, with room to go over it, and that isn't a step (a staircase
        /// rising ahead, say): in mid-air, not rising fast, steering into it or moving toward it near head-on; or on the
        /// ground, sprinting into it head-on, with somewhere to land beyond it. Carries it on: a vault spends its rise
        /// time closing in on the wall while rising to the top's height, then swings over the edge, speeding up to the
        /// speed it leaves with, until the whole body is past the wall's face, without gravity. A step up lifts the
        /// character onto the top as fast as it takes to reach the wall, keeping its speed.
        /// </summary>
        public override bool TakeOver(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            VaultState vault = controller.vault;
            if (!vault.IsActive && !TryStartVault(ref movementData, inputs, controller)) { return false; }
            if (vault.obstacle == null || !vault.obstacle.gameObject.activeInHierarchy)
            {
                EndVault(ref movementData, controller); // What it belonged to is gone.
                return false;
            }

            // The path is kept in the obstacle's space, so a vault over something moving still goes over it.
            MovementController.GetPhysicsPose(vault.obstacle, out Vector3 obstaclePosition, out Quaternion obstacleRotation);
            vault.elapsed += movementData.deltaTime;
            if (vault.IsSteppingUp) { return StepUp(ref movementData, controller, obstaclePosition, obstacleRotation); }

            Vector3 start = obstaclePosition + obstacleRotation * vault.localStart;
            Vector3 end = obstaclePosition + obstacleRotation * vault.localEnd;
            Vector3 travel = new Vector3(end.x - start.x, 0f, end.z - start.z);
            float length = travel.magnitude;
            Vector3 direction = length > 1e-5f ? travel / length : Facing(movementData);

            // Rising: closing the gap to the wall, slowing evenly from the speed coming in (see riseStartSpeed), while
            // lifted to the top's height, easing in to it.
            // Then over: the rest of the way, at the top's height, speeding up evenly to the speed it leaves with, so it
            // doesn't drift over slowly and then shoot off.
            float distance, up;
            bool over = false;
            if (!MovementHelpers.HasElapsed(vault.elapsed, vault.riseDuration, movementData.deltaTime))
            {
                // Slowing evenly from the speed it rises at first to the speed it swings over at.
                float rise = vault.elapsed / vault.riseDuration, t = vault.elapsed;
                float slowing = (vault.crossStartSpeed - vault.riseStartSpeed) / Mathf.Max(vault.riseDuration, 1e-4f);
                distance = Mathf.Min(vault.gapDistance, vault.riseStartSpeed * t + 0.5f * slowing * t * t);
                up = Mathf.SmoothStep(0f, 1f, rise);
            }
            else
            {
                float since = vault.elapsed - vault.riseDuration, t = Mathf.Min(since, vault.crossDuration);
                float acceleration = vault.crossDuration > 0f ? (vault.exitSpeed - vault.crossStartSpeed) / vault.crossDuration : 0f;
                distance = Mathf.Min(length, vault.gapDistance + vault.crossStartSpeed * t + 0.5f * acceleration * t * t);
                up = 1f;
                over = MovementHelpers.HasElapsed(vault.elapsed, vault.riseDuration + vault.crossDuration, movementData.deltaTime);
                // The step it ends on, on past the end for the rest of the step at the speed it leaves with: stopped at the
                // end, that step moved only part of one, and the character lurched as it left the vault at full speed.
                if (over) { distance = length + vault.exitSpeed * Mathf.Max(0f, since - vault.crossDuration); }
            }
            Vector3 point = start + direction * distance + Vector3.up * ((end.y - start.y) * up);
            // Over the top and standing on it: no higher than where it stands. The path holds the feet a little clear of
            // the top (see VaultClearance), and standing on it, the ground snap set them down on it: lifted back up the
            // next step and set down again the one after, the character bounced on the top all the way across (climbing
            // out of the water, onto a pool's side, too).
            if (up >= 1f && movementData.initialGrounded) { point.y = Mathf.Min(point.y, movementData.initialPosition.y); }

            movementData.velocityOverride = (point - movementData.initialPosition) / movementData.deltaTime;
            movementData.directMovementVector = Vector3.zero;
            movementData.targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            movementData.state |= CharacterState.Vaulting;
            if (over)
            {
                // Over: this step still reaches the end of the path, and the character leaves it moving at the vault's
                // exit speed (which the swing over has sped up to), under gravity again from the next step.
                Vector3 exit = direction * vault.exitSpeed;
                vault.phase = VaultState.Phase.None;
                vault.obstacle = null;
                movementData.velocityOverride = exit;
                movementData.directMovementVector = point - movementData.initialPosition - exit * movementData.deltaTime;
            }
            return true;
        }

        /// <summary>
        /// Steps up onto the top: lifted just fast enough to clear it as the body reaches the wall (no faster than the
        /// step up's rise speed), moving on at the speed the character came in at, until its axis is past the edge.
        /// </summary>
        private bool StepUp(ref MovementData movementData, MovementController controller, Vector3 obstaclePosition, Quaternion obstacleRotation)
        {
            VaultState vault = controller.vault;
            Vector3 edge = obstaclePosition + obstacleRotation * vault.localEdge;
            Vector3 wallNormal = obstacleRotation * vault.localWallNormal;
            wallNormal.y = 0f;
            wallNormal = wallNormal.sqrMagnitude > 1e-6f ? wallNormal.normalized : -Facing(movementData);
            Vector3 feet = movementData.initialPosition;
            float rise = edge.y + VaultClearance(controller) - feet.y;
            float past = Vector3.Dot(feet - edge, -wallNormal);
            if ((rise <= 0f && past >= 0f) || vault.elapsed > StepUpTimeout * controller.DurationScale)
            {
                EndVault(ref movementData, controller);
                return false;
            }
            float vertical = 0f;
            if (rise > 0f)
            {
                float gap = Mathf.Max(0f, -past - controller.Body.FrontExtent);
                float speedIn = Mathf.Max(Vector3.Dot(vault.exitVelocity, -wallNormal), 1e-3f);
                float timeToWall = Mathf.Max(gap / speedIn, movementData.deltaTime);
                vertical = Mathf.Min(StepUpRiseSpeed * controller.SpeedScale, rise / timeToWall);
            }
            movementData.velocityOverride = vault.exitVelocity + Vector3.up * vertical;
            movementData.directMovementVector = Vector3.zero;
            movementData.state |= CharacterState.SteppingUp;
            return true;
        }

        /// <summary>
        /// Starts a vault or a step up if the character is heading into a wall just ahead whose top is above the step
        /// height and below the lowest ledge it could grab (see TakeOver), with room to rise up to it
        /// where the character is and to be on top of it just past the edge.
        /// </summary>
        private bool TryStartVault(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            if (vaultRiseDuration <= 0f || controller.crouch.IsCrouched || Mode.AbilityInProgress(controller)) { return false; }
            // Below the window a ledge is grabbed in, whether or not ledges are (see Ledges).
            Ledges ledges = SettingsOf<Ledges>();
            bool grounded = movementData.initialGrounded;
            Vector3 steer = InputDirection(movementData, inputs);
            if (grounded)
            {
                // Sprinting into it; or, wading (as the last step was), walking into it: the water slows the character
                // too much to sprint up to something low in it, and climbing over beats wading round.
                bool sprinting = inputs.sprint != null && inputs.sprint.value;
                bool wading = (movementData.previousState & CharacterState.Wading) != 0;
                if (!vaultFromGround || !(sprinting || wading) || steer.sqrMagnitude < 0.25f) { return false; }
            }
            else if (movementData.initialVelocity.y > ledges.LedgeMaxGrabRiseSpeed * controller.SpeedScale) { return false; }

            // Which way the character is heading: as steered, or else as it's moving.
            Vector3 velocity = movementData.initialVelocity;
            Vector3 moving = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 heading = steer.sqrMagnitude > 0.01f ? steer.normalized
                : moving.sqrMagnitude > 0.01f ? moving.normalized : Vector3.zero;
            if (heading == Vector3.zero) { return false; }

            // From just over a step to just under a ledge the character could grab.
            float stepHeight = controller.MaxStepHeight * controller.HeightScale;
            return StartVaultOver(ref movementData, controller, ledges, heading, steer, grounded, stepHeight, ledges.LedgeGrabMinHeight * controller.Body.StandingHeight);
        }

        /// <summary>
        /// Vaults (or steps up) out of the water, for a swimmer heading into something whose top sticks out of it (see
        /// Swimming): its top between low and high above the character's feet (from the water's surface to as high as
        /// Swimming vaults from), over or onto it as from mid-air. The vault's first step is this one, as for a vault
        /// started in TakeOver: a step with the swim over and the vault not yet begun (neither swimming nor vaulting)
        /// had the Animator start blending from swimming into the air, which the climb out then had to wait for, and the
        /// model went up, down and up again. Returns whether it started.
        /// </summary>
        internal bool TryVaultOutOfWater(ref MovementData movementData, Inputs inputs, MovementController controller, Vector3 heading,
            float low, float high)
        {
            if (vaultRiseDuration <= 0f || controller.crouch.IsCrouched || heading.sqrMagnitude < 1e-4f) { return false; }
            heading = heading.normalized;
            if (!StartVaultOver(ref movementData, controller, SettingsOf<Ledges>(), heading, heading, grounded: false, low, high)) { return false; }
            controller.vault.outOfWater = true;
            return TakeOver(ref movementData, inputs, controller);
        }

        /// <summary>
        /// Starts a vault or a step up over a wall just ahead, heading (and steered, steer) into it, whose top is between
        /// low and high above the character's feet, with room to rise up to it and to be on top of it just past the edge
        /// (see TryStartVault). From the ground, only over to somewhere to land.
        /// </summary>
        private bool StartVaultOver(ref MovementData movementData, MovementController controller, Ledges ledges, Vector3 heading,
            Vector3 steer, bool grounded, float low, float high)
        {
            VaultState vault = controller.vault;
            Vector3 velocity = movementData.initialVelocity;
            Vector3 moving = new Vector3(velocity.x, 0f, velocity.z);
            float stepHeight = controller.MaxStepHeight * controller.HeightScale;
            // Looked for far enough ahead that a fast approach is caught before the next step takes it into the wall, and
            // that the rise can slow evenly from the speed coming in to its share of it (the take off speed) over its time.
            float approach = Mathf.Max(0f, Vector3.Dot(moving, heading));
            float takeOff = 0.5f * (1f + vaultTakeOffSpeed) * approach * vaultRiseDuration * controller.DurationScale;
            float reach = controller.Body.FrontExtent + Mathf.Max(ledges.LedgeGrabReach * controller.HeightScale,
                Mathf.Max(takeOff, 2f * moving.magnitude * movementData.deltaTime));
            Vector3 feet = movementData.initialPosition;
            if (!ledges.FindLedge(controller, feet, heading, low, high, reach, out RaycastHit top, out Vector3 wallNormal, out Vector3 edge)) { return false; }
            bool steeringIn = Vector3.Dot(steer, -wallNormal) >= (grounded ? Mathf.Cos(vaultGroundMaxAngle * Mathf.Deg2Rad) : 0.5f);
            bool movingIn = !grounded && Vector3.Dot(moving, -wallNormal) >= 1f * controller.SpeedScale;
            if (!steeringIn && !movingIn) { return false; }

            // Not a step: something to stand on just in front of the wall, no more than a step below its top (the step
            // before it, on a staircase rising ahead), means it's climbed as stairs are, not vaulted. Vaults found the
            // risers of stairs a couple of steps ahead, whose treads are within the vault's heights.
            float skin = controller.Skin;
            Vector3 inFront = edge + wallNormal * (2f * skin + 0.05f * controller.HeightScale) + Vector3.up * skin;
            if (controller.Raycast(inFront, Vector3.down, stepHeight + skin, out _)) { return false; }

            // What's beyond the edge, a body's length past the wall's face: the top carrying on (something deep, to
            // stand on), or else whatever's below, where the character lands after vaulting over.
            Vector3 beyond = edge - wallNormal * (controller.Body.FrontExtent + controller.Body.BackExtent + 2f * skin);
            bool deep = controller.Raycast(beyond + Vector3.up * stepHeight, Vector3.down, 2f * stepHeight, out RaycastHit far)
                        && Vector3.Angle(far.normal, Vector3.up) <= controller.MaxSlopeAngle;
            if (grounded && !deep)
            {
                // From the ground, only over to somewhere to land, not much below: not over a railing into a drop.
                float down = edge.y - feet.y + vaultMaxDrop * controller.Body.StandingHeight;
                if (!controller.Raycast(beyond, Vector3.down, down, out _)) { return false; }
            }

            // Room to rise up to the top where the character is, and to be on it with its whole body past the wall's
            // face, where the vault ends.
            float clearedHeight = edge.y + VaultClearance(controller);
            if (!controller.CanFitAt(new Vector3(feet.x, Mathf.Max(feet.y, clearedHeight), feet.z))) { return false; }
            float headingIn = Mathf.Max(Vector3.Dot(heading, -wallNormal), 0.5f);
            float toFace = Mathf.Max(0f, Vector3.Dot(edge - feet, -wallNormal));
            Vector3 end = feet + heading * ((toFace + controller.Body.BackExtent) / headingIn);
            end.y = clearedHeight;
            if (!controller.CanFitAt(end)) { return false; }

            float minSpeed = vaultMinSpeed * controller.SpeedScale;
            float speed = Mathf.Max(moving.magnitude, minSpeed, 1e-3f);
            vault.elapsed = 0f;
            vault.outOfWater = false;
            vault.obstacle = top.transform;
            MovementController.GetPhysicsPose(vault.obstacle, out Vector3 obstaclePosition, out Quaternion obstacleRotation);
            Quaternion toObstacleSpace = Quaternion.Inverse(obstacleRotation);
            vault.localEdge = toObstacleSpace * (edge - obstaclePosition);
            vault.localWallNormal = toObstacleSpace * wallNormal;
            // A hand planted on the top counts as touching the ground: the character's jumps and air dashes come back.
            GiveBackAirMoves(controller);

            if (deep && edge.y - feet.y <= vaultStepUpHeight * controller.Body.StandingHeight)
            {
                // Onto the top of something low and deep: stepped up onto at once, as stairs are, keeping the speed.
                vault.phase = VaultState.Phase.SteppingUp;
                vault.exitVelocity = heading * speed;
                return true;
            }

            // A vault: the rise closes the gap to the wall (from the capsule's front to the face), and the swing over
            // carries the body the rest of the way, speeding up to the speed it leaves with. All along the heading.
            float length = Vector3.Distance(new Vector3(feet.x, 0f, feet.z), new Vector3(end.x, 0f, end.z));
            float gap = Mathf.Max(0f, toFace - controller.Body.FrontExtent) / headingIn;
            vault.phase = VaultState.Phase.Vaulting;
            vault.localStart = toObstacleSpace * (feet - obstaclePosition);
            vault.localEnd = toObstacleSpace * (end - obstaclePosition);
            vault.gapDistance = Mathf.Min(gap, length);
            vault.riseDuration = vaultRiseDuration * controller.DurationScale;
            // Rising, slowing evenly from the speed coming in to the speed that closes the gap over the rise, if that's
            // no faster than coming in; else, too close for that, at the gap's pace throughout, as it can't keep more of
            // the speed without reaching the wall before it's up.
            float even = vault.gapDistance / vault.riseDuration;
            float approachSpeed = Mathf.Max(0f, Vector3.Dot(moving, heading));
            vault.riseStartSpeed = approachSpeed <= 2f * even ? Mathf.Max(approachSpeed, even) : even;
            vault.crossStartSpeed = 2f * even - vault.riseStartSpeed;
            vault.exitSpeed = Mathf.Max(minSpeed, speed * vaultSpeedRetention);
            vault.crossDuration = 2f * (length - vault.gapDistance) / (vault.crossStartSpeed + vault.exitSpeed);
            vault.exitVelocity = heading * vault.exitSpeed;
            return true;
        }

        /// <summary>How far above the top vaulted over the character's feet go, so its capsule clears the edge.</summary>
        private static float VaultClearance(MovementController controller) => 2f * controller.Skin;

        /// <summary>Ends a vault or step up, the character carrying on at the speed it leaves with.</summary>
        private static void EndVault(ref MovementData movementData, MovementController controller)
        {
            VaultState vault = controller.vault;
            vault.phase = VaultState.Phase.None;
            vault.obstacle = null;
            movementData.initialInertia = vault.exitVelocity;
        }
    }

    /// <summary>
    /// A vault over something low, or a step up onto it, in progress. Positions are kept in the space of the object
    /// vaulted over, so a vault over something moving still goes over it.
    /// </summary>
    public class VaultState
    {
        /// <summary>Neither; vaulting over something; or stepping up onto it.</summary>
        public enum Phase { None, Vaulting, SteppingUp }

        /// <summary>
        /// The share of a vault's Progress spent rising: about when the Quaternius library's safety vault, played over the
        /// vault, carries its body over the hand it has planted.
        /// </summary>
        public const float RiseShare = 0.35f;

        /// <summary>Whether the character is vaulting or stepping up (None when it's doing neither).</summary>
        public Phase phase;
        /// <summary>
        /// Whether this vault is out of the water (see Swimming): a swimmer pulling itself up over or onto something,
        /// which animation shows as a climb rather than a vault.
        /// </summary>
        public bool outOfWater;
        /// <summary>The object vaulted over (its Rigidbody's, if it has one).</summary>
        public Transform obstacle;
        /// <summary>The point on the edge vaulted over (on its top, in front of the character), in the obstacle's space.</summary>
        public Vector3 localEdge;
        /// <summary>
        /// The wall's normal below the edge vaulted over (pointing toward the character where the vault started), in the
        /// obstacle's space.
        /// </summary>
        public Vector3 localWallNormal;
        /// <summary>Where the character's feet start a vault, in the obstacle's space.</summary>
        public Vector3 localStart;
        /// <summary>Where the character's feet end a vault (over the edge, the whole body past the wall's face), in the obstacle's space.</summary>
        public Vector3 localEnd;
        /// <summary>How far (m) the rise carries the character toward the wall, closing the gap to it.</summary>
        public float gapDistance;
        /// <summary>Seconds the rise takes.</summary>
        public float riseDuration;
        /// <summary>Seconds the swing over, after the rise, takes.</summary>
        public float crossDuration;
        /// <summary>How fast (m/s) the rise starts, slowing evenly to the swing over's start.</summary>
        public float riseStartSpeed;
        /// <summary>How fast (m/s) the swing over starts.</summary>
        public float crossStartSpeed;
        /// <summary>The speed (m/s) the swing over speeds up to, which the character leaves with.</summary>
        public float exitSpeed;
        /// <summary>The velocity the character leaves with (and, stepping up, moves at), in world space.</summary>
        public Vector3 exitVelocity;
        /// <summary>Seconds since the vault or step up started.</summary>
        public float elapsed;

        /// <summary>Whether the character is vaulting or stepping up.</summary>
        public bool IsActive => phase != Phase.None;
        /// <summary>Whether the character is vaulting over something.</summary>
        public bool IsVaulting => phase == Phase.Vaulting;
        /// <summary>Whether the character is stepping up onto something.</summary>
        public bool IsSteppingUp => phase == Phase.SteppingUp;

        /// <summary>
        /// How far through the vault the character is, for its animation: from 0 to RiseShare over the rise, then on to
        /// 1 over the swing over (and still 1 on the step it ends, once the phase has been cleared).
        /// </summary>
        public float Progress
        {
            get
            {
                if (elapsed < riseDuration) { return RiseShare * elapsed / Mathf.Max(riseDuration, 1e-4f); }
                return Mathf.Lerp(RiseShare, 1f, crossDuration > 0f ? Mathf.Clamp01((elapsed - riseDuration) / crossDuration) : 1f);
            }
        }

        /// <summary>
        /// Where the edge being vaulted over (or stepped up onto) is now, in world space, and the wall's normal below it
        /// (horizontal, toward where the character started), from the obstacle's transform: its rendered pose, for
        /// placing a hand or the feet on it. False when not vaulting or stepping up.
        /// </summary>
        public bool TryGetEdge(out Vector3 edge, out Vector3 wallNormal)
        {
            edge = wallNormal = Vector3.zero;
            if (!IsActive || obstacle == null) { return false; }
            edge = obstacle.position + obstacle.rotation * localEdge;
            wallNormal = obstacle.rotation * localWallNormal;
            wallNormal.y = 0f;
            if (wallNormal.sqrMagnitude < 1e-6f) { return false; }
            wallNormal.Normalize();
            return true;
        }
    }
}

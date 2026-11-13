using UnityEngine;

namespace Character
{
    // Vaulting: heading into something whose top is too low to grab as a ledge but too high to step onto, in mid-air or
    // sprinting at it on the ground, the character puts a hand on the top and swings itself over. Onto the top of
    // something low and deep (a platform rather than a wall), it steps up instead, at once, as it does onto stairs.
    public partial class BasicMovementMode
    {
        [Header("Vaulting")]
        [Tooltip("Seconds a vault spends getting a hand onto the top and lifting the body up to it, at the character's " +
                 "authored size, before it swings over (speeding up to the speed it leaves with). 0 turns vaulting off")]
        [SerializeField] private float vaultRiseDuration = 0.25f;
        [Tooltip("The share of the speed the character came in at that it leaves a vault with")]
        [Range(0f, 1f)] [SerializeField] private float vaultSpeedRetention = 0.75f;
        [Tooltip("The slowest (m/s) a vault leaves the character, at its authored size: a slower approach is sped up to " +
                 "this, so a vault is always brisk")]
        [SerializeField] private float vaultMinSpeed = 3f;
        [Tooltip("Whether sprinting into something low on the ground vaults it, as running into it in mid-air does")]
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

        float VaultMode.vaultRiseDuration { get => vaultRiseDuration; set => vaultRiseDuration = value; }

        /// <summary>
        /// Starts a vault (or a step up) when the character heads into a wall just ahead whose top is above the step
        /// height but below the lowest ledge it could grab, with room to go over it, and that isn't a step (a staircase
        /// rising ahead, say): in mid-air, not rising fast, steering into it or moving toward it near head-on; or on the
        /// ground, sprinting into it head-on, with somewhere to land beyond it. Carries it on: a vault spends its rise
        /// time closing in on the wall while rising to the top's height, then swings over the edge, speeding up to the
        /// speed it leaves with, until the whole body is past the wall's face, without gravity. A step up lifts the
        /// character onto the top as fast as it takes to reach the wall, keeping its speed.
        /// </summary>
        public bool ProcessMovementDataForVault(ref MovementData movementData, Inputs inputs, MovementController controller)
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

            // Rising: closing the gap to the wall at an even pace while lifted to the top's height, easing in to it.
            // Then over: the rest of the way, at the top's height, speeding up evenly to the speed it leaves with, so it
            // doesn't drift over slowly and then shoot off.
            float distance, up;
            bool over = false;
            if (vault.elapsed < vault.riseDuration)
            {
                float rise = vault.elapsed / vault.riseDuration;
                distance = vault.gapDistance * rise;
                up = Mathf.SmoothStep(0f, 1f, rise);
            }
            else
            {
                float t = Mathf.Min(vault.elapsed - vault.riseDuration, vault.crossDuration);
                float acceleration = vault.crossDuration > 0f ? (vault.exitSpeed - vault.crossStartSpeed) / vault.crossDuration : 0f;
                distance = Mathf.Min(length, vault.gapDistance + vault.crossStartSpeed * t + 0.5f * acceleration * t * t);
                up = 1f;
                over = vault.elapsed >= vault.riseDuration + vault.crossDuration;
            }
            Vector3 point = start + direction * distance + Vector3.up * ((end.y - start.y) * up);

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
                float gap = Mathf.Max(0f, -past - controller.Radius);
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
        /// height and below the lowest ledge it could grab (see ProcessMovementDataForVault), with room to rise up to it
        /// where the character is and to be on top of it just past the edge.
        /// </summary>
        private bool TryStartVault(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            VaultState vault = controller.vault;
            if (vaultRiseDuration <= 0f || controller.crouch.IsCrouched || AbilityInProgress(controller)) { return false; }
            bool grounded = movementData.initialGrounded;
            Vector3 steer = InputDirection(movementData, inputs);
            if (grounded)
            {
                bool sprinting = inputs.sprint != null && inputs.sprint.value;
                if (!vaultFromGround || !sprinting || steer.sqrMagnitude < 0.25f) { return false; }
            }
            else if (movementData.initialVelocity.y > ledgeMaxGrabRiseSpeed * controller.SpeedScale) { return false; }

            // Which way the character is heading: as steered, or else as it's moving.
            Vector3 velocity = movementData.initialVelocity;
            Vector3 moving = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 heading = steer.sqrMagnitude > 0.01f ? steer.normalized
                : moving.sqrMagnitude > 0.01f ? moving.normalized : Vector3.zero;
            if (heading == Vector3.zero) { return false; }

            // From just over a step to just under a ledge the character could grab. Looked for far enough ahead that a
            // fast approach is caught before the next step takes it into the wall.
            float stepHeight = controller.MaxStepHeight * controller.HeightScale;
            float high = ledgeGrabMinHeight * controller.StandingHeight;
            float reach = controller.Radius + Mathf.Max(ledgeGrabReach * controller.HeightScale, 2f * moving.magnitude * movementData.deltaTime);
            Vector3 feet = movementData.initialPosition;
            if (!FindLedge(controller, feet, heading, stepHeight, high, reach, out RaycastHit top, out Vector3 wallNormal, out Vector3 edge)) { return false; }
            bool steeringIn = Vector3.Dot(steer, -wallNormal) >= (grounded ? Mathf.Cos(vaultGroundMaxAngle * Mathf.Deg2Rad) : 0.5f);
            bool movingIn = !grounded && Vector3.Dot(moving, -wallNormal) >= 1f * controller.SpeedScale;
            if (!steeringIn && !movingIn) { return false; }

            // Not a step: something to stand on just in front of the wall, no more than a step below its top (the step
            // before it, on a staircase rising ahead), means it's climbed as stairs are, not vaulted. Vaults found the
            // risers of stairs a couple of steps ahead, whose treads are within the vault's heights.
            float skin = controller.Skin;
            Vector3 inFront = edge + wallNormal * (2f * skin + 0.05f * controller.HeightScale) + Vector3.up * skin;
            if (controller.Raycast(inFront, Vector3.down, stepHeight + skin, out _)) { return false; }

            // What's beyond the edge, a body's width past the wall's face: the top carrying on (something deep, to
            // stand on), or else whatever's below, where the character lands after vaulting over.
            Vector3 beyond = edge - wallNormal * (2f * controller.Radius + 2f * skin);
            bool deep = controller.Raycast(beyond + Vector3.up * stepHeight, Vector3.down, 2f * stepHeight, out RaycastHit far)
                        && Vector3.Angle(far.normal, Vector3.up) <= controller.MaxSlopeAngle;
            if (grounded && !deep)
            {
                // From the ground, only over to somewhere to land, not much below: not over a railing into a drop.
                float down = edge.y - feet.y + vaultMaxDrop * controller.StandingHeight;
                if (!controller.Raycast(beyond, Vector3.down, down, out _)) { return false; }
            }

            // Room to rise up to the top where the character is, and to be on it with its whole body past the wall's
            // face, where the vault ends.
            float clearedHeight = edge.y + VaultClearance(controller);
            if (!controller.CanFitAt(new Vector3(feet.x, Mathf.Max(feet.y, clearedHeight), feet.z), 1f)) { return false; }
            float headingIn = Mathf.Max(Vector3.Dot(heading, -wallNormal), 0.5f);
            float toFace = Mathf.Max(0f, Vector3.Dot(edge - feet, -wallNormal));
            Vector3 end = feet + heading * ((toFace + controller.Radius) / headingIn);
            end.y = clearedHeight;
            if (!controller.CanFitAt(end, 1f)) { return false; }

            float minSpeed = vaultMinSpeed * controller.SpeedScale;
            float speed = Mathf.Max(moving.magnitude, minSpeed, 1e-3f);
            vault.elapsed = 0f;
            vault.obstacle = top.transform;
            MovementController.GetPhysicsPose(vault.obstacle, out Vector3 obstaclePosition, out Quaternion obstacleRotation);
            Quaternion toObstacleSpace = Quaternion.Inverse(obstacleRotation);
            vault.localEdge = toObstacleSpace * (edge - obstaclePosition);
            vault.localWallNormal = toObstacleSpace * wallNormal;
            // A hand planted on the top counts as touching the ground: the character's jumps and air dashes come back.
            GiveBackAirMoves(controller);

            if (deep && edge.y - feet.y <= vaultStepUpHeight * controller.StandingHeight)
            {
                // Onto the top of something low and deep: stepped up onto at once, as stairs are, keeping the speed.
                vault.phase = VaultState.Phase.SteppingUp;
                vault.exitVelocity = heading * speed;
                return true;
            }

            // A vault: the rise closes the gap to the wall (from the capsule's front to the face), and the swing over
            // carries the body the rest of the way, speeding up to the speed it leaves with. All along the heading.
            float length = Vector3.Distance(new Vector3(feet.x, 0f, feet.z), new Vector3(end.x, 0f, end.z));
            float gap = Mathf.Max(0f, toFace - controller.Radius) / headingIn;
            vault.phase = VaultState.Phase.Vaulting;
            vault.localStart = toObstacleSpace * (feet - obstaclePosition);
            vault.localEnd = toObstacleSpace * (end - obstaclePosition);
            vault.gapDistance = Mathf.Min(gap, length);
            vault.riseDuration = vaultRiseDuration * controller.DurationScale;
            vault.crossStartSpeed = vault.gapDistance / vault.riseDuration;
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
}

using Level;
using UnityEngine;

namespace Character
{
    // Pushing and pulling: holding the grab button by a large object (a PushableObject) takes hold of it, and movement
    // input toward or away from it pushes or pulls it along the ground.
    public partial class BasicMovementMode
    {
        [Header("Pushing & Pulling")]
        [Tooltip("How fast (m/s) the character pushes or pulls an object, at its authored size, before the object's own " +
                 "speed multiplier (lower for heavier objects)")]
        [SerializeField] private float pushPullSpeed = 1.8f;
        [Tooltip("How far (m) in front of the character an object can be to take hold of it, at its authored size")]
        [SerializeField] private float grabReach = 0.25f;
        [Tooltip("How far (degrees) from facing an object's side head-on the character can be and still take hold of it")]
        [Range(0f, 90f)] [SerializeField] private float grabMaxAngle = 50f;

        // How far (m, at the authored size) the character can end up from where it holds the object (the object was
        // stopped by something the character wasn't, say) before it lets go.
        private const float MaxHoldDrift = 0.15f;

        float PushPullMode.pushPullSpeed { get => pushPullSpeed; set => pushPullSpeed = value; }

        /// <summary>
        /// Takes hold of an object in front of the character while the grab button is held on the ground, and moves
        /// with it: movement input toward the object pushes it, and away from it pulls it (if it can be pulled), at
        /// the push and pull speed; sideways input does nothing. The character faces the object and can't jump, dash,
        /// crouch or roll while holding it. Letting go of the button, leaving the ground, or the object losing the
        /// ground under it (pushed off an edge, it falls) lets go.
        /// </summary>
        public bool ProcessMovementDataForPushPull(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            GrabState grab = controller.grab;
            bool held = inputs.grab != null && inputs.grab.value;
            if (!grab.IsGrabbing)
            {
                if (!TryGrab(movementData, held, controller)) { return false; }
            }
            else if (!held || !movementData.initialGrounded || !grab.target.isActiveAndEnabled || !grab.target.IsSupported)
            {
                LetGo(controller);
                return false;
            }

            PushableObject target = grab.target;
            Vector3 toward = -grab.faceNormal;
            // Where the character holds the object, which it keeps to as the object moves.
            Vector3 drift = target.Position + target.Rotation * grab.localHoldOffset - movementData.initialPosition;
            drift.y = 0f;
            if (drift.magnitude > MaxHoldDrift * controller.HeightScale)
            {
                LetGo(controller);
                return false;
            }

            float along = Vector3.Dot(InputDirection(movementData, inputs), toward);
            if (along < 0f && !target.CanBePulled) { along = 0f; }
            Vector3 direction = along >= 0f ? toward : -toward;
            float distance = Mathf.Abs(along) * pushPullSpeed * controller.SpeedScale * target.SpeedMultiplier * movementData.deltaTime;
            // Pushing goes only as far as the object can: the character's own collision ignores the object it holds.
            if (along > 0f) { distance = target.FreeDistance(direction, distance); }

            Vector3 inertia = movementData.initialInertia;
            movementData.initialInertia = new Vector3(0f, inertia.y, 0f);
            // The drift is made up by the character alone (see AfterMovementResolved).
            grab.holdCorrection = drift;
            movementData.directMovementVector = direction * distance + drift;
            movementData.targetRotation = Quaternion.LookRotation(toward, Vector3.up);
            movementData.state |= CharacterState.Grabbing | CharacterState.Moving;
            return true;
        }

        /// <summary>
        /// Moves the held object with the character, by however far the character moved toward or away from it this step
        /// (it may have been blocked; the object goes no further than it can either).
        /// </summary>
        public override void AfterMovementResolved(MovementData movementData, MovementController controller)
        {
            GrabState grab = controller.grab;
            if (!grab.IsGrabbing || !movementData.state.HasFlag(CharacterState.Grabbing)) { return; }
            Vector3 toward = -grab.faceNormal;
            float along = Vector3.Dot(movementData.finalMovementVector - grab.holdCorrection, toward);
            if (Mathf.Abs(along) > 1e-6f) { grab.target.Move(toward * along); }
        }

        /// <summary>
        /// Takes hold of an object if the grab button is held on the ground and the character faces one of its sides,
        /// close enough to reach it.
        /// </summary>
        private bool TryGrab(MovementData movementData, bool held, MovementController controller)
        {
            if (!held || !movementData.initialGrounded || controller.crouch.IsCrouched || AbilityInProgress(controller)) { return false; }
            Vector3 feet = movementData.initialPosition;
            Vector3 facing = Facing(movementData);
            if (!controller.SweepCapsule(feet, facing, grabReach * controller.HeightScale + controller.Skin, out RaycastHit hit)) { return false; }
            PushableObject target = hit.collider.GetComponentInParent<PushableObject>();
            if (target == null || !target.isActiveAndEnabled) { return false; }

            // The side held is the object's face nearest the hit's normal (a hit on an edge or corner has a normal
            // between two faces).
            Vector3 normal = NearestSide(target.Rotation, hit.normal);
            if (Vector3.Angle(facing, -normal) > grabMaxAngle) { return false; }

            GrabState grab = controller.grab;
            grab.target = target;
            grab.faceNormal = normal;
            grab.localHoldOffset = Quaternion.Inverse(target.Rotation) * (feet - target.Position);
            target.SetHolder(controller.transform);
            controller.IgnoredCollisionRoot = target.transform;
            return true;
        }

        private static readonly Vector3[] SideAxes = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

        /// <summary>Of an object's four sides (its local ±x and ±z, flattened), the one whose normal is nearest the given direction.</summary>
        private static Vector3 NearestSide(Quaternion rotation, Vector3 direction)
        {
            Vector3 best = Vector3.zero;
            float bestDot = float.NegativeInfinity;
            foreach (Vector3 axis in SideAxes)
            {
                Vector3 side = rotation * axis;
                side.y = 0f;
                if (side.sqrMagnitude < 1e-6f) { continue; }
                side.Normalize();
                float dot = Vector3.Dot(side, direction);
                if (dot > bestDot) { bestDot = dot; best = side; }
            }
            return best;
        }

        private static void LetGo(MovementController controller)
        {
            GrabState grab = controller.grab;
            if (grab.target != null) { grab.target.SetHolder(null); }
            grab.target = null;
            controller.IgnoredCollisionRoot = null;
        }
    }
}

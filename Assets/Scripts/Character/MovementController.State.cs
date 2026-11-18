using System.Runtime.InteropServices;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// A character's movement state, saved (MovementController.SaveState) to be put back later (RestoreState): everything
    /// its next step reads or changes, so after putting it back the steps that follow, given the same input frames in the
    /// same world, are exactly the ones that followed it before. For rolling a character back to a past step and stepping
    /// it forward again (a networked game's correction from the server: restore its state for a tick, replay the inputs
    /// since). In this process only: it holds the character's own objects, and the scene objects it was on or holding by
    /// reference. What crosses a network is its Summary.
    /// </summary>
    public sealed class MovementStateSnapshot
    {
        internal MovementController owner;
        internal ObjectGraphSnapshot movement, controller;
        internal Vector3 position;
        internal Quaternion rotation;

        /// <summary>The character's step tick when it was saved (MovementController.StepTick).</summary>
        public long StepTick { get; internal set; }
        /// <summary>The compact part of it (see MovementStateSummary).</summary>
        public MovementStateSummary Summary { get; internal set; }
    }

    /// <summary>
    /// The compact, unmanaged part of a character's state: its pose, its momentum, what it's doing, its clock and its
    /// latest input frame. What a server sends to say where a character is (to correct a client's prediction of it, or to
    /// draw someone else's character between updates). Not enough to restore a character exactly by itself: an ability
    /// under way keeps more (the wall it runs along, the rope it holds), which MovementStateSnapshot carries.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MovementStateSummary
    {
        /// <summary>The steps it had taken (MovementController.Tick).</summary>
        public uint tick;
        /// <summary>Its feet (Rigidbody position) and facing.</summary>
        public Vector3 position;
        public Quaternion rotation;
        /// <summary>Its velocity and the momentum it carries (its last step's).</summary>
        public Vector3 velocity, inertia;
        /// <summary>What it was doing (its last step's state).</summary>
        public CharacterState state;
        /// <summary>The last input frame it was given.</summary>
        public CharacterInputFrame input;
    }

    // Saving and restoring the character's state (see MovementStateSnapshot).
    public partial class MovementController
    {
        /// <summary>
        /// Saves the character's movement state as it is now (between steps): the controller and everything of its own
        /// it reaches (its body, each ability's state, its input state, its last steps' data), its ACCharacterController's
        /// (a change of size under way), and its Rigidbody's pose. Allocates: for a rollback, not every step.
        /// </summary>
        public MovementStateSnapshot SaveState()
        {
            ACCharacterController character = characterController != null ? characterController : GetComponent<ACCharacterController>();
            return new MovementStateSnapshot
            {
                owner = this,
                movement = ObjectGraphSnapshot.Capture(this, typeof(MonoBehaviour)),
                controller = character != null ? ObjectGraphSnapshot.Capture(character, typeof(MonoBehaviour)) : null,
                position = PhysicsPosition,
                rotation = rb != null ? rb.rotation : transform.rotation,
                StepTick = StepTick,
                Summary = Summarize(),
            };
        }

        /// <summary>
        /// Puts the character back as it was when the snapshot was saved (between steps), its Rigidbody and transform at
        /// its pose then. False, changing nothing, for another character's snapshot.
        /// </summary>
        public bool RestoreState(MovementStateSnapshot snapshot)
        {
            if (snapshot == null || snapshot.owner != this) { return false; }
            snapshot.movement.Restore();
            snapshot.controller?.Restore();
            if (rb != null)
            {
                rb.position = snapshot.position;
                rb.rotation = snapshot.rotation;
            }
            transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);
            Physics.SyncTransforms();
            return true;
        }

        /// <summary>The character's state now, compactly (see MovementStateSummary).</summary>
        public MovementStateSummary Summarize()
        {
            MovementData last = lastFrameMovementData;
            return new MovementStateSummary
            {
                tick = Tick,
                position = PhysicsPosition,
                rotation = rb != null ? rb.rotation : transform.rotation,
                velocity = last != null ? last.finalVelocity : Vector3.zero,
                inertia = last != null ? last.finalInertia : Vector3.zero,
                state = last != null ? last.state : CharacterState.Idle,
                input = InputState.Frame,
            };
        }
    }
}

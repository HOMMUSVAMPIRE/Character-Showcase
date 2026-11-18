using UnityEngine;

namespace Character
{
    /// <summary>The base class for scriptable objects which control the ways characters move.</summary>
    public abstract class MovementMode : ScriptableObject
    {
        /// <summary>
        /// One physics step of the mode's movement, called by the MovementController: turns the inputs into how the
        /// character means to move this step (movementData's direct movement, velocity change or override, rotation and
        /// state), before it's resolved against the world.
        /// </summary>
        public virtual void ProcessMovementData(MovementData movementData, Inputs inputs, MovementController controller)
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

        /// <summary>
        /// Adds to the step's events the starts and ends of the states this mode's movement sets (an ability's: see
        /// StateEvent), found by comparing the step's state with the last's. Called by the MovementController after the
        /// step.
        /// </summary>
        public virtual void DetectStateEvents(MovementData data)
        {
        }

        /// <summary>
        /// Measures what this mode lets a character do (see MovementController.Capabilities), by running its movement in
        /// the given simulation, into capabilities (whose step height, slope, stand height and radius are already filled
        /// in from the character). A mode measures the moves it has: jumps, climbs, and its abilities' reach.
        /// </summary>
        public virtual void MeasureCapabilities(MovementSimulation simulation, MovementCapabilities capabilities)
        {
        }
    }
}

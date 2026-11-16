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

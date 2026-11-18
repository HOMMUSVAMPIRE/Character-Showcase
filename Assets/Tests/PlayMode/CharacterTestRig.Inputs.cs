using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterShowcase.Tests
{
    // The rig's inputs: the scripted inputs tests give the character, a step at a time.
    public sealed partial class CharacterTestRig
    {
        public static Inputs Idle() => Move(Vector3.zero);

        /// <summary>Movement input in world space (the test camera looks down +Z).</summary>
        public static Inputs Move(Vector3 direction) => new()
        {
            movementVector = Vector3.ClampMagnitude(new Vector3(direction.x, 0f, direction.z), 1f),
            jump = new InputButton(),
            dash = new InputButton(),
        };

        // A step's input says only which buttons are down (and the move input): the character works out presses and how
        // long a button's been held from the steps it's down in (see InputState). So a press is a button down in a step
        // after one it was up in, and holding it is giving it down step after step.

        /// <summary>Movement input in world space with Sprint held.</summary>
        public static Inputs Sprint(Vector3 direction)
        {
            Inputs inputs = Move(direction);
            inputs.sprint = Held();
            return inputs;
        }

        /// <summary>Jump down this step, with optional movement input.</summary>
        public static Inputs Jump(Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.jump = Held();
            return inputs;
        }

        /// <summary>Dash down this step, with optional movement input.</summary>
        public static Inputs Dash(Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.dash = Held();
            return inputs;
        }

        /// <summary>A button down this step.</summary>
        public static InputButton Held() => new() { value = true };

        /// <summary>Crouch down this step, with optional movement input.</summary>
        public static Inputs Crouch(Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.crouch = Held();
            return inputs;
        }

        /// <summary>Roll down this step, with optional movement input.</summary>
        public static Inputs Roll(Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.roll = Held();
            return inputs;
        }

        /// <summary>Grab down this step, with optional movement input.</summary>
        public static Inputs Grab(Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.grab = Held();
            return inputs;
        }
    }
}

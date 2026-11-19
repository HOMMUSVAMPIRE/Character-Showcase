using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeridianMotion.Tests
{
    // The rig's inputs: the scripted inputs tests give the character, a step at a time.
    public sealed partial class CharacterTestRig
    {
        public static StepInputs Idle() => Move(Vector3.zero);

        /// <summary>Movement input in world space (the test camera looks down +Z).</summary>
        public static StepInputs Move(Vector3 direction) => new()
        {
            movementVector = Vector3.ClampMagnitude(new Vector3(direction.x, 0f, direction.z), 1f),
            jump = new InputButton(),
            dash = new InputButton(),
        };

        // A step's input says only which buttons are down (and the move input): the character works out presses and how
        // long a button's been held from the steps it's down in (see InputState). So a press is a button down in a step
        // after one it was up in, and holding it is giving it down step after step.

        /// <summary>Movement input in world space with Sprint held.</summary>
        public static StepInputs Sprint(Vector3 direction)
        {
            StepInputs inputs = Move(direction);
            inputs.sprint = Held();
            return inputs;
        }

        /// <summary>Jump down this step, with optional movement input.</summary>
        public static StepInputs Jump(Vector3 direction = default)
        {
            StepInputs inputs = Move(direction);
            inputs.jump = Held();
            return inputs;
        }

        /// <summary>Dash down this step, with optional movement input.</summary>
        public static StepInputs Dash(Vector3 direction = default)
        {
            StepInputs inputs = Move(direction);
            inputs.dash = Held();
            return inputs;
        }

        /// <summary>A button down this step.</summary>
        public static InputButton Held() => new() { held = true };

        /// <summary>Crouch down this step, with optional movement input.</summary>
        public static StepInputs Crouch(Vector3 direction = default)
        {
            StepInputs inputs = Move(direction);
            inputs.crouch = Held();
            return inputs;
        }

        /// <summary>Roll down this step, with optional movement input.</summary>
        public static StepInputs Roll(Vector3 direction = default)
        {
            StepInputs inputs = Move(direction);
            inputs.roll = Held();
            return inputs;
        }

        /// <summary>Grab down this step, with optional movement input.</summary>
        public static StepInputs Interact(Vector3 direction = default)
        {
            StepInputs inputs = Move(direction);
            inputs.interact = Held();
            return inputs;
        }
    }
}

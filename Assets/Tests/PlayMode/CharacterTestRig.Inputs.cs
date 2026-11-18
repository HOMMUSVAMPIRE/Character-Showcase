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

        /// <summary>Movement input in world space with Sprint held (on the given step of the press; 0 = the step it was pressed).</summary>
        public static Inputs Sprint(Vector3 direction, int heldSteps = 1)
        {
            Inputs inputs = Move(direction);
            inputs.sprint = new InputButton
            {
                value = true,
                justPressed = heldSteps == 0,
                holdDuration = heldSteps * Time.fixedDeltaTime,
            };
            return inputs;
        }

        /// <summary>Jump held for the given step of the press (0 = the step it was pressed).</summary>
        public static Inputs Jump(int heldSteps, Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.jump = new InputButton
            {
                value = true,
                justPressed = heldSteps == 0,
                holdDuration = heldSteps * Time.fixedDeltaTime,
            };
            return inputs;
        }

        /// <summary>Dash held for the given step of the press (0 = the step it was pressed), with optional movement input.</summary>
        public static Inputs Dash(int heldSteps, Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.dash = new InputButton
            {
                value = true,
                justPressed = heldSteps == 0,
                holdDuration = heldSteps * Time.fixedDeltaTime,
            };
            return inputs;
        }

        /// <summary>A button held for the given step of its press (0 = the step it was pressed).</summary>
        public static InputButton Held(int heldSteps) => new()
        {
            value = true,
            justPressed = heldSteps == 0,
            holdDuration = heldSteps * Time.fixedDeltaTime,
        };

        /// <summary>Crouch held for the given step of the press (0 = the step it was pressed), with optional movement input.</summary>
        public static Inputs Crouch(int heldSteps, Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.crouch = Held(heldSteps);
            return inputs;
        }

        /// <summary>Roll held for the given step of the press (0 = the step it was pressed), with optional movement input.</summary>
        public static Inputs Roll(int heldSteps, Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.roll = Held(heldSteps);
            return inputs;
        }

        /// <summary>Grab held for the given step of the press (0 = the step it was pressed), with optional movement input.</summary>
        public static Inputs Grab(int heldSteps, Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.grab = Held(heldSteps);
            return inputs;
        }
    }
}

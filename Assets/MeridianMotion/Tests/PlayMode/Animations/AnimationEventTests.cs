using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Animations
{
    /// <summary>
    /// Tests of how the movement's events reach animation: the MMotionAnimator passes on every movement event, in
    /// the order raised, along with the animation state at the time (see MovementEventTests for the events themselves).
    /// </summary>
    [Category(TestArea.Animation)]
    public class AnimationEventTests : CharacterRigFixture
    {
        [UnityTest]
        public IEnumerator AnimationController_PassesEventsOn_WithTheAnimationState()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            rig.ClearEvents();
            var received = new List<(MovementEvents movementEvent, CharacterAnimationState state)>();
            rig.Animation.MovementEventReceived += (movementEvent, state) => received.Add((movementEvent, state));

            yield return rig.Run(0.1f, i => i == 0 ? CharacterTestRig.Jump() : CharacterTestRig.Idle());

            Assert.That(received.Select(r => r.movementEvent), Is.EqualTo(rig.EventsRaised),
                "The MMotionAnimator should pass on every movement event, in order.");
            var jumped = received.First(r => r.movementEvent == MovementEvents.Jumped);
            Assert.That(jumped.state.jumping, Is.True);
            Assert.That(jumped.state.verticalSpeed, Is.GreaterThan(0f));
        }
    }
}

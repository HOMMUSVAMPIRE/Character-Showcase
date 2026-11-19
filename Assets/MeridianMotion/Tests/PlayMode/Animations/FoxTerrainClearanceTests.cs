using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.Animations
{
    /// <summary>
    /// Tests that the fox stays out of the ground where it's steepest: walking up and down a flight of stairs and up a
    /// steep ramp, no part of its mesh goes into them (its legs all but touching them), its body pitched with them. Each
    /// measures the whole posed mesh against the level's boxes every step, the slowest of the fox's checks, so they're
    /// a fixture of their own, run when the paw placement or the fox's body changes. (FoxPawPlacementTests has the rest.)
    /// </summary>
    public class FoxTerrainClearanceTests : FoxPawTestsBase
    {

        /// <summary>
        /// Walking up or down a flight of stairs (as steep as the levels'), the fox stays out of the steps (its legs all
        /// but touching them: see IntoGround), its body pitched up or down the flight, not left level as the flat
        /// animation has it. Its long box body rests on the step under its front end going up and its back end going
        /// down, half a metre above the flight under its middle: its body has to be lowered onto the flight, along it.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingUpOrDownStairs_TheFoxStaysOutOfTheSteps_AndPitchesWithThem([Values(true, false)] bool up)
        {
            rig.Floor();
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            // As steep as the levels' stairs (3 up to 4 along), steps a little under the step height.
            const int steps = 12;
            float rise = 0.85f * rig.StepHeight, run = 4f / 3f * rise, top = steps * rise;
            for (int i = 0; i < steps - 1; i++)
            {
                float height = up ? rise * (i + 1) : top - rise * (i + 1);
                rig.Box($"Step {i + 1}", new Vector3(0f, height / 2f, 1.5f + i * run + run / 2f), new Vector3(6f, height, run));
            }
            rig.Box("Landing", new Vector3(0f, top / 2f, up ? 1.5f + (steps - 1) * run + 5f : -3.5f), new Vector3(6f, top, 10f));
            Physics.SyncTransforms();
            if (!up)
            {
                rig.Teleport(new Vector3(0f, top + 0.02f, -1f));
                yield return Settle();
            }
            float legDepth = 0f, bodyDepth = 0f, pitch = 0f;
            float firstStep = 1.5f, lastStep = 1.5f + (steps - 1) * run;
            yield return rig.Run(rig.SecondsToWalk(lastStep - firstStep + 2f), _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAndPlace(step);
                float z = rig.Position.z;
                if (z < firstStep - 0.5f) { return; } // From reaching the flight (not starting off on the flat).
                (float legsIn, float bodyIn) = IntoGround();
                legDepth = Mathf.Max(legDepth, legsIn);
                bodyDepth = Mathf.Max(bodyDepth, bodyIn);
                // Pitched most on the flight's middle, with all four paws on it.
                if (z > firstStep + 1f && z < lastStep - 1f) { pitch = up ? Mathf.Max(pitch, paws.BodyPitch) : Mathf.Min(pitch, paws.BodyPitch); }
            });
            float slope = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            TestContext.WriteLine($"At deepest, its legs went {legDepth * 1000f:0} mm into the steps and its body {bodyDepth * 1000f:0} mm; " +
                                  $"its body pitched as much as {pitch:0.0}° on {slope:0.0}° stairs.");
            Assume.That(rig.Position.z, Is.GreaterThan(lastStep), "Test setup: the fox should walk the flight.");
            Assert.That(bodyDepth, Is.LessThan(BodyIntoGroundTolerance), "The fox's body, head and tail should stay out of the steps.");
            Assert.That(legDepth, Is.LessThan(StairLegsIntoGroundTolerance), "Its legs should stay (all but) out of the steps.");
            Assert.That(Mathf.Abs(pitch), Is.EqualTo(slope).Within(8f), "The body should pitch with the stairs.");
        }

        /// <summary>
        /// Walking up a steep ramp (the levels' steepest slope is 40°), the fox stays out of it, its body pitched with it,
        /// its tail held up off it (Counter Pitch Bones) rather than swung down into it.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingUpASteepRamp_TheFoxStaysOutOfIt([Values(30f, 40f)] float slope)
        {
            rig.Floor();
            Quaternion tilt = Quaternion.Euler(-slope, 0f, 0f);
            const float length = 6f;
            Vector3 bottom = new Vector3(0f, 0f, 1.5f), along = tilt * Vector3.forward * length;
            rig.Box("Ramp", bottom + along / 2f + tilt * Vector3.down * 0.5f, new Vector3(6f, 1f, length), tilt);
            rig.Box("Ramp Top", new Vector3(0f, along.y / 2f, bottom.z + along.z + 5f), new Vector3(6f, along.y, 10f));
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            float legDepth = 0f, bodyDepth = 0f, pitch = 0f;
            yield return rig.Run(rig.SecondsToWalk(along.z), _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAndPlace(step);
                float z = rig.Position.z;
                if (z < bottom.z - 1f || z > bottom.z + along.z) { return; } // On the ramp.
                (float legsIn, float bodyIn) = IntoGround();
                legDepth = Mathf.Max(legDepth, legsIn);
                bodyDepth = Mathf.Max(bodyDepth, bodyIn);
                pitch = Mathf.Max(pitch, paws.BodyPitch);
            });
            TestContext.WriteLine($"At deepest, its legs went {legDepth * 1000f:0} mm into the ramp and its body {bodyDepth * 1000f:0} mm; " +
                                  $"its body pitched as much as {pitch:0.0}° on a {slope}° ramp.");
            Assume.That(rig.Position.z, Is.GreaterThan(bottom.z + 1f), "Test setup: the fox should walk up the ramp.");
            Assert.That(bodyDepth, Is.LessThan(BodyIntoGroundTolerance), "The fox's body, head and tail should stay out of the ramp.");
            Assert.That(legDepth, Is.LessThan(LegsIntoGroundTolerance), "Its legs should stay (all but) out of the ramp.");
            Assert.That(pitch, Is.EqualTo(slope).Within(5f), "The body should pitch with the ramp.");
        }
    }
}

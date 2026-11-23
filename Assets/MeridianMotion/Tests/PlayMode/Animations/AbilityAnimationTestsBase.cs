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
    /// What the fixtures checking the movement's abilities on the character's own model share (AbilityAnimationTests,
    /// TraversalAnimationTests, WaterAnimationTests): spawning it with its animation stepped by hand with the physics,
    /// expecting a state to play, and measuring the posed model.
    /// </summary>
    public abstract class AbilityAnimationTestsBase : AnimationTestsBase
    {
        /// <summary>Whether the base layer is in the named state, or on its way into it.</summary>
        protected bool InAnimatorState(string state) =>
            animator.GetCurrentAnimatorStateInfo(0).IsName(state) || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state));

        /// <summary>Every state the generator makes (HumanoidAnimatorStates), for naming the one playing.</summary>
        private static readonly string[] AllStates = typeof(HumanoidAnimatorStates)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()).ToArray();

        protected string CurrentAnimatorState()
        {
            foreach (string name in AllStates)
            {
                if (animator.GetCurrentAnimatorStateInfo(0).IsName(name)) { return name; }
            }
            return "(another state)";
        }

        /// <summary>Spawns the character with its own model, its animation stepped by hand with the physics.</summary>
        protected IEnumerator Spawn()
        {
            yield return SpawnWithOwnModel();
            TakeOverAnimator();
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle(), StepAnimator);
        }

        /// <summary>Runs the given inputs, stepping the animation, and asserts the named state played at some point.</summary>
        protected IEnumerator ExpectState(string state, float seconds, System.Func<int, StepInputs> inputs)
        {
            bool played = false;
            yield return rig.Run(seconds, inputs, step => { StepAnimator(step); played |= InAnimatorState(state); });
            Assert.That(played, Is.True, $"The '{state}' state should play (the Animator is in '{CurrentAnimatorState()}').");
        }

        /// <summary>The highest point of the model's meshes as they're posed now, in world space.</summary>
        protected float HighestPointOfModel()
        {
            float highest = float.NegativeInfinity;
            var baked = new Mesh();
            foreach (SkinnedMeshRenderer skinned in rig.Models.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinned.BakeMesh(baked, false);
                Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                foreach (Vector3 vertex in baked.vertices) { highest = Mathf.Max(highest, toWorld.MultiplyPoint3x4(vertex).y); }
            }
            Object.Destroy(baked);
            return highest;
        }

        /// <summary>How far a point is from the line between two points (the segment between them).</summary>
        protected static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            float t = Mathf.Clamp01(Vector3.Dot(point - from, along) / Mathf.Max(along.sqrMagnitude, 1e-8f));
            return Vector3.Distance(point, from + along * t);
        }

        /// <summary>The largest change in speed (m/s²) between physics steps of a height over the steps.</summary>
        protected static float PeakJolt(List<float> heights)
        {
            float dt = Time.fixedDeltaTime, peak = 0f;
            for (int i = 2; i < heights.Count; i++) { peak = Mathf.Max(peak, Mathf.Abs(heights[i] - 2f * heights[i - 1] + heights[i - 2]) / (dt * dt)); }
            return peak;
        }
    }
}

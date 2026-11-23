using System.Collections;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Traversal
{
    /// <summary>
    /// Tests of wading: walking (or sprinting) through water too shallow to swim in slows the character by how deep it is,
    /// from full speed at the wade start depth to the wade speed multiplier at the swim depth; water shallower than the
    /// start depth doesn't slow it; walking in and out raises the wading events; walking or sprinting into a low wall vaults
    /// it (on dry land, only sprinting does); and Wading turned off doesn't slow it.
    /// The test camera looks down +Z. Expected values are read from the movement mode's settings. The checks every ability
    /// gets (turned off, letting go, its events, saved and put back, at another size) come from AbilityFixture: set down
    /// on a floor in water half as deep as the character would swim in, walking. It has no setting whose 0 turns it off,
    /// so that check is skipped.
    /// </summary>
    [Category(TestArea.Traversal)]
    public class WadeTests : AbilityFixture<Wading>
    {
        private float StartDepth => rig.MovementSetting<float>("wadeStartDepth") * rig.StandingHeight;
        private float Multiplier => rig.MovementSetting<float>("wadeSpeedMultiplier");

        /// <summary>The share of its speed the character should keep with the water this deep over its feet.</summary>
        private float ExpectedShare(float under) =>
            Mathf.Lerp(1f, Multiplier, Mathf.Clamp01((under - StartDepth) / (rig.SwimDepth - StartDepth)));

        /// <summary>
        /// As StandInWater, without standing there first: wading starts on the kit's first steps (standing in the water
        /// already wades, which would leave the kit's check no start to see).
        /// </summary>
        protected override IEnumerator SetUpToStart()
        {
            yield return SpawnCharacter(Vector3.up * 5f * Scale, settleSeconds: 0f);
            PlaceInWater(Mathf.Lerp(StartDepth, rig.SwimDepth, 0.5f));
        }

        protected override StepInputs InputsFor(int step) => CharacterTestRig.Move(Vector3.forward);

        // Wade Max Depth: at 0 the character wades in no water at all.
        protected override string OffSetting => "wadeMaxDepth";

        // Turned off, either way, the water doesn't slow the walk: by the end of the kit's run (well past getting up to
        // speed), the character walks at its full walking speed.
        protected override Instead WhatHappensInstead(bool byToggle) => new Instead
        {
            Afterwards = () => Assert.That(rig.HorizontalSpeed, Is.EqualTo(rig.WalkSpeed).Within(0.05f),
                "With Wading turned off, the water shouldn't slow the walk."),
        };

        /// <summary>The character standing on a floor with water this far over its feet (worked out from its size, so spawned first).</summary>
        private IEnumerator StandInWater(System.Func<float> depthOverFeet)
        {
            yield return rig.SpawnSettled(Vector3.up * 5f, settleSeconds: 0f);
            PlaceInWater(depthOverFeet());
            yield return rig.Settle();
            Assume.That(rig.Grounded && !rig.Swimming, Is.True, "Test setup: the character should be standing in the water.");
        }

        /// <summary>A floor with water under deep over it, and the character set down on the floor.</summary>
        private void PlaceInWater(float under)
        {
            rig.Floor(-under);
            rig.Water(Vector3.zero, new Vector3(100f, under + 1f, 100f));
            rig.Teleport(new Vector3(0f, -under + 0.02f, 0f));
        }

        private IEnumerator MeanSpeed(System.Func<int, StepInputs> inputs, System.Action<float> result)
        {
            yield return rig.Run(0.5f, inputs);
            yield return rig.MeanSpeed(0.5f, inputs, result);
        }

        [UnityTest]
        public IEnumerator WalkingThroughShallowWater_IsSlowedByHowDeepItIs([Values(0.25f, 0.5f, 0.9f)] float share)
        {
            float under = 0f;
            yield return StandInWater(() => under = Mathf.Lerp(StartDepth, rig.SwimDepth, share));
            float speed = 0f;
            yield return MeanSpeed(_ => CharacterTestRig.Move(Vector3.forward), s => speed = s);
            Assert.That(speed, Is.EqualTo(rig.WalkSpeed * ExpectedShare(under)).Within(0.05f), "Wading should slow the walk by how deep the water is.");
            Assert.That(speed, Is.LessThan(rig.WalkSpeed - 0.1f));
            Assert.That(rig.InState(CharacterState.Wading), Is.True);
        }

        [UnityTest]
        public IEnumerator SprintingThroughShallowWater_IsSlowedToo()
        {
            float under = 0f;
            yield return StandInWater(() => under = Mathf.Lerp(StartDepth, rig.SwimDepth, 0.5f));
            float speed = 0f;
            yield return MeanSpeed(step => CharacterTestRig.Sprint(Vector3.forward), s => speed = s);
            Assert.That(speed, Is.EqualTo(rig.RunSpeed * ExpectedShare(under)).Within(0.05f), "Wading should slow a sprint as it slows a walk.");
        }

        [UnityTest]
        public IEnumerator AnkleDeepWater_DoesntSlowTheCharacter()
        {
            yield return StandInWater(() => StartDepth * 0.5f);
            float speed = 0f;
            yield return MeanSpeed(_ => CharacterTestRig.Move(Vector3.forward), s => speed = s);
            Assert.That(speed, Is.EqualTo(rig.WalkSpeed).Within(0.05f), "Water shallower than the wade start depth shouldn't slow the walk.");
            Assert.That(rig.InState(CharacterState.Wading), Is.False);
        }

        [UnityTest]
        public IEnumerator WaterDeeperThanTheWadeMaxDepth_DoesntSlowTheCharacter()
        {
            float under = 0f;
            yield return StandInWater(() => under = Mathf.Lerp(StartDepth, rig.SwimDepth, 0.75f));
            // The max halfway between where wading starts and how deep this water is.
            rig.SetMovementSetting("wadeMaxDepth", (StartDepth + under) / 2f / rig.StandingHeight);
            float speed = 0f;
            yield return MeanSpeed(_ => CharacterTestRig.Move(Vector3.forward), s => speed = s);
            Assert.That(speed, Is.EqualTo(rig.WalkSpeed).Within(0.05f), "Water deeper than Wade Max Depth shouldn't slow the walk.");
            Assert.That(rig.InState(CharacterState.Wading), Is.False);
        }

        [UnityTest]
        public IEnumerator WalkingIntoWaterAndOut_RaisesTheWadingEvents()
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero);
            // Water ahead, from 3 m on, half as deep as the character would swim in.
            float under = Mathf.Lerp(StartDepth, rig.SwimDepth, 0.5f);
            rig.Water(new Vector3(0f, under, 13f), new Vector3(20f, under + 1f, 20f));
            rig.ClearEvents();
            yield return rig.RunUntil(() => rig.Position.z > 6f, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: 5f);
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.WadeStarted));
            Assert.That(rig.EventsRaised, Has.No.Member(MovementEvents.WadeEnded));
            yield return rig.RunUntil(() => rig.Position.z < 0f, _ => CharacterTestRig.Move(Vector3.back), timeoutSeconds: 5f);
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.WadeEnded));
        }

        // Walking too, as on dry land (see VaultTests): the water's too slow to sprint in.
        [UnityTest]
        public IEnumerator WalkingOrSprintingIntoALowWallWhileWading_VaultsIt([Values(false, true)] bool sprint)
        {
            yield return StandInWater(() => Mathf.Lerp(StartDepth, rig.SwimDepth, 0.5f));
            // A low wall ahead, between the step height and where a ledge would be grabbed, with the floor beyond.
            float floor = rig.Position.y;
            float height = Mathf.Lerp(rig.StepHeight, rig.MovementSetting<float>("ledgeGrabMinHeight") * rig.StandingHeight, 0.5f);
            rig.Box("Low Wall", new Vector3(0f, floor + height / 2f, 4f), new Vector3(10f, height, 0.3f));
            Physics.SyncTransforms();
            bool vaulted = false;
            yield return rig.RunUntil(() => rig.Position.z > 5f,
                step => sprint ? CharacterTestRig.Sprint(Vector3.forward) : CharacterTestRig.Move(Vector3.forward),
                _ => vaulted |= rig.InState(CharacterState.Vaulting), timeoutSeconds: 6f);
            Assert.That(vaulted, Is.True, "Heading into a low wall while wading, the character should vault it.");
            Assert.That(rig.Position.z, Is.GreaterThan(5f), "The character should be past the wall.");
        }
    }
}

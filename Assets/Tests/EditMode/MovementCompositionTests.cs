using System.Collections.Generic;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.Tests.EditMode
{
    /// <summary>
    /// Tests of movement modes made of components (ComposedMovementMode) as assets: a new BasicMovementMode starts with
    /// its default components, one of each, in order; the movement mode asset has no missing or doubled components; a
    /// mode's JSON holds its components' settings (which the character's capabilities rely on to know when to measure
    /// again); and adding, removing and finding components.
    /// </summary>
    public class MovementCompositionTests
    {
        private const string ModeAssetPath = "Assets/ScriptableObjects/BasicMovementMode.asset";
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) { Object.DestroyImmediate(obj); }
            created.Clear();
        }

        private BasicMovementMode NewMode()
        {
            var mode = ScriptableObject.CreateInstance<BasicMovementMode>();
            created.Add(mode);
            return mode;
        }

        [Test]
        public void ANewBasicMovementMode_StartsWithItsComponents_InOrder()
        {
            BasicMovementMode mode = NewMode();
            Assert.That(mode.Components.Select(c => c.GetType()),
                Is.EqualTo(new[]
                {
                    typeof(BouncePads), typeof(Ziplines), typeof(RopeSwinging), typeof(Ledges), typeof(Vaulting),
                    typeof(PushingAndPulling), typeof(WallClimbing), typeof(WallRunning), typeof(WallSliding), typeof(Rolling),
                    typeof(Crouching), typeof(FallAndAirHandling), typeof(WalkingAndRunning), typeof(Dashing), typeof(Jumping),
                }),
                "Most committed first: a bounce pad launches whatever the character is doing; what it hangs from holds it " +
                "over everything below; a wall's top is grabbed out of a slide down it; a climbable wall is climbed rather " +
                "than run along or slid down; a roll or a slide on the ground; crouching, which only changes how it walks; the ground's friction before the walking that makes up the " +
                "difference to the momentum it leaves; then a dash and the jump that cancels it.");
            Assert.That(mode.Components.All(c => c.Enabled), Is.True);
        }

        [Test]
        public void AComponentsInspectorName_IsItsOwnName_OrItsClassNameSpacedOut()
        {
            Assert.That(MovementComponent.DisplayNameOf(typeof(FallAndAirHandling)), Is.EqualTo("Fall & Air Handling"));
            Assert.That(MovementComponent.DisplayNameOf(typeof(WalkingAndRunning)), Is.EqualTo("Walking & Running"));
            Assert.That(MovementComponent.DisplayNameOf(typeof(PushingAndPulling)), Is.EqualTo("Pushing And Pulling"));
        }

        [Test]
        public void TheMovementModeAsset_HasNoMissingOrDoubledComponents()
        {
            var mode = AssetDatabase.LoadAssetAtPath<ComposedMovementMode>(ModeAssetPath);
            Assert.That(mode, Is.Not.Null, $"There's no movement mode made of components at {ModeAssetPath}.");
            Assert.That(mode.Components, Has.None.Null, "A component's type is missing (renamed or deleted?).");
            Assert.That(mode.Components.Select(c => c.GetType()), Is.Unique, "A mode should have one of each component.");
            Assert.That(mode.Components.Count, Is.GreaterThan(0), "The asset has no components.");
        }

        [Test]
        public void AModesJson_HoldsItsComponentsSettings()
        {
            BasicMovementMode mode = NewMode();
            string before = JsonUtility.ToJson(mode);
            mode.Get<Ziplines>().ZiplineGrabReach += 1f;
            Assert.That(JsonUtility.ToJson(mode), Is.Not.EqualTo(before),
                "MovementController.Capabilities knows to measure again from the mode's JSON, so it must change with a component's settings.");
            mode.Get<Ziplines>().Enabled = false;
            Assert.That(JsonUtility.ToJson(mode), Does.Contain("ziplineGrabReach"));
        }

        [Test]
        public void Components_AreAdded_Removed_AndFound()
        {
            BasicMovementMode mode = NewMode();
            Ziplines ziplines = mode.Get<Ziplines>();
            Assert.That(mode.Add<Ziplines>(), Is.SameAs(ziplines), "Adding a component the mode has gives back the one it has.");
            ziplines.Enabled = false;
            Assert.That(mode.Get<Ziplines>(), Is.Null, "A component turned off isn't found...");
            Assert.That(mode.Get<Ziplines>(includeDisabled: true), Is.SameAs(ziplines), "...unless asked for.");
            Assert.That(mode.Remove<Ziplines>(), Is.True);
            Assert.That(mode.Get<Ziplines>(includeDisabled: true), Is.Null);
            Assert.That(mode.Remove<Ziplines>(), Is.False);
            Ziplines added = mode.Add<Ziplines>();
            Assert.That(mode.Components.Last(), Is.SameAs(added), "A component is added at the end of the list.");
            Assert.That(added.Mode, Is.SameAs(mode));
        }
    }
}

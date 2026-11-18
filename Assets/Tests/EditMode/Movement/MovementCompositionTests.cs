using System.Collections.Generic;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.Tests.EditMode.Movement
{
    /// <summary>
    /// Tests of movement modes made of components (ComposedMovementMode) as assets: a new BasicMovementMode starts with
    /// its default components, one of each, in order; the movement mode asset has no missing or doubled components; a
    /// mode's JSON holds its components' settings (which the character's capabilities rely on to know when to measure
    /// again); each ability's state is declared once, with its events; and adding, removing and finding components.
    /// </summary>
    [Category(TestArea.Movement)]
    [TestTiming]
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
                    typeof(PushingAndPulling), typeof(WallClimbing), typeof(Swimming), typeof(WallRunning), typeof(WallSliding), typeof(Rolling),
                    typeof(Crouching), typeof(Wading), typeof(FallAndAirHandling), typeof(WalkingAndRunning), typeof(Dashing), typeof(Jumping),
                }),
                "A new BasicMovementMode's components differ from this list. If you added a component to " +
                "BasicMovementMode.DefaultComponents, add its type here too, at the same place: the order is a deliberate " +
                "choice, checked here (see docs/adding-an-ability.md, Putting it in the mode). The order: " +
                "most committed first: a bounce pad launches whatever the character is doing; what it hangs from holds it " +
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

        /// <summary>
        /// Each ability's state's starts and ends are declared once, by the component that sets it (StateEvents), with
        /// an event for each: a state declared by two components would raise its events twice over, and one with no
        /// events would come and go unannounced. Checked on the movement mode asset (the character's) and on a new
        /// BasicMovementMode's default components.
        /// </summary>
        [Test]
        public void EachAbilityState_IsDeclaredOnce_WithItsEvents()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ComposedMovementMode>(ModeAssetPath);
            Assert.That(asset, Is.Not.Null, $"There's no movement mode made of components at {ModeAssetPath}.");
            foreach (ComposedMovementMode mode in new ComposedMovementMode[] { asset, NewMode() })
            {
                var declaredBy = new Dictionary<CharacterState, string>();
                foreach (MovementComponent component in mode.Components.Where(c => c != null))
                {
                    foreach (StateEvent pair in component.StateEvents)
                    {
                        string name = component.GetType().Name;
                        Assert.That(declaredBy.ContainsKey(pair.state), Is.False,
                            $"{pair.state} is declared by both {(declaredBy.TryGetValue(pair.state, out string other) ? other : "")} and {name}.");
                        declaredBy[pair.state] = name;
                        Assert.That(pair.started, Is.Not.EqualTo(MovementEvents.None), $"{name}'s {pair.state} should raise an event as it starts.");
                        Assert.That(pair.ended, Is.Not.EqualTo(MovementEvents.None), $"{name}'s {pair.state} should raise an event as it ends.");
                        Assert.That(pair.started, Is.Not.EqualTo(pair.ended), $"{name}'s {pair.state} should start and end with different events.");
                    }
                }
                Assert.That(declaredBy, Is.Not.Empty, "Test setup: the abilities should declare their states' events.");
            }
        }

        /// <summary>
        /// Every movement mode asset in the project (the character's, the fox's) has every one of its code's default
        /// components, turned on or off: a component added to the code's list but not to an asset would be missing from
        /// it silently, and no tuning preset could give it back.
        /// </summary>
        [Test]
        public void EveryMovementModeAsset_HasEveryDefaultComponent()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(ComposedMovementMode));
            Assert.That(guids, Is.Not.Empty, "Test setup: the project should have movement mode assets.");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mode = AssetDatabase.LoadAssetAtPath<ComposedMovementMode>(path);
                List<System.Type> missing = mode.MissingDefaultComponentTypes();
                Assert.That(missing, Is.Empty,
                    $"{path} doesn't have {string.Join(", ", missing.Select(MovementComponent.DisplayNameOf))}. Select it and " +
                    "press Add Missing Components in its inspector (turned off for a character they don't suit yet), or run " +
                    "-executeMethod CharacterShowcase.EditorTools.MovementModeSync.AddToAllModesFromCommandLine.");
            }
        }

        [Test]
        public void MissingDefaultComponents_AreAddedAtTheirDefaultPlaces_LeavingTheRestAsTheyAre()
        {
            // Read from the code's own order, so a new ability (New Ability adds one to it) doesn't change what's tested:
            // the first default, a middle one and the last are taken out, and the one above the last moved to the top, as
            // a mode whose order was changed by hand.
            BasicMovementMode mode = NewMode();
            List<System.Type> defaults = mode.DefaultComponentTypes().ToList();
            System.Type first = defaults[0], middle = typeof(Ledges), last = defaults[defaults.Count - 1], moved = defaults[defaults.Count - 2];
            foreach (System.Type type in new[] { first, middle, last }) { mode.Remove(type); }
            MovementComponent movedComponent = mode.Get(moved, includeDisabled: true);
            mode.Remove(moved);
            mode.Insert(0, movedComponent);
            WallRunning wallRunning = mode.Get<WallRunning>();
            wallRunning.Enabled = false;
            List<System.Type> before = mode.Components.Select(c => c.GetType()).ToList();

            var expected = new[] { first, middle, last }.OrderBy(defaults.IndexOf).ToArray();
            Assert.That(mode.MissingDefaultComponentTypes(), Is.EqualTo(expected));
            List<System.Type> added = mode.AddMissingDefaultComponents(enabled: false);

            Assert.That(added, Is.EqualTo(expected), "What it added, in the default order.");
            Assert.That(mode.MissingDefaultComponentTypes(), Is.Empty);
            List<System.Type> after = mode.Components.Select(c => c.GetType()).ToList();
            Assert.That(after.Where(t => !added.Contains(t)), Is.EqualTo(before), "The components the mode had keep their order.");
            foreach (System.Type type in added)
            {
                // Each goes just after the default above it (added before it, if it was missing too), wherever that was
                // moved; one with none above (the first) goes just before the one below it.
                int at = defaults.IndexOf(type);
                if (at > 0) { Assert.That(after.IndexOf(type), Is.EqualTo(after.IndexOf(defaults[at - 1]) + 1), $"{type.Name} should follow {defaults[at - 1].Name}."); }
                else { Assert.That(after.IndexOf(type), Is.EqualTo(after.IndexOf(defaults[1]) - 1), $"{type.Name} should come just before {defaults[1].Name}."); }
            }
            Assert.That(after.IndexOf(last), Is.EqualTo(1), $"{last.Name} follows {moved.Name}, moved to the top.");
            Assert.That(added.Select(t => mode.Get(t, includeDisabled: true)), Has.All.Matches<MovementComponent>(c => !c.Enabled && c.Mode == mode));
            Assert.That(mode.Get(moved, includeDisabled: true), Is.SameAs(movedComponent), "Components the mode had are kept as they were.");
            Assert.That(wallRunning.Enabled, Is.False);

            Assert.That(mode.AddMissingDefaultComponents(enabled: true), Is.Empty, "With none missing, nothing is added.");
            mode.Remove<Swimming>();
            mode.AddMissingDefaultComponents(enabled: true);
            Assert.That(mode.Get<Swimming>(), Is.Not.Null, "Added turned on when asked.");
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

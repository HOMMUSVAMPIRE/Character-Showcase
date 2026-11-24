using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using MeridianMotion.Showcase.TutorialRing;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.TutorialRing
{
    /// <summary>
    /// Tests of the Tutorial Ring's lessons (TutorialLessonCatalog, TutorialLessonTracker): coming into a zone makes its
    /// lesson current; its event does it, once a session; coming back to a done lesson doesn't do it again; zones are a
    /// stack (leaving the inner of two shows the outer's prompt again, and the last entered is current whatever the
    /// order); a lesson with no event is done on leaving; an event that only counts in a state needs it; the character
    /// walking into a zone and jumping does its lesson; and the catalog has the design's eighteen lessons in order.
    /// </summary>
    [Category(TestArea.Levels)]
    public class LessonTrackerTests : CharacterRigFixture
    {
        private TutorialLessonCatalog catalog;
        private TutorialLessonTracker tracker;
        private readonly List<string> completed = new();

        [SetUp]
        public void MakeTracker()
        {
            catalog = TutorialTestLessons.Catalog();
            tracker = new GameObject("Tracker").AddComponent<TutorialLessonTracker>();
            tracker.Catalog = catalog;
            completed.Clear();
            tracker.Completed += lesson => completed.Add(lesson.id);
        }

        [TearDown]
        public void RemoveTracker()
        {
            Object.Destroy(tracker.gameObject);
            Object.Destroy(catalog);
        }

        [Test]
        public void EnteringAZone_MakesItsLessonCurrent_ShowingItsPrompt()
        {
            tracker.Enter("jump");
            Assert.That(tracker.Current.id, Is.EqualTo("jump"));
            Assert.That(tracker.Showing, Is.EqualTo(TutorialLessonTracker.Shown.Prompt));
        }

        [Test]
        public void ItsEvent_DoesTheLesson_Once()
        {
            tracker.Enter("jump");
            tracker.Heard(MovementEvents.Landed, CharacterState.None);
            Assert.That(completed, Is.Empty, "Another event shouldn't do it.");
            tracker.Heard(MovementEvents.Jumped, CharacterState.None);
            tracker.Heard(MovementEvents.Jumped, CharacterState.None);
            Assert.That(completed, Is.EqualTo(new[] { "jump" }), "Its event should do it, once.");
            Assert.That(tracker.Showing, Is.EqualTo(TutorialLessonTracker.Shown.Nothing), "With no follow-up, the prompt clears.");
        }

        [Test]
        public void ComingBackToADoneLesson_ShowsItsPrompt_ButDoesntDoItAgain()
        {
            tracker.Enter("jump");
            tracker.Heard(MovementEvents.Jumped, CharacterState.None);
            tracker.Leave("jump");
            tracker.Enter("jump");
            Assert.That(tracker.Showing, Is.EqualTo(TutorialLessonTracker.Shown.Prompt));
            tracker.Heard(MovementEvents.Jumped, CharacterState.None);
            Assert.That(completed, Is.EqualTo(new[] { "jump" }), "A lesson's done once a session.");
            Assert.That(tracker.Showing, Is.EqualTo(TutorialLessonTracker.Shown.Nothing));
        }

        [Test]
        public void EnteringAnInnerZone_ThenLeavingIt_ReturnsToTheOuter()
        {
            tracker.Enter("move");
            tracker.Enter("jump");
            Assert.That(tracker.Current.id, Is.EqualTo("jump"));
            tracker.Leave("jump");
            Assert.That(tracker.Current.id, Is.EqualTo("move"), "Leaving the inner zone should go back to the outer one.");
            Assert.That(tracker.Showing, Is.EqualTo(TutorialLessonTracker.Shown.Prompt), "Its prompt should show again.");
        }

        [Test]
        public void EnteringZonesOutOfOrder_TracksTheLastEntered()
        {
            tracker.Enter("crouch");
            tracker.Enter("move");
            Assert.That(tracker.Current.id, Is.EqualTo("move"));
            tracker.Leave("crouch");
            Assert.That(tracker.Current.id, Is.EqualTo("move"), "Leaving a zone under the current one leaves the current one current.");
            Assert.That(tracker.Last.id, Is.EqualTo("move"));
        }

        [Test]
        public void ALessonWithNoEvent_IsDoneOnLeaving()
        {
            tracker.Enter("move");
            Assert.That(completed, Is.Empty);
            tracker.Leave("move");
            Assert.That(completed, Is.EqualTo(new[] { "move" }));
        }

        [Test]
        public void AnEventThatCountsInAState_NeedsIt()
        {
            tracker.Enter("sprint");
            tracker.Heard(MovementEvents.Jumped, CharacterState.Grounded);
            Assert.That(completed, Is.Empty, "A jump without running shouldn't do the sprint lesson.");
            tracker.Heard(MovementEvents.Jumped, CharacterState.Running);
            Assert.That(completed, Is.EqualTo(new[] { "sprint" }));
        }

        [Test]
        public void DoingALessonWithAFollowUp_ShowsItUntilLeaving()
        {
            tracker.Enter("crouch");
            tracker.Heard(MovementEvents.CrouchStarted, CharacterState.None);
            Assert.That(tracker.Showing, Is.EqualTo(TutorialLessonTracker.Shown.FollowUp));
            tracker.Leave("crouch");
            Assert.That(tracker.Showing, Is.EqualTo(TutorialLessonTracker.Shown.Nothing));
        }

        [UnityTest]
        public IEnumerator WalkingIntoAZone_AndJumping_DoesItsLesson()
        {
            rig.Floor();
            var zone = new GameObject("Lesson Zone").AddComponent<LessonZone>();
            rig.Track(zone.gameObject);
            zone.transform.position = new Vector3(0f, 1f, 5f);
            zone.Center = Vector3.zero;
            zone.Size = new Vector3(6f, 4f, 4f);
            zone.LessonId = "jump";
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            tracker.Follow(rig.Movement);
            yield return rig.RunUntil(() => tracker.Current != null, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: 4f);
            Assert.That(tracker.Current?.id, Is.EqualTo("jump"), "Walking into the zone should make its lesson current.");
            yield return rig.Run(CharacterTestRig.StepsFor(0.3f), step => step < 3 ? CharacterTestRig.Jump() : CharacterTestRig.Idle());
            Assert.That(completed, Is.EqualTo(new[] { "jump" }), "Jumping in the zone should do its lesson.");
        }

#if UNITY_EDITOR
        [Test]
        public void TheCatalog_HasTheDesignsEighteenLessons_InOrder()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TutorialLessonCatalog>("Assets/Showcase/TutorialRing/Tutorial Lessons.asset");
            Assert.That(asset, Is.Not.Null, "The catalog should exist (Create Tutorial Ring Assets).");
            string[] expected =
            {
                "L01-move", "L02-jump", "L03-sprint", "L04-crouch", "L05-slide", "L06-roll", "L07-vault", "L08-ledge", "L09-wallrun",
                "L10-walljump", "L11-climb", "L12-ladder", "L13-crate", "L14-bounce", "L15-rope", "L16-zipline", "L17-swim", "L18-dash",
            };
            Assert.That(asset.Lessons.ConvertAll(l => l.id), Is.EqualTo(expected));
            foreach (TutorialLesson lesson in asset.Lessons)
            {
                Assert.That(lesson.prompt, Is.Not.Empty, $"{lesson.id} should have a prompt.");
                if (!string.IsNullOrEmpty(lesson.clearsOn)) { Assert.That(lesson.ClearingEvent.IsEmpty, Is.False, $"{lesson.id}'s event '{lesson.clearsOn}' should be a MovementEvents."); }
            }
        }
#endif
    }
}

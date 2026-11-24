using System.Collections;
using System.IO;
using System.Linq;
using MeridianMotion.Characters;
using MeridianMotion.CharacterUI;
using MeridianMotion.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the replays in the pause menu (ReplayMenu, the bridge between the ReplayRecorder and the UI), on the
    /// package's UI Manager and the test rig's character: it registers as the replay controls, saving once there's a
    /// run; Load Replay playing the newest saved, and at its end the choice of continuing (the player's again, recording
    /// afresh), replaying or resetting; and the run saved before quitting when nothing's been saved, once. The replays
    /// it saves (in the player's data folder) are deleted after.
    /// </summary>
    [Category(TestArea.UI)]
    public class ReplayMenuTests : CharacterRigFixture
    {
        private const string ManagerGuid = "c7a565a23b671483b87cbdcbf55b7a3b";
        private GameObject managerObject;
        private ReplayRecorder recorder;
        private ReplayMenu menu;

        // The UI Manager, the rig's character on a floor, and the Replay Recorder recording it. (Not a UnitySetUp: that
        // runs before the rig's made, in the base fixture's SetUp.)
        private IEnumerator Ready()
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(ManagerGuid));
#endif
            if (prefab == null) { Assert.Ignore("There's no UI Manager prefab (the package's UI content) here."); }
            managerObject = Object.Instantiate(prefab);
            ReplayRecorder.ForgetSessionSaves();
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            var holder = new GameObject("Replay Recorder");
            holder.SetActive(false);
            recorder = holder.AddComponent<ReplayRecorder>();
            menu = holder.AddComponent<ReplayMenu>();
            menu.SavesOnQuit = ReplayMenu.SaveOnQuit.Always;
            holder.SetActive(true);
            recorder.Attach(rig.Controller);
        }

        [UnityTearDown]
        public IEnumerator TearDownMenu()
        {
            if (ReplayRecorder.LastSavedPath != null && File.Exists(ReplayRecorder.LastSavedPath)) { File.Delete(ReplayRecorder.LastSavedPath); }
            foreach (string path in SavedHere()) { File.Delete(path); }
            ReplayRecorder.ForgetSessionSaves();
            if (recorder != null) { Object.Destroy(recorder.gameObject); }
            if (managerObject != null) { Object.Destroy(managerObject); }
            ReplayClock.Remove();
            Time.timeScale = 1f;
            yield return null;
        }

        // This scene's replays in the player's data folder.
        private string[] SavedHere() =>
            Directory.Exists(Replay.Folder) && recorder != null ? Directory.GetFiles(Replay.Folder, $"{recorder.SceneName} *{Replay.FileExtension}") : new string[0];

        private IEnumerator Walk(int steps) => rig.Run(steps, _ => CharacterTestRig.Move(Vector3.forward));

        [UnityTest]
        public IEnumerator ItRegistersAsTheReplayControls_AndCanSaveOnceThereIsARun()
        {
            yield return Ready();
            Assert.That(ReplayControls.Current, Is.SameAs(menu), "Enabled, it's the UI's replay controls (so the pause menu has its tab).");
            Assert.That(menu.CanSave, Is.False, "Nothing's recorded before the first step.");
            yield return Walk(30);
            Assert.That(menu.CanSave, Is.True);
            StringAssert.StartsWith("Recording", menu.Status);
            menu.SaveReplay();
            Assert.That(File.Exists(ReplayRecorder.LastSavedPath), Is.True, "Save Replay writes the run to a file.");
            Assert.That(menu.CanLoad, Is.True, "...which Load Replay can play.");
            recorder.gameObject.SetActive(false);
            Assert.That(ReplayControls.Current, Is.Null, "Disabled, it's taken off (and the tab goes).");
        }

        [UnityTest]
        public IEnumerator LoadReplay_PlaysTheNewestSaved_AndAtItsEndOffersTheChoices()
        {
            yield return Ready();
            Vector3 start = rig.Position;
            yield return Walk(40);
            Vector3 end = rig.Position;
            menu.SaveReplay();
            yield return Walk(20);

            menu.LoadReplay();
            Assert.That(recorder.IsPlaying, Is.True, "Load Replay plays the newest replay saved for the scene.");
            Assert.That(Vector3.Distance(rig.Position, start), Is.LessThan(0.001f), "...from where its run began.");
            yield return rig.RunWithSource(41, recorder.PlaybackSource);
            Assert.That(Vector3.Distance(rig.Position, end), Is.LessThan(0.01f), "Played to its end, it's where the run ended.");
            yield return null;
            UIChoiceDialog dialog = menu.EndDialog;
            Assert.That(dialog, Is.Not.Null, "At its end, a choice.");
            Assert.That(dialog.Buttons.Select(b => b.GetComponentInChildren<TMP_Text>().text),
                Is.EqualTo(new[] { "CONTINUE PLAYING", "REPLAY AGAIN", "RESET LEVEL" }));

            dialog.Choose(0);
            Assert.That(recorder.IsPlaying, Is.False, "Continue: the replay stops...");
            Assert.That(rig.Controller.InputSource, Is.Null, "...the character's the player's again...");
            Assert.That(recorder.IsRecording, Is.True, "...and a new recording starts from there.");
        }

        [UnityTest]
        public IEnumerator ReplayAgain_PlaysItFromItsStartOnceMore()
        {
            yield return Ready();
            Vector3 start = rig.Position;
            yield return Walk(30);
            menu.SaveReplay();
            menu.LoadReplay();
            yield return rig.RunWithSource(31, recorder.PlaybackSource);
            yield return null;
            menu.EndDialog.Choose(1);
            Assert.That(recorder.IsPlaying, Is.True);
            Assert.That(recorder.PlaybackComplete, Is.False);
            Assert.That(Vector3.Distance(rig.Position, start), Is.LessThan(0.001f), "Replay again: back where it began.");
        }

        [UnityTest]
        public IEnumerator BeforeQuitting_TheRunIsSaved_IfNothingHasBeen_Once()
        {
            yield return Ready();
            yield return Walk(30);
            menu.BeforeQuit();
            Assert.That(ReplayRecorder.SavedThisSession, Is.True, "Quitting with nothing saved saves the run.");
            Assert.That(SavedHere().Length, Is.EqualTo(1));
            yield return Walk(30);
            menu.BeforeQuit();
            Assert.That(SavedHere().Length, Is.EqualTo(1), "Once something's been saved this session, quitting saves nothing more.");

            menu.SavesOnQuit = ReplayMenu.SaveOnQuit.InBuilds;
            ReplayRecorder.ForgetSessionSaves();
            menu.BeforeQuit();
            Assert.That(SavedHere().Length, Is.EqualTo(Application.isEditor ? 1 : 2), "In Builds: not in the editor.");
        }
    }
}

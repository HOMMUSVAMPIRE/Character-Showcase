using System.Collections;
using MeridianMotion.CharacterUI;
using MeridianMotion.Characters;
using MeridianMotion.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the controls page's options (ControlOptions, ControlOptionToggle, and the Sprint option, SprintModeOption):
    /// the button hidden while the game's registered no option of its name, shown with the option's name and state once
    /// it has, and pressing it flipping the option; and the Sprint option setting the input reader's Toggle Sprint,
    /// keeping the choice, and putting it back as it starts.
    /// </summary>
    [Category(TestArea.UI)]
    public class ControlOptionTests : UITestsBase
    {
        private const string SavedKey = "MeridianMotion.Tests.ControlOptionTests";

        private sealed class Option : IControlOption
        {
            public string Name => "Sprint";
            public string OffText => "Hold";
            public string OnText => "Toggle";
            public bool Value { get; set; }
        }

        [TearDown]
        public void ForgetTheSavedChoice() => PlayerPrefs.DeleteKey(SavedKey);

        private ControlOptionToggle Toggle()
        {
            var holder = Track(new GameObject("Sprint Option", typeof(RectTransform)));
            holder.transform.SetParent(manager.defaultCanvas, false);
            var buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(holder.transform, false);
            new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).transform.SetParent(buttonObject.transform, false);
            var toggle = holder.AddComponent<ControlOptionToggle>();
            toggle.Button = buttonObject.GetComponent<Button>();
            return toggle;
        }

        [UnityTest]
        public IEnumerator TheButton_ShowsAndFlipsTheOption_AndHidesWithoutIt()
        {
            ControlOptionToggle toggle = Toggle();
            yield return null;
            Assert.That(toggle.Button.gameObject.activeSelf, Is.False, "With no Sprint option registered, its button should be hidden.");

            var option = new Option();
            ControlOptions.Register(option);
            try
            {
                Assert.That(toggle.Button.gameObject.activeSelf, Is.True);
                Assert.That(toggle.Text, Is.EqualTo("SPRINT: HOLD"));
                toggle.Button.onClick.AddListener(toggle.Flip);
                toggle.Button.onClick.Invoke();
                Assert.That(option.Value, Is.True, "Pressing it should flip the option.");
                Assert.That(toggle.Text, Is.EqualTo("SPRINT: TOGGLE"));
            }
            finally
            {
                ControlOptions.Unregister(option);
            }
            Assert.That(toggle.Button.gameObject.activeSelf, Is.False, "Unregistered, its button should hide again.");
        }

        [UnityTest]
        public IEnumerator TheSprintOption_SetsTheReadersToggleSprint_KeepsIt_AndPutsItBack()
        {
            // The reader turned off: only its setting's wanted, not its reading a device.
            var readerObject = Track(new GameObject("Input Manager"));
            readerObject.SetActive(false);
            var reader = readerObject.AddComponent<PlayerInputReader>();
            reader.enabled = false;
            var sprint = readerObject.AddComponent<SprintModeOption>();
            sprint.Reader = reader;
            sprint.Key = SavedKey;
            readerObject.SetActive(true);
            yield return null;
            Assert.That(ControlOptions.Find("Sprint"), Is.SameAs(sprint), "It should register with the UI.");
            Assert.That(reader.ToggleSprint, Is.False, "With no choice kept, the reader's own setting stands.");

            ControlOptions.Set(sprint, true);
            Assert.That(reader.ToggleSprint, Is.True);
            Assert.That(PlayerPrefs.GetInt(SavedKey, 0), Is.EqualTo(1), "The choice should be kept.");

            readerObject.SetActive(false);
            Assert.That(ControlOptions.Find("Sprint"), Is.Null, "Turned off, it should unregister.");
            reader.ToggleSprint = false;
            readerObject.SetActive(true);
            Assert.That(reader.ToggleSprint, Is.True, "Starting again, it should put the kept choice back.");
        }
    }
}

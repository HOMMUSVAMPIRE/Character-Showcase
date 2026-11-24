using MeridianMotion.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the widgets drawn in the theme (ThemedButton, ThemedText): a button's kind sets its colours by role, its
    /// state tints are the theme's, it's hatched only while it can't be used; text takes the theme's fonts and colours, and
    /// changes with it.
    /// </summary>
    [Category(TestArea.UI)]
    public class ThemedWidgetTests : UITestsBase
    {
        private UITheme theme;

        [SetUp]
        public void MakeTheme() => theme = Track(ScriptableObject.CreateInstance<UITheme>());

        private ThemedButton Button()
        {
            var root = Track(new GameObject("Button", typeof(RectTransform)));
            root.transform.SetParent(manager.defaultCanvas, false);
            var shape = root.AddComponent<ChamferedBox>();
            shape.Theme = theme;
            var hatch = new GameObject("Hatch", typeof(RectTransform)).AddComponent<StripeFill>();
            hatch.transform.SetParent(root.transform, false);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).AddComponent<ThemedText>();
            label.transform.SetParent(root.transform, false);
            label.Theme = theme;
            var button = root.AddComponent<ThemedButton>();
            button.Shape = shape;
            button.Hatch = hatch;
            button.Label = label;
            return button;
        }

        [Test]
        public void AButtonsKind_SetsItsShapesAndLabelsRoles()
        {
            ThemedButton button = Button();
            foreach (ThemedButton.Kind kind in System.Enum.GetValues(typeof(ThemedButton.Kind)))
            {
                button.ButtonKind = kind;
                Assert.That(button.Shape.FillRole, Is.EqualTo(ThemedButton.FillRoleOf(kind)), kind.ToString());
                Assert.That(button.Shape.Outline, Is.EqualTo(kind == ThemedButton.Kind.Outline), kind.ToString());
                Assert.That(button.Label.Role, Is.EqualTo(kind == ThemedButton.Kind.Outline ? UIColorRole.Text : UIColorRole.TextOnFill), kind.ToString());
            }
            Assert.That(button.targetGraphic, Is.SameAs(button.Shape), "Its shape should be what its states tint.");
        }

        [Test]
        public void AButtonsStateTints_AreTheThemes()
        {
            ThemedButton button = Button();
            Assert.That(button.colors.highlightedColor, Is.EqualTo(theme.HoveredTint));
            Assert.That(button.colors.pressedColor, Is.EqualTo(theme.PressedTint));
            Assert.That(button.colors.selectedColor, Is.EqualTo(theme.SelectedTint));
            Assert.That(button.colors.disabledColor, Is.EqualTo(theme.DisabledTint));
        }

        [Test]
        public void AButton_IsHatchedOnlyWhileItCantBeUsed()
        {
            ThemedButton button = Button();
            Assert.That(button.Hatch.gameObject.activeSelf, Is.False, "Usable, it shouldn't be hatched.");
            button.interactable = false;
            Assert.That(button.Hatch.gameObject.activeSelf, Is.True, "Not usable, it should be hatched.");
            button.interactable = true;
            Assert.That(button.Hatch.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ThemedText_TakesTheThemesFontAndColour_AndFollowsAChange()
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null) { Assert.Ignore("TextMeshPro has no default font here."); }
            theme.DisplayFont = font;
            var text = Track(new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI))).AddComponent<ThemedText>();
            text.Theme = theme;
            text.Display = true;
            text.Role = UIColorRole.Accent;
            var label = text.GetComponent<TMP_Text>();
            Assert.That(label.font, Is.SameAs(font));
            Assert.That(label.color, Is.EqualTo(theme.Color(UIColorRole.Accent)));

            theme.SetColor(UIColorRole.Accent, Color.green);
            Assert.That(label.color, Is.EqualTo(Color.green), "A change to the theme should reach the text at once.");
        }
    }
}

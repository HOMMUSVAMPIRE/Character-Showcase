using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MeridianMotion.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the HUD widgets (ReferenceAssets/OpenTasks.md, H6), each driven as the game would: a radial progress's hold
    /// on the UI's clock; an ability tile's states to its look, and the bar reusing its tiles; the interact prompt's label,
    /// hold and binding (following the device in use); a world marker clamped to the screen's edge with its arrow pointing
    /// the way; the minimap placing a world point and holding a far one at its rim; the compass following a heading; the
    /// reticle's lock-on; and the dialogue panel writing out its lines and going through them on Submit.
    /// </summary>
    [Category(TestArea.UI)]
    public class HUDWidgetTests : UITestsBase
    {
        private readonly List<InputDevice> devices = new();

        [TearDown]
        public void RemoveDevices()
        {
            foreach (InputDevice device in devices) { if (device.added) { InputSystem.RemoveDevice(device); } }
            ControlSchemeTracker.Forget();
        }

        private static void Set(object target, string name, object value)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field == null) { continue; }
                field.SetValue(target, value);
                return;
            }
            Assert.Fail($"{target.GetType().Name} has no field '{name}'.");
        }

        private T Child<T>(Transform parent, string name) where T : Component
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<T>();
        }

        private GameObject Widget(string name, Vector2 size)
        {
            var obj = Track(new GameObject(name, typeof(RectTransform)));
            obj.transform.SetParent(manager.defaultCanvas, false);
            ((RectTransform)obj.transform).sizeDelta = size;
            return obj;
        }

        private UIRadialProgress Progress(Transform parent)
        {
            var ring = Child<RingShape>(parent, "Ring");
            var progress = ring.gameObject.AddComponent<UIRadialProgress>();
            progress.Ring = ring;
            return progress;
        }

        [Test]
        public void ARadialProgressHold_FillsOnTheUIClock_AndCompletesOnce()
        {
            UIRadialProgress progress = Progress(Widget("Hold", new Vector2(64f, 64f)).transform);
            int completed = 0;
            progress.Completed += () => completed++;
            progress.Hold(2f);
            Now = 1f;
            progress.Tick();
            Assert.That(progress.Fraction, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(progress.Ring.Fraction, Is.EqualTo(0.5f).Within(0.001f), "Its ring should show it.");
            Now = 3f;
            progress.Tick();
            progress.Tick();
            Assert.That((progress.Fraction, completed, progress.Holding), Is.EqualTo((1f, 1, false)), "Full, it completes once.");
            progress.Drain = true;
            progress.Fraction = 0.25f;
            Assert.That(progress.Ring.Fraction, Is.EqualTo(0.75f).Within(0.001f), "Draining, the ring shows what's left.");
            progress.Release();
            Assert.That(progress.Fraction, Is.Zero);
        }

        private AbilityTile Tile(Transform parent)
        {
            var root = new GameObject("Tile", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var tile = root.AddComponent<AbilityTile>();
            tile.Frame = root.AddComponent<ChamferedBox>();
            tile.Icon = Child<UIIcon>(root.transform, "Icon");
            tile.Cooldown = Progress(root.transform);
            return tile;
        }

        [Test]
        public void AnAbilityTilesState_SetsItsLook()
        {
            AbilityTile tile = Tile(Widget("Bar", new Vector2(72f, 72f)).transform);
            tile.SetState(AbilityTile.State.Ready);
            Assert.That((tile.Frame.FillRole, tile.Frame.Outline, tile.Icon.FillRole), Is.EqualTo((UIColorRole.Panel, true, UIColorRole.Text)));
            Assert.That(tile.Cooldown.gameObject.activeSelf, Is.False, "Ready, no cooldown shows.");
            tile.SetState(AbilityTile.State.Active);
            Assert.That((tile.Frame.FillRole, tile.Frame.Outline, tile.Icon.FillRole), Is.EqualTo((UIColorRole.Accent, false, UIColorRole.TextOnFill)));
            tile.SetState(AbilityTile.State.CoolingDown, 0.3f);
            Assert.That(tile.Cooldown.gameObject.activeSelf, Is.True);
            Assert.That(tile.Cooldown.Ring.Fraction, Is.EqualTo(0.3f).Within(0.001f), "Its ring shows the cooldown left.");
            Assert.That(tile.Icon.FillRole, Is.EqualTo(UIColorRole.TextMuted));
            tile.SetState(AbilityTile.State.Unavailable);
            Assert.That((tile.Frame.OutlineRole, tile.Icon.FillRole), Is.EqualTo((UIColorRole.Disabled, UIColorRole.Disabled)));
        }

        [Test]
        public void AnAbilityBar_ShowsATilePerAbility_ReusingThem()
        {
            GameObject templateHolder = Track(new GameObject("Templates"));
            templateHolder.SetActive(false);
            AbilityTile template = Tile(templateHolder.transform);
            var bar = Widget("Bar", new Vector2(400f, 72f)).AddComponent<AbilityBar>();
            bar.TilePrefab = template;
            bar.SetAbilities(new[] { ("a", UIIconLibrary.Dash), ("b", UIIconLibrary.Roll), ("c", UIIconLibrary.Grab) });
            AbilityTile[] first = bar.Tiles.ToArray();
            Assert.That(first.Select(t => t.IconName), Is.EqualTo(new[] { UIIconLibrary.Dash, UIIconLibrary.Roll, UIIconLibrary.Grab }));
            bar.SetAbilities(new[] { ("b", UIIconLibrary.Roll), ("d", UIIconLibrary.Sprint) });
            Assert.That(bar.Tiles.Count(), Is.EqualTo(2));
            Assert.That(bar.GetComponentsInChildren<AbilityTile>(true).Length, Is.EqualTo(3), "Tiles are reused and hidden, not destroyed.");
            bar.SetState("d", AbilityTile.State.Active);
            Assert.That(bar.Tile("d").CurrentState, Is.EqualTo(AbilityTile.State.Active));
            Assert.That(bar.Tile("a"), Is.Null);
        }

        [Test]
        public void TheInteractPrompt_ShowsItsLabelAndHold_AndTheBindingOfTheDeviceInUse()
        {
            var actions = Track(ScriptableObject.CreateInstance<InputActionAsset>());
            actions.AddControlScheme("Keyboard&Mouse").WithRequiredDevice("<Keyboard>");
            actions.AddControlScheme("Gamepad").WithRequiredDevice("<Gamepad>");
            InputAction interact = actions.AddActionMap("Player").AddAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e", groups: "Keyboard&Mouse");
            interact.AddBinding("<Gamepad>/buttonWest", groups: "Gamepad");
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            devices.Add(keyboard);
            devices.Add(gamepad);
            ControlSchemeTracker.NoteDeviceUsed(keyboard);

            InteractPrompt template = Template<InteractPrompt>("Prompt", new Vector2(400f, 80f));
            Transform contents = new GameObject("Contents", typeof(RectTransform)) { transform = { parent = template.transform } }.transform;
            Set(template, "contents", contents.gameObject);
            Set(template, "label", Child<TextMeshProUGUI>(contents, "Label"));
            Set(template, "bindingText", Child<TextMeshProUGUI>(contents, "Binding"));
            Set(template, "icon", Child<UIIcon>(contents, "Icon"));
            Set(template, "holdRing", Progress(contents));
            InteractPrompt prompt = Open<InteractPrompt>(template);
            prompt.Action = interact;

            prompt.Show("Climb", UIIconLibrary.Ladder);
            Assert.That((prompt.Showing, prompt.Label, prompt.Icon.IconName), Is.EqualTo((true, "Climb", UIIconLibrary.Ladder)));
            Assert.That(prompt.Binding, Is.EqualTo(interact.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"))));
            prompt.SetHold(0.5f);
            Assert.That(prompt.HoldRing.Ring.Fraction, Is.EqualTo(0.5f).Within(0.001f));

            ControlSchemeTracker.NoteDeviceUsed(gamepad);
            Assert.That(prompt.Binding, Is.EqualTo(interact.GetBindingDisplayString(InputBinding.MaskByGroup("Gamepad"))), "On the gamepad, its button.");
            prompt.Hide();
            Assert.That(prompt.Showing, Is.False);
            Assert.That(prompt.HoldRing.Fraction, Is.Zero, "Hidden, its hold is let go.");
        }

        [Test]
        public void AMarkersEdgePoint_IsThePointOnScreen_ElseTheEdgeInItsDirection()
        {
            var area = new Rect(-100f, -50f, 200f, 100f);
            Assert.That(WorldMarker.EdgePoint(new Vector2(10f, 10f), area, false, out bool clamped, out _), Is.EqualTo(new Vector2(10f, 10f)));
            Assert.That(clamped, Is.False);
            Assert.That(WorldMarker.EdgePoint(new Vector2(300f, 0f), area, false, out clamped, out float angle), Is.EqualTo(new Vector2(100f, 0f)));
            Assert.That((clamped, angle), Is.EqualTo((true, 0f)));
            Assert.That(WorldMarker.EdgePoint(new Vector2(0f, 500f), area, false, out _, out angle), Is.EqualTo(new Vector2(0f, 50f)));
            Assert.That(angle, Is.EqualTo(90f).Within(0.001f));
            Assert.That(WorldMarker.EdgePoint(new Vector2(10f, 0f), area, true, out clamped, out angle), Is.EqualTo(new Vector2(-100f, 0f)),
                "Behind the camera, it's held at the edge away from where it projects.");
            Assert.That(clamped, Is.True);
            Assert.That(Mathf.Abs(angle), Is.EqualTo(180f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator AMarkerBehindTheCamera_IsHeldAtTheEdge_ItsArrowPointingTheWay()
        {
            var camera = Track(new GameObject("Camera")).AddComponent<Camera>();
            WorldMarker template = Template<WorldMarker>("Marker", new Vector2(80f, 80f));
            var templateArrow = (RectTransform)new GameObject("Arrow", typeof(RectTransform)) { transform = { parent = template.transform } }.transform;
            Set(template, "arrow", templateArrow);
            WorldMarker marker = Open<WorldMarker>(template, UICanvas.InWorld);
            Transform arrow = marker.transform.Find("Arrow");

            marker.Mark(new Vector3(0f, 0f, 10f), camera);
            Assert.That(marker.Clamped, Is.False, "In front, in the middle of the screen: where it shows.");
            Assert.That(marker.Distance, Is.EqualTo(10f).Within(0.001f), "Its distance should be the camera's.");
            Assert.That(arrow.gameObject.activeSelf, Is.False, "Its arrow shows only at the edge.");

            marker.Mark(new Vector3(0f, 0f, -10f), camera);
            Assert.That(marker.Clamped, Is.True, "Behind the camera, it's held at the edge.");
            Assert.That(arrow.gameObject.activeSelf, Is.True, "Its arrow shows the way.");
            Assert.That(marker.ArrowAngle, Is.EqualTo(-90f).Within(0.001f), "Straight behind: down.");

            marker.Mark(new Vector3(1000f, 0f, 10f), camera);
            Assert.That(marker.Clamped, Is.True);
            Assert.That(marker.ArrowAngle, Is.EqualTo(0f).Within(1f), "Far to the right: the arrow points right.");
            yield return null;
        }

        [Test]
        public void TheMinimap_PlacesAWorldPoint_AndHoldsAFarOneAtItsRim()
        {
            GameObject root = Widget("Minimap", new Vector2(200f, 200f));
            var markers = (RectTransform)new GameObject("Markers", typeof(RectTransform)) { transform = { parent = root.transform } }.transform;
            markers.anchorMin = Vector2.zero;
            markers.anchorMax = Vector2.one;
            markers.offsetMin = markers.offsetMax = Vector2.zero;
            var template = Child<UIIcon>(markers, "Template");
            template.gameObject.SetActive(false);
            var arrow = (RectTransform)new GameObject("Arrow", typeof(RectTransform)) { transform = { parent = root.transform } }.transform;
            var view = root.AddComponent<MinimapView>();
            Set(view, "markersParent", markers);
            Set(view, "markerPrefab", template);
            view.PlayerArrow = arrow;
            view.ViewSize = 80f;

            view.SetPlayer(new Vector3(10f, 0f, 10f), 90f);
            Assert.That(view.MapPosition(new Vector3(30f, 0f, 10f)), Is.EqualTo(new Vector2(50f, 0f)), "20 m east of the player, a quarter of the 80 m shown: 50 units right.");
            Assert.That(view.MapPosition(new Vector3(10f, 0f, 30f)), Is.EqualTo(new Vector2(0f, 50f)), "North is up.");
            Assert.That(arrow.localEulerAngles.z, Is.EqualTo(270f).Within(0.01f), "Heading east, the arrow turns a quarter clockwise.");

            UIIcon far = view.AddMarker("far", new Vector3(500f, 0f, 10f));
            Assert.That(far.gameObject.activeSelf, Is.True);
            Vector2 at = far.rectTransform.localPosition;
            Assert.That(at.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(at.x, Is.EqualTo(100f - 8f).Within(0.001f), "Far east, it's held at the rim (less Rim Inset), to the east.");
            view.RemoveMarker("far");
            Assert.That(view.Marker("far"), Is.Null);
        }

        [Test]
        public void TheCompass_FollowsAHeading()
        {
            GameObject root = Widget("Compass", new Vector2(400f, 40f));
            var points = new List<TMP_Text>();
            foreach (string point in new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" }) { points.Add(Child<TextMeshProUGUI>(root.transform, point)); }
            var compass = root.AddComponent<UICompass>();
            compass.Points.AddRange(points);
            compass.UnitsPerDegree = 3f;

            compass.Heading = 90f;
            Assert.That(points[2].rectTransform.localPosition.x, Is.EqualTo(0f).Within(0.01f), "Facing east, E is at the middle.");
            Assert.That(points[2].gameObject.activeSelf, Is.True);
            Assert.That(points[0].gameObject.activeSelf, Is.False, "N is 90 degrees off, past the strip's half width.");
            compass.Heading = 350f;
            Assert.That(points[0].rectTransform.localPosition.x, Is.EqualTo(30f).Within(0.01f), "Ten degrees west of north, N is a little right.");
            Assert.That(UICompass.HeadingOf(Vector3.right), Is.EqualTo(90f).Within(0.001f));
            Assert.That(UICompass.HeadingOf(Vector3.back), Is.EqualTo(180f).Within(0.001f));
        }

        [Test]
        public void TheReticlesLockOn_ClosesInOnTheUIClock()
        {
            var reticle = Widget("Reticle", new Vector2(48f, 48f)).AddComponent<Reticle>();
            reticle.LockOn();
            Assert.That((reticle.CurrentStyle, reticle.LockOnProgress), Is.EqualTo((Reticle.Style.LockOn, 0f)));
            Assert.That(reticle.Scale, Is.GreaterThan(1f), "It starts bigger...");
            Now = 10f;
            Assert.That((reticle.LockOnProgress, reticle.Scale), Is.EqualTo((1f, 1f)), "...and closes in to its size.");
            reticle.Unlock();
            Assert.That((reticle.Locked, reticle.CurrentStyle), Is.EqualTo((false, Reticle.Style.Crosshair)));
        }

        [UnityTest]
        public IEnumerator TheDialoguePanel_WritesOutItsLines_AndSubmitGoesThroughThem()
        {
            DialoguePanel template = Template<DialoguePanel>("Dialogue", new Vector2(600f, 200f));
            var line = Child<TextMeshProUGUI>(template.transform, "Line");
            Set(template, "speakerText", Child<TextMeshProUGUI>(template.transform, "Speaker"));
            Set(template, "lineText", line);
            Set(template, "nextArrow", new GameObject("Next") { transform = { parent = template.transform } });
            DialoguePanel panel = Open<DialoguePanel>(template);
            bool finished = false;
            panel.Play(new[] { new DialoguePanel.Line("Operator", "Take the ladder."), new DialoguePanel.Line("Operator", "Then the zipline.") }, () => finished = true);

            Assert.That(panel.VisibleCharacters, Is.Zero, "A line's written out over time...");
            Assert.That(panel.transform.Find("Next").gameObject.activeSelf, Is.False);
            Now = 0.1f;
            Assert.That(panel.VisibleCharacters, Is.EqualTo(6));
            manager.HandleSubmit();
            Assert.That(panel.LineComplete, Is.True, "...Submit shows the rest...");
            yield return null;
            Assert.That(panel.transform.Find("Next").gameObject.activeSelf, Is.True, "...and the next arrow.");
            manager.HandleSubmit();
            Assert.That(panel.Current.Text, Is.EqualTo("Then the zipline."), "Submit again goes on to the next line.");
            Now = 10f;
            manager.HandleSubmit();
            Assert.That(finished, Is.True, "After the last, it's finished...");
            yield return null;
            Assert.That(panel == null, Is.True, "...and closes.");
        }

        [UnityTest]
        public IEnumerator BackOnTheDialoguePanel_SkipsTheRest()
        {
            DialoguePanel template = Template<DialoguePanel>("Dialogue", new Vector2(600f, 200f));
            DialoguePanel panel = Open<DialoguePanel>(template);
            bool finished = false;
            panel.Play(new[] { new DialoguePanel.Line("A", "One."), new DialoguePanel.Line("A", "Two.") }, () => finished = true);
            manager.HandleBack();
            Assert.That(finished, Is.True, "Back skips the conversation, and it's finished.");
            yield return null;
        }
    }
}

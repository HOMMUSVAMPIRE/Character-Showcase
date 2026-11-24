using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the context menu (UIRightClickMenu: UI_Framework_Blueprint, Context menu, and Building it, step 7): an entry
    /// for each item shown (those whose ShowIf says no left out), each group's entries placed first, middle and last (one
    /// alone, solo), a featured item on its own prefab; clicking an entry running its item and closing the menu; the
    /// pointer leaving it, or never coming onto it, closing it; and its opening flipped to stay on the screen.
    /// </summary>
    [Category(TestArea.UI)]
    public class ContextMenuTests : UITestsBase
    {
        [SetUp]
        public void SetUpMenu()
        {
            UIRightClickMenu menu = Template<UIRightClickMenu>("Right Click Menu", new Vector2(200f, 300f));
            menu.ParentCanvas = UICanvas.Overlay;
            RightClickMenuEntry Entry(string name)
            {
                var obj = Track(new GameObject(name, typeof(RectTransform)));
                obj.SetActive(false);
                return obj.AddComponent<RightClickMenuEntry>();
            }
            var fields = typeof(UIRightClickMenu);
            void Set(string field, Object value) => fields.GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(menu, value);
            Set("entryPrefab", Entry("Entry").gameObject);
            Set("featuredEntryPrefab", Entry("Featured Entry").gameObject);
            Set("spacerEntryPrefab", Track(new GameObject("Spacer", typeof(RectTransform))));
            manager.RightClickMenuPrefab = menu.gameObject;
        }

        [UnityTest]
        public IEnumerator ItsEntries_AreTheItemsShown_PlacedInTheirGroups()
        {
            int ran = 0;
            var items = new List<MenuItemData>
            {
                new("Open", () => ran++, featured: true),
                new("Rename", () => { }),
                new("Hidden", () => { }, showIf: () => false),
                new("Copy", () => { }),
                MenuItemData.Spacer,
                new("Delete", () => { }),
            };
            UIRightClickMenu menu = UIRightClickMenu.Open(items, new Vector2(10f, Screen.height - 10f));
            Assert.That(menu.Entries.Select(e => e.Item.Text), Is.EqualTo(new[] { "Open", "Rename", "Copy", "Delete" }), "A hidden item should be left out.");
            Assert.That(menu.Entries.Select(e => e.Place), Is.EqualTo(new[]
            {
                RightClickMenuEntry.Position.First, RightClickMenuEntry.Position.Middle, RightClickMenuEntry.Position.Last, RightClickMenuEntry.Position.Solo,
            }));
            Assert.That(menu.Entries[0].name, Does.StartWith("Featured Entry"), "A featured item should use the featured prefab.");

            menu.Entries[0].OnClicked();
            Assert.That(ran, Is.EqualTo(1));
            Assert.That(menu.IsClosing, Is.True, "Clicking an entry should close the menu.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePointerLeaving_OrNeverComingOntoIt_ClosesIt()
        {
            UIRightClickMenu menu = UIRightClickMenu.Open(new List<MenuItemData> { new("Open", () => { }) }, Vector2.zero);
            menu.OnPointerEnter();
            Now += 60f;
            yield return null;
            Assert.That(menu.IsClosing, Is.False, "With the pointer on it, it should stay.");
            menu.OnPointerExit();
            Assert.That(menu.IsClosing, Is.True, "The pointer leaving it should close it.");
            yield return Frames();

            menu = UIRightClickMenu.Open(new List<MenuItemData> { new("Open", () => { }) }, Vector2.zero);
            Now += 60f;
            yield return null;
            Assert.That(menu.IsClosing, Is.True, "Never pointed at, it should close by itself.");
        }

        [UnityTest]
        public IEnumerator ItOpensDownAndRight_FlippedAtTheScreensEdges()
        {
            UIRightClickMenu menu = UIRightClickMenu.Open(new List<MenuItemData> { new("Open", () => { }) }, new Vector2(10f, Screen.height - 10f));
            Assert.That(((RectTransform)menu.transform).pivot, Is.EqualTo(new Vector2(0f, 1f)), "Room to the right and below: its top left at the pointer.");
            menu.PlaceAt(new Vector2(Screen.width - 1f, 1f));
            Assert.That(((RectTransform)menu.transform).pivot, Is.EqualTo(new Vector2(1f, 0f)), "At the bottom right, it should open up and left.");
            yield return null;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the data lists (DataDisplay, DataDisplayList and the categorized list: UI_Framework_Blueprint, Generic data
    /// lists and How a list refreshes, and Building it, step 6), on the blueprint's own example (ItemData, ItemDisplay,
    /// ItemDisplayList) filled from a fake source: a row per record, in order; search by every word; rows reused, never
    /// destroyed, from the item prefabs in turn; the selection kept across refreshes (else the first row's) and passed to
    /// the panel; the empty state; only the IDs given, or the records given; a refresh while one's under way ignored; and
    /// a header before each category.
    /// </summary>
    [Category(TestArea.UI)]
    public class DataListTests : UITestsBase
    {
        private class ItemData : IdentifiableElement
        {
            public string name;
            public int price;
        }

        private class ItemDisplay : DataDisplay<ItemData>
        {
            public string drawn;
            public override void InitializeFromID(string id) => drawn = id;
            protected override void Refresh() => drawn = $"{Data.name} ({Data.price})";
        }

        private class ItemDisplayList : DataDisplayList<ItemData, ItemDisplay>
        {
            public Dictionary<string, ItemData> source = new();
            public int fetches;
            public bool slow;

            protected override async Awaitable<Dictionary<string, ItemData>> GetData()
            {
                fetches++;
                if (slow) { await Awaitable.NextFrameAsync(); }
                return source;
            }

            protected override bool HasSearchTerm(ItemData item, string term) =>
                item.name.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0;

            public void SetUp(Transform parent, List<GameObject> prefabs, ToggleGroup group, GameObject whenEmpty)
            {
                itemsParent = parent;
                itemPrefabs = prefabs;
                toggleGroup = group;
                shownIfNoItems = new List<GameObject> { whenEmpty };
            }
        }

        private class CategorizedItemList : DataDisplayList_Categorized<ItemData, ItemDisplay>
        {
            public Dictionary<string, ItemData> source = new();
            protected override async Awaitable<Dictionary<string, ItemData>> GetData() => await Later(source);
            private static async Awaitable<Dictionary<string, ItemData>> Later(Dictionary<string, ItemData> data)
            {
                await Awaitable.NextFrameAsync();
                return data;
            }

            public void SetUp(Transform parent, GameObject prefab, GameObject header)
            {
                itemsParent = parent;
                itemPrefabs = new List<GameObject> { prefab };
                CategoryHeaderPrefab = header;
            }
        }

        private static Dictionary<string, ItemData> Items(params (string id, string name, int price, string category)[] items) =>
            items.ToDictionary(i => i.id, i => new ItemData { ID = i.id, name = i.name, price = i.price, elementType = i.category });

        private ItemDisplayList list;
        private GameObject empty;
        private GameObject rowA, rowB;

        [SetUp]
        public void SetUpList()
        {
            var obj = Track(new GameObject("Item List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ToggleGroup)));
            obj.transform.SetParent(manager.defaultCanvas, false);
            empty = Track(new GameObject("Nothing To Show"));
            rowA = Row("Row A");
            rowB = Row("Row B");
            list = obj.AddComponent<ItemDisplayList>();
            var group = obj.GetComponent<ToggleGroup>();
            group.allowSwitchOff = true;
            list.SetUp(obj.transform, new List<GameObject> { rowA, rowB }, group, empty);
            list.source = Items(("sword", "Iron Sword", 10, "Weapons"), ("bow", "Short Bow", 8, "Weapons"), ("potion", "Red Potion", 2, "Items"));
        }

        // A row template: inactive, with a toggle, as a row prefab would be.
        private GameObject Row(string name)
        {
            var obj = Track(new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle)));
            obj.SetActive(false);
            var display = obj.AddComponent<ItemDisplay>();
            display.Toggle = obj.GetComponent<Toggle>();
            display.Toggle.isOn = false;
            return obj;
        }

        private static IEnumerator Refreshed<T, U>(DataDisplayList<T, U> list) where T : Identifiable where U : DataDisplay<T>
        {
            _ = list.Refresh();
            for (int i = 0; i < 100 && list.IsUpdating; i++) { yield return null; }
            Assert.That(list.IsUpdating, Is.False, "Test setup: the refresh should finish.");
        }

        private List<ItemDisplay> Shown => list.ShownItems.ToList();

        [UnityTest]
        public IEnumerator Refresh_ShowsARowPerRecord_InOrder_FromTheItemPrefabsInTurn()
        {
            yield return Refreshed(list);
            Assert.That(Shown.Select(row => row.drawn), Is.EqualTo(new[] { "Iron Sword (10)", "Short Bow (8)", "Red Potion (2)" }));
            Assert.That(Shown.Select(row => row.name.Replace("(Clone)", "")), Is.EqualTo(new[] { "Row A", "Row B", "Row A" }), "The prefabs should be used in turn.");
            Assert.That(empty.activeSelf, Is.False, "With rows, the empty state should be hidden.");
        }

        [UnityTest]
        public IEnumerator Search_KeepsTheRecordsMatchingEveryWord()
        {
            yield return Refreshed(list);
            list.SetSearchTerms("sword iron");
            for (int i = 0; i < 100 && list.IsUpdating; i++) { yield return null; }
            Assert.That(Shown.Select(row => row.Data.ID), Is.EqualTo(new[] { "sword" }));
            list.SetSearchTerms("sword bow");
            for (int i = 0; i < 100 && list.IsUpdating; i++) { yield return null; }
            Assert.That(Shown, Is.Empty, "A record must match every word.");
            Assert.That(empty.activeSelf, Is.True, "With no rows, the empty state should show.");
        }

        [UnityTest]
        public IEnumerator Rows_AreReused_NeverDestroyed()
        {
            yield return Refreshed(list);
            List<ItemDisplay> first = Shown;
            list.source = Items(("bow", "Short Bow", 8, "Weapons"));
            yield return Refreshed(list);
            Assert.That(Shown.Count, Is.EqualTo(1));
            Assert.That(first.All(row => row != null), Is.True, "Unused rows should be hidden, not destroyed.");
            list.source = Items(("a", "A", 1, ""), ("b", "B", 2, ""), ("c", "C", 3, ""));
            yield return Refreshed(list);
            Assert.That(Shown, Is.EqualTo(first), "The same rows should be used again.");
        }

        [UnityTest]
        public IEnumerator TheSelection_IsKeptAcrossRefreshes_ElseTheFirst_AndPassedToThePanel()
        {
            var selected = new List<string>();
            list.ItemSelected.AddListener(item => selected.Add(item.ID));
            yield return Refreshed(list);
            Assert.That(list.SelectedItemID, Is.EqualTo("sword"), "With nothing selected, the first row should be.");

            Shown[2].Select(true);
            Assert.That(list.SelectedItemID, Is.EqualTo("potion"));
            Assert.That(selected.Last(), Is.EqualTo("potion"), "Selecting a row should tell the panel.");
            yield return Refreshed(list);
            Assert.That(list.SelectedItemID, Is.EqualTo("potion"), "The selection should be kept across a refresh.");
            Assert.That(Shown.Single(row => row.Selected).Data.ID, Is.EqualTo("potion"));

            list.DeselectAll();
            Assert.That(list.SelectedItemID, Is.Null);
            Assert.That(Shown.Any(row => row.Selected), Is.False);
        }

        [UnityTest]
        public IEnumerator Populate_ShowsOnlyTheIDsGiven_OrTheRecordsGiven()
        {
            list.Populate(new List<string> { "potion", "bow" });
            for (int i = 0; i < 100 && list.IsUpdating; i++) { yield return null; }
            Assert.That(Shown.Select(row => row.Data.ID), Is.EqualTo(new[] { "bow", "potion" }), "In the source's order, only those IDs.");

            list.Populate(new List<ItemData> { new() { ID = "key", name = "Old Key", price = 0 } });
            for (int i = 0; i < 100 && list.IsUpdating; i++) { yield return null; }
            Assert.That(Shown.Select(row => row.Data.ID), Is.EqualTo(new[] { "key" }), "The records given, not the source's.");
        }

        [UnityTest]
        public IEnumerator ARefreshWhileOneIsUnderWay_IsIgnored()
        {
            list.slow = true;
            _ = list.Refresh();
            Assert.That(list.IsUpdating, Is.True);
            _ = list.Refresh();
            for (int i = 0; i < 100 && list.IsUpdating; i++) { yield return null; }
            Assert.That(list.fetches, Is.EqualTo(1), "The second refresh should have been ignored.");
        }

        [UnityTest]
        public IEnumerator ACategorizedList_PutsAHeaderBeforeEachCategory()
        {
            var obj = Track(new GameObject("Categorized", typeof(RectTransform), typeof(VerticalLayoutGroup)));
            obj.transform.SetParent(manager.defaultCanvas, false);
            var header = Track(new GameObject("Header", typeof(RectTransform)));
            header.SetActive(false);
            header.AddComponent<CategoryDisplay>();
            var categorized = obj.AddComponent<CategorizedItemList>();
            categorized.SetUp(obj.transform, rowA, header);
            categorized.source = Items(("sword", "Iron Sword", 10, "Weapons"), ("bow", "Short Bow", 8, "Weapons"), ("potion", "Red Potion", 2, "Items"));
            yield return Refreshed(categorized);

            Assert.That(categorized.ShownHeaders.Select(h => h.Category), Is.EqualTo(new[] { "Weapons", "Items" }));
            string[] order = Enumerable.Range(0, obj.transform.childCount).Select(i => obj.transform.GetChild(i))
                .Where(child => child.gameObject.activeSelf)
                .Select(child => child.TryGetComponent(out CategoryDisplay h) ? "# " + h.Category : child.GetComponent<ItemDisplay>().Data.ID).ToArray();
            Assert.That(order, Is.EqualTo(new[] { "# Weapons", "sword", "bow", "# Items", "potion" }));
        }
    }
}

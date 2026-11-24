using MeridianMotion.UI;
using MeridianMotion.UI.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.Tests.EditMode.UI
{
    /// <summary>
    /// Checks of remaking a prefab in place (UIAssets.ReplaceContents, Rebuild UI Assets): its GUID and its root's file ID
    /// stay, as do the file IDs of the root's components of types it keeps; its contents are the new ones; what in it
    /// named the made object's root names the prefab's; and a Graphic can replace another on the root.
    /// </summary>
    [Category(TestArea.UI)]
    [TestTiming]
    public class RebuildInPlaceTests
    {
        private const string Folder = "Assets/ZzRebuildInPlaceTests";
        private const string PrefabPath = Folder + "/Window.prefab";

        [SetUp]
        public void MakeFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { AssetDatabase.CreateFolder("Assets", "ZzRebuildInPlaceTests"); }
        }

        [TearDown]
        public void RemoveFolder() => AssetDatabase.DeleteAsset(Folder);

        // A window: an Image on its root (or a chamfered box), a RootUIPrefab, and a child button whose click closes it.
        private static GameObject Window(string childName, bool shape)
        {
            var root = new GameObject("Window", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = shape ? new Vector2(300f, 200f) : new Vector2(100f, 100f);
            if (shape) { root.AddComponent<ChamferedBox>().FillRole = UIColorRole.Accent; }
            else { root.AddComponent<Image>(); }
            var window = root.AddComponent<RootUIPrefab>();
            window.AllowMultiple = shape;
            var child = new GameObject(childName, typeof(RectTransform), typeof(Image), typeof(Button));
            child.transform.SetParent(root.transform, false);
            UnityEventTools.AddPersistentListener(child.GetComponent<Button>().onClick, window.Close);
            return root;
        }

        private static long FileId(Object obj)
        {
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string _, out long id), Is.True);
            return id;
        }

        [Test]
        public void Rebuilding_KeepsTheGuidAndRootIds_AndTakesTheNewContents()
        {
            GameObject made = Window("Old Button", shape: false);
            PrefabUtility.SaveAsPrefabAsset(made, PrefabPath);
            Object.DestroyImmediate(made);
            var before = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            long rootId = FileId(before), windowId = FileId(before.GetComponent<RootUIPrefab>());

            GameObject fresh = Window("New Button", shape: true);
            using (var editing = new PrefabUtility.EditPrefabContentsScope(PrefabPath)) { UIAssets.ReplaceContents(editing.prefabContentsRoot, fresh); }
            Object.DestroyImmediate(fresh);

            var after = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(AssetDatabase.AssetPathToGUID(PrefabPath), Is.EqualTo(guid));
            Assert.That(FileId(after), Is.EqualTo(rootId), "The root's file ID should stay (what names the prefab names it).");
            var window = after.GetComponent<RootUIPrefab>();
            Assert.That(FileId(window), Is.EqualTo(windowId), "A component of a type it keeps should keep its file ID.");
            Assert.That(window.AllowMultiple, Is.True, "Its settings should be the new ones.");
            Assert.That(after.GetComponent<Image>(), Is.Null, "The old graphic should be gone...");
            Assert.That(after.GetComponent<ChamferedBox>(), Is.Not.Null, "...for the new one.");
            Assert.That(((RectTransform)after.transform).sizeDelta, Is.EqualTo(new Vector2(300f, 200f)));
            Assert.That(after.transform.Find("Old Button"), Is.Null);
            Transform button = after.transform.Find("New Button");
            Assert.That(button, Is.Not.Null);
            Object target = button.GetComponent<Button>().onClick.GetPersistentTarget(0);
            Assert.That(target, Is.SameAs(window), "The button's click should call the prefab's window, not the made one's.");
        }
    }
}

using MeridianMotion.UI;
using UnityEngine;

namespace MeridianMotion.UI.EditorTools
{
    public static partial class UIGallery
    {
        // The replays (J4): the pause menu's Replays tab, and the choice at a replay's end.
        private static void ReplaysPage(GalleryPage page)
        {
            page.Row("Pause menu");
            GameObject menu = Instance(page, "Pause Menu: Replays", UIAssets.PauseMenuPrefabPath, 0.62f);
            Show(menu, "Main Page", false);
            Show(menu, "Replays Page", true);
            Show(menu, "Replays Tab", true);
            Show(menu, "Menu Tab/Selected", false);
            Show(menu, "Replays Tab/Selected", true);
            Text(menu, "Replays Page/Status", "Recording: 4:12\nLast saved: MeridianBasin 2026-11-24 14-02-31.mmreplay");

            page.Row("Choice dialog");
            GameObject end = Instance(page, "At a replay's end", UIAssets.ChoiceDialogPrefabPath, 0.8f);
            end.GetComponent<UIChoiceDialog>().Show("The replay's over.", 0,
                new UIChoiceDialog.Option("Continue playing", null, ThemedButton.Kind.Primary),
                new UIChoiceDialog.Option("Replay again", null),
                new UIChoiceDialog.Option("Reset level", null));
            foreach (Transform child in end.GetComponentsInChildren<Transform>(true)) { child.gameObject.layer = end.layer; }
        }
    }
}

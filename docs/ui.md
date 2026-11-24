# UI Framework
A window framework for any game, on Unity's uGUI: a window manager with its canvases and the Back key, a base for every window, modal dialogs, a pause menu, data lists, tabs, context menus, notifications, controls gated on rules, and labels anchored to the world. It's its own assembly (`MeridianMotion.UI`, in Assets/MeridianMotion/Runtime/UI) and knows nothing of the character, the level or the camera: it tells the game what its windows need through events the scene connects (see [How it meets the game](#how-it-meets-the-game)). Like the camera, whether it ships with the package is undecided; nothing else that ships depends on it.

*See also:* [Architecture](architecture.md#what-may-know-about-what), [Glossary](glossary.md), [Unity Basics](unity-basics.md).

## The layers
- **The window framework:** `UIManager`, a scene singleton (its `instance`) that owns three canvases (In World, for labels anchored to the world; Default, for windows; Overlay, over every window) and which window is open; `RootUIPrefab`, the base every window has on its root; the dialogs; the click blockers; and the labels anchored to the world.
- **Showing data:** `DataDisplay<T>` (one row) and `DataDisplayList<T,U>` (a pooled, searchable list of them).
- **Widgets and services:** tabs, the context menu, notifications, rule-gated controls, and `LayoutRebuildHelper`.
- **The game** sits outside them all, reached only through what a list fetches (its data source), what a gated control asks (the rules service), and what a menu item or a dialog's button does (its callback).

## Setting it up
**Tools > Meridian Motion > Set Up Scene…** adds the UI to the scene it sets up (see [Spawning the Character](spawning.md#setting-up-a-scene)): an instance of the UI Manager prefab (Assets/MeridianMotion/Content/UI), or the scene's own UI Manager if it has one, connected to the camera and the player's input. The prefab holds the three canvases, an EventSystem whose input module reads the input actions' UI map (Navigate, Submit, Cancel, Point, Click and the rest), the notifications, and every field set: the framework's own dialogs, the context menu, the click blockers, the Back and Submit keys, and the window catalog. A scene with an EventSystem of its own keeps it, and the UI Manager's is turned off.

The UI's prefabs (the UI Manager, the dialogs, the pause menu, the context menu and its entries, the notifications' styles, the click blockers, the window catalog) are made by **Tools > Meridian Motion > Utilities > Create UI Assets** where they're missing, in the levels' palette, then edited by hand: nothing remakes one that's there.

## Windows
Every window has a `RootUIPrefab` on its root (or a class deriving it). In its inspector:
- **Window:** Parent Canvas (the canvas it goes on), Allow Multiple (opening it again makes another copy, rather than bringing the open one to the front), Exempt From Auto Close (it stays when every window's closed: a HUD).
- **Back key:** Close On Cancel Button, and Cancel Action Overrides (what Back does instead of closing it: go back a page, say).
- **Click blocker:** what's put behind it to stop clicks reaching what's under it: Dim, Invisible, Darken or Blackout Background (the UI Manager's four blocker prefabs).
- **Animation:** an Animator playing its open and close. Its close is the Animator's "Close" trigger into a state tagged "Close" (`RootUIPrefab.CloseTrigger`, `CloseStateTag`), or an animation event calling `Animator_OnCloseComplete`. Animators run in unscaled time, so a window animates while the game is paused.
- **The game:** Needs Cursor (the cursor's freed while it's open) and Blocks Game Input (the player's input stops while it's open).

A window is opened through the UI Manager, never by instantiating it: `UIManager.instance.OpenUI<T>(prefab)` returns the window's component T (a window's own class needn't derive `RootUIPrefab`: a component beside it on the prefab is what's returned). Closing (`Close()`) raises OnClosing, closes its blocker, plays its close, then raises OnClosed and destroys it: closed windows aren't kept, and the next open makes another. `HideAllUI` hides every window, keeping its state (for a cutscene), and `RestorePreviouslyShownUI` shows them again as they were stacked. `CloseAllRootUIs` closes all but those exempt.

### A window of your own
1. Make its prefab: a RectTransform with a `RootUIPrefab` (or your class deriving it) on its root, and your own component for what it does.
2. Add the prefab to the window catalog (UI Window Catalog, in Assets/MeridianMotion/Content/UI, or a catalog of your own set on the UI Manager), so it's found by its component.
3. Give your class a static `Open` that opens it and sets it up, so nothing else touches the prefab:

```csharp
public class InventoryWindow : MonoBehaviour
{
    public static InventoryWindow Open(Inventory inventory)
    {
        InventoryWindow window = UIManager.instance.Open<InventoryWindow>();
        window.Initialize(inventory);
        return window;
    }

    public void Initialize(Inventory inventory) { /* fill it */ }
}
```

## The Back key
Back (the UI map's Cancel: Escape, or the gamepad's Cancel button) goes to the top window that takes it: the last opened that's still shown, isn't closing, and does something with Back. That window closes, or runs its Cancel Action Overrides; pressed again, Back goes to the next. With no window to take it, the UI Manager's fallback runs (`RegisterFallbackHandler`): on the UI Manager prefab, `PauseMenuOnBack` makes it open the pause menu. Back does nothing while a text field has the keyboard. Submit (Enter) goes to the top window too: a dialog takes it as OK. Code can press either: `HandleBack`, `HandleSubmit`.

## The pause menu
`PauseMenu` (Assets/MeridianMotion/Content/UI/Windows) has Resume, Restart (the scene again), Controls and Quit (asked first, with a yes/no dialog). While it's open the game is paused (the time scale's put back as it was when it closes) and, as it Needs Cursor and Blocks Game Input, the cursor's freed and the player stops. Controls is a tab listing the bindings, read from the input actions (its Actions, Action Map and Control Schemes), the actions in its Hidden Actions left out. To do something else with Back, take `PauseMenuOnBack` off the UI Manager and register your own fallback.

## Dialogs
`UIMessageBox.Open(text, onOk)`, `OpenError(text, onOk)` and `OpenYesNoDialog(text, onYes, onNo)` open the UI Manager's dialog prefabs and return at once: the callbacks are called when a button's pressed, and the dialog then closes itself. Back declines (No), or, for a dialog with only OK, presses OK, so the caller always hears. `UIMessageBox_PromptForString.Open(prompt, onSubmit)` asks for a line of text, passed to onSubmit on OK (or Enter in its field).

## Data lists
A list of records is two small classes over the framework's: a row (`DataDisplay<T>`, drawing one record) and a list (`DataDisplayList<T,U>`, saying where the records come from). The list fetches them (`GetData`), keeps those matching every word of its search terms (`HasSearchTerm`, `SetSearchTerms`) and of the IDs it's given (`Populate`), and shows each in a row from its pool: rows are made from its Item Prefabs in turn (two give alternating rows), reused, and hidden when unused. Its selection is kept across refreshes (else the first row's), and On Item Selected tells its panel. Shown If Items and Shown If No Items show an empty state; a loading indicator and a refresh button show while it fetches. `DataDisplayList_Categorized` puts a header (its Category Header Prefab) before each category's rows. A record derives `Identifiable` (`IdentifiableElement`, with a category):

```csharp
public class ItemData : Identifiable { public string name; public int price; }

public class ItemDisplay : DataDisplay<ItemData>
{
    [SerializeField] private TMPro.TextMeshProUGUI label;
    protected override void Refresh() => label.text = $"{Data.name}  {Data.price}";
    public override void InitializeFromID(string id) { /* look the record up */ }
}

public class ItemDisplayList : DataDisplayList<ItemData, ItemDisplay>
{
    protected override async Awaitable<Dictionary<string, ItemData>> GetData() => await MyShop.LoadItems();
    protected override bool HasSearchTerm(ItemData item, string term) => item.name.Contains(term);
}
```

## Tabs
A `TabGroup` holds its `Tab`s, one selected at a time: clicking a tab (its button calls `OnClicked`) shows its Selected State and its Tab Contents and hides the others'. With Allow Deselection, clicking the selected tab deselects it. A tab can track a window (Root UI To Track): selecting it opens the window, and the window's closing deselects it (a toolbar kept in step with its windows). Tabs find their group by themselves.

## Context menus
`UIRightClickMenu.Open(items)` opens the UI Manager's context menu at the pointer, flipped to stay on the screen, with an entry for each `MenuItemData` (its text, icon and callback; Featured for the main choice; ShowIf to leave it out). `MenuItemData.Spacer` splits the items into groups, each entry drawn for its place in its group (rounded at a group's ends). It closes when an entry's clicked, when the pointer leaves it, or if the pointer never comes onto it (its Never Entered Close Seconds).

## Notifications
`UIManager.instance.ShowNotification(title, message)` shows a toast (`Notifications.ShowNotification` takes a duration, a place on the screen and a style too). Each style (`Notifications.Type`: Passive, Center) has its prefab, and each place (`Notifications.Location`) its parent, on the UI Manager's Notifications. At most Max Notif Count of a style show at once (the oldest closes for a new one), each for its duration (or Default Duration), on the UI's clock, so they run while the game's paused. A message of several lines shows its first until Read More's clicked. `ClearAllPassiveNotifications` closes the toasts. Add values to both enums for a game's own styles and places.

## Rule-gated controls
To hide a control, or grey it out, unless the player's allowed to use it, add **Permission Visibility** (shows its Target, its own object unless set) or **Permission Interactability** (its Selectable) and pick its Feature and Permission: rules, `UIRule` assets (Create > Meridian Motion > UI > Rule), each permitted or not by default. The game sets them (`RulesService.Set(rule, permitted)`), and every gated control is evaluated again at once, with no window opened again; code drawing its own controls listens to `RulesService.OnSelfPermissionsUpdated`. A gated control registers as it wakes, so put it on an object that starts active (pointing its Target at what starts hidden).

## Labels in the world
`FollowWorldTransformUIPrefab` is a window anchored to something in the world (a name tag): each frame, after the camera's moved, it's put where its target (plus World Position Offset) shows on its canvas (plus UI Position Offset), hidden while the target's behind the camera, and closed when the target's destroyed (Destroy On Follow Target Null). Its subclass `FollowWorldTransformWithSafeAreaUIPrefab` stays inside the screen's safe area (Safe Area Padding in from it). A label of your own derives one and calls `Initialize(target, camera)` from its static Open; on the In World canvas, behind the windows.

## Time
Every UI timer (notifications, a close animation's limit, a menu never pointed at) goes by `UIClock.Now`: unscaled time, so the UI runs while the game is paused. Tests set `UIClock.TimeSource` to drive it themselves (see [Automated Tests](testing.md#ui-the-ui-category)).

## How it meets the game
The UI raises; the scene connects. Set Up Scene connects these, as persistent listeners (the UI's editor tools do it, through `CharacterSceneSetup.AfterSettingUp`, so a package cut without the UI sets scenes up without it):

| The UI Manager's | Connected to | So that |
| --- | --- | --- |
| On Cursor Needed Changed | the camera rig's Set Cursor Requested | a window that needs the pointer frees the cursor, and it locks again when the last one closes |
| On Game Input Blocked Changed | the player's input reader's Set Suppressed | a window that stops the game (a dialog, the pause menu) stops the player; a key held through it isn't taken as a press when it closes |

Back is the UI's: Set Up Scene sets the camera's Unlock Key to None, and the pause menu frees the cursor. In a scene without the UI, Escape still releases the cursor as before.

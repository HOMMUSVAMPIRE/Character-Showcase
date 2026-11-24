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

The UI's prefabs (the UI Manager, the dialogs, the pause menu, the context menu and its entries, the notifications' styles, the click blockers, the widgets, the window catalog, the theme) are made by **Tools > Meridian Motion > Utilities > Create UI Assets** where they're missing, drawn in the theme (see [The look](#the-look)), then edited by hand: Create UI Assets remakes none that's there. **Rebuild UI Assets** (or `UIAssets.CreateFromCommandLine` with `-rebuild`) remakes every one but the UI Manager in place, from `UIAssets`: their hand edits are lost, but their GUIDs and their roots stay, so what names them (the UI Manager, the window catalog, a scene) still does. The UI Manager is never rebuilt, since scenes hold it with their own connections; Create UI Assets only fills its empty fields (a new notification style's prefab, say).

## The look
The UI's look is one asset, its theme (`UITheme`: UI Theme, in Assets/MeridianMotion/Content/UI, named by the UI Manager's Theme). It holds a colour for each role (`UIColorRole`: Backdrop, Panel, Panel Raised, Outline, Text, Accent, Highlight, Disabled, Positive, Warning, Danger and the rest), how far a panel's corners are cut (Chamfer Size), an outline's width, a glow's size and strength, the fonts, and the tints of a control's states (hovered, pressed, selected, disabled; `StateTints` gives them as a Selectable's colours). What's drawn asks for a role, never a colour, so retuning the theme restyles all of it, at once: a change to it redraws every shape, in the editor too. Without a theme, the built-in one (`UITheme.BuiltIn`) is used.

The shapes (Add Component > Meridian Motion > UI > Shapes) draw themselves as meshes, with no sprites: sharp at any size, their edges smoothed by a feathered band a pixel wide. Each has a Fill Role and an Outline Role (and its own Theme, to draw in another), and its Color tints both:
- **Chamfered Box:** a panel or button, each corner cut or square (Corners, Chamfer: the theme's unless set), with an optional Header Tab and "//" Gloss.
- **Skewed Bar:** a slanted bar, continuous or in Segments, filled to its Fraction (progress, pips).
- **Diamond Shape:** a marker or toggle, with an optional Glow.
- **Ring Shape:** radial progress: its track, and its Fraction filled from Start Angle.
- **Stripe Fill:** hazard stripes or hatching (Stripe Width, Gap, Angle), clipped to its rect with a chamfered box's corners.
- **Corner Brackets** and **Divider** (with its accent segment).

### Fonts
When there's no UI Manager with a theme (a prefab being edited, the gallery), shapes and text are drawn in `UITheme.Fallback`, which the editor sets to the package's theme (`UIThemeFallback`); without it, in the built-in one.

The theme's Display Font (headers, numerals, buttons) is Chakra Petch Bold, and its Body Font Chakra Petch Regular: TextMeshPro font assets in Assets/MeridianMotion/Content/UI/Fonts, made from the font files beside them by **Tools > Meridian Motion > Utilities > Rebuild UI Fonts** (`UIFonts.RebuildFromCommandLine`), with the characters `UIFonts.Characters` lists baked into a static atlas. Rebuild them after adding a character there. Chakra Petch is under the SIL Open Font License 1.1 (its text, Chakra Petch OFL.txt, is beside it), which lets it be bundled with software, sold or not, as long as the licence goes with it and the font isn't sold on its own; it names no Reserved Font Name. To use other fonts, set the theme's.

- **Trapezoid Shape:** a tab of a tab bar, its sides slanted in by Slant (Upside Down: the wide edge at the top).

### Themed text and buttons
**Themed Text** (beside a TextMeshPro text) sets its font to the theme's Display or Body font and its colour to its Role's, and keeps them so as the theme changes: set them there, not on the text. **Themed Button** is a Button drawn in the theme: its Shape (a chamfered box, its target graphic) is coloured by its Kind (Primary: the accent, for the main choice; Secondary: light; Highlight; Outline: a panel outlined in the accent, for the lesser choices), its Label coloured to suit, its states tinted by the theme's state tints, and its Hatch (stripes) shown while it can't be used. Gloss gives it the "//" at its left, for long buttons only.

The widgets (Assets/MeridianMotion/Content/UI/Widgets) are prefabs for a project's own windows: **Button** (a themed button), **List Row** (a toggle on a panel row, the accent's with a filled diamond while selected: add your `DataDisplay` to it and put it in a list's Item Prefabs), **Scrollbar**, **Radio Toggle** (a diamond) and **Checkbox**. The dialogs are panels with the accent's tab along their top and corner brackets outside them, the pause menu's pages tabs of a trapezoid tab bar, and the click blockers the theme's backdrop.

A shape of your own derives `UIShapeGraphic` and builds its mesh in `Build` with `UIShapeMesh` (filled and outlined convex polygons, glows, arcs, clipping).

### Icons
`UIIcon` (Add Component > Meridian Motion > UI > Icon) draws an icon of the library's (`UIIconLibrary`) by its Icon Name, as a mesh in its Fill Role, fitted to its rect and centred: sharp at any size, recoloured by role. An unknown name draws a crossed box. The names are `UIIconLibrary`'s constants (`UIIconLibrary.Names` lists them all, and the gallery's Icons page shows them):
- **The system set:** markers, alerts and tools (pin, waypoint, alert, help, info, plus, gear, wrench, clipboard, database, download), places and locks (lock, unlock, elevator, stairs, door), marks (check, cross, warning), crate, chevrons, the four arrows, and the Meridian emblem.
- **The ability set:** simple figures for what the character does (grab, push, ladder, run, sprint, zipline, rope, wall-run, dash, crouch, slide, roll).

A name never changes once an icon's in use: every UIIcon showing it saves it, and a theme's override names it. A new icon is a new name.

**Drawing one.** An icon is a method of `UIIconLibrary` (`UIIconLibrary.System.cs`, `.Abilities.cs`), listed in its Icons with its name, drawing with a `UIIconPainter` on a 24-unit grid: (0, 0) its bottom left, y up, its content within about 2 units of the edge. The painter has strokes with round ends and corners (`Line`, `Loop`, `Frame`), filled convex polygons (`Poly`, `Box`), `Dot`, `Circle` and `Arc`, and `Turned` and `Ellipse` for points. Its strokes come in the library's weights (its constants: Stroke for most, Bold for glyphs and arrows, Fine for cables and ropes, Limb for a figure's body and legs), and a figure's head is a dot of HeadRadius. Everything's drawn in one colour and added together, so an icon has no holes: draw a hole's surround as strokes. Look at a new icon on the gallery's Icons page, at its large size as well as on a tile.

**An artist's icons.** A theme's Icon Overrides put a sprite in an icon's place, by name: every UIIcon of that name draws the sprite (fitted, its aspect kept, tinted by its role, so draw it white) until the override's taken out. So a set can be replaced one icon at a time.

### The gallery
**Tools > Meridian Motion > Utilities > Open UI Gallery** builds every shape and widget, in each of its states, in a new scene, on the theme's backdrop. To get pictures of it (to look at beside a reference, or after retuning the theme), run `UIGallery.CaptureFromCommandLine` with the editor closed:

```
<Unity> -batchmode -projectPath . -executeMethod MeridianMotion.UI.EditorTools.UIGallery.CaptureFromCommandLine
```

It writes a PNG of each page to Logs/UIGallery. A page is a method of a part of `UIGallery` (`UIGallery.<Page>.cs`), listed in its Pages.

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
`UIManager.instance.ShowNotification(title, message)` shows a toast (`Notifications.ShowNotification` takes a duration, a place on the screen and a style too). Each style (`Notifications.Type`: Passive, a toast; Center, a banner across the middle; and Success, Warning, Info, Item and Locked, toasts with their own icon and colour role) has its prefab, and each place (`Notifications.Location`) its parent, on the UI Manager's Notifications. At most Max Notif Count of a style show at once (the oldest closes for a new one), each for its duration (or Default Duration), on the UI's clock, so they run while the game's paused. A message of several lines shows its first until Read More's clicked. `ClearAllPassiveNotifications` closes the toasts. Add values to both enums for a game's own styles and places (on the end: a style's saved by its number); Create UI Assets makes a new style's prefab in the output folder, a toast to restyle.

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

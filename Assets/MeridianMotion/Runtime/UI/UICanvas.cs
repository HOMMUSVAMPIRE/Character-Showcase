namespace MeridianMotion.UI
{
    /// <summary>
    /// The canvases a window can be put on (UIManager's), from back to front: InWorld for labels anchored to the world,
    /// Default for windows, Overlay for what goes over every window (notifications, a context menu). None: as the window
    /// asks, for an override (OpenUI); the default canvas, for a window's own choice.
    /// </summary>
    public enum UICanvas
    {
        None,
        InWorld,
        Default,
        Overlay,
    }

    /// <summary>
    /// What's put behind a window to stop clicks reaching what's under it (a click blocker): nothing, or a full-screen
    /// panel that dims, is invisible, darkens or blacks out the background. Each is a prefab on the UIManager.
    /// </summary>
    public enum ClickBlockStyle
    {
        None,
        DimBackground,
        Invisible,
        DarkenBackground,
        BlackoutBackground,
    }
}

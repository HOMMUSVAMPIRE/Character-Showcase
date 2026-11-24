using MeridianMotion.UI;
using UnityEngine;

namespace MeridianMotion.UI.EditorTools
{
    public static partial class UIGallery
    {
        // The HUD widgets (H6), each set as the game would set it.
        private static void HUDPage(GalleryPage page)
        {
            page.Row("Prompt, reticles");
            GameObject prompt = Instance(page, "Interact prompt, holding", UIAssets.HUDPath("Interact Prompt"), 0.85f);
            Text(prompt, "Contents/Label", "CLIMB");
            Text(prompt, "Contents/Key/Binding", "E");
            prompt.GetComponent<InteractPrompt>().HoldRing.Fraction = 0.6f;
            foreach (Reticle.Style style in System.Enum.GetValues(typeof(Reticle.Style)))
            {
                Instance(page, style.ToString(), UIAssets.HUDPath("Reticle"), 1.4f).GetComponent<Reticle>().CurrentStyle = style;
            }

            page.Row("Abilities, compass");
            var bar = Instance(page, "Ability bar: ready, active, cooling, unavailable, ready", UIAssets.HUDPath("Ability Bar"), 1f).GetComponent<AbilityBar>();
            bar.SetAbilities(new[] { ("dash", UIIconLibrary.Dash), ("roll", UIIconLibrary.Roll), ("sprint", UIIconLibrary.Sprint), ("grab", UIIconLibrary.Grab), ("zip", UIIconLibrary.Zipline) });
            bar.SetState("roll", AbilityTile.State.Active);
            bar.SetState("sprint", AbilityTile.State.CoolingDown, 0.4f);
            bar.SetState("grab", AbilityTile.State.Unavailable);
            page.Row("");
            Instance(page, "Compass, heading 30", UIAssets.HUDPath("Compass"), 1f).GetComponent<UICompass>().Heading = 30f;

            page.Row("Markers, map");
            Text(Instance(page, "World marker", UIAssets.HUDPath("World Marker"), 1f), "Distance", "42 m");
            GameObject edge = Instance(page, "At the screen's edge", UIAssets.HUDPath("World Marker"), 1f);
            Text(edge, "Distance", "180 m");
            Show(edge, "Arrow", true);
            edge.transform.Find("Arrow").localRotation = Quaternion.Euler(0f, 0f, -60f);
            var minimap = Instance(page, "Minimap", UIAssets.HUDPath("Minimap"), 0.8f).GetComponent<MinimapView>();
            minimap.Source = new TextureMinimapSource { texture = GridTexture(), worldBounds = new Rect(-100f, -100f, 200f, 200f) };
            minimap.SetPlayer(new Vector3(5f, 0f, -8f), 35f);
            minimap.AddMarker("tower", new Vector3(25f, 0f, 15f));
            minimap.AddMarker("gate", new Vector3(-150f, 0f, 40f), UIIconLibrary.Pin, UIColorRole.Highlight);

            page.Row("Dialogue");
            GameObject dialogue = Instance(page, "Dialogue panel", UIAssets.HUDPath("Dialogue Panel"), 0.75f);
            Text(dialogue, "Speaker Tab/Speaker", "RELAY OPERATOR");
            Text(dialogue, "Line", "The lift's out again. Take the ladder by the pumps, then the zipline across the basin; I'll hold the gate.");
        }

        // A map's stand-in: a graphite grid with lighter blocks, for the minimap's picture.
        private static Texture2D GridTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            Color ground = new(0.24f, 0.26f, 0.29f), line = new(0.33f, 0.36f, 0.40f), block = new(0.45f, 0.48f, 0.52f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool grid = x % 16 == 0 || y % 16 == 0, solid = (x / 16 + y / 16 * 3) % 5 == 0 && x % 16 > 3 && y % 16 > 3;
                    texture.SetPixel(x, y, solid ? block : grid ? line : ground);
                }
            }
            texture.Apply();
            return texture;
        }
    }
}

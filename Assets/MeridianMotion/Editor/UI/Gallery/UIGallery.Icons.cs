using MeridianMotion.UI;
using UnityEngine;

namespace MeridianMotion.UI.EditorTools
{
    public static partial class UIGallery
    {
        // The icons (H4): every one of the library's, on a tile, then a few at other sizes and in other roles.
        private static void IconsPage(GalleryPage page)
        {
            const int perRow = 11;
            int index = 0;
            foreach (string name in UIIconLibrary.Names)
            {
                if (index % perRow == 0) { page.Row(index == 0 ? "Library" : ""); }
                index++;
                RectTransform slot = page.Add(name, new Vector2(72f, 72f));
                ChamferedBox tile = page.Shape<ChamferedBox>(slot);
                tile.FillRole = UIColorRole.PanelRaised;
                tile.Chamfer = 8f;
                UIIcon icon = page.Shape<UIIcon>(Inset(slot, 14f));
                icon.IconName = name;
            }

            page.Row("Sizes, roles");
            foreach (float size in new[] { 16f, 24f, 32f, 48f })
            {
                page.Add<UIIcon>($"{size:0}", new Vector2(size, size)).IconName = UIIconLibrary.Gear;
            }
            (string name, UIColorRole role)[] roles =
            {
                (UIIconLibrary.Check, UIColorRole.Positive), (UIIconLibrary.Warning, UIColorRole.Warning),
                (UIIconLibrary.Info, UIColorRole.Info), (UIIconLibrary.Lock, UIColorRole.Disabled), (UIIconLibrary.Cross, UIColorRole.Danger),
            };
            foreach ((string name, UIColorRole role) in roles)
            {
                UIIcon icon = page.Add<UIIcon>(role.ToString(), new Vector2(48f, 48f));
                icon.IconName = name;
                icon.FillRole = role;
            }
            page.Add<UIIcon>("Unknown name", new Vector2(48f, 48f)).IconName = "no-such-icon";

            // The figures are the most detailed: large, to judge them.
            page.Row("Abilities, large");
            foreach (string name in new[] { UIIconLibrary.Grab, UIIconLibrary.Push, UIIconLibrary.Run, UIIconLibrary.Sprint, UIIconLibrary.Zipline,
                UIIconLibrary.Rope, UIIconLibrary.WallRun, UIIconLibrary.Crouch, UIIconLibrary.Slide, UIIconLibrary.Roll, UIIconLibrary.Emblem })
            {
                page.Add<UIIcon>(name, new Vector2(96f, 96f)).IconName = name;
            }
        }

        // A child of parent inset by inset on every side.
        private static RectTransform Inset(RectTransform parent, float inset)
        {
            var obj = new GameObject("Inset", typeof(RectTransform));
            obj.layer = parent.gameObject.layer;
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }
    }
}

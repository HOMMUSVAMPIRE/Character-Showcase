using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MeridianMotion.UI.EditorTools
{
    // The HUD widgets' prefabs (ReferenceAssets/OpenTasks.md, H6): the interact prompt, the world marker and the dialogue
    // panel (windows, in the catalog), and the ability tile and bar, the reticle, the minimap and the compass (pieces for a
    // HUD window of the game's).
    public static partial class UIAssets
    {
        private static readonly Vector2 InteractPromptSize = new(560f, 84f), MarkerSize = new(120f, 96f), DialogueSize = new(1100f, 230f);
        private static readonly Vector2 TileSize = new(72f, 72f), MinimapSize = new(240f, 240f), CompassSize = new(520f, 44f);
        private const float PromptAboveBottom = 220f, DialogueAboveBottom = 60f, PortraitSize = 170f, SpeakerTabHeight = 38f;
        private const float HoldRingThickness = 4f, TileGap = 10f, CompassPointSize = 20f, CompassUnitsPerDegree = 2.6f;

        private static readonly string[] CompassPoints = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        // Every HUD widget's prefab (the windows added to the catalog).
        private static void MakeHUD(InputActionAsset actions, UIWindowCatalog catalog)
        {
            void Catalogue(GameObject window)
            {
                if (window == null || catalog.Windows.Contains(window)) { return; }
                catalog.Windows.Add(window);
                EditorUtility.SetDirty(catalog);
            }
            Catalogue(PrefabAt(HUDPath("Interact Prompt"), () => InteractPromptWindow(actions)));
            Catalogue(PrefabAt(HUDPath("World Marker"), WorldMarkerWindow));
            Catalogue(PrefabAt(HUDPath("Dialogue Panel"), DialoguePanelWindow));
            GameObject tile = PrefabAt(HUDPath("Ability Tile"), AbilityTileWidget);
            PrefabAt(HUDPath("Ability Bar"), () => AbilityBarWidget(tile));
            PrefabAt(HUDPath("Reticle"), ReticleWidget);
            PrefabAt(HUDPath("Minimap"), MinimapWidget);
            PrefabAt(HUDPath("Compass"), CompassWidget);
        }

        // A child of parent of the size, anchored and pivoted at anchor, at position from it.
        private static RectTransform Placed(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 position)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        // A child stretched over parent, less the offsets.
        private static RectTransform Stretched(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            Stretch(rect, Vector2.zero, Vector2.one, offsetMin, offsetMax);
            return rect;
        }

        // A HUD window: no blocker, doesn't take Back, close with the rest, or want the cursor.
        private static void HUDWindow(RootUIPrefab window, UICanvas canvas)
        {
            window.ParentCanvas = canvas;
            window.CloseOnCancelButton = false;
            window.NeedsCursor = false;
            window.ExemptFromAutoClose = true;
            window.ClickBlockerStyle = ClickBlockStyle.None;
        }

        // The interact prompt: a light call-out, the accent's hazard stripes at its left, the binding on a key with the
        // hold ring round it, the label, and an icon at its right.
        private static GameObject InteractPromptWindow(InputActionAsset actions)
        {
            var root = new GameObject("Interact Prompt", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = InteractPromptSize;
            rect.anchoredPosition = new Vector2(0f, PromptAboveBottom);
            var prompt = root.AddComponent<InteractPrompt>();
            HUDWindow(prompt, UICanvas.Default);

            RectTransform contents = Stretched(root.transform, "Contents", Vector2.zero, Vector2.zero);
            var panel = contents.gameObject.AddComponent<ChamferedBox>();
            panel.FillRole = UIColorRole.Light;
            panel.Outline = false;
            panel.Corners = UIShapeMesh.Corners.TopLeft | UIShapeMesh.Corners.BottomRight;
            panel.raycastTarget = false;

            float stripes = InteractPromptSize.y;
            RectTransform block = Placed(contents, "Stripes", new Vector2(stripes, InteractPromptSize.y), new Vector2(0f, 0.5f), Vector2.zero);
            var blockShape = block.gameObject.AddComponent<ChamferedBox>();
            blockShape.FillRole = UIColorRole.Accent;
            blockShape.Outline = false;
            blockShape.Corners = UIShapeMesh.Corners.TopLeft;
            RectTransform hatch = Stretched(block, "Hatch", Vector2.zero, Vector2.zero);
            var hatchShape = hatch.gameObject.AddComponent<StripeFill>();
            hatchShape.FillRole = UIColorRole.Panel;
            hatchShape.StripeWidth = 7f;
            hatchShape.Gap = 9f;
            hatchShape.Corners = UIShapeMesh.Corners.TopLeft;

            float key = InteractPromptSize.y - Padding * 0.9f;
            RectTransform keyRect = Placed(contents, "Key", new Vector2(key, key), new Vector2(0f, 0.5f), new Vector2(stripes + Padding * 0.6f, 0f));
            var keyShape = keyRect.gameObject.AddComponent<ChamferedBox>();
            keyShape.FillRole = UIColorRole.Panel;
            keyShape.Outline = false;
            keyShape.Chamfer = SmallChamfer;
            TextMeshProUGUI binding = MakeText(keyRect, "Binding", "E", ButtonFontSize, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, display: true);
            binding.enableAutoSizing = true;
            binding.fontSizeMin = 10f;
            binding.fontSizeMax = ButtonFontSize;
            RectTransform ringRect = Stretched(keyRect, "Hold Ring", -Vector2.one * (HoldRingThickness * 2f), Vector2.one * (HoldRingThickness * 2f));
            var ring = ringRect.gameObject.AddComponent<RingShape>();
            ring.Thickness = HoldRingThickness;
            ring.Track = false;
            ring.Fraction = 0f;
            var hold = ringRect.gameObject.AddComponent<UIRadialProgress>();
            hold.Ring = ring;

            float labelLeft = stripes + Padding * 1.2f + key;
            TextMeshProUGUI label = MakeText(contents, "Label", "INTERACT", TitleFontSize * 0.7f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one,
                new Vector2(labelLeft, 0f), new Vector2(-InteractPromptSize.y, 0f), display: true, UIColorRole.TextOnFill);
            RectTransform iconRect = Placed(contents, "Icon", new Vector2(InteractPromptSize.y * 0.55f, InteractPromptSize.y * 0.55f), new Vector2(1f, 0.5f), new Vector2(-Padding * 0.6f, 0f));
            var icon = iconRect.gameObject.AddComponent<UIIcon>();
            icon.IconName = UIIconLibrary.ArrowRight;
            icon.FillRole = UIColorRole.Accent;

            var fields = new SerializedObject(prompt);
            fields.FindProperty("label").objectReferenceValue = label;
            fields.FindProperty("bindingText").objectReferenceValue = binding;
            fields.FindProperty("icon").objectReferenceValue = icon;
            fields.FindProperty("holdRing").objectReferenceValue = hold;
            fields.FindProperty("action").objectReferenceValue = ActionReference(actions, "Player", "Interact");
            fields.FindProperty("contents").objectReferenceValue = contents.gameObject;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // A world marker: a glowing diamond, the distance under it, and an arrow round it (shown at the screen's edge).
        private static GameObject WorldMarkerWindow()
        {
            var root = new GameObject("World Marker", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = MarkerSize;
            var marker = root.AddComponent<WorldMarker>();
            HUDWindow(marker, UICanvas.InWorld);
            marker.AllowMultiple = true;

            RectTransform diamondRect = Placed(root.transform, "Diamond", new Vector2(34f, 34f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f));
            var diamond = diamondRect.gameObject.AddComponent<DiamondShape>();
            diamond.FillRole = UIColorRole.Accent;
            diamond.OutlineRole = UIColorRole.Light;
            diamond.Glow = true;
            diamond.GlowRole = UIColorRole.Accent;
            diamond.raycastTarget = false;
            TextMeshProUGUI distance = MakeText(root.transform, "Distance", "0 m", 18f, TextAlignmentOptions.Bottom, Vector2.zero, new Vector2(1f, 0.3f),
                Vector2.zero, Vector2.zero, display: true);
            distance.raycastTarget = false;
            RectTransform arrow = Placed(root.transform, "Arrow", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f));
            RectTransform arrowIcon = Placed(arrow, "Icon", new Vector2(24f, 24f), new Vector2(0.5f, 0.5f), new Vector2(0f, 34f));
            var arrowShape = arrowIcon.gameObject.AddComponent<UIIcon>();
            arrowShape.IconName = UIIconLibrary.ArrowUp;
            arrowShape.FillRole = UIColorRole.Accent;
            arrowShape.raycastTarget = false;
            arrow.gameObject.SetActive(false);

            var fields = new SerializedObject(marker);
            fields.FindProperty("distanceText").objectReferenceValue = distance;
            fields.FindProperty("arrow").objectReferenceValue = arrow;
            fields.FindProperty("worldPositionOffset").vector3Value = Vector3.zero;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // The dialogue panel: a panel with corner brackets, the speaker on the accent's tab above it, the portrait's frame
        // at its left, the line, and the "next" arrow at its bottom right.
        private static GameObject DialoguePanelWindow()
        {
            var root = new GameObject("Dialogue Panel", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = DialogueSize;
            rect.anchoredPosition = new Vector2(0f, DialogueAboveBottom);
            Panel(root, headerTab: false);
            var panel = root.AddComponent<DialoguePanel>();
            panel.ParentCanvas = UICanvas.Default;
            panel.NeedsCursor = false;
            panel.BlocksGameInput = true;
            panel.ClickBlockerStyle = ClickBlockStyle.None;

            RectTransform frame = Placed(root.transform, "Portrait Frame", new Vector2(PortraitSize, PortraitSize), new Vector2(0f, 0.5f), new Vector2(Padding, 0f));
            var frameShape = frame.gameObject.AddComponent<ChamferedBox>();
            frameShape.FillRole = UIColorRole.PanelRaised;
            frameShape.Corners = UIShapeMesh.Corners.TopLeft | UIShapeMesh.Corners.BottomRight;
            RectTransform silhouette = Stretched(frame, "Silhouette", Vector2.one * Padding, -Vector2.one * Padding);
            var person = silhouette.gameObject.AddComponent<UIIcon>();
            person.IconName = UIIconLibrary.Run;
            person.FillRole = UIColorRole.Disabled;
            RectTransform portraitRect = Stretched(frame, "Portrait", Vector2.one * 4f, -Vector2.one * 4f);
            var portrait = portraitRect.gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.enabled = false;

            float textLeft = Padding * 2f + PortraitSize;
            RectTransform tab = Placed(root.transform, "Speaker Tab", new Vector2(320f, SpeakerTabHeight), new Vector2(0f, 1f), new Vector2(textLeft - Padding * 0.5f, SpeakerTabHeight * 0.6f));
            var tabShape = tab.gameObject.AddComponent<ChamferedBox>();
            tabShape.FillRole = UIColorRole.Accent;
            tabShape.Outline = false;
            tabShape.Corners = UIShapeMesh.Corners.TopLeft | UIShapeMesh.Corners.TopRight;
            TextMeshProUGUI speaker = MakeText(tab, "Speaker", "SPEAKER", ButtonFontSize, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one,
                new Vector2(Padding * 0.5f, 0f), Vector2.zero, display: true, UIColorRole.TextOnFill);
            TextMeshProUGUI line = MakeText(root.transform, "Line", "", BodyFontSize, TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one,
                new Vector2(textLeft, Padding * 1.5f), new Vector2(-Padding * 2f, -Padding - SpeakerTabHeight * 0.4f));
            RectTransform next = Placed(root.transform, "Next", new Vector2(28f, 28f), new Vector2(1f, 0f), new Vector2(-Padding, Padding * 0.6f));
            var nextShape = next.gameObject.AddComponent<UIIcon>();
            nextShape.IconName = UIIconLibrary.ArrowDown;
            nextShape.FillRole = UIColorRole.Accent;

            var fields = new SerializedObject(panel);
            fields.FindProperty("speakerText").objectReferenceValue = speaker;
            fields.FindProperty("lineText").objectReferenceValue = line;
            fields.FindProperty("portrait").objectReferenceValue = portrait;
            fields.FindProperty("nextArrow").objectReferenceValue = next.gameObject;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // An ability tile: a square chamfered tile, its icon, its cooldown ring and its key.
        private static GameObject AbilityTileWidget()
        {
            var root = new GameObject("Ability Tile", typeof(RectTransform), typeof(LayoutElement));
            ((RectTransform)root.transform).sizeDelta = TileSize;
            root.GetComponent<LayoutElement>().preferredWidth = TileSize.x;
            root.GetComponent<LayoutElement>().preferredHeight = TileSize.y;
            var frame = root.AddComponent<ChamferedBox>();
            frame.Chamfer = SmallChamfer + 3f;
            RectTransform iconRect = Stretched(root.transform, "Icon", Vector2.one * 15f, -Vector2.one * 15f);
            var icon = iconRect.gameObject.AddComponent<UIIcon>();
            icon.IconName = UIIconLibrary.Dash;
            RectTransform ringRect = Stretched(root.transform, "Cooldown", Vector2.one * 5f, -Vector2.one * 5f);
            var ring = ringRect.gameObject.AddComponent<RingShape>();
            ring.Thickness = HoldRingThickness;
            ring.Track = false;
            ring.FillRole = UIColorRole.Highlight;
            var cooldown = ringRect.gameObject.AddComponent<UIRadialProgress>();
            cooldown.Ring = ring;
            TextMeshProUGUI key = MakeText(root.transform, "Key", "", 14f, TextAlignmentOptions.BottomRight, Vector2.zero, Vector2.one,
                Vector2.one * 5f, -Vector2.one * 6f, display: true, UIColorRole.TextMuted);
            var tile = root.AddComponent<AbilityTile>();
            tile.Frame = frame;
            tile.Icon = icon;
            tile.Cooldown = cooldown;
            var fields = new SerializedObject(tile);
            fields.FindProperty("keyText").objectReferenceValue = key;
            fields.ApplyModifiedPropertiesWithoutUndo();
            tile.SetState(AbilityTile.State.Ready);
            return root;
        }

        // An ability bar: its tiles in a row.
        private static GameObject AbilityBarWidget(GameObject tile)
        {
            var root = new GameObject("Ability Bar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = TileGap;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = root.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ((RectTransform)root.transform).sizeDelta = new Vector2(TileSize.x * 4f + TileGap * 3f, TileSize.y);
            root.AddComponent<AbilityBar>().TilePrefab = tile.GetComponent<AbilityTile>();
            return root;
        }

        private static GameObject ReticleWidget()
        {
            var root = new GameObject("Reticle", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(48f, 48f);
            root.AddComponent<Reticle>().raycastTarget = false;
            return root;
        }

        // The minimap: its picture in a round mask, the rim, north, the markers (from a hidden template) and the player's
        // arrow at its centre.
        private static GameObject MinimapWidget()
        {
            var root = new GameObject("Minimap", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = MinimapSize;
            RectTransform maskRect = Stretched(root.transform, "Mask", Vector2.zero, Vector2.zero);
            var disc = maskRect.gameObject.AddComponent<RingShape>();
            disc.Thickness = MinimapSize.x / 2f;
            disc.Track = false;
            disc.FillRole = UIColorRole.Panel;
            maskRect.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            RectTransform mapRect = Stretched(maskRect, "Map", Vector2.zero, Vector2.zero);
            var map = mapRect.gameObject.AddComponent<RawImage>();
            map.color = new Color(1f, 1f, 1f, 0.85f);
            map.raycastTarget = false;
            RectTransform markers = Stretched(root.transform, "Markers", Vector2.zero, Vector2.zero);
            RectTransform template = Placed(markers, "Marker Template", new Vector2(18f, 18f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var templateIcon = template.gameObject.AddComponent<UIIcon>();
            templateIcon.IconName = UIIconLibrary.Waypoint;
            templateIcon.FillRole = UIColorRole.Accent;
            template.gameObject.SetActive(false);
            RectTransform rim = Stretched(root.transform, "Rim", Vector2.zero, Vector2.zero);
            var rimShape = rim.gameObject.AddComponent<RingShape>();
            rimShape.Thickness = 3f;
            rimShape.Track = false;
            rimShape.FillRole = UIColorRole.Outline;
            rimShape.raycastTarget = false;
            RectTransform north = Placed(root.transform, "North", new Vector2(30f, 26f), new Vector2(0.5f, 1f), new Vector2(0f, 4f));
            var northTab = north.gameObject.AddComponent<ChamferedBox>();
            northTab.FillRole = UIColorRole.Accent;
            northTab.Outline = false;
            northTab.Chamfer = 6f;
            MakeText(north, "N", "N", 16f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, display: true, UIColorRole.TextOnFill);
            RectTransform arrow = Placed(root.transform, "Player", new Vector2(28f, 28f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var arrowShape = arrow.gameObject.AddComponent<UIIcon>();
            arrowShape.IconName = UIIconLibrary.ArrowUp;
            arrowShape.FillRole = UIColorRole.Accent;

            var view = root.AddComponent<MinimapView>();
            var fields = new SerializedObject(view);
            fields.FindProperty("map").objectReferenceValue = map;
            fields.FindProperty("playerArrow").objectReferenceValue = arrow;
            fields.FindProperty("markersParent").objectReferenceValue = markers;
            fields.FindProperty("markerPrefab").objectReferenceValue = templateIcon;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // The compass strip: the points along a band, clipped to it, north in the accent, and a tick at its middle.
        private static GameObject CompassWidget()
        {
            var root = new GameObject("Compass", typeof(RectTransform), typeof(RectMask2D));
            ((RectTransform)root.transform).sizeDelta = CompassSize;
            var band = root.AddComponent<ChamferedBox>();
            band.FillRole = UIColorRole.Panel;
            band.Corners = UIShapeMesh.Corners.TopLeft | UIShapeMesh.Corners.BottomRight;
            band.Chamfer = SmallChamfer;
            var labels = new List<TMP_Text>();
            foreach (string point in CompassPoints)
            {
                bool cardinal = point.Length == 1;
                RectTransform at = Placed(root.transform, point, new Vector2(48f, CompassSize.y), new Vector2(0.5f, 0.5f), Vector2.zero);
                TextMeshProUGUI text = MakeText(at, "Label", point, cardinal ? CompassPointSize : CompassPointSize * 0.75f, TextAlignmentOptions.Center,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, display: true, point == "N" ? UIColorRole.Accent : cardinal ? UIColorRole.Text : UIColorRole.TextMuted);
                labels.Add(text);
            }
            RectTransform tick = Placed(root.transform, "Tick", new Vector2(10f, 10f), new Vector2(0.5f, 0f), new Vector2(0f, -3f));
            var tickShape = tick.gameObject.AddComponent<DiamondShape>();
            tickShape.FillRole = UIColorRole.Accent;
            tickShape.Outline = false;
            var compass = root.AddComponent<UICompass>();
            compass.Points.AddRange(labels);
            compass.UnitsPerDegree = CompassUnitsPerDegree;
            compass.Heading = 0f;
            return root;
        }
    }
}

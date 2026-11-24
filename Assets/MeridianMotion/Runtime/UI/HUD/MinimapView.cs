using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A round minimap (ReferenceAssets/OpenTasks.md, H6): its source's picture (an IMinimapSource), north up, centred on
    /// the player (SetPlayer: a world position and a heading), showing View Size metres across; the player's arrow turned
    /// to the heading; and markers (AddMarker: an icon at a world position), each where its place falls on the map, held
    /// at the map's rim when it's further away. It knows nothing of the level: the game gives it its source and the
    /// player's place.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/HUD/Minimap View")]
    public class MinimapView : MonoBehaviour
    {
        [Tooltip("Shows the map's picture")]
        [SerializeField] private RawImage map;
        [Tooltip("The player's arrow (its up is the player's forward)")]
        [SerializeField] private RectTransform playerArrow;
        [Tooltip("Where the markers go")]
        [SerializeField] private RectTransform markersParent;
        [Tooltip("A marker (a UIIcon on its root: a prefab, or a hidden template in the map)")]
        [SerializeField] private UIIcon markerPrefab;
        [Tooltip("How many metres the map shows across")]
        [Min(1f)] [SerializeField] private float viewSize = 80f;
        [Tooltip("How far in (canvas units) from the rim a marker held at the rim sits")]
        [Min(0f)] [SerializeField] private float rimInset = 8f;
        [Tooltip("A picture shown without a source of the game's (optional)")]
        [SerializeField] private TextureMinimapSource defaultSource = new();

        private readonly Dictionary<string, (UIIcon icon, Vector3 place)> markers = new();
        private IMinimapSource source;
        private Vector3 player;
        private float heading;

        /// <summary>Where the picture comes from (null: its Default Source).</summary>
        public IMinimapSource Source { get => source ?? defaultSource; set { source = value; Redraw(); } }
        /// <summary>How many metres the map shows across.</summary>
        public float ViewSize { get => viewSize; set { viewSize = Mathf.Max(1f, value); Redraw(); } }
        /// <summary>The player's arrow.</summary>
        public RectTransform PlayerArrow { get => playerArrow; set => playerArrow = value; }

        /// <summary>Centres the map on the player, and turns their arrow to the heading (degrees clockwise from north, world +z).</summary>
        public void SetPlayer(Vector3 position, float headingDegrees)
        {
            player = position;
            heading = headingDegrees;
            Redraw();
        }

        /// <summary>
        /// Where a world position falls on the map, unclamped: its local position in the markers' parent (the map's rect,
        /// if it has none), the player at its centre.
        /// </summary>
        public Vector2 MapPosition(Vector3 world)
        {
            Rect rect = Space.rect;
            Vector2 offset = new(world.x - player.x, world.z - player.z);
            return rect.center + offset / viewSize * Mathf.Min(rect.width, rect.height);
        }

        /// <summary>Where a marker for the world position goes: its map position, held inside the rim (less Rim Inset).</summary>
        public Vector2 MarkerPosition(Vector3 world, out bool atRim)
        {
            Rect rect = Space.rect;
            Vector2 at = MapPosition(world), fromCentre = at - rect.center;
            float radius = Mathf.Min(rect.width, rect.height) / 2f - rimInset;
            atRim = fromCentre.magnitude > radius;
            return atRim ? rect.center + fromCentre.normalized * radius : at;
        }

        /// <summary>Adds (or moves) a marker: the icon, in the role, at the world position.</summary>
        public UIIcon AddMarker(string id, Vector3 world, string iconName = UIIconLibrary.Waypoint, UIColorRole role = UIColorRole.Accent)
        {
            if (!markers.TryGetValue(id, out (UIIcon icon, Vector3 place) marker) || marker.icon == null)
            {
                if (markerPrefab == null) { return null; }
                marker.icon = Instantiate(markerPrefab, markersParent != null ? markersParent : transform, false);
                marker.icon.name = id;
                marker.icon.gameObject.SetActive(true); // Its prefab may be a hidden template in the map.
            }
            marker.icon.IconName = iconName;
            marker.icon.FillRole = role;
            marker.place = world;
            markers[id] = marker;
            Place(marker.icon, world);
            return marker.icon;
        }

        /// <summary>Takes a marker off.</summary>
        public void RemoveMarker(string id)
        {
            if (markers.TryGetValue(id, out (UIIcon icon, Vector3 place) marker) && marker.icon != null) { Destroy(marker.icon.gameObject); }
            markers.Remove(id);
        }

        /// <summary>A marker's icon, or null.</summary>
        public UIIcon Marker(string id) => markers.TryGetValue(id, out (UIIcon icon, Vector3 place) marker) ? marker.icon : null;

        // What the markers are placed in.
        private RectTransform Space => markersParent != null ? markersParent : (RectTransform)transform;

        private void Redraw()
        {
            IMinimapSource from = Source;
            if (map != null && from != null)
            {
                map.texture = from.Map;
                Rect bounds = from.WorldBounds;
                if (bounds.width > 0f && bounds.height > 0f)
                {
                    Vector2 centre = new((player.x - bounds.xMin) / bounds.width, (player.z - bounds.yMin) / bounds.height);
                    Vector2 size = new(viewSize / bounds.width, viewSize / bounds.height);
                    map.uvRect = new Rect(centre - size / 2f, size);
                }
            }
            if (playerArrow != null) { playerArrow.localRotation = Quaternion.Euler(0f, 0f, -heading); }
            foreach ((UIIcon icon, Vector3 place) in markers.Values) { if (icon != null) { Place(icon, place); } }
        }

        private void Place(UIIcon icon, Vector3 world) => ((RectTransform)icon.transform).localPosition = MarkerPosition(world, out _);
    }
}

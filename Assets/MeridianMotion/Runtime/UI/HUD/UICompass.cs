using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A compass (ReferenceAssets/OpenTasks.md, H6), from a heading (degrees clockwise from north): a Strip of the points
    /// (N, NE, E...) slid along under its middle so the heading's at the centre, those further than half its width hidden;
    /// or a Rose, its Rose turned so north points north. It knows nothing of the camera or the player: set its Heading.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/HUD/Compass")]
    public class UICompass : MonoBehaviour
    {
        /// <summary>A compass's look.</summary>
        public enum Style { Strip, Rose }

        [Tooltip("A strip of the points, or a turning rose")]
        [SerializeField] private Style style = Style.Strip;
        [Tooltip("The points' labels on the strip, in order from north clockwise (8: N, NE, E... or 4: N, E, S, W)")]
        [SerializeField] private List<TMP_Text> points = new();
        [Tooltip("How many canvas units the strip moves per degree")]
        [Min(0.1f)] [SerializeField] private float unitsPerDegree = 3f;
        [Tooltip("The rose, turned by the heading")]
        [SerializeField] private RectTransform rose;
        [Tooltip("The heading, in degrees clockwise from north")]
        [SerializeField] private float heading;

        /// <summary>A compass's look.</summary>
        public Style CurrentStyle { get => style; set { style = value; Apply(); } }
        /// <summary>The points' labels on the strip.</summary>
        public List<TMP_Text> Points => points;
        /// <summary>How many canvas units the strip moves per degree.</summary>
        public float UnitsPerDegree { get => unitsPerDegree; set { unitsPerDegree = value; Apply(); } }
        /// <summary>The rose.</summary>
        public RectTransform Rose { get => rose; set { rose = value; Apply(); } }

        /// <summary>The heading, in degrees clockwise from north (world +z).</summary>
        public float Heading
        {
            get => heading;
            set
            {
                heading = Mathf.Repeat(value, 360f);
                Apply();
            }
        }

        /// <summary>Where on the strip (canvas units from its middle) a bearing shows, and whether it's within its width.</summary>
        public float StripOffset(float bearing, out bool shown)
        {
            float offset = Mathf.DeltaAngle(heading, bearing) * unitsPerDegree;
            shown = Mathf.Abs(offset) <= ((RectTransform)transform).rect.width / 2f;
            return offset;
        }

        /// <summary>The heading from a forward direction in the world (as a camera's).</summary>
        public static float HeadingOf(Vector3 forward) => Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);

        private void OnValidate() => Apply();

        private void Apply()
        {
            UIUtility.SetActive(rose, style == Style.Rose);
            if (rose != null) { rose.localRotation = Quaternion.Euler(0f, 0f, heading); }
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null) { continue; }
                float offset = StripOffset(i * 360f / points.Count, out bool shown);
                points[i].gameObject.SetActive(style == Style.Strip && shown);
                Vector3 at = points[i].rectTransform.localPosition;
                points[i].rectTransform.localPosition = new Vector3(offset, at.y, at.z);
            }
        }
    }
}

using System;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A ring showing progress (ReferenceAssets/OpenTasks.md, H6): a hold to interact filling, or a cooldown draining. Set
    /// its Fraction, or Hold it (it fills over the seconds given, on the UI's clock, raising Completed when full) and
    /// Release it (it empties). Drain shows 1 - Fraction (a cooldown's time left). It drives its Ring (a RingShape).
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/HUD/Radial Progress")]
    public class UIRadialProgress : MonoBehaviour
    {
        [Tooltip("The ring it fills")]
        [SerializeField] private RingShape ring;
        [Tooltip("Whether it shows what's left (1 - Fraction: a cooldown) rather than what's done")]
        [SerializeField] private bool drain;
        [Tooltip("How far it is (0 to 1)")]
        [Range(0f, 1f)] [SerializeField] private float fraction;

        private float holdSeconds, holdStartedAt, holdFrom;
        private bool holding;

        /// <summary>Raised when a Hold fills it.</summary>
        public event Action Completed;

        /// <summary>The ring it fills.</summary>
        public RingShape Ring { get => ring; set { ring = value; Apply(); } }
        /// <summary>Whether it shows what's left rather than what's done.</summary>
        public bool Drain { get => drain; set { drain = value; Apply(); } }
        /// <summary>Whether a Hold is filling it.</summary>
        public bool Holding => holding;

        /// <summary>How far it is (0 to 1): setting it stops a Hold.</summary>
        public float Fraction
        {
            get => fraction;
            set
            {
                holding = false;
                SetFraction(value);
            }
        }

        /// <summary>Starts filling it, from where it is, to full over seconds (on the UI's clock).</summary>
        public void Hold(float seconds)
        {
            holdSeconds = Mathf.Max(0.0001f, seconds);
            holdFrom = fraction;
            holdStartedAt = UIClock.Now;
            holding = true;
        }

        /// <summary>Stops a Hold and empties it.</summary>
        public void Release() => Fraction = 0f;

        /// <summary>Advances a Hold to now (Update does it each frame; a test can call it).</summary>
        public void Tick()
        {
            if (!holding) { return; }
            SetFraction(holdFrom + (UIClock.Now - holdStartedAt) / holdSeconds);
            if (fraction < 1f) { return; }
            holding = false;
            Completed?.Invoke();
        }

        private void Update() => Tick();

        private void OnValidate() => Apply();

        private void SetFraction(float value)
        {
            fraction = Mathf.Clamp01(value);
            Apply();
        }

        private void Apply()
        {
            if (ring != null) { ring.Fraction = drain ? 1f - fraction : fraction; }
        }
    }
}

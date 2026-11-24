using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// A gate that blocks a shortcut until it's opened from its far side (by its ShortcutTrigger), then moves out of the
    /// way (by Travel, over Open Time, eased) and stays open both ways for the rest of the session: nothing closes it and
    /// nothing saves it. A kinematic Rigidbody moved as a TickedPlatform is: its pose a function of the level's clock
    /// (LevelClock) and the tick it was opened at, set at once before the characters step, and drawn between its last two
    /// physics poses. Its state (the tick it opened) is one of the level's moving parts (WorldStateSnapshot), so a replay's
    /// keyframes put it back.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(TickedPlatform.MoveOrder)]
    [AddComponentMenu("Meridian Motion/Levels/Shortcut Gate")]
    public class ShortcutGate : MonoBehaviour
    {
        [Tooltip("A name for the gate, unique in the level: what its trigger and play logs name it by.")]
        [SerializeField] private string id = "";
        [Tooltip("How far the gate moves to open: meters along its own axes (its rotation applies, its scale doesn't).")]
        [SerializeField] private Vector3 travel = new(0f, -4f, 0f);
        [Tooltip("Seconds the gate takes to open, eased in and out.")]
        [Min(0.05f)] [SerializeField] private float openTime = 1.5f;

        /// <summary>The tick a gate that hasn't opened was opened at: never.</summary>
        public const long NeverOpened = long.MinValue / 4;

        private static readonly List<ShortcutGate> active = new();

        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Vector3 previousPosition, currentPosition;
        // The level's tick it was opened at (NeverOpened while closed).
        private long openedTick = NeverOpened;

        /// <summary>
        /// Raised as a gate opens. Static, not one per gate: a gate's fields are its saved state (WorldStateSnapshot), and
        /// putting them back would put back its listeners as they were when saved too.
        /// </summary>
        public static event Action<ShortcutGate> AnyOpened;

        /// <summary>Every enabled gate.</summary>
        public static IReadOnlyList<ShortcutGate> All => active;
        /// <summary>The gate's name, unique in the level.</summary>
        public string Id { get => id; set => id = value ?? ""; }
        /// <summary>How far it moves to open, along its own axes.</summary>
        public Vector3 Travel { get => travel; set => travel = value; }
        /// <summary>Seconds it takes to open.</summary>
        public float OpenTime { get => openTime; set => openTime = Mathf.Max(0.05f, value); }
        /// <summary>Whether it's been opened (it may still be moving).</summary>
        public bool IsOpen => openedTick != NeverOpened;
        /// <summary>The level's tick it was opened at (NeverOpened while closed).</summary>
        public long OpenedTick => openedTick;

        /// <summary>The enabled gate with this id, or null.</summary>
        public static ShortcutGate Find(string gateId)
        {
            for (int i = 0; i < active.Count; i++) { if (active[i].id == gateId) { return active[i]; } }
            return null;
        }

        /// <summary>Opens every gate in the level (a development shortcut).</summary>
        public static void OpenAll()
        {
            for (int i = 0; i < active.Count; i++) { active[i].Open(); }
        }

        /// <summary>Opens the gate (from the current tick), if it isn't already: it moves out of the way and stays open.</summary>
        public void Open()
        {
            if (IsOpen) { return; }
            openedTick = LevelClock.Tick;
            AnyOpened?.Invoke(this);
        }

        /// <summary>Where the gate is at a tick of the level's clock: closed until it opened, then moving by Travel over Open Time.</summary>
        public Vector3 PositionAt(long tick)
        {
            if (!IsOpen) { return startPosition; }
            float t = Mathf.Clamp01((float)(LevelClock.SecondsAt(tick - openedTick) / openTime));
            return startPosition + startRotation * travel * Mathf.SmoothStep(0f, 1f, t);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            // Drawn between its physics poses here (see Update), as a TickedPlatform is.
            body.interpolation = RigidbodyInterpolation.None;
            startPosition = previousPosition = currentPosition = body.position;
            startRotation = body.rotation;
        }

        private void OnEnable()
        {
            if (!active.Contains(this)) { active.Add(this); }
        }

        private void OnDisable()
        {
            active.Remove(this);
            if (body != null) { transform.position = currentPosition; }
        }

        // Moved to where it is at this step at once, before the characters step (see TickedPlatform.FixedUpdate for why).
        private void FixedUpdate()
        {
            previousPosition = currentPosition;
            currentPosition = PositionAt(LevelClock.Tick);
            if (currentPosition == previousPosition && body.position == currentPosition) { return; }
            body.position = currentPosition;
            transform.position = currentPosition;
        }

        private void Update()
        {
            if (previousPosition == currentPosition) { return; }
            float t = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            transform.position = Vector3.Lerp(previousPosition, currentPosition, t);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 start = Application.isPlaying ? startPosition : transform.position;
            Quaternion rotation = Application.isPlaying ? startRotation : transform.rotation;
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawLine(start, start + rotation * travel);
        }
    }
}

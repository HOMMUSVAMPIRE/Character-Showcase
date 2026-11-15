using System;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// The component that drives a character by delegating different categories of functionality to different components which this manages.
    /// </summary>
    public class ACCharacterController : MonoBehaviour
    {
        [Header("Subcomponent references")]
        [Tooltip("The component responsible for character movement")]
        [SerializeField] private MovementController movementController;
        [Tooltip("The component responsible for updating character animations and visuals")]
        [SerializeField] private AnimationController animationController;
        [Tooltip("The component that holds the character's model, whose model parent is scaled to resize the character. Found automatically if left empty.")]
        [SerializeField] private CharacterModelHost modelHost;

        [Header("Size")]
        [Tooltip("The character's scale: x is width, y height and z depth, relative to the character as authored. Change it " +
                 "while playing to resize the character (it goes through SetScale). Only the model is scaled, never the " +
                 "character's own transform; collision, movement, animation and the camera follow.")]
        [SerializeField] private Vector3 scale = Vector3.one;
        [Tooltip("Seconds a change of scale takes. Growing pauses whenever there isn't room (under a low ceiling, say) and " +
                 "carries on once there is. 0 changes size at once, when there's room.")]
        [Min(0f)] [SerializeField] private float scaleChangeDuration = 0.25f;

        // The smallest scale on any axis; anything smaller would make the collider degenerate.
        private const float MinimumScale = 0.05f;

        // The size change in progress: from startScale to targetScale, progress 0 to 1, in log space so growing and
        // shrinking by the same factor take the same time and look alike.
        private Vector3 currentScale = Vector3.one;
        private Vector3 startScale = Vector3.one;
        private Vector3 targetScale = Vector3.one;
        private float scaleProgress = 1f;
        private float baseMass = -1f;
        private Rigidbody body;

        /// <summary>The character's scale right now (it changes over scaleChangeDuration after SetScale).</summary>
        public Vector3 Scale => currentScale;
        /// <summary>The scale the character is changing to: the last one passed to SetScale.</summary>
        public Vector3 TargetScale => targetScale;
        /// <summary>Whether the character is still changing size, including while waiting for room to grow.</summary>
        public bool IsResizing => scaleProgress < 1f;
        /// <summary>Raised whenever the character's scale changes, with the new scale.</summary>
        public event Action<Vector3> ScaleChanged;

        private void Awake()
        {
            if (modelHost == null) { modelHost = GetComponent<CharacterModelHost>(); }
            if (movementController == null) { movementController = GetComponent<MovementController>(); }
            body = GetComponent<Rigidbody>();
            if (body != null) { baseMass = body.mass; }
            CheckRootScale();
        }

        private void Start()
        {
            // A scale set in the inspector before playing applies from the start.
            if (scale != Vector3.one && !SetScale(scale, immediately: true)) { scale = Vector3.one; }
        }

        private void OnValidate()
        {
            // Editing Scale in the inspector while playing resizes the character. A scale that's refused (a negative
            // one, say) is put back in the field, so it shows the scale the character is actually changing to.
            if (Application.isPlaying && body != null && scale != targetScale && !SetScale(scale)) { scale = targetScale; }
        }

        /// <summary>
        /// Resizes the character to the given scale (x width, y height, z depth, relative to as authored; any mix of
        /// values works). The scale goes on the model parent, not on the character's own transform, so the character's
        /// physics and movement code never see a scaled transform; instead:
        /// - the collider is resized: height with y, radius with the larger of x and z (capped at half the height)
        /// - movement scales with size as the MovementController's size setting says (speeds, jump, step height)
        /// - the mass scales with volume, and animation, foot placement, the eye point and the camera follow
        /// The change takes scaleChangeDuration, unless immediately is set. Growing waits while there isn't room.
        ///
        /// Every component must be positive: a negative (mirrored), zero (flattened) or non-finite scale is refused with
        /// an error, leaving the character as it was, and this returns false. Positive values smaller than
        /// MinimumScale are raised to it, since a smaller collider would be degenerate.
        /// </summary>
        public bool SetScale(Vector3 newScale, bool immediately = false)
        {
            if (!IsValidScale(newScale))
            {
                Debug.LogError($"[{nameof(ACCharacterController)}] Can't set '{name}' to scale {newScale}: every component " +
                               "must be positive and finite. The scale wasn't changed.", this);
                return false;
            }
            if (newScale.x < MinimumScale || newScale.y < MinimumScale || newScale.z < MinimumScale)
            {
                Debug.LogWarning($"[{nameof(ACCharacterController)}] Scale {newScale} is smaller than the minimum of " +
                                 $"{MinimumScale} on some axis; those axes are set to {MinimumScale}.", this);
                newScale = Vector3.Max(newScale, Vector3.one * MinimumScale);
            }
            scale = newScale;
            startScale = currentScale;
            targetScale = newScale;
            scaleProgress = startScale == targetScale ? 1f : 0f;
            if (immediately || scaleChangeDuration <= 0f) { UpdateScale(float.PositiveInfinity); }
            return true;
        }

        /// <summary>
        /// Advances a change of size by deltaTime. Called every physics step (before movement); call it yourself if this
        /// component is disabled and something else steps the character's movement.
        /// </summary>
        public void UpdateScale(float deltaTime)
        {
            CheckRootScale();
            if (scaleProgress >= 1f) { return; }
            float next = scaleChangeDuration > 0f ? Mathf.Min(1f, scaleProgress + deltaTime / scaleChangeDuration) : 1f;
            Vector3 candidate = next >= 1f ? targetScale : LogLerp(startScale, targetScale, next);
            // Growing into the world can't be undone by pushing the character out (squeezed under a low ceiling, it
            // would be pushed through the floor), so wait for room. Shrinking always fits.
            if (Grows(candidate) && movementController != null && !movementController.CanFit(candidate)) { return; }
            scaleProgress = next;
            ApplyScale(candidate);
        }

        private void ApplyScale(Vector3 newScale)
        {
            currentScale = newScale;
            if (movementController != null) { movementController.ApplyScale(newScale); }
            // Mass grows with volume, for anything that pushes or is pushed by the character.
            if (body != null && baseMass > 0f) { body.mass = baseMass * newScale.x * newScale.y * newScale.z; }

            Transform modelParent = modelHost != null ? modelHost.ModelParent : null;
            if (modelParent != null && modelParent != transform) { modelParent.localScale = newScale; }
            else { Debug.LogWarning($"[{nameof(ACCharacterController)}] '{name}' has no model parent separate from itself to scale.", this); }
            if (modelHost != null) { modelHost.RefreshScale(); }
            ScaleChanged?.Invoke(newScale);
        }

        /// <summary>Whether the collider at the given scale reaches further than it does now, anywhere.</summary>
        private bool Grows(Vector3 candidate) =>
            candidate.y > currentScale.y + 1e-5f || Mathf.Max(candidate.x, candidate.z) > Mathf.Max(currentScale.x, currentScale.z) + 1e-5f;

        private static Vector3 LogLerp(Vector3 from, Vector3 to, float t) => new(
            Mathf.Exp(Mathf.Lerp(Mathf.Log(from.x), Mathf.Log(to.x), t)),
            Mathf.Exp(Mathf.Lerp(Mathf.Log(from.y), Mathf.Log(to.y), t)),
            Mathf.Exp(Mathf.Lerp(Mathf.Log(from.z), Mathf.Log(to.z), t)));

        private static bool IsValidScale(Vector3 value)
        {
            static bool Axis(float v) => v > 0f && !float.IsNaN(v) && !float.IsInfinity(v);
            return Axis(value.x) && Axis(value.y) && Axis(value.z);
        }

        // The last wrong root scale reported, so a scaled root is reported once, not every physics step.
        private Vector3 reportedRootScale = Vector3.one;

        /// <summary>
        /// The character's own transform must never be scaled (by itself or by a parent): the character is resized only
        /// through SetScale, which scales the model parent, and movement and collision rely on that. A scaled root is
        /// reported as an error each time its scale changes.
        /// </summary>
        private void CheckRootScale()
        {
            Vector3 rootScale = transform.lossyScale;
            if (rootScale == Vector3.one)
            {
                reportedRootScale = Vector3.one; // Fixed: report it again if it's ever scaled again.
                return;
            }
            if (rootScale == reportedRootScale) { return; }
            reportedRootScale = rootScale;
            Debug.LogError($"[{nameof(ACCharacterController)}] '{name}' has its own transform scaled to {rootScale}. Only " +
                           $"{nameof(SetScale)} may resize a character (it scales the model parent); a scaled root breaks " +
                           "its collision and movement.", this);
        }

        private void Update()
        {
            // TODO: collect inputs if not driven by the player

            // Visual-only smoothing runs at render rate. Doing it in FixedUpdate makes the model visibly step at the
            // physics rate, even with Rigidbody interpolation enabled.
            if (animationController != null)
            {
                // Rotate the character's visual representation smoothly to avoid unnatural "snapping"
                Quaternion groundRotation = movementController != null ? movementController.ConsumeGroundRotation() : Quaternion.identity;
                animationController.SmoothRotateVisuals(Time.deltaTime, groundRotation);
                // Drive the character's animations from the most recently completed movement step
                animationController.UpdateAnimator(movementController != null ? movementController.lastFrameMovementData : null, Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            // Calculate and apply movement to the character using input data
            UpdateScale(Time.fixedDeltaTime);
            if (movementController != null)
            {
                movementController.ProcessMovementInputs(ACInputManager.instance.inputs);

                movementController.MovementCleanup();
            }
        }

        private void LateUpdate()
        {
            // TODO: do any end-of frame cleanup and store data to be referenced in future frames
        }

        public void Setup()
        {
            // TODO: setup subcomponents as needed
        }

        /// <summary>
        /// This should be called by a future game manager script at the start of the frame on all active character objects to cache data about the character such as the character's bounds
        /// </summary>
        public void UpdateCaches()
        {
            
        }

    }
}

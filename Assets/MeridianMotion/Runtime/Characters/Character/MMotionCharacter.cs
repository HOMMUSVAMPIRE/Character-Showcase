using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// The component that drives a character by delegating different categories of functionality to different components which this manages.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Characters/MMotion Character")]
    public class MMotionCharacter : MonoBehaviour
    {
        [Header("Subcomponent references")]
        [Tooltip("The component responsible for character movement")]
        [SerializeField] [FormerlySerializedAs("movementController")] private MMotionMotor motor;
        [Tooltip("The component responsible for updating character animations and visuals")]
        [SerializeField] [FormerlySerializedAs("animationController")] private MMotionAnimator animator;
        [Tooltip("The component that holds the character's model, whose model parent is scaled to resize the character. Found automatically if left empty.")]
        [SerializeField] private CharacterModelSlot modelHost;

        [Header("Owner and Input")]
        [Tooltip("Who the character is for. Local Player: followed by the camera when spawned, and moved by the player's " +
                 "device unless given another input source. AI: moved by its input source, standing idle without one. " +
                 "Remote: moved by its input source; without one it isn't stepped at all (something else places it, as " +
                 "another machine's character is). Set by CharacterSpawner when it spawns one.")]
        [SerializeField] private ControlledBy owner = ControlledBy.LocalPlayer;
        [Tooltip("Optional: what drives the character's input (a ScriptedInputSource, a WaypointInputSource). Empty: the " +
                 "one on the character if it has one, else its owner's (the player's device for the local player's).")]
        [SerializeField] private CharacterInputSource inputSource;

        [Header("Size")]
        [Tooltip("The character's scale: x is width, y height and z depth, relative to the character as authored. Change it " +
                 "while playing to resize the character (it goes through SetScale). Only the model is scaled, never the " +
                 "character's own transform; collision, movement, animation and the camera follow.")]
        [SerializeField] [FormerlySerializedAs("scale")] private Vector3 requestedScale = Vector3.one;
        [Tooltip("Seconds a change of scale takes. Growing pauses whenever there isn't room (under a low ceiling, say) and " +
                 "carries on once there is. 0 changes size at once, when there's room.")]
        [Min(0f)] [SerializeField] private float scaleChangeDuration = 0.25f;

        // The smallest scale on any axis; anything smaller would make the body degenerate.
        private const float MinimumScale = 0.05f;

        // The size change in progress: from startScale to targetScale, progress 0 to 1, in log space so growing and
        // shrinking by the same factor take the same time and look alike.
        private Vector3 currentScale = Vector3.one;
        private Vector3 startScale = Vector3.one;
        private Vector3 targetScale = Vector3.one;
        private float scaleProgress = 1f;
        private float baseMass = -1f;
        private Rigidbody body;

        /// <summary>Who the character is for: only the local player's is moved by the player's device. See owner.</summary>
        public ControlledBy ControlledBy { get => owner; set => owner = value; }

        // A source given from code that isn't a component (a test's script, a network feed).
        private ICharacterInputSource givenSource;

        /// <summary>
        /// What drives the character's input, if it's been given something (a component on it, or any source set from
        /// code); null for its owner's (see ActiveInputSource). Set null to go back to the owner's.
        /// </summary>
        public ICharacterInputSource InputSource
        {
            get => givenSource ?? (inputSource != null ? inputSource : null);
            set
            {
                inputSource = value as CharacterInputSource;
                givenSource = value is CharacterInputSource ? null : value;
            }
        }

        /// <summary>
        /// What drives the character now: its input source, else its owner's (the player's device for the local player's;
        /// none for an AI's or a remote one: see owner).
        /// </summary>
        public ICharacterInputSource ActiveInputSource => InputSource ?? (owner == ControlledBy.LocalPlayer ? PlayerInputSource.Instance : null);

        /// <summary>The character's scale right now (it changes over scaleChangeDuration after SetScale).</summary>
        public Vector3 CurrentScale => currentScale;
        /// <summary>The scale the character is changing to: the last one passed to SetScale.</summary>
        public Vector3 TargetScale => targetScale;
        /// <summary>Whether the character is still changing size, including while waiting for room to grow.</summary>
        public bool IsResizing => scaleProgress < 1f;
        /// <summary>Raised whenever the character's scale changes, with the new scale.</summary>
        public event Action<Vector3> ScaleChanged;

        /// <summary>What's told of each step before it's taken: the character and the input frame it's about to step with.</summary>
        public delegate void StepHandler(MMotionCharacter character, in CharacterInputFrame frame);

        /// <summary>
        /// Raised each step just before the movement steps, with the frame it's given (tagged with its tick): between
        /// steps, so a listener may save the character's state (StateSerializer) or put a saved one back (a replay's
        /// keyframe). What records a play session listens here (ReplayRecorder).
        /// </summary>
        public event StepHandler BeforeStep;

        /// <summary>The character's movement.</summary>
        public MMotionMotor Motor => motor;

        // Records the frames the character is given, if it has one (see CharacterInputRecorder).
        private CharacterInputRecorder recorder;

        private void Awake()
        {
            if (modelHost == null) { modelHost = GetComponent<CharacterModelSlot>(); }
            if (motor == null) { motor = GetComponent<MMotionMotor>(); }
            body = GetComponent<Rigidbody>();
            recorder = GetComponent<CharacterInputRecorder>();
            if (inputSource == null) { inputSource = GetComponent<CharacterInputSource>(); }
            if (body != null) { baseMass = body.mass; }
            CheckRootScale();
        }

        private void Start()
        {
            // A scale set in the inspector before playing applies from the start.
            if (requestedScale != Vector3.one && !SetScale(requestedScale, immediately: true)) { requestedScale = Vector3.one; }
        }

        private void OnValidate()
        {
            // Editing Scale in the inspector while playing resizes the character. A scale that's refused (a negative
            // one, say) is put back in the field, so it shows the scale the character is actually changing to.
            if (Application.isPlaying && body != null && requestedScale != targetScale && !SetScale(requestedScale)) { requestedScale = targetScale; }
        }

        /// <summary>
        /// Resizes the character to the given scale (x width, y height, z depth, relative to as authored; any mix of
        /// values works). The scale goes on the model parent, not on the character's own transform, so the character's
        /// physics and movement code never see a scaled transform; instead:
        /// - the body is resized with it (MMotionMotor.ApplyScale), which collision and the abilities measure
        /// - movement scales with size as the MMotionMotor's size setting says (speeds, jump, step height)
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
                Debug.LogError($"[{nameof(MMotionCharacter)}] Can't set '{name}' to scale {newScale}: every component " +
                               "must be positive and finite. The scale wasn't changed.", this);
                return false;
            }
            if (newScale.x < MinimumScale || newScale.y < MinimumScale || newScale.z < MinimumScale)
            {
                Debug.LogWarning($"[{nameof(MMotionCharacter)}] Scale {newScale} is smaller than the minimum of " +
                                 $"{MinimumScale} on some axis; those axes are set to {MinimumScale}.", this);
                newScale = Vector3.Max(newScale, Vector3.one * MinimumScale);
            }
            requestedScale = newScale;
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
            if (Grows(candidate) && motor != null && !motor.CanFit(candidate)) { return; }
            scaleProgress = next;
            ApplyScale(candidate);
        }

        private void ApplyScale(Vector3 newScale)
        {
            currentScale = newScale;
            if (motor != null) { motor.ApplyScale(newScale); }
            // Mass grows with volume, for anything that pushes or is pushed by the character.
            if (body != null && baseMass > 0f) { body.mass = baseMass * newScale.x * newScale.y * newScale.z; }

            Transform modelParent = modelHost != null ? modelHost.ModelParent : null;
            if (modelParent != null && modelParent != transform) { modelParent.localScale = newScale; }
            else { Debug.LogWarning($"[{nameof(MMotionCharacter)}] '{name}' has no model parent separate from itself to scale.", this); }
            if (modelHost != null) { modelHost.RefreshScale(); }
            ScaleChanged?.Invoke(newScale);
        }

        /// <summary>Whether the body at the given scale reaches further than it does now, anywhere.</summary>
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
            Debug.LogError($"[{nameof(MMotionCharacter)}] '{name}' has its own transform scaled to {rootScale}. Only " +
                           $"{nameof(SetScale)} may resize a character (it scales the model parent); a scaled root breaks " +
                           "its collision and movement.", this);
        }

        private void Update()
        {
            // Visual-only smoothing runs at render rate. Doing it in FixedUpdate makes the model visibly step at the
            // physics rate, even with Rigidbody interpolation enabled.
            if (animator != null)
            {
                // Rotate the character's visual representation smoothly to avoid unnatural "snapping"
                Quaternion groundRotation = motor != null ? motor.ConsumeGroundRotation() : Quaternion.identity;
                animator.SmoothRotateVisuals(Time.deltaTime, groundRotation);
                // Drive the character's animations from the most recently completed movement step
                animator.UpdateAnimator(motor != null ? motor.lastStep : null, Time.deltaTime);
            }
        }

        private void FixedUpdate() => StepMovement(Time.fixedDeltaTime);

        /// <summary>
        /// One physics step of the character, as its FixedUpdate takes it: any change of size, then its movement, given its
        /// input source's frame for the step (ActiveInputSource; an idle frame without one). A remote character with no
        /// source isn't moved: something else places it. Called by FixedUpdate; call it yourself to step a character that
        /// isn't (the tests' rig does, with the controller turned off).
        /// </summary>
        public void StepMovement(float deltaTime)
        {
            UpdateScale(deltaTime);
            if (motor == null) { return; }
            ICharacterInputSource source = ActiveInputSource;
            if (source == null && owner == ControlledBy.Remote) { return; }
            uint tick = motor.InputTick;
            CharacterInputFrame frame = source != null ? source.FrameFor(tick, motor) : default;
            // Tagged with the step it's for, whatever its source tagged it.
            frame.tick = tick;
            if (recorder != null && recorder.isActiveAndEnabled) { recorder.Record(frame); }
            BeforeStep?.Invoke(this, frame);
            motor.Step(frame, deltaTime);
            motor.FinishStep();
        }
    }
}

using UnityEngine;

namespace Level
{
    /// <summary>
    /// A gate on a timed course: a trigger volume that tells its CourseTimer when something passes into it (the object
    /// a Rigidbody carries, or the collider's own). Its collider is made a trigger, so movement passes through it (the
    /// character's collision sweeps ignore triggers). Something with a Rigidbody must pass for the trigger to see it, as
    /// a character does.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class TimingGate : MonoBehaviour
    {
        [Tooltip("The course this gate times.")]
        [SerializeField] private CourseTimer timer;
        [Tooltip("What passing this gate does: starts and ends laps, starts a run, or ends one.")]
        [SerializeField] private TimingGateRole role = TimingGateRole.StartAndFinish;

        /// <summary>The course this gate times.</summary>
        public CourseTimer Timer { get => timer; set => timer = value; }
        /// <summary>What passing this gate does.</summary>
        public TimingGateRole Role { get => role; set => role = value; }

        private void Awake() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (timer == null) { return; }
            timer.Pass(role, other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform);
        }
    }
}

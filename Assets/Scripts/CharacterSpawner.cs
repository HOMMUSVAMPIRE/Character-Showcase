using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace Character
{
    /// <summary>A place a character can spawn: a world position (the character's feet) and the way it faces.</summary>
    [Serializable]
    public struct SpawnPoint
    {
        [Tooltip("Where the character's feet go, in world space.")]
        public Vector3 position;
        [Tooltip("Which way the character faces when it spawns.")]
        public Quaternion rotation;

        public SpawnPoint(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }

        /// <summary>
        /// The rotation, normalized. A spawn point added in the inspector to an empty list starts with an all-zero
        /// rotation, which isn't a rotation at all (spawning with it gives the character NaNs), so that's treated as
        /// no rotation.
        /// </summary>
        public Quaternion Rotation
        {
            get
            {
                float sqrMagnitude = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
                if (sqrMagnitude < 1e-6f || float.IsNaN(sqrMagnitude) || float.IsInfinity(sqrMagnitude)) { return Quaternion.identity; }
                return Quaternion.Normalize(rotation);
            }
        }
    }

    /// <summary>
    /// Spawns a character into the scene at one of a list of spawn points, chosen at random, so a scene doesn't need
    /// the character placed in it beforehand. It can spawn the character on Start, or wait for Spawn to be called.
    /// Whatever needs the spawned character (a camera to follow it, say) can listen for it through the
    /// CharacterSpawned event or the onCharacterSpawned UnityEvent, which can be wired up in the inspector.
    /// </summary>
    // Early, so a character spawned on Start is there before other scripts' Start runs: a camera's Start then
    // starts out behind it, facing its way, rather than warning that it has no target.
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class CharacterSpawner : MonoBehaviour
    {
        [Header("Character")]
        [Tooltip("The character prefab to spawn (Assets/Prefabs/Character.prefab, say).")]
        [SerializeField] private GameObject characterPrefab;
        [Tooltip("Optional: the model the spawned character uses, instead of the prefab's own. Leave empty to keep " +
                 "the prefab's.")]
        [SerializeField] private CharacterModelSource modelSource;
        [Tooltip("Optional: the movement mode asset the spawned character moves with, instead of the prefab's own. " +
                 "Leave empty to keep the prefab's.")]
        [SerializeField] private MovementMode movementMode;

        [Header("Spawn Points")]
        [Tooltip("The places the character can spawn, in world space. One is chosen at random each time it spawns. " +
                 "They're drawn in the Scene view as capsules with an arrow for the way the character faces.")]
        [SerializeField] private List<SpawnPoint> spawnPoints = new() { new SpawnPoint(Vector3.zero, Quaternion.identity) };

        [Header("Spawning")]
        [Tooltip("Spawn the character automatically when the scene starts. Turn this off to spawn it from code, " +
                 "by calling Spawn.")]
        [SerializeField] private bool spawnOnStart = true;
        [Tooltip("Called with the spawned character's root transform after each spawn. Use it to connect things that " +
                 "need the character, such as a camera to follow it.")]
        [SerializeField] private UnityEvent<Transform> onCharacterSpawned = new();
        [Tooltip("Called after each spawn with what a camera needs to follow the character: its root transform, its eye " +
                 "point (for a first-person view) and its size reference (its model parent, whose scale is its size), " +
                 "in that order. Wire it to CameraRig.Follow. The eye point or size reference is empty if the character " +
                 "has none.")]
        [SerializeField] private UnityEvent<Transform, Transform, Transform> onCameraTargetSpawned = new();

        [Header("Scene View")]
        [Tooltip("The height of the capsule drawn at each spawn point in the Scene view, in meters.")]
        [Min(0.1f)] [SerializeField] private float gizmoHeight = 1.8f;
        [Tooltip("The radius of the capsule drawn at each spawn point in the Scene view, in meters.")]
        [Min(0.05f)] [SerializeField] private float gizmoRadius = 0.3f;

        /// <summary>Raised after each spawn, with the spawned character.</summary>
        public event Action<GameObject> CharacterSpawned;

        /// <summary>The character spawned most recently, or null if none has been (or it's been destroyed).</summary>
        public GameObject Character { get; private set; }

        /// <summary>The spawn point the most recent character was spawned at, or -1 if none has been.</summary>
        public int LastSpawnIndex { get; private set; } = -1;

        public GameObject CharacterPrefab { get => characterPrefab; set => characterPrefab = value; }

        /// <summary>The model spawned characters use instead of the prefab's own, or null to keep the prefab's.</summary>
        public CharacterModelSource ModelSource { get => modelSource; set => modelSource = value; }

        /// <summary>The movement mode spawned characters use instead of the prefab's own, or null to keep the prefab's.</summary>
        public MovementMode MovementMode { get => movementMode; set => movementMode = value; }

        /// <summary>The spawn points. Change the list to add, move or remove them.</summary>
        public List<SpawnPoint> SpawnPoints => spawnPoints;

        public bool SpawnOnStart { get => spawnOnStart; set => spawnOnStart = value; }

        /// <summary>The inspector's spawn event, called with each spawned character's root transform.</summary>
        public UnityEvent<Transform> OnCharacterSpawned => onCharacterSpawned;

        /// <summary>The inspector's camera event, called with each spawned character's root, eye point and size reference.</summary>
        public UnityEvent<Transform, Transform, Transform> OnCameraTargetSpawned => onCameraTargetSpawned;

        private void Start()
        {
            if (spawnOnStart) { Spawn(); }
        }

        /// <summary>
        /// Spawns a new character at a spawn point chosen at random, and returns it. Returns null, with an error, if
        /// there's no prefab or no spawn points. Each call spawns another character: earlier ones are left as they are.
        /// </summary>
        public GameObject Spawn()
        {
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                Debug.LogError($"[{nameof(CharacterSpawner)}] '{name}' has no spawn points, so no character was spawned.", this);
                return null;
            }
            return SpawnAt(Random.Range(0, spawnPoints.Count));
        }

        /// <summary>
        /// Spawns a new character at the given spawn point, and returns it. Returns null, with an error, if there's
        /// no prefab or the index is out of range.
        /// </summary>
        public GameObject SpawnAt(int index)
        {
            if (characterPrefab == null)
            {
                Debug.LogError($"[{nameof(CharacterSpawner)}] '{name}' has no character prefab, so no character was spawned.", this);
                return null;
            }
            if (spawnPoints == null || index < 0 || index >= spawnPoints.Count)
            {
                Debug.LogError($"[{nameof(CharacterSpawner)}] '{name}' has no spawn point {index} (it has " +
                               $"{spawnPoints?.Count ?? 0}), so no character was spawned.", this);
                return null;
            }

            SpawnPoint point = spawnPoints[index];
            // Instantiating at the pose (rather than moving the character afterwards) means the Rigidbody starts there
            // too, so the character's first physics step, and its interpolation, start from the spawn point.
            GameObject character = Instantiate(characterPrefab, point.position, point.Rotation);
            character.name = characterPrefab.name;
            // Overrides go on before the character's Start, which is when it loads its model (Awake has already run,
            // during Instantiate, but doesn't load or move anything).
            if (movementMode != null && character.TryGetComponent(out MovementController movement)) { movement.movementHandler = movementMode; }
            character.TryGetComponent(out CharacterModelHost host);
            if (modelSource != null && host != null) { host.SetModelSourceBeforeStart(modelSource); }
            Character = character;
            LastSpawnIndex = index;

            CharacterSpawned?.Invoke(character);
            onCharacterSpawned?.Invoke(character.transform);
            // The model parent's scale is the character's size (see ACCharacterController.SetScale), unless the model
            // goes straight under the root, which is never scaled.
            Transform eyePoint = host != null ? host.EyePoint : null;
            Transform sizeReference = host != null && host.ModelParent != character.transform ? host.ModelParent : null;
            onCameraTargetSpawned?.Invoke(character.transform, eyePoint, sizeReference);
            return character;
        }

        private void OnDrawGizmos()
        {
            if (spawnPoints == null) { return; }
            float radius = Mathf.Min(gizmoRadius, gizmoHeight / 2f);
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                SpawnPoint point = spawnPoints[i];
                Quaternion rotation = point.Rotation;
                Vector3 up = rotation * Vector3.up;
                Vector3 forward = rotation * Vector3.forward;
                Vector3 bottom = point.position + up * radius;
                Vector3 top = point.position + up * (gizmoHeight - radius);

                Gizmos.color = new Color(0.2f, 0.9f, 0.4f);
                Gizmos.DrawWireSphere(bottom, radius);
                Gizmos.DrawWireSphere(top, radius);
                Vector3 right = rotation * Vector3.right * radius;
                Vector3 front = forward * radius;
                Gizmos.DrawLine(bottom + right, top + right);
                Gizmos.DrawLine(bottom - right, top - right);
                Gizmos.DrawLine(bottom + front, top + front);
                Gizmos.DrawLine(bottom - front, top - front);

                // An arrow at about chest height for the way the character faces.
                Vector3 arrowStart = point.position + up * (gizmoHeight * 0.6f);
                Vector3 arrowEnd = arrowStart + forward * (radius * 3f);
                Gizmos.color = new Color(0.2f, 0.6f, 1f);
                Gizmos.DrawLine(arrowStart, arrowEnd);
                Gizmos.DrawLine(arrowEnd, arrowEnd - forward * radius + right);
                Gizmos.DrawLine(arrowEnd, arrowEnd - forward * radius - right);
            }
        }
    }
}

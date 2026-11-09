using Character;
using Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Builds a small obstacle course in the open scene for testing foot placement and moving ground: stairs, a gentle
    /// and a steep ramp, scattered low bumps, and moving platforms (a slider, an elevator and a spinner). Sized to stay
    /// within the movement controller's step height (0.35 m) and slope limit (55°).
    /// </summary>
    public static class FootPlacementTestArea
    {
        private const string RootName = "Foot Placement Test Area";

        [MenuItem("Tools/Character Showcase/Create Foot Placement Test Area")]
        public static void Create()
        {
            // Rebuild rather than keep an existing course, so it always matches this code (undoable with Ctrl/Cmd+Z).
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log($"[FootPlacementTestArea] Replacing the existing '{RootName}'.");
            }

            // Place the course a few meters in front of the character, on its ground level.
            var character = Object.FindAnyObjectByType<ACCharacterController>();
            Vector3 origin = Vector3.zero;
            Quaternion facing = Quaternion.identity;
            if (character != null)
            {
                Transform t = character.transform;
                Vector3 forward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 1e-4f) { forward = Vector3.forward; }
                facing = Quaternion.LookRotation(forward, Vector3.up);
                origin = t.position + forward * 4f;
            }

            var root = new GameObject(RootName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create Foot Placement Test Area");
            root.SetPositionAndRotation(origin, facing);

            // Stairs: 6 steps of 0.2 m rise and 0.35 m run, ending in a landing.
            const float rise = 0.2f, run = 0.35f, width = 2f;
            for (int i = 0; i < 6; i++)
            {
                float height = rise * (i + 1);
                Box(root, $"Step {i + 1}", new Vector3(-3f, height / 2f, i * run + run / 2f), new Vector3(width, height, run));
            }
            Box(root, "Stairs Landing", new Vector3(-3f, rise * 3f, 6 * run + 1f), new Vector3(width, rise * 6f, 2f));

            // Ramps: 20° and 40°.
            Ramp(root, "Gentle Ramp (20°)", new Vector3(0f, 0f, 0f), 20f, 4f, width);
            Ramp(root, "Steep Ramp (40°)", new Vector3(3f, 0f, 0f), 40f, 3f, width);

            // Low bumps of varied height, slightly rotated so the feet land on uneven, tilted surfaces.
            var random = new System.Random(7);
            for (int i = 0; i < 10; i++)
            {
                float x = 6f + (float)random.NextDouble() * 3f;
                float z = (float)random.NextDouble() * 5f;
                float h = 0.05f + (float)random.NextDouble() * 0.12f;
                Transform bump = Box(root, $"Bump {i + 1}", new Vector3(x, h / 2f, z), new Vector3(0.6f, h, 0.6f));
                bump.localRotation = Quaternion.Euler((float)random.NextDouble() * 10f - 5f, (float)random.NextDouble() * 90f,
                    (float)random.NextDouble() * 10f - 5f);
            }

            // Moving platforms. Each top surface starts 0.3 m up, so the character can step straight onto it.
            Platform(root, "Sliding Platform", new Vector3(-3.5f, 0.15f, 9f), new Vector3(2.5f, 0.3f, 2.5f),
                travel: new Vector3(0f, 0f, 5f), spin: Vector3.zero);
            Platform(root, "Elevator", new Vector3(0f, 0.15f, 9f), new Vector3(2f, 0.3f, 2f),
                travel: new Vector3(0f, 3f, 0f), spin: Vector3.zero);
            Box(root, "Elevator Top Ledge", new Vector3(0f, 1.65f, 11f), new Vector3(2f, 3.3f, 2f));
            Platform(root, "Spinning Platform", new Vector3(4f, 0.15f, 9f), new Vector3(3f, 0.3f, 3f),
                travel: Vector3.zero, spin: new Vector3(0f, 45f, 0f));

            Selection.activeGameObject = root.gameObject;
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log($"[FootPlacementTestArea] Created '{RootName}' at {origin}. Save the scene to keep it.");
        }

        private static Transform Box(Transform parent, string name, Vector3 localPosition, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(box, "Create Foot Placement Test Area");
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;
            return box.transform;
        }

        private static void Platform(Transform parent, string name, Vector3 localPosition, Vector3 size, Vector3 travel, Vector3 spin)
        {
            Transform platform = Box(parent, name, localPosition, size);
            platform.gameObject.AddComponent<Rigidbody>();
            var mover = platform.gameObject.AddComponent<MovingPlatform>();
            mover.travel = travel; // Meters along the platform's own axes (its scale doesn't apply).
            mover.spinDegreesPerSecond = spin;
        }

        /// <summary>A slab tilted to the given angle, rising away from the character, with a platform at the top.</summary>
        private static void Ramp(Transform parent, string name, Vector3 localStart, float angle, float length, float width)
        {
            const float thickness = 0.2f;
            float radians = angle * Mathf.Deg2Rad;
            float height = Mathf.Sin(radians) * length;
            float depth = Mathf.Cos(radians) * length;

            Transform slab = Box(parent, name, Vector3.zero, new Vector3(width, thickness, length));
            slab.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            // Position the slab so its top surface starts at ground level at localStart and rises along +Z.
            Vector3 up = slab.localRotation * Vector3.up;
            Vector3 along = slab.localRotation * Vector3.forward;
            slab.localPosition = localStart + along * (length / 2f) - up * (thickness / 2f);

            Box(parent, $"{name} Top", localStart + new Vector3(0f, height / 2f, depth + 1f), new Vector3(width, height, 2f));
        }
    }
}

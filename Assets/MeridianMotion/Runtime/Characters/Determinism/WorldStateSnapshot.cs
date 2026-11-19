using System.Collections.Generic;
using MeridianMotion.Levels;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// The level's moving parts' own state, saved (Save) to be put back later (Restore), in this process: each rope's (how
    /// it swings, who holds it) and each pushable object's (where it is, its fall, who holds it), with their poses. With a
    /// character's MovementStateSnapshot and the level's clock (LevelClock, which places the platforms), everything a
    /// character's next steps depend on, for rolling the world back to a step and stepping it forward again. What crosses
    /// a network is StateSerializer.WriteWorld.
    /// </summary>
    public sealed class WorldStateSnapshot
    {
        private readonly List<(MonoBehaviour part, ObjectGraphSnapshot state, Vector3 position, Quaternion rotation)> parts = new();

        /// <summary>The level's ropes and pushable objects, in their StableId order (so every machine lists them alike).</summary>
        public static List<MonoBehaviour> MovingParts()
        {
            var found = new List<MonoBehaviour>();
            foreach (SwingRope rope in SwingRope.All) { if (rope != null) { found.Add(rope); } }
            foreach (PushableBlock pushable in Object.FindObjectsByType<PushableBlock>()) { if (pushable.isActiveAndEnabled) { found.Add(pushable); } }
            found.Sort((a, b) => StableId.Of(a.transform).CompareTo(StableId.Of(b.transform)));
            return found;
        }

        /// <summary>Saves every rope's and pushable object's state now (between steps).</summary>
        public static WorldStateSnapshot Save()
        {
            var snapshot = new WorldStateSnapshot();
            foreach (MonoBehaviour part in MovingParts())
            {
                Rigidbody body = part.GetComponent<Rigidbody>();
                snapshot.parts.Add((part, ObjectGraphSnapshot.Capture(part, typeof(MonoBehaviour)),
                    body != null ? body.position : part.transform.position, body != null ? body.rotation : part.transform.rotation));
            }
            return snapshot;
        }

        /// <summary>Puts each rope and pushable object back as it was when saved (one destroyed since is skipped).</summary>
        public void Restore()
        {
            foreach ((MonoBehaviour part, ObjectGraphSnapshot state, Vector3 position, Quaternion rotation) in parts)
            {
                if (part == null) { continue; }
                state.Restore();
                Rigidbody body = part.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.position = position;
                    body.rotation = rotation;
                }
                part.transform.SetPositionAndRotation(position, rotation);
            }
            Physics.SyncTransforms();
        }
    }
}

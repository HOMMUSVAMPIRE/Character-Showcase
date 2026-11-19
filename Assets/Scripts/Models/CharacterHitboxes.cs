using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>How a model's hitboxes meet what comes at the character (see CharacterHitboxes).</summary>
    public enum HitboxResponse
    {
        /// <summary>They only detect it: triggers, which what meets them passes through (and the world's queries ignore).</summary>
        Detect,
        /// <summary>They're solid: what meets them bounces off, and the character pushes physics objects with them.</summary>
        Collide,
    }

    /// <summary>
    /// A model's hitboxes: a capsule on each part of its body (see BoneHitbox), fitted to its mesh (Fit Hitboxes, which Prepare
    /// Character Models runs too), moving with its bones as it's animated, on the CharacterHitbox layer. They're
    /// the character's physical presence for what meets it (a thrown ball, a trigger it walks into), and raise HitStarted
    /// and HitEnded as something does. The movement never uses them: it moves the character's body, a shape of its own (see
    /// CharacterBody), and the CharacterHitbox layer is kept out of the movement's layers. On the model, so each model has
    /// its own, and a model swapped in brings its own (see CharacterModelSlot.Hitboxes).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Models/Character Hitboxes")]
    public sealed class CharacterHitboxes : MonoBehaviour
    {
        /// <summary>The layer hitboxes are on (see ProjectSettings' Tags and Layers).</summary>
        public const string LayerName = "CharacterHitbox";

        [Header("Response")]
        [Tooltip("How the hitboxes meet what comes at the character. Detect: they're triggers, reporting what passes into " +
                 "them (it passes through). Collide: they're solid, so what meets them bounces off, and the character pushes " +
                 "physics objects with its limbs")]
        [SerializeField] private HitboxResponse response = HitboxResponse.Detect;
        [Tooltip("Report contacts with what doesn't move (colliders without a Rigidbody: the level's floors and walls), " +
                 "which feet and hands brush all the time. Off: only what moves (has a Rigidbody) is reported")]
        [SerializeField] private bool reportStaticContacts;

        [Header("Fitting")]
        [Tooltip("The bones Fit Hitboxes gives a hitbox each, by name; the mesh weighted to any other bone goes to its nearest " +
                 "listed ancestor's (a hand's fingers to the hand's). Empty: a humanoid's body bones (its hips, spine, " +
                 "chest, neck and head, and each arm's and leg's three), or, for a model that isn't a humanoid, every bone " +
                 "carrying enough of its mesh (see Fit Hitboxes)")]
        [SerializeField] private List<string> hitboxBones = new();
        [Tooltip("The hitboxes, as Fit Hitboxes made them. Refit (Tools > Meridian Motion > Fit Hitboxes) rather than " +
                 "editing them by hand: it replaces them")]
        [SerializeField] private List<BoneHitbox> hitboxes = new();

        /// <summary>Something started meeting one of the hitboxes (passing into it, detecting; touching it, colliding).</summary>
        public event Action<BoneHitbox, Collider> HitStarted;

        /// <summary>Something stopped meeting one of the hitboxes.</summary>
        public event Action<BoneHitbox, Collider> HitEnded;

        /// <summary>The hitboxes.</summary>
        public IReadOnlyList<BoneHitbox> Hitboxes => hitboxes;

        /// <summary>The bones Fit Hitboxes gives a hitbox each, by name (empty: chosen for the model; see the field's tooltip).</summary>
        public IReadOnlyList<string> HitboxBones => hitboxBones;

        /// <summary>
        /// How the hitboxes meet what comes at the character (see HitboxResponse). Setting it changes them at once.
        /// </summary>
        public HitboxResponse Response
        {
            get => response;
            set
            {
                response = value;
                Apply();
            }
        }

        /// <summary>Whether contacts with what doesn't move (no Rigidbody) are reported.</summary>
        public bool ReportStaticContacts { get => reportStaticContacts; set => reportStaticContacts = value; }

        /// <summary>The CharacterHitbox layer's number: -1 if the project has no such layer.</summary>
        public static int Layer => LayerMask.NameToLayer(LayerName);

        // On the Rigidbody the hitboxes move with (the character's), passing on what touches them while they're solid.
        private HitboxContactRelay relay;

        private void Awake()
        {
            hitboxes.RemoveAll(h => h == null);
            Apply();
        }

        // A model is made under a staging object, then moved under the character: the relay goes on the Rigidbody it's
        // under once it's there.
        private void OnEnable() => Connect();
        private void OnTransformParentChanged() => Connect();

        private void Connect()
        {
            Rigidbody body = GetComponentInParent<Rigidbody>();
            if (body == null || (relay != null && relay.gameObject == body.gameObject)) { return; }
            if (!body.TryGetComponent(out relay)) { relay = body.gameObject.AddComponent<HitboxContactRelay>(); }
        }

        /// <summary>
        /// Takes the hitboxes Fit Hitboxes made (replacing any it had), and gives them the response and layer. For the
        /// fitting tool.
        /// </summary>
        public void SetHitboxes(IEnumerable<BoneHitbox> fitted)
        {
            hitboxes.Clear();
            hitboxes.AddRange(fitted);
            Apply();
        }

        /// <summary>Gives each hitbox its owner, the response (a trigger, or solid) and the CharacterHitbox layer.</summary>
        private void Apply()
        {
            int layer = Layer;
            foreach (BoneHitbox hitbox in hitboxes)
            {
                if (hitbox == null) { continue; }
                hitbox.Owner = this;
                hitbox.Collider.isTrigger = response == HitboxResponse.Detect;
                if (layer >= 0) { hitbox.gameObject.layer = layer; }
            }
        }

        /// <summary>Passes on what met a hitbox (see BoneHitbox), unless it's the character's own, or (unless asked for) doesn't move.</summary>
        internal void Report(BoneHitbox hitbox, Collider other, bool started)
        {
            if (other == null) { return; }
            Rigidbody body = other.attachedRigidbody;
            if (body != null && body == hitbox.Collider.attachedRigidbody) { return; }
            if (body == null && !reportStaticContacts) { return; }
            (started ? HitStarted : HitEnded)?.Invoke(hitbox, other);
        }
    }
}

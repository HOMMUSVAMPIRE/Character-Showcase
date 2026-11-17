using UnityEngine;

namespace Character
{
    /// <summary>
    /// A body (see BodyShape), as an asset: a character's (its MovementController's Body Definition), or a model's (its
    /// CharacterModelSource's, in the model's own units, which a character fitting its body to its model takes: see
    /// CharacterModelHost's Collider Fit). Prepare Character Models fits one to each model it prepares, from the model's
    /// meshes in their bind pose, unless Refit On Prepare is turned off for one shaped by hand.
    /// </summary>
    [CreateAssetMenu(menuName = "Character Showcase/Body Definition", fileName = "Body", order = 40)]
    public class BodyDefinition : ScriptableObject
    {
        [Tooltip("The body: its kind, its size and where it sits standing (relative to the feet, the character's origin), and " +
                 "how thick it is lying down")]
        [SerializeField] private BodyShape shape = BodyShape.Capsule(0.5f, 2f);
        [Tooltip("Let Prepare Character Models fit this body to its model again each time it runs. Turn off for a body " +
                 "shaped by hand, which it then leaves as it is")]
        [SerializeField] private bool refitOnPrepare = true;

        /// <summary>The body this defines.</summary>
        public BodyShape Shape { get => shape; set => shape = value; }

        /// <summary>Whether Prepare Character Models fits this body to its model each time it runs.</summary>
        public bool RefitOnPrepare { get => refitOnPrepare; set => refitOnPrepare = value; }

        /// <summary>A definition of the given body, not saved as an asset (destroy it when done).</summary>
        public static BodyDefinition Of(BodyShape shape)
        {
            var definition = CreateInstance<BodyDefinition>();
            definition.shape = shape;
            return definition;
        }
    }
}

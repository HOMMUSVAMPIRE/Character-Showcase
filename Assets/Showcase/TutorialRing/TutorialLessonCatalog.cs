using System;
using System.Collections.Generic;
using System.Reflection;
using MeridianMotion.Characters;
using UnityEngine;

namespace MeridianMotion.Showcase.TutorialRing
{
    /// <summary>
    /// One lesson of the Tutorial Ring: its id (what its Lesson Zone in the level names), its title, the prompt shown in
    /// its zone and an optional follow-up shown once it's done (the combo it leads to), and the movement event that says
    /// it's done (by the event's name in MovementEvents: "Jumped"), optionally only while the character's in a state
    /// ("Running"). A lesson with no event is done when the character leaves its zone. Prompts name controls by their
    /// actions in braces ("Press {Jump} to jump": see PromptText).
    /// </summary>
    [Serializable]
    public class TutorialLesson
    {
        [Tooltip("The lesson's id: what its Lesson Zone in the level names (L01-move, ...)")]
        public string id = "";
        [Tooltip("The lesson's title, for play logs")]
        public string title = "";
        [Tooltip("The prompt shown in the lesson's zone; actions in braces show their binding (\"Press {Jump} to jump\")")]
        [TextArea] public string prompt = "";
        [Tooltip("Shown once the lesson's done, until the character leaves its zone (the combo it leads to); empty: none")]
        [TextArea] public string followUp = "";
        [Tooltip("The movement event that says the lesson's done (its name in MovementEvents, \"Jumped\"); empty: done on leaving its zone")]
        public string clearsOn = "";
        [Tooltip("If set, the event only counts while the character's in this state (its name in CharacterState, \"Running\")")]
        public string clearsOnlyWhile = "";

        [NonSerialized] private bool resolved;
        [NonSerialized] private MovementEvents clearingEvent;
        [NonSerialized] private CharacterState clearingState;

        /// <summary>The event that says it's done (None: done on leaving its zone).</summary>
        public MovementEvents ClearingEvent { get { Resolve(); return clearingEvent; } }
        /// <summary>The state the event only counts in (None: any).</summary>
        public CharacterState ClearingState { get { Resolve(); return clearingState; } }

        /// <summary>Whether this event, raised in this state, says the lesson's done.</summary>
        public bool ClearedBy(MovementEvents raised, CharacterState state) =>
            !ClearingEvent.IsEmpty && raised.HasAny(ClearingEvent) && (ClearingState.IsEmpty || state.HasAny(ClearingState));

        private void Resolve()
        {
            if (resolved) { return; }
            resolved = true;
            clearingEvent = Named<MovementEvents>(clearsOn);
            clearingState = Named<CharacterState>(clearsOnlyWhile);
        }

        /// <summary>The flag of type T named so (a public static field of T), or T's empty value for none (with a warning if the name's unknown).</summary>
        public static T Named<T>(string name) where T : struct
        {
            if (string.IsNullOrEmpty(name)) { return default; }
            FieldInfo field = typeof(T).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field != null && field.FieldType == typeof(T)) { return (T)field.GetValue(null); }
            Debug.LogWarning($"[TutorialLesson] {typeof(T).Name} has no '{name}'.");
            return default;
        }
    }

    /// <summary>The Tutorial Ring's lessons, in the order they're taught.</summary>
    [CreateAssetMenu(fileName = "Tutorial Lessons", menuName = "Meridian Motion/Showcase/Tutorial Lesson Catalog")]
    public class TutorialLessonCatalog : ScriptableObject
    {
        [Tooltip("The lessons, in the order they're taught")]
        [SerializeField] private List<TutorialLesson> lessons = new();

        /// <summary>The lessons, in the order they're taught.</summary>
        public List<TutorialLesson> Lessons => lessons;

        /// <summary>The lesson with this id, or null.</summary>
        public TutorialLesson Find(string id)
        {
            foreach (TutorialLesson lesson in lessons) { if (lesson != null && lesson.id == id) { return lesson; } }
            return null;
        }
    }
}

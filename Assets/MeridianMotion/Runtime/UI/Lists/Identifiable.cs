using System;
using UnityEngine.Events;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A record a data list shows (UI_Framework_Blueprint, Generic data lists): anything with an ID, which the list finds,
    /// filters and keeps its selection by. A game's own records derive it (an item, a save slot, a character).
    /// </summary>
    [Serializable]
    public abstract class Identifiable
    {
        /// <summary>The record's ID, unique among the records a list shows.</summary>
        public string ID;
    }

    /// <summary>A record with a category, for a categorized list (DataDisplayList_Categorized), which heads each category's rows.</summary>
    [Serializable]
    public abstract class IdentifiableElement : Identifiable
    {
        /// <summary>The record's category, by name: the header its rows are shown under.</summary>
        public string elementType;
    }

    /// <summary>A UnityEvent passing a record (a list's selection, wired to a panel in the inspector).</summary>
    [Serializable]
    public class IdentifiableEvent<T> : UnityEvent<T> where T : Identifiable
    {
    }
}

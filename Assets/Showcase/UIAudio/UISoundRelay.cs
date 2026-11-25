using MeridianMotion.Showcase.Audio;
using MeridianMotion.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MeridianMotion.Showcase.UIAudio
{
    /// <summary>
    /// A control's sounds, added by UISounds as its window opens: hover as the pointer comes onto it (or navigation chooses
    /// it: not the selecting a click does), click as a button's clicked or submitted, denied as a greyed-out control's
    /// clicked, a toggle's on and off (when a person changes it, not code), a slider's moving, a tab group's change. Hooked once, however often its window opens.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class UISoundRelay : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler, ISelectHandler
    {
        private Selectable control;
        private Toggle toggle;
        private Slider slider;
        private TabGroup tabs;

        /// <summary>The UI Sounds it plays through (null: none, silent).</summary>
        public UISounds Owner { get; set; }

        private UISoundSet Set => Owner != null ? Owner.Sounds : null;

        private void Awake()
        {
            control = GetComponent<Selectable>();
            toggle = control as Toggle;
            slider = control as Slider;
            tabs = GetComponent<TabGroup>();
            if (toggle != null) { toggle.onValueChanged.AddListener(Toggled); }
            if (slider != null) { slider.onValueChanged.AddListener(Slid); }
            if (tabs != null) { tabs.SelectionChanged.AddListener(TabChanged); }
        }

        private void OnDestroy()
        {
            if (toggle != null) { toggle.onValueChanged.RemoveListener(Toggled); }
            if (slider != null) { slider.onValueChanged.RemoveListener(Slid); }
            if (tabs != null) { tabs.SelectionChanged.RemoveListener(TabChanged); }
        }

        private bool Interactable => control != null && control.IsInteractable();

        private void Play(SoundEvent sound)
        {
            if (Owner != null) { Owner.Play(sound); }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Set != null && Interactable) { Play(Set.Hover); }
        }

        public void OnSelect(BaseEventData eventData)
        {
            // A click selects what it's on, with the pointer's event: the pointer coming onto it already played its hover.
            if (eventData is PointerEventData) { return; }
            if (Set != null && Interactable) { Play(Set.Hover); }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (toggle != null) { Pressed(); }
            if (Set == null || control == null) { return; }
            if (!Interactable) { Play(Set.Denied); }
            else if (control is Button) { Play(Set.Click); }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (toggle != null) { Pressed(); }
            if (Set != null && Interactable && control is Button) { Play(Set.Click); }
        }

        // A toggle's sound is for a person changing it, not code setting it (a list showing what's chosen, a group turning
        // the others off): it plays only for a change in the frame it was clicked or submitted. The toggle may hear the
        // click before or after this does (its components' order), so a change waits for the click within the frame.
        private int pressedFrame = -1, changedFrame = -1;
        private bool changedTo;

        private void Pressed()
        {
            pressedFrame = Time.frameCount;
            if (changedFrame == pressedFrame) { PlayToggle(changedTo); changedFrame = -1; }
        }

        private void Toggled(bool on)
        {
            if (pressedFrame == Time.frameCount) { PlayToggle(on); return; }
            changedFrame = Time.frameCount;
            changedTo = on;
        }

        private void PlayToggle(bool on)
        {
            if (Set != null) { Play(on ? Set.ToggleOn : Set.ToggleOff); }
        }

        private void Slid(float value)
        {
            if (Set != null) { Play(Set.Slider); }
        }

        private void TabChanged()
        {
            if (Set != null) { Play(Set.Tab); }
        }
    }
}

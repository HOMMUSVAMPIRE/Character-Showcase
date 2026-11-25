using MeridianMotion.Showcase.Audio;
using UnityEngine;

namespace MeridianMotion.Showcase.UIAudio
{
    /// <summary>The UI's sounds (UISounds plays them): each empty is silent. Put them on the UI bus, 2D.</summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Audio/UI Sound Set", fileName = "UI Sound Set")]
    public class UISoundSet : ScriptableObject
    {
        [Header("Controls")]
        [Tooltip("The pointer coming onto a control, or navigation choosing one")]
        [SerializeField] private SoundEvent hover;
        [Tooltip("A button clicked or submitted")]
        [SerializeField] private SoundEvent click;
        [Tooltip("A greyed-out control clicked")]
        [SerializeField] private SoundEvent denied;
        [Tooltip("A toggle turned on")]
        [SerializeField] private SoundEvent toggleOn;
        [Tooltip("A toggle turned off")]
        [SerializeField] private SoundEvent toggleOff;
        [Tooltip("A slider moved (its Sound Event's Cooldown keeps a drag from playing every frame)")]
        [SerializeField] private SoundEvent slider;
        [Tooltip("A tab group's tab changed")]
        [SerializeField] private SoundEvent tab;

        [Header("Windows")]
        [Tooltip("A window opening (or shown again)")]
        [SerializeField] private SoundEvent windowOpen;
        [Tooltip("A window closed")]
        [SerializeField] private SoundEvent windowClose;
        [Tooltip("The Back key taken by a window or the pause menu")]
        [SerializeField] private SoundEvent back;
        [Tooltip("A notification shown")]
        [SerializeField] private SoundEvent notification;

        public SoundEvent Hover { get => hover; set => hover = value; }
        public SoundEvent Click { get => click; set => click = value; }
        public SoundEvent Denied { get => denied; set => denied = value; }
        public SoundEvent ToggleOn { get => toggleOn; set => toggleOn = value; }
        public SoundEvent ToggleOff { get => toggleOff; set => toggleOff = value; }
        public SoundEvent Slider { get => slider; set => slider = value; }
        public SoundEvent Tab { get => tab; set => tab = value; }
        public SoundEvent WindowOpen { get => windowOpen; set => windowOpen = value; }
        public SoundEvent WindowClose { get => windowClose; set => windowClose = value; }
        public SoundEvent Back { get => back; set => back = value; }
        public SoundEvent Notification { get => notification; set => notification = value; }
    }
}

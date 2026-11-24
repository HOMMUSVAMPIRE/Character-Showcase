using MeridianMotion.Showcase.TutorialRing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeridianMotion.Showcase.Tests.TutorialRing
{
    /// <summary>What the tutorial's tests share: a small catalog of lessons, and input actions with a keyboard and a gamepad scheme.</summary>
    internal static class TutorialTestLessons
    {
        public const string KeyboardScheme = "Keyboard&Mouse", GamepadScheme = "Gamepad";

        /// <summary>A catalog: Move (done on leaving), Jump (on Jumped), Sprint (Jumped while Running), and Crouch (on CrouchStarted, with a follow-up).</summary>
        public static TutorialLessonCatalog Catalog()
        {
            var catalog = ScriptableObject.CreateInstance<TutorialLessonCatalog>();
            catalog.Lessons.Add(new TutorialLesson { id = "move", prompt = "Move with {Move}." });
            catalog.Lessons.Add(new TutorialLesson { id = "jump", prompt = "Press {Jump} to jump.", clearsOn = "Jumped" });
            catalog.Lessons.Add(new TutorialLesson { id = "sprint", prompt = "Hold {Sprint}, then {Jump}.", clearsOn = "Jumped", clearsOnlyWhile = "Running" });
            catalog.Lessons.Add(new TutorialLesson { id = "crouch", prompt = "Press {Crouch} to crouch.", followUp = "Now slide.", clearsOn = "CrouchStarted" });
            return catalog;
        }

        /// <summary>Input actions with Crouch and Jump bound on a keyboard (C, Space) and a gamepad (east and south buttons), each in its scheme.</summary>
        public static InputActionAsset Actions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.AddControlScheme(KeyboardScheme).WithRequiredDevice("<Keyboard>");
            asset.AddControlScheme(GamepadScheme).WithRequiredDevice("<Gamepad>");
            InputActionMap map = asset.AddActionMap("Player");
            InputAction crouch = map.AddAction("Crouch");
            crouch.AddBinding("<Keyboard>/c", groups: KeyboardScheme);
            crouch.AddBinding("<Gamepad>/buttonEast", groups: GamepadScheme);
            InputAction jump = map.AddAction("Jump");
            jump.AddBinding("<Keyboard>/space", groups: KeyboardScheme);
            jump.AddBinding("<Gamepad>/buttonSouth", groups: GamepadScheme);
            return asset;
        }

        /// <summary>An action's binding as it's shown in a scheme.</summary>
        public static string Shown(InputActionAsset actions, string action, string scheme) =>
            actions.FindAction(action).GetBindingDisplayString(InputBinding.MaskByGroup(scheme));
    }
}

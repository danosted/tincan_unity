using UnityEngine;

namespace TinCan.Core.Domain
{
    public interface IInputService
    {
        bool IsActionPressed(string actionName);
        bool WasActionTriggered(string actionName);
        float GetAxis(string positiveAction, string negativeAction);
        Vector2 GetMouseDelta();

        /// <summary>
        /// Returns a 64-bit mask representing all currently pressed GameplayInputs.
        /// Configured via InputBindingConfig.
        /// </summary>
        ulong GetActiveInputMask();
    }

    public static class ActionNames
    {
        public const string MoveForward = "MoveForward";
        public const string MoveBackward = "MoveBackward";
        public const string MoveLeft = "MoveLeft";
        public const string MoveRight = "MoveRight";
        public const string ToggleControl = "ToggleControl";
        public const string Interact = "Interact";
        public const string Jump = "Jump";
        public const string Cancel = "Cancel";
        public const string Sprint = "Sprint";
        public const string AbilityPrimary = "AbilityPrimary";
        public const string AbilitySecondary = "AbilitySecondary";
    }
}

using UnityEngine;

namespace TinCan.Core.Domain
{
    public interface IInputService
    {
        bool IsActionPressed(string actionName);
        bool WasActionTriggered(string actionName);
        float GetAxis(string positiveAction, string negativeAction);
        Vector2 GetMouseDelta();
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
    }
}

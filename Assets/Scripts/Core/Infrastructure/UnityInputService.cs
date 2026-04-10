using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TinCan.Core.Domain;
using VContainer.Unity;

namespace TinCan.Core.Infrastructure
{
    public class UnityInputService : IInputService, IInitializable
    {
        private readonly Dictionary<string, Key> _keyMappings = new Dictionary<string, Key>();

        public void Initialize()
        {
            _keyMappings[ActionNames.MoveForward] = Key.W;
            _keyMappings[ActionNames.MoveBackward] = Key.S;
            _keyMappings[ActionNames.MoveLeft] = Key.A;
            _keyMappings[ActionNames.MoveRight] = Key.D;
            _keyMappings[ActionNames.ToggleControl] = Key.Tab;
            _keyMappings[ActionNames.Interact] = Key.E;
            _keyMappings[ActionNames.Jump] = Key.Space;
            _keyMappings[ActionNames.Cancel] = Key.Escape;
            _keyMappings[ActionNames.Sprint] = Key.LeftShift;
        }

        public bool IsActionPressed(string actionName)
        {
            if (_keyMappings.TryGetValue(actionName, out Key key))
            {
                return Keyboard.current != null && Keyboard.current[key].isPressed;
            }
            return false;
        }

        public bool WasActionTriggered(string actionName)
        {
            if (_keyMappings.TryGetValue(actionName, out Key key))
            {
                return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
            }
            return false;
        }

        public float GetAxis(string positiveAction, string negativeAction)
        {
            float val = 0;
            if (IsActionPressed(positiveAction)) val += 1f;
            if (IsActionPressed(negativeAction)) val -= 1f;
            return val;
        }

        public Vector2 GetMouseDelta()
        {
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        }
    }
}

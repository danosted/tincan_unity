using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace TinCan.Core
{
    /// <summary>
    /// Centralized manager for mapping game actions to physical inputs.
    /// This allows for dynamic remapping and decoupling input logic from gameplay scripts.
    /// </summary>
    public class InputMappingManager : MonoBehaviour
    {
        public static InputMappingManager Instance { get; private set; }

        // Action names used throughout the project
        public static class Actions
        {
            public const string MoveForward = "MoveForward";
            public const string MoveBackward = "MoveBackward";
            public const string MoveLeft = "MoveLeft";
            public const string MoveRight = "MoveRight";
            public const string ToggleControl = "ToggleControl";
            public const string Interact = "Interact";
            public const string Jump = "Jump";
            public const string Cancel = "Cancel";
        }

        private Dictionary<string, Key> _keyMappings = new Dictionary<string, Key>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeDefaults();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeDefaults()
        {
            _keyMappings[Actions.MoveForward] = Key.W;
            _keyMappings[Actions.MoveBackward] = Key.S;
            _keyMappings[Actions.MoveLeft] = Key.A;
            _keyMappings[Actions.MoveRight] = Key.D;
            _keyMappings[Actions.ToggleControl] = Key.Tab;
            _keyMappings[Actions.Interact] = Key.E;
            _keyMappings[Actions.Jump] = Key.Space;
            _keyMappings[Actions.Cancel] = Key.Escape;
        }

        /// <summary>
        /// Checks if an action is currently held down.
        /// </summary>
        public bool IsActionPressed(string actionName)
        {
            if (_keyMappings.TryGetValue(actionName, out Key key))
            {
                return Keyboard.current != null && Keyboard.current[key].isPressed;
            }
            return false;
        }

        /// <summary>
        /// Checks if an action was triggered this frame.
        /// </summary>
        public bool WasActionTriggered(string actionName)
        {
            if (_keyMappings.TryGetValue(actionName, out Key key))
            {
                return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
            }
            return false;
        }

        /// <summary>
        /// Returns a value between -1 and 1 based on two actions (e.g., Forward and Backward).
        /// </summary>
        public float GetAxis(string positiveAction, string negativeAction)
        {
            float val = 0;
            if (IsActionPressed(positiveAction)) val += 1f;
            if (IsActionPressed(negativeAction)) val -= 1f;
            return val;
        }

        /// <summary>
        /// Gets the current mouse delta for look/aiming.
        /// </summary>
        public Vector2 GetMouseDelta()
        {
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        }

        /// <summary>
        /// Dynamically remap an action to a new key.
        /// </summary>
        public void RemapAction(string actionName, Key newKey)
        {
            if (_keyMappings.ContainsKey(actionName))
            {
                _keyMappings[actionName] = newKey;
                Debug.Log($"InputMappingManager: Action '{actionName}' remapped to {newKey}");
            }
        }
    }
}

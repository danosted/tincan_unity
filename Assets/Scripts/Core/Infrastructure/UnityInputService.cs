using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using VContainer.Unity;

namespace TinCan.Core.Infrastructure
{
    public class UnityInputService : IInputService, IInitializable
    {
        private readonly Dictionary<string, ButtonControl> _keyMappings = new Dictionary<string, ButtonControl>();
        private readonly InputBindingConfig _bindingConfig;

        public UnityInputService(InputBindingConfig bindingConfig)
        {
            _bindingConfig = bindingConfig;
        }

        public void Initialize()
        {
            if (Keyboard.current != null)
            {
                _keyMappings[ActionNames.MoveForward] = Keyboard.current.wKey;
                _keyMappings[ActionNames.MoveBackward] = Keyboard.current.sKey;
                _keyMappings[ActionNames.MoveLeft] = Keyboard.current.aKey;
                _keyMappings[ActionNames.MoveRight] = Keyboard.current.dKey;
                _keyMappings[ActionNames.ToggleControl] = Keyboard.current.tabKey;
                _keyMappings[ActionNames.Interact] = Keyboard.current.eKey;
                _keyMappings[ActionNames.Jump] = Keyboard.current.spaceKey;
                _keyMappings[ActionNames.Cancel] = Keyboard.current.escapeKey;
                _keyMappings[ActionNames.Sprint] = Keyboard.current.leftShiftKey;
            }

            if (Mouse.current != null)
            {
                _keyMappings[ActionNames.AbilityPrimary] = Mouse.current.leftButton;
                _keyMappings[ActionNames.AbilitySecondary] = Mouse.current.rightButton;
            }

            if (_bindingConfig != null && _bindingConfig.Bindings != null)
            {
                for (int i = 0; i < _bindingConfig.Bindings.Count && i < 64; i++)
                {
                    if (_bindingConfig.Bindings[i].InputType != null)
                    {
                        _bindingConfig.Bindings[i].InputType.BitIndex = i;
                    }
                }
            }
            else
            {
                Debug.LogWarning("[UnityInputService] InputBindingConfig was not injected or is empty.");
            }
        }

        public bool IsActionPressed(string actionName)
        {
            if (_keyMappings.TryGetValue(actionName, out var control))
            {
                return control != null && control.isPressed;
            }
            return false;
        }

        public bool WasActionTriggered(string actionName)
        {
            if (_keyMappings.TryGetValue(actionName, out var control))
            {
                return control != null && control.wasPressedThisFrame;
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

        public ulong GetActiveInputMask()
        {
            ulong mask = 0;

            if (_bindingConfig == null || _bindingConfig.Bindings == null)
            {
                Debug.LogWarning("[UnityInputService] InputBindingConfig is not set. Returning empty input mask.");
                return mask;
            }

            foreach (var binding in _bindingConfig.Bindings)
            {
                if (binding.InputType != null && binding.InputType.BitIndex >= 0)
                {
                    if (IsActionPressed(binding.UnityActionName))
                    {
                        mask |= (1UL << binding.InputType.BitIndex);
                    }
                }
            }

            return mask;
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Core.Input
{
    /// <summary>
    /// Infrastructure: the only reader of Unity's Input System. It plays a private copy of the actions asset (so binding
    /// overrides never touch the asset on disk), maps each <see cref="InputActionId"/> to its action by id, merges
    /// <see cref="ScriptedInput"/>, and answers nothing for an action the live contexts have not turned on.
    /// </summary>
    public sealed class InputSystemReader : IInputReader, IInputActionSwitch, IInitializable, IDisposable
    {
        private readonly InputConfig _config;
        private readonly ScriptedInput _scripted;
        private readonly Dictionary<InputActionId, InputAction> _actions = new();
        private readonly HashSet<InputActionId> _enabled = new();

        public InputSystemReader(InputConfig config, ScriptedInput scripted)
        {
            _config = config;
            _scripted = scripted;
        }

        /// <summary>The playing copy of the actions asset (rebinding works on this one).</summary>
        public InputActionAsset? Asset { get; private set; }

        public void Initialize()
        {
            if (Asset != null) return;
            if (_config.Actions == null)
            {
                Debug.LogError("[Input] InputConfig has no actions asset; nothing can be pressed.");
                return;
            }

            Asset = Object.Instantiate(_config.Actions);
            Asset.name = _config.Actions.name;
            foreach (var id in _config.ActionIds)
            {
                if (id == null) continue;
                var action = Guid.TryParse(id.ActionId, out var guid) ? Asset.FindAction(guid) : null;
                if (action == null)
                {
                    Debug.LogError($"[Input] {id.name} points at no action in {Asset.name} ({id.Path}). Rebuild the input assets.");
                    continue;
                }
                _actions[id] = action;
            }

            for (int i = 0; i < _config.GameplayInputs.Count && i < 64; i++)
            {
                if (_config.GameplayInputs[i] != null) _config.GameplayInputs[i].BitIndex = i;
            }

            ApplyEnabled();
        }

        /// <summary>The Unity action behind an id (for rebinding and the Input Map report).</summary>
        public InputAction? Find(InputActionId? id) => id != null && _actions.TryGetValue(id, out var action) ? action : null;

        public void SetEnabled(IReadOnlyCollection<InputActionId> enabled)
        {
            _enabled.Clear();
            foreach (var id in enabled) _enabled.Add(id);
            ApplyEnabled();
        }

        public bool IsPressed(InputActionId? action)
        {
            if (!Listens(action)) return false;
            return _scripted.IsPressed(action!) || (Find(action) is { } unity && unity.IsPressed());
        }

        public bool WasPressedThisFrame(InputActionId? action)
        {
            if (!Listens(action)) return false;
            return _scripted.WasTriggered(action!) || (Find(action) is { } unity && unity.WasPressedThisFrame());
        }

        public float ReadAxis(InputActionId? action)
        {
            if (!Listens(action)) return 0f;
            float device = Find(action) is { } unity ? unity.ReadValue<float>() : 0f;
            return Mathf.Clamp(device + _scripted.Value(action!).x, -1f, 1f);
        }

        public Vector2 ReadVector2(InputActionId? action)
        {
            if (!Listens(action)) return Vector2.zero;
            var device = Find(action) is { } unity ? unity.ReadValue<Vector2>() : Vector2.zero;
            return device + _scripted.Value(action!);
        }

        public ulong GameplayInputMask()
        {
            ulong mask = 0;
            foreach (var input in _config.GameplayInputs)
            {
                if (input == null || input.BitIndex < 0) continue;
                foreach (var action in input.Actions)
                {
                    if (!IsPressed(action)) continue;
                    mask |= 1UL << input.BitIndex;
                    break;
                }
            }
            return mask;
        }

        public void Dispose()
        {
            if (Asset == null) return;
            Asset.Disable();
            if (Application.isPlaying) Object.Destroy(Asset);
            else Object.DestroyImmediate(Asset);
            Asset = null;
        }

        private bool Listens(InputActionId? action) => action != null && _enabled.Contains(action);

        private void ApplyEnabled()
        {
            foreach (var (id, action) in _actions)
            {
                bool on = _enabled.Contains(id);
                if (on && !action.enabled) action.Enable();
                else if (!on && action.enabled) action.Disable();
            }
        }
    }
}

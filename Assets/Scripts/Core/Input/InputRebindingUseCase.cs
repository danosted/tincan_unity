#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace TinCan.Core.Input
{
    /// <summary>
    /// Application Layer: the player's key bindings. Loads the saved overrides at start, lists every rebindable key
    /// binding (composite parts on their own), runs Unity's interactive rebinding while the Rebinding context silences
    /// everything else, refuses a key that an action live at the same time already uses
    /// (<see cref="InputBindingConflictProcessor"/>), and saves each accepted change.
    /// </summary>
    public sealed class InputRebindingUseCase : IInputBindings, IInitializable, IDisposable
    {
        private const string KeyboardMouse = "Keyboard&Mouse";

        private readonly InputConfig _config;
        private readonly InputSystemReader _reader;
        private readonly InputContextSet _contexts;
        private readonly InputRebindState _state;
        private readonly IInputBindingStore _store;
        private readonly InputBindingConflictProcessor _conflicts;
        private readonly List<InputBindingSlot> _slots = new();
        private InputActionRebindingExtensions.RebindingOperation? _operation;
        private string? _previousOverride;

        public InputRebindingUseCase(InputConfig config, InputSystemReader reader, InputContextSet contexts, InputRebindState state,
            IInputBindingStore store, InputBindingConflictProcessor conflicts)
        {
            _config = config;
            _reader = reader;
            _contexts = contexts;
            _state = state;
            _store = store;
            _conflicts = conflicts;
        }

        public IReadOnlyList<InputBindingSlot> Slots => _slots;
        public InputBindingSlot? Rebinding { get; private set; }
        public string? LastMessage { get; private set; }
        public event Action? Changed;

        public void Initialize()
        {
            _reader.Initialize();
            if (_reader.Asset == null) return;

            if (_store.Load() is { } saved) _reader.Asset.LoadBindingOverridesFromJson(saved);
            BuildSlots();
        }

        public string Describe(InputBindingSlot slot) =>
            _reader.Find(slot.Action) is { } action ? action.GetBindingDisplayString(slot.BindingIndex) : "?";

        public void StartRebind(InputBindingSlot slot)
        {
            CancelRebind();
            if (_reader.Find(slot.Action) is not { } action) return;

            // Unity rebinds a disabled action only; the Rebinding context keeps every action off until it is done.
            action.Disable();
            _previousOverride = action.bindings[slot.BindingIndex].overridePath;
            Rebinding = slot;
            LastMessage = null;
            _state.IsRebinding = true;

            _operation = action.PerformInteractiveRebinding(slot.BindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => Finish(accepted: true))
                .OnCancel(_ => Finish(accepted: false))
                .Start();
            Changed?.Invoke();
        }

        public void CancelRebind()
        {
            if (_operation == null) return;
            _operation.Cancel();
        }

        /// <summary>
        /// Binds a slot to a control path as if the player had pressed it (also the seam tests use). False, with
        /// <see cref="LastMessage"/> set, when an action live at the same time already uses it; the old key stays.
        /// </summary>
        public bool TryBind(InputBindingSlot slot, string path)
        {
            if (_reader.Find(slot.Action) is not { } action) return false;
            _previousOverride = action.bindings[slot.BindingIndex].overridePath;
            action.ApplyBindingOverride(slot.BindingIndex, path);
            return Accept(slot);
        }

        public void ResetAll()
        {
            CancelRebind();
            _reader.Asset?.RemoveAllBindingOverrides();
            LastMessage = null;
            Save();
            Changed?.Invoke();
        }

        public void Dispose()
        {
            _operation?.Dispose();
            _operation = null;
            _state.IsRebinding = false;
        }

        private void Finish(bool accepted)
        {
            var slot = Rebinding;
            _operation?.Dispose();
            _operation = null;
            Rebinding = null;
            _state.IsRebinding = false;

            if (accepted && slot is { } done) Accept(done);
            else Changed?.Invoke();
        }

        private bool Accept(InputBindingSlot slot)
        {
            var action = _reader.Find(slot.Action)!;
            string path = action.bindings[slot.BindingIndex].effectivePath;

            foreach (var other in _slots)
            {
                if (other.SameAs(slot) || _reader.Find(other.Action) is not { } otherAction) continue;
                if (otherAction.bindings[other.BindingIndex].effectivePath != path) continue;
                if (!ReferenceEquals(other.Action, slot.Action) && _conflicts.CanShareAKey(slot.Action, other.Action, _contexts.All)) continue;

                Restore(action, slot.BindingIndex);
                LastMessage = $"{Describe(other)} is already {other.Group} {other.Label}.";
                Changed?.Invoke();
                return false;
            }

            LastMessage = null;
            Save();
            Changed?.Invoke();
            return true;
        }

        private void Restore(InputAction action, int bindingIndex)
        {
            if (string.IsNullOrEmpty(_previousOverride)) action.RemoveBindingOverride(bindingIndex);
            else action.ApplyBindingOverride(bindingIndex, _previousOverride);
        }

        private void Save()
        {
            if (_reader.Asset != null) _store.Save(_reader.Asset.SaveBindingOverridesAsJson());
        }

        private void BuildSlots()
        {
            _slots.Clear();
            foreach (var id in _config.ActionIds)
            {
                if (id == null || !id.Rebindable || _reader.Find(id) is not { } action) continue;

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.isComposite) continue;
                    if (!string.IsNullOrEmpty(binding.groups) && !binding.groups.Contains(KeyboardMouse)) continue;

                    string label = binding.isPartOfComposite ? $"{id.DisplayName} {PartName(binding.name)}" : id.DisplayName;
                    _slots.Add(new InputBindingSlot(id, i, label, action.actionMap.name));
                }
            }
        }

        private static string PartName(string part) => part switch
        {
            "up" => "Forward",
            "down" => "Back",
            "left" => "Left",
            "right" => "Right",
            "negative" => "-",
            "positive" => "+",
            _ => part,
        };
    }
}
